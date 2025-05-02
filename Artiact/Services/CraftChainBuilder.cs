using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class CraftChainBuilder : ICraftChainBuilder
{
    private readonly IGameClient _gameClient;
    private List<ItemDatum>? _allItems;

    public CraftChainBuilder( IGameClient gameClient )
    {
        _gameClient = gameClient;
    }

    public async Task<CraftTarget?> TryCreateCraftChain( ItemDatum targetItem,
                                                         Dictionary<string, int> availableResources )
    {
        _allItems ??= await _gameClient.GetItems();
        HashSet<string> visited = new();
        CraftTarget craftTarget = new() { FinalItem = targetItem };

        if ( CanCraftItem( targetItem, 1, availableResources, visited, craftTarget ) )
        {
            return craftTarget;
        }

        return null;
    }

    private bool CanCraftItem(
        ItemDatum item,
        int requiredQuantity,
        Dictionary<string, int> availableResources,
        HashSet<string> visited,
        CraftTarget craftTarget )
    {
        if ( visited.Contains( item.Code ) )
        {
            return false;
        }

        visited.Add( item.Code );

        if ( item.Craft == null )
        {
            return availableResources.ContainsKey( item.Code );
        }

        CraftStep craftStep = CreateCraftStep( item, requiredQuantity );

        foreach ( Item requiredItem in item.Craft.Items )
        {
            ItemDatum? requiredItemData = _allItems!.FirstOrDefault( i => i.Code == requiredItem.Code );
            if ( requiredItemData == null )
            {
                return false;
            }

            if ( !ProcessRequiredItem( requiredItem, craftStep, requiredItemData, availableResources, visited,
                    craftTarget ) )
            {
                return false;
            }
        }

        if ( !ValidateResources( craftStep, availableResources ) )
        {
            return false;
        }

        craftTarget.Steps.Add( craftStep );
        return true;
    }

    private CraftStep CreateCraftStep( ItemDatum item, int requiredQuantity )
    {
        return new CraftStep
        {
            Item = item,
            RequiredItems = new List<Item>(),
            Quantity = requiredQuantity
        };
    }

    private bool ProcessRequiredItem(
        Item requiredItem,
        CraftStep craftStep,
        ItemDatum itemData,
        Dictionary<string, int> availableResources,
        HashSet<string> visited,
        CraftTarget craftTarget )
    {
        int totalRequired = requiredItem.Quantity * craftStep.Quantity;
        int existingAmount = availableResources.GetValueOrDefault( requiredItem.Code );
        int needToCraft = Math.Max( 0, totalRequired - existingAmount );

        if ( needToCraft == 0 )
        {
            AddRequiredItem( craftStep, requiredItem );
            return true;
        }

        if ( itemData.Craft == null )
        {
            return false;
        }

        int craftQuantity = ( needToCraft + itemData.Craft.Quantity - 1 ) / itemData.Craft.Quantity;
        Dictionary<string, int> tempResources = new( availableResources );

        if ( CanCraftItem( itemData, craftQuantity, tempResources, visited, craftTarget ) )
        {
            AddRequiredItem( craftStep, requiredItem );
            int newAmount = existingAmount + craftQuantity * itemData.Craft.Quantity;
            availableResources[ requiredItem.Code ] = newAmount;
            return true;
        }

        return false;
    }

    private bool HasEnoughResources( Item requiredItem,
                                     int stepQuantity,
                                     Dictionary<string, int> availableResources )
    {
        return availableResources.ContainsKey( requiredItem.Code ) &&
            availableResources[ requiredItem.Code ] >= requiredItem.Quantity * stepQuantity;
    }

    private void AddRequiredItem( CraftStep craftStep, Item requiredItem )
    {
        craftStep.RequiredItems.Add( new Item
        {
            Code = requiredItem.Code,
            Quantity = requiredItem.Quantity * craftStep.Quantity
        } );
    }

    private bool ValidateResources( CraftStep craftStep, Dictionary<string, int> availableResources )
    {
        Dictionary<string, int> remainingResources = new( availableResources );
        foreach ( Item requiredItem in craftStep.RequiredItems )
        {
            if ( !remainingResources.ContainsKey( requiredItem.Code ) ||
                remainingResources[ requiredItem.Code ] < requiredItem.Quantity )
            {
                return false;
            }

            remainingResources[ requiredItem.Code ] -= requiredItem.Quantity;
        }

        return true;
    }
}