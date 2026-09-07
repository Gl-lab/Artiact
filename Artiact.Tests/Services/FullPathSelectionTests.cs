using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class FullPathSelectionTests
{
    private sealed class World(Clock? clock = null) : IStrategyObserver
    {
        public int Xp, Level = 1, Weapon = 1, Catalog;
        public bool LoseReply;
        public StrategyObservation State => new(JsonSerializer.SerializeToElement(new { name = "hero", mining_level = Level, mining_xp = Xp,
            mining_max_xp = 100, attack_fire = Weapon, cooldown_expiration = "2000-01-01T00:00:00Z", inventory = Array.Empty<object>() }),
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty.Add("version", [JsonSerializer.SerializeToElement(Catalog)]), "p");
        public Task<StrategyObservation> ObserveAsync(CancellationToken token) { clock?.Advance(0.25); return Task.FromResult(State); }
        public void Act(int xp) { Xp += xp; clock?.Advance(0.5); }
    }
    private sealed class Option(World world, string id, int seconds, int xp, decimal preparation = 0) : IProgressionStrategy
    {
        public StrategyCandidate Evaluate(StrategyObservation state) => new(id, "skill", 1, 1, 0, 0, null, world.Xp >= 40,
            new("Gather:mining", state.Fingerprint, true, after => after.Character.GetProperty("mining_xp").GetInt32() == state.Character.GetProperty("mining_xp").GetInt32() + xp,
                _ => { world.Act(xp); if (world.LoseReply) throw new HttpRequestException("Lost reply"); return Task.FromResult(new StrategyReply(world.State, seconds)); }),
            Path: new(new("xp", "mining", 2), "Gather:mining", 40 - world.Xp, 10, 1, preparation, 0, 0, ImmutableDictionary<string, int>.Empty, "Synthetic declared estimate"));
    }
    private sealed class Delay(Clock? clock = null) : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) { clock?.Advance(seconds); return Task.CompletedTask; } }
    private sealed class Clock : TimeProvider
    {
        private long _ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _ticks;
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero).AddTicks(_ticks);
        public void Advance(double seconds) => _ticks += (long)(seconds * TimeSpan.TicksPerSecond);
    }
    private sealed class Store : IRunCheckpointStore
    {
        public RunCheckpoint? Saved;
        public RunCheckpoint? Load() => Saved;
        public void Save(RunCheckpoint checkpoint) => Saved = checkpoint;
    }
    private sealed class InterruptedDelay(Store store) : IMiningCooldownDelay
    {
        public Task WaitAsync(int seconds, CancellationToken token)
        {
            Assert.NotNull(Assert.Single(store.Saved!.Journal).Facts);
            Assert.Null(store.Saved.Journal[0].Facts!.WaitSeconds);
            throw new IOException("Interrupted cooldown wait");
        }
    }
    [Fact]
    public async Task FactsAreDurableBeforeWaitAndInterruptedWaitIsNotReportedComplete()
    {
        var world = new World(); var saved = new Store();
        var run = new StrategySession(world, [new Option(world, "a", 3, 10)], new InterruptedDelay(saved), checkpoints: saved, identity: "wait", selection: new(FullPaths: true));
        Assert.Equal("CooldownFailed", (await run.TickAsync()).Reason);
        var report = RunPerformance.From(saved.Saved!);
        Assert.Equal(1, report.VerifiedFactCount); Assert.Equal(0, report.CompletedWaitCount); Assert.Equal(10, report.Progress["mining"].KnownXp);
    }
    [Theory]
    [InlineData("equipment")]
    [InlineData("level")]
    [InlineData("catalog")]
    public async Task ChangedContextDoesNotReuseEarlierTiming(string change)
    {
        var world = new World(); var store = new Store();
        StrategySession Run() => new(world, [new Option(world, "a", 3, 10)], new Delay(), checkpoints: store, identity: "context", selection: new(FullPaths: true));
        await Run().TickAsync();
        if (change == "equipment") world.Weapon = 2;
        else if (change == "level") world.Level = 2;
        else world.Catalog = 1;
        Assert.Equal(0, Assert.Single((await Run().TickAsync()).Candidates).Samples);
    }
    [Fact]
    public async Task LostReplyReconciliationDoesNotInventXpOrTimingMeasurements()
    {
        var world = new World { LoseReply = true }; var store = new Store();
        StrategySession Run() => new(world, [new Option(world, "a", 3, 10)], new Delay(), checkpoints: store, identity: "loss", selection: new(FullPaths: true));
        Assert.Equal(StrategyStatus.UnknownOutcome, (await Run().TickAsync()).Status);
        world.LoseReply = false;
        Assert.Equal(StrategyStatus.Reconciled, (await Run().TickAsync()).Status);
        Assert.Empty(store.Saved!.Measurements!); Assert.Null(Assert.Single(store.Saved.Journal).Facts);
        Assert.Equal(1, store.Saved.Attempts);
    }
    [Theory]
    [InlineData(12, 3, 10, 21, 4, 1)]
    [InlineData(2, 2, 10, 8, 4, 0)]
    [InlineData(3, 20, 10, 29, 4, 2)]
    [InlineData(12, 3, 1, 24, 5, 1)]
    public async Task FixedAndAdaptiveComparisonsIncludeWinTieLossAndUnequalXp(int firstSeconds, int secondSeconds, int firstXp, int adaptiveSeconds, int adaptiveActions, int switches)
    {
        async Task<(int Actions, long Cooldown, double Active, int Switches, int Xp)> Execute(bool adaptive)
        {
            var clock = new Clock(); var world = new World(clock); var saved = new Store();
            StrategyDecision? final = null;
            for (int i = 0; i < 60; i++)
            {
                var run = new StrategySession(world, [new Option(world, "a", firstSeconds, firstXp), new Option(world, "b", secondSeconds, 10)], new Delay(clock),
                    time: clock, checkpoints: saved, identity: "compare", selection: adaptive ? new(FullPaths: true) : null);
                final = await run.TickAsync();
                if (final.Status != StrategyStatus.Selected) break;
            }
            Assert.Equal(StrategyStatus.Completed, final!.Status);
            var journal = saved.Saved!.Journal;
            Assert.All(journal, x => { Assert.Equal(0.5, x.Facts!.ObservationSeconds); Assert.Equal(0.5, x.Facts.DispatchSeconds); Assert.NotNull(x.Facts.WaitSeconds); });
            Assert.Equal(world.Xp, journal.Sum(x => x.Facts!.Skills["mining"].Xp));
            Assert.All(journal, x => Assert.Empty(x.Facts!.Inputs));
            int changes = journal.Zip(journal.Skip(1)).Count(x => x.First.Candidate != x.Second.Candidate);
            var report = RunPerformance.From(saved.Saved);
            Assert.Equal(changes, report.DispatchedCandidateSwitches); Assert.Equal(final.Attempts, report.CompletedWaitCount);
            Assert.Equal(world.Xp, report.Progress["mining"].KnownXp); Assert.Equal(0, report.Progress["mining"].UnknownXpTransitions);
            Assert.Empty(report.ConsumedInputs);
            return (final.Attempts, final.CooldownSeconds, journal.Sum(x => x.Facts!.ObservationSeconds + x.Facts.DispatchSeconds + x.Facts.WaitSeconds!.Value), changes, world.Xp);
        }
        var fixedRun = await Execute(false); var adaptiveRun = await Execute(true);
        Assert.Equal(40 / firstXp, fixedRun.Actions); Assert.Equal(firstSeconds * fixedRun.Actions, fixedRun.Cooldown);
        Assert.Equal(adaptiveActions, adaptiveRun.Actions); Assert.Equal(adaptiveSeconds, adaptiveRun.Cooldown); Assert.Equal(switches, adaptiveRun.Switches);
        Assert.Equal(adaptiveSeconds + adaptiveActions, adaptiveRun.Active); Assert.Equal(adaptiveRun, await Execute(true));
    }
    [Fact]
    public async Task FullRouteRetainsPreparationBeyondFastCurrentAction()
    {
        var world = new World();
        var run = new StrategySession(world, [new Option(world, "a", 1, 10, 100), new Option(world, "b", 2, 10)], new Delay(), selection: new(FullPaths: true));
        Assert.Equal("b", (await run.TickAsync()).Candidate);
    }
    [Fact]
    public async Task LearnedFastWorkCannotEraseRemainingPreparation()
    {
        var world = new World();
        var run = new StrategySession(world, [new Option(world, "a", 1, 10, 100)], new Delay(), selection: new(FullPaths: true));
        await run.TickAsync();
        var candidate = Assert.Single((await run.TickAsync()).Candidates);
        Assert.Equal(1, candidate.Samples); Assert.Equal(203, candidate.ActionSeconds);
    }
}
