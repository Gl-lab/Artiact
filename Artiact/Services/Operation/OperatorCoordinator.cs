using Artiact.Services.Strategy;
using System.Text.Json;

namespace Artiact.Services.Operation;

public sealed record OperatorRunRequest(string RunId, int MaxActions, int MaxSeconds, int MaxDecisions, int MaxNoProgress);
public sealed record OperatorControlResult(bool Accepted, string Reason, string? Receipt = null, StrategyDecision? Decision = null);
public interface IOperatorExecution
{
    Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token);
}

public sealed class OperatorCoordinator(ExecutionSettings settings, ApiSettings api, PortfolioSettings portfolio,
    OperationState state, IOperatorExecution execution, TimeProvider? time = null) : IHostedService
{
    private sealed record Ticket(OperatorRunRequest Request, DateTimeOffset Created, string Profile, bool Started = false);
    private readonly Dictionary<string, Ticket> _tickets = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly CancellationTokenSource _shutdown = new();
    private Task _active = Task.CompletedTask;

    public async Task<OperatorControlResult> InspectAsync(OperatorRunRequest request)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_active.IsCompleted) return new(false, "ExecutorBusy");
            var prepared = Prepare(request, "Inspect");
            string stamp = ProfileStamp(prepared);
            var result = await execution.ExecuteAsync(prepared, _shutdown.Token);
            if (result?.Status != StrategyStatus.Selected) return new(false, result?.Reason ?? "InspectFailed", Decision: result);
            foreach (var key in _tickets.Where(x => _time.GetUtcNow() - x.Value.Created > TimeSpan.FromMinutes(5)).Select(x => x.Key).ToArray()) _tickets.Remove(key);
            if (_tickets.Count >= 64) return new(false, "InspectReceiptLimit");
            string receipt = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
            _tickets.Add(receipt, new(request, _time.GetUtcNow(), stamp));
            return new(true, "InspectReady", receipt, result);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OperationCanceledException)
        { return new(false, "ProfileUnavailableOrInvalid"); }
        finally { _gate.Release(); }
    }

    public async Task<OperatorControlResult> StartRunAsync(string receipt)
    {
        await _gate.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(receipt) || !_tickets.TryGetValue(receipt, out var ticket)) return new(false, "InspectRequired");
            if (ticket.Started) return new(true, "AlreadyAccepted", receipt);
            if (!_active.IsCompleted) return new(false, "ExecutorBusy");
            if (_shutdown.IsCancellationRequested) return new(false, "HostStopping");
            if (_time.GetUtcNow() < ticket.Created || _time.GetUtcNow() - ticket.Created > TimeSpan.FromSeconds(settings.FreshnessSeconds))
                return new(false, "InspectExpired");
            var prepared = Prepare(ticket.Request, "Bounded");
            if (ticket.Profile != ProfileStamp(prepared)) return new(false, "InspectProfileChanged");
            using (var store = Store())
            {
                var saved = store.Load();
                if (saved?.Terminal is not null) return new(false, "RunFinished");
                if (saved is not null && saved.Identity != StagedExecution.RunIdentity(prepared, api, portfolio.Policy()))
                    return new(false, "ExistingRunIdentityMismatch");
            }
            state.ResetForRun();
            _tickets[receipt] = ticket with { Started = true };
            _active = Task.Run(async () =>
            {
                try
                {
                    var result = await execution.ExecuteAsync(prepared, _shutdown.Token);
                    if (result is not null) state.Progress(prepared.RunId, result);
                }
                catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { state.Set("Cancelled"); }
                catch (Exception) { state.Set("ExecutionFailed"); }
            });
            return new(true, "StartAccepted", receipt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { return new(false, "StartRefused"); }
        finally { _gate.Release(); }
    }

    public async Task<OperatorControlResult> RequestStopAsync()
    {
        await _gate.WaitAsync();
        try { state.RequestStop(); return new(true, _active.IsCompleted ? "ExecutorIdle" : "StopRequested"); }
        finally { _gate.Release(); }
    }

    public async Task<OperatorControlResult> ArchiveAsync(string digest)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_active.IsCompleted) return new(false, "ExecutorBusy");
            using var store = Store();
            store.ArchiveCompleted(digest);
            return new(true, "Archived");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { return new(false, "ArchiveRefused"); }
        finally { _gate.Release(); }
    }

    public JsonElement Profile() => JsonSerializer.SerializeToElement(new
    {
        api.Character, portfolio.AutonomousGoals,
        Needs = portfolio.Needs is { } needs ? new { needs.Version, needs.AllowCraft, needs.AllowBankWithdrawal, needs.Reserved } : null,
        settings.AllowActions, settings.LiveActionsApproved,
        Limits = new OperatorRunRequest(settings.RunId, settings.MaxActions, settings.MaxSeconds, settings.MaxDecisions, settings.MaxNoProgress),
        Permissions = new { Move = true, Gather = true, Bank = portfolio.BankRetain is not null,
            Craft = portfolio.Items.Length > 0 || portfolio.Recovery?.AllowCraft == true || portfolio.Needs?.AllowCraft == true || portfolio.Consumable is not null,
            Use = portfolio.Recovery?.AllowUse == true || portfolio.Consumable is not null,
            Rest = portfolio.Recovery?.AllowRest == true || portfolio.Consumable?.AllowRest == true },
        Retain = portfolio.BankRetain, Recovery = portfolio.Recovery,
        Skills = portfolio.Skills, Items = portfolio.Items,
        Consumable = portfolio.Consumable, ProductionReserves = portfolio.ProductionReserves
    });

    public NeedOrderBook Orders() => OrdersStore().Read();

    public async Task<OperatorControlResult> ChangeOrderAsync(NeedOrder order, int expectedRevision)
    {
        await _gate.WaitAsync();
        try
        {
            // Completion is written only from a fresh observation by the executor.
            if (order is null || order.Status is not ("Active" or "Cancelled")) return new(false, "OrderChangeRefused");
            OrdersStore().Put(order, expectedRevision);
            _tickets.Clear();
            return new(true, "OrderSaved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { return new(false, "OrderChangeRefused"); }
        finally { _gate.Release(); }
    }

    private NeedOrderStore OrdersStore() => new(portfolio.Needs?.Directory ?? throw new InvalidOperationException("Needs disabled."), api.Character);

    private ExecutionSettings Prepare(OperatorRunRequest request, string mode)
    {
        if (request is null || request.RunId is null || !System.Text.RegularExpressions.Regex.IsMatch(request.RunId, "^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$") ||
            request.MaxActions <= 0 || request.MaxActions > settings.MaxActions || request.MaxSeconds <= 0 || request.MaxSeconds > settings.MaxSeconds ||
            request.MaxDecisions <= 0 || request.MaxDecisions > settings.MaxDecisions || request.MaxNoProgress <= 0 ||
            request.MaxNoProgress > settings.MaxNoProgress || request.MaxNoProgress > request.MaxDecisions)
            throw new ArgumentException("Invalid run bounds.");
        var policy = portfolio.Policy();
        if (policy.CombatEnabled || portfolio.CombatDiscovery is not null || portfolio.AutonomousCombat is not null ||
            !string.IsNullOrWhiteSpace(portfolio.Equipment) || portfolio.MonsterAlternatives.Length > 0)
            throw new ArgumentException("Operator control is noncombat only.");
        var result = new ExecutionSettings
        {
            Mode = mode, AllowActions = mode == "Bounded" && settings.AllowActions, LiveActionsApproved = settings.LiveActionsApproved,
            ExpectedApiVersion = settings.ExpectedApiVersion, FreshnessSeconds = settings.FreshnessSeconds,
            RunDirectory = settings.RunDirectory, RunId = request.RunId,
            MaxActions = request.MaxActions, MaxSeconds = request.MaxSeconds, MaxDecisions = request.MaxDecisions, MaxNoProgress = request.MaxNoProgress
        };
        result.Validate(api);
        return result;
    }

    private FileRunCheckpointStore Store() => new(settings.RunDirectory, new Uri(api.BaseUrl).GetLeftPart(UriPartial.Authority) + "/" + api.Character);
    private string ProfileStamp(ExecutionSettings prepared) => FileRunCheckpointStore.IdentityDigest(JsonSerializer.Serialize(new
    {
        Identity = StagedExecution.RunIdentity(prepared, api, portfolio.Policy()), settings.RunDirectory,
        settings.AllowActions, settings.LiveActionsApproved, settings.ExpectedApiVersion, settings.FreshnessSeconds
    }));
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WaitForIdleAsync(CancellationToken cancellationToken = default) => _active.WaitAsync(cancellationToken);
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _shutdown.Cancel();
        await RequestStopAsync();
        await _active.WaitAsync(cancellationToken);
    }
}

public sealed class ScopedOperatorExecution(IServiceScopeFactory scopes, ApiSettings api, PortfolioSettings portfolio, OperationState state) : IOperatorExecution
{
    public async Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        return await new StagedExecution(settings, api, portfolio, scope.ServiceProvider.GetRequiredService<StrategySessionFactory>(), state).RunAsync(token);
    }
}
