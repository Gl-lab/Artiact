using Artiact.Contracts.Models;

namespace Artiact.Models;

public enum GoalDecisionStatus { Selected, Completed, Blocked }
public enum GoalDecisionReason
{
    MiningBelowTarget, MiningTargetReached, InvalidGoalPolicy,
    InvalidCharacterSnapshot, InvalidInventorySnapshot, InventoryPressure,
    InvalidMiningProgress, MiningDestinationNotReached, MiningNoProgress,
    MiningCycleLimit, InvalidMiningCatalog, NoMiningDestination
}

public sealed record GoalDecision
{
    private GoalDecision(GoalDecisionStatus status, GoalDecisionReason reason, int target,
        int? current, int? capacity, int? used, int? free, GoalType? selectedGoalType)
    {
        Status = status;
        Reason = reason;
        MiningTargetLevel = target;
        CurrentMiningLevel = current;
        InventoryCapacity = capacity;
        InventoryUsed = used;
        InventoryFree = free;
        SelectedGoalType = selectedGoalType;
    }

    public GoalDecisionStatus Status { get; }
    public GoalDecisionReason Reason { get; }
    public string ReasonCode => Reason switch
    {
        GoalDecisionReason.MiningBelowTarget => "mining_below_target",
        GoalDecisionReason.MiningTargetReached => "mining_target_reached",
        GoalDecisionReason.InvalidGoalPolicy => "invalid_goal_policy",
        GoalDecisionReason.InvalidCharacterSnapshot => "invalid_character_snapshot",
        GoalDecisionReason.InvalidInventorySnapshot => "invalid_inventory_snapshot",
        GoalDecisionReason.InventoryPressure => "inventory_pressure",
        GoalDecisionReason.InvalidMiningProgress => "invalid_mining_progress",
        GoalDecisionReason.MiningDestinationNotReached => "mining_destination_not_reached",
        GoalDecisionReason.MiningNoProgress => "mining_no_progress",
        GoalDecisionReason.MiningCycleLimit => "mining_cycle_limit",
        GoalDecisionReason.InvalidMiningCatalog => "invalid_mining_catalog",
        GoalDecisionReason.NoMiningDestination => "no_mining_destination",
        _ => throw new ArgumentOutOfRangeException(nameof(Reason))
    };
    public int MiningTargetLevel { get; }
    public int? CurrentMiningLevel { get; }
    public int? InventoryCapacity { get; }
    public int? InventoryUsed { get; }
    public int? InventoryFree { get; }
    public const int InventoryReserve = 10;
    public int RequiredFreeInventory => InventoryReserve;
    public GoalType? SelectedGoalType { get; }

    public static GoalDecision Create(GoalDecisionStatus status, GoalDecisionReason reason,
        int target, int? current = null, int? capacity = null, int? used = null,
        int? free = null, GoalType? selectedGoalType = null)
    {
        bool noInventory = capacity is null && used is null && free is null;
        bool validInventory = capacity >= 0 && used >= 0 && free >= 0 &&
                              (long?)used + free == capacity;
        bool belowTarget = target > 0 && current >= 0 && current < target;
        bool valid = (status, reason) switch
        {
            (GoalDecisionStatus.Selected, GoalDecisionReason.MiningBelowTarget) =>
                belowTarget && validInventory && free >= InventoryReserve && selectedGoalType == GoalType.Gathering,
            (GoalDecisionStatus.Completed, GoalDecisionReason.MiningTargetReached) =>
                target > 0 && current >= target && noInventory && selectedGoalType is null,
            (GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidGoalPolicy) =>
                target <= 0 && current is null && noInventory && selectedGoalType is null,
            (GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidCharacterSnapshot) =>
                target > 0 && (current is null || current < 0) && noInventory && selectedGoalType is null,
            (GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidInventorySnapshot) =>
                belowTarget && noInventory && selectedGoalType is null,
            (GoalDecisionStatus.Blocked, GoalDecisionReason.InventoryPressure) =>
                belowTarget && validInventory && free < InventoryReserve && selectedGoalType is null,
            (GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidMiningProgress or
                GoalDecisionReason.MiningDestinationNotReached or GoalDecisionReason.MiningNoProgress or
                GoalDecisionReason.MiningCycleLimit or GoalDecisionReason.InvalidMiningCatalog or
                GoalDecisionReason.NoMiningDestination) =>
                belowTarget && noInventory && selectedGoalType is null,
            _ => false
        };
        if (!valid)
            throw new ArgumentException("Invalid goal decision facts.");

        return new GoalDecision(status, reason, target, current, capacity, used, free, selectedGoalType);
    }
}
