using Artiact.Client;
using Artiact.Models;
using Artiact.Models.Api;
using System.Collections.Generic;
using System.Linq;

namespace Artiact.Services;

public class WearCraftTargetFinder : IWearCraftTargetFinder
{
    private readonly IGameClient _gameClient;
    private readonly ICraftTargetEvaluator _targetEvaluator;
    private readonly ICraftChainBuilder _chainBuilder;
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

    public CraftTarget? FindTarget( List<Item> availableItems )
    {
        _allItems = _gameClient.GetItems().Result;
        List<CraftTarget> possibleTargets = FindPossibleTargets( availableItems );

        if ( !possibleTargets.Any() )
        {
            return null;
        }

        return _targetEvaluator.SelectBestTarget( possibleTargets );
    }

    private List<CraftTarget> FindPossibleTargets( List<Item> availableItems )
    {
        List<CraftTarget> targets = new List<CraftTarget>();
        Dictionary<string, int> availableResources = CalculateAvailableResources( availableItems );

        foreach ( ItemDatum item in _allItems.Where( i => _wearableTypes.Contains( i.Type ) && i.Craft != null ) )
        {
            if ( CanCraftFinalItem( item, availableResources ) )
            {
                CraftTarget? craftTarget = _chainBuilder.TryCreateCraftChain( item, availableResources );
                if ( craftTarget != null )
                {
                    targets.Add( craftTarget );
                }
            }
        }

        return targets;
    }

    private Dictionary<string, int> CalculateAvailableResources( List<Item> items )
    {
        Dictionary<string, int> resources = new Dictionary<string, int>();
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

        foreach ( Item craftItem in itemData.Craft.Items )
        {
            if ( !availableResources.ContainsKey( craftItem.Code ) ||
                availableResources[ craftItem.Code ] < craftItem.Quantity * requiredItem.Quantity )
            {
                return false;
            }
        }

        return true;
    }
}