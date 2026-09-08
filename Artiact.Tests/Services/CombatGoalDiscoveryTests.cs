using Artiact.Services.Operation;
using Microsoft.Extensions.Configuration;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class CombatGoalDiscoveryTests
{
    [Fact]
    public void AbsentCategoryPreservesIdentityAndManualProfilesRejectIt()
    {
        var policy = new PortfolioSettings { AutonomousGoals = true }.Policy();
        Assert.DoesNotContain("CombatDiscovery", policy.Identity);
        Assert.Throws<ArgumentException>(() => (policy with { AutonomousGoals = false, Skills = [new("mining", 2, 1)], CombatDiscovery = new() }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { CombatDiscovery = new(AllowBankWithdrawal: true) }).Validate());
        Assert.Throws<ArgumentException>(() => (policy with { CombatDiscovery = new(MaxMaterialUnits: 0) }).Validate());
        Assert.NotEqual((policy with { CombatDiscovery = new() }).Identity, (policy with { CombatDiscovery = new(AllowFight: true) }).Identity);
    }

    [Fact]
    public void CombatProvenanceValidatesVersionAndUtility()
    {
        var goal = new GoalDiscoveryEvidence("combat-discovery-v1", "combat", 2, "enemy", 1, 0, 0, [], "bounded", "level");
        Assert.True(AutonomousRunState.ValidGoal(goal));
        Assert.False(AutonomousRunState.ValidGoal(goal with { Version = "future" }));
        Assert.False(AutonomousRunState.ValidGoal(goal with { Target = 51 }));
        Assert.False(AutonomousRunState.ValidGoal(goal with { RecipeUtility = 1 }));
    }
    [Fact]
    public void CombatPermissionsAreBoundWithoutManualGoals()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AutonomousGoals"] = "true", ["CombatDiscovery:AllowFight"] = "true"
        }).Build();
        var settings = new PortfolioSettings(); configuration.Bind(settings);
        Assert.Contains("CombatDiscovery", settings.Policy().Identity);
    }
}
