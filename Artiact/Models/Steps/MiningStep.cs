using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;

namespace Artiact.Models.Steps;

public sealed class MiningStep(ICharacterService characterService, int target,
    MiningDestination destination, MiningRunState runState, IMiningCooldownDelay delay) : IStep
{
    private bool CanMine(Character character) => character is not null &&
        character.MiningLevel < target && MiningRunState.ValidProgress(character) &&
        MiningInventory.TryRead(character, out _, out int free) && free >= GoalDecision.InventoryReserve &&
        destination.ResourceLevel <= Math.Max(1, character.MiningLevel) &&
        (long)character.MiningLevel < (long)destination.ResourceLevel + 10;

    private bool AtDestination(Character character) => character.X == destination.X && character.Y == destination.Y;

    public async Task Execute(IGameClient client, CancellationToken cancellationToken)
    {
        using var operation = (client as Artiact.Client.GameClient)?.BeginOperation(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Character before = characterService.GetCharacter();
        if (!CanMine(before)) return;
        int selectedLevel = before.MiningLevel;
        if (!AtDestination(before))
        {
            var move = await client.Move(new MapPoint { X = destination.X, Y = destination.Y });
            characterService.SaveCharacter(move.RequireCharacter());
            if (move.RequireCharacter() is not null && !AtDestination(move.RequireCharacter())) runState.RecordMovementFailure();
            cancellationToken.ThrowIfCancellationRequested();
            await delay.WaitAsync(move.RequireCooldown().TotalSeconds, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            before = characterService.GetCharacter();
            if (!CanMine(before) || before.MiningLevel != selectedLevel || !AtDestination(before)) return;
        }
        // Capture scalar progress before the call; an external provider may mutate its DTO.
        int level = before.MiningLevel;
        int xp = before.MiningXp;
        cancellationToken.ThrowIfCancellationRequested();
        var gather = await client.Gathering();
        characterService.SaveCharacter(gather.RequireCharacter());
        if (gather.RequireCharacter() is not null) runState.RecordGather(level, xp, gather.RequireCharacter());
        cancellationToken.ThrowIfCancellationRequested();
        await delay.WaitAsync(gather.RequireCooldown().TotalSeconds, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
}
