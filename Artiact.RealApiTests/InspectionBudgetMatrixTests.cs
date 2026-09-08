using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Strategy;

namespace Artiact.RealApiTests;

public class InspectionBudgetMatrixTests
{
    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task RowsResetBudgetContextAndNeverDispatchSelectedCommands()
    {
        var observation = new StrategyObservation(JsonSerializer.SerializeToElement(new
        { name = "fixture", cooldown_expiration = "2000-01-01T00:00:00Z" }),
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "fixture",
            context: StrategyRunContext.Empty with { RemainingActions = 2, RemainingSeconds = 120 });
        var budgets = new[] { new StrategyLimits(20, 3, 2, 120), new StrategyLimits(20, 3, 13, 134) };
        var seen = new List<(int?, decimal?)>();
        var rows = await InspectionBudgetMatrix.EvaluateAsync(observation, _ => new Probe(seen), null, budgets, default);
        Assert.Equal(2, rows.Length);
        Assert.Equal(new (int?, decimal?)[] { (2, 120), (13, 134) }, seen);
        Assert.Equal("AutonomousBudgetExhausted", rows[0].Decision.Reason);
        Assert.Equal(StrategyStatus.Selected, rows[1].Decision.Status);
        Assert.All(rows, row => Assert.Equal(0, row.Decision.Attempts));
        Assert.Equal(2, observation.Context.RemainingActions);
    }

    private sealed class Probe(List<(int?, decimal?)> seen) : IProgressionStrategy
    {
        public StrategyCandidate Evaluate(StrategyObservation observation)
        {
            seen.Add((observation.Context.RemainingActions, observation.Context.RemainingSeconds));
            bool fits = observation.Context.RemainingActions >= 13 && observation.Context.RemainingSeconds >= 134;
            return new("fixture", "skill", 1, 5, 0, 0, fits ? null : "EstimatedPathExceedsBudget", false,
                fits ? new AtomicCommand("Gather:fixture", observation.Fingerprint, true, _ => true,
                    _ => throw new InvalidOperationException("Inspection must never dispatch.")) : null);
        }
    }
}
