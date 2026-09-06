namespace Artiact.Contracts.Client;

public enum ActionFailureKind
{
    Rejected,
    UnknownOutcome
}

// Deliberately carries no response body, request URI or transport exception:
// callers may log this exception without exposing account or server payloads.
public sealed class ActionFailureException(ActionFailureKind kind, int? statusCode = null)
    : Exception($"Action failed: {kind}; HTTP status: {statusCode?.ToString() ?? "unavailable"}.")
{
    public ActionFailureKind Kind { get; } = kind;
    public int? StatusCode { get; } = statusCode;
}
