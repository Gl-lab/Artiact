using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models.Steps;

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