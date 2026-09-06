using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;

namespace Artiact.Services;

public interface IGoalService
{
    GoalDecision Evaluate(Character? character);
}
