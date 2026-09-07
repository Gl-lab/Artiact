using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed class SkillPrerequisiteStrategy(ItemMilestone goal, PortfolioPolicy policy, StrategyActionPort port,
    bool requireInventory = false) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation)
    {
        var baseline = new ItemProductionStrategy(goal, policy, port, requireInventory).Evaluate(observation);
        if (policy.Preparation is null || baseline.Complete) return baseline;
        try { return Prepare(goal, observation, new(StringComparer.Ordinal), 0) with { Id = baseline.Id, Value = baseline.Value }; }
        catch (Exception) { return baseline with { Rejection = "InvalidPrerequisiteState", Command = null }; }
    }

    private StrategyCandidate Prepare(ItemMilestone target, StrategyObservation observation, HashSet<string> path, int depth)
    {
        var candidate = new ItemProductionStrategy(target, policy, port, requireInventory).Evaluate(observation);
        if (candidate.Complete) return candidate;
        if (depth >= policy.Preparation!.MaxDepth || !path.Add("item:" + target.Code))
            return candidate with { Rejection = "PrerequisiteCycleOrDepth", Command = null };
        try
        {
            var state = CharacterObservation.Read(observation.Character)!;
            var items = observation.Catalogs["items"];
            var resources = observation.Catalogs["resources"];
            var plan = ItemProductionPlan.Build(target.Code, target.Quantity, state.Inventory,
                observation.Bank?.Items ?? ImmutableDictionary<string, int>.Empty, items, resources,
                policy.PrepareEquipment ? observation.Catalogs["monsters"].Where(x => x.GetProperty("code").GetString() == policy.Monster).ToArray() : null);
            if (plan.Rejection is not null) return candidate;
            foreach (var step in plan.Steps.Where(x => x.Kind == "Craft"))
            {
                var recipe = items.Single(x => x.GetProperty("code").GetString() == step.Code).GetProperty("craft");
                int required = recipe.GetProperty("level").GetInt32();
                int current = observation.Character.GetProperty(step.Skill + "_level").GetInt32();
                if (current >= required) continue;
                if (step.Skill != "weaponcrafting") return candidate with { Rejection = "UnsupportedTrainingSkill:" + step.Skill, Command = null };
                string key = "skill:" + step.Skill;
                if (!path.Add(key)) return candidate with { Rejection = "PrerequisiteCycleOrDepth", Command = null };
                try
                {
                    var eligible = items.Where(x => x.TryGetProperty("craft", out var craft) && craft.ValueKind == JsonValueKind.Object &&
                        craft.GetProperty("skill").GetString() == step.Skill && craft.GetProperty("level").GetInt32() is > 0 &&
                        craft.GetProperty("level").GetInt32() <= current && x.TryGetProperty("level", out var level) &&
                        level.GetInt32() > 0 && (long)current - level.GetInt32() < 10 && StrategyRules.Empty(x, "conditions"))
                        .OrderByDescending(x => x.GetProperty("craft").GetProperty("level").GetInt32())
                        .ThenBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal);
                    string? failure = null;
                    foreach (var training in eligible)
                    {
                        var craft = training.GetProperty("craft");
                        var ingredients = craft.GetProperty("items").EnumerateArray().ToArray();
                        if (ingredients.Length == 0 || ingredients.Any(x => x.GetProperty("quantity").GetInt32() <= 0) ||
                            ingredients.Sum(x => (long)x.GetProperty("quantity").GetInt32()) > policy.Preparation.MaxIngredientUnits) continue;
                        string code = training.GetProperty("code").GetString()!;
                        int output = craft.GetProperty("quantity").GetInt32();
                        if (output <= 0) continue;
                        if (path.Contains("item:" + code)) { failure = "PrerequisiteCycleOrDepth"; continue; }
                        int owned = checked(state.Inventory.GetValueOrDefault(code) + (observation.Bank?.Items.GetValueOrDefault(code) ?? 0));
                        var subgoal = new ItemMilestone(code, checked(owned + output), target.Value);
                        var prepared = Prepare(subgoal, observation, path, depth + 1);
                        if (prepared.Command is null)
                        {
                            if (prepared.Rejection is "RecipeCycle" or "PrerequisiteCycleOrDepth") failure = prepared.Rejection;
                            continue;
                        }
                        var command = prepared.Command;
                        if (command.Id == "Craft:" + code + ":1")
                            command = command with { Postcondition = after => prepared.Command.Postcondition(after) &&
                                ValidProgress(observation.Character, after.Character, step.Skill) };
                        return prepared with { Id = candidate.Id, Command = command,
                            Prerequisite = new("item:" + goal.Code, step.Skill, required, code) };
                    }
                    return candidate with { Rejection = failure ?? "NoSupportedTrainingRecipe:" + step.Skill, Command = null };
                }
                finally { path.Remove(key); }
            }
            if (plan.Steps.FirstOrDefault() is { Kind: "Gather" } gather)
            {
                var sources = resources.Where(x => StrategyRules.Empty(x, "conditions") &&
                    x.GetProperty("drops").EnumerateArray().Any(d => d.GetProperty("code").GetString() == gather.Code) &&
                    observation.Catalogs["maps"].Any(map => StrategyRules.GatheringPlace(map, state) &&
                        map.GetProperty("interactions").GetProperty("content") is { ValueKind: JsonValueKind.Object } content &&
                        content.GetProperty("type").GetString() == "resource" && content.GetProperty("code").GetString() == x.GetProperty("code").GetString()))
                    .OrderBy(x => x.GetProperty("level").GetInt32()).ThenBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).ToArray();
                if (sources.Length == 0) return candidate with { Rejection = "NoSupportedResourcePrerequisite", Command = null };
                if (!sources.Any(x => x.GetProperty("level").GetInt32() <= observation.Character.GetProperty(x.GetProperty("skill").GetString() + "_level").GetInt32()))
                {
                    foreach (var source in sources)
                    {
                        string skill = source.GetProperty("skill").GetString()!;
                        if (skill != "mining" || !StrategyRules.Empty(source, "conditions")) continue;
                        int required = source.GetProperty("level").GetInt32();
                        var training = new GatheringStrategy(new(skill, required, target.Value), policy, port).Evaluate(observation);
                        if (training.Command is not null) return training with { Id = candidate.Id, Category = candidate.Category,
                            Prerequisite = new("item:" + goal.Code, skill, required, null) };
                    }
                    return candidate with { Rejection = "NoSupportedGatheringTraining", Command = null };
                }
            }
            return candidate;
        }
        finally { path.Remove("item:" + target.Code); }
    }

    private static bool ValidProgress(JsonElement before, JsonElement after, string skill) =>
        StrategyRules.Progress(before, after, skill + "_") && after.GetProperty(skill + "_xp").GetInt32() >= 0 &&
        after.GetProperty(skill + "_max_xp").GetInt32() > after.GetProperty(skill + "_xp").GetInt32();
}
