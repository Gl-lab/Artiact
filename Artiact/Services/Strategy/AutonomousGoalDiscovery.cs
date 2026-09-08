using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record GoalDiscoveryEvidence(string Version, string Skill, int Target, string? Unlock,
    decimal UnlockUtility, decimal ProgressUtility, decimal RecipeUtility, ImmutableArray<string> DependentRecipes,
    string Assumptions, string ReevaluateWhen,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? OriginalTarget = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? FallbackReason = null);

public sealed class AutonomousGoalDiscovery(PortfolioPolicy policy, StrategyActionPort port, StrategyLimits limits) : IProgressionStrategy
{
    public const string Version = "discovery-v2";
    public const int SkillCap = 50;
    private static readonly string[] Skills = ["alchemy", "fishing", "mining", "woodcutting"];

    public StrategyCandidate Evaluate(StrategyObservation observation) => EvaluateAll(observation)
        .OrderByDescending(x => x.Score).ThenBy(x => x.Id, StringComparer.Ordinal).First();

    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation)
    {
        try { return Discover(observation); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { return [Rejected("discovery", "InvalidDiscoveryCatalogOrState")]; }
    }

    private ImmutableArray<StrategyCandidate> Discover(StrategyObservation observation)
    {
        var state = CharacterObservation.Read(observation.Character);
        if (state is null) return [Rejected("discovery", "UnsupportedObservation")];
        var resources = observation.Catalogs["resources"];
        var items = observation.Catalogs["items"];
        var maps = observation.Catalogs["maps"];
        if (resources.Length > 4096 || items.Length > 16384 || DuplicateCodes(resources) || DuplicateCodes(items))
            return [Rejected("discovery", "InvalidDiscoveryCatalog")];
        if (maps.IsEmpty || maps.Select(x => x.GetProperty("map_id").GetInt32()).Distinct().Count() != maps.Length ||
            !StrategyRules.GatheringPlace(maps.Single(x => x.GetProperty("map_id").GetInt32() == state.MapId), state))
            return [Rejected("discovery", "UnsupportedAccess")];
        bool Reachable(JsonElement resource) => SupportedDrops(resource) && StrategyRules.Empty(resource, "conditions") && maps.Any(map =>
            StrategyRules.GatheringPlace(map, state) && map.GetProperty("interactions").GetProperty("content") is { ValueKind: JsonValueKind.Object } content &&
            content.GetProperty("type").GetString() == "resource" && content.GetProperty("code").GetString() == resource.GetProperty("code").GetString());
        var result = ImmutableArray.CreateBuilder<StrategyCandidate>();
        foreach (string skill in Skills)
        {
            var active = observation.Context.Autonomous?.Active;
            if (active is not null && active.Skill != skill) continue;
            if (!observation.Character.TryGetProperty(skill + "_level", out var rawLevel) || !rawLevel.TryGetInt32(out int level) || level is < 1 or > SkillCap)
            { result.Add(Rejected("skill:" + skill, "UnsupportedSkillObservation")); continue; }
            if (level == SkillCap)
            {
                bool valid = observation.Character.TryGetProperty(skill + "_xp", out var xp) && xp.TryGetInt32(out int value) && value >= 0 &&
                    observation.Character.TryGetProperty(skill + "_max_xp", out var max) && max.TryGetInt32(out int maximum) && maximum >= value;
                result.Add(Rejected("skill:" + skill, valid ? "SupportedSkillCapReached" : "UnsupportedSkillObservation")); continue;
            }
            var eligible = resources.Where(x => x.GetProperty("skill").GetString() == skill)
                .OrderBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).Take(32).ToArray();
            var trainers = eligible.Where(x => x.GetProperty("level").GetInt32() is > 0 && x.GetProperty("level").GetInt32() <= level &&
                level - x.GetProperty("level").GetInt32() < 10 && Reachable(x)).ToArray();
            var unlock = eligible.Where(x => x.GetProperty("level").GetInt32() > level && x.GetProperty("level").GetInt32() <= SkillCap && Reachable(x) &&
                trainers.Any(t => x.GetProperty("level").GetInt32() - 1 - t.GetProperty("level").GetInt32() < 10) &&
                trainers.Length > 0 && (Base(x.GetProperty("level").GetInt32()) > trainers.Max(t => Base(t.GetProperty("level").GetInt32())) ||
                    x.GetProperty("level").GetInt32() - trainers.Max(t => t.GetProperty("level").GetInt32()) >= 10))
                .OrderBy(x => x.GetProperty("level").GetInt32()).ThenBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).FirstOrDefault();
            bool hasUnlock = unlock.ValueKind != JsonValueKind.Undefined;
            int target = hasUnlock ? unlock.GetProperty("level").GetInt32() : level + 1;
            var evidence = new GoalDiscoveryEvidence(Version, skill, target, hasUnlock ? unlock.GetProperty("code").GetString() : null,
                hasUnlock ? 1 : 0, hasUnlock ? 0 : 0.1m, 0, hasUnlock ? Recipes(items, unlock) : [],
                "Skill cap 50; recipe references have no utility without a proven need; future XP thresholds and yields are estimates.",
                "Milestone reached, catalog/access change, or route becomes infeasible");
            if (active is not null) { evidence = active; target = active.Target; }
            if (observation.Context.Autonomous?.History.Any(x => x.Outcome == "Rejected" && x.Goal.Skill == skill && x.Goal.Target == target) == true)
            { result.Add(Rejected("skill:" + skill, "PreviouslyRejectedMilestone")); continue; }
            var initial = EvaluateTarget(evidence);
            result.AddRange(initial);
            var retryRoutes = initial.Where(x => x.Rejection is "EstimatedInventoryInsufficient" or "EstimatedPathExceedsBudget").ToArray();
            if (active is null && target > level + 1 && retryRoutes.Length > 0 &&
                !initial.Any(x => x.Score.HasValue && x.Command is not null) &&
                observation.Context.Autonomous?.History.Any(x => x.Outcome == "Rejected" && x.Goal.Skill == skill && x.Goal.Target == level + 1) != true)
            {
                var intermediate = evidence with { Target = level + 1, Unlock = null, UnlockUtility = 0, ProgressUtility = 0.1m,
                    DependentRecipes = [], OriginalTarget = target, FallbackReason = retryRoutes[0].Rejection };
                var ids = retryRoutes.Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
                result.AddRange(EvaluateTarget(intermediate).Where(x => ids.Contains(x.Id.Replace(":intermediate", "", StringComparison.Ordinal))));
            }

            ImmutableArray<StrategyCandidate> EvaluateTarget(GoalDiscoveryEvidence goalEvidence)
            {
                int target = goalEvidence.Target;
                var evaluated = ImmutableArray.CreateBuilder<StrategyCandidate>();
                var goal = new SkillMilestone(skill, target, goalEvidence.UnlockUtility + goalEvidence.ProgressUtility);
                var derived = policy with { Skills = [goal], AutonomousGoals = false };
                var strategy = new FullPathStrategy(new ResourceAlternatives(goal, derived, port), derived with { Bank = null });
                foreach (var original in strategy.EvaluateAll(observation))
                {
                    var candidate = original with { Discovery = goalEvidence };
                    if (candidate.Path is { } path && candidate.Rejection is null)
                    {
                        decimal work = Math.Ceiling(path.Remaining / Math.Max(0.001m, path.ExpectedProgress));
                        decimal seconds = work * path.UnitSeconds + path.PreparationSeconds + path.TravelSeconds + path.RecoverySeconds;
                        decimal multiplier = policy.Measurement?.UnknownMultiplier ?? 2;
                        string resourceCode = candidate.Id.Split(':')[2];
                        int trainingLevel = resources.Single(x => x.GetProperty("code").GetString() == resourceCode).GetProperty("level").GetInt32();
                        long dropUnits = resources.Single(x => x.GetProperty("code").GetString() == resourceCode).GetProperty("drops").EnumerateArray()
                            .Sum(x => (long)x.GetProperty("max_quantity").GetInt32());
                        var bank = policy.Bank is null ? null : GatheringBankEstimate.Calculate(observation,
                            resources.Single(x => x.GetProperty("code").GetString() == resourceCode), work, policy);
                        decimal actions = bank?.Actions ?? work + (path.TravelSeconds > 0 ? 1 : 0);
                        if (bank is not null)
                        {
                            seconds = bank.Seconds;
                            candidate = candidate with { Path = path with { PreparationSeconds = Math.Max(0, bank.Seconds - work * path.UnitSeconds),
                                TravelSeconds = 0, Assumptions = path.Assumptions + " Maximum-drop stock simulation includes permitted bank transfers and travel." } };
                        }
                        candidate = candidate with { Feasibility = new(state.FreeUnits, work * dropUnits,
                            Math.Max(0, work * dropUnits - state.FreeUnits), policy.Bank is not null,
                            actions, observation.Context.RemainingActions ?? limits.Actions,
                            seconds * multiplier, observation.Context.RemainingSeconds ?? limits.DurationSeconds, bank?.Unloads ?? 0) };
                        string? rejection = target - 1 - trainingLevel >= 10 ? "TrainingCannotReachMilestone" :
                            bank?.Rejection is { } bankRejection ? bankRejection :
                            policy.Bank is null && work * dropUnits > state.FreeUnits ? "EstimatedInventoryInsufficient" :
                            actions > (observation.Context.RemainingActions ?? limits.Actions) ||
                            seconds * multiplier > (observation.Context.RemainingSeconds ?? limits.DurationSeconds) ? "EstimatedPathExceedsBudget" : null;
                        if (rejection is not null) candidate = candidate with { Rejection = rejection, Command = null };
                    }
                    if (goalEvidence.OriginalTarget is not null) candidate = candidate with { Id = candidate.Id + ":intermediate" };
                    evaluated.Add(candidate);
                }
                return evaluated.ToImmutable();
            }
        }
        return result.ToImmutable();
    }

    private static bool DuplicateCodes(ImmutableArray<JsonElement> entries) => entries.Select(x => x.GetProperty("code").GetString()).Distinct(StringComparer.Ordinal).Count() != entries.Length;
    private static bool SupportedDrops(JsonElement resource)
    {
        var drops = resource.GetProperty("drops").EnumerateArray().ToArray();
        return drops.Length is > 0 and <= 100 && drops.Select(x => x.GetProperty("code").GetString()).Distinct(StringComparer.Ordinal).Count() == drops.Length &&
            drops.Any(x => x.GetProperty("rate").GetInt32() == 1) && drops.All(x => !string.IsNullOrWhiteSpace(x.GetProperty("code").GetString()) &&
                x.GetProperty("min_quantity").GetInt32() > 0 && x.GetProperty("max_quantity").GetInt32() >= x.GetProperty("min_quantity").GetInt32() &&
                x.GetProperty("rate").GetInt32() >= 1);
    }
    private static decimal Base(int level) => level switch { < 10 => 5, < 20 => 10, < 30 => 13, < 35 => 16, < 40 => 20, < 45 => 28, _ => 36 };
    private static ImmutableArray<string> Recipes(ImmutableArray<JsonElement> items, JsonElement resource)
    {
        var drops = resource.GetProperty("drops").EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToHashSet(StringComparer.Ordinal);
        return items.Where(x => x.TryGetProperty("craft", out var craft) && craft.ValueKind == JsonValueKind.Object &&
                craft.GetProperty("items").EnumerateArray().Any(i => drops.Contains(i.GetProperty("code").GetString())))
            .Select(x => x.GetProperty("code").GetString()!).Order(StringComparer.Ordinal).Take(32).ToImmutableArray();
    }
    private static StrategyCandidate Rejected(string id, string reason) => new(id, "skill", 0.1m, 1, 0, 0, reason, false, null);
}
