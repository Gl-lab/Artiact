using Artiact.Models;

namespace Artiact.Services;

public class GoalService : IGoalService
{
    public Goal GetGoal( Character character )
    {
        return new Goal()
        {
            Type = GoalType.Mining,
            TagetLevel = 2
        };
    }
}