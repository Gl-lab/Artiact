using Artiact.Contracts.Models;
using Artiact.Models;

namespace Artiact.Tests.Services;

public class MiningGoalDecisionFactoryTests
{
    public static TheoryData<GoalDecisionReason, string> Reasons => new()
    {
        { GoalDecisionReason.InvalidMiningProgress, "invalid_mining_progress" },
        { GoalDecisionReason.MiningDestinationNotReached, "mining_destination_not_reached" },
        { GoalDecisionReason.MiningNoProgress, "mining_no_progress" },
        { GoalDecisionReason.MiningCycleLimit, "mining_cycle_limit" },
        { GoalDecisionReason.InvalidMiningCatalog, "invalid_mining_catalog" },
        { GoalDecisionReason.NoMiningDestination, "no_mining_destination" }
    };

    [Theory]
    [MemberData(nameof(Reasons))]
    public void ProgressionReasonsRequireBlockedBelowTargetAndNoInventedFacts(GoalDecisionReason reason, string code)
    {
        GoalDecision decision = GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 1);
        Assert.Equal(code, decision.ReasonCode);
        Assert.Equal(GoalDecisionStatus.Blocked, decision.Status);
        Assert.Equal(reason, decision.Reason);
        Assert.Equal(3, decision.MiningTargetLevel);
        Assert.Equal(1, decision.CurrentMiningLevel);
        Assert.Null(decision.InventoryCapacity);
        Assert.Null(decision.InventoryUsed);
        Assert.Null(decision.InventoryFree);
        Assert.Null(decision.SelectedGoalType);
        Assert.Equal(0, GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 0).CurrentMiningLevel);
        foreach (var status in new[] { GoalDecisionStatus.Selected, GoalDecisionStatus.Completed, (GoalDecisionStatus)99 })
            Assert.Throws<ArgumentException>(() => GoalDecision.Create(status, reason, 3, 1));
        foreach (int? level in new int?[] { null, -1, 3, 4 })
            Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, level));
        foreach (int target in new[] { 0, -1 })
            Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, target, 0));
        Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 1, capacity: 20));
        Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 1, used: 0));
        Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 1, free: 20));
        Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 1, 20, 0, 20));
        Assert.Throws<ArgumentException>(() => GoalDecision.Create(GoalDecisionStatus.Blocked, reason, 3, 1, selectedGoalType: GoalType.Gathering));
    }
}
