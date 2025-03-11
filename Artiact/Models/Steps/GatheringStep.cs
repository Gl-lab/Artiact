using Artiact.Client;

namespace Artiact.Models;

public class GatheringStep : BaseStep, IStep
{
 
    public GatheringStep( Character character ) : base( character)
    {
        
    }

    public async Task Execute( IGameClient client )
    {
        ActionResponse actionResponse = await client.Gathering(  );
        await Delay( actionResponse.Data.Cooldown.TotalSeconds );
    }
}