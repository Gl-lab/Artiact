using Artiact.Contracts.Models;
using Artiact.Models.Steps;

namespace Artiact.Services;

public interface IStepBuilder
{
    Task<IStep> BuildStep( Goal goal, ICharacterService characterService );
}