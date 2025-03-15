using Artiact.Client;
using Artiact.Models.Api;

namespace Artiact.Models.Steps;

public class ActionStep : BaseStep, IStep
{
    private readonly Func<IGameClient, Task<ActionResponse>> _action;
    private readonly Func<bool>? _needRepeat;

    public ActionStep( Character character,
                       Func<IGameClient, Task<ActionResponse>> action,
                       Func<bool>? needRepeat = null ) : base( character )
    {
        _action = action;
        _needRepeat = needRepeat;
    }

    public async Task Execute( IGameClient client )
    {
        do
        {
            ActionResponse actionResponse = await _action( client );
            await Delay( actionResponse.Data.Cooldown.TotalSeconds );
        } while ( _needRepeat?.Invoke() ?? false );
    }
}