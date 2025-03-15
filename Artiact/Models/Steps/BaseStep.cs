using Artiact.Models.Api;

namespace Artiact.Models.Steps;

public abstract class BaseStep
{
    protected Character Character;

    protected BaseStep( Character character )
    {
        Character = character;
    }

    protected Task Delay( int seconds )
    {
        return Task.Delay( TimeSpan.FromSeconds( seconds ) );
    }
}