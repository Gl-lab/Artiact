using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class MoveStep : BaseStep, IStep
{
    public MoveStep( MapPoint point, ICharacterService characterService ) : base( characterService )
    {
        Point = point;
    }

    public MapPoint Point { get; }

    public async Task Execute( IGameClient client )
    {
        ActionResponse actionResponse = await client.Move( Point );
        await Delay( actionResponse.Data.Cooldown.TotalSeconds );
        CharacterService.SaveCharacter( actionResponse.Data.Character );
    }
}