using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class ActionStep : BaseStep, IStep
{
    private readonly Func<IGameClient, Task<ActionResponse>> _action;
    private readonly Func<ICharacterService, bool>? _needRepeat;

    public ActionStep( ICharacterService characterService,
                       Func<IGameClient, Task<ActionResponse>> action,
                       Func<ICharacterService, bool>? needRepeat = null ) : base( characterService )
    {
        _action = action;
        _needRepeat = needRepeat;
    }

    public async Task Execute( IGameClient client )
    {
        do
        {
            ActionResponse actionResponse = await _action( client );
            CharacterService.SaveCharacter( actionResponse.Data.Character );
            await Delay( actionResponse.Data.Cooldown.TotalSeconds );
        } while ( _needRepeat?.Invoke( CharacterService ) ?? false );
    }
}