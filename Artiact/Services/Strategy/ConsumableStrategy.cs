using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class ConsumableStrategy(IProgressionStrategy parent, PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation) => Apply(observation, parent.Evaluate(observation));
    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation) => parent.EvaluateAll(observation)
        .Select(candidate => candidate.Rejection is null ? Apply(observation, candidate) : candidate);
    private StrategyCandidate Apply(StrategyObservation observation, StrategyCandidate candidate)
    {
        if (candidate.Complete) return candidate;
        var food = policy.Consumable!;
        StrategyCandidate Reject(string reason) => candidate with { Rejection = reason, Command = null };
        try
        {
            var state = CharacterObservation.Read(observation.Character)!;
            int hp = observation.Character.GetProperty("hp").GetInt32(), maxHp = observation.Character.GetProperty("max_hp").GetInt32();
            if (hp <= 0 || maxHp <= 0 || hp > maxHp) return Reject("InvalidHealingState");
            var item = observation.Catalogs["items"].Single(x => x.GetProperty("code").GetString() == food.Code);
            if (item.GetProperty("type").GetString() != "consumable" || !StrategyRules.Empty(item, "conditions") ||
                item.GetProperty("effects") is not { ValueKind: JsonValueKind.Array } effects || effects.GetArrayLength() != 1 ||
                effects[0].GetProperty("code").GetString() != "heal" || effects[0].GetProperty("value").GetInt32() <= 0)
                return Reject("UnsupportedConsumableEffectOrCondition");
            int heal = effects[0].GetProperty("value").GetInt32();
            int owned = state.Inventory.GetValueOrDefault(food.Code), total = checked(owned + (observation.Bank?.Items.GetValueOrDefault(food.Code) ?? 0));
            int reserve = Math.Max(food.Reserve, Math.Max(policy.Production?.Reserved.GetValueOrDefault(food.Code) ?? 0,
                policy.Items.IsDefault ? 0 : policy.Items.Where(x => x.Code == food.Code).Select(x => x.Quantity).DefaultIfEmpty(0).Max()));
            if (food.TargetStock <= reserve) return Reject("ConsumableReserveConflict");
            int used = observation.Context.Used.GetValueOrDefault("use:" + food.Code);
            bool needsHeal = hp < maxHp && (long)hp * 100 < (long)maxHp * food.HpBelowPercent;
            decimal rest = Math.Max(3, Math.Ceiling((decimal)(maxHp - hp) * 100 / maxHp));
            if (needsHeal && food.AllowRest && (used >= food.MaxUsed || rest <= food.UseSeconds + (total <= reserve ? food.PreparationSeconds : 0)))
                return candidate with { Rejection = null, ActionSeconds = rest, TravelSeconds = 0, RecoverySeconds = 0,
                    Command = port.Combat(observation, CombatCommand.Rest, new(state.MapId, state.Layer, "", new(1, 0), true), null, false,
                        after => after.Character.GetProperty("hp").GetInt32() == maxHp && CharacterObservation.Preserved(observation.Character, after.Character, "hp"))
                        with { RefillCode = food.Code, Refilling = false } };
            bool refill = total < Math.Max(food.MinimumStock, reserve + 1) && (needsHeal || used > 0) ||
                observation.Context.Refilling.GetValueOrDefault(food.Code) && total < food.TargetStock;
            if (refill) return Supply(food.TargetStock, false, true);
            if (needsHeal)
            {
                if (used >= food.MaxUsed) return Reject("ConsumableUseBudgetExhausted");
                if (total <= reserve) return Supply(food.TargetStock, false, true);
                if (owned == 0) return Supply(food.ParentItem == "recovery" ? Math.Min((int)Math.Ceiling((maxHp - hp) / (decimal)heal), total - reserve) : 1, true, false);
                int quantity = (int)Math.Min(Math.Min((long)(maxHp - hp + (long)heal - 1) / heal, owned), Math.Min(total - reserve, food.MaxUsed - used));
                if (quantity <= 0) return Reject("ConsumableReserveUnavailable");
                var expected = state.Inventory.ToBuilder(); expected[food.Code] -= quantity;
                if (expected[food.Code] == 0) expected.Remove(food.Code);
                var command = port.Use(observation, item, quantity, after =>
                    after.Character.GetProperty("hp").GetInt32() == Math.Min((long)maxHp, hp + (long)heal * quantity) &&
                    CharacterObservation.Preserved(observation.Character, after.Character, "hp", "inventory") &&
                    CharacterObservation.Read(after.Character) is { } changed && changed.Inventory.Count == expected.Count &&
                    expected.All(x => changed.Inventory.GetValueOrDefault(x.Key) == x.Value));
                return candidate with { Rejection = null, ActionSeconds = food.UseSeconds, TravelSeconds = 0, RecoverySeconds = 0,
                    Command = command with { Charges = ImmutableDictionary<string, int>.Empty.Add("use:" + food.Code, quantity), RefillCode = food.Code, Refilling = false } };
            }
            return candidate.Command is null ? candidate : candidate with { Command = candidate.Command with { RefillCode = food.Code, Refilling = false } };

            StrategyCandidate Supply(int quantity, bool inventory, bool refilling)
            {
                var supplyPolicy = food.ParentItem == "recovery" && inventory ? policy with { Production = null } : policy;
                var supply = new SkillPrerequisiteStrategy(new(food.Code, quantity, candidate.Value), supplyPolicy, port, inventory).Evaluate(observation);
                if (supply.Command is null) return Reject("ConsumableSupply:" + supply.Rejection);
                var command = supply.Command;
                ImmutableDictionary<string, int>? charges = null;
                if (command.Id.StartsWith("Craft:", StringComparison.Ordinal))
                {
                    var parts = command.Id.Split(':');
                    var recipe = observation.Catalogs["items"].Single(x => x.GetProperty("code").GetString() == parts[1]).GetProperty("craft");
                    int cost = checked(recipe.GetProperty("items").EnumerateArray().Sum(x => x.GetProperty("quantity").GetInt32()) * int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                    charges = ImmutableDictionary<string, int>.Empty.Add("materials:" + food.Code, cost);
                }
                return supply with { Id = candidate.Id, Category = candidate.Category, Complete = false,
                    CombatRoute = candidate.CombatRoute, Path = candidate.Path,
                    Prerequisite = new(candidate.Id, "consumable", quantity, food.Code),
                    Command = command with { Productive = false, Charges = charges, RefillCode = food.Code, Refilling = refilling } };
            }
        }
        catch (Exception) { return Reject("InvalidConsumableStateOrCatalog"); }
    }
}
