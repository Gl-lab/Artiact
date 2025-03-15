using Artiact.Models;
using Artiact.Models.Api;

namespace Artiact.Services;

public class GoalService : IGoalService
{
    public Goal GetGoal( Character character )
    {
        return new MiningGoal( 20 )
        {
        };
    }
}