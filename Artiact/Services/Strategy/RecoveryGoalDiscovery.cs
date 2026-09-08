using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class RecoveryGoalDiscovery(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    private sealed class Parent(StrategyCandidate candidate) : IProgressionStrategy
    {
        public StrategyCandidate Evaluate(StrategyObservation observation) => candidate;
    }
    public StrategyCandidate Evaluate(StrategyObservation observation) => EvaluateAll(observation).FirstOrDefault() ?? Rejected("recovery", "NoRecoveryNeed");
    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation)
    {
        try { return Discover(observation); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { return [Rejected("recovery", "InvalidRecoveryStateOrCatalog")]; }
    }
    private ImmutableArray<StrategyCandidate> Discover(StrategyObservation observation)
    {
        var recovery = policy.Recovery!;
        var active = observation.Context.Autonomous?.Active;
        if (active is not null && active.Skill != "recovery") return [];
        int hp = observation.Character.GetProperty("hp").GetInt32(), maximum = observation.Character.GetProperty("max_hp").GetInt32();
        var state = CharacterObservation.Read(observation.Character);
        if (state is null || hp <= 0 || maximum is <= 0 or > 1_000_000 || hp > maximum) return [Rejected("recovery", "InvalidHealingState")];
        if (hp == maximum || active is null && (long)hp * 100 >= (long)maximum * recovery.HpBelowPercent) return [];
        if (active is not null && active.Target != maximum) return [Rejected("recovery", "RecoveryTargetChanged")];
        if (observation.Context.Autonomous?.History.Any(x => x.Outcome == "Rejected" && x.Goal.Skill == "recovery" && x.Goal.Target == maximum) == true)
            return [Rejected("recovery", "PreviouslyRejectedRecovery")];
        decimal value = active?.UnlockUtility ?? 4m * (maximum - hp) / maximum;
        var result = ImmutableArray.CreateBuilder<StrategyCandidate>();
        GoalDiscoveryEvidence Evidence(string code) => active is not null ? active with { Unlock = code } : new("recovery-v1", "recovery", maximum, code, value, 0, 0, [],
            "Observed HP deficit; supply and training are one parent cost, not independently rewarded stock.", "Full HP, changed catalog/need or infeasible route");
        if (recovery.AllowRest)
        {
            decimal seconds = Math.Max(3, Math.Ceiling(100m * (maximum - hp) / maximum));
            var rest = new StrategyCandidate("recovery:0:rest", "recovery", value, seconds, 0, 0, null, false,
                port.Combat(observation, CombatCommand.Rest, new(state.MapId, state.Layer, "", new(1, 0), true), null, true,
                    after => after.Character.GetProperty("hp").GetInt32() == maximum && CharacterObservation.Preserved(observation.Character, after.Character, "hp")),
                Path: new(new("hp", "recovery", maximum), "Rest", maximum - hp, maximum - hp, seconds, 0, 0, 0,
                    ImmutableDictionary<string, int>.Empty, "Official rest percentage estimate; returned cooldown remains authoritative."), Discovery: Evidence("rest"));
            result.Add(Budget(rest, observation));
        }
        if (!recovery.AllowUse)
        {
            if (result.Count == 0) result.Add(Rejected("recovery", "RecoveryUseNotAllowed"));
            return result.ToImmutable();
        }
        var items = observation.Catalogs["items"];
        if (items.Length > 16384 || items.Select(x => x.GetProperty("code").GetString()).Distinct().Count() != items.Length)
            return [Rejected("recovery", "InvalidRecoveryCatalog")];
        var candidates = items.Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "consumable")
            .OrderBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).Take(32);
        foreach (var item in candidates)
        {
            string code = item.GetProperty("code").GetString()!;
            string id = "recovery:1:" + code;
            if (!StrategyRules.Empty(item, "conditions") || !item.TryGetProperty("effects", out var effects) || effects.ValueKind != JsonValueKind.Array ||
                effects.GetArrayLength() != 1 || effects[0].GetProperty("code").GetString() != "heal" || effects[0].GetProperty("value").GetInt32() <= 0)
            { result.Add(Rejected(id, "UnsupportedRecoveryEffect")); continue; }
            try { result.Add(Food(item, Evidence(code))); }
            catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or OverflowException or ArgumentException or FormatException)
            { result.Add(Rejected(id, "UnsupportedRecoveryProductionPath")); }

            StrategyCandidate Food(JsonElement foodItem, GoalDiscoveryEvidence evidence)
            {
                int heal = foodItem.GetProperty("effects")[0].GetProperty("value").GetInt32();
                int needed = (int)Math.Ceiling((maximum - hp) / (decimal)heal);
                var floors = recovery.Reserved ?? ImmutableDictionary<string, int>.Empty;
                int reserve = floors.GetValueOrDefault(code), target = checked(needed + reserve);
                int used = observation.Context.Used.GetValueOrDefault("recovery:use"), materials = observation.Context.Used.GetValueOrDefault("recovery:materials");
                if (needed > recovery.MaxUsed - used || target > 10000) return Rejected(id, "RecoveryUseBudgetExhausted");
                var context = observation.Context with { Used = observation.Context.Used.SetItem("use:" + code, used).SetItem("materials:" + code, materials) };
                var planning = new StrategyObservation(observation.Character, observation.Catalogs, observation.Policy,
                    recovery.AllowBankWithdrawal ? observation.Bank : null, context);
                int owned = state.Inventory.GetValueOrDefault(code), total = checked(owned + (planning.Bank?.Items.GetValueOrDefault(code) ?? 0));
                if (total < target && !recovery.AllowCraft) return Rejected(id, "RecoveryProductionNotAllowed");
                var derived = policy with { AutonomousGoals = false, Recovery = null, Skills = [], Items = [],
                    Bank = recovery.AllowBankWithdrawal ? policy.Bank : null, Preparation = recovery.AllowCraft ? new() : null,
                    Production = new(floors),
                    Consumable = new("recovery", code, 100, target, target, reserve, recovery.MaxUsed, recovery.MaxMaterialUnits) };
                decimal preparation = 0, travel = 0;
                var inputs = ImmutableDictionary<string, int>.Empty;
                if (total < target)
                {
                    var goal = new ItemMilestone(code, target, value);
                    var estimatePolicy = derived with { Items = [goal], Consumable = null };
                    var production = new FullPathStrategy(new SkillPrerequisiteStrategy(goal, estimatePolicy, port), estimatePolicy).Evaluate(planning);
                    if (production.Rejection is not null || production.Path is not { } path) return Rejected(id, "RecoveryProduction:" + production.Rejection);
                    preparation = Math.Ceiling(path.Remaining / path.ExpectedProgress) * path.UnitSeconds + path.PreparationSeconds;
                    travel = path.TravelSeconds; inputs = path.Materials;
                    if (inputs.Values.Sum(x => (long)x) > recovery.MaxMaterialUnits - materials) return Rejected(id, "RecoveryMaterialBudgetExhausted");
                }
                else if (owned < needed)
                {
                    bool atBank = observation.Catalogs["maps"].Any(x => x.GetProperty("map_id").GetInt32() == state.MapId &&
                        x.GetProperty("interactions").GetProperty("content") is { ValueKind: JsonValueKind.Object } c && c.GetProperty("type").GetString() == "bank");
                    preparation = 3; travel = atBank ? 0 : policy.MoveSeconds;
                }
                var parent = new StrategyCandidate(id, "recovery", value, 3, travel, 0, null, hp >= maximum, null,
                    Path: new(new("hp", "recovery", maximum), "Use:" + code + ":" + needed, maximum - hp, Math.Min((long)needed * heal, maximum - hp),
                        3, preparation, travel, 0, inputs, "Finite food supply plus immediate use; existing bounded training estimate remains uncertain."), Discovery: evidence);
                var candidate = new ConsumableStrategy(new Parent(parent), derived, port).Evaluate(planning) with { Discovery = evidence, Path = parent.Path, Value = value };
                if (candidate.Command is { } command)
                {
                    if (!recovery.AllowCraft && command.Id.StartsWith("Craft:", StringComparison.Ordinal) ||
                        !recovery.AllowBankWithdrawal && (command.Id.StartsWith("Withdraw:", StringComparison.Ordinal) || command.Id.StartsWith("Deposit:", StringComparison.Ordinal)))
                        return Rejected(id, "RecoveryActionNotAllowed");
                    var charges = command.Charges?.ToImmutableDictionary(x => x.Key.StartsWith("use:", StringComparison.Ordinal) ? "recovery:use" : "recovery:materials", x => x.Value);
                    candidate = candidate with { Command = command with { SourceFingerprint = observation.Fingerprint, Charges = charges,
                        Productive = command.Id.StartsWith("Use:", StringComparison.Ordinal),
                        Dispatch = async token =>
                        {
                            var reply = await command.Dispatch(token);
                            return reply with { State = new(reply.State.Character, observation.Catalogs, observation.Policy,
                                recovery.AllowBankWithdrawal ? reply.State.Bank : observation.Bank) };
                        } } };
                }
                return Budget(candidate, observation);
            }
        }
        if (result.Count == 0) result.Add(Rejected("recovery", "NoSupportedRecoveryRoute"));
        return result.ToImmutable();
    }
    private StrategyCandidate Budget(StrategyCandidate candidate, StrategyObservation observation)
    {
        if (candidate.Command is null || candidate.Path is not { } path) return candidate;
        decimal seconds = (path.UnitSeconds + path.PreparationSeconds + path.TravelSeconds) * (policy.Measurement?.UnknownMultiplier ?? 2);
        return observation.Context.RemainingSeconds is { } remaining && seconds > remaining ? candidate with { Rejection = "EstimatedPathExceedsBudget", Command = null } : candidate;
    }
    private static StrategyCandidate Rejected(string id, string reason) => new(id, "recovery", 1, 1, 0, 0, reason, false, null);
}
