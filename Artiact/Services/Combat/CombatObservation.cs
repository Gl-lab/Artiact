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
            foreach (string slot in new[] { "rune_slot", "utility1_slot", "utility2_slot" })
                if (raw.TryGetProperty(slot, out var equipped) && equipped.GetString() != "") return null;
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

    // Every channel must be explicitly present; unsupported effects remain fail-closed.
    internal static CombatStats? ReadStats(JsonElement raw, bool monster)
    {
        if (raw.TryGetProperty("effects", out var effects) && effects.ValueKind != JsonValueKind.Null &&
            (effects.ValueKind != JsonValueKind.Array || effects.GetArrayLength() != 0)) return null;
        ElementStats? Channel(string element)
        {
            var channel = new ElementStats(Int(raw, "attack_" + element), monster ? 0 : Int(raw, "dmg_" + element), Int(raw, "res_" + element));
            return channel == new ElementStats() ? null : channel;
        }
        var stats = new CombatStats(Int(raw, "hp"), Int(raw, "attack_fire"),
            monster ? 0 : Int(raw, "dmg"), monster ? 0 : Int(raw, "dmg_fire"),
            Int(raw, "res_fire"), Int(raw, "critical_strike"), Channel("earth"), Channel("water"), Channel("air"));
        return CombatPrediction.Evaluate(stats, new CombatStats(1, 0)).Viability == CombatViability.Unknown ? null : stats;
    }

    internal static int Int(JsonElement raw, string name) => raw.GetProperty(name).GetInt32();
}
