using Artiact.Models;
using Artiact.Models.Api;

namespace Artiact.Services;

public interface IGoalDecomposer
{
    Task DecomposeGoal( Goal goal, Character character );
}