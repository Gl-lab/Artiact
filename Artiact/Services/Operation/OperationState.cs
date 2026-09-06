namespace Artiact.Services.Operation;

public sealed record OperationHealth(bool Ready, string State, string? ApiVersion, DateTimeOffset? ObservedAt, string? Fingerprint);
public sealed class OperationState(TimeProvider? time = null)
{
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
