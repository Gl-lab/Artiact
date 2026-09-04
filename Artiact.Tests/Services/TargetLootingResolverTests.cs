using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class TargetLootingResolverTests
{
    [Fact]
    public async Task FindTarget_SelectsHighestRateEligibleMonster()
    {
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetMonsters() ).ReturnsAsync( new List<MonsterDatum>
        {
            Monster( "slow_wolf", 4, "wolf_hair", 2 ),
            Monster( "fast_wolf", 5, "wolf_hair", 8 ),
            Monster( "too_strong", 7, "wolf_hair", 10 )
        } );
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character { Level = 4 } );
        TargetLootingResolver resolver = new( gameClient.Object );

        var result = await resolver.FindTarget( MobItem( "wolf_hair" ), 3, characterService.Object );

        Assert.NotNull( result );
        Assert.Equal( "fast_wolf", result.Monster.Code );
        Assert.Equal( "wolf_hair", result.ItemCode );
        Assert.Equal( 3, result.RequiredQuantity );
    }

    [Fact]
    public async Task FindTarget_RejectsNonMobItemsAndMonstersAboveLevelTolerance()
    {
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetMonsters() ).ReturnsAsync( new List<MonsterDatum>
        {
            Monster( "dragon", 7, "scale", 10 )
        } );
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character { Level = 4 } );
        TargetLootingResolver resolver = new( gameClient.Object );

        Assert.Null( await resolver.FindTarget( MobItem( "scale" ), 1, characterService.Object ) );
        Assert.Null( await resolver.FindTarget( new ItemDatum { Code = "ore", Subtype = "mining" }, 1,
            characterService.Object ) );
    }

    private static ItemDatum MobItem( string code ) => new() { Code = code, Subtype = "mob" };

    private static MonsterDatum Monster( string code, int level, string itemCode, int rate ) => new()
    {
        Code = code,
        Level = level,
        Drops = new List<Drop> { new() { Code = itemCode, Rate = rate } },
        Effects = new List<Effect>()
    };
}
