using System.Diagnostics;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using System.Text.Json;

namespace Artiact.Services;

public class GoalDecomposer : IGoalDecomposer
{
    private readonly ILogger<GoalDecomposer> _logger;
    private readonly IWearCraftTargetFinder _wearCraftTargetFinder;
    private readonly ActivitySource _activitySource;

    public GoalDecomposer( ILogger<GoalDecomposer> logger,
                           IWearCraftTargetFinder wearCraftTargetFinder,
                           ActivitySource activitySource )
    {
        _logger = logger;
        _wearCraftTargetFinder = wearCraftTargetFinder;
        _activitySource = activitySource;
    }

    public async Task DecomposeGoal( Goal goal, ICharacterService characterService )
    {
        _logger.LogDebug( "Decomposed goal {GoalType}", goal.Type );
        switch ( goal )
        {
            case GatheringGoal gatheringGoal:
                await DecomposeGatheringGoal( gatheringGoal, characterService );
                break;
            case SpendResourcesGoal spendGoal:
                await DecomposeSpendResourcesGoal( spendGoal );
                break;
        }
    }

    private async Task DecomposeGatheringGoal( GatheringGoal gatheringGoal, ICharacterService characterService )
    {
        Character character = characterService.GetCharacter();
        Activity? activity = _activitySource.StartActivity( "DecomposeGatheringGoal" );
        if ( activity == null )
        {
            throw new Exception( "Listener not initialized" );
        }

        // Проверяем текущее состояние инвентаря
        int currentInventorySpace = character.InventoryMaxItems;
        int usedInventorySpace = character.Inventory.Sum( item => item.Quantity );
        int availableSpace = currentInventorySpace - usedInventorySpace;

        _logger.LogDebug( $"Checking inventory space: {availableSpace} slots available" );

        // Если места достаточно, возвращаем исходную цель
        if ( availableSpace >= 10 )
        {
            return;
        }

        // Если места недостаточно, создаем цель для освобождения инвентаря
        _logger.LogDebug( "Not enough inventory space, creating SpendResourcesGoal" );

        // Получаем список ресурсов, которые можно потратить
        List<ResourceToSpend> resourcesToSpend = new();

        foreach ( Item item in character.Inventory.Where( x => x.Quantity > 0 ).Select( x => new Item
                 {
                     Code = x.Code,
                     Quantity = x.Quantity
                 } ) )
        {
            resourcesToSpend.Add( new ResourceToSpend( item, SpendMethod.Craft ) );
        }
        // Пока тратим все


        // Создаем подцель для освобождения места
        SpendResourcesGoal spendResourcesGoal = new( resourcesToSpend );
        await DecomposeGoal( spendResourcesGoal, characterService );
        gatheringGoal.AddSubGoal( spendResourcesGoal );
    }


    private async Task DecomposeSpendResourcesGoal( SpendResourcesGoal goal )
    {
        List<Item> craftResources =
            goal.Resources.Where( x => x.Method == SpendMethod.Craft ).Select( x => x.Item ).ToList();
        _logger.LogDebug( "Craft resources: {CraftResources}", JsonSerializer.Serialize( craftResources ) );
        List<CraftTarget> targets = await _wearCraftTargetFinder.FindTargets( craftResources );
        foreach ( CraftTarget craftTarget in targets )
        {
            _logger.LogDebug( "Craft target: {CraftTarget}", JsonSerializer.Serialize( craftTarget ) );
            goal.AddSubGoal( new GearCraftingGoal( craftTarget ) );
        }


        foreach ( ResourceToSpend resource in goal.Resources )
        {
            switch ( resource.Method )
            {
                case SpendMethod.Delete:
                case SpendMethod.Recycle:
                    // These methods can be handled directly by the client

                    break;
            }
        }
    }
}