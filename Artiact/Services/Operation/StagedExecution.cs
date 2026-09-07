using Artiact.Services.Strategy;

namespace Artiact.Services.Operation;

public sealed class StagedExecution(ExecutionSettings settings, ApiSettings api, PortfolioSettings portfolio,
    StrategySessionFactory factory, OperationState status)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _ran;
    private StrategyDecision? _result;
    public async Task<StrategyDecision?> RunAsync(CancellationToken token)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_ran) return _result;
            _ran = true;
            ExecutionMode mode;
            PortfolioPolicy policy;
            try { mode = settings.Validate(api); policy = portfolio.Policy(); }
            catch (ArgumentException) { status.Set("ConfigurationRequiredOrInvalid"); return null; }
            if (mode == ExecutionMode.Legacy) { status.Set("LegacyCompatibilityMode"); return null; }
            if (mode == ExecutionMode.Bounded) return _result = await RunBoundedAsync(policy, token);
            var run = factory.Create(policy);
            _result = mode == ExecutionMode.Inspect ? await run.InspectAsync(token) : await run.TickAsync(token);
            if (mode == ExecutionMode.OneShot && _result.Status == StrategyStatus.UnknownOutcome && !token.IsCancellationRequested)
                _result = await run.TickAsync(token);
            bool success = _result.Status is StrategyStatus.Selected or StrategyStatus.Completed or StrategyStatus.Reconciled;
            status.Finish(mode + ":" + _result.Status + ":" + _result.Reason, success);
            return _result;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { status.Set("Cancelled"); return null; }
        catch (Exception) { status.Set("ExecutionFailed"); return null; }
        finally { _gate.Release(); }
    }

    private async Task<StrategyDecision?> RunBoundedAsync(PortfolioPolicy policy, CancellationToken token)
    {
        using var store = new FileRunCheckpointStore(settings.RunDirectory, new Uri(api.BaseUrl).GetLeftPart(UriPartial.Authority) + "/" + api.Character);
        var limits = new StrategyLimits(settings.MaxDecisions, settings.MaxNoProgress, settings.MaxActions, settings.MaxSeconds);
        string identity = System.Text.Json.JsonSerializer.Serialize(new { settings.RunId, api.BaseUrl, api.Character, Policy = policy.Identity, Limits = limits });
        var saved = store.Load();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token, status.StopToken);
        var remaining = TimeSpan.FromSeconds(settings.MaxSeconds) - (DateTimeOffset.UtcNow - (saved?.Started ?? DateTimeOffset.UtcNow));
        if (remaining > TimeSpan.Zero) stop.CancelAfter(remaining);
        var run = factory.Create(policy, limits, store, identity);
        while (true)
        {
            var result = await run.TickAsync(stop.Token);
            status.Progress(settings.RunId, result);
            status.Finish("Bounded:" + result.Status + ":" + result.Reason, result.Status is StrategyStatus.Selected or StrategyStatus.Completed or StrategyStatus.Reconciled);
            if (result.Status is StrategyStatus.Completed or StrategyStatus.Blocked or StrategyStatus.Cancelled) return result;
            if (result.Status == StrategyStatus.UnknownOutcome)
            {
                result = await run.TickAsync(stop.Token);
                status.Progress(settings.RunId, result);
                if (result.Status != StrategyStatus.Reconciled)
                { status.Finish("Bounded:" + result.Status + ":" + result.Reason, false); return result; }
            }
            if (result.Status == StrategyStatus.CoolingDown)
            {
                var expiration = run.State!.Character.GetProperty("cooldown_expiration").GetDateTimeOffset();
                var delay = expiration - DateTimeOffset.UtcNow;
                try { if (delay > TimeSpan.Zero) await Task.Delay(delay, stop.Token); }
                catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            }
        }
    }
}

public sealed class StagedWorker(IServiceScopeFactory scopes, OperationState state, ILogger<StagedWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var result = await scope.ServiceProvider.GetRequiredService<StagedExecution>().RunAsync(stoppingToken);
            if (result is not null) logger.LogInformation("Staged decision {Decision}", System.Text.Json.JsonSerializer.Serialize(result));
            else logger.LogWarning("Staged execution did not produce a decision: {State}", state.Snapshot(30).State);
        }
        catch (Exception) { state.Set("ConfigurationRequiredOrInvalid"); logger.LogWarning("Staged initialization failed; no game action scheduled"); }
    }
}
