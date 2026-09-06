using System.Text.Json;
using Artiact.Client;

namespace Artiact.Services.Combat;

// Combat deliberately bypasses legacy coordinate-only cache DTOs. The wire fields
// must remain available to distinguish missing data from explicit zero/defaults.
public sealed class CombatCatalog(IGameHttpClient http)
{
    public async Task<(CombatDestination? Destination, CombatGear? Gear)> ResolveAsync(
        CombatObservation state, string monsterCode, CancellationToken cancellationToken)
    {
        var monsters = await ReadPagesAsync("monsters", cancellationToken);
        var maps = await ReadPagesAsync("maps", cancellationToken);
        var items = await ReadPagesAsync("items", cancellationToken);
        return Resolve(state, monsterCode, monsters, maps, items);
    }

    public static (CombatDestination? Destination, CombatGear? Gear) Resolve(CombatObservation state,
        string monsterCode, IReadOnlyList<JsonElement> monsters, IReadOnlyList<JsonElement> maps, IReadOnlyList<JsonElement> items)
    {
        try
        {
            var matched = monsters.Where(x => x.GetProperty("code").GetString() == monsterCode).ToArray();
            if (matched.Length != 1 || matched[0].GetProperty("type").GetString() != "normal") return (null, null);
            var stats = CombatObservation.ReadStats(matched[0], true);
            if (stats is null) return (null, null);
            if (maps.Select(x => CombatObservation.Int(x, "map_id")).Distinct().Count() != maps.Count) return (null, null);
            var destinations = maps.Where(x => x.GetProperty("layer").GetString() == state.Layer &&
                x.GetProperty("access").GetProperty("type").GetString() == "standard" &&
                Empty(x.GetProperty("access"), "conditions") && Empty(x.GetProperty("interactions"), "transition") &&
                x.GetProperty("interactions").TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object &&
                content.GetProperty("type").GetString() == "monster" && content.GetProperty("code").GetString() == monsterCode)
                .OrderBy(x => CombatObservation.Int(x, "map_id")).ToArray();
            // Both current and target map require explicit supported access; no implicit cross-layer travel.
            var current = maps.SingleOrDefault(x => CombatObservation.Int(x, "map_id") == state.MapId);
            if (current.ValueKind == JsonValueKind.Undefined || current.GetProperty("layer").GetString() != state.Layer ||
                current.GetProperty("access").GetProperty("type").GetString() != "standard" ||
                !Empty(current.GetProperty("access"), "conditions") || !Empty(current.GetProperty("interactions"), "transition") ||
                destinations.Length == 0) return (null, null);
            var destination = new CombatDestination(CombatObservation.Int(destinations[0], "map_id"), state.Layer, monsterCode, stats, true);
            var catalog = items.ToDictionary(x => x.GetProperty("code").GetString()!, StringComparer.Ordinal);
            if (!catalog.TryGetValue(state.Weapon, out var currentWeapon) || !TryWeapon(currentWeapon, state.Level, out int oldAttack))
                return (null, null);
            var baseline = CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, stats);
            CombatGear? gear = null;
            long bestLoss = baseline.Viability == CombatViability.Safe ? baseline.MaximumLoss : long.MaxValue;
            foreach (var pair in catalog.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (pair.Key == state.Weapon || state.Inventory.GetValueOrDefault(pair.Key) < 1 ||
                    !TryWeapon(pair.Value, state.Level, out int attack)) continue;
                var projected = state.Stats with { Attack = checked(state.Stats.Attack - oldAttack + attack), Hp = state.MaxHp };
                var prediction = CombatPrediction.Evaluate(projected, stats);
                if (prediction.Viability == CombatViability.Safe && prediction.MaximumLoss < bestLoss)
                {
                    gear = new(pair.Key, projected);
                    bestLoss = prediction.MaximumLoss;
                }
            }
            return (destination, gear);
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            return (null, null);
        }
    }

    internal static bool TryWeapon(JsonElement item, int level, out int attack)
    {
        attack = 0;
        if (item.GetProperty("type").GetString() != "weapon" || CombatObservation.Int(item, "level") is < 1 ||
            CombatObservation.Int(item, "level") > level || !Empty(item, "conditions")) return false;
        var effects = item.GetProperty("effects");
        if (effects.ValueKind != JsonValueKind.Array || effects.GetArrayLength() != 1) return false;
        var effect = effects[0];
        if (effect.GetProperty("code").GetString() != "attack_fire") return false;
        attack = CombatObservation.Int(effect, "value");
        return attack is >= 0 and <= 10_000;
    }

    private static bool Empty(JsonElement raw, string property) => !raw.TryGetProperty(property, out var value) ||
        value.ValueKind == JsonValueKind.Null || value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0;

    internal async Task<IReadOnlyList<JsonElement>> ReadPagesAsync(string endpoint, CancellationToken token)
    {
        var result = new List<JsonElement>();
        int expectedPages = 1;
        for (int page = 1; page <= expectedPages; page++)
        {
            token.ThrowIfCancellationRequested();
            using var response = await http.GetAsync($"/{endpoint}?page={page}");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = document.RootElement;
            int pages = CombatObservation.Int(root, "pages");
            if (pages < 0 || pages > 1000 || CombatObservation.Int(root, "page") != page ||
                page > 1 && pages != expectedPages) throw new InvalidOperationException("Invalid combat catalog pagination.");
            expectedPages = pages;
            result.AddRange(root.GetProperty("data").EnumerateArray().Select(x => x.Clone()));
        }
        return result;
    }
}
