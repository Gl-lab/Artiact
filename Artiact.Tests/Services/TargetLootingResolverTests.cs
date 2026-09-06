using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class TargetLootingResolverTests
{
    [Fact]
    public async Task Resolve_RejectsNonpositiveRatesAndUsesStableCodeTieBreak()
    {
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetMonsters()).ReturnsAsync([Monster("zero",1,0), Monster("negative",1,-1), Monster("b",1,5), Monster("a",1,5)]);
        var maps = new Mock<IMapService>();
        maps.Setup(x => x.GetByContentCode(It.IsAny<ContentCode>())).ReturnsAsync(new MapPoint { X = 1, Y = 1 });
        var state = new CharacterService(); state.SaveCharacter(new Character { Level = 1 });
        var result = await new TargetLootingResolver(client.Object, maps.Object).Resolve(MobItem(), 1, state);
        Assert.Equal("a", result!.MonsterCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Resolve_NonpositiveRequestDoesNotLoadMonsters(int quantity)
    {
        var client = new Mock<IGameClient>(MockBehavior.Strict);
        Assert.Null(await new TargetLootingResolver(client.Object, Mock.Of<IMapService>()).Resolve(MobItem(), quantity, Mock.Of<ICharacterService>()));
        client.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task Resolve_ChoosesLowestReciprocalRateEligibleMonster()
    {
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetMonsters() ).ReturnsAsync( new List<MonsterDatum>
        {
            Monster( "eligible_low_rate", 5, 10 ),
            Monster( "too_strong", 7, 50 ),
            Monster( "eligible_high_rate", 6, 30 )
        } );
        Mock<IMapService> mapService = new();
        mapService.Setup( x => x.GetByContentCode( It.IsAny<ContentCode>() ) )
                  .ReturnsAsync( new MapPoint { X = 1, Y = 2 } );
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character { Level = 5 } );
        TargetLootingResolver resolver = new( gameClient.Object, mapService.Object );

        LootPrerequisite? result = await resolver.Resolve( MobItem(), 1, characterService.Object );

        Assert.NotNull( result );
        Assert.Equal( "eligible_low_rate", result.MonsterCode );
    }

    [Fact]
    public async Task Resolve_MoreFrequentMonsterWithoutMapPoint_ChoosesReachableMonster()
    {
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetMonsters() ).ReturnsAsync( new List<MonsterDatum>
        {
            Monster( "unreachable_high_rate", 5, 5 ),
            Monster( "reachable_low_rate", 5, 10 )
        } );
        Mock<IMapService> mapService = new();
        mapService.Setup( x => x.GetByContentCode( new ContentCode( "unreachable_high_rate" ) ) )
                  .ReturnsAsync( ( MapPoint? )null );
        mapService.Setup( x => x.GetByContentCode( new ContentCode( "reachable_low_rate" ) )
                  ).ReturnsAsync( new MapPoint { X = 4, Y = 7 } );
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character { Level = 5 } );
        TargetLootingResolver resolver = new( gameClient.Object, mapService.Object );

        LootPrerequisite? result = await resolver.Resolve( MobItem(), 3, characterService.Object );

        Assert.NotNull( result );
        Assert.Equal( "reachable_low_rate", result.MonsterCode );
        Assert.Equal( 4, result.MonsterPoint.X );
        Assert.Equal( 7, result.MonsterPoint.Y );
        Assert.Equal( "wolf_hair", result.ItemCode );
        Assert.Equal( 3, result.RequiredQuantity );
    }

    [Fact]
    public async Task Resolve_NonMobItem_ReturnsNullWithoutLoadingMonsters()
    {
        Mock<IGameClient> gameClient = new();
        Mock<IMapService> mapService = new();
        Mock<ICharacterService> characterService = new();
        TargetLootingResolver resolver = new( gameClient.Object, mapService.Object );

        LootPrerequisite? result = await resolver.Resolve(
            new ItemDatum { Code = "copper_ore", Subtype = "mining" },
            1,
            characterService.Object );

        Assert.Null( result );
        gameClient.Verify( x => x.GetMonsters(), Times.Never );
    }

    [Fact]
    public async Task Resolve_OnlyMonsterAboveLevelLimit_ReturnsNull()
    {
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetMonsters() ).ReturnsAsync( new List<MonsterDatum>
        {
            Monster( "too_strong", 7, 50 )
        } );
        Mock<IMapService> mapService = new();
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character { Level = 5 } );
        TargetLootingResolver resolver = new( gameClient.Object, mapService.Object );

        LootPrerequisite? result = await resolver.Resolve( MobItem(), 1, characterService.Object );

        Assert.Null( result );
    }

    private static ItemDatum MobItem()
    {
        return new ItemDatum { Code = "wolf_hair", Subtype = "mob" };
    }

    private static MonsterDatum Monster( string code, int level, int rate )
    {
        return new MonsterDatum
        {
            Code = code,
            Level = level,
            Drops = new List<Drop> { new() { Code = "wolf_hair", Rate = rate } }
        };
    }
}
