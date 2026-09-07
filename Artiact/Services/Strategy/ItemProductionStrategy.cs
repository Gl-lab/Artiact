using System.Collections.Immutable;
using Artiact.Services.Combat;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed class ItemProductionStrategy(ItemMilestone goal, PortfolioPolicy policy, StrategyActionPort port, bool requireInventory = false) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation)
    {
        decimal prerequisites = 0;
        StrategyCandidate Result(string? rejection, bool complete = false, AtomicCommand? command = null) =>
            new("item:" + goal.Code, "item", goal.Value, 4, 0, prerequisites, rejection, complete, command);
        try
        {
            var state = CharacterObservation.Read(observation.Character);
            if (state is null) return Result("InvalidItemObservation");
            if ((long)state.Inventory.GetValueOrDefault(goal.Code) + (requireInventory ? 0 : observation.Bank?.Items.GetValueOrDefault(goal.Code) ?? 0) >= goal.Quantity)
                return Result(null, true);
            var plan = ProductionStock.Plan(observation, goal, policy, requireInventory);
            if (plan.Rejection is not null) return Result(plan.Rejection);
            StrategyCandidate Pressure()
            {
                var blocked = Result("InventoryPressure");
                return policy.Production is null ? blocked : BankPrerequisite.Evaluate(observation, blocked,
                    ProductionStock.Retain(observation, policy, plan), port, policy.MoveSeconds);
            }
            if (policy.Production is not null && plan.Steps.Where(x => x.Kind == "Craft").Any(x =>
                    Math.Max(x.Ingredients!.Values.Sum(v => (long)v), x.Output) + policy.Production.Reserved.Sum(r =>
                        (long)Math.Min(r.Value, state.Inventory.GetValueOrDefault(r.Key))) > state.Capacity))
                return Result("MinimalRecipeExceedsCapacity");
            if (policy.Measurement is not null)
                prerequisites = Math.Max(0, plan.Steps.Sum(x => x.Kind switch
                { "Gather" => x.Quantity * policy.GatherSeconds, "Loot" => x.Quantity * (policy.FightSeconds + policy.RestSeconds),
                    "Withdraw" => 3m, _ => x.Quantity * 4m }) + plan.Steps.Length * policy.MoveSeconds - 4);
            var next = plan.Steps[0];
            if (next.Kind == "Loot")
            {
                var combat = new CombatMilestoneStrategy(policy with { PrepareEquipment = false,
                    CombatTarget = checked(observation.Character.GetProperty("level").GetInt32() + 1) }, port).Evaluate(observation);
                return Result(combat.Rejection, command: combat.Command);
            }
            if (next.Kind == "Gather")
            {
                var resources = observation.Catalogs["resources"].Where(x => x.GetProperty("drops").EnumerateArray()
                    .Any(d => d.GetProperty("code").GetString() == next.Code)).ToImmutableArray();
                var resource = resources.OrderBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).First();
                string skill = resource.GetProperty("skill").GetString()!;
                var filtered = new StrategyObservation(observation.Character, observation.Catalogs.SetItem("resources", resources), observation.Policy, observation.Bank);
                var gatherPolicy = policy.Production is null ? policy : policy with { Bank = ProductionStock.Retain(observation, policy, plan) };
                var candidate = new GatheringStrategy(new(skill, checked(observation.Character.GetProperty(skill + "_level").GetInt32() + 1), goal.Value), gatherPolicy, port).Evaluate(filtered);
                // Re-evaluate dispatch against the complete world fingerprint; filtered catalogs are planning-only.
                if (candidate.Command is null) return Result(candidate.Rejection);
                var selected = candidate.Command;
                return Result(null, command: new(selected.Id, observation.Fingerprint, selected.Productive,
                    after => selected.Postcondition(new(after.Character, filtered.Catalogs, after.Policy, after.Bank)), async token =>
                    {
                        var reply = await selected.Dispatch(token);
                        return reply with { State = new(reply.State.Character, observation.Catalogs, observation.Policy, reply.State.Bank) };
                    }));
            }
            var maps = observation.Catalogs["maps"];
            if (!StrategyRules.GatheringPlace(maps.Single(x => x.GetProperty("map_id").GetInt32() == state.MapId), state)) return Result("UnsupportedAccess");
            string type = next.Kind == "Withdraw" ? "bank" : "workshop";
            var map = maps.Where(x => StrategyRules.GatheringPlace(x, state) &&
                x.GetProperty("interactions").TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Object &&
                content.GetProperty("type").GetString() == type && (type == "bank" || content.GetProperty("code").GetString() == next.Skill))
                .OrderBy(x => x.GetProperty("map_id").GetInt32()).FirstOrDefault();
            if (map.ValueKind == JsonValueKind.Undefined) return Result("NoSupported" + type);
            if (next.Kind == "Craft")
            {
                var recipe = observation.Catalogs["items"].Single(x => x.GetProperty("code").GetString() == next.Code).GetProperty("craft");
                if (observation.Character.GetProperty(next.Skill + "_level").GetInt32() < recipe.GetProperty("level").GetInt32()) return Result("CraftSkillTooLow");
            }
            int id = map.GetProperty("map_id").GetInt32();
            if (id != state.MapId) return Result(null, command: port.Combat(observation, CombatCommand.Move,
                new(id, state.Layer, "", new(1, 0), true), null, false, after => CharacterObservation.Read(after.Character)?.MapId == id &&
                    CharacterObservation.Preserved(observation.Character, after.Character, "map_id", "x", "y")));
            var stock = state.Inventory.ToBuilder();
            if (next.Kind == "Withdraw")
            {
                if (state.FreeUnits < next.Quantity) return Pressure();
                stock[next.Code] = checked(stock.GetValueOrDefault(next.Code) + next.Quantity);
                var bank = observation.Bank!.Items.ToBuilder(); bank[next.Code] -= next.Quantity;
                if (bank[next.Code] == 0) bank.Remove(next.Code);
                return Result(null, command: port.Deposit(observation, [new() { Code = next.Code, Quantity = next.Quantity }], after =>
                    CharacterObservation.Preserved(observation.Character, after.Character, "inventory") && Matches(stock, after) &&
                    BankPrerequisite.SameBank(new(observation.Bank.Slots, bank.ToImmutable()), after.Bank), withdraw: true));
            }
            foreach (var ingredient in next.Ingredients!)
            {
                if (stock.GetValueOrDefault(ingredient.Key) < ingredient.Value) return Result("MissingCraftStock");
                stock[ingredient.Key] -= ingredient.Value;
                if (stock[ingredient.Key] == 0) stock.Remove(ingredient.Key);
            }
            stock[next.Code] = checked(stock.GetValueOrDefault(next.Code) + next.Output);
            if (stock.Values.Sum(x => (long)x) > state.Capacity) return Pressure();
            return Result(null, command: port.Craft(observation, next, after => Matches(stock, after) &&
                CharacterObservation.Preserved(observation.Character, after.Character, "inventory", next.Skill + "_xp", next.Skill + "_level", next.Skill + "_max_xp")));
        }
        catch (Exception) { return Result("InvalidItemStateOrRecipe"); }
    }
    private static bool Matches(ImmutableDictionary<string, int>.Builder expected, StrategyObservation after) =>
        CharacterObservation.Read(after.Character) is { } state && expected.Count == state.Inventory.Count &&
        expected.All(x => state.Inventory.GetValueOrDefault(x.Key) == x.Value);
}
