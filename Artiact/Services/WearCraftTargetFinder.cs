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

    public WearCraftTargetFinder(
        IGameClient gameClient,
        ICraftTargetEvaluator targetEvaluator,
        ICraftChainBuilder chainBuilder )
    {
        _gameClient = gameClient;
        _targetEvaluator = targetEvaluator;
        _chainBuilder = chainBuilder;
        _wearableTypes = new HashSet<string>
        {
            "weapon", "boots", "helmet", "body_armor",
            "leg_armor", "ring", "amulet", "shield"
        };
    }

    public async Task<List<CraftTarget>> FindTargets( List<Item> availableItems )
    {
        _allItems = await _gameClient.GetItems();
        return await FindOptimalTargets( availableItems );
    }

    private async Task<List<CraftTarget>> FindOptimalTargets( List<Item> availableItems )
    {
        List<CraftTarget> selectedTargets = new();
        Dictionary<string, int> remainingResources = CalculateAvailableResources( availableItems );

        while ( true )
        {
            List<CraftTarget> possibleTargets = await FindPossibleTargets( remainingResources );

            if ( !possibleTargets.Any() )
            {
                break;
            }

            CraftTarget bestTarget = _targetEvaluator.SelectBestTarget( possibleTargets );
            if ( !CanCraftWithRemainingResources( bestTarget, remainingResources ) )
            {
                break;
            }

            selectedTargets.Add( bestTarget );
            SubtractResources( remainingResources, bestTarget );
        }

        return selectedTargets;
    }

    private async Task<List<CraftTarget>> FindPossibleTargets( Dictionary<string, int> availableResources )
    {
        List<CraftTarget> targets = new();

        foreach ( ItemDatum item in _allItems.Where( i => _wearableTypes.Contains( i.Type ) && i.Craft != null ) )
        {
            if ( CanCraftFinalItem( item, availableResources ) )
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

    private bool CanCraftFinalItem( ItemDatum item, Dictionary<string, int> availableResources )
    {
        if ( item.Craft == null )
        {
            return false;
        }

        foreach ( Item requiredItem in item.Craft.Items )
        {
            ItemDatum? itemData = _allItems.FirstOrDefault( i => i.Code == requiredItem.Code );
            if ( itemData == null )
            {
                return false;
            }

            if ( !HasEnoughResources( requiredItem, availableResources ) &&
                !CanCraftRequiredItem( itemData, requiredItem, availableResources ) )
            {
                return false;
            }
        }

        return true;
    }

    private bool HasEnoughResources( Item requiredItem, Dictionary<string, int> availableResources )
    {
        return availableResources.ContainsKey( requiredItem.Code ) &&
            availableResources[ requiredItem.Code ] >= requiredItem.Quantity;
    }

    private bool CanCraftRequiredItem( ItemDatum itemData,
                                       Item requiredItem,
                                       Dictionary<string, int> availableResources )
    {
        if ( itemData.Craft == null )
        {
            return false;
        }

        int existingQuantity = availableResources.ContainsKey( requiredItem.Code )
            ? availableResources[ requiredItem.Code ]
            : 0;

        int remainingNeeded = requiredItem.Quantity - existingQuantity;
        if ( remainingNeeded <= 0 )
        {
            return true;
        }

        foreach ( Item craftItem in itemData.Craft.Items )
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