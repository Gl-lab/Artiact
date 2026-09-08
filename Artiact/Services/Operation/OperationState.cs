namespace Artiact.Services.Operation;

public sealed record OperationHealth(bool Ready, string State, string? ApiVersion, DateTimeOffset? ObservedAt, string? Fingerprint);
public sealed class OperationState(TimeProvider? time = null)
{
    private Strategy.StrategyDecision? _decision;
    private string? _runId;
    private string? _lastCommand, _goal;
    private readonly CancellationTokenSource _stop = new();
    private bool _running;
    public void WorkerStarted() { lock (_sync) _running = true; }
    public void WorkerStopped() { lock (_sync) _running = false; }
    public (bool Running, bool StopRequested, Strategy.StrategyDecision? Decision) WorkerSnapshot()
    { lock (_sync) return (_running, _stop.IsCancellationRequested, _decision); }
    public CancellationToken StopToken => _stop.Token;
    public void RequestStop() => _stop.Cancel();
    public void Progress(string runId, Strategy.StrategyDecision decision)
    {
        lock (_sync) { _runId = runId; _decision = decision; _lastCommand = decision.Command ?? _lastCommand; _goal = decision.Candidate ?? _goal; }
    }
    public object RunStatus()
    {
        lock (_sync) return new { RunId = _runId, Goal = _goal, LastCommand = _lastCommand, Decision = _decision,
            InterventionRequired = _decision?.Status is Strategy.StrategyStatus.Blocked or Strategy.StrategyStatus.UnknownOutcome,
            StopRequested = _stop.IsCancellationRequested };
    }
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _sync = new();
    private string _state = "NotInitialized";
    private string? _version, _fingerprint;
    private DateTimeOffset? _probe, _observed;
    private bool _successful;
    public void Probe(string version) { lock (_sync) { _version = version; _probe = _time.GetUtcNow(); } }
    public void Observed(string fingerprint) { lock (_sync) { _fingerprint = fingerprint; _observed = _time.GetUtcNow(); } }
    public void Set(string state, bool successful = false) { lock (_sync) { _state = state; _successful = successful; } }
    public void Finish(string state, bool successful)
    {
        lock (_sync)
        {
            if (!successful && _state is "ApiContractUnavailableOrDrift" or "StaleObservation") return;
            _state = state; _successful = successful;
        }
    }
    public OperationHealth Snapshot(int freshnessSeconds)
    {
        lock (_sync)
        {
            bool fresh = _probe.HasValue && _observed.HasValue && _time.GetUtcNow() - _probe.Value <= TimeSpan.FromSeconds(freshnessSeconds) &&
                _time.GetUtcNow() - _observed.Value <= TimeSpan.FromSeconds(freshnessSeconds);
            return new(_successful && fresh, _successful && !fresh ? "StaleObservation" : _state, _version, _observed, _fingerprint);
        }
    }
}
