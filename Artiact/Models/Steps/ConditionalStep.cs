using Artiact.Contracts.Client;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class ConditionalStep : BaseStep, IStep
{
    private readonly IStep _step;
    private readonly Func<ICharacterService, bool> _condition;

    public ConditionalStep( IStep step,
                            Func<ICharacterService, bool> condition,
                            ICharacterService characterService ) : base( characterService )
    {
        _step = step;
        _condition = condition;
    }

    public async Task Execute( IGameClient client, CancellationToken cancellationToken )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ( _condition( CharacterService ) )
        {
            await _step.Execute( client, cancellationToken );
        }
    }
}
