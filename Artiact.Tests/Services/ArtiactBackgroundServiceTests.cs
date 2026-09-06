using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artiact.Tests.Services;

public class ArtiactBackgroundServiceTests
{
    [Fact]
    public void DefaultDependencyInjectionActivatesHostedWorker()
    {
        using ServiceProvider provider = new ServiceCollection()
            .AddLogging()
            .AddHostedService<ArtiactBackgroundService>()
            .BuildServiceProvider();

        ArtiactBackgroundService worker = Assert.IsType<ArtiactBackgroundService>(
            Assert.Single( provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>() ) );

        Assert.NotNull( worker );
    }

    [Fact]
    public async Task Worker_InitializesOnceAndRunsOneCycleAtATimeUntilCancellation()
    {
        using CancellationTokenSource cancellation = new();
        RecordingActionService actionService = new( cancellation, cancelAfterCycles: 2 );
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IActionService>( actionService )
            .BuildServiceProvider();
        ArtiactBackgroundService worker = new(
            NullLogger<ArtiactBackgroundService>.Instance,
            provider );

        await worker.StartAsync( cancellation.Token );
        await worker.ExecuteTask!;

        Assert.Equal( 1, actionService.InitializeCalls );
        Assert.Equal( 2, actionService.CycleCalls );
    }

    [Fact]
    public async Task Worker_CancellationDuringRecoveryStopsNormally()
    {
        using CancellationTokenSource cancellation = new();
        CancellingFailureActionService actionService = new( cancellation );
        int recoveryDelayCalls = 0;
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IActionService>( actionService )
            .BuildServiceProvider();
        ArtiactBackgroundService worker = new(
            NullLogger<ArtiactBackgroundService>.Instance,
            provider,
            ( _, _ ) =>
            {
                recoveryDelayCalls++;
                return Task.CompletedTask;
            } );

        await worker.StartAsync( cancellation.Token );
        await worker.ExecuteTask!;

        Assert.Equal( 1, actionService.CycleCalls );
        Assert.Equal( 0, recoveryDelayCalls );
    }

    [Fact]
    public async Task Worker_RecoverableFailureWaitsOnceBeforeRetry()
    {
        using CancellationTokenSource cancellation = new();
        RecoverOnceActionService actionService = new( cancellation );
        int delayCalls = 0;
        TimeSpan requestedDelay = TimeSpan.Zero;
        using ServiceProvider provider = new ServiceCollection()
            .AddSingleton<IActionService>( actionService )
            .BuildServiceProvider();
        ArtiactBackgroundService worker = new(
            NullLogger<ArtiactBackgroundService>.Instance,
            provider,
            ( delay, _ ) =>
            {
                delayCalls++;
                requestedDelay = delay;
                return Task.CompletedTask;
            } );

        await worker.StartAsync( cancellation.Token );
        await worker.ExecuteTask!;

        Assert.Equal( 2, actionService.CycleCalls );
        Assert.Equal( 1, delayCalls );
        Assert.Equal( TimeSpan.FromSeconds( 30 ), requestedDelay );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Worker_TerminalFirstCycleStopsWithoutRecoveryOrDuplicateEvent(bool completed)
    {
        using CancellationTokenSource stop = new();
        var action = new Moq.Mock<IActionService>();
        action.Setup(x=>x.InitializeAsync(Moq.It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        int calls=0, delays=0;
        GoalDecision decision=completed
            ? GoalDecision.Create(GoalDecisionStatus.Completed,GoalDecisionReason.MiningTargetReached,20,20)
            : GoalDecision.Create(GoalDecisionStatus.Blocked,GoalDecisionReason.InventoryPressure,20,19,20,11,9);
        action.Setup(x=>x.ExecuteCycleAsync(Moq.It.IsAny<CancellationToken>())).Returns(()=>
        {
            calls++; if(calls>1) stop.Cancel();
            return Task.FromResult(decision);
        });
        using ServiceProvider provider=new ServiceCollection().AddSingleton(action.Object).BuildServiceProvider();
        DecisionLogger<ArtiactBackgroundService> logger=new();
        using ArtiactBackgroundService worker=new(logger,provider,(_,_)=>{delays++;stop.Cancel();return Task.CompletedTask;});
        await worker.StartAsync(stop.Token);
        await worker.ExecuteTask!;
        Assert.Equal(1,calls);Assert.Equal(0,delays);
        Assert.DoesNotContain(logger.Events,e=>e.Event.Name=="GoalDecision" || e.Fields.Keys.Any(k=>k.StartsWith("goal.")));
    }

    private sealed class RecordingActionService(
        CancellationTokenSource cancellation,
        int cancelAfterCycles ) : IActionService
    {
        public int InitializeCalls { get; private set; }
        public int CycleCalls { get; private set; }

        public Task InitializeAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            InitializeCalls++;
            return Task.CompletedTask;
        }

        public Task<GoalDecision> ExecuteCycleAsync( CancellationToken cancellationToken )
        {
            cancellationToken.ThrowIfCancellationRequested();
            CycleCalls++;
            if ( CycleCalls == cancelAfterCycles )
            {
                cancellation.Cancel();
            }
            return Task.FromResult(GoalDecision.Create(GoalDecisionStatus.Selected, GoalDecisionReason.MiningBelowTarget,20,19,20,10,10,Artiact.Contracts.Models.GoalType.Gathering));
        }
    }

    private sealed class CancellingFailureActionService(
        CancellationTokenSource cancellation ) : IActionService
    {
        public int CycleCalls { get; private set; }

        public Task InitializeAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task<GoalDecision> ExecuteCycleAsync( CancellationToken cancellationToken )
        {
            CycleCalls++;
            cancellation.Cancel();
            throw new InvalidOperationException( "Recoverable failure." );
        }
    }

    private sealed class RecoverOnceActionService(
        CancellationTokenSource cancellation ) : IActionService
    {
        public int CycleCalls { get; private set; }

        public Task InitializeAsync( CancellationToken cancellationToken ) => Task.CompletedTask;

        public Task<GoalDecision> ExecuteCycleAsync( CancellationToken cancellationToken )
        {
            CycleCalls++;
            if ( CycleCalls == 1 )
            {
                throw new InvalidOperationException( "Recoverable failure." );
            }

            cancellation.Cancel();
            return Task.FromResult(GoalDecision.Create(GoalDecisionStatus.Selected, GoalDecisionReason.MiningBelowTarget,20,19,20,10,10,Artiact.Contracts.Models.GoalType.Gathering));
        }
    }
}
