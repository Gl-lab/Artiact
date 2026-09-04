using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class ActionStep : BaseStep, IStep
{
    private readonly Func<IGameClient, Task<ActionResponse>> _action;
    private readonly Func<ICharacterService, bool>? _needRepeat;
    private readonly Func<ICharacterService, bool>? _shouldExecute;

    public ActionStep( ICharacterService characterService,
                       Func<IGameClient, Task<ActionResponse>> action,
                       Func<ICharacterService, bool>? needRepeat = null,
                       Func<ICharacterService, bool>? shouldExecute = null ) : base( characterService )
    {
        _action = action;
        _needRepeat = needRepeat;
        _shouldExecute = shouldExecute;
    }

    public async Task Execute( IGameClient client )
    {
        if ( _shouldExecute?.Invoke( CharacterService ) == false )
            return;

        do
        {
            ActionResponse actionResponse = await _action( client );
            CharacterService.SaveCharacter( actionResponse.Data.Character );
            await Delay( actionResponse.Data.Cooldown.TotalSeconds );
        } while ( _needRepeat?.Invoke( CharacterService ) ?? false );
    }
}