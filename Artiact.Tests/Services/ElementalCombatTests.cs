using Artiact.Services.Combat;
using Xunit;

namespace Artiact.Tests.Services;

public class ElementalCombatTests
{
    [Fact]
    public void MixedElementsApplyMatchingResistanceBeforeSumming()
    {
        var result = CombatPrediction.Evaluate(new(100, 10, Water: new(10)), new(30, 3, Resistance: 50, Water: new(4)));
        Assert.Equal(CombatViability.Safe, result.Viability); Assert.Equal(2, result.Exchanges); Assert.Equal(14, result.MaximumLoss);
    }
    [Fact]
    public void SecondaryChannelsRoundEachStageAndPessimisticallyChargeCritical()
    {
        var result = CombatPrediction.Evaluate(new(10, 0, Water: new(1, 50, 25)), new(2, 0, Critical: 1, Water: new(1, 50, 25)));
        Assert.Equal(1, result.Exchanges); Assert.Equal(3, result.MaximumLoss);
    }
    [Fact]
    public void InvalidSecondaryChannelIsUnknown()
    {
        Assert.Equal(CombatViability.Unknown, CombatPrediction.Evaluate(new(20, 10, Air: new(-1)), new(20, 3)).Viability);
    }
}
