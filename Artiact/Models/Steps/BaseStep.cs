namespace Artiact.Models;

public abstract class BaseStep
{
    protected Character Character;

    protected BaseStep( Character character )
    {
        Character = character;
    }

    protected Task Delay( int seconds ) => Task.Delay( TimeSpan.FromSeconds( seconds ) );
}