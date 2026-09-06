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

    public async Task Execute( IGameClient client, CancellationToken cancellationToken )
    {
        using var operation = (client as Artiact.Client.GameClient)?.BeginOperation(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ActionResponse actionResponse = await client.Move( Point );
        CharacterService.SaveCharacter( actionResponse.RequireCharacter() );
        await Delay( actionResponse.RequireCooldown().TotalSeconds, cancellationToken );
    }
}