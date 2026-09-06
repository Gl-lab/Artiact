using System.Text.Json;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

internal static class StrategyRules
{
    public static bool Progress(JsonElement before, JsonElement after, string prefix) =>
        after.GetProperty(prefix + "level").GetInt32() > before.GetProperty(prefix + "level").GetInt32() ||
        after.GetProperty(prefix + "level").GetInt32() == before.GetProperty(prefix + "level").GetInt32() &&
        after.GetProperty(prefix + "xp").GetInt32() > before.GetProperty(prefix + "xp").GetInt32();
    public static bool Stock(CombatObservation a, CombatObservation b) => a.Inventory.Count == b.Inventory.Count &&
        a.Inventory.All(x => b.Inventory.GetValueOrDefault(x.Key) == x.Value);
    public static bool Place(JsonElement map, CombatObservation state) => map.GetProperty("layer").GetString() == state.Layer &&
        map.GetProperty("access").GetProperty("type").GetString() == "standard" && Empty(map.GetProperty("access"), "conditions") &&
        Empty(map.GetProperty("interactions"), "transition");
    public static bool Empty(JsonElement element, string property) => !element.TryGetProperty(property, out var v) ||
        v.ValueKind == JsonValueKind.Null || v.ValueKind == JsonValueKind.Array && v.GetArrayLength() == 0;
    public static bool Moved(StrategyObservation after, CombatObservation before, int map) =>
        CombatObservation.Read(after.Character) is { } state && state.MapId == map && state.Layer == before.Layer &&
        state.Weapon == before.Weapon && state.Stats == before.Stats && state.Level == before.Level && state.Xp == before.Xp && Stock(state, before);
}

public sealed class GatheringStrategy(SkillMilestone goal, PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation)
    {
        StrategyCandidate Result(string? rejection, bool complete = false, AtomicCommand? command = null, decimal travel = 0) =>
            new("skill:" + goal.Skill, "skill", goal.Value, policy.GatherSeconds, travel, 0, rejection, complete, command);
        try
        {
            var state = CombatObservation.Read(observation.Character);
            if (state is null) return Result("UnsupportedObservation");
            string prefix = goal.Skill + "_";
            int level = observation.Character.GetProperty(prefix + "level").GetInt32();
            int xp = observation.Character.GetProperty(prefix + "xp").GetInt32();
            if (goal.Target <= 0 || level <= 0 || xp < 0 || observation.Character.GetProperty(prefix + "max_xp").GetInt32() <= xp)
                return Result("InvalidMilestone");
            if (level >= goal.Target) return Result(null, true);
            var maps = observation.Catalogs["maps"];
            if (maps.Select(x => x.GetProperty("map_id").GetInt32()).Distinct().Count() != maps.Length ||
                !StrategyRules.Place(maps.Single(x => x.GetProperty("map_id").GetInt32() == state.MapId), state)) return Result("UnsupportedAccess");
            var resources = observation.Catalogs["resources"];
            if (resources.Select(x => x.GetProperty("code").GetString()).Distinct().Count() != resources.Length) return Result("InvalidCatalog");
            foreach (var resource in resources.Where(x => x.GetProperty("skill").GetString() == goal.Skill &&
                x.GetProperty("level").GetInt32() > 0 && x.GetProperty("level").GetInt32() <= level)
                .OrderByDescending(x => x.GetProperty("level").GetInt32()).ThenBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal))
            {
                if (!StrategyRules.Empty(resource, "conditions")) continue;
                var drops = resource.GetProperty("drops");
                if (drops.GetArrayLength() != 1) continue;
                var drop = drops[0];
                string code = drop.GetProperty("code").GetString()!;
                int min = drop.GetProperty("min_quantity").GetInt32(), max = drop.GetProperty("max_quantity").GetInt32();
                if (string.IsNullOrWhiteSpace(code) || min <= 0 || max < min || drop.GetProperty("rate").GetInt32() != 1) continue;
                if (state.FreeUnits < max) return Result("InventoryPressure");
                var map = maps.Where(x => StrategyRules.Place(x, state) &&
                    x.GetProperty("interactions").GetProperty("content") is { ValueKind: JsonValueKind.Object } content &&
                    content.GetProperty("type").GetString() == "resource" && content.GetProperty("code").GetString() == resource.GetProperty("code").GetString())
                    .OrderBy(x => x.GetProperty("map_id").GetInt32() == state.MapId ? 0 : 1)
                    .ThenBy(x => x.GetProperty("map_id").GetInt32()).FirstOrDefault();
                if (map.ValueKind == JsonValueKind.Undefined) continue;
                int id = map.GetProperty("map_id").GetInt32();
                if (id != state.MapId) return Result(null, command: port.Combat(observation, CombatCommand.Move,
                    new(id, state.Layer, "", new(1, 0), true), null, false, after => StrategyRules.Moved(after, state, id)), travel: policy.MoveSeconds);
                return Result(null, command: port.Gather(observation, goal.Skill, after =>
                {
                    var changed = CombatObservation.Read(after.Character);
                    if (changed is null || changed.MapId != state.MapId || changed.Layer != state.Layer || changed.Weapon != state.Weapon ||
                        changed.Stats != state.Stats || !StrategyRules.Progress(observation.Character, after.Character, prefix)) return false;
                    long delta = (long)changed.Inventory.GetValueOrDefault(code) - state.Inventory.GetValueOrDefault(code);
                    return delta >= min && delta <= max && changed.Inventory.Remove(code).Count == state.Inventory.Remove(code).Count &&
                        state.Inventory.Where(x => x.Key != code).All(x => changed.Inventory.GetValueOrDefault(x.Key) == x.Value);
                }));
            }
            return Result("NoSupportedResource");
        }
        catch (Exception) { return Result("InvalidCatalogOrState"); }
    }
}

public sealed class CombatMilestoneStrategy(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation)
    {
        StrategyCandidate Result(string? rejection, bool complete = false, AtomicCommand? command = null, decimal travel = 0) =>
            new("combat", "combat", policy.CombatValue, policy.FightSeconds, travel, policy.RestSeconds, rejection, complete, command);
        try
        {
            var state = CombatObservation.Read(observation.Character);
            if (state is null) return Result("UnsupportedObservation");
            if (state.Level >= policy.CombatTarget) return Result(null, true);
            if (state.FreeUnits < 1) return Result("InventoryPressure");
            var world = CombatCatalog.Resolve(state, policy.Monster, observation.Catalogs["monsters"], observation.Catalogs["maps"], observation.Catalogs["items"]);
            if (world.Destination is not { } destination) return Result("UnsupportedAccessOrEquipment");
            var prediction = CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, destination.Monster);
            if (prediction.Viability != CombatViability.Safe) return Result(prediction.Viability.ToString());
            var command = state.Stats.Hp < state.MaxHp ? CombatCommand.Rest : state.MapId != destination.MapId ? CombatCommand.Move : CombatCommand.Fight;
            return Result(null, command: port.Combat(observation, command, destination, null, command == CombatCommand.Fight, after =>
            {
                var changed = CombatObservation.Read(after.Character);
                if (changed is null || changed.Layer != state.Layer || changed.Weapon != state.Weapon) return false;
                if (command == CombatCommand.Move) return StrategyRules.Moved(after, state, destination.MapId);
                if (changed.MapId != state.MapId) return false;
                if (command == CombatCommand.Rest) return changed.Stats.Hp > state.Stats.Hp && StrategyRules.Stock(state, changed);
                return changed.Stats.Hp >= state.Stats.Hp - prediction.MaximumLoss && StrategyRules.Progress(observation.Character, after.Character, "") &&
                    state.Inventory.All(x => changed.Inventory.GetValueOrDefault(x.Key) >= x.Value);
            }), travel: state.MapId != destination.MapId ? policy.MoveSeconds : 0);
        }
        catch (Exception) { return Result("InvalidCatalogOrState"); }
    }
}

public sealed class EquipmentStrategy(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation)
    {
        StrategyCandidate Result(string? rejection, bool complete = false, AtomicCommand? command = null, bool swap = true) =>
            new("equipment", "equipment", policy.EquipmentValue, policy.EquipmentSeconds * (swap ? 2 : 1), 0, 0, rejection, complete, command);
        try
        {
            var state = CombatObservation.Read(observation.Character);
            if (state is null) return Result("UnsupportedObservation");
            if (state.Weapon == policy.Equipment) return Result(null, true);
            if (state.Inventory.GetValueOrDefault(policy.Equipment) < 1) return Result("NotOwned");
            var items = observation.Catalogs["items"];
            var target = items.Single(x => x.GetProperty("code").GetString() == policy.Equipment);
            if (!CombatCatalog.TryWeapon(target, state.Level, out int attack)) return Result("UnsupportedEquipment");
            int oldAttack = 0;
            if (state.Weapon.Length > 0 && !CombatCatalog.TryWeapon(items.Single(x => x.GetProperty("code").GetString() == state.Weapon), state.Level, out oldAttack))
                return Result("UnsupportedEquipment");
            var projected = state.Stats with { Attack = checked(state.Stats.Attack - oldAttack + attack) };
            var world = CombatCatalog.Resolve(state with { Weapon = policy.Equipment, Stats = projected }, policy.Monster,
                observation.Catalogs["monsters"], observation.Catalogs["maps"], items);
            if (world.Destination is not { } destination) return Result("UnsupportedAccess");
            var baseline = CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, destination.Monster);
            var improved = CombatPrediction.Evaluate(projected with { Hp = state.MaxHp }, destination.Monster);
            if (improved.Viability != CombatViability.Safe || baseline.Viability == CombatViability.Safe && improved.MaximumLoss >= baseline.MaximumLoss)
                return Result("NoImprovement");
            bool swap = state.Weapon.Length > 0;
            if (swap && state.FreeUnits < 1) return Result("InventoryPressure");
            string code = swap ? state.Weapon : policy.Equipment;
            int count = state.Inventory.GetValueOrDefault(code) + (swap ? 1 : -1);
            var expected = count == 0 ? state.Inventory.Remove(code) : state.Inventory.SetItem(code, count);
            return Result(null, command: port.Combat(observation, swap ? CombatCommand.Unequip : CombatCommand.Equip,
                destination, policy.Equipment, true, after =>
                {
                    var changed = CombatObservation.Read(after.Character);
                    return changed is not null && changed.MapId == state.MapId && changed.Layer == state.Layer &&
                        changed.Weapon == (swap ? "" : policy.Equipment) &&
                        changed.Stats == (swap ? state.Stats with { Attack = state.Stats.Attack - oldAttack } : projected) &&
                        expected.Count == changed.Inventory.Count && expected.All(x => changed.Inventory.GetValueOrDefault(x.Key) == x.Value);
                }), swap: swap);
        }
        catch (Exception) { return Result("InvalidCatalogOrState"); }
    }
}
