using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class SkillPreparationPolicyTests
{
    [Fact]
    public void DisabledPreparationPreservesExistingPolicyIdentity()
    {
        var policy = new PortfolioPolicy([], 0, "", "", Items: [new("tool", 1)]);
        Assert.DoesNotContain("Preparation", policy.Identity);
        Assert.Contains("Preparation", (policy with { Preparation = new() }).Identity);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1001, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 17)]
    public void InvalidMaterialAndDepthLimitsAreRejected(int units, int depth)
    {
        var settings = new PortfolioSettings { Items = [new("tool", 1)], Preparation = new(units, depth) };
        Assert.Throws<ArgumentException>(() => settings.Policy());
    }
}
