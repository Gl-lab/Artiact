using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Contracts.Models.Steps;

namespace Artiact.Services;

public interface IStepBuilder
{
    Task<IStep> BuildStep( Goal goal, Character character );
}