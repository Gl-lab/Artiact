using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Models.Steps;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class MiningExecutionBoundaryTests
{
    [Fact]
    public async Task ResolvedMiningNeverIntroducesInventoryRemediation()
    {
        CharacterService character = new(); var snapshot = MiningStepTests.Snapshot(); snapshot.InventoryMaxItems = 9;
        character.SaveCharacter(snapshot);
        Mock<IWearCraftTargetFinder> finder = new(MockBehavior.Strict);
        using System.Diagnostics.ActivitySource source = new("ResolvedMiningDecomposition");
        var goal = new ResolvedMiningGoal(3, new("copper", 1, 2, 0));
        await new GoalDecomposer(Microsoft.Extensions.Logging.Abstractions.NullLogger<GoalDecomposer>.Instance, finder.Object, source)
            .DecomposeGoal(goal, character);
        Assert.Empty(goal.SubGoals); finder.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("target")]
    [InlineData("pressure")]
    [InlineData("inventory")]
    [InlineData("level")]
    [InlineData("xp")]
    [InlineData("max_xp")]
    [InlineData("full_xp")]
    [InlineData("locked")]
    [InlineData("zero_xp")]
    public async Task InvalidLiveFactsPreventMovementAndGather(string condition)
    {
        var c = MiningStepTests.Snapshot(x: 0);
        int resourceLevel = 1;
        switch (condition)
        {
            case "target": c.MiningLevel = 3; break;
            case "pressure": c.InventoryMaxItems = 9; break;
            case "inventory": c.Inventory = null!; break;
            case "level": c.MiningLevel = -1; break;
            case "xp": c.MiningXp = -1; break;
            case "max_xp": c.MiningMaxXp = 0; break;
            case "full_xp": c.MiningXp = 10; break;
            case "locked": resourceLevel = 2; break;
            case "zero_xp": c.MiningLevel = 11; break;
        }
        CharacterService character = new(); character.SaveCharacter(c);
        Mock<IGameClient> client = new(MockBehavior.Strict);
        RecordingDelay delay = new();
        await new MiningStep(character, condition == "zero_xp" ? 20 : 3,
            new("copper", resourceLevel, 2, 0), TestMining.State(), delay).Execute(client.Object, default);
        client.VerifyNoOtherCalls(); Assert.Empty(delay.Seconds);
    }

    [Theory]
    [InlineData("changed_level")]
    [InlineData("target")]
    [InlineData("pressure")]
    [InlineData("xp")]
    [InlineData("wrong_destination")]
    public async Task MoveResponseRechecksBeforeGather(string condition)
    {
        var response = MiningStepTests.Snapshot();
        switch(condition)
        {
            case "changed_level": response.MiningLevel = 2; break;
            case "target": response.MiningLevel = 3; break;
            case "pressure": response.InventoryMaxItems = 9; break;
            case "xp": response.MiningXp = -1; break;
            case "wrong_destination": response.X = 1; break;
        }
        CharacterService character = new(); character.SaveCharacter(MiningStepTests.Snapshot(x: 0));
        Mock<IGameClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.Move(It.Is<MapPoint>(p => p.X == 2 && p.Y == 0))).ReturnsAsync(MiningStepTests.Response(response, 7));
        RecordingDelay delay = new(); var state = TestMining.State();
        await new MiningStep(character, 3, new("copper", 1, 2, 0), state, delay).Execute(client.Object, default);
        Assert.Same(response, character.GetCharacter());
        Assert.Equal(new[] { 7 }, delay.Seconds);
        Assert.Equal(condition == "wrong_destination", state.DestinationNotReached);
        Assert.Equal(0, state.ConsecutiveNoProgress);
        client.Verify(c => c.Move(It.IsAny<MapPoint>()), Times.Once);
        client.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulResponseIsSavedAndAccountedBeforeCancellationWithoutWait(bool move)
    {
        CharacterService character = new(); character.SaveCharacter(MiningStepTests.Snapshot(x: move ? 0 : 2));
        Mock<IGameClient> client = new(MockBehavior.Strict);
        using CancellationTokenSource cancel = new();
        var response = MiningStepTests.Snapshot();
        if(move) client.Setup(c => c.Move(It.IsAny<MapPoint>())).ReturnsAsync(() => { cancel.Cancel(); return MiningStepTests.Response(response, 7); });
        else client.Setup(c => c.Gathering()).ReturnsAsync(() => { cancel.Cancel(); return MiningStepTests.Response(response, 5); });
        RecordingDelay delay = new(); var state = TestMining.State();
        await Assert.ThrowsAsync<OperationCanceledException>(() => new MiningStep(character, 3, new("copper", 1, 2, 0), state, delay).Execute(client.Object, cancel.Token));
        Assert.Same(response, character.GetCharacter()); Assert.Empty(delay.Seconds);
        Assert.Equal(move ? 0 : 1, state.ConsecutiveNoProgress);
        client.Verify(c => c.Gathering(), move ? Times.Never() : Times.Once());
    }

    [Fact]
    public async Task ControlledMoveCooldownCancellationPreventsGather()
    {
        CharacterService character = new(); character.SaveCharacter(MiningStepTests.Snapshot(x: 0));
        var response = MiningStepTests.Snapshot();
        Mock<IGameClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.Move(It.IsAny<MapPoint>())).ReturnsAsync(MiningStepTests.Response(response, 7));
        using CancellationTokenSource cancel = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingDelay delay = new() { Wait = (_, token) => { entered.SetResult(); return release.Task.WaitAsync(token); } };
        var task = new MiningStep(character, 3, new("copper", 1, 2, 0), TestMining.State(), delay).Execute(client.Object, cancel.Token);
        await entered.Task; Assert.Same(response, character.GetCharacter()); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Equal(new[] { 7 }, delay.Seconds); client.Verify(c => c.Gathering(), Times.Never());
    }

    [Fact]
    public async Task ResolvedGoalDoesNotLoadCatalogAgainAndHonorsTotalCooldowns()
    {
        CharacterService character = new(); character.SaveCharacter(MiningStepTests.Snapshot(x: 0));
        Mock<IGameClient> client = new(MockBehavior.Strict);
        client.Setup(c => c.Move(It.IsAny<MapPoint>())).ReturnsAsync(MiningStepTests.Response(MiningStepTests.Snapshot(), 7));
        client.Setup(c => c.Gathering()).ReturnsAsync(MiningStepTests.Response(MiningStepTests.Snapshot(xp: 6), 5));
        RecordingDelay delay = new();
        var builder = new StepBuilder(TestMining.State(), delay, client.Object, Mock.Of<IMapService>());
        var step = await builder.BuildStep(new ResolvedMiningGoal(3, new("copper", 1, 2, 0)), character);
        await step.Execute(client.Object, default);
        Assert.Equal(new[] { 7, 5 }, delay.Seconds);
        client.Verify(c => c.Move(It.IsAny<MapPoint>()), Times.Once()); client.Verify(c => c.Gathering(), Times.Once());
        client.VerifyNoOtherCalls();
    }
}

internal sealed class RecordingDelay : IMiningCooldownDelay
{
    public List<int> Seconds { get; } = [];
    public Func<int, CancellationToken, Task>? Wait { get; init; }
    public Task WaitAsync(int totalSeconds, CancellationToken cancellationToken)
    {
        Seconds.Add(totalSeconds); return Wait?.Invoke(totalSeconds, cancellationToken) ?? Task.CompletedTask;
    }
}
