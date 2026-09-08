using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed record GoalTransition(GoalDiscoveryEvidence Goal, string Outcome, string Fingerprint);
public sealed record AutonomousRunState(string Algorithm, GoalDiscoveryEvidence? Active, ImmutableArray<GoalTransition> History, string? ActiveWorld = null)
{
    public static AutonomousRunState Empty => new(AutonomousGoalDiscovery.Version, null, []);
    public bool Valid => Algorithm == AutonomousGoalDiscovery.Version && !History.IsDefault && History.Length <= 128 &&
        (Active is null || ValidGoal(Active) && !string.IsNullOrWhiteSpace(ActiveWorld)) && History.All(x => ValidGoal(x.Goal) && x.Outcome is "Completed" or "Rejected" && !string.IsNullOrWhiteSpace(x.Fingerprint));
    public static bool ValidGoal(GoalDiscoveryEvidence goal) => goal.Version == AutonomousGoalDiscovery.Version &&
        goal.Skill is "alchemy" or "fishing" or "mining" or "woodcutting" && goal.Target is > 1 and <= AutonomousGoalDiscovery.SkillCap &&
        goal.UnlockUtility is 0 or 1 && goal.ProgressUtility is 0 or 0.1m && goal.UnlockUtility + goal.ProgressUtility > 0 && goal.RecipeUtility == 0;
    public AutonomousRunState CompleteObserved(StrategyObservation state) => Active is { } goal &&
        state.Character.TryGetProperty(goal.Skill + "_level", out var level) && level.TryGetInt32(out int observed) && observed >= goal.Target
        ? Transition("Completed", state.Fingerprint) : this;
    public AutonomousRunState Transition(string outcome, string fingerprint)
    {
        if (Active is null || History.Length >= 128) throw new InvalidOperationException("AutonomousHistoryExhausted");
        return this with { Active = null, ActiveWorld = null, History = History.Add(new(Active, outcome, fingerprint)) };
    }
}
