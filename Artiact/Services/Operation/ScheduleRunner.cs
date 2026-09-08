using Artiact.Services.Strategy;
using System.Text.Json;

namespace Artiact.Services.Operation;

public sealed class ScheduleSettings
{
    public bool Enabled { get; set; }
    public bool LiveSeriesApproved { get; set; }
    public string SeriesId { get; set; } = "";
    public DateTimeOffset FirstRunUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public int IntervalSeconds { get; set; } = 60;
    public int MaxRuns { get; set; }
    public int MaxTotalActions { get; set; }
    public int MaxTotalDecisions { get; set; }
    public int MaxTotalSeconds { get; set; }

    public void Validate(ExecutionSettings execution, ApiSettings api, PortfolioSettings portfolio)
    {
        if (!Enabled) return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(SeriesId ?? "", "^[A-Za-z0-9][A-Za-z0-9_-]{0,59}$") ||
            FirstRunUtc == default || ExpiresUtc <= FirstRunUtc || ExpiresUtc - FirstRunUtc > TimeSpan.FromDays(31) ||
            IntervalSeconds is < 1 or > 86400 || MaxRuns is < 1 or > 10000 || MaxTotalActions is < 1 or > 1000000 ||
            MaxTotalDecisions is < 1 or > 2000000 || MaxTotalSeconds is < 1 or > 2678400 ||
            !string.Equals(execution.Mode, "Inspect", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(execution.RunDirectory))
            throw new ArgumentException("Invalid finite schedule.");
        var policy = portfolio.Policy();
        if (policy.Needs is not null || !policy.AutonomousGoals || !policy.Skills.IsDefaultOrEmpty || !policy.Items.IsDefaultOrEmpty ||
            portfolio.Recovery is not null || portfolio.CombatDiscovery is not null || portfolio.AutonomousCombat is not null ||
            portfolio.BankRetain is not null || portfolio.Consumable is not null || portfolio.Preparation is not null ||
            policy.CombatEnabled || !string.IsNullOrEmpty(portfolio.Equipment) || portfolio.MonsterAlternatives.Length != 0)
            throw new ArgumentException("Schedule supports autonomous gathering only.");
        if (!new Uri(api.BaseUrl).IsLoopback && !LiveSeriesApproved) throw new ArgumentException("Live series approval required.");
    }
}

public sealed record ScheduleNotification(long Id, DateTimeOffset At, string Kind, string Reason, string? RunId);
public sealed record ScheduleSnapshot(string Status, string Reason, int ReservedRuns, int ReservedActions,
    int ReservedDecisions, int ReservedSeconds, DateTimeOffset NextDue, string? RunId, ScheduleNotification[] Notifications);
public interface IScheduledRun
{
    Task<StrategyDecision?> InspectAsync(ExecutionSettings settings, CancellationToken token);
    Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token);
    Task ArchiveAsync(ExecutionSettings settings);
}

public sealed class ScheduleRunner(ScheduleSettings schedule, ExecutionSettings execution, ApiSettings api,
    PortfolioSettings portfolio, OperationState state, IScheduledRun run, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private volatile ScheduleSnapshot _snapshot = new("Waiting", "NotInitialized", 0, 0, 0, 0, schedule.FirstRunUtc, null, []);
    private bool _faulted;
    public ScheduleSnapshot Snapshot() => _snapshot;
    public void RequestStop() { _stop.Cancel(); state.RequestStop(); }

    public async Task TickAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_faulted) return;
            if (!schedule.Enabled) { _snapshot = _snapshot with { Status = "Disabled" }; return; }
            schedule.Validate(execution, api, portfolio);
            var prepared = Prepare(schedule.SeriesId + "-0001", "Bounded");
            prepared.Validate(api);
            string Identity() => FileRunCheckpointStore.IdentityDigest(JsonSerializer.Serialize(new
            {
                schedule.SeriesId, schedule.FirstRunUtc, schedule.ExpiresUtc, schedule.IntervalSeconds, schedule.MaxRuns,
                schedule.MaxTotalActions, schedule.MaxTotalDecisions, schedule.MaxTotalSeconds, schedule.LiveSeriesApproved,
                api.BaseUrl, api.Character, Policy = portfolio.Policy().Identity,
                execution.MaxActions, execution.MaxDecisions, execution.MaxNoProgress, execution.MaxSeconds,
                execution.FreshnessSeconds, execution.ExpectedApiVersion, execution.AllowActions, execution.LiveActionsApproved
            }));
            string identity = Identity();
            using var store = new ScheduleStore(execution.RunDirectory);
            var loaded = store.Load();
            var saved = loaded ?? new ScheduleRecord(1, identity, 0, _time.GetUtcNow(),
                new("Waiting", "Scheduled", 0, 0, 0, 0, schedule.FirstRunUtc, null, []));
            Validate(saved, identity);
            _snapshot = saved.Snapshot;
            if (_snapshot.Status is "Completed" or "Stopped" or "InterventionRequired") return;
            void Save(ScheduleSnapshot next)
            {
                saved = saved with { Snapshot = next, UpdatedAt = _time.GetUtcNow() };
                try { store.Save(saved); }
                catch (Exception) { throw new SchedulePersistenceException(); }
                _snapshot = next;
            }
            void Notify(string status, string kind, string reason)
            {
                long sequence = checked(saved.Sequence + 1);
                var notification = new ScheduleNotification(sequence, _time.GetUtcNow(), kind, reason, _snapshot.RunId);
                saved = saved with { Sequence = sequence };
                Save(_snapshot with { Status = status, Reason = reason, Notifications = _snapshot.Notifications.Append(notification).TakeLast(100).ToArray() });
            }
            if (_snapshot.Status == "Running") { Notify("InterventionRequired", "InterventionRequired", "InterruptedReservation"); return; }
            if (_stop.IsCancellationRequested || token.IsCancellationRequested)
            { Notify("Stopped", "SeriesStopped", "StopRequested"); return; }
            var now = _time.GetUtcNow();
            if (_snapshot.ReservedRuns >= schedule.MaxRuns ||
                (long)_snapshot.ReservedActions + execution.MaxActions > schedule.MaxTotalActions ||
                (long)_snapshot.ReservedDecisions + execution.MaxDecisions > schedule.MaxTotalDecisions ||
                (long)_snapshot.ReservedSeconds + execution.MaxSeconds > schedule.MaxTotalSeconds)
            { Notify("Completed", "SeriesCompleted", "SeriesBudgetExhausted"); return; }
            if (now.AddSeconds(execution.MaxSeconds) > schedule.ExpiresUtc)
            { Notify("Completed", "SeriesCompleted", "SeriesWindowExhausted"); return; }
            if (now < _snapshot.NextDue) { if (loaded is null) store.Save(saved); return; }
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
            prepared = Prepare(schedule.SeriesId + "-" + (_snapshot.ReservedRuns + 1).ToString("D4"), "Inspect");
            try
            {
                state.ResetForRun();
                var inspect = await run.InspectAsync(prepared, linked.Token);
                if (linked.IsCancellationRequested) { Notify("Stopped", "SeriesStopped", "StopRequested"); return; }
                schedule.Validate(execution, api, portfolio);
                if (identity != Identity()) throw new InvalidOperationException("Series configuration changed during inspection.");
                if (inspect?.Status != StrategyStatus.Selected)
                {
                    bool complete = inspect?.Reason == "NoUsefulSupportedGoals";
                    Notify(complete ? "Completed" : "InterventionRequired", complete ? "SeriesCompleted" : "InterventionRequired", inspect?.Reason ?? "InspectFailed"); return;
                }
                // Inspection time counts toward the finite series window too.
                if (_time.GetUtcNow().AddSeconds(prepared.MaxSeconds) > schedule.ExpiresUtc)
                { Notify("Completed", "SeriesCompleted", "SeriesWindowExhausted"); return; }
                Save(_snapshot with { Status = "Running", Reason = "Reserved", RunId = prepared.RunId,
                    ReservedRuns = _snapshot.ReservedRuns + 1, ReservedActions = _snapshot.ReservedActions + prepared.MaxActions,
                    ReservedDecisions = _snapshot.ReservedDecisions + prepared.MaxDecisions, ReservedSeconds = _snapshot.ReservedSeconds + prepared.MaxSeconds });
                prepared.Mode = "Bounded";
                var result = await run.ExecuteAsync(prepared, linked.Token);
                if (linked.IsCancellationRequested) { Notify("Stopped", "SeriesStopped", "StopRequested"); return; }
                if (result is not { Status: StrategyStatus.Stopped, Reason: "AutonomousBudgetExhausted" or "NoUsefulSupportedGoals" })
                { Notify("InterventionRequired", result?.Status == StrategyStatus.Blocked ? "RunFailed" : "InterventionRequired", result?.Reason ?? "ExecutionFailed"); return; }
                await run.ArchiveAsync(prepared);
                _snapshot = _snapshot with { NextDue = _time.GetUtcNow().AddSeconds(schedule.IntervalSeconds) };
                Notify("Waiting", "RunCompleted", result.Reason);
                if (_snapshot.ReservedRuns >= schedule.MaxRuns ||
                    (long)_snapshot.ReservedActions + execution.MaxActions > schedule.MaxTotalActions ||
                    (long)_snapshot.ReservedDecisions + execution.MaxDecisions > schedule.MaxTotalDecisions ||
                    (long)_snapshot.ReservedSeconds + execution.MaxSeconds > schedule.MaxTotalSeconds)
                    Notify("Completed", "SeriesCompleted", "SeriesBudgetExhausted");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or OperationCanceledException)
            {
                if (linked.IsCancellationRequested) Notify("Stopped", "SeriesStopped", "StopRequested");
                else Notify("InterventionRequired", "RunFailed", "RunUnavailableOrInvalid");
            }
        }
        catch (Exception)
        {
            _faulted = true;
            _snapshot = _snapshot with { Status = "InterventionRequired", Reason = "ScheduleStorageOrConfigurationInvalid" };
        }
        finally { _gate.Release(); }
    }

    private sealed class SchedulePersistenceException : Exception;
    private ExecutionSettings Prepare(string runId, string mode) => new()
    {
        Mode = mode, RunId = runId, RunDirectory = execution.RunDirectory, AllowActions = execution.AllowActions,
        LiveActionsApproved = execution.LiveActionsApproved, ExpectedApiVersion = execution.ExpectedApiVersion,
        FreshnessSeconds = execution.FreshnessSeconds, MaxActions = execution.MaxActions, MaxDecisions = execution.MaxDecisions,
        MaxNoProgress = execution.MaxNoProgress, MaxSeconds = execution.MaxSeconds
    };
    private void Validate(ScheduleRecord saved, string identity)
    {
        var value = saved.Snapshot;
        if (saved.Version != 1 || saved.Identity != identity || saved.Sequence < 0 || saved.UpdatedAt > _time.GetUtcNow() || value is null ||
            value.Status is not ("Waiting" or "Running" or "Completed" or "Stopped" or "InterventionRequired") ||
            value.NextDue < schedule.FirstRunUtc || value.Status == "Running" && value.ReservedRuns == 0 ||
            value.ReservedRuns < 0 || value.ReservedRuns > schedule.MaxRuns ||
            value.ReservedActions != (long)value.ReservedRuns * execution.MaxActions || value.ReservedActions > schedule.MaxTotalActions ||
            value.ReservedDecisions != (long)value.ReservedRuns * execution.MaxDecisions || value.ReservedDecisions > schedule.MaxTotalDecisions ||
            value.ReservedSeconds != (long)value.ReservedRuns * execution.MaxSeconds || value.ReservedSeconds > schedule.MaxTotalSeconds ||
            value.Notifications is null || value.Notifications.Length > 100 || value.Notifications.Any(x => x is null || x.Id < 1 || x.Id > saved.Sequence) ||
            value.Notifications.Select(x => x.Id).Distinct().Count() != value.Notifications.Length ||
            (value.Notifications.LastOrDefault()?.Id ?? 0) != saved.Sequence ||
            !value.Notifications.Select(x => x.Id).SequenceEqual(value.Notifications.Select(x => x.Id).Order()) ||
            value.ReservedRuns > 0 && value.RunId != schedule.SeriesId + "-" + value.ReservedRuns.ToString("D4"))
            throw new IOException("Invalid or changed series state.");
    }
}
