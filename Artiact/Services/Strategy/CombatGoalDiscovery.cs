using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class CombatGoalDiscovery(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation state) => EvaluateAll(state).OrderByDescending(x => x.Score).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault() ?? Reject("NoCombatNeed");
    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation state)
    {
        try { return Discover(state); }
        catch (Exception ex) when (ex is InvalidOperationException or KeyNotFoundException or ArgumentException or OverflowException or FormatException)
        { return [Reject("InvalidCombatDiscoveryStateOrCatalog")]; }
    }
    private ImmutableArray<StrategyCandidate> Discover(StrategyObservation observation)
    {
        var permission = policy.CombatDiscovery!;
        var active = observation.Context.Autonomous?.Active;
        if (active is not null && active.Skill != "combat") return [];
        var state = CombatObservation.Read(observation.Character);
        if (state is null || state.Level is < 1 or > 50) return [Reject("UnsupportedCombatObservation")];
        if (state.Level == 50) return [Reject("SupportedSkillCapReached")];
        if (!permission.AllowFight) return [Reject("CombatFightNotAllowed")];
        int target = active?.Target ?? state.Level + 1;
        if (observation.Context.Autonomous?.History.Any(x => x.Outcome == "Rejected" && x.Goal.Skill == "combat" && x.Goal.Target == target) == true)
            return [Reject("PreviouslyRejectedCombat")];
        var monsters = observation.Catalogs["monsters"]; var items = observation.Catalogs["items"];
        if (monsters.Length > 4096 || items.Length > 16384 || Duplicate(monsters) || Duplicate(items)) return [Reject("InvalidCombatDiscoveryCatalog")];
        var opponents = monsters.OrderBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).Take(10).Select(x => x.GetProperty("code").GetString()!).ToImmutableArray();
        var equipment = permission.AllowEquip ? items.Where(x => x.TryGetProperty("type", out var type) && type.GetString() is "weapon" or "shield" &&
                EquipmentProjection.Supported(x, type.GetString()!, state.Level))
            .OrderBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).Take(20).Select(x => x.GetProperty("code").GetString()!).ToImmutableArray() : [];
        if (opponents.IsEmpty) return [Reject("NoSupportedCombatRoute")];
        var planning = new StrategyObservation(observation.Character, observation.Catalogs, observation.Policy,
            permission.AllowBankWithdrawal ? observation.Bank : null, observation.Context);
        string? safeFallback = opponents.FirstOrDefault(code => Safe(code) && state.Level - monsters.Single(x => x.GetProperty("code").GetString() == code).GetProperty("level").GetInt32() < 10);
        string fallback = safeFallback ?? opponents[0];
        bool Safe(string code)
        {
            var destination = CombatCatalog.Resolve(state, code, monsters, observation.Catalogs["maps"], items).Destination;
            return destination is not null && CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, destination.Monster).Viability == CombatViability.Safe;
        }
        var derived = policy with { AutonomousGoals = false, CombatDiscovery = null, Recovery = null, Skills = [], Items = [],
            CombatTarget = target, Monster = fallback, CombatValue = 0.1m, AutonomousCombat = new([new(target, opponents)], equipment),
            Bank = permission.AllowBankWithdrawal ? policy.Bank : null, Production = new(permission.Reserved ?? ImmutableDictionary<string, int>.Empty),
            Preparation = permission.AllowCraft ? new() : null };
        var routes = new FullPathStrategy(new AutonomousCombatStrategy(derived, port, safeFallback is not null), derived with { PrepareEquipment = safeFallback is not null }).EvaluateAll(planning);
        var result = ImmutableArray.CreateBuilder<StrategyCandidate>();
        if (permission.AllowEquip)
            foreach (var unsupported in items.Where(x => x.TryGetProperty("type", out var type) && type.GetString() is "weapon" or "shield" &&
                !EquipmentProjection.Supported(x, type.GetString()!, state.Level)).OrderBy(x => x.GetProperty("code").GetString(), StringComparer.Ordinal).Take(20))
            {
                string code = unsupported.GetProperty("code").GetString()!;
                result.Add(Reject("UnsupportedDiscoveryEquipment:" + code) with { Id = "combat-discovery:equipment:" + code });
            }
        foreach (var original in routes)
        {
            var candidate = original;
            if (candidate.CombatRoute is not { } route) { result.Add(candidate); continue; }
            if (active?.UnlockUtility == 1 && active.Unlock != route.Monster) continue;
            bool unlock = route.Equipment is not null && !Safe(route.Monster);
            var evidence = new GoalDiscoveryEvidence("combat-discovery-v1", "combat", target, route.Monster, unlock ? 1 : 0, unlock ? 0 : 0.1m, 0, [],
                "Next combat level; safe route opening vs ordinary progress. Single replacement, finite preparation and uncertain future cooldown/XP.", "Target level, changed catalog or infeasible route");
            if (active?.UnlockUtility == 1) evidence = active;
            candidate = candidate with { Value = evidence.UnlockUtility + evidence.ProgressUtility, Discovery = evidence };
            if (candidate.Command is null || candidate.Path is not { } path) { result.Add(candidate); continue; }
            int spent = observation.Context.Used.GetValueOrDefault("combat:materials");
            if (path.Materials.Values.Sum(x => (long)x) > permission.MaxMaterialUnits - spent)
            { result.Add(Block(candidate, "CombatMaterialBudgetExhausted")); continue; }
            if (!permission.AllowCraft && route.Equipment is { } gear && CharacterObservation.Read(planning.Character)!.Inventory.GetValueOrDefault(gear) + (planning.Bank?.Items.GetValueOrDefault(gear) ?? 0) == 0)
            { result.Add(Block(candidate, "CombatProductionNotAllowed")); continue; }
            if (state.Stats.Hp < state.MaxHp)
            {
                if (policy.Recovery is null) { result.Add(Block(candidate, "CombatRecoveryNotAllowed")); continue; }
                var recoveryPolicy = policy with { CombatDiscovery = null, Recovery = policy.Recovery with { HpBelowPercent = 100 } };
                var recoveryState = new StrategyObservation(observation.Character, observation.Catalogs, observation.Policy, observation.Bank,
                    observation.Context with { Autonomous = AutonomousRunState.Empty });
                var recovery = new RecoveryGoalDiscovery(recoveryPolicy, port).EvaluateAll(recoveryState)
                    .Where(x => x.Command is not null && x.Rejection is null && x.Path is not null)
                    .OrderBy(x => x.Path!.UnitSeconds + x.Path.PreparationSeconds + x.Path.TravelSeconds).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                if (recovery is null) { result.Add(Block(candidate, "NoAllowedCombatRecoveryRoute")); continue; }
                candidate = candidate with { Command = recovery.Command, Path = path with {
                    PreparationSeconds = path.PreparationSeconds + recovery.Path!.PreparationSeconds + recovery.Path.TravelSeconds,
                    RecoverySeconds = recovery.Path.UnitSeconds } };
            }
            var command = candidate.Command!;
            if (!permission.AllowCraft && command.Id.StartsWith("Craft:", StringComparison.Ordinal) && command.Charges is null ||
                !permission.AllowEquip && (command.Id.StartsWith("Equip:", StringComparison.Ordinal) || command.Id.StartsWith("Unequip:", StringComparison.Ordinal)))
            { result.Add(Block(candidate, "CombatActionNotAllowed")); continue; }
            var charges = command.Charges;
            if (command.Id.StartsWith("Craft:", StringComparison.Ordinal) && charges is null)
            {
                string[] parts = command.Id.Split(':'); int quantity = parts.Length > 2 ? int.Parse(parts[2]) : 1;
                int inputs = checked(items.Single(x => x.GetProperty("code").GetString() == parts[1]).GetProperty("craft").GetProperty("items").EnumerateArray().Sum(x => x.GetProperty("quantity").GetInt32()) * quantity);
                if (inputs > permission.MaxMaterialUnits - spent) { result.Add(Block(candidate, "CombatMaterialBudgetExhausted")); continue; }
                charges = ImmutableDictionary<string, int>.Empty.Add("combat:materials", inputs);
            }
            candidate = candidate with { Command = command with { SourceFingerprint = observation.Fingerprint, Charges = charges,
                Productive = command.Id == "Fight", Dispatch = async token => {
                    var reply = await command.Dispatch(token);
                    return reply with { State = new(reply.State.Character, observation.Catalogs, observation.Policy, reply.State.Bank ?? observation.Bank) };
                } } };
            var cost = candidate.Path!;
            decimal seconds = (Math.Ceiling(cost.Remaining / Math.Max(1, cost.ExpectedProgress)) * cost.UnitSeconds + cost.PreparationSeconds + cost.TravelSeconds + cost.RecoverySeconds) * (policy.Measurement?.UnknownMultiplier ?? 2);
            result.Add(observation.Context.RemainingSeconds is { } remaining && seconds > remaining ? Block(candidate, "EstimatedPathExceedsBudget") : candidate);
        }
        return result.ToImmutable();
    }
    private static bool Duplicate(ImmutableArray<JsonElement> items) => items.Select(x => x.GetProperty("code").GetString()).Distinct(StringComparer.Ordinal).Count() != items.Length;
    private static StrategyCandidate Block(StrategyCandidate candidate, string reason) => candidate with { Rejection = reason, Command = null };
    private static StrategyCandidate Reject(string reason) => new("combat-discovery", "combat", 0.1m, 1, 0, 0, reason, false, null);
}
