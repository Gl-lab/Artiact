using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Microsoft.Extensions.Options;

namespace Artiact.Services;

public sealed class MiningRunState(IOptions<MiningProgressionSettings> options)
{
    public int MaxCycles { get; } = options.Value.MaxCycles;
    public int MaxNoProgress { get; } = options.Value.MaxConsecutiveNoProgress;
    public int AttemptedCycles { get; private set; }
    public int ConsecutiveNoProgress { get; private set; }
    public bool DestinationNotReached { get; private set; }

    public static bool ValidProgress(Character character) => character.MiningLevel >= 0 &&
        character.MiningXp >= 0 && character.MiningMaxXp > 0 && character.MiningXp < character.MiningMaxXp;

    public GoalDecisionReason? Guard(Character character) =>
        !ValidProgress(character) ? GoalDecisionReason.InvalidMiningProgress :
        DestinationNotReached ? GoalDecisionReason.MiningDestinationNotReached :
        ConsecutiveNoProgress >= MaxNoProgress ? GoalDecisionReason.MiningNoProgress :
        AttemptedCycles >= MaxCycles ? GoalDecisionReason.MiningCycleLimit : null;

    public void ReserveAttempt() => AttemptedCycles++;
    public void RecordMovementFailure() => DestinationNotReached = true;
    public void RecordGather(int beforeLevel, int beforeXp, Character after)
    {
        if (!ValidProgress(after)) return;
        ConsecutiveNoProgress = after.MiningLevel > beforeLevel ||
            after.MiningLevel == beforeLevel && after.MiningXp > beforeXp ? 0 : ConsecutiveNoProgress + 1;
    }
    public void Reset()
    {
        AttemptedCycles = 0;
        ConsecutiveNoProgress = 0;
        DestinationNotReached = false;
    }
}
