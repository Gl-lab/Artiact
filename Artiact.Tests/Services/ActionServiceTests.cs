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
            Mock.Of<ICharacterService>(),
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
        GatheringGoal goal = new( 20 );
        InvalidOperationException expected = new( "cycle failed" );

        characterService.Setup( x => x.GetCharacter() )
                        .Returns( new Character { Name = "TestCharacter", Inventory = new List<Inventory>() } );
        goalService.Setup( x => x.GetGoal( characterService.Object ) ).Returns( goal );
        goalDecomposer.Setup( x => x.DecomposeGoal( goal, characterService.Object ) ).Returns( Task.CompletedTask );
        stepBuilder.Setup( x => x.BuildStep( goal, characterService.Object ) ).ReturnsAsync( step.Object );
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
        GatheringGoal goal = new( 20 );

        characterService.Setup( x => x.GetCharacter() )
                        .Returns( new Character { Name = "TestCharacter", Inventory = new List<Inventory>() } );
        goalService.Setup( x => x.GetGoal( characterService.Object ) ).Returns( goal );
        goalDecomposer.Setup( x => x.DecomposeGoal( goal, characterService.Object ) ).Returns( Task.CompletedTask );
        stepBuilder.Setup( x => x.BuildStep( goal, characterService.Object ) ).ReturnsAsync( step.Object );
        step.Setup( x => x.Execute( client.Object, CancellationToken.None ) ).Returns( Task.CompletedTask );
        ActionService service = new( client.Object,
            goalService.Object,
            stepBuilder.Object,
            goalDecomposer.Object,
            characterService.Object,
            new ActivitySource( "ActionServiceTests" ) );

        await service.ExecuteCycleAsync( CancellationToken.None );

        goalService.Verify( x => x.GetGoal( characterService.Object ), Times.Once );
        goalDecomposer.Verify( x => x.DecomposeGoal( goal, characterService.Object ), Times.Once );
        stepBuilder.Verify( x => x.BuildStep( goal, characterService.Object ), Times.Once );
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
        GatheringGoal goal = new( 20 );

        characterService.Setup( x => x.GetCharacter() )
                        .Returns( new Character { Name = "TestCharacter", Inventory = new List<Inventory>() } );
        goalService.Setup( x => x.GetGoal( characterService.Object ) ).Returns( goal );
        goalDecomposer.Setup( x => x.DecomposeGoal( goal, characterService.Object ) ).Returns( Task.CompletedTask );
        stepBuilder.Setup( x => x.BuildStep( goal, characterService.Object ) ).ReturnsAsync( step.Object );
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
