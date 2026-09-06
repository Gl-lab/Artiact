using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Combat;

public sealed record CombatObservation(string Name, int Level, int Xp, int MaxXp, int MaxHp,
    int MapId, string Layer, string Weapon, int Capacity, ImmutableDictionary<string, int> Inventory, CombatStats Stats)
{
    public long FreeUnits => Capacity - Inventory.Values.Sum(x => (long)x);
    public static CombatObservation? Read(JsonElement raw)
    {
        try
        {
            var stats = ReadStats(raw, false);
            if (stats is null) return null;
            var inventory = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
            foreach (var item in raw.GetProperty("inventory").EnumerateArray())
            {
                int quantity = item.GetProperty("quantity").GetInt32();
                string code = item.GetProperty("code").GetString() ?? "";
                if (quantity < 0 || quantity > 0 && string.IsNullOrWhiteSpace(code)) return null;
                if (quantity > 0) inventory[code] = checked(inventory.GetValueOrDefault(code) + quantity);
            }
            var result = new CombatObservation(raw.GetProperty("name").GetString()!,
                Int(raw, "level"), Int(raw, "xp"), Int(raw, "max_xp"), Int(raw, "max_hp"),
                Int(raw, "map_id"), raw.GetProperty("layer").GetString()!,
                raw.GetProperty("weapon_slot").GetString()!, Int(raw, "inventory_max_items"), inventory.ToImmutable(), stats);
            return string.IsNullOrWhiteSpace(result.Name) || result.Level < 1 || result.Xp < 0 ||
                result.MaxXp <= result.Xp || result.MaxHp is <= 0 or > 1_000_000 || stats.Hp > result.MaxHp ||
                result.MapId <= 0 || string.IsNullOrWhiteSpace(result.Layer) || result.Weapon is null ||
                result.Capacity < 0 || result.FreeUnits < 0 ? null : result;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }

    // First production subset is fire-only. Zero secondary attacks must be explicitly present.
    internal static CombatStats? ReadStats(JsonElement raw, bool monster)
    {
        if (raw.TryGetProperty("effects", out var effects) && effects.ValueKind != JsonValueKind.Null &&
            (effects.ValueKind != JsonValueKind.Array || effects.GetArrayLength() != 0)) return null;
        foreach (string element in new[] { "earth", "water", "air" })
        {
            if (Int(raw, "attack_" + element) != 0) return null;
            if (Int(raw, "res_" + element) is < 0 or > 100) return null;
            if (!monster && Int(raw, "dmg_" + element) is < 0 or > 1000) return null;
        }
        var stats = new CombatStats(Int(raw, "hp"), Int(raw, "attack_fire"),
            monster ? 0 : Int(raw, "dmg"), monster ? 0 : Int(raw, "dmg_fire"),
            Int(raw, "res_fire"), Int(raw, "critical_strike"));
        return CombatPrediction.Evaluate(stats, new CombatStats(1, 0)).Viability == CombatViability.Unknown ? null : stats;
    }

    internal static int Int(JsonElement raw, string name) => raw.GetProperty(name).GetInt32();
}
