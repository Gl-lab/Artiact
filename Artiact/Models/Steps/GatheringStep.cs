using Artiact.Client;
using Artiact.Models.Api;

namespace Artiact.Models.Steps;

public class GatheringStep : BaseStep, IStep
{
    public GatheringStep( Character character ) : base( character )
    {
    }

    public async Task Execute( IGameClient client )
    {
        ActionResponse actionResponse = await client.Gathering();
        await Delay( actionResponse.Data.Cooldown.TotalSeconds );
    }
}