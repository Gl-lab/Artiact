using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed class FullPathStrategy(IProgressionStrategy inner, PortfolioPolicy policy) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation) => EvaluateAll(observation).OrderByDescending(x => x.Score).ThenBy(x => x.Id, StringComparer.Ordinal).First();
    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation) => inner.EvaluateAll(observation).Select(x => Estimate(x, observation));

    private StrategyCandidate Estimate(StrategyCandidate candidate, StrategyObservation observation)
    {
        if (candidate.Command is null || candidate.Complete || candidate.Rejection is not null) return candidate;
        try
        {
            var empty = ImmutableDictionary<string, int>.Empty;
            PathEstimate path;
            if (candidate.Id.StartsWith("skill:", StringComparison.Ordinal))
            {
                var parts = candidate.Id.Split(':'); string skill = parts[1];
                int level = observation.Character.GetProperty(skill + "_level").GetInt32();
                var goal = policy.Skills.Single(x => x.Skill == skill);
                var resource = observation.Catalogs["resources"].Single(x => x.GetProperty("code").GetString() == parts[2]);
                int required = resource.GetProperty("level").GetInt32();
                decimal basis = required switch { < 10 => 5, < 20 => 10, < 30 => 13, < 35 => 16, < 40 => 20, < 45 => 28, _ => 36 };
                decimal xp = Math.Round((basis + required * 8m / level) * Wisdom(observation), 0, MidpointRounding.AwayFromZero);
                path = new(new("xp", skill, goal.Target), "Gather:" + skill, RemainingXp(observation, skill, goal.Target), xp,
                    policy.GatherSeconds, 0, candidate.TravelSeconds, 0, empty, "Official gathering formula estimate; future levels reuse current XP threshold; next observation replans.");
            }
            else if (candidate.Id.StartsWith("item:", StringComparison.Ordinal))
            {
                string code = candidate.Id[5..]; var goal = policy.Items.Single(x => x.Code == code);
                var plan = Plan(observation, goal);
                var root = plan.Steps.LastOrDefault(x => x.Code == code) ?? throw new InvalidOperationException();
                decimal remaining = goal.Quantity - ActionFacts.Stock(observation)!.GetValueOrDefault(code);
                decimal unit = root.Kind == "Craft" ? 4 : root.Kind == "Gather" ? policy.GatherSeconds : 3;
                string command = root.Kind == "Craft" ? "Craft:" + code + ":1" : root.Kind == "Gather" ? "Gather:" + root.Skill : "Withdraw:" + code + ":1";
                if (root.Kind == "Gather") command = "Gather:" + observation.Catalogs["resources"].First(x => x.GetProperty("drops").EnumerateArray().Any(d => d.GetProperty("code").GetString() == code)).GetProperty("skill").GetString();
                var costs = Cost(plan);
                decimal output = root.Kind == "Craft" ? (decimal)root.Output / root.Quantity : 1;
                path = new(new("item", code, goal.Quantity), command, remaining, output, unit,
                    Math.Max(0, costs.Seconds - Math.Ceiling(remaining / output) * unit) + Training(candidate), costs.Travel, 0,
                    costs.Materials, "Finite remaining recipe/stock plan; travel per work group, capacity bank allowance; training is uncertain; drops use conservative assumptions.");
            }
            else if (candidate.Category == "combat")
            {
                string monster = candidate.CombatRoute?.Monster ?? (candidate.Id.StartsWith("combat:", StringComparison.Ordinal) ? candidate.Id[7..] : policy.Monster);
                var enemy = observation.Catalogs["monsters"].Single(x => x.GetProperty("code").GetString() == monster);
                int level = observation.Character.GetProperty("level").GetInt32(), enemyLevel = enemy.GetProperty("level").GetInt32();
                decimal penalty = level - enemyLevel >= 10 ? 0 : level - enemyLevel >= 5 ? 0.7m : 1;
                decimal xp = Math.Round((enemyLevel * 20m / level + enemy.GetProperty("hp").GetInt32() * 0.04m) * penalty * Wisdom(observation), 0, MidpointRounding.AwayFromZero);
                int target = candidate.CombatRoute?.Target ?? policy.CombatTarget;
                var costs = candidate.CombatRoute?.Equipment is { } gear ? Cost(Plan(observation, new(gear, 1))) : (Seconds: 0m, Travel: 0m, Materials: empty);
                decimal preparation = candidate.CombatRoute?.Equipment is null ? 0 : Math.Max(candidate.CombatRoute.PreparationSeconds,
                    costs.Seconds + costs.Travel + 2 * policy.EquipmentSeconds + Training(candidate));
                path = new(new("xp", "combat", target), "Fight", RemainingXp(observation, "combat", target), xp,
                    policy.FightSeconds, preparation, candidate.TravelSeconds, candidate.RecoverySeconds, costs.Materials,
                    "Official normal-monster XP estimate; future XP thresholds and fight/rest times assumed; single gear replacement and bounded training allowance.");
            }
            else return candidate with { Rejection = "UnsupportedPathMetric", Command = null };
            decimal inventoryUnits = path.Goal.Kind == "xp" ? Math.Ceiling(path.Remaining / Math.Max(1, path.ExpectedProgress)) : path.Remaining;
            if (policy.Bank is not null && CharacterObservation.Read(observation.Character) is { } state && inventoryUnits > state.FreeUnits)
                path = path with { PreparationSeconds = path.PreparationSeconds + Math.Ceiling(inventoryUnits / Math.Max(1, state.Capacity)) * (policy.MoveSeconds * 2 + 3),
                    Assumptions = path.Assumptions + " Bank trips estimated from capacity; exact deposits remain runtime prerequisites." };
            path = Food(path, candidate, observation);
            return candidate with { Path = path };
        }
        catch (Exception) { return candidate with { Rejection = "UnsupportedFullPathEstimate", Command = null }; }
    }

    private static decimal Wisdom(StrategyObservation state) => state.Character.TryGetProperty("wisdom", out var value) ? 1 + value.GetInt32() * 0.001m : 1;
    private static decimal RemainingXp(StrategyObservation state, string skill, int target)
    {
        string prefix = skill == "combat" ? "" : skill + "_";
        return checked((decimal)(target - state.Character.GetProperty(prefix + "level").GetInt32()) * state.Character.GetProperty(prefix + "max_xp").GetInt32() - state.Character.GetProperty(prefix + "xp").GetInt32());
    }
    private decimal Training(StrategyCandidate candidate) => candidate.Prerequisite is { Skill: not "consumable" }
        ? 10 * (policy.GatherSeconds + policy.MoveSeconds + 4) : 0;
    private ItemProductionPlan Plan(StrategyObservation observation, ItemMilestone goal)
    {
        var stock = ProductionStock.Available(observation, goal, policy);
        var plan = ItemProductionPlan.Build(goal.Code, goal.Quantity, stock.Inventory, stock.Bank,
            observation.Catalogs["items"], observation.Catalogs["resources"], null, capacityAware: true);
        if (plan.Rejection is not null) throw new InvalidOperationException(plan.Rejection);
        var state = CharacterObservation.Read(observation.Character)!;
        foreach (var step in plan.Steps)
        {
            bool reachable = observation.Catalogs["maps"].Any(map => StrategyRules.GatheringPlace(map, state) &&
                map.GetProperty("interactions").GetProperty("content") is { ValueKind: JsonValueKind.Object } content && (step.Kind switch
                {
                    "Craft" => content.GetProperty("type").GetString() == "workshop" && content.GetProperty("code").GetString() == step.Skill,
                    "Withdraw" => content.GetProperty("type").GetString() == "bank",
                    "Gather" => content.GetProperty("type").GetString() == "resource" && observation.Catalogs["resources"].Any(resource =>
                        resource.GetProperty("code").GetString() == content.GetProperty("code").GetString() && StrategyRules.Empty(resource, "conditions") &&
                        resource.GetProperty("drops").EnumerateArray().Any(drop => drop.GetProperty("code").GetString() == step.Code)),
                    _ => false
                }));
            if (!reachable) throw new InvalidOperationException("UnreachablePathStep");
        }
        return plan;
    }
    private (decimal Seconds, decimal Travel, ImmutableDictionary<string, int> Materials) Cost(ItemProductionPlan plan)
    {
        var materials = ImmutableDictionary.CreateBuilder<string, int>(StringComparer.Ordinal);
        decimal seconds = 0;
        foreach (var step in plan.Steps)
        {
            seconds += step.Kind switch { "Craft" => step.Quantity * 4m, "Gather" => step.Quantity * policy.GatherSeconds, "Withdraw" => 3m, _ => throw new InvalidOperationException("UnboundedLootEstimate") };
            foreach (var input in step.Ingredients ?? ImmutableDictionary<string, int>.Empty) materials[input.Key] = checked(materials.GetValueOrDefault(input.Key) + input.Value);
        }
        return (seconds, plan.Steps.Length * policy.MoveSeconds, materials.ToImmutable());
    }
    private PathEstimate Food(PathEstimate path, StrategyCandidate candidate, StrategyObservation observation)
    {
        if (policy.Consumable is not { } food || !(food.ParentItem == "combat" && candidate.Category == "combat" || candidate.Id == "item:" + food.ParentItem)) return path;
        int hp = observation.Character.GetProperty("hp").GetInt32(), maxHp = observation.Character.GetProperty("max_hp").GetInt32();
        long damage = candidate.CombatRoute?.MaximumLoss ?? 0;
        var item = observation.Catalogs["items"].Single(x => x.GetProperty("code").GetString() == food.Code);
        int heal = item.GetProperty("effects")[0].GetProperty("value").GetInt32();
        int cycles = (int)Math.Min(10000, Math.Ceiling(path.Remaining / Math.Max(1, path.ExpectedProgress)));
        int uses = (long)hp * 100 < (long)maxHp * food.HpBelowPercent ? (int)Math.Ceiling((maxHp - hp) / (decimal)heal) : 0;
        if (damage > 0 && (maxHp - damage) * 100 < (long)maxHp * food.HpBelowPercent)
            uses = checked(uses + Math.Max(0, cycles - 1) * (int)Math.Ceiling(damage / (decimal)heal));
        int target = uses > 0 || observation.Context.Used.GetValueOrDefault("use:" + food.Code) > 0 ? checked(food.TargetStock + uses) : 0;
        if (target == 0) return path;
        (decimal Seconds, decimal Travel, ImmutableDictionary<string, int> Materials) costs;
        try { costs = Cost(Plan(observation, new(food.Code, target))); }
        catch (InvalidOperationException) when (food.AllowRest)
        {
            return path with { RecoverySeconds = Math.Max(path.RecoverySeconds, Math.Max(1, cycles) * policy.RestSeconds),
                Assumptions = path.Assumptions + " Food plan unavailable; optional rest estimate retained." };
        }
        decimal supply = costs.Seconds + costs.Travel + uses * food.UseSeconds;
        bool available = (long)uses + observation.Context.Used.GetValueOrDefault("use:" + food.Code) <= food.MaxUsed &&
            costs.Materials.Values.Sum(x => (long)x) + observation.Context.Used.GetValueOrDefault("materials:" + food.Code) <= food.MaxMaterialUnits;
        decimal rest = Math.Max(0, cycles - 1) * policy.RestSeconds + (hp < maxHp ? policy.RestSeconds : 0);
        if (food.AllowRest && (!available || rest <= supply)) return path with { RecoverySeconds = Math.Max(path.RecoverySeconds, rest), Assumptions = path.Assumptions + " Rest preferred to estimated food supply/allowance." };
        if (!available) throw new InvalidOperationException("FoodAllowanceInsufficientForEstimate");
        var material = path.Materials.ToBuilder();
        foreach (var input in costs.Materials) material[input.Key] = checked(material.GetValueOrDefault(input.Key) + input.Value);
        return path with { PreparationSeconds = path.PreparationSeconds + supply, Materials = material.ToImmutable(),
            Assumptions = path.Assumptions + " Food estimate includes production/delivery, " + uses + " uses and target stock; unobserved training remains uncertain." };
    }
}
