using Artiact.Client;
using Artiact.Models;

namespace Artiact.Services;

public class StepBuilder : IStepBuilder
{
    private readonly IGameClient _gameClient;
    private readonly MapService _mapService;

    public StepBuilder( IGameClient gameClient, MapService mapService )
    {
        _gameClient = gameClient;
        _mapService = mapService;
    }

    public async Task<IStep> BuildSteps( Goal goal, Character character )
    {
        if ( goal.Type == GoalType.Mining )
        {
            ResourceDatum? resCandidate = await FindResourceCandidate( character );

            MapPoint? tagetMap = await _mapService.GetByContentCode( new ContentCode( resCandidate.Code ) );

            List<IStep> steps = new();

            if ( character.X != tagetMap.X || character.Y != tagetMap.Y )
            {
                steps.Add( new MoveStep( tagetMap, character ) );
            }

            steps.Add( new ActionStep( character, ( client ) => client.Gathering(),
                () => character.InventoryMaxItems - 2 >= character.Inventory.Sum( x => x.Quantity ) ) );


            if ( steps.Count > 1 )
            {
                return new MixedStep( steps, character );
            }

            return steps.First();
        }

        throw new NotImplementedException();
    }

    private async Task<ResourceDatum?> FindResourceCandidate( Character character )
    {
        List<ResourceDatum> resource = await _gameClient.GetResources();
        List<ResourceDatum> miningResource = resource
                                            .Where( x => x.Skill == ResourceSkill.Mining.ToString() )
                                            .Where( x =>
                                                 x.Level <=
                                                 ( character.MiningLevel == 0 ? 1 : character.MiningLevel ) )
                                            .ToList();

        ResourceDatum resCandidate =
            miningResource.FirstOrDefault( x => x.Level == miningResource.Max( y => y.Level ) );
        return resCandidate;
    }
}