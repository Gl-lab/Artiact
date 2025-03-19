using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;

namespace Artiact.Contracts.Models.Steps;

public class GatheringStep : BaseStep, IStep
{
    public GatheringStep( Character character ) : base( character )
    {
    }

    public async Task Execute( IGameClient client )
    {
        ActionResponse actionResponse = await client.Gathering();
        Character = actionResponse.Data.Character;  
        await Delay( actionResponse.Data.Cooldown.TotalSeconds );
    }
}