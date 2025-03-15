using Artiact.Models;
using Artiact.Models.Api;

namespace Artiact.Services;

public interface IGoalService
{
    Goal GetGoal( Character character );
}