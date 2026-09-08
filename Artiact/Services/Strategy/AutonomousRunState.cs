using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed record GoalTransition(GoalDiscoveryEvidence Goal, string Outcome, string Fingerprint, int? ObservedHp = null);
public sealed record AutonomousRunState(string Algorithm, GoalDiscoveryEvidence? Active, ImmutableArray<GoalTransition> History, string? ActiveWorld = null)
{
    public static AutonomousRunState Empty => new(AutonomousGoalDiscovery.Version, null, []);
    public bool Valid => Algorithm is "discovery-v1" or AutonomousGoalDiscovery.Version && !History.IsDefault && History.Length <= 128 &&
        (Active is null || ValidGoal(Active) && !string.IsNullOrWhiteSpace(ActiveWorld)) && History.All(x => ValidGoal(x.Goal) && x.Outcome is "Completed" or "Rejected" && !string.IsNullOrWhiteSpace(x.Fingerprint));
    public static bool ValidGoal(GoalDiscoveryEvidence goal) => goal.Skill == "combat" ? goal.Version == "combat-discovery-v1" &&
        goal.Target is > 1 and <= 50 && goal.UnlockUtility is 0 or 1 && goal.ProgressUtility is 0 or 0.1m &&
        goal.UnlockUtility + goal.ProgressUtility > 0 && goal.RecipeUtility == 0 : goal.Skill == "recovery" ? goal.Version == "recovery-v1" &&
        goal.Target is > 0 and <= 1_000_000 && goal.UnlockUtility is > 0 and <= 4 && goal.ProgressUtility == 0 && goal.RecipeUtility == 0 : goal.Version is "discovery-v1" or AutonomousGoalDiscovery.Version &&
        goal.Skill is "alchemy" or "fishing" or "mining" or "woodcutting" && goal.Target is > 1 and <= AutonomousGoalDiscovery.SkillCap &&
        goal.UnlockUtility is 0 or 1 && goal.ProgressUtility is 0 or 0.1m && goal.UnlockUtility + goal.ProgressUtility > 0 && goal.RecipeUtility == 0 &&
        (goal.OriginalTarget is null ? goal.FallbackReason is null : goal.Version == AutonomousGoalDiscovery.Version &&
            goal.OriginalTarget > goal.Target && goal.OriginalTarget <= AutonomousGoalDiscovery.SkillCap && goal.UnlockUtility == 0 &&
            goal.ProgressUtility == 0.1m && goal.FallbackReason is "EstimatedInventoryInsufficient" or "EstimatedPathExceedsBudget");
    public AutonomousRunState CompleteObserved(StrategyObservation state) => Active is { } goal &&
        state.Character.TryGetProperty(goal.Skill == "recovery" ? "hp" : goal.Skill == "combat" ? "level" : goal.Skill + "_level", out var level) && level.TryGetInt32(out int observed) && observed >= goal.Target
        ? Transition("Completed", state.Fingerprint, goal.Skill == "recovery" ? observed : null) : this;
    public AutonomousRunState Transition(string outcome, string fingerprint, int? observedHp = null)
    {
        if (Active is null || History.Length >= 128) throw new InvalidOperationException("AutonomousHistoryExhausted");
        return this with { Active = null, ActiveWorld = null, History = History.Add(new(Active, outcome, fingerprint, observedHp)) };
    }
}
