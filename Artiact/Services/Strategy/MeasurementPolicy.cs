namespace Artiact.Services.Strategy;

public sealed record MeasurementPolicy(decimal UnknownMultiplier = 2, decimal SwitchRatio = 1.1m,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)] bool FullPaths = false);
public sealed record ActionMeasurement(long Seconds, int Samples, int Progress, decimal UsefulProgress = 0, int ProgressSamples = 0);
public sealed record GoalMetric(string Kind, string Code, int Target);
public sealed record PathEstimate(GoalMetric Goal, string WorkCommand, decimal Remaining, decimal ExpectedProgress,
    decimal UnitSeconds, decimal PreparationSeconds, decimal TravelSeconds, decimal RecoverySeconds,
    System.Collections.Immutable.ImmutableDictionary<string, int> Materials, string Assumptions, string? Context = null);
