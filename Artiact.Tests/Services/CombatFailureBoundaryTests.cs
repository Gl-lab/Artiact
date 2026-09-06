using Artiact.Services.Combat;
using Moq;

namespace Artiact.Tests.Services;

public class CombatFailureBoundaryTests
{
    [Theory]
    [InlineData(false, CombatReason.FightLimit)]
    [InlineData(true, CombatReason.RestLimit)]
    public async Task ActionSpecificBudgetCannotBeRefunded(bool rest, CombatReason expected)
    {
        var state = CombatRunTests.Initial() with { MapId = 2 };
        if (rest) state = state with { Stats = state.Stats with { Hp = 1 } };
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (rest) state = state with { Stats = state.Stats with { Hp = 2 } };
                return Task.FromResult(new CombatReply(state, 0));
            });
        var run = new CombatRun(new(2), state, CombatRunTests.Destination(), port.Object,
            new CombatRunTests.NoDelay(), new(Fights: 1, Rests: 1, NoProgress: 5));
        Assert.Equal(CombatStatus.Selected, (await run.ExecuteCycleAsync()).Status);
        Assert.Equal(expected, (await run.ExecuteCycleAsync()).Reason);
        port.Verify(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
    [Theory]
    [InlineData("access", CombatReason.UnsupportedAccess)]
    [InlineData("unsafe", CombatReason.UnsafeCombat)]
    [InlineData("unknown", CombatReason.UnknownCombat)]
    [InlineData("inventory", CombatReason.InventoryPressure)]
    [InlineData("limits", CombatReason.InvalidLimits)]
    [InlineData("decision", CombatReason.DecisionLimit)]
    [InlineData("gear", CombatReason.EquipmentUnavailable)]
    public async Task InvalidPrerequisiteDoesNotDispatch(string kind, CombatReason expected)
    {
        var state = CombatRunTests.Initial();
        var destination = CombatRunTests.Destination();
        var limits = new CombatLimits();
        CombatGear? gear = null;
        switch (kind)
        {
            case "access": destination = destination with { Accessible = false }; break;
            case "unsafe": state = state with { Stats = state.Stats with { Attack = 1 } }; break;
            case "unknown": state = state with { Stats = state.Stats with { Attack = -1 } }; break;
            case "inventory": state = state with { Capacity = 0 }; break;
            case "limits": limits = limits with { Fights = 0 }; break;
            case "decision": limits = limits with { Decisions = 1, NoProgress = 1 }; break;
            case "gear": gear = new("missing", new(20, 100)); break;
        }
        var port = new Mock<ICombatActionPort>(MockBehavior.Strict);
        var result = await new CombatRun(new(2), state, destination, port.Object, new CombatRunTests.NoDelay(), limits, gear).ExecuteCycleAsync();
        Assert.Equal(expected, result.Reason);
        port.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("wrong_move", CombatReason.InvalidPostcondition)]
    [InlineData("defeat", CombatReason.Defeat)]
    [InlineData("rest", CombatReason.RecoveryNoProgress)]
    [InlineData("cooldown", CombatReason.InvalidPostcondition)]
    [InlineData("identity", CombatReason.InvalidPostcondition)]
    [InlineData("cancel", CombatReason.Cancelled)]
    public async Task FailedResponseIsRetainedAndStops(string kind, CombatReason expected)
    {
        using var stop = new CancellationTokenSource();
        var initial = CombatRunTests.Initial();
        if (kind == "rest") initial = initial with { Stats = initial.Stats with { Hp = 14 } };
        var returned = initial with { MapId = kind == "wrong_move" || kind == "rest" ? 1 : 2 };
        if (kind == "identity") returned = returned with { Name = "other" };
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (kind == "cancel") stop.Cancel();
                return Task.FromResult(new CombatReply(returned, kind == "cooldown" ? -1 : 0, kind == "defeat"));
            });
        var run = new CombatRun(new(2), initial, CombatRunTests.Destination(), port.Object, new CombatRunTests.NoDelay());
        var result = await run.ExecuteCycleAsync(stop.Token);
        Assert.Equal(expected, result.Reason);
        Assert.Same(returned, run.State);
        Assert.Same(result, await run.ExecuteCycleAsync());
        port.Verify(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NoXpEventuallyStopsWithinConfiguredBound()
    {
        var state = CombatRunTests.Initial() with { MapId = 2 };
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(It.IsAny<CombatCommand>(), It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CombatReply(state, 0));
        var run = new CombatRun(new(2), state, CombatRunTests.Destination(), port.Object, new CombatRunTests.NoDelay(), new(NoProgress: 2));
        await run.ExecuteCycleAsync(); await run.ExecuteCycleAsync();
        Assert.Equal(CombatReason.NoProgress, (await run.ExecuteCycleAsync()).Reason);
        port.Verify(x => x.DispatchAsync(CombatCommand.Fight, It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
