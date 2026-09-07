using System.Collections.Immutable;
using Artiact.Contracts.Client;
using System.Text.Json.Serialization;

namespace Artiact.Services.Strategy;

public enum StrategyStatus { Selected, Completed, Blocked, Replan, Reconciled, UnknownOutcome, Cancelled, CoolingDown }
public sealed record StrategyLimits(int Decisions = 100, int NoProgress = 10, int Actions = 100, int DurationSeconds = 3600);
public sealed record StrategyReply(StrategyObservation State, int Cooldown, bool Valid = true, bool Defeat = false);
public sealed record AtomicCommand(string Id, string SourceFingerprint, bool Productive,
    Func<StrategyObservation, bool> Postcondition, Func<CancellationToken, Task<StrategyReply>> Dispatch);
public sealed record StrategyCandidate(string Id, string Category, decimal Value, decimal ActionSeconds,
    decimal TravelSeconds, decimal RecoverySeconds, string? Rejection, bool Complete, [property: JsonIgnore] AtomicCommand? Command)
{
    public decimal? TotalSeconds => ActionSeconds is >= 0.001m and <= 1_000_000 &&
        TravelSeconds is >= 0 and <= 1_000_000 && RecoverySeconds is >= 0 and <= 1_000_000
        ? ActionSeconds + TravelSeconds + RecoverySeconds : null;
    public decimal? Score => Rejection is null && !Complete && Value is > 0 and <= 1_000_000 &&
        TotalSeconds is { } total ? Value / total : null;
}
public interface IProgressionStrategy { StrategyCandidate Evaluate(StrategyObservation observation); }
public interface IStrategyObserver { Task<StrategyObservation> ObserveAsync(CancellationToken cancellationToken); }
public sealed record StrategyDecision(StrategyStatus Status, string Reason, string? Candidate, string? Command,
    ImmutableArray<StrategyCandidate> Candidates, int Decisions, int Attempts, int NoProgress, long CooldownSeconds);

public sealed class StrategySession(IStrategyObserver observer, IEnumerable<IProgressionStrategy> strategies,
    IMiningCooldownDelay cooldown, StrategyLimits? limits = null, TimeProvider? time = null,
    IRunCheckpointStore? checkpoints = null, string identity = "")
{
    private readonly IProgressionStrategy[] _strategies = strategies.ToArray();
    private readonly StrategyLimits _limits = limits ?? new();
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    private AtomicCommand? _pending;
    private StrategyObservation? _baseline;
    private StrategyDecision? _terminal;
    private ImmutableArray<StrategyCandidate> _candidates = [];
    private int _decisions, _attempts, _noProgress;
    private long _seconds;
    private bool _loaded;
    private bool _storageFailed;
    private DateTimeOffset _started;
    private SavedObservation? _verified;
    private readonly List<JournalCommand> _journal = [];
    public StrategyObservation? State { get; private set; }
    public async Task<StrategyDecision> TickAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            try
            {
                if (_storageFailed) return _terminal!;
                Restore();
                var result = await TickCoreAsync(token);
                Save();
                return result;
            }
            catch (Exception) { _loaded = true; _storageFailed = true; return Stop(StrategyStatus.Blocked, "CheckpointUnavailableOrInvalid"); }
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
        if (_terminal is not null) return _terminal;
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        if (_limits.Decisions <= 0 || _limits.NoProgress <= 0 || _limits.NoProgress > _limits.Decisions || _limits.Actions <= 0 || _limits.DurationSeconds <= 0)
            return Stop(StrategyStatus.Blocked, "InvalidLimits");
        if (_pending is null && (_decisions >= _limits.Decisions || _noProgress >= _limits.NoProgress || _attempts >= _limits.Actions ||
            _loaded && _time.GetUtcNow() - _started >= TimeSpan.FromSeconds(_limits.DurationSeconds)))
            return Stop(StrategyStatus.Blocked, "BudgetExhausted");
        if (!inspect) { _decisions++; Save(); }
        try { State = await observer.ObserveAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(_pending is null ? StrategyStatus.Blocked : StrategyStatus.UnknownOutcome, "ObservationFailed"); }
        if (_pending is not null)
        {
            var pending = _pending;
            if (!Matches(pending, State, _baseline!)) return Stop(StrategyStatus.UnknownOutcome, "UnresolvedOutcome");
            _consumed.Add(Key(pending)); _pending = null;
            _verified = SavedObservation.From(State);
            RecordOutcome("Reconciled", State.Fingerprint);
            if (pending.Productive) _noProgress = 0;
            return Decision(StrategyStatus.Reconciled, "PostconditionObserved", command: pending.Id);
        }
        try
        {
            if (string.IsNullOrWhiteSpace(State.Name)) return Stop(StrategyStatus.Blocked, "InvalidObservation");
            if (!State.Character.GetProperty("cooldown_expiration").TryGetDateTimeOffset(out var expiration))
                return Stop(StrategyStatus.Blocked, "InvalidCooldown");
            if (expiration > _time.GetUtcNow()) return Decision(StrategyStatus.CoolingDown, "CooldownPending");
            _candidates = _strategies.Select(x => x.Evaluate(State)).OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray();
            if (_candidates.Length == 0 || _candidates.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != _candidates.Length ||
                _candidates.Any(x => string.IsNullOrWhiteSpace(x.Id) || x.Value is <= 0 or > 1_000_000 ||
                    x.ActionSeconds is < 0.001m or > 1_000_000 || x.TravelSeconds is < 0 or > 1_000_000 || x.RecoverySeconds is < 0 or > 1_000_000))
                return Stop(StrategyStatus.Blocked, "InvalidPolicy");
        }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "InvalidObservationOrPolicy"); }
        if (_candidates.All(x => x.Complete)) return Stop(StrategyStatus.Completed, "TargetsReached");
        var selected = _candidates.Where(x => x.Score.HasValue && x.Command is not null)
            .OrderByDescending(x => x.Score).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (selected is null) return Stop(StrategyStatus.Blocked, "NoFeasibleCandidate");
        var command = selected.Command!;
        if (inspect) return Decision(StrategyStatus.Selected, "InspectOnly", selected.Id, command.Id);
        if (command.SourceFingerprint != State.Fingerprint || _consumed.Contains(Key(command)))
            return Stop(StrategyStatus.Blocked, "ConsumedOrInvalidCommand");
        _baseline = State;
        StrategyObservation preflight;
        try { preflight = await observer.ObserveAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "PreflightFailed"); }
        State = preflight;
        if (preflight.Fingerprint != command.SourceFingerprint)
        { _noProgress++; return Decision(StrategyStatus.Replan, "StaleObservation", selected.Id); }
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        if (_loaded && _time.GetUtcNow() - _started >= TimeSpan.FromSeconds(_limits.DurationSeconds))
            return Stop(StrategyStatus.Blocked, "BudgetExhausted");
        _attempts++; _noProgress++;
        _pending = command;
        _journal.Add(new(command.Id, command.SourceFingerprint, "Intent"));
        Save();
        StrategyReply reply;
        try { reply = await command.Dispatch(token); }
        catch (ActionFailureException ex) when (ex.Kind != ActionFailureKind.UnknownOutcome)
        { _pending = null; RecordOutcome(ex.Kind.ToString()); return Stop(StrategyStatus.Blocked, ex.Kind.ToString()); }
        catch (Exception)
        {
            _pending = command;
            return Decision(StrategyStatus.UnknownOutcome, "DispatchOutcomeUnknown", selected.Id, command.Id);
        }
        State = reply.State;
        _pending = null;
        _consumed.Add(Key(command));
        if (reply.Defeat) { RecordOutcome("Defeat"); return Stop(StrategyStatus.Blocked, "Defeat"); }
        if (!reply.Valid || reply.Cooldown < 0 || !Matches(command, State, _baseline))
        { RecordOutcome("InvalidPostcondition"); return Stop(StrategyStatus.Blocked, "InvalidPostcondition"); }
        _verified = SavedObservation.From(State);
        RecordOutcome("Verified", State.Fingerprint);
        _seconds += reply.Cooldown;
        if (command.Productive) _noProgress = 0;
        Save();
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        try { await cooldown.WaitAsync(reply.Cooldown, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "CooldownFailed"); }
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
        var saved = checkpoints?.Load();
        if (saved is not null)
        {
            if (saved.Version != 1 || saved.Identity != identity || saved.Decisions < 0 || saved.Attempts < 0 ||
                saved.NoProgress < 0 || saved.Seconds < 0 || saved.Started > _started || saved.Consumed is null || saved.Journal.IsDefault ||
                saved.Attempts != saved.Journal.Length || saved.Attempts > saved.Decisions)
                throw new InvalidOperationException("Incompatible checkpoint");
            _started = saved.Started; _decisions = saved.Decisions; _attempts = saved.Attempts;
            _noProgress = saved.NoProgress; _seconds = saved.Seconds; _terminal = saved.Terminal;
            _consumed.UnionWith(saved.Consumed);
            _journal.AddRange(saved.Journal); _verified = saved.Verified;
            if (saved.PendingCommand is not null)
            {
                _baseline = saved.Baseline?.Restore() ?? throw new InvalidOperationException("Missing baseline");
                _pending = _strategies.Select(x => x.Evaluate(_baseline)).Select(x => x.Command)
                    .Single(x => x?.Id == saved.PendingCommand) ?? throw new InvalidOperationException("Unknown command");
                if (_pending.SourceFingerprint != _baseline.Fingerprint) throw new InvalidOperationException("Invalid baseline");
            }
        }
        _loaded = true;
        Save();
    }
    private void Save() => checkpoints?.Save(new(1, identity, _started, _decisions, _attempts, _noProgress, _seconds,
        _consumed.ToArray(), _pending?.Id, _baseline is null ? null : SavedObservation.From(_baseline), _terminal, _verified, _journal.ToImmutableArray()));
    private void RecordOutcome(string status, string? fingerprint = null)
    {
        if (_journal.Count > 0) _journal[^1] = _journal[^1] with { Status = status, ResultFingerprint = fingerprint };
    }
    private static string Key(AtomicCommand command) => command.SourceFingerprint + ":" + command.Id;
    private StrategyDecision Stop(StrategyStatus status, string reason) => _terminal = Decision(status, reason);
    private StrategyDecision Decision(StrategyStatus status, string reason, string? candidate = null, string? command = null) =>
        new(status, reason, candidate, command, _candidates, _decisions, _attempts, _noProgress, _seconds);
}
