using System.Drawing;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class MoveStep : BaseStep, IStep
{
    private readonly MapPoint _point;
    
    public MapPoint Point => _point;


    public MoveStep( MapPoint point, ICharacterService characterService ) : base( characterService )
    {
        _point = point;
    }

    public async Task Execute( IGameClient client )
    {
        ActionResponse actionResponse = await client.Move( _point );
        await Delay( actionResponse.Data.Cooldown.TotalSeconds );
        CharacterService.SaveCharacter( actionResponse.Data.Character );
    }
}