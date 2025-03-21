using Artiact.Contracts.Models;

namespace Artiact.Services;

public interface IGoalDecomposer
{
    Task DecomposeGoal( Goal goal, ICharacterService characterService );
}