using System.Collections.Immutable;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Moq;

namespace Artiact.Tests.Services;

public class CombatRunTests
{
    [Fact]
    public async Task SwapCanEquipAfterUnequipFillsLastInventoryUnit()
    {
        var state = Initial() with { Weapon = "old", Capacity = 2,
            Inventory = ImmutableDictionary<string, int>.Empty.Add("quick_blade", 1), Stats = new(20, 5) };
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((CombatCommand command, CombatDestination _, string? _, CancellationToken _) =>
            {
                state = command == CombatCommand.Unequip
                    ? state with { Weapon = "", Stats = new(20, 0), Inventory = state.Inventory.Add("old", 1) }
                    : state with { Weapon = "quick_blade", Stats = new(20, 10), Inventory = state.Inventory.Remove("quick_blade") };
                return Task.FromResult(new CombatReply(state, 3));
            });
        var run = new CombatRun(new(2), state, Destination(), port.Object, new NoDelay(), gear: new("quick_blade", new(20, 10)));
        Assert.Equal(CombatCommand.Unequip, (await run.ExecuteCycleAsync()).Command);
        Assert.Equal(CombatCommand.Equip, (await run.ExecuteCycleAsync()).Command);
        Assert.Equal(1, run.State!.Inventory["old"]);
    }
    internal static CombatObservation Initial() => new("researcher", 1, 0, 10, 20, 1, "overworld", "quick_blade",
        10, ImmutableDictionary<string, int>.Empty, new CombatStats(20, 10));
    internal static CombatDestination Destination() => new(2, "overworld", "dummy", new CombatStats(20, 3), true);

    [Fact]
    public async Task BaselineCompletesInFiveDecisionsWithoutActionsAfterTarget()
    {
        var state = Initial();
        var commands = new List<CombatCommand>();
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns((CombatCommand command, CombatDestination _, string? _, CancellationToken _) =>
            {
                commands.Add(command);
                int seconds;
                switch (command)
                {
                    case CombatCommand.Move: state = state with { MapId = 2 }; seconds = 7; break;
                    case CombatCommand.Rest: state = state with { Stats = state.Stats with { Hp = 20 } }; seconds = 6; break;
                    case CombatCommand.Fight:
                        state = state with { Level = state.Xp == 0 ? 1 : 2, Xp = state.Xp == 0 ? 5 : 0,
                            Stats = state.Stats with { Hp = 14 },
                            Inventory = state.Inventory.SetItem("feather", state.Inventory.GetValueOrDefault("feather") + 1) };
                        seconds = 8; break;
                    default: throw new InvalidOperationException();
                }
                return Task.FromResult(new CombatReply(state, seconds));
            });
        var run = new CombatRun(new(2), state, Destination(), port.Object, new NoDelay());
        CombatDecision? decision = null;
        for (int i = 0; i < 5; i++) decision = await run.ExecuteCycleAsync();
        Assert.Equal(CombatStatus.Completed, decision!.Status);
        Assert.Equal(29, decision.VirtualSeconds);
        Assert.Equal(2, decision.State!.Level);
        Assert.Equal(14, decision.State.Stats.Hp);
        Assert.Equal(8, decision.State.FreeUnits);
        Assert.Equal(new[] { CombatCommand.Move, CombatCommand.Fight, CombatCommand.Rest, CombatCommand.Fight }, commands);
        Assert.Same(decision, await run.ExecuteCycleAsync());
    }

    [Theory]
    [InlineData(ActionFailureKind.UnknownOutcome, CombatReason.UnknownOutcome)]
    [InlineData(ActionFailureKind.Rejected, CombatReason.Rejected)]
    public async Task ActionFailureIsStickyAndNeverRetried(ActionFailureKind failure, CombatReason reason)
    {
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ActionFailureException(failure));
        var run = new CombatRun(new(2), Initial(), Destination(), port.Object, new NoDelay());
        var decision = await run.ExecuteCycleAsync();
        Assert.Equal(reason, decision.Reason);
        Assert.Same(decision, await run.ExecuteCycleAsync());
        port.Verify(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0, CombatReason.InvalidTarget)]
    [InlineData(1, CombatReason.TargetReached)]
    public async Task TerminalTargetDoesNotDispatch(int target, CombatReason reason)
    {
        var port = new Mock<ICombatActionPort>(MockBehavior.Strict);
        var result = await new CombatRun(new(target), Initial(), Destination(), port.Object, new NoDelay()).ExecuteCycleAsync();
        Assert.Equal(reason, result.Reason);
        port.VerifyNoOtherCalls();
    }

    internal sealed class NoDelay : IMiningCooldownDelay
    {
        public Task WaitAsync(int totalSeconds, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
