using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class GoalService : IGoalService
{
    public Goal GetGoal( Character character )
    {
        return new GatheringGoal( 20 )
        {
        };
    }
}