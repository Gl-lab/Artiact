using Artiact.Client;

namespace Artiact.Models;

public class MixedStep : BaseStep, IStep
{
    private readonly List<IStep> _steps;

    public MixedStep( List<IStep> steps, Character character ) : base( character )
    {
        _steps = steps;
    }


    public async Task Execute( IGameClient client )
    {
        foreach ( IStep step in _steps )
        {
            await step.Execute( client );
        }
    }
}