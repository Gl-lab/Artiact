using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;

namespace Artiact.Services;

public class TargetLootingResolver : ITargetLootingResolver
{
    private readonly IGameClient _gameClient;

    public TargetLootingResolver( List<MonsterDatum> allMonsters, IGameClient gameClient )
    {
        _gameClient = gameClient;
    }

    public async Task<bool> CanLooting( ItemDatum informationAboutCraftComponent, ICharacterService characterService )
    {
        if ( informationAboutCraftComponent.Subtype != "mob" )
        {
            return false;
        }

        List<MonsterDatum> allMonsters = await _gameClient.GetMonsters();

        IOrderedEnumerable<MonsterDatum> targetMonsters = allMonsters
                                                         .Where( x => x.Drops.Exists( drop =>
                                                              drop.Code == informationAboutCraftComponent.Code ) )
                                                         .OrderByDescending( x =>
                                                              x.Drops.Find( y =>
                                                                    y.Code == informationAboutCraftComponent.Code )!
                                                               .Rate );
        if ( !targetMonsters.Any() )
        {
            return false;
        }

        Character character = characterService.GetCharacter();
        foreach ( MonsterDatum targetMonster in targetMonsters )
        {
            if ( character.Level + 1 <= targetMonster.Level )
            {
                return true;
            }
        }

        return false;
    }
}