using System.Diagnostics;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Artiact.Tests.Services;

public class MiningRunTests
{
    [Theory]
    [InlineData(-1, 0, 10, GoalDecisionReason.InvalidCharacterSnapshot)]
    [InlineData(3, -1, 0, GoalDecisionReason.MiningTargetReached)]
    [InlineData(1, -1, 10, GoalDecisionReason.InvalidMiningProgress)]
    [InlineData(1, 0, 0, GoalDecisionReason.InvalidMiningProgress)]
    [InlineData(1, 10, 10, GoalDecisionReason.InvalidMiningProgress)]
    public async Task SnapshotGuardsPrecedeMovementNoProgressAndBudget(int level, int xp, int maxXp, GoalDecisionReason expected)
    {
        using Harness h = new(1, 1);
        h.State.ReserveAttempt(); h.State.RecordMovementFailure(); h.State.RecordGather(1, 0, MiningStepTests.Snapshot());
        var c = MiningStepTests.Snapshot(level, xp); c.MiningMaxXp = maxXp; h.Character.SaveCharacter(c);
        Assert.Equal(expected, (await h.Cycle()).Reason); Assert.Equal(1, h.State.AttemptedCycles);
        h.Client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false, GoalDecisionReason.InventoryPressure)]
    [InlineData(true, GoalDecisionReason.InvalidInventorySnapshot)]
    public async Task InventoryPrecedesInvalidProgress(bool invalid, GoalDecisionReason expected)
    {
        using Harness h = new(); var c = MiningStepTests.Snapshot(xp: -1);
        if(invalid) c.Inventory = null!; else c.InventoryMaxItems = 9;
        h.Character.SaveCharacter(c); Assert.Equal(expected, (await h.Cycle()).Reason); h.Client.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MovementFailurePrecedesNoProgressWhichPrecedesBudget()
    {
        using Harness h = new(1, 1); h.State.ReserveAttempt(); h.State.RecordGather(1, 0, MiningStepTests.Snapshot());
        h.State.RecordMovementFailure(); Assert.Equal(GoalDecisionReason.MiningDestinationNotReached, (await h.Cycle()).Reason);
        h.State.Reset(); h.State.ReserveAttempt(); h.State.RecordGather(1, 0, MiningStepTests.Snapshot());
        Assert.Equal(GoalDecisionReason.MiningNoProgress, (await h.Cycle()).Reason);
        h.State.Reset(); h.State.ReserveAttempt(); Assert.Equal(GoalDecisionReason.MiningCycleLimit, (await h.Cycle()).Reason);
        h.Client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThrownAttemptsConsumeBudgetAndPreserveException(bool actionFailure)
    {
        using Harness h = new(2, 2); InvalidOperationException failure = new("expected failure");
        if(actionFailure) h.Client.Setup(c => c.Gathering()).ThrowsAsync(failure);
        else h.Client.Setup(c => c.GetResources()).ThrowsAsync(failure);
        for(int i = 0; i < 2; i++) Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => h.Cycle()));
        Assert.Equal(GoalDecisionReason.MiningCycleLimit, (await h.Cycle()).Reason);
        Assert.Equal(2, h.State.AttemptedCycles);
        Assert.Equal(actionFailure ? 3 : 1, h.Logger.Events.Count);
        h.Client.Verify(c => c.GetResources(), Times.Exactly(2));
    }

    [Fact]
    public async Task NoProgressResetsOnXpAndLevelUpAndStopsAtThreshold()
    {
        using Harness h = new(20, 2);
        var responses = new Queue<Character>(new[] { MiningStepTests.Snapshot(), MiningStepTests.Snapshot(xp: 6),
            MiningStepTests.Snapshot(), MiningStepTests.Snapshot(2, 2), MiningStepTests.Snapshot(2, 2), MiningStepTests.Snapshot(2, 1) });
        h.Client.Setup(c => c.Gathering()).ReturnsAsync(() => MiningStepTests.Response(responses.Dequeue()));
        foreach(int expected in new[] { 1, 0, 1, 0, 1, 2 })
        {
            Assert.Equal(GoalDecisionStatus.Selected, (await h.Cycle()).Status); Assert.Equal(expected, h.State.ConsecutiveNoProgress);
        }
        Assert.Equal(GoalDecisionReason.MiningNoProgress, (await h.Cycle()).Reason);
        h.Client.Verify(c => c.Gathering(), Times.Exactly(6));
    }

    [Fact]
    public async Task LastAllowedGatherCompletionWinsOverBudgetAndInventory()
    {
        using Harness h = new(1, 1); var response = MiningStepTests.Snapshot(3, 4); response.InventoryMaxItems = 0;
        h.Client.Setup(c => c.Gathering()).ReturnsAsync(MiningStepTests.Response(response));
        Assert.Equal(GoalDecisionStatus.Selected, (await h.Cycle()).Status);
        Assert.Equal(GoalDecisionReason.MiningTargetReached, (await h.Cycle()).Reason);
        Assert.Equal(1, h.State.AttemptedCycles); h.Client.Verify(c => c.Gathering(), Times.Once());
        Assert.Equal(4, h.Logger.Events.Last().Fields.Count);
    }

    [Fact]
    public async Task MoveOnlyCycleConsumesAttemptButNotNoProgress()
    {
        using Harness h = new(1, 1); h.Character.SaveCharacter(MiningStepTests.Snapshot(x: 0));
        h.Client.Setup(c => c.Move(It.IsAny<MapPoint>())).ReturnsAsync(MiningStepTests.Response(MiningStepTests.Snapshot(2)));
        Assert.Equal(GoalDecisionStatus.Selected, (await h.Cycle()).Status);
        Assert.Equal(0, h.State.ConsecutiveNoProgress); Assert.Equal(GoalDecisionReason.MiningCycleLimit, (await h.Cycle()).Reason);
        h.Client.Verify(c => c.Gathering(), Times.Never());
    }

    [Fact]
    public async Task InvalidGatherIsSavedAndBlockedNextCycle()
    {
        using Harness h = new(); var invalid = MiningStepTests.Snapshot(xp: -1);
        h.Client.Setup(c => c.Gathering()).ReturnsAsync(MiningStepTests.Response(invalid));
        Assert.Equal(GoalDecisionStatus.Selected, (await h.Cycle()).Status); Assert.Same(invalid, h.Character.GetCharacter());
        Assert.Equal(GoalDecisionReason.InvalidMiningProgress, (await h.Cycle()).Reason); Assert.Equal(0, h.State.ConsecutiveNoProgress);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CatalogTerminalEmitsOnlyFinalReasonWithExactCounterFields(bool invalid)
    {
        using Harness h = new();
        h.Client.Setup(c => c.GetResources()).ReturnsAsync(invalid ? null! : new List<ResourceDatum>());
        var decision = await h.Cycle();
        Assert.Equal(invalid ? GoalDecisionReason.InvalidMiningCatalog : GoalDecisionReason.NoMiningDestination, decision.Reason);
        var fields = Assert.Single(h.Logger.Events).Fields;
        Assert.Equal(new Dictionary<string, object?>
        {
            ["goal.decision.status"] = "Blocked", ["goal.decision.reason"] = invalid ? "invalid_mining_catalog" : "no_mining_destination",
            ["goal.mining.target_level"] = 3, ["goal.mining.current_level"] = 1,
            ["goal.mining.attempted_cycles"] = 1, ["goal.mining.max_cycles"] = 10,
            ["goal.mining.consecutive_no_progress"] = 0, ["goal.mining.max_no_progress"] = 3
        }.OrderBy(p => p.Key), fields.OrderBy(p => p.Key));
        h.Client.Verify(c => c.Gathering(), Times.Never());
    }

    [Fact]
    public async Task InitializationResetsOnlyAfterFullSuccessAndPostLoadCancellationCheck()
    {
        using Harness h = new(); h.State.ReserveAttempt(); h.State.RecordMovementFailure();
        h.Client.Setup(c => c.WarmUpCache()).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Action.InitializeAsync(default)); Assert.Equal(1, h.State.AttemptedCycles);
        h.Client.Setup(c => c.WarmUpCache()).Returns(Task.CompletedTask);
        h.Client.Setup(c => c.GetCharacter()).ThrowsAsync(new InvalidOperationException());
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Action.InitializeAsync(default)); Assert.True(h.State.DestinationNotReached);
        using CancellationTokenSource cancel = new(); var loaded = MiningStepTests.Snapshot();
        h.Client.Setup(c => c.GetCharacter()).ReturnsAsync(() => { cancel.Cancel(); return loaded; });
        await Assert.ThrowsAsync<OperationCanceledException>(() => h.Action.InitializeAsync(cancel.Token));
        Assert.Same(loaded, h.Character.GetCharacter()); Assert.Equal(1, h.State.AttemptedCycles);
        h.Client.Setup(c => c.GetCharacter()).ReturnsAsync(loaded);
        await h.Action.InitializeAsync(default); Assert.Equal(0, h.State.AttemptedCycles); Assert.False(h.State.DestinationNotReached);
    }

    [Theory]
    [InlineData(GoalDecisionReason.InvalidMiningProgress)]
    [InlineData(GoalDecisionReason.MiningDestinationNotReached)]
    [InlineData(GoalDecisionReason.MiningNoProgress)]
    [InlineData(GoalDecisionReason.MiningCycleLimit)]
    [InlineData(GoalDecisionReason.InvalidMiningCatalog)]
    [InlineData(GoalDecisionReason.NoMiningDestination)]
    public async Task ProgressionTerminalEventsMatchOptionalActivityExactly(GoalDecisionReason reason)
    {
        GoalDecision? first = null;
        foreach(bool listen in new[] { false, true })
        {
            using Harness h = new(1,1);
            switch(reason)
            {
                case GoalDecisionReason.InvalidMiningProgress: h.Character.GetCharacter().MiningXp = -1; break;
                case GoalDecisionReason.MiningDestinationNotReached: h.State.RecordMovementFailure(); break;
                case GoalDecisionReason.MiningNoProgress: h.State.RecordGather(1,0,MiningStepTests.Snapshot()); break;
                case GoalDecisionReason.MiningCycleLimit: h.State.ReserveAttempt(); break;
                case GoalDecisionReason.InvalidMiningCatalog: h.Client.Setup(c => c.GetResources()).ReturnsAsync((List<ResourceDatum>)null!); break;
                case GoalDecisionReason.NoMiningDestination: h.Client.Setup(c => c.GetResources()).ReturnsAsync(new List<ResourceDatum>()); break;
            }
            Activity? stopped = null;
            using ActivityListener listener = new() { ShouldListenTo = s => listen && s == h.Source,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData, ActivityStopped = a => stopped = a };
            ActivitySource.AddActivityListener(listener);
            var decision = await h.Cycle(); Assert.Equal(reason, decision.Reason);
            if(first is not null) Assert.Equal(first, decision); first = decision;
            var fields = Assert.Single(h.Logger.Events).Fields; Assert.Equal(8, fields.Count);
            Assert.Equal(decision.ReasonCode, fields["goal.decision.reason"]);
            Assert.Equal(reason is GoalDecisionReason.MiningCycleLimit or GoalDecisionReason.InvalidMiningCatalog or GoalDecisionReason.NoMiningDestination ? 1 : 0, fields["goal.mining.attempted_cycles"]);
            Assert.Equal(reason == GoalDecisionReason.MiningNoProgress ? 1 : 0, fields["goal.mining.consecutive_no_progress"]);
            if(listen) Assert.Equal(fields.OrderBy(p => p.Key), stopped!.TagObjects.OrderBy(p => p.Key)); else Assert.Null(stopped);
        }
    }

    private sealed class Harness : IDisposable
    {
        public Mock<IGameClient> Client { get; } = MiningStepTests.Client();
        public CharacterService Character { get; } = new();
        public MiningRunState State { get; }
        public DecisionLogger<ActionService> Logger { get; } = new();
        public ActionService Action { get; }
        public ActivitySource Source => _source;
        private readonly ActivitySource _source = new("MiningRunTests");
        public Harness(int cycles = 10, int noProgress = 3)
        {
            State = TestMining.State(cycles, noProgress); Character.SaveCharacter(MiningStepTests.Snapshot());
            Action = new(State, Client.Object, new GoalService(Options.Create(new GoalSelectionSettings { MiningTargetLevel = 3 })),
                new StepBuilder(State, new RecordingDelay(), Client.Object, Mock.Of<IMapService>()),
                new GoalDecomposer(NullLogger<GoalDecomposer>.Instance, Mock.Of<IWearCraftTargetFinder>(), _source), Character, _source, Logger);
        }
        public Task<GoalDecision> Cycle() => Action.ExecuteCycleAsync(default);
        public void Dispose() => _source.Dispose();
    }
}
