using System.Collections.Immutable;
using Artiact.Services.Strategy;

namespace Artiact.Services.Operation;

public static class OperatorDecisionProjection
{
    public static object[] Candidates(ImmutableArray<StrategyCandidate> candidates) => candidates.IsDefaultOrEmpty ? [] :
        candidates.Take(128).Select(x => (object)new
        {
            x.Id, x.Rejection, Skill = x.Discovery?.Skill, Target = x.Discovery?.Target,
            OriginalTarget = x.Discovery?.OriginalTarget, FallbackReason = x.Discovery?.FallbackReason, x.Feasibility
        }).ToArray();

    public static object? Decision(StrategyDecision? decision) => decision is null ? null : new
    {
        Status = decision.Status.ToString(), decision.Reason, decision.Candidate, decision.Command,
        Candidates = Candidates(decision.Candidates), decision.Decisions, decision.Attempts, decision.NoProgress
    };
}
