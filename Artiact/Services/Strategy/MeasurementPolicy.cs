namespace Artiact.Services.Strategy;

public sealed record MeasurementPolicy(decimal UnknownMultiplier = 2, decimal SwitchRatio = 1.1m);
public sealed record ActionMeasurement(long Seconds, int Samples, int Progress);
