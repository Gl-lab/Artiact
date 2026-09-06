using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Microsoft.Extensions.Options;

namespace Artiact.Services;

public class GoalService(IOptions<GoalSelectionSettings> settings) : IGoalService
{
    private readonly int _target = settings.Value.MiningTargetLevel;

    public GoalDecision Evaluate(Character? character)
    {
        if (_target <= 0)
            return GoalDecision.Create(GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidGoalPolicy, _target);
        if (character is null || character.MiningLevel < 0)
            return GoalDecision.Create(GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidCharacterSnapshot,
                _target, character?.MiningLevel);
        int level = character.MiningLevel;
        if (level >= _target)
            return GoalDecision.Create(GoalDecisionStatus.Completed, GoalDecisionReason.MiningTargetReached, _target, level);
        if (!MiningInventory.TryRead(character, out int used, out int free))
            return GoalDecision.Create(GoalDecisionStatus.Blocked, GoalDecisionReason.InvalidInventorySnapshot, _target, level);
        return free < GoalDecision.InventoryReserve
            ? GoalDecision.Create(GoalDecisionStatus.Blocked, GoalDecisionReason.InventoryPressure,
                _target, level, character.InventoryMaxItems, used, free)
            : GoalDecision.Create(GoalDecisionStatus.Selected, GoalDecisionReason.MiningBelowTarget,
                _target, level, character.InventoryMaxItems, used, free, GoalType.Gathering);
    }

}
