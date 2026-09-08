using Artiact.Services.Strategy;

namespace Artiact.Services.Operation;

public sealed class ScheduledRun(IOperatorExecution execution, ApiSettings api, PortfolioSettings portfolio) : IScheduledRun
{
    public async Task<StrategyDecision?> InspectAsync(ExecutionSettings settings, CancellationToken token)
    {
        using (var store = Store(settings))
            if (store.Load() is not null) throw new IOException("Prior checkpoint requires intervention.");
        return await execution.ExecuteAsync(settings, token);
    }
    public Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token) => execution.ExecuteAsync(settings, token);
    public Task ArchiveAsync(ExecutionSettings settings)
    {
        using var store = Store(settings);
        store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(StagedExecution.RunIdentity(settings, api, portfolio.Policy())));
        return Task.CompletedTask;
    }
    private FileRunCheckpointStore Store(ExecutionSettings settings) => new(settings.RunDirectory,
        new Uri(api.BaseUrl).GetLeftPart(UriPartial.Authority) + "/" + api.Character);
}

public sealed class ScheduleWorker(ScheduleRunner runner) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var registration = stoppingToken.Register(runner.RequestStop);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await runner.TickAsync(stoppingToken);
                if (runner.Snapshot().Status is "Completed" or "Stopped" or "InterventionRequired" or "Disabled") return;
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally { if (stoppingToken.IsCancellationRequested) await runner.TickAsync(stoppingToken); }
    }
}
