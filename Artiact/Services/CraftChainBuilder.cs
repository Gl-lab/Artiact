using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class CraftChainBuilder( IGameClient gameClient ) : ICraftChainBuilder
{
    public async Task<CraftTarget?> TryCreateCraftChain( ItemDatum targetItem,
        Dictionary<string, int> availableResources )
    {
        if (targetItem.Craft is null || availableResources.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value < 0))
            return null;
        var allItems = await gameClient.GetItems();
        if (allItems.Any(x => x is null || string.IsNullOrWhiteSpace(x.Code)) ||
            allItems.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != allItems.Count) return null;
        var catalog = allItems.ToDictionary(x => x.Code, StringComparer.Ordinal);
        var stock = new Dictionary<string, int>(availableResources, StringComparer.Ordinal);
        var path = new HashSet<string>(StringComparer.Ordinal);
        var result = new CraftTarget { FinalItem = targetItem };
        try { return Produce(targetItem, 1) ? result : null; }
        catch (OverflowException) { return null; }

        bool Produce(ItemDatum item, int batches)
        {
            if (batches <= 0 || item.Craft is not { Quantity: > 0, Items: not null } recipe ||
                recipe.Items.Count == 0 || !path.Add(item.Code)) return false;
            try
            {
                var required = new List<Item>();
                foreach (var ingredient in recipe.Items)
                {
                    if (ingredient is null || ingredient.Quantity <= 0 || string.IsNullOrWhiteSpace(ingredient.Code) ||
                        !catalog.TryGetValue(ingredient.Code, out var definition)) return false;
                    int amount = checked(ingredient.Quantity * batches);
                    int available = stock.GetValueOrDefault(ingredient.Code);
                    int deficit = Math.Max(0, amount - available);
                    if (deficit > 0)
                    {
                        if (definition.Craft is not { Quantity: > 0 } child) return false;
                        int childBatches = checked((int)(((long)deficit + child.Quantity - 1) / child.Quantity));
                        if (!Produce(definition, childBatches)) return false;
                    }
                    int remaining = stock.GetValueOrDefault(ingredient.Code) - amount;
                    if (remaining < 0) return false;
                    stock[ingredient.Code] = remaining;
                    required.Add(new Item { Code = ingredient.Code, Quantity = amount });
                }
                stock[item.Code] = checked(stock.GetValueOrDefault(item.Code) + checked(recipe.Quantity * batches));
                result.Steps.Add(new CraftStep { Item = item, Quantity = batches, RequiredItems = required });
                return true;
            }
            finally { path.Remove(item.Code); }
        }
    }
}
