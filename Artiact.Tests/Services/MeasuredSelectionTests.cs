using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services;
using Artiact.Services.Strategy;
using Xunit;

namespace Artiact.Tests.Services;

public class MeasuredSelectionTests
{
    private sealed class World : IStrategyObserver
    {
        public int Progress;
        public StrategyObservation State => new(JsonSerializer.SerializeToElement(new { name = "hero", progress = Progress, cooldown_expiration = "2000-01-01T00:00:00Z" }),
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "p");
        public Task<StrategyObservation> ObserveAsync(CancellationToken token) => Task.FromResult(State);
    }
    private sealed class Option(World world, string id, int duration) : IProgressionStrategy
    {
        public StrategyCandidate Evaluate(StrategyObservation state) => new(id, "skill", 1, 1, 0, 0, null, world.Progress >= 4,
            new("Gather", state.Fingerprint, true, after => after.Character.GetProperty("progress").GetInt32() == state.Character.GetProperty("progress").GetInt32() + 1,
                _ => { world.Progress++; return Task.FromResult(new StrategyReply(world.State, duration)); }));
    }
    private sealed class Delay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
    private sealed class Store : IRunCheckpointStore
    {
        private RunCheckpoint? _saved;
        public RunCheckpoint? Load() => _saved;
        public void Save(RunCheckpoint checkpoint) => _saved = checkpoint;
    }
    [Theory]
    [InlineData(1.1, "b")]
    [InlineData(2, "a")]
    public async Task RestartRetainsMeasurementsAndSwitchingThreshold(double ratio, string expected)
    {
        var world = new World(); var store = new Store();
        StrategySession Run() => new(world, [new Option(world, "a", 3), new Option(world, "b", 2)], new Delay(),
            checkpoints: store, identity: "same", selection: new(SwitchRatio: (decimal)ratio));
        Assert.Equal("a", (await Run().TickAsync()).Candidate);
        var result = await Run().TickAsync();
        Assert.Equal(expected, result.Candidate);
        Assert.Equal(1, result.Candidates.Single(x => x.Id == "a").Samples);
        Assert.Equal("ObservedWithAssumedPrerequisites", result.Candidates.Single(x => x.Id == "a").EstimateSource);
    }
    [Theory]
    [InlineData(12, 3, 21)]
    [InlineData(9, 2, 15)]
    [InlineData(20, 5, 35)]
    public async Task MeasuredPolicyCompletesSameGoalWithFewerVirtualSecondsThanFixed(int slow, int fast, int expected)
    {
        async Task<StrategyDecision> Execute(bool measured)
        {
            var world = new World();
            var run = new StrategySession(world, [new Option(world, "a", slow), new Option(world, "b", fast)], new Delay(), selection: measured ? new() : null);
            StrategyDecision? final = null;
            for (int i = 0; i < 5; i++) final = await run.TickAsync();
            Assert.Equal(StrategyStatus.Completed, final!.Status); Assert.Equal(4, final.Attempts); Assert.Equal(4, world.Progress);
            return final;
        }
        Assert.Equal(slow * 4, (await Execute(false)).CooldownSeconds);
        Assert.Equal(expected, (await Execute(true)).CooldownSeconds);
        Assert.Equal(expected, (await Execute(true)).CooldownSeconds);
    }
}
