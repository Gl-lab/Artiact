using Artiact.Services.Strategy;

namespace Artiact.RealApiTests;

internal static class InspectionBudgetMatrix
{
    internal sealed record Row(StrategyLimits Limits, StrategyDecision Decision);

    internal static async Task<Row[]> EvaluateAsync(StrategyObservation observation,
        Func<StrategyLimits, IProgressionStrategy> strategy, MeasurementPolicy? measurement,
        IReadOnlyList<StrategyLimits> budgets, CancellationToken token)
    {
        var rows = new List<Row>();
        foreach (var budget in budgets)
        {
            token.ThrowIfCancellationRequested();
            var session = new StrategySession(new FrozenObserver(observation), [strategy(budget)],
                new Artiact.Services.MiningCooldownDelay(), budget, selection: measurement,
                inspectOnly: true, autonomous: true);
            rows.Add(new(budget, await session.InspectAsync(token)));
        }
        return rows.ToArray();
    }

    private sealed class FrozenObserver(StrategyObservation observation) : IStrategyObserver
    {
        public Task<StrategyObservation> ObserveAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(observation);
        }
    }
}
