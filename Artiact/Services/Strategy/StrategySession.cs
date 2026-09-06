using System.Collections.Immutable;
using Artiact.Contracts.Client;
using System.Text.Json.Serialization;

namespace Artiact.Services.Strategy;

public enum StrategyStatus { Selected, Completed, Blocked, Replan, Reconciled, UnknownOutcome, Cancelled, CoolingDown }
public sealed record StrategyLimits(int Decisions = 100, int NoProgress = 10);
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
    IMiningCooldownDelay cooldown, StrategyLimits? limits = null, TimeProvider? time = null)
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
    public StrategyObservation? State { get; private set; }
    public async Task<StrategyDecision> TickAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(CancellationToken.None);
        try { return await TickCoreAsync(token); }
        finally { _gate.Release(); }
    }

    private async Task<StrategyDecision> TickCoreAsync(CancellationToken token)
    {
        if (_terminal is not null) return _terminal;
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        if (_limits.Decisions <= 0 || _limits.NoProgress <= 0 || _limits.NoProgress > _limits.Decisions)
            return Stop(StrategyStatus.Blocked, "InvalidLimits");
        if (_pending is null && (_decisions >= _limits.Decisions || _noProgress >= _limits.NoProgress))
            return Stop(StrategyStatus.Blocked, "BudgetExhausted");
        _decisions++;
        try { State = await observer.ObserveAsync(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(_pending is null ? StrategyStatus.Blocked : StrategyStatus.UnknownOutcome, "ObservationFailed"); }
        if (_pending is not null)
        {
            var pending = _pending;
            if (!Matches(pending, State, _baseline!)) return Stop(StrategyStatus.UnknownOutcome, "UnresolvedOutcome");
            _consumed.Add(Key(pending)); _pending = null;
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
        _attempts++; _noProgress++;
        StrategyReply reply;
        try { reply = await command.Dispatch(token); }
        catch (ActionFailureException ex) when (ex.Kind != ActionFailureKind.UnknownOutcome)
        { return Stop(StrategyStatus.Blocked, ex.Kind.ToString()); }
        catch (Exception)
        {
            _pending = command;
            return Decision(StrategyStatus.UnknownOutcome, "DispatchOutcomeUnknown", selected.Id, command.Id);
        }
        State = reply.State;
        _consumed.Add(Key(command));
        if (reply.Defeat) return Stop(StrategyStatus.Blocked, "Defeat");
        if (!reply.Valid || reply.Cooldown < 0 || !Matches(command, State, _baseline))
            return Stop(StrategyStatus.Blocked, "InvalidPostcondition");
        _seconds += reply.Cooldown;
        if (command.Productive) _noProgress = 0;
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        try { await cooldown.WaitAsync(reply.Cooldown, token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return Stop(StrategyStatus.Cancelled, "Cancelled"); }
        catch (Exception) { return Stop(StrategyStatus.Blocked, "CooldownFailed"); }
        if (token.IsCancellationRequested) return Stop(StrategyStatus.Cancelled, "Cancelled");
        return Decision(StrategyStatus.Selected, "CommandVerified", selected.Id, command.Id);
    }
    private static bool Matches(AtomicCommand command, StrategyObservation after, StrategyObservation before)
    {
        try { return before.SameWorld(after) && command.Postcondition(after); }
        catch (Exception) { return false; }
    }
    private static string Key(AtomicCommand command) => command.SourceFingerprint + ":" + command.Id;
    private StrategyDecision Stop(StrategyStatus status, string reason) => _terminal = Decision(status, reason);
    private StrategyDecision Decision(StrategyStatus status, string reason, string? candidate = null, string? command = null) =>
        new(status, reason, candidate, command, _candidates, _decisions, _attempts, _noProgress, _seconds);
}
