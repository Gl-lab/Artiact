using Artiact.Contracts.Models;

namespace Artiact.Services;

public class GoalService : IGoalService
{
    public Goal GetGoal( ICharacterService character )
    {
        return new GatheringGoal( 20 );
    }
}