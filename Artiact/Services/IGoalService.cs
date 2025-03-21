using Artiact.Contracts.Models;

namespace Artiact.Services;

public interface IGoalService
{
    Goal GetGoal( ICharacterService character );
}