namespace Artiact.Services.Combat;

public sealed record CombatLevelGoal(int TargetLevel);
public sealed record CombatLimits(int Decisions = 20, int Fights = 4, int Rests = 2, int NoProgress = 3);
public enum CombatCommand { Move, Fight, Rest, Unequip, Equip, Craft }
public enum CombatStatus { Selected, Completed, Blocked }
public enum CombatReason
{
    CommandSelected, TargetReached, InvalidTarget, InvalidLimits, InvalidState, InventoryPressure,
    NoProgress, DecisionLimit, FightLimit, RestLimit, UnsupportedAccess, UnknownCombat, UnsafeCombat,
    EquipmentUnavailable, CraftUnavailable, InvalidPostcondition, RecoveryNoProgress, Defeat, Rejected, UnknownOutcome, Cancelled
}
public sealed record CombatDestination(int MapId, string Layer, string MonsterCode, CombatStats Monster, bool Accessible,
    CombatCraftCommand? CraftCommand = null);
public sealed record CombatGear(string Code, CombatStats ProjectedStats, bool ConditionsMet = true);
public sealed record CombatReply(CombatObservation? State, int Cooldown, bool Defeat = false, bool ContractValid = true);
public sealed record CombatDecision(CombatStatus Status, CombatReason Reason, CombatCommand? Command,
    CombatObservation? State, int Decisions, int Fights, int Rests, int NoProgress, long VirtualSeconds);

public interface ICombatActionPort
{
    Task<CombatReply> DispatchAsync(CombatCommand command, CombatDestination destination, string? equipment,
        CancellationToken cancellationToken);
}

public sealed class CombatRun(CombatLevelGoal goal, CombatObservation? initial, CombatDestination destination,
    ICombatActionPort port, IMiningCooldownDelay cooldown, CombatLimits? limits = null, CombatGear? gear = null,
    CombatCraftPlan? craftPlan = null)
{
    private readonly CombatLimits _limits = limits ?? new();
    private readonly SemaphoreSlim _cycle = new(1, 1);
    private int _decisions, _fights, _rests, _noProgress;
    private long _seconds;
    private CombatDecision? _terminal;
    private int _craftIndex;
    public CombatObservation? State { get; private set; } = initial;
    public async Task<CombatDecision> ExecuteCycleAsync(CancellationToken cancellationToken = default)
    {
        await _cycle.WaitAsync(CancellationToken.None);
        try { return await ExecuteCoreAsync(cancellationToken); }
        finally { _cycle.Release(); }
    }

    private async Task<CombatDecision> ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (_terminal is not null) return _terminal;
        _decisions++;
        if (cancellationToken.IsCancellationRequested) return Stop(CombatReason.Cancelled);
        if (goal.TargetLevel <= 0) return Stop(CombatReason.InvalidTarget);
        if (_limits.Decisions <= 0 || _limits.Fights <= 0 || _limits.Rests <= 0 ||
            _limits.NoProgress <= 0 || _limits.NoProgress > _limits.Decisions) return Stop(CombatReason.InvalidLimits);
        if (!Valid(State)) return Stop(CombatReason.InvalidState);
        var state = State!;
        if (state.Level >= goal.TargetLevel)
            return _terminal = Decision(CombatStatus.Completed, CombatReason.TargetReached);
        var activeGear = craftPlan is not null && _craftIndex == craftPlan.Steps.Length ? craftPlan.Gear : gear;
        bool canFinishSwap = activeGear is not null && state.Weapon.Length == 0 && state.Inventory.GetValueOrDefault(activeGear.Code) > 0;
        bool canConsumeForCraft = craftPlan is not null && _craftIndex < craftPlan.Steps.Length &&
            craftPlan.Steps[_craftIndex].Required.Count > 0 && craftPlan.Steps[_craftIndex].Required.All(x =>
                x.Value > 0 && state.Inventory.GetValueOrDefault(x.Key) >= x.Value);
        if (state.FreeUnits < 1 && !canFinishSwap && !canConsumeForCraft) return Stop(CombatReason.InventoryPressure);
        if (_noProgress >= _limits.NoProgress) return Stop(CombatReason.NoProgress);
        if (_decisions >= _limits.Decisions) return Stop(CombatReason.DecisionLimit);
        if (!destination.Accessible || destination.MapId <= 0 || destination.Layer != state.Layer ||
            string.IsNullOrWhiteSpace(destination.MonsterCode)) return Stop(CombatReason.UnsupportedAccess);
        var baseline = CombatPrediction.Evaluate(state.Stats with { Hp = state.MaxHp }, destination.Monster);
        if (baseline.Viability == CombatViability.Unknown) return Stop(CombatReason.UnknownCombat);

        CombatCommand command;
        var dispatchDestination = destination;
        CombatCraftCommand? craft = null;
        if (craftPlan is not null && _craftIndex < craftPlan.Steps.Length)
        {
            craft = craftPlan.Steps[_craftIndex];
            if (craft.Quantity <= 0 || craft.OutputQuantity <= 0 || craft.WorkshopMapId <= 0 || craft.Layer != state.Layer ||
                craft.Required.Count == 0 || craft.Required.Any(x => x.Value <= 0)) return Stop(CombatReason.CraftUnavailable);
            var missing = craft.Required.Where(x => state.Inventory.GetValueOrDefault(x.Key) < x.Value).ToArray();
            if (missing.Any(x => x.Key != craftPlan.LootCode)) return Stop(CombatReason.CraftUnavailable);
            if (missing.Length == 0)
            {
                if (state.FreeUnits + craft.Required.Values.Sum(x => (long)x) < craft.OutputQuantity)
                    return Stop(CombatReason.InventoryPressure);
                dispatchDestination = destination with { MapId = craft.WorkshopMapId, CraftCommand = craft };
                command = state.MapId == craft.WorkshopMapId ? CombatCommand.Craft : CombatCommand.Move;
            }
            else
            {
                if (baseline.Viability != CombatViability.Safe) return Stop(CombatReason.UnsafeCombat);
                command = state.Stats.Hp < state.MaxHp ? CombatCommand.Rest : state.MapId != destination.MapId ? CombatCommand.Move : CombatCommand.Fight;
            }
        }
        else if (activeGear is not null && state.Weapon != activeGear.Code)
        {
            var candidate = CombatPrediction.Evaluate(activeGear.ProjectedStats with { Hp = state.MaxHp }, destination.Monster);
            if (!activeGear.ConditionsMet || !state.Inventory.TryGetValue(activeGear.Code, out int owned) || owned < 1 ||
                candidate.Viability != CombatViability.Safe ||
                baseline.Viability == CombatViability.Safe && candidate.MaximumLoss >= baseline.MaximumLoss)
                return Stop(CombatReason.EquipmentUnavailable);
            command = state.Weapon.Length == 0 ? CombatCommand.Equip : CombatCommand.Unequip;
        }
        else
        {
            if (baseline.Viability != CombatViability.Safe) return Stop(CombatReason.UnsafeCombat);
            command = state.Stats.Hp < state.MaxHp ? CombatCommand.Rest :
                state.MapId != destination.MapId ? CombatCommand.Move : CombatCommand.Fight;
        }
        if (command == CombatCommand.Fight && _fights >= _limits.Fights) return Stop(CombatReason.FightLimit);
        if (command == CombatCommand.Rest && _rests >= _limits.Rests) return Stop(CombatReason.RestLimit);
        if (cancellationToken.IsCancellationRequested) return Stop(CombatReason.Cancelled);
        if (command == CombatCommand.Fight) _fights++;
        if (command == CombatCommand.Rest) _rests++;
        _noProgress++;
        CombatReply reply;
        try { reply = await port.DispatchAsync(command, dispatchDestination, activeGear?.Code, cancellationToken); }
        catch (Artiact.Contracts.Client.ActionFailureException ex)
        {
            return Stop(ex.Kind == Artiact.Contracts.Client.ActionFailureKind.Rejected ? CombatReason.Rejected :
                ex.Kind == Artiact.Contracts.Client.ActionFailureKind.Defeat ? CombatReason.Defeat : CombatReason.UnknownOutcome);
        }
        catch (Exception) { return Stop(CombatReason.UnknownOutcome); }

        State = reply.State;
        if (reply.Defeat) return Stop(CombatReason.Defeat);
        if (!reply.ContractValid || !Valid(State) || reply.Cooldown < 0) return Stop(CombatReason.InvalidPostcondition);
        var after = State!;
        _seconds += reply.Cooldown;
        if (command == CombatCommand.Fight && (after.Level > state.Level || after.Level == state.Level && after.Xp > state.Xp))
            _noProgress = 0;
        if (after.Name != state.Name || after.Layer != state.Layer) return Stop(CombatReason.InvalidPostcondition);
        if (command == CombatCommand.Move && after.MapId != dispatchDestination.MapId ||
            command != CombatCommand.Move && after.MapId != state.MapId) return Stop(CombatReason.InvalidPostcondition);
        if (command == CombatCommand.Rest && after.Stats.Hp <= state.Stats.Hp) return Stop(CombatReason.RecoveryNoProgress);
        if (command is CombatCommand.Equip or CombatCommand.Unequip && !ValidEquipment(state, after, command, activeGear!))
            return Stop(CombatReason.InvalidPostcondition);
        if (command == CombatCommand.Craft)
        {
            var expected = state.Inventory.ToBuilder();
            foreach (var ingredient in craft!.Required)
            {
                int remaining = expected.GetValueOrDefault(ingredient.Key) - ingredient.Value;
                if (remaining < 0) return Stop(CombatReason.InvalidPostcondition);
                if (remaining == 0) expected.Remove(ingredient.Key); else expected[ingredient.Key] = remaining;
            }
            long output = (long)expected.GetValueOrDefault(craft.Code) + craft.OutputQuantity;
            if (output > int.MaxValue) return Stop(CombatReason.InvalidPostcondition);
            expected[craft.Code] = (int)output;
            if (after.Weapon != state.Weapon || expected.Count != after.Inventory.Count ||
                expected.Any(x => after.Inventory.GetValueOrDefault(x.Key) != x.Value)) return Stop(CombatReason.InvalidPostcondition);
            _craftIndex++;
        }
        if (cancellationToken.IsCancellationRequested) return Stop(CombatReason.Cancelled);
        try { await cooldown.WaitAsync(reply.Cooldown, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return Stop(CombatReason.Cancelled); }
        catch (Exception) { return Stop(CombatReason.InvalidPostcondition); }
        if (cancellationToken.IsCancellationRequested) return Stop(CombatReason.Cancelled);
        return Decision(CombatStatus.Selected, CombatReason.CommandSelected, command);
    }

    private bool ValidEquipment(CombatObservation before, CombatObservation after, CombatCommand command, CombatGear activeGear)
    {
        string code = command == CombatCommand.Unequip ? before.Weapon : activeGear.Code;
        if (after.Weapon != (command == CombatCommand.Unequip ? "" : code)) return false;
        int quantity = before.Inventory.GetValueOrDefault(code) + (command == CombatCommand.Unequip ? 1 : -1);
        var expected = quantity == 0 ? before.Inventory.Remove(code) : before.Inventory.SetItem(code, quantity);
        return expected.Count == after.Inventory.Count && expected.All(x => after.Inventory.GetValueOrDefault(x.Key) == x.Value);
    }

    private static bool Valid(CombatObservation? state) => state is not null && !string.IsNullOrWhiteSpace(state.Name) &&
        state.Level > 0 && state.Xp >= 0 && state.MaxXp > state.Xp && state.MaxHp is > 0 and <= 1_000_000 &&
        state.Stats is not null && state.Stats.Hp > 0 && state.Stats.Hp <= state.MaxHp && state.MapId > 0 &&
        state.Weapon is not null && state.Inventory is not null && state.Capacity >= 0 && state.FreeUnits >= 0 &&
        state.Inventory.All(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0);

    private CombatDecision Stop(CombatReason reason) => _terminal = Decision(CombatStatus.Blocked, reason);
    private CombatDecision Decision(CombatStatus status, CombatReason reason, CombatCommand? command = null) =>
        new(status, reason, command, State, _decisions, _fights, _rests, _noProgress, _seconds);
}
