using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class TargetLootingResolver : ITargetLootingResolver
{
    private readonly IGameClient _gameClient;

    public TargetLootingResolver( IGameClient gameClient )
    {
        _gameClient = gameClient;
    }

    public async Task<LootTarget?> FindTarget( ItemDatum craftComponent, int requiredQuantity,
                                               ICharacterService characterService )
    {
        if ( craftComponent.Subtype != "mob" || requiredQuantity <= 0 )
        {
            return null;
        }

        int maximumMonsterLevel = characterService.GetCharacter().Level + 1;
        MonsterDatum? monster = ( await _gameClient.GetMonsters() )
            .Where( candidate => candidate.Level <= maximumMonsterLevel )
            .Where( candidate => candidate.Drops.Any( drop => drop.Code == craftComponent.Code ) )
            .OrderByDescending( candidate => candidate.Drops
                .First( drop => drop.Code == craftComponent.Code ).Rate )
            .FirstOrDefault();

        return monster == null
            ? null
            : new LootTarget
            {
                Monster = monster,
                ItemCode = craftComponent.Code,
                RequiredQuantity = requiredQuantity
            };
    }
}
