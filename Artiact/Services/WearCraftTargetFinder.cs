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

            CraftTarget bestTarget = _targetEvaluator.SelectBestTarget( possibleTargets, characterService );
            if ( !CanCraftWithRemainingResources( bestTarget, remainingResources ) )
            {
                break;
            }

            selectedTargets.Add( bestTarget );
            SubtractResources( remainingResources, bestTarget );
        }

        return selectedTargets;
    }

    private async Task<List<CraftTarget>> FindPossibleTargets( Dictionary<string, int> availableResources,
                                                               ICharacterService characterService )
    {
        List<CraftTarget> targets = new();

        foreach ( ItemDatum item in _allItems.Where( i => _wearableTypes.Contains( i.Type ) && i.Craft != null ) )
        {
            if ( await CanCraftFinalItem( item, availableResources, characterService ) )
            {
                CraftTarget? craftTarget = await _chainBuilder.TryCreateCraftChain( item, availableResources );
                if ( craftTarget != null )
                {
                    targets.Add( craftTarget );
                }
            }
        }

        return targets;
    }

    private bool CanCraftWithRemainingResources( CraftTarget target, Dictionary<string, int> resources )
    {
        Dictionary<string, int> resourcesCopy = new( resources );
        return TrySubtractResources( resourcesCopy, target );
    }

    private void SubtractResources( Dictionary<string, int> resources, CraftTarget target )
    {
        foreach ( CraftStep step in target.Steps )
        {
            foreach ( Item item in step.RequiredItems )
            {
                resources[ item.Code ] -= item.Quantity;
            }
        }
    }

    private bool TrySubtractResources( Dictionary<string, int> resources, CraftTarget target )
    {
        foreach ( CraftStep step in target.Steps )
        {
            foreach ( Item item in step.RequiredItems )
            {
                if ( !resources.ContainsKey( item.Code ) || resources[ item.Code ] < item.Quantity )
                {
                    return false;
                }

                resources[ item.Code ] -= item.Quantity;
            }
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

    private async Task<bool> CanCraftFinalItem( ItemDatum targetItem,
                                                Dictionary<string, int> availableResources,
                                                ICharacterService characterService )
    {
        if ( targetItem.Craft == null )
        {
            return false;
        }

        foreach ( Item craftComponent in targetItem.Craft.Items )
        {
            ItemDatum? informationAboutCraftComponent = _allItems.FirstOrDefault( i => i.Code == craftComponent.Code );
            if ( informationAboutCraftComponent == null )
            {
                return false;
            }

            if ( !HasEnoughResources( craftComponent, availableResources ) &&
                !CanCraftComponent( informationAboutCraftComponent, craftComponent, availableResources ) &&
                !await CanLooting( informationAboutCraftComponent, characterService ) )
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanLooting( ItemDatum informationAboutCraftComponent, ICharacterService characterService )
    {
        return await _targetLootingResolver.CanLooting( informationAboutCraftComponent, characterService );
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