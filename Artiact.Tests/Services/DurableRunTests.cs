using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services;
using Artiact.Services.Strategy;
using Xunit;

namespace Artiact.Tests.Services;

public class DurableRunTests
{
    private sealed class Store : IRunCheckpointStore
    {
        public RunCheckpoint? Saved;
        public Func<RunCheckpoint, bool>? Fail;
        public RunCheckpoint? Load() => Saved;
        public void Save(RunCheckpoint checkpoint)
        {
            if (Fail?.Invoke(checkpoint) == true) throw new IOException("Injected persistence failure");
            Saved = checkpoint;
        }
    }
    private sealed class World : IStrategyObserver, IProgressionStrategy
    {
        public int Xp, Actions;
        public bool Lose;
        public StrategyObservation State => new(JsonSerializer.SerializeToElement(new
        { name = "hero", xp = Xp, cooldown_expiration = "2000-01-01T00:00:00Z" }),
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "policy");
        public Task<StrategyObservation> ObserveAsync(CancellationToken token) => Task.FromResult(State);
        public StrategyCandidate Evaluate(StrategyObservation state) => new("skill", "skill", 1, 1, 0, 0, null, false,
            new("Gather", state.Fingerprint, true,
                after => after.Character.GetProperty("xp").GetInt32() == state.Character.GetProperty("xp").GetInt32() + 1,
                _ => { Actions++; Xp++; if (Lose) throw new IOException("Lost reply"); return Task.FromResult(new StrategyReply(State, 0)); }));
    }
    private sealed class Delay : IMiningCooldownDelay
    { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
    private static StrategySession Run(World world, Store store, StrategyLimits? limits = null) =>
        new(world, [world], new Delay(), limits, checkpoints: store, identity: "run1");

    [Fact]
    public async Task FailedIntentWritePreventsPost()
    {
        var world = new World(); var store = new Store { Fail = c => c.PendingCommand is not null };
        Assert.Equal(StrategyStatus.Blocked, (await Run(world, store).TickAsync()).Status);
        Assert.Equal(0, world.Actions);
    }
    [Fact]
    public async Task LostReplyAfterRestartOnlyReconciles()
    {
        var world = new World { Lose = true }; var store = new Store();
        await Run(world, store).TickAsync();
        Assert.Equal(StrategyStatus.Reconciled, (await Run(world, store).TickAsync()).Status);
        Assert.Equal(1, world.Actions);
    }
    [Fact]
    public async Task RestartAfterReplyBeforeSaveOnlyReconciles()
    {
        var world = new World(); var store = new Store { Fail = c => c.Attempts == 1 && c.PendingCommand is null };
        var failed = Run(world, store);
        await failed.TickAsync(); store.Fail = null;
        await failed.TickAsync(); // A stopped executor cannot overwrite the durable pending intent.
        Assert.Equal(StrategyStatus.Reconciled, (await Run(world, store).TickAsync()).Status);
        Assert.Equal(1, world.Actions);
    }
    [Fact]
    public async Task ActionBudgetSurvivesRestart()
    {
        var world = new World(); var store = new Store(); var limits = new StrategyLimits(Actions: 1);
        await Run(world, store, limits).TickAsync();
        Assert.Equal(StrategyStatus.Blocked, (await Run(world, store, limits).TickAsync()).Status);
        Assert.Equal(1, world.Actions);
    }

    [Fact]
    public async Task PersistedIntentBeforePostWithUnchangedStateStopsAmbiguously()
    {
        var world = new World { Lose = true }; var store = new Store();
        await Run(world, store).TickAsync();
        world.Xp = 0; world.Actions = 0; // Durable intent is indistinguishable from a crash before POST.
        var resumed = Run(world, store);
        Assert.Equal(StrategyStatus.UnknownOutcome, (await resumed.TickAsync()).Status);
        Assert.Equal(StrategyStatus.UnknownOutcome, (await resumed.TickAsync()).Status);
        Assert.Equal(0, world.Actions);
    }

    [Fact]
    public async Task SavedDeadlineIncludesDowntime()
    {
        var world = new World(); var store = new Store();
        await Run(world, store).TickAsync();
        store.Saved = store.Saved! with { Started = DateTimeOffset.UtcNow.AddHours(-2) };
        Assert.Equal("BudgetExhausted", (await Run(world, store).TickAsync()).Reason);
        Assert.Equal(1, world.Actions);
    }

    [Fact]
    public async Task CancellationIsPersistedAndCannotResumeActions()
    {
        var world = new World(); var store = new Store();
        Assert.Equal(StrategyStatus.Cancelled, (await Run(world, store).TickAsync(new CancellationToken(true))).Status);
        Assert.Equal(StrategyStatus.Cancelled, (await Run(world, store).TickAsync()).Status);
        Assert.Equal(0, world.Actions);
    }

    [Fact]
    public async Task FileLeaseExcludesSecondOwnerAndCheckpointRoundTrips()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-run-" + Guid.NewGuid().ToString("N"));
        try
        {
            var world = new World { Lose = true };
            using (var store = new FileRunCheckpointStore(directory, "hero"))
            {
                Assert.Throws<IOException>(() => new FileRunCheckpointStore(directory, "hero"));
                Assert.Throws<IOException>(() => { using var other = new FileRunCheckpointStore(directory, "HERO"); });
                await new StrategySession(world, [world], new Delay(), checkpoints: store, identity: "run1").TickAsync();
            }
            using (var store = new FileRunCheckpointStore(directory, "hero"))
            {
                var resumed = new StrategySession(world, [world], new Delay(), checkpoints: store, identity: "run1");
                Assert.Equal(StrategyStatus.Reconciled, (await resumed.TickAsync()).Status);
                Assert.Equal(1, world.Actions);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task CorruptCheckpointNeverDispatches()
    {
        var world = new World(); var store = new Store();
        await Run(world, store).TickAsync();
        store.Saved = store.Saved! with { Identity = "different-policy" };
        Assert.Equal("CheckpointUnavailableOrInvalid", (await Run(world, store).TickAsync()).Reason);
        Assert.Equal(1, world.Actions);
    }
}
