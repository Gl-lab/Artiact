using Artiact.Client;

namespace Artiact.Services.Combat;

// Explicit run construction only: registering this factory does not start game actions.
public sealed class CombatSessionFactory(GameClient client, CombatCatalog catalog,
    ICharacterService characters, IMiningCooldownDelay cooldown)
{
    public async Task<CombatRun> CreateAsync(CombatLevelGoal goal, string monsterCode,
        CombatLimits limits, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        characters.SaveCharacter(await client.GetCharacter());
        cancellationToken.ThrowIfCancellationRequested();
        var state = client.LastCharacterPayload is { } raw ? CombatObservation.Read(raw) : null;
        var world = state is null || goal.TargetLevel <= state.Level
            ? (Destination: (CombatDestination?)null, Gear: (CombatGear?)null)
            : await catalog.ResolveAsync(state, monsterCode, cancellationToken);
        return new(goal, state, world.Destination ?? new(0, "", monsterCode, new(1, 0), false),
            new CombatActionPort(client, characters), cooldown, limits, world.Gear);
    }
}
