using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class WearCraftTargetFinder : IWearCraftTargetFinder
{
    private readonly ICraftChainBuilder _chainBuilder;
    private readonly IGameClient _gameClient;
    private readonly ICraftTargetEvaluator _targetEvaluator;
    private readonly ITargetLootingResolver _targetLootingResolver;
    private readonly HashSet<string> _wearableTypes;
    private List<ItemDatum> _allItems = new();

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

    public async Task<CraftTarget?> FindTargetAsync(string code, List<Item> availableItems, ICharacterService characterService)
    {
        _allItems = await _gameClient.GetItems();
        var item = _allItems.SingleOrDefault(x => x.Code == code);
        if (item?.Craft is null || !_wearableTypes.Contains(item.Type)) return null;
        var (resources, prerequisite) = await CreatePlanningResources(item, CalculateAvailableResources(availableItems), characterService);
        if (resources is null) return null;
        var target = await _chainBuilder.TryCreateCraftChain(item, resources);
        if (target is not null) target.LootPrerequisite = prerequisite;
        return target;
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
            if ( !TryCalculateConsumption( bestTarget, remainingResources, out Dictionary<string, int> consumption ) )
            {
                break;
            }

            selectedTargets.Add( bestTarget );
            SubtractResources( remainingResources, consumption );

            if ( consumption.Values.All( quantity => quantity == 0 ) )
            {
                break;
            }
        }

        return selectedTargets;
    }

    private async Task<List<CraftTarget>> FindPossibleTargets( Dictionary<string, int> availableResources,
                                                               ICharacterService characterService )
    {
        List<CraftTarget> targets = new();

        foreach ( ItemDatum item in _allItems.Where( i => _wearableTypes.Contains( i.Type ) && i.Craft != null ) )
        {
            ( Dictionary<string, int>? planningResources, LootPrerequisite? prerequisite ) =
                await CreatePlanningResources( item, availableResources, characterService );
            if ( planningResources == null )
            {
                continue;
            }

            CraftTarget? craftTarget = await _chainBuilder.TryCreateCraftChain( item, planningResources );
            if ( craftTarget != null )
            {
                craftTarget.LootPrerequisite = prerequisite;
                targets.Add( craftTarget );
            }
        }

        return targets;
    }

    private async Task<( Dictionary<string, int>?, LootPrerequisite? )> CreatePlanningResources(
        ItemDatum targetItem,
        Dictionary<string, int> availableResources,
        ICharacterService characterService )
    {
        Dictionary<string, int> planningResources = new( availableResources );
        Dictionary<string, int> missingLeaves = new();
        if ( !TryCollectMissingLeaves( targetItem, 1, new Dictionary<string, int>( availableResources ),
                missingLeaves, new HashSet<string>() ) )
        {
            return ( null, null );
        }

        if ( missingLeaves.Count == 0 )
        {
            return ( planningResources, null );
        }

        // The planner intentionally supports one distinct loot prerequisite per craft target.
        if ( missingLeaves.Count != 1 )
        {
            return ( null, null );
        }

        KeyValuePair<string, int> missingLeaf = missingLeaves.Single();
        ItemDatum? leafData = _allItems.FirstOrDefault( item => item.Code == missingLeaf.Key );
        if ( leafData == null )
        {
            return ( null, null );
        }

        int requiredInventoryQuantity = availableResources.GetValueOrDefault( missingLeaf.Key ) + missingLeaf.Value;
        LootPrerequisite? prerequisite = await _targetLootingResolver.Resolve(
            leafData,
            requiredInventoryQuantity,
            characterService );
        if ( prerequisite == null )
        {
            return ( null, null );
        }

        planningResources[ missingLeaf.Key ] = requiredInventoryQuantity;
        return ( planningResources, prerequisite );
    }

    private bool TryCollectMissingLeaves( ItemDatum item,
                                          int requiredQuantity,
                                          Dictionary<string, int> stock,
                                          Dictionary<string, int> missingLeaves,
                                          HashSet<string> path )
    {
        if ( !path.Add( item.Code ) )
        {
            return false;
        }

        int fromStock = Math.Min( requiredQuantity, stock.GetValueOrDefault( item.Code ) );
        stock[ item.Code ] = stock.GetValueOrDefault( item.Code ) - fromStock;
        int remainingQuantity = requiredQuantity - fromStock;
        if ( remainingQuantity == 0 )
        {
            path.Remove( item.Code );
            return true;
        }

        if ( item.Craft == null )
        {
            missingLeaves[ item.Code ] = missingLeaves.GetValueOrDefault( item.Code ) + remainingQuantity;
            path.Remove( item.Code );
            return true;
        }

        if (item.Craft.Quantity <= 0 || item.Craft.Items is null || item.Craft.Items.Count == 0 ||
            item.Craft.Items.Any(x => x is null || x.Quantity <= 0 || string.IsNullOrWhiteSpace(x.Code)))
        {
            path.Remove(item.Code);
            return false;
        }
        int craftsNeeded = (int)(((long)remainingQuantity + item.Craft.Quantity - 1) / item.Craft.Quantity);
        if ((long)craftsNeeded * item.Craft.Quantity > int.MaxValue)
        {
            path.Remove(item.Code);
            return false;
        }
        foreach ( Item requiredItem in item.Craft.Items )
        {
            ItemDatum? requiredItemData = _allItems.FirstOrDefault( candidate => candidate.Code == requiredItem.Code );
            if ( requiredItemData == null || (long)requiredItem.Quantity * craftsNeeded > int.MaxValue ||
                 !TryCollectMissingLeaves( requiredItemData, requiredItem.Quantity * craftsNeeded, stock,
                     missingLeaves, path ) )
            {
                path.Remove( item.Code );
                return false;
            }
        }

        int surplus = craftsNeeded * item.Craft.Quantity - remainingQuantity;
        stock[ item.Code ] = stock.GetValueOrDefault( item.Code ) + surplus;
        path.Remove( item.Code );
        return true;
    }

    private bool TryCalculateConsumption( CraftTarget target,
                                          Dictionary<string, int> resources,
                                          out Dictionary<string, int> consumption )
    {
        Dictionary<string, int> stock = new( resources );
        if ( target.LootPrerequisite != null )
        {
            stock[ target.LootPrerequisite.ItemCode ] = Math.Max(
                stock.GetValueOrDefault( target.LootPrerequisite.ItemCode ),
                target.LootPrerequisite.RequiredQuantity );
        }

        Dictionary<string, int> initialStock = new( stock );
        foreach ( CraftStep step in target.Steps )
        {
            foreach ( Item requiredItem in step.RequiredItems )
            {
                if ( stock.GetValueOrDefault( requiredItem.Code ) < requiredItem.Quantity )
                {
                    consumption = new Dictionary<string, int>();
                    return false;
                }

                stock[ requiredItem.Code ] -= requiredItem.Quantity;
            }

            int producedQuantity = step.Quantity * ( step.Item.Craft?.Quantity ?? 1 );
            stock[ step.Item.Code ] = stock.GetValueOrDefault( step.Item.Code ) + producedQuantity;
        }

        consumption = resources.ToDictionary(
            resource => resource.Key,
            resource => Math.Min( resource.Value,
                Math.Max( 0, initialStock.GetValueOrDefault( resource.Key ) - stock.GetValueOrDefault( resource.Key ) ) ) );
        return true;
    }

    private void SubtractResources( Dictionary<string, int> resources, Dictionary<string, int> consumption )
    {
        foreach ( KeyValuePair<string, int> resource in consumption )
        {
            resources[ resource.Key ] -= resource.Value;
        }
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

}
