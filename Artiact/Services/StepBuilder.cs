using Artiact.Client;
using Artiact.Models;
using Artiact.Models.Api;
using Artiact.Models.Steps;

namespace Artiact.Services;

public class StepBuilder : IStepBuilder
{
    private readonly IGoalDecomposer _goalDecomposer;
    private readonly IGameClient _gameClient;
    private readonly IMapService _mapService;

    public StepBuilder( IGoalDecomposer goalDecomposer,
                        IGameClient gameClient,
                        IMapService mapService )
    {
        _goalDecomposer = goalDecomposer;
        _gameClient = gameClient;
        _mapService = mapService;
    }

    public async Task<IStep> BuildStep( Goal goal, Character character )
    {
        MixedStep step = new MixedStep( character );

        // Сначала декомпозируем цель на подцели
        goal = await _goalDecomposer.DecomposeGoal( goal, character );

        // Если есть подцели, сначала обрабатываем их
        if ( goal.SubGoals.Count != 0 )
        {
            foreach ( Goal subGoal in goal.SubGoals )
            {
                step.AddStep( await BuildStepsForGoal( subGoal, character ) );
            }
        }

        step.AddStep( await BuildStepsForGoal( goal, character ) );


        return step;
    }


    private async Task<IStep> BuildStepsForGoal( Goal goal, Character character )
    {
        switch ( goal )
        {
            case MiningGoal:
                return await BuildMiningSteps( character );
            case SpendResourcesGoal spendGoal:
                return BuildSpendResourcesStep( spendGoal, character );
            default:
                throw new InvalidOperationException();
        }
    }

    private async Task<IStep> BuildMiningSteps( Character character )
    {
        ResourceDatum? resCandidate = await FindResourceCandidate( character );

        MapPoint? tagetMap = await _mapService.GetByContentCode( new ContentCode( resCandidate.Code ) );

        List<IStep> steps = new();

        if ( character.X != tagetMap.X || character.Y != tagetMap.Y )
        {
            steps.Add( new MoveStep( tagetMap, character ) );
        }

        steps.Add( new ActionStep( character, client => client.Gathering(),
            () => character.InventoryMaxItems - 2 >= character.Inventory.Sum( x => x.Quantity ) ) );


        if ( steps.Count > 1 )
        {
            return new MixedStep( steps, character );
        }

        return steps.First();
    }

    private async Task<ResourceDatum?> FindResourceCandidate( Character character )
    {
        List<ResourceDatum> resource = await _gameClient.GetResources();
        List<ResourceDatum> miningResource = resource
                                            .Where( x => x.Skill is not null && x.Skill == ResourceSkill.Mining.ToString() )
                                            .Where( x =>
                                                 x.Level <=
                                                 ( character.MiningLevel == 0 ? 1 : character.MiningLevel ) )
                                            .ToList();

        ResourceDatum resCandidate =
            miningResource.FirstOrDefault( x => x.Level == miningResource.Max( y => y.Level ) );
        return resCandidate;
    }


    private IStep BuildSpendResourcesStep( SpendResourcesGoal goal, Character character )
    {
        MixedStep step = new MixedStep( character );
        foreach ( ResourceToSpend resource in goal.Resources )
        {
            switch ( resource.Method )
            {
                case SpendMethod.Delete:
                    foreach ( Item resourceItem in resource.Items )
                    {
                        step.AddStep( new ActionStep( character, client => client.DeleteItem( resourceItem ) ) );
                    }

                    break;

                case SpendMethod.Recycle:
                    throw new NotImplementedException();
                    //TODO: Сделать шаги поиск подходящей точки на карте, движение к ней и ActionStep с Recycle
                    break;

                // Crafting is handled by CraftingGoal decomposition
            }
        }

        return step;
    }
}