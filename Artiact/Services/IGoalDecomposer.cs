using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface IGoalDecomposer
{
    Task DecomposeGoal( Goal goal, Character character );
}