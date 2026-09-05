using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models.Steps;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Models.Steps;

public class StepCancellationTests
{
    [Fact]
    public async Task ConditionalStep_PreCancelledTokenSkipsConditionAndChild()
    {
        bool conditionEvaluated = false;
        Mock<IStep> child = new();
        ConditionalStep step = new(
            child.Object,
            _ =>
            {
                conditionEvaluated = true;
                return true;
            },
            Mock.Of<ICharacterService>() );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => step.Execute( Mock.Of<IGameClient>(), cancellation.Token ) );

        Assert.False( conditionEvaluated );
        child.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MixedStep_CancellationAfterChildStartsNoFollowingChild()
    {
        using CancellationTokenSource cancellation = new();
        Mock<IStep> first = new();
        Mock<IStep> second = new();
        Mock<IGameClient> client = new();
        first.Setup( x => x.Execute( client.Object, cancellation.Token ) )
             .Callback( cancellation.Cancel )
             .Returns( Task.CompletedTask );
        second.Setup( x => x.Execute( client.Object, cancellation.Token ) )
              .Returns( Task.CompletedTask );
        MixedStep mixed = new( new List<IStep> { first.Object, second.Object }, Mock.Of<ICharacterService>() );

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => mixed.Execute( client.Object, cancellation.Token ) );

        first.Verify( x => x.Execute( client.Object, cancellation.Token ), Times.Once );
        second.Verify( x => x.Execute( client.Object, cancellation.Token ), Times.Never );
    }

    [Fact]
    public async Task GatheringStep_PreCancelledTokenStartsNoAction()
    {
        Mock<IGameClient> client = new();
        GatheringStep step = new( Mock.Of<ICharacterService>() );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => step.Execute( client.Object, cancellation.Token ) );

        client.Verify( x => x.Gathering(), Times.Never );
    }

    [Fact]
    public async Task MoveStep_CancellationDuringAction_SavesResponseBeforeStopping()
    {
        using CancellationTokenSource cancellation = new();
        Mock<ICharacterService> characterService = new();
        Mock<IGameClient> client = new();
        Character returnedCharacter = new() { Name = "Moved", Inventory = new List<Inventory>() };
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) )
              .Callback( cancellation.Cancel )
              .ReturnsAsync( new ActionResponse
              {
                  Data = new ActionData
                  {
                      Character = returnedCharacter,
                      Cooldown = new Cooldown { TotalSeconds = 5 }
                  }
              } );
        MoveStep step = new( new MapPoint { X = 1, Y = 2 }, characterService.Object );

        Task execution = step.Execute( client.Object, cancellation.Token );
        Task completed = await Task.WhenAny( execution, Task.Delay( 500 ) );

        Assert.Same( execution, completed );
        await Assert.ThrowsAnyAsync<OperationCanceledException>( () => execution );
        characterService.Verify( x => x.SaveCharacter( returnedCharacter ), Times.Once );
    }

    [Fact]
    public async Task MoveStep_PreCancelledTokenStartsNoAction()
    {
        Mock<IGameClient> client = new();
        MoveStep step = new( new MapPoint { X = 1, Y = 2 }, Mock.Of<ICharacterService>() );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => step.Execute( client.Object, cancellation.Token ) );

        client.Verify( x => x.Move( It.IsAny<MapPoint>() ), Times.Never );
    }

    [Fact]
    public async Task ActionStep_CancellationDuringAction_SavesResponseAndStopsCooldownPromptly()
    {
        using CancellationTokenSource cancellation = new();
        Mock<ICharacterService> characterService = new();
        Character returnedCharacter = new() { Name = "Updated", Inventory = new List<Inventory>() };
        int actionCalls = 0;
        ActionStep step = new(
            characterService.Object,
            _ =>
            {
                actionCalls++;
                cancellation.Cancel();
                return Task.FromResult( new ActionResponse
                {
                    Data = new ActionData
                    {
                        Character = returnedCharacter,
                        Cooldown = new Cooldown { TotalSeconds = 5 }
                    }
                } );
            },
            _ => true );

        Task execution = step.Execute( Mock.Of<IGameClient>(), cancellation.Token );
        Task completed = await Task.WhenAny( execution, Task.Delay( 500 ) );

        Assert.Same( execution, completed );
        await Assert.ThrowsAnyAsync<OperationCanceledException>( () => execution );
        Assert.Equal( 1, actionCalls );
        characterService.Verify( x => x.SaveCharacter( returnedCharacter ), Times.Once );
    }

    [Fact]
    public async Task ActionStep_PreCancelledTokenStartsNoAction()
    {
        int actionCalls = 0;
        ActionStep step = new(
            Mock.Of<ICharacterService>(),
            _ =>
            {
                actionCalls++;
                return Task.FromResult<ActionResponse>( null! );
            } );
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => step.Execute( Mock.Of<IGameClient>(), cancellation.Token ) );

        Assert.Equal( 0, actionCalls );
    }
}
