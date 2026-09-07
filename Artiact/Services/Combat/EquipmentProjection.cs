using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Artiact.Services.Combat;

public static class EquipmentProjection
{
    public static readonly ImmutableArray<string> Fields = ["attack_fire", "attack_earth", "attack_water", "attack_air",
        "dmg", "dmg_fire", "dmg_earth", "dmg_water", "dmg_air", "res_fire", "res_earth", "res_water", "res_air", "critical_strike"];

    public static bool Supported(JsonElement item, string slot, int level)
    {
        try
        {
            if (slot is not ("weapon" or "shield") || item.GetProperty("type").GetString() != slot ||
                item.GetProperty("level").GetInt32() is < 1 || item.GetProperty("level").GetInt32() > level ||
                !Strategy.StrategyRules.Empty(item, "conditions")) return false;
            var effects = item.GetProperty("effects").EnumerateArray().ToArray();
            return effects.Length is > 0 and <= 14 && effects.Select(x => x.GetProperty("code").GetString()).Distinct().Count() == effects.Length &&
                effects.All(x => Fields.Contains(x.GetProperty("code").GetString()!) && x.GetProperty("value").GetInt32() is >= 0 and <= 10000);
        }
        catch (Exception) { return false; }
    }

    public static JsonElement? Replace(JsonElement character, IReadOnlyList<JsonElement> items, string slot, string code)
    {
        try
        {
            if (slot is not ("weapon" or "shield")) return null;
            int level = character.GetProperty("level").GetInt32();
            string old = character.GetProperty(slot + "_slot").GetString()!;
            var node = JsonNode.Parse(character.GetRawText())!;
            foreach (var change in new[] { (Code: old, Sign: -1), (Code: code, Sign: 1) })
            {
                if (change.Code == "") continue;
                var item = items.Single(x => x.GetProperty("code").GetString() == change.Code);
                if (!Supported(item, slot, level)) return null;
                foreach (var effect in item.GetProperty("effects").EnumerateArray())
                {
                    string field = effect.GetProperty("code").GetString()!;
                    node[field] = checked(node[field]!.GetValue<int>() + change.Sign * effect.GetProperty("value").GetInt32());
                    if (node[field]!.GetValue<int>() < 0) return null;
                }
            }
            node[slot + "_slot"] = code;
            var result = JsonSerializer.SerializeToElement(node);
            return CombatObservation.Read(result) is null ? null : result;
        }
        catch (Exception) { return null; }
    }
}
