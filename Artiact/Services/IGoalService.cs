using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public interface IGoalService
{
    Goal GetGoal( Character character );
}