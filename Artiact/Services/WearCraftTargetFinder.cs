using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class WearCraftTargetFinder : IWearCraftTargetFinder
{
    private readonly ICraftChainBuilder _chainBuilder;
    private readonly IGameClient _gameClient;
    private readonly ICraftTargetEvaluator _targetEvaluator;
    private readonly HashSet<string> _wearableTypes;
    private List<ItemDatum> _allItems;
    private readonly ITargetLootingResolver _targetLootingResolver;


    public WearCraftTargetFinder(
        IGameClient gameClient,
        ICraftTargetEvaluator targetEvaluator,
        ICraftChainBuilder chainBuilder,
        ITargetLootingResolver targetLootingResolver )
    {
        _gameClient = gameClient;
        _targetEvaluator = targetEvaluator;
        _chainBuilder = chainBuilder;
        _targetLootingResolver = targetLootingResolver;
        _wearableTypes = new HashSet<string>
        {
            "weapon", "boots", "helmet", "body_armor",
            "leg_armor", "ring", "amulet", "shield"
        };
    }

    public async Task<List<CraftTarget>> FindTargets( List<Item> availableItems, ICharacterService characterService )
    {
        _allItems = await _gameClient.GetItems();

        return await FindOptimalTargets( availableItems, characterService );
    }

    private async Task<List<CraftTarget>> FindOptimalTargets( List<Item> availableItems,
                                                              ICharacterService characterService )
    {
        List<CraftTarget> selectedTargets = new();
        Dictionary<string, int> remainingResources = CalculateAvailableResources( availableItems );

        while ( true )
        {
            List<CraftTarget> possibleTargets = await FindPossibleTargets( remainingResources, characterService );

            if ( !possibleTargets.Any() )
            {
                break;
            }

            CraftTarget bestTarget = _targetEvaluator.SelectBestTarget( possibleTargets );
            if ( bestTarget.LootTargets.Any() )
            {
                selectedTargets.Add( bestTarget );
                break;
            }

            if ( !TryApplyTargetResources( bestTarget, remainingResources ) )
            {
                break;
            }

            selectedTargets.Add( bestTarget );
        }

        return selectedTargets;
    }

    private async Task<List<CraftTarget>> FindPossibleTargets( Dictionary<string, int> availableResources,
                                                               ICharacterService characterService )
    {
        List<CraftTarget> targets = new();

        foreach ( ItemDatum item in _allItems.Where( i => _wearableTypes.Contains( i.Type ) && i.Craft != null ) )
        {
            List<LootTarget>? lootTargets = await ResolveLootTargets( item, availableResources, characterService );
            if ( lootTargets != null )
            {
                Dictionary<string, int> planningResources = new( availableResources );
                foreach ( LootTarget lootTarget in lootTargets )
                {
                    planningResources[ lootTarget.ItemCode ] = lootTarget.RequiredQuantity;
                }

                CraftTarget? craftTarget = await _chainBuilder.TryCreateCraftChain( item, planningResources );
                if ( craftTarget != null )
                {
                    craftTarget.LootTargets = lootTargets;
                    targets.Add( craftTarget );
                }
            }
        }

        return targets;
    }

    private bool TryApplyTargetResources( CraftTarget target, Dictionary<string, int> resources )
    {
        Dictionary<string, int> resourcesCopy = new( resources );
        foreach ( CraftStep step in target.Steps )
        {
            foreach ( Item item in step.RequiredItems )
            {
                if ( resourcesCopy.GetValueOrDefault( item.Code ) < item.Quantity )
                {
                    return false;
                }

                resourcesCopy[ item.Code ] -= item.Quantity;
            }

            int producedQuantity = step.Quantity * ( step.Item.Craft?.Quantity ?? 1 );
            resourcesCopy[ step.Item.Code ] = resourcesCopy.GetValueOrDefault( step.Item.Code ) + producedQuantity;
        }

        bool consumedInventory = resources.Any( resource =>
            resourcesCopy.GetValueOrDefault( resource.Key ) < resource.Value );
        if ( !consumedInventory )
        {
            return false;
        }

        foreach ( string code in resources.Keys.ToList() )
        {
            resources[ code ] = resourcesCopy.GetValueOrDefault( code );
        }

        return true;
    }

    private Dictionary<string, int> CalculateAvailableResources( List<Item> items )
    {
        Dictionary<string, int> resources = new();
        foreach ( Item item in items )
        {
            resources.TryAdd( item.Code, 0 );
            resources[ item.Code ] += item.Quantity;
        }

        return resources;
    }

    private async Task<List<LootTarget>?> ResolveLootTargets( ItemDatum targetItem,
                                                              Dictionary<string, int> availableResources,
                                                              ICharacterService characterService )
    {
        if ( targetItem.Craft == null )
        {
            return null;
        }

        List<LootTarget> lootTargets = new();
        foreach ( Item craftComponent in targetItem.Craft.Items )
        {
            ItemDatum? informationAboutCraftComponent = _allItems.FirstOrDefault( i => i.Code == craftComponent.Code );
            if ( informationAboutCraftComponent == null )
            {
                return null;
            }

            if ( !HasEnoughResources( craftComponent, availableResources ) &&
                !CanCraftComponent( informationAboutCraftComponent, craftComponent, availableResources ) )
            {
                LootTarget? lootTarget = await _targetLootingResolver.FindTarget(
                    informationAboutCraftComponent, craftComponent.Quantity, characterService );
                if ( lootTarget == null )
                {
                    return null;
                }

                lootTargets.Add( lootTarget );
            }
        }

        return lootTargets;
    }

    private bool HasEnoughResources( Item requiredItem, Dictionary<string, int> availableResources )
    {
        return availableResources.ContainsKey( requiredItem.Code ) &&
            availableResources[ requiredItem.Code ] >= requiredItem.Quantity;
    }

    private bool CanCraftComponent( ItemDatum informationAboutCraftComponent,
                                    Item craftComponent,
                                    Dictionary<string, int> availableResources )
    {
        if ( informationAboutCraftComponent.Craft == null )
        {
            return false;
        }

        int existingQuantity = availableResources.ContainsKey( craftComponent.Code )
            ? availableResources[ craftComponent.Code ]
            : 0;

        int remainingNeeded = craftComponent.Quantity - existingQuantity;
        if ( remainingNeeded <= 0 )
        {
            return true;
        }

        foreach ( Item craftItem in informationAboutCraftComponent.Craft.Items )
        {
            if ( !availableResources.ContainsKey( craftItem.Code ) ||
                availableResources[ craftItem.Code ] < craftItem.Quantity * remainingNeeded )
            {
                return false;
            }
        }

        return true;
    }
}