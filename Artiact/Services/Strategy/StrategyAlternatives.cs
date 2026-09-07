using System.Collections.Immutable;

namespace Artiact.Services.Strategy;

public sealed class ResourceAlternatives(SkillMilestone goal, PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation) => new GatheringStrategy(goal, policy, port).Evaluate(observation);
    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation)
    {
        if (!observation.Catalogs.TryGetValue("resources", out var resources)) return [Evaluate(observation)];
        var candidates = new List<StrategyCandidate>();
        foreach (var resource in resources.Where(x => x.TryGetProperty("skill", out var skill) && skill.GetString() == goal.Skill))
        {
            var filtered = new StrategyObservation(observation.Character, observation.Catalogs.SetItem("resources", [resource]), observation.Policy, observation.Bank);
            var candidate = new GatheringStrategy(goal, policy, port).Evaluate(filtered);
            var command = candidate.Command;
            if (command is not null)
                command = new(command.Id, observation.Fingerprint, command.Productive, candidate.Command!.Postcondition, async token =>
                {
                    var reply = await candidate.Command.Dispatch(token);
                    return reply with { State = new(reply.State.Character, observation.Catalogs, observation.Policy, reply.State.Bank) };
                });
            candidates.Add(candidate with { Id = candidate.Id + ":" + resource.GetProperty("code").GetString(), Command = command });
        }
        return candidates.Count == 0 ? [Evaluate(observation)] : candidates;
    }
}

public sealed class NamedStrategy(string id, IProgressionStrategy strategy) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation) => strategy.Evaluate(observation) with { Id = id };
}
