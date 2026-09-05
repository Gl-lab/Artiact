using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public class GatheringStep : BaseStep, IStep
{
    public GatheringStep( ICharacterService characterService ) : base( characterService )
    {
    }

    public async Task Execute( IGameClient client, CancellationToken cancellationToken )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ActionResponse actionResponse = await client.Gathering();
        CharacterService.SaveCharacter( actionResponse.Data.Character );
        await Delay( actionResponse.Data.Cooldown.TotalSeconds, cancellationToken );
    }
}