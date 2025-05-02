using Artiact.Contracts.Client;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class MixedStep : BaseStep, IStep
{
    private readonly List<IStep> _steps;

    public MixedStep( List<IStep> steps, ICharacterService characterService ) : base( characterService )
    {
        _steps = steps;
    }

    public MixedStep( ICharacterService characterService ) : base( characterService )
    {
        _steps = [];
    }

    public IReadOnlyList<IStep> Steps => _steps;


    public async Task Execute( IGameClient client )
    {
        foreach ( IStep step in _steps )
        {
            await step.Execute( client );
        }
    }

    public void AddStep( IStep step )
    {
        _steps.Add( step );
    }

    public void AddSteps( List<IStep> steps )
    {
        _steps.AddRange( steps );
    }
}