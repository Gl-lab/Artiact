using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class ActionStep : BaseStep, IStep
{
    private readonly Func<IGameClient, Task<ActionResponse>> _action;
    private readonly int? _maxAttempts;
    private readonly Func<ICharacterService, bool>? _needRepeat;

    public ActionStep( ICharacterService characterService,
                       Func<IGameClient, Task<ActionResponse>> action,
                       Func<ICharacterService, bool>? needRepeat = null,
                       int? maxAttempts = null ) : base( characterService )
    {
        _action = action;
        _needRepeat = needRepeat;
        _maxAttempts = maxAttempts;
    }

    public async Task Execute( IGameClient client, CancellationToken cancellationToken )
    {
        int attempts = 0;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            ActionResponse actionResponse = await _action( client );
            CharacterService.SaveCharacter( actionResponse.Data.Character );
            if ( actionResponse.Data.Fight?.Result == "loss" )
                throw new ActionFailureException( ActionFailureKind.Defeat );
            await Delay( actionResponse.Data.Cooldown.TotalSeconds, cancellationToken );
            attempts++;

            if ( _needRepeat?.Invoke( CharacterService ) != true )
            {
                return;
            }

            if ( _maxAttempts.HasValue && attempts >= _maxAttempts.Value )
            {
                throw new InvalidOperationException( $"Action did not complete after {_maxAttempts} attempts." );
            }
        } while ( true );
    }
}
