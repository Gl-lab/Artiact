using Artiact.Services;

namespace Artiact.Models.Steps;

public abstract class BaseStep
{
    protected ICharacterService CharacterService;

    protected BaseStep( ICharacterService characterService )
    {
        CharacterService = characterService;
    }

    protected Task Delay( int seconds )
    {
        return Task.Delay( TimeSpan.FromSeconds( seconds ) );
    }
}