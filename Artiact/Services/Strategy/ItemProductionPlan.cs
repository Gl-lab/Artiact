using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record ProductionStep(string Kind, string Code, int Quantity, int Output = 0, string Skill = "",
    ImmutableDictionary<string, int>? Ingredients = null);
public sealed record ItemProductionPlan(ImmutableArray<ProductionStep> Steps, string? Rejection)
{
    public static ItemProductionPlan Build(string code, int quantity, IReadOnlyDictionary<string, int> inventory,
        IReadOnlyDictionary<string, int> bank, IReadOnlyList<JsonElement> items, IReadOnlyList<JsonElement> resources,
        IReadOnlyList<JsonElement>? monsters = null)
    {
        var steps = ImmutableArray.CreateBuilder<ProductionStep>();
        try
        {
            if (quantity is <= 0 or > 10000 || inventory.Any(x => x.Value < 0) || bank.Any(x => x.Value < 0)) throw new InvalidOperationException("InvalidStockOrTarget");
            var stock = inventory.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var stored = bank.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            var catalog = items.ToDictionary(x => x.GetProperty("code").GetString()!, StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            void Need(string item, int count)
            {
                if (count <= 0 || steps.Count >= 100 || visiting.Count >= 30) throw new InvalidOperationException("PlanBoundExceeded");
                int owned = Math.Min(count, stock.GetValueOrDefault(item));
                stock[item] = stock.GetValueOrDefault(item) - owned; count -= owned;
                if (count == 0) return;
                int fromBank = Math.Min(count, stored.GetValueOrDefault(item));
                if (fromBank > 0) { stored[item] -= fromBank; count -= fromBank; steps.Add(new("Withdraw", item, fromBank)); }
                if (count == 0) return;
                if (!visiting.Add(item)) throw new InvalidOperationException("RecipeCycle");
                if (catalog.TryGetValue(item, out var raw) && raw.TryGetProperty("craft", out var craft) && craft.ValueKind == JsonValueKind.Object)
                {
                    if (!StrategyRules.Empty(raw, "conditions")) throw new InvalidOperationException("UnsupportedRecipeConditions");
                    int output = craft.GetProperty("quantity").GetInt32();
                    string skill = craft.GetProperty("skill").GetString()!;
                    if (output <= 0 || string.IsNullOrWhiteSpace(skill) || craft.GetProperty("level").GetInt32() <= 0) throw new InvalidOperationException("InvalidRecipe");
                    int batches = checked((count + output - 1) / output);
                    var ingredients = craft.GetProperty("items").EnumerateArray().ToImmutableDictionary(x => x.GetProperty("code").GetString()!,
                        x => checked(x.GetProperty("quantity").GetInt32() * batches));
                    if (ingredients.IsEmpty || ingredients.Any(x => string.IsNullOrWhiteSpace(x.Key) || x.Value <= 0)) throw new InvalidOperationException("InvalidRecipe");
                    foreach (var ingredient in ingredients.OrderBy(x => x.Key, StringComparer.Ordinal)) Need(ingredient.Key, ingredient.Value);
                    int made = checked(output * batches);
                    steps.Add(new("Craft", item, batches, made, skill, ingredients));
                    stock[item] = checked(stock.GetValueOrDefault(item) + made - count);
                }
                else if (resources.Any(x => x.GetProperty("drops").EnumerateArray().Any(d => d.GetProperty("code").GetString() == item)))
                    steps.Add(new("Gather", item, count));
                else if (monsters?.Any(x => x.GetProperty("drops").EnumerateArray().Any(d => d.GetProperty("code").GetString() == item &&
                    d.GetProperty("rate").GetInt32() > 0 && d.GetProperty("max_quantity").GetInt32() > 0)) == true)
                    steps.Add(new("Loot", item, count));
                else throw new InvalidOperationException("UnavailableIngredient:" + item);
                visiting.Remove(item);
            }
            Need(code, quantity);
            return new(steps.ToImmutable(), null);
        }
        catch (InvalidOperationException ex) { return new([], ex.Message); }
        catch (Exception) { return new([], "InvalidRecipeOrStock"); }
    }
}
