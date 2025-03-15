using Artiact.Client;
using Artiact.Models.Api;

namespace Artiact.Models.Steps;

public class MixedStep : BaseStep, IStep
{
    private readonly List<IStep> _steps;

    public MixedStep( List<IStep> steps, Character character ) : base( character )
    {
        _steps = steps;
    }

    public MixedStep( Character character ) : base( character )
    {
        _steps = [];
    }

    public void AddStep( IStep step )
    {
        _steps.Add( step );
    }
    
    public void AddSteps( List<IStep> steps )
    {
        _steps.AddRange( steps );
    }


    public async Task Execute( IGameClient client )
    {
        foreach ( IStep step in _steps )
        {
            await step.Execute( client );
        }
    }
}