using System.Text.Json;
using Artiact.Services.Combat;

namespace Artiact.Services.Strategy;

public sealed class AutonomousCombatStrategy(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
{
    public StrategyCandidate Evaluate(StrategyObservation observation) => EvaluateAll(observation)
        .OrderByDescending(x => x.Score).ThenBy(x => x.Id, StringComparer.Ordinal).First();

    public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation observation)
    {
        var result = new List<StrategyCandidate>();
        StrategyCandidate Reject(string reason) => new("combat", "combat", policy.CombatValue, policy.FightSeconds, 0, 0, reason, false, null);
        try
        {
            var state = CombatObservation.Read(observation.Character);
            if (state is null) return [Reject("UnsupportedObservation")];
            if (state.Level >= policy.CombatTarget) return [Reject("") with { Rejection = null, Complete = true }];
            var stage = policy.AutonomousCombat!.Stages.First(x => x.Target > state.Level);
            var items = observation.Catalogs["items"];
            // Every equipped item must be understood, including slots outside this slice.
            foreach (var field in observation.Character.EnumerateObject().Where(x => x.Name.EndsWith("_slot", StringComparison.Ordinal)))
            {
                string code = field.Value.GetString()!;
                if (code == "") continue;
                string slot = field.Name[..^5];
                if (!EquipmentProjection.Supported(items.Single(x => x.GetProperty("code").GetString() == code), slot, state.Level))
                    return [Reject("UnsupportedEquippedItem:" + slot)];
            }
            foreach (string monster in stage.Monsters.Order(StringComparer.Ordinal))
            {
                var rawMonster = observation.Catalogs["monsters"].SingleOrDefault(x => x.GetProperty("code").GetString() == monster);
                if (rawMonster.ValueKind == JsonValueKind.Undefined || rawMonster.GetProperty("level").GetInt32() < 1 ||
                    (long)state.Level - rawMonster.GetProperty("level").GetInt32() >= 10) { result.Add(Reject("UnsupportedOrZeroXpOpponent:" + monster) with { Id = "combat:" + monster + ":rejected" }); continue; }
                var destination = CombatCatalog.Resolve(state, monster, observation.Catalogs["monsters"], observation.Catalogs["maps"], items).Destination;
                if (destination is null) { result.Add(Reject("UnsupportedOpponentOrAccess:" + monster) with { Id = "combat:" + monster + ":rejected" }); continue; }
                var baseline = CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, destination.Monster);
                var direct = new CombatMilestoneStrategy(policy with { AutonomousCombat = null, Monster = monster, CombatTarget = stage.Target }, port).Evaluate(observation);
                result.Add(direct with { Id = "combat:" + monster + ":current", CombatRoute = new(stage.Target, monster, null, null, baseline.MaximumLoss, 0) });
                foreach (string code in policy.AutonomousCombat.Equipment.Order(StringComparer.Ordinal))
                {
                    var item = items.SingleOrDefault(x => x.GetProperty("code").GetString() == code);
                    if (item.ValueKind == JsonValueKind.Undefined) continue;
                    string slot = item.GetProperty("type").GetString()!;
                    if (!EquipmentProjection.Supported(item, slot, state.Level) || observation.Character.GetProperty(slot + "_slot").GetString() == code) continue;
                    var projected = EquipmentProjection.Replace(observation.Character, items, slot, code);
                    if (projected is null) continue;
                    var prediction = CombatPrediction.Evaluate(CombatObservation.Read(projected.Value)!.Stats with { Hp = state.MaxHp }, destination.Monster);
                    if (prediction.Viability != CombatViability.Safe || baseline.Viability == CombatViability.Safe && prediction.MaximumLoss >= baseline.MaximumLoss) continue;
                    var preparation = Prepare(observation, slot, code);
                    decimal cost = PreparationCost(observation, code, preparation);
                    result.Add(preparation with { Id = "combat:" + monster + ":" + slot + ":" + code, Category = "combat", Value = policy.CombatValue,
                        ActionSeconds = policy.FightSeconds + cost, TravelSeconds = state.MapId == destination.MapId ? 0 : policy.MoveSeconds,
                        RecoverySeconds = policy.RestSeconds, Complete = false, EstimateSource = "ConfiguredRouteWithTrainingAllowance",
                        CombatRoute = new(stage.Target, monster, slot, code, prediction.MaximumLoss, cost) });
                }
            }
            return result.Count == 0 ? [Reject("NoSupportedCombatRoute")] : result;
        }
        catch (Exception) { return [Reject("InvalidCombatRouteStateOrCatalog")]; }
    }

    private decimal PreparationCost(StrategyObservation observation, string code, StrategyCandidate preparation)
    {
        var plan = ProductionStock.Plan(observation, new(code, 1), policy, true);
        if (plan.Rejection is not null) return 0; // Rejected routes have no executable cost; do not invalidate other candidates with a sentinel.
        return policy.EquipmentSeconds * 2 + plan.Steps.Sum(x => x.Kind switch
        {
            "Gather" => x.Quantity * policy.GatherSeconds + policy.MoveSeconds,
            "Craft" => x.Quantity * policy.EquipmentSeconds + policy.MoveSeconds,
            "Withdraw" => policy.EquipmentSeconds + policy.MoveSeconds,
            _ => x.Quantity * (policy.FightSeconds + policy.RestSeconds) + policy.MoveSeconds
        }) + (preparation.Prerequisite is null ? 0 : 10 * (policy.GatherSeconds + policy.EquipmentSeconds + policy.MoveSeconds));
    }

    private StrategyCandidate Prepare(StrategyObservation observation, string slot, string code)
    {
        var production = new SkillPrerequisiteStrategy(new(code, 1, policy.CombatValue), policy, port, true).Evaluate(observation);
        if (!production.Complete) return production;
        var state = CharacterObservation.Read(observation.Character)!;
        string old = observation.Character.GetProperty(slot + "_slot").GetString()!;
        bool remove = old != "";
        if (remove && state.FreeUnits < 1) return production with { Complete = false, Rejection = "InventoryPressure" };
        string movedCode = remove ? old : code;
        var projected = EquipmentProjection.Replace(observation.Character, observation.Catalogs["items"], slot, remove ? "" : code);
        if (projected is null) return production with { Complete = false, Rejection = "UnsupportedEquipmentProjection" };
        var expected = state.Inventory.ToBuilder();
        expected[movedCode] = checked(expected.GetValueOrDefault(movedCode) + (remove ? 1 : -1));
        if (expected[movedCode] == 0) expected.Remove(movedCode);
        return production with { Complete = false, Rejection = null, Command = port.Equipment(observation, slot, movedCode, remove, after =>
            CharacterObservation.Read(after.Character) is { } changed && changed.Inventory.Count == expected.Count && expected.All(x => changed.Inventory.GetValueOrDefault(x.Key) == x.Value) &&
            CharacterObservation.Preserved(projected.Value, after.Character, "inventory")) };
    }
}
