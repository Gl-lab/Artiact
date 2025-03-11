using Artiact.Models;

namespace Artiact.Services;

public interface IGoalService
{
    Goal GetGoal( Character character );
}