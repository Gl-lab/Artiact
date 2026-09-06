using Artiact.Models;
using System.Diagnostics;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models.Steps;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class ActionServiceTests
{
    [Fact]
    public async Task ExecuteCycleAsync_PreCancelledTokenSelectsNoGoal()
    {
        Mock<IGoalService> goalService = new();
        ActionService service = new(
            Mock.Of<IGameClient>(),
            goalService.Object,
            Mock.Of<IStepBuilder>(),
            Mock.Of<IGoalDecomposer>(),
            new Mock<ICharacterService>(MockBehavior.Strict).Object,
            new ActivitySource( "ActionServiceTests.PreCancelled" ) );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.ExecuteCycleAsync( cancellation.Token ) );

        goalService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteCycleAsync_FailureMarksActiveTraceAndPreservesException()
    {
        ActivityStatusCode? stoppedStatus = null;
        using ActivityListener listener = ListenTo(
            "ActionServiceTests.Failure",
            activity => stoppedStatus = activity.Status );
        Mock<IGameClient> client = new();
        Mock<IGoalService> goalService = new();
        Mock<IStepBuilder> stepBuilder = new();
        Mock<IGoalDecomposer> goalDecomposer = new();
        Mock<ICharacterService> characterService = new();
        Mock<IStep> step = new();
        GoalDecision decision = GoalDecision.Create(GoalDecisionStatus.Selected,GoalDecisionReason.MiningBelowTarget,20,19,20,10,10,GoalType.Gathering);
        InvalidOperationException expected = new( "cycle failed" );

        characterService.Setup( x => x.GetCharacter() )
                        .Returns( new Character { Name = "TestCharacter", Inventory = new List<Inventory>() } );
        goalService.Setup( x => x.Evaluate( It.IsAny<Character>() ) ).Returns( decision );
        goalDecomposer.Setup( x => x.DecomposeGoal( It.IsAny<Goal>(), characterService.Object ) ).Returns( Task.CompletedTask );
        stepBuilder.Setup( x => x.BuildStep( It.IsAny<Goal>(), characterService.Object ) ).ReturnsAsync( step.Object );
        step.Setup( x => x.Execute( client.Object, CancellationToken.None ) ).ThrowsAsync( expected );
        ActionService service = new(
            client.Object,
            goalService.Object,
            stepBuilder.Object,
            goalDecomposer.Object,
            characterService.Object,
            new ActivitySource( "ActionServiceTests.Failure" ) );

        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteCycleAsync( CancellationToken.None ) );

        Assert.Same( expected, actual );
        Assert.Equal( ActivityStatusCode.Error, stoppedStatus );
    }

    [Fact]
    public async Task InitializeAsync_PreCancelledTokenStartsNoClientWork()
    {
        Mock<IGameClient> client = new();
        ActionService service = CreateService( client: client );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.InitializeAsync( cancellation.Token ) );

        client.Verify( x => x.WarmUpCache(), Times.Never );
        client.Verify( x => x.GetCharacter(), Times.Never );
    }

    [Fact]
    public async Task InitializeAsync_CancellationAfterWarmUpSkipsCharacterLoad()
    {
        using CancellationTokenSource cancellation = new();
        Mock<IGameClient> client = new();
        client.Setup( x => x.WarmUpCache() )
              .Callback( cancellation.Cancel )
              .Returns( Task.CompletedTask );
        ActionService service = CreateService( client: client );

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.InitializeAsync( cancellation.Token ) );

        client.Verify( x => x.WarmUpCache(), Times.Once );
        client.Verify( x => x.GetCharacter(), Times.Never );
    }

    [Fact]
    public async Task ExecuteCycleAsync_ExecutesExactlyOneGoalCycle()
    {
        using ActivityListener listener = ListenTo( "ActionServiceTests" );
        Mock<IGameClient> client = new();
        Mock<IGoalService> goalService = new();
        Mock<IStepBuilder> stepBuilder = new();
        Mock<IGoalDecomposer> goalDecomposer = new();
        Mock<ICharacterService> characterService = new();
        Mock<IStep> step = new();
        GoalDecision decision = GoalDecision.Create(GoalDecisionStatus.Selected,GoalDecisionReason.MiningBelowTarget,20,19,20,10,10,GoalType.Gathering);

        characterService.Setup( x => x.GetCharacter() )
                        .Returns( new Character { Name = "TestCharacter", Inventory = new List<Inventory>() } );
        goalService.Setup( x => x.Evaluate( It.IsAny<Character>() ) ).Returns( decision );
        goalDecomposer.Setup( x => x.DecomposeGoal( It.IsAny<Goal>(), characterService.Object ) ).Returns( Task.CompletedTask );
        stepBuilder.Setup( x => x.BuildStep( It.IsAny<Goal>(), characterService.Object ) ).ReturnsAsync( step.Object );
        step.Setup( x => x.Execute( client.Object, CancellationToken.None ) ).Returns( Task.CompletedTask );
        ActionService service = new( client.Object,
            goalService.Object,
            stepBuilder.Object,
            goalDecomposer.Object,
            characterService.Object,
            new ActivitySource( "ActionServiceTests" ) );

        await service.ExecuteCycleAsync( CancellationToken.None );

        goalService.Verify( x => x.Evaluate( It.IsAny<Character>() ), Times.Once );
        goalDecomposer.Verify( x => x.DecomposeGoal( It.IsAny<Goal>(), characterService.Object ), Times.Once );
        stepBuilder.Verify( x => x.BuildStep( It.IsAny<Goal>(), characterService.Object ), Times.Once );
        step.Verify( x => x.Execute( client.Object, CancellationToken.None ), Times.Once );
    }

    [Fact]
    public async Task ExecuteCycleAsync_WithoutActivityListener_StillExecutesCycle()
    {
        Mock<IGameClient> client = new();
        Mock<IGoalService> goalService = new();
        Mock<IStepBuilder> stepBuilder = new();
        Mock<IGoalDecomposer> goalDecomposer = new();
        Mock<ICharacterService> characterService = new();
        Mock<IStep> step = new();
        GoalDecision decision = GoalDecision.Create(GoalDecisionStatus.Selected,GoalDecisionReason.MiningBelowTarget,20,19,20,10,10,GoalType.Gathering);

        characterService.Setup( x => x.GetCharacter() )
                        .Returns( new Character { Name = "TestCharacter", Inventory = new List<Inventory>() } );
        goalService.Setup( x => x.Evaluate( It.IsAny<Character>() ) ).Returns( decision );
        goalDecomposer.Setup( x => x.DecomposeGoal( It.IsAny<Goal>(), characterService.Object ) ).Returns( Task.CompletedTask );
        stepBuilder.Setup( x => x.BuildStep( It.IsAny<Goal>(), characterService.Object ) ).ReturnsAsync( step.Object );
        step.Setup( x => x.Execute( client.Object, CancellationToken.None ) ).Returns( Task.CompletedTask );
        ActionService service = new( client.Object,
            goalService.Object,
            stepBuilder.Object,
            goalDecomposer.Object,
            characterService.Object,
            new ActivitySource( "ActionServiceTests.NoListener" ) );

        await service.ExecuteCycleAsync( CancellationToken.None );

        step.Verify( x => x.Execute( client.Object, CancellationToken.None ), Times.Once );
    }

    [Theory]
    [InlineData(GoalDecisionStatus.Selected)]
    [InlineData(GoalDecisionStatus.Completed)]
    [InlineData(GoalDecisionStatus.Blocked)]
    public async Task Cycle_ReturnsExactDecisionAndExecutesOnlySelected(GoalDecisionStatus status)
    {
        Character snapshot = GoalServiceTests.Snapshot();
        GoalDecision decision = status switch
        {
            GoalDecisionStatus.Selected => GoalDecision.Create(status,GoalDecisionReason.MiningBelowTarget,27,19,20,10,10,GoalType.Gathering),
            GoalDecisionStatus.Completed => GoalDecision.Create(status,GoalDecisionReason.MiningTargetReached,20,20),
            _ => GoalDecision.Create(status,GoalDecisionReason.InventoryPressure,20,19,20,11,9)
        };
        Mock<ICharacterService> character = new(MockBehavior.Strict);
        character.Setup(x=>x.GetCharacter()).Returns(snapshot);
        Mock<IGoalService> selector = new(MockBehavior.Strict);
        selector.Setup(x=>x.Evaluate(snapshot)).Returns(decision);
        Mock<IGameClient> client = new(MockBehavior.Strict);
        Mock<IGoalDecomposer> decomposer = new(MockBehavior.Strict);
        Mock<IStepBuilder> builder = new(MockBehavior.Strict);
        Mock<IStep> step = new(MockBehavior.Strict);
        List<Goal> graphs = new();
        if(status == GoalDecisionStatus.Selected)
        {
            decomposer.Setup(x=>x.DecomposeGoal(It.IsAny<Goal>(),character.Object))
                .Callback<Goal,ICharacterService>((goal,_)=>
                {
                    Assert.Equal(27,Assert.IsType<GatheringGoal>(goal).TargetLevel);
                    graphs.Add(goal); goal.Type = GoalType.LevelUp;
                    goal.AddSubGoal(new LevelUpGoal());
                }).Returns(Task.CompletedTask);
            builder.Setup(x=>x.BuildStep(It.IsAny<Goal>(),character.Object))
                .Callback<Goal,ICharacterService>((goal,_)=>Assert.Same(graphs.Last(),goal)).ReturnsAsync(step.Object);
            step.Setup(x=>x.Execute(client.Object,CancellationToken.None)).Returns(Task.CompletedTask);
        }
        using ActivitySource source = new("ActionServiceTests.Boundary");
        ActionService service = new(client.Object,selector.Object,builder.Object,decomposer.Object,character.Object,source);
        Assert.Same(decision,await service.ExecuteCycleAsync(CancellationToken.None));
        character.Verify(x=>x.GetCharacter(),Times.Once);
        selector.Verify(x=>x.Evaluate(snapshot),Times.Once);
        if(status == GoalDecisionStatus.Selected)
        {
            decomposer.Verify(x=>x.DecomposeGoal(It.IsAny<Goal>(),character.Object),Times.Once);
            builder.Verify(x=>x.BuildStep(It.IsAny<Goal>(),character.Object),Times.Once);
            step.Verify(x=>x.Execute(client.Object,CancellationToken.None),Times.Once);
            Assert.Same(decision,await service.ExecuteCycleAsync(CancellationToken.None));
            Assert.NotSame(graphs[0],graphs[1]);
            Assert.Equal(GoalType.Gathering,decision.SelectedGoalType);
        }
        else { decomposer.VerifyNoOtherCalls(); builder.VerifyNoOtherCalls(); step.VerifyNoOtherCalls(); }
        client.VerifyNoOtherCalls();
    }

    private static ActionService CreateService( Mock<IGameClient>? client = null )
    {
        return new ActionService( ( client ?? new Mock<IGameClient>() ).Object,
            Mock.Of<IGoalService>(),
            Mock.Of<IStepBuilder>(),
            Mock.Of<IGoalDecomposer>(),
            Mock.Of<ICharacterService>(),
            new ActivitySource( "ActionServiceTests.NoListener" ) );
    }

    private static ActivityListener ListenTo(
        string sourceName,
        Action<Activity>? activityStopped = null )
    {
        ActivityListener listener = new()
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = ( ref ActivityCreationOptions<ActivityContext> _ ) => ActivitySamplingResult.AllData,
            SampleUsingParentId = ( ref ActivityCreationOptions<string> _ ) => ActivitySamplingResult.AllData,
            ActivityStopped = activityStopped
        };
        ActivitySource.AddActivityListener( listener );
        return listener;
    }
}
