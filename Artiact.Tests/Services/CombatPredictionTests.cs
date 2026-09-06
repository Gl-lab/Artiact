using Artiact.Services.Combat;

namespace Artiact.Tests.Services;

public class CombatPredictionTests
{
    [Theory]
    [InlineData(20, 10, 3, CombatViability.Safe, 2, 6)]
    [InlineData(6, 10, 3, CombatViability.Unsafe, 2, 6)]
    [InlineData(7, 10, 3, CombatViability.Safe, 2, 6)]
    [InlineData(20, 0, 3, CombatViability.Unsafe, 0, 0)]
    [InlineData(20, -1, 3, CombatViability.Unknown, 0, 0)]
    [InlineData(20, 10, -1, CombatViability.Unknown, 0, 0)]
    public void SurvivalUsesConservativeIncomingBound(int hp, int attack, int incoming,
        CombatViability expected, int exchanges, int loss)
    {
        var result = CombatPrediction.Evaluate(new(hp, attack), new(20, incoming));
        Assert.Equal(expected, result.Viability);
        Assert.Equal(exchanges, result.Exchanges);
        Assert.Equal(loss, result.MaximumLoss);
    }

    [Fact]
    public void WorstEnemyCriticalAndHalfUpRoundingAreConservative()
    {
        var result = CombatPrediction.Evaluate(new(20, 10), new(20, 3, Critical: 1));
        Assert.Equal(10, result.MaximumLoss);
        Assert.Equal(CombatViability.Safe, result.Viability);
    }

    [Fact]
    public void MoreThanFiftyExchangesIsUnsafe()
    {
        Assert.Equal(CombatViability.Unsafe, CombatPrediction.Evaluate(new(1000, 1), new(51, 1)).Viability);
    }
}
