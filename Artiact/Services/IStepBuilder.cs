using Artiact.Models;
using Artiact.Models.Api;
using Artiact.Models.Steps;

namespace Artiact.Services;

public interface IStepBuilder
{
    Task<IStep> BuildStep( Goal goal, Character character );
}