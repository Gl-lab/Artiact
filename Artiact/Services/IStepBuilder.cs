using Artiact.Models;

namespace Artiact.Services;

public interface IStepBuilder
{
    Task<IStep> BuildSteps( Goal goal, Character character );
}