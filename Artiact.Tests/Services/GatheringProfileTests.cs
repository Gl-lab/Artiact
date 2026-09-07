using Artiact.Services.Operation;
using Xunit;

namespace Artiact.Tests.Services;

public class GatheringProfileTests
{
    [Fact]
    public void OmittedCombatSettingsProduceProfessionOnlyPolicy()
    {
        var policy = new PortfolioSettings { Skills = [new("mining", 20, 30)] }.Policy();
        Assert.False(policy.CombatEnabled);
    }

    [Theory]
    [InlineData(-1, "", "")]
    [InlineData(0, "dummy", "")]
    [InlineData(0, "", "blade")]
    [InlineData(2, "", "blade")]
    public void PartialCombatConfigurationIsRejected(int target, string monster, string equipment)
    {
        Assert.Throws<ArgumentException>(() => new PortfolioSettings
        { Skills = [new("mining", 20, 30)], CombatTarget = target, Monster = monster, Equipment = equipment }.Policy());
    }
}
