using System.Collections.Immutable;
using Artiact.Contracts.Client;
using System.Text.Json.Serialization;

namespace Artiact.Services.Strategy;

public enum StrategyStatus { Selected, Completed, Blocked, Replan, Reconciled, UnknownOutcome, Cancelled, CoolingDown, Stopped }
public sealed record StrategyLimits(int Decisions = 100, int NoProgress = 10, int Actions = 100, int DurationSeconds = 3600);
public sealed record StrategyReply(StrategyObservation State, int Cooldown, bool Valid = true, bool Defeat = false);
public sealed record AtomicCommand(string Id, string SourceFingerprint, bool Productive,
    Func<StrategyObservation, bool> Postcondition, Func<CancellationToken, Task<StrategyReply>> Dispatch,
    ImmutableDictionary<string, int>? Charges = null, string? RefillCode = null, bool? Refilling = null);
public sealed record StrategyCandidate(string Id, string Category, decimal Value, decimal ActionSeconds,
    decimal TravelSeconds, decimal RecoverySeconds, string? Rejection, bool Complete, [property: JsonIgnore] AtomicCommand? Command,
    string EstimateSource = "Configured", int Samples = 0, SkillPrerequisite? Prerequisite = null, CombatRoute? CombatRoute = null, PathEstimate? Path = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GoalDiscoveryEvidence? Discovery = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FeasibilityEvidence? Feasibility = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] NeedEvidence? Need = null)
{
    public decimal? TotalSeconds => ActionSeconds is >= 0.001m and <= 1_000_000 &&
        TravelSeconds is >= 0 and <= 1_000_000 && RecoverySeconds is >= 0 and <= 1_000_000
        ? ActionSeconds + TravelSeconds + RecoverySeconds : null;
    public decimal? Score => Rejection is null && !Complete && Value is > 0 and <= 1_000_000 &&
        TotalSeconds is { } total ? Value / total : null;
}
public sealed record SkillPrerequisite(string Parent, string Skill, int Target, string? TrainingItem);
public sealed record FeasibilityEvidence(long FreeUnits, decimal RequiredUnits, decimal DeficitUnits, bool BankConfigured,
    decimal RequiredActions, int RemainingActions, decimal RequiredSeconds, decimal RemainingSeconds, int Unloads = 0);
public sealed record CombatRoute(int Target, string Monster, string? Slot, string? Equipment, long MaximumLoss, decimal PreparationSeconds);
public interface IProgressionStrategy
{
    StrategyCandidate Evaluate(StrategyObservation observation);
    IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation) => [Evaluate(observation)];
}
public interface IStrategyObserver { Task<StrategyObservation> ObserveAsync(CancellationToken cancellationToken); }
public sealed record StrategyDecision(StrategyStatus Status, string Reason, string? Candidate, string? Command,
    ImmutableArray<StrategyCandidate> Candidates, int Decisions, int Attempts, int NoProgress, long CooldownSeconds,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RunFailure? Failure = null);
public sealed record RunFailure(string Operation, string ExceptionType, int ErrorCode, bool ReconciliationRequired);

public sealed class StrategySession(IStrategyObserver observer, IEnumerable<IProgressionStrategy> strategies,
    IMiningCooldownDelay cooldown, StrategyLimits? limits = null, TimeProvider? time = null,
    IRunCheckpointStore? checkpoints = null, string identity = "", MeasurementPolicy? selection = null,
    IReadOnlyDictionary<string, int>? resourceLimits = null, bool inspectOnly = false, bool autonomous = false,
    Func<StrategyObservation, StrategyObservation>? reconcileNeeds = null)
{
    private readonly IProgressionStrategy[] _strategies = strategies.ToArray();
    private readonly StrategyLimits _limits = limits ?? new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    private AtomicCommand? _pending;
    private string? _pendingCandidate;
    private StrategyObservation? _baseline;
    private StrategyDecision? _terminal;
    private ImmutableArray<StrategyCandidate> _candidates = [];
    private int _decisions, _attempts, _noProgress;
    private long _seconds;
    private bool _loaded;
    private bool _storageFailed;
    private string _operation = "Restore";
    private DateTimeOffset _started;
    private SavedObservation? _verified;
    private SavedObservation? _initial, _latest;
    private DateTimeOffset? _finished;
    private bool _newRun;
    private readonly List<JournalCommand> _journal = [];
    private readonly Dictionary<string, ActionMeasurement> _measurements = new(StringComparer.Ordinal);
    private string? _incumbent;
    private AutonomousRunState? _goals = autonomous ? AutonomousRunState.Empty : null;
    public StrategyObservation? State { get; private set; }
    public async Task<StrategyDecision> TickAsync(CancellationToken token = default)
    {
        if (inspectOnly) return Decision(StrategyStatus.Blocked, "AutonomousGoalsRequireInspect");
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            try
            {
                if (_storageFailed) return _terminal!;
                Restore();
                _operation = "Execution";
                var result = await TickCoreAsync(token);
                Save();
                return result;
            }
            catch (Exception ex)
            {
                _loaded = true; _storageFailed = true;
                var failure = new RunFailure(_operation, ex.GetType().Name, ex.HResult,
                    _pending is not null || _operation == "Write" && _attempts > 0);
                return _terminal = Stop(StrategyStatus.Blocked, _operation == "Execution" ? "ExecutionFailed" : "CheckpointUnavailableOrInvalid") with { Failure = failure };
            }
        }
        finally { _gate.Release(); }
    }
    public async Task<StrategyDecision> InspectAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try { return await TickCoreAsync(token, inspect: true); }
        finally { _gate.Release(); }
    }

    private async Task<StrategyDecision> TickCoreAsync(CancellationToken token, bool inspect = false)
    {
        long tickStarted = _time.GetTimestamp();
        if (_terminal is not null) return _terminal;
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        if (_limits.Decisions <= 0 || _limits.NoProgress <= 0 || _limits.NoProgress > _limits.Decisions || _limits.Actions <= 0 || _limits.DurationSeconds <= 0)
            return Stop(StrategyStatus.Blocked, "InvalidLimits");
        if (selection is not null && (selection.UnknownMultiplier is < 1 or > 10 || selection.SwitchRatio is < 1 or > 10))
            return Stop(StrategyStatus.Blocked, "InvalidMeasurementPolicy");
        if (_pending is null && _noProgress >= _limits.NoProgress) return Stop(StrategyStatus.Blocked, "BudgetExhausted");
        if (_pending is null && (_decisions >= _limits.Decisions || _attempts >= _limits.Actions ||
            _loaded && _time.GetUtcNow() - _started >= TimeSpan.FromSeconds(_limits.DurationSeconds)))
            return Stop(autonomous ? StrategyStatus.Stopped : StrategyStatus.Blocked, autonomous ? "AutonomousBudgetExhausted" : "BudgetExhausted");
        if (!inspect) { _decisions++; Save(); }
        try { State = (await observer.ObserveAsync(token)).WithContext(RunContext()); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(_pending is null ? StrategyStatus.Blocked : StrategyStatus.UnknownOutcome, "ObservationFailed"); }
        _latest = SavedObservation.From(State, _time.GetUtcNow());
        if (_newRun && _initial is null) _initial = _latest;
        if (_pending is not null)
        {
            var pending = _pending;
            if (!Matches(pending, State, _baseline!)) return Stop(StrategyStatus.UnknownOutcome, "UnresolvedOutcome");
            _consumed.Add(Key(pending)); _pending = null;
            _verified = SavedObservation.From(State, _time.GetUtcNow());
            RecordOutcome("Reconciled", State.Fingerprint);
            if (_goals is not null) _goals = _goals.CompleteObserved(State);
            if (pending.Productive) _noProgress = 0;
            return Decision(StrategyStatus.Reconciled, "PostconditionObserved", command: pending.Id);
        }
        try
        {
            if (string.IsNullOrWhiteSpace(State.Name)) return Stop(StrategyStatus.Blocked, "InvalidObservation");
            if (!State.Character.GetProperty("cooldown_expiration").TryGetDateTimeOffset(out var expiration))
                return Stop(StrategyStatus.Blocked, "InvalidCooldown");
            if (expiration > _time.GetUtcNow()) return Decision(StrategyStatus.CoolingDown, "CooldownPending");
            if (_goals is not null)
            {
                _goals = _goals.CompleteObserved(State);
                // Reevaluate the active milestone against fresh catalogs below. A world change
                // alone does not prove that its route is infeasible.
                if (_goals.History.Length >= 128) return Stop(StrategyStatus.Blocked, "AutonomousHistoryExhausted");
                State = State.WithContext(RunContext());
            }
            if (!inspect && reconcileNeeds is not null)
            {
                State = reconcileNeeds(State);
                _latest = SavedObservation.From(State, _time.GetUtcNow());
            }
            _candidates = _strategies.SelectMany(x => x.EvaluateAll(State)).Select(Measured).OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray();
            if (autonomous && !_candidates.Any(x => x.Score.HasValue && x.Command is not null) &&
                _candidates.Any(x => x.Rejection == "EstimatedPathExceedsBudget"))
                return Stop(StrategyStatus.Stopped, "AutonomousBudgetExhausted");
            if (_goals?.Active is not null && !_candidates.Any(x => x.Score.HasValue && x.Command is not null))
            {
                _goals = _goals.Transition("Rejected", State.Fingerprint);
                State = State.WithContext(RunContext());
                _candidates = _strategies.SelectMany(x => x.EvaluateAll(State)).Select(Measured).OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray();
            }
            if (_candidates.Length == 0 || _candidates.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != _candidates.Length ||
                _candidates.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Value is <= 0 or > 1_000_000 ||
                    x.ActionSeconds is < 0.001m or > 1_000_000 || x.TravelSeconds is < 0 or > 1_000_000 || x.RecoverySeconds is < 0 or > 1_000_000))
                return Stop(StrategyStatus.Blocked, "InvalidPolicy");
        }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "InvalidObservationOrPolicy"); }
        if (autonomous && _candidates.Length is 4 or 5 && _candidates.All(x => x.Rejection == "SupportedSkillCapReached"))
            return Stop(StrategyStatus.Stopped, "NoUsefulSupportedGoals");
        if (autonomous && _candidates.Length == 1 && _candidates[0] is { Category: "need", Rejection: "NoActiveSupportedNeeds", Complete: true })
            return inspect ? Decision(StrategyStatus.Stopped, "NoActiveSupportedNeeds") : Stop(StrategyStatus.Stopped, "NoActiveSupportedNeeds");
        if (_candidates.All(x => x.Complete)) return Stop(StrategyStatus.Completed, "TargetsReached");
        var selected = _candidates.Where(x => x.Score.HasValue && x.Command is not null)
            .OrderByDescending(x => x.Need?.Priority ?? 0).ThenByDescending(x => x.Score).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (selected is null) return Stop(StrategyStatus.Blocked, "NoFeasibleCandidate");
        if (selection is not null && selected.Need is null && _candidates.SingleOrDefault(x => x.Id == _incumbent && x.Score.HasValue && x.Command is not null) is { } incumbent &&
            selected.Score <= incumbent.Score * selection.SwitchRatio) selected = incumbent;
        var command = selected.Command!;
        if (inspect) return Decision(StrategyStatus.Selected, "InspectOnly", selected.Id, command.Id);
        if (_goals is not null && selected.Discovery is not null)
        {
            _goals = _goals with { Active = selected.Discovery, ActiveWorld = State.WorldFingerprint };
            State = State.WithContext(RunContext());
            Save();
        }
        if (_goals is not null && selected.Need is not null)
        {
            _goals = _goals with { Need = selected.Need };
            State = State.WithContext(RunContext());
            Save();
        }
        if (command.SourceFingerprint != State.Fingerprint || _consumed.Contains(Key(command)))
            return Stop(StrategyStatus.Blocked, "ConsumedOrInvalidCommand");
        if (command.Charges is not null && command.Charges.Any(x => x.Value <= 0 || resourceLimits is null ||
                !resourceLimits.TryGetValue(x.Key, out int maximum) || (long)State.Context.Used.GetValueOrDefault(x.Key) + x.Value > maximum))
            return Stop(StrategyStatus.Blocked, "ResourceBudgetExhausted");
        _baseline = State;
        StrategyObservation preflight;
        try { preflight = (await observer.ObserveAsync(token)).WithContext(RunContext()); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "PreflightFailed"); }
        State = preflight;
        _latest = SavedObservation.From(State, _time.GetUtcNow());
        if (preflight.Fingerprint != command.SourceFingerprint)
        { _noProgress++; return Decision(StrategyStatus.Replan, "StaleObservation", selected.Id); }
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        if (_loaded && _time.GetUtcNow() - _started >= TimeSpan.FromSeconds(_limits.DurationSeconds))
            return Stop(autonomous ? StrategyStatus.Stopped : StrategyStatus.Blocked, autonomous ? "AutonomousBudgetExhausted" : "BudgetExhausted");
        _attempts++; _noProgress++;
        _pending = command;
        _pendingCandidate = selected.Id;
        _journal.Add(new(command.Id, command.SourceFingerprint, "Intent", Charges: command.Charges, RefillCode: command.RefillCode, Refilling: command.Refilling,
            Candidate: selected.Id, MeasurementContext: selection?.FullPaths == true ? MeasurementContext.Key(_baseline, selected, command.Id) : null));
        Save();
        long dispatched = _time.GetTimestamp();
        StrategyReply reply;
        try { reply = await command.Dispatch(token); }
        catch (ActionFailureException ex) when (ex.Kind != ActionFailureKind.UnknownOutcome)
        { _pending = null; RecordOutcome(ex.Kind.ToString()); return Stop(StrategyStatus.Blocked, ex.Kind.ToString()); }
        catch (Exception)
        {
            _pending = command;
            return Decision(StrategyStatus.UnknownOutcome, "DispatchOutcomeUnknown", selected.Id, command.Id);
        }
        State = reply.State.WithContext(RunContext());
        _latest = SavedObservation.From(State, _time.GetUtcNow());
        _pending = null;
        _consumed.Add(Key(command));
        if (reply.Defeat) { RecordOutcome("Defeat"); return Stop(StrategyStatus.Blocked, "Defeat"); }
        if (!reply.Valid || reply.Cooldown < 0 || !Matches(command, State, _baseline))
        { RecordOutcome("InvalidPostcondition"); return Stop(StrategyStatus.Blocked, "InvalidPostcondition"); }
        _verified = SavedObservation.From(State, _time.GetUtcNow());
        RecordOutcome("Verified", State.Fingerprint);
        if (_goals is not null) _goals = _goals.CompleteObserved(State);
        var facts = ActionFacts.Read(_baseline, State, selected, _time.GetElapsedTime(tickStarted, dispatched).TotalSeconds, _time.GetElapsedTime(dispatched).TotalSeconds);
        _journal[^1] = _journal[^1] with { Facts = facts };
        if (selection is not null && reply.Cooldown > 0)
        {
            string key = selection.FullPaths ? MeasurementContext.Key(_baseline, selected, command.Id) : MeasureKey(selected);
            var old = _measurements.GetValueOrDefault(key) ?? new(0, 0, 0);
            _measurements[key] = new(checked(old.Seconds + reply.Cooldown), checked(old.Samples + 1), checked(old.Progress + (command.Productive ? 1 : 0)),
                old.UsefulProgress + Math.Max(0, facts.UsefulProgress ?? 0), old.ProgressSamples + (facts.UsefulProgress.HasValue ? 1 : 0));
            _incumbent = selected.Id;
        }
        _seconds += reply.Cooldown;
        if (command.Productive) _noProgress = 0;
        Save();
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        using var deadline = autonomous ? CancellationTokenSource.CreateLinkedTokenSource(token) : null;
        if (deadline is not null)
        {
            var remaining = TimeSpan.FromSeconds(_limits.DurationSeconds) - (_time.GetUtcNow() - _started);
            if (remaining <= TimeSpan.Zero) return Stop(StrategyStatus.Stopped, "AutonomousBudgetExhausted");
            deadline.CancelAfter(remaining);
        }
        long waiting = _time.GetTimestamp();
        try { await cooldown.WaitAsync(reply.Cooldown, deadline?.Token ?? token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (OperationCanceledException) when (deadline?.IsCancellationRequested == true) { return Stop(StrategyStatus.Stopped, "AutonomousBudgetExhausted"); }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "CooldownFailed"); }
        _journal[^1] = _journal[^1] with { Facts = facts with { WaitSeconds = _time.GetElapsedTime(waiting).TotalSeconds } };
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        return Decision(StrategyStatus.Selected, "CommandVerified", selected.Id, command.Id);
    }
    private static bool Matches(AtomicCommand command, StrategyObservation after, StrategyObservation before)
    {
        try { return before.SameWorld(after) && (command.Id.StartsWith("Deposit:", StringComparison.Ordinal) || command.Id.StartsWith("Withdraw:", StringComparison.Ordinal) || BankPrerequisite.SameBank(before.Bank, after.Bank)) && command.Postcondition(after); }
        catch (Exception) { return false; }
    }
    private void Restore()
    {
        if (_loaded) return;
        _started = _time.GetUtcNow();
        _operation = "Read";
        var saved = checkpoints?.Load();
        _operation = "Restore";
        _newRun = saved is null;
        if (saved is not null)
        {
            if (saved.Version != (autonomous ? 2 : 1) || autonomous && saved.Autonomous is not { Valid: true, Algorithm: AutonomousGoalDiscovery.Version } || !autonomous && saved.Autonomous is not null ||
                saved.Identity != identity || saved.Decisions < 0 || saved.Attempts < 0 ||
                saved.NoProgress < 0 || saved.Seconds < 0 || saved.Started > _started || saved.Consumed is null || saved.Journal.IsDefault ||
                saved.Attempts != saved.Journal.Length || saved.Attempts > saved.Decisions)
                throw new InvalidOperationException("Incompatible checkpoint");
            _started = saved.Started; _decisions = saved.Decisions; _attempts = saved.Attempts;
            _goals = saved.Autonomous;
            _noProgress = saved.NoProgress; _seconds = saved.Seconds; _terminal = saved.Terminal;
            _consumed.UnionWith(saved.Consumed);
            _journal.AddRange(saved.Journal); _verified = saved.Verified;
            _ = RunContext();
            _initial = saved.Initial; _latest = saved.Latest; _finished = saved.Finished;
            if (saved.Measurements is not null)
                foreach (var entry in saved.Measurements)
                {
                    if (entry.Value.Samples <= 0 || entry.Value.Seconds <= 0 || entry.Value.Progress < 0 || entry.Value.UsefulProgress < 0 ||
                        entry.Value.ProgressSamples < 0 || entry.Value.ProgressSamples > entry.Value.Samples) throw new InvalidOperationException("Invalid measurements");
                    _measurements.Add(entry.Key, entry.Value);
                }
            _incumbent = saved.Incumbent;
            if (saved.PendingCommand is not null)
            {
                _baseline = saved.Baseline?.Restore() ?? throw new InvalidOperationException("Missing baseline");
                _pendingCandidate = saved.PendingCandidate;
                _pending = _strategies.SelectMany(x => x.EvaluateAll(_baseline))
                    .Where(x => saved.PendingCandidate is null || x.Id == saved.PendingCandidate).Select(x => x.Command)
                    .Single(x => x?.Id == saved.PendingCommand) ?? throw new InvalidOperationException("Unknown command");
                if (_pending.SourceFingerprint != _baseline.Fingerprint) throw new InvalidOperationException("Invalid baseline");
            }
        }
        _loaded = true;
        Save();
    }
    private void Save()
    {
        var previous = _operation;
        _operation = "Write";
        checkpoints?.Save(new(autonomous ? 2 : 1, identity, _started, _decisions, _attempts, _noProgress, _seconds,
        _consumed.ToArray(), _pending?.Id, _baseline is null ? null : SavedObservation.From(_baseline), _terminal, _verified, _journal.ToImmutableArray(),
        _measurements.ToImmutableDictionary(StringComparer.Ordinal), _incumbent, _initial, _latest, _finished, _pending is null ? null : _pendingCandidate, _goals));
        _operation = previous;
    }
    private static string MeasureKey(StrategyCandidate candidate) => candidate.Id + ":" + candidate.Command!.Id.Split(':')[0];
    private StrategyRunContext RunContext()
    {
        var used = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        var refilling = ImmutableDictionary.CreateBuilder<string, bool>(StringComparer.Ordinal);
        foreach (var entry in _journal)
        {
            foreach (var charge in entry.Charges ?? ImmutableDictionary<string, int>.Empty)
            {
                if (charge.Value <= 0 || resourceLimits is null || !resourceLimits.TryGetValue(charge.Key, out int maximum))
                    throw new InvalidOperationException("Invalid resource journal.");
                used[charge.Key] = checked(used.GetValueOrDefault(charge.Key) + charge.Value);
                if (used[charge.Key] > maximum) throw new InvalidOperationException("Invalid resource journal.");
            }
            if (entry.RefillCode is not null)
            {
                if (string.IsNullOrWhiteSpace(entry.RefillCode) || entry.Refilling is null) throw new InvalidOperationException("Invalid refill journal.");
                refilling[entry.RefillCode] = entry.Refilling.Value;
            }
        }
        return new(used.ToImmutable(), refilling.ToImmutable(), _goals,
            autonomous ? Math.Max(0, _limits.Actions - _attempts) : null,
            autonomous ? Math.Max(0, _limits.DurationSeconds - (_loaded ? (decimal)(_time.GetUtcNow() - _started).TotalSeconds : 0)) : null,
            Math.Max(0, _limits.Decisions - _decisions), _noProgress, _limits.NoProgress);
    }
    private StrategyCandidate Measured(StrategyCandidate candidate)
    {
        if (selection is null || candidate.Command is null || candidate.Need is not null) return candidate;
        if (selection.FullPaths)
        {
            if (candidate.Path is not { } path || path.Remaining <= 0 || path.ExpectedProgress <= 0 || path.UnitSeconds <= 0)
                return candidate with { Rejection = candidate.Rejection ?? "UnsupportedPathEstimate", Command = null };
            string context = MeasurementContext.Key(State!, candidate, path.WorkCommand);
            var measured = _measurements.GetValueOrDefault(context);
            decimal progress = measured is { ProgressSamples: > 0 } ? measured.UsefulProgress / measured.ProgressSamples : path.ExpectedProgress;
            if (progress <= 0) return candidate with { Rejection = "NoMeasuredGoalProgress", Command = null };
            decimal unit = measured is null ? path.UnitSeconds * selection.UnknownMultiplier : (decimal)measured.Seconds / measured.Samples;
            decimal work = Math.Ceiling(path.Remaining / progress) * unit;
            return candidate with { ActionSeconds = Math.Max(0.001m, work + path.PreparationSeconds * selection.UnknownMultiplier),
                TravelSeconds = path.TravelSeconds * selection.UnknownMultiplier, RecoverySeconds = path.RecoverySeconds * selection.UnknownMultiplier,
                Samples = measured?.Samples ?? 0, EstimateSource = measured is null ? "AssumedFullPath" : "ObservedProgressWithAssumedPath",
                Path = path with { ExpectedProgress = progress, UnitSeconds = unit, Context = context } };
        }
        var sample = _measurements.GetValueOrDefault(MeasureKey(candidate));
        decimal estimate = sample is null ? 0 : Math.Max(0.001m, (decimal)sample.Seconds / sample.Samples);
        bool move = candidate.Command.Id.StartsWith("Move:", StringComparison.Ordinal);
        bool rest = candidate.Command.Id == "Rest";
        return candidate with
        {
            ActionSeconds = !move && !rest && sample is not null ? estimate : candidate.ActionSeconds * selection.UnknownMultiplier,
            TravelSeconds = move && sample is not null ? estimate : candidate.TravelSeconds * selection.UnknownMultiplier,
            RecoverySeconds = rest && sample is not null ? estimate : candidate.RecoverySeconds * selection.UnknownMultiplier,
            EstimateSource = sample is null ? "Assumed" : "ObservedWithAssumedPrerequisites", Samples = sample?.Samples ?? 0
        };
    }
    private void RecordOutcome(string status, string? fingerprint = null)
    {
        if (_journal.Count > 0) _journal[^1] = _journal[^1] with { Status = status, ResultFingerprint = fingerprint };
    }
    private static string Key(AtomicCommand command) => command.SourceFingerprint + ":" + command.Id;
    private StrategyDecision Stop(StrategyStatus status, string reason)
    {
        _finished ??= _time.GetUtcNow();
        return _terminal = Decision(status, reason);
    }
    private StrategyDecision Decision(StrategyStatus status, string reason, string? candidate = null, string? command = null) =>
        new(status, reason, candidate, command, _candidates, _decisions, _attempts, _noProgress, _seconds);
}
