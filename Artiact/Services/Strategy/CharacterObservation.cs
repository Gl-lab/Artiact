using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record CharacterObservation(string Name, int MapId, string Layer, int Capacity,
    ImmutableDictionary<string, int> Inventory)
{
    public long FreeUnits => Capacity - Inventory.Values.Sum(x => (long)x);
    public static CharacterObservation? Read(JsonElement raw)
    {
        try
        {
            var inventory = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            foreach (var item in raw.GetProperty("inventory").EnumerateArray())
            {
                int quantity = item.GetProperty("quantity").GetInt32();
                string code = item.GetProperty("code").GetString() ?? "";
                if (quantity < 0 || quantity > 0 && string.IsNullOrWhiteSpace(code)) return null;
                if (quantity > 0) inventory[code] = checked(inventory.GetValueOrDefault(code) + quantity);
            }
            var result = new CharacterObservation(raw.GetProperty("name").GetString()!, raw.GetProperty("map_id").GetInt32(),
                raw.GetProperty("layer").GetString()!, raw.GetProperty("inventory_max_items").GetInt32(), inventory.ToImmutable());
            return string.IsNullOrWhiteSpace(result.Name) || result.MapId <= 0 || string.IsNullOrWhiteSpace(result.Layer) ||
                result.Capacity < 0 || result.FreeUnits < 0 ? null : result;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        { return null; }
    }

    public static bool Preserved(JsonElement before, JsonElement after, params string[] mutable)
    {
        var ignored = mutable.Concat(["cooldown", "cooldown_expiration"]).ToHashSet(StringComparer.Ordinal);
        JsonElement Project(JsonElement value) => JsonSerializer.SerializeToElement(value.EnumerateObject()
            .Where(x => !ignored.Contains(x.Name)).ToDictionary(x => x.Name, x => x.Value));
        return StrategyObservation.Hash(Project(before)) == StrategyObservation.Hash(Project(after));
    }
}
