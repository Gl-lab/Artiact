using Artiact.Client;

namespace Artiact.Models;

public class ActionStep : BaseStep, IStep
{
    private readonly Func<IGameClient, Task<ActionResponse>> _action;
    private Func<bool>? _needRepeat;

    public ActionStep( Character character, Func<IGameClient, Task<ActionResponse>> action,
                       Func<bool>? needRepeat = null) : base( character )
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