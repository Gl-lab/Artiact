using Artiact.Client;

namespace Artiact.Models;

public class MoveStep : BaseStep, IStep
{
    private readonly MapPoint _point;


    public MoveStep( MapPoint point, Character character ) : base( character )
    {
        _point = point;
    }

    public async Task Execute( IGameClient client )
    {
        ActionResponse actionResponse = await client.Move( _point );
        await Delay( actionResponse.Data.Cooldown.TotalSeconds );
        Character = actionResponse.Data.Character;
    }
}