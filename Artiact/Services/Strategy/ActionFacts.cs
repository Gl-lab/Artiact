using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SkillDelta(int Levels, long? Xp);
public sealed record ActionFacts(ImmutableDictionary<string, SkillDelta> Skills,
    ImmutableDictionary<string, long>? StockDelta, ImmutableDictionary<string, long> Inputs,
    long? HpDelta, decimal? UsefulProgress, double ObservationSeconds, double DispatchSeconds, double? WaitSeconds = null)
{
    public static ActionFacts Read(StrategyObservation before, StrategyObservation after, StrategyCandidate candidate, double observation, double dispatch)
    {
        var skills = ImmutableDictionary.CreateBuilder<string, SkillDelta>(StringComparer.Ordinal);
        foreach (var field in before.Character.EnumerateObject().Where(x => x.Name == "level" || x.Name.EndsWith("_level", StringComparison.Ordinal)))
        {
            string prefix = field.Name[..^5];
            if (!field.Value.TryGetInt32(out int level) || !after.Character.TryGetProperty(field.Name, out var endLevel) ||
                !endLevel.TryGetInt32(out int last) || !Number(before.Character, prefix + "xp", out long xp) ||
                !Number(after.Character, prefix + "xp", out long endXp)) continue;
            long? gain = last == level ? endXp - xp : last == level + 1 && Number(before.Character, prefix + "max_xp", out long max) ? max - xp + endXp : null;
            skills[prefix == "" ? "combat" : prefix[..^1]] = new(last - level, gain);
        }
        var first = Stock(before); var final = Stock(after);
        var delta = first is null || final is null ? null : first.Keys.Union(final.Keys, StringComparer.Ordinal)
            .ToImmutableDictionary(x => x, x => final.GetValueOrDefault(x) - first.GetValueOrDefault(x), StringComparer.Ordinal);
        var inputs = candidate.Command!.Id.StartsWith("Craft:", StringComparison.Ordinal) || candidate.Command.Id.StartsWith("Use:", StringComparison.Ordinal)
            ? (delta ?? ImmutableDictionary<string, long>.Empty).Where(x => x.Value < 0).ToImmutableDictionary(x => x.Key, x => -x.Value, StringComparer.Ordinal)
            : ImmutableDictionary<string, long>.Empty;
        decimal? useful = candidate.Path?.Goal is { } goal ? goal.Kind == "hp" ?
            Number(before.Character, "hp", out long priorHp) && Number(after.Character, "hp", out long nextHp) ? nextHp - priorHp : null :
            goal.Kind == "item" ? delta?.GetValueOrDefault(goal.Code) : skills.GetValueOrDefault(goal.Code)?.Xp : null;
        return new(skills.ToImmutable(), delta, inputs,
            Number(before.Character, "hp", out long hp) && Number(after.Character, "hp", out long endHp) ? endHp - hp : null,
            useful, observation, dispatch);
    }
    private static bool Number(JsonElement raw, string field, out long value)
    {
        value = 0; return raw.TryGetProperty(field, out var number) && number.TryGetInt64(out value);
    }
    public static ImmutableDictionary<string, long>? Stock(StrategyObservation state)
    {
        if (!state.Character.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array) return null;
        var result = ImmutableDictionary.CreateBuilder<string, long>(StringComparer.Ordinal);
        foreach (var item in inventory.EnumerateArray())
        {
            string code = item.GetProperty("code").GetString()!; long count = item.GetProperty("quantity").GetInt64();
            if (count > 0) result[code] = checked(result.GetValueOrDefault(code) + count);
        }
        if (state.Bank is not null) foreach (var item in state.Bank.Items) result[item.Key] = checked(result.GetValueOrDefault(item.Key) + item.Value);
        return result.ToImmutable();
    }
}

public static class MeasurementContext
{
    public static string Key(StrategyObservation state, StrategyCandidate candidate, string command)
    {
        var relevant = state.Character.EnumerateObject().Where(x => x.Name is "level" or "max_hp" or "inventory_max_items" or "layer" or "wisdom" or "haste" or "prospecting" or "initiative" or "critical_strike" or "dmg" or "effects" ||
            x.Name.EndsWith("_level", StringComparison.Ordinal) || x.Name.Contains("_slot", StringComparison.Ordinal) ||
            x.Name.StartsWith("attack_", StringComparison.Ordinal) || x.Name.StartsWith("dmg_", StringComparison.Ordinal) || x.Name.StartsWith("res_", StringComparison.Ordinal) ||
            command.StartsWith("Move:", StringComparison.Ordinal) && x.Name == "map_id" || (command == "Rest" || command.StartsWith("Use:", StringComparison.Ordinal)) && x.Name == "hp")
            .ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);
        string context = StrategyObservation.Hash(JsonSerializer.SerializeToElement(new { state.Policy, state.Catalogs, Relevant = relevant, Goal = candidate.Path?.Goal }));
        return "path:" + candidate.Id + ":" + command + ":" + context;
    }
}
