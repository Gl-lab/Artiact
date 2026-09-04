using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models.Steps;

namespace Artiact.Services;

public class StepBuilder : IStepBuilder
{
    private readonly IGameClient _gameClient;
    private readonly IMapService _mapService;

    public StepBuilder(
        IGameClient gameClient,
        IMapService mapService )
    {
        _gameClient = gameClient;
        _mapService = mapService;
    }

    public async Task<IStep> BuildStep( Goal goal, ICharacterService characterService )
    {
        MixedStep step = new( characterService );

        // Если есть подцели, сначала обрабатываем их
        if ( goal.SubGoals.Count != 0 )
        {
            foreach ( Goal subGoal in goal.SubGoals )
            {
                step.AddStep( await BuildStep( subGoal, characterService ) );
            }
        }

        step.AddStep( await BuildStepsForGoal( goal, characterService ) );


        return step;
    }


    private async Task<IStep> BuildStepsForGoal( Goal goal, ICharacterService characterService )
    {
        switch ( goal )
        {
            case GatheringGoal:
                return await BuildMiningSteps( characterService );
            case SpendResourcesGoal spendGoal:
                return await BuildSpendResourcesStep( spendGoal, characterService );
            case GearCraftingGoal craftingGoal:
                return await BuildCraftingSteps( craftingGoal, characterService );
            default:
                throw new InvalidOperationException();
        }
    }

    private async Task<IStep> BuildMiningSteps( ICharacterService characterService )
    {
        Character character = characterService.GetCharacter();
        ResourceDatum? resCandidate = await FindResourceCandidate( character );

        MapPoint? tagetMap = await _mapService.GetByContentCode( new ContentCode( resCandidate.Code ) );

        List<IStep> steps = new();

        if ( character.X != tagetMap.X || character.Y != tagetMap.Y )
        {
            steps.Add( new MoveStep( tagetMap, characterService ) );
        }


        steps.Add( new ActionStep( characterService, client => client.Gathering(), characterService =>
            characterService.GetCharacter().InventoryMaxItems - 2 >=
            characterService.GetCharacter().Inventory.Sum( x => x.Quantity )
        ) );


        if ( steps.Count > 1 )
        {
            return new MixedStep( steps, characterService );
        }

        return steps.First();
    }

    private async Task<ResourceDatum?> FindResourceCandidate( Character character )
    {
        List<ResourceDatum> resource = await _gameClient.GetResources();
        List<ResourceDatum> miningResource = resource
                                            .Where( x =>
                                                 x.Skill is not null && x.Skill == ResourceSkill.Mining.ToString() )
                                            .Where( x =>
                                                 x.Level <=
                                                 ( character.MiningLevel == 0 ? 1 : character.MiningLevel ) )
                                            .ToList();

        ResourceDatum resCandidate =
            miningResource.FirstOrDefault( x => x.Level == miningResource.Max( y => y.Level ) );
        return resCandidate;
    }


    private async Task<IStep> BuildSpendResourcesStep( SpendResourcesGoal goal, ICharacterService character )
    {
        MixedStep step = new( character );
        foreach ( ResourceToSpend resource in goal.Resources )
        {
            switch ( resource.Method )
            {
                case SpendMethod.Delete:

                    step.AddStep( new ActionStep( character, client => client.DeleteItem( resource.Item ) ) );
                    break;

                case SpendMethod.Recycle:
                    throw new NotImplementedException();
                    //TODO: Сделать шаги поиск подходящей точки на карте, движение к ней и ActionStep с Recycle
                    break;
            }
        }

        return step;
    }

    private async Task<IStep> BuildCraftingSteps( GearCraftingGoal goal, ICharacterService characterService )
    {
        List<IStep> steps = new();
        Character character = characterService.GetCharacter();
        MapPoint plannedPosition = new() { X = character.X, Y = character.Y };

        foreach ( LootTarget lootTarget in goal.Item.LootTargets )
        {
            MapPoint? monsterPoint = await _mapService.GetByContentCode( new ContentCode( lootTarget.Monster.Code ) );
            if ( monsterPoint == null )
            {
                throw new InvalidOperationException( $"Monster not found on map: {lootTarget.Monster.Code}" );
            }

            if ( plannedPosition.X != monsterPoint.X || plannedPosition.Y != monsterPoint.Y )
            {
                steps.Add( new MoveStep( monsterPoint, characterService ) );
            }
            plannedPosition = monsterPoint;

            Func<ICharacterService, bool> needsLoot = service =>
                service.GetCharacter().Inventory
                       .Where( item => item.Code == lootTarget.ItemCode )
                       .Sum( item => item.Quantity ) < lootTarget.RequiredQuantity;
            steps.Add( new ActionStep( characterService, client => client.Fight(), needsLoot, needsLoot ) );
        }

        // Группируем шаги крафта по мастерским
        Dictionary<string, List<CraftStep>> stepsByWorkshop = new();

        // Добавляем промежуточные шаги
        foreach ( CraftStep craftStep in goal.Item.Steps )
        {
            string skill = craftStep.Item.Craft.Skill;
            if ( !stepsByWorkshop.ContainsKey( skill ) )
            {
                stepsByWorkshop[ skill ] = new List<CraftStep>();
            }

            stepsByWorkshop[ skill ].Add( craftStep );
        }

        // Старые CraftTarget могли не включать финальный предмет в Steps.
        if ( goal.Item.Steps.All( step => step.Item.Code != goal.Item.FinalItem.Code ) )
        {
            string finalSkill = goal.Item.FinalItem.Craft.Skill;
            if ( !stepsByWorkshop.ContainsKey( finalSkill ) )
            {
                stepsByWorkshop[ finalSkill ] = new List<CraftStep>();
            }

            stepsByWorkshop[ finalSkill ].Add( new CraftStep
            {
                Item = goal.Item.FinalItem,
                RequiredItems = goal.Item.FinalItem.Craft.Items,
                Quantity = 1
            } );
        }

        // Обрабатываем каждую мастерскую
        foreach ( KeyValuePair<string, List<CraftStep>> workshopGroup in stepsByWorkshop )
        {
            // Находим мастерскую для текущего навыка
            MapPoint? workshop = await _mapService.GetWorkshopBySkillCode( new ContentCode( workshopGroup.Key ) );
            if ( workshop == null )
            {
                throw new InvalidOperationException( $"Workshop not found for skill {workshopGroup.Key}" );
            }

            // Если персонаж не в мастерской, добавляем шаг перемещения
            if ( plannedPosition.X != workshop.X || plannedPosition.Y != workshop.Y )
            {
                steps.Add( new MoveStep( workshop, characterService ) );
            }
            plannedPosition = workshop;

            // Добавляем шаги крафта для текущей мастерской
            foreach ( CraftStep craftStep in workshopGroup.Value )
            {
                steps.Add( new ActionStep( characterService, client => client.Crafting( new Item
                {
                    Code = craftStep.Item.Code,
                    Quantity = craftStep.Quantity
                } ) ) );
            }
        }

        if ( steps.Count > 1 )
        {
            return new MixedStep( steps, characterService );
        }

        return steps.First();
    }
}