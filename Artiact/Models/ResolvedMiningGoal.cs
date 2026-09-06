using Artiact.Contracts.Models;

namespace Artiact.Models;

public sealed class ResolvedMiningGoal(int targetLevel, MiningDestination destination) : GatheringGoal(targetLevel)
{
    public MiningDestination Destination { get; } = destination;
}
