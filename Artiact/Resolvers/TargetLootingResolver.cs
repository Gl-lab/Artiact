using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class TargetLootingResolver : ITargetLootingResolver
{
    private readonly IGameClient _gameClient;
    private readonly IMapService _mapService;

    public TargetLootingResolver( IGameClient gameClient, IMapService mapService )
    {
        _gameClient = gameClient;
        _mapService = mapService;
    }

    public async Task<LootPrerequisite?> Resolve( ItemDatum craftComponent,
                                                  int requiredQuantity,
                                                  ICharacterService characterService )
    {
        if ( craftComponent.Subtype != "mob" || requiredQuantity <= 0 )
        {
            return null;
        }

        Character character = characterService.GetCharacter();
        List<MonsterDatum> allMonsters = await _gameClient.GetMonsters();
        IEnumerable<MonsterDatum> candidates = allMonsters
            .Where( monster => monster.Level <= (long)character.Level + 1 )
            .Where( monster => monster.Drops?.Exists( drop => drop.Code == craftComponent.Code && drop.Rate > 0 ) == true )
            .OrderBy( monster => monster.Drops!.Where( drop => drop.Code == craftComponent.Code && drop.Rate > 0 ).Min(drop => drop.Rate) )
            .ThenBy( monster => monster.Code, StringComparer.Ordinal );

        foreach ( MonsterDatum monster in candidates )
        {
            MapPoint? point = await _mapService.GetByContentCode( new ContentCode( monster.Code ) );
            if ( point != null )
            {
                return new LootPrerequisite
                {
                    MonsterCode = monster.Code,
                    MonsterPoint = point,
                    ItemCode = craftComponent.Code,
                    RequiredQuantity = requiredQuantity
                };
            }
        }

        return null;
    }

}
