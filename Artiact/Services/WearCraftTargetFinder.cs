using Artiact.Client;
using Artiact.Models;
using Artiact.Models.Api;
using System.Collections.Generic;
using System.Linq;

namespace Artiact.Services;

public interface IWearCraftTargetFinder
{
    CraftTarget? FindTarget( List<Item> items );
}

public class CraftTarget
{
    public ItemDatum FinalItem { get; set; }
    public List<CraftStep> Steps { get; set; } = new();
}

public class CraftStep
{
    public ItemDatum Item { get; set; }
    public List<Item> RequiredItems { get; set; }
}

public class WearCraftTargetFinder : IWearCraftTargetFinder
{
    private readonly IGameClient _gameClient;
    private List<ItemDatum> _allItems;

    private readonly HashSet<string> _wearableTypes = new()
    {
        "weapon",
        "boots",
        "helmet",
        "body_armor",
        "leg_armor",
        "ring",
        "amulet",
        "shield"
    };

    public WearCraftTargetFinder( IGameClient gameClient )
    {
        _gameClient = gameClient;
    }

    public CraftTarget? FindTarget( List<Item> availableItems )
    {
        _allItems = _gameClient.GetItems().Result;

        // Получаем все возможные предметы для крафта на основе имеющихся ресурсов
        List<CraftTarget> possibleTargets = FindPossibleTargets( availableItems );

        // Если нет возможных целей, возвращаем null
        if ( !possibleTargets.Any() )
            return null;

        // Выбираем лучший предмет для крафта (например, с наивысшим уровнем)
        CraftTarget bestTarget = possibleTargets.OrderByDescending( t => t.FinalItem.Level ).First();

        return bestTarget;
    }

    private List<CraftTarget> FindPossibleTargets( List<Item> availableItems )
    {
        List<CraftTarget> targets = new List<CraftTarget>();
        Dictionary<string, int> availableResources = new Dictionary<string, int>();

        // Подсчитываем количество доступных ресурсов
        foreach ( Item item in availableItems )
        {
            if ( !availableResources.ContainsKey( item.Code ) )
                availableResources[ item.Code ] = 0;
            availableResources[ item.Code ] += item.Quantity;
        }

        // Ищем все предметы, которые можно скрафтить
        foreach ( ItemDatum item in _allItems.Where( i => _wearableTypes.Contains( i.Type ) && i.Craft != null ) )
        {
            CraftTarget? craftTarget = TryCreateCraftChain( item, availableResources );
            if ( craftTarget != null )
            {
                targets.Add( craftTarget );
            }
        }

        return targets;
    }

    private CraftTarget? TryCreateCraftChain( ItemDatum targetItem, Dictionary<string, int> availableResources )
    {
        HashSet<string> visited = new HashSet<string>();
        CraftTarget craftTarget = new CraftTarget { FinalItem = targetItem };

        if ( CanCraftItem( targetItem, availableResources, visited, craftTarget ) )
        {
            return craftTarget;
        }

        return null;
    }

    private bool CanCraftItem( ItemDatum item,
                               Dictionary<string, int> availableResources,
                               HashSet<string> visited,
                               CraftTarget craftTarget )
    {
        if ( visited.Contains( item.Code ) )
            return false;

        visited.Add( item.Code );

        // Если у предмета нет рецепта крафта, проверяем есть ли он в доступных ресурсах
        if ( item.Craft == null )
        {
            return availableResources.ContainsKey( item.Code );
        }

        CraftStep craftStep = new CraftStep
        {
            Item = item,
            RequiredItems = new List<Item>()
        };

        // Проверяем каждый требуемый предмет для крафта
        foreach ( Item requiredItem in item.Craft.Items )
        {
            ItemDatum? itemData = _allItems.FirstOrDefault( i => i.Code == requiredItem.Code );
            if ( itemData == null )
                return false;

            // Если предмет доступен напрямую
            if ( availableResources.ContainsKey( requiredItem.Code ) &&
                availableResources[ requiredItem.Code ] >= requiredItem.Quantity )
            {
                craftStep.RequiredItems.Add( new Item
                {
                    Code = requiredItem.Code,
                    Quantity = requiredItem.Quantity
                } );
                continue;
            }

            // Пробуем создать промежуточный предмет
            Dictionary<string, int> tempResources = new Dictionary<string, int>( availableResources );
            if ( CanCraftItem( itemData, tempResources, visited, craftTarget ) )
            {
                craftStep.RequiredItems.Add( new Item
                {
                    Code = requiredItem.Code,
                    Quantity = requiredItem.Quantity
                } );
                continue;
            }

            return false;
        }

        craftTarget.Steps.Add( craftStep );
        return true;
    }
}