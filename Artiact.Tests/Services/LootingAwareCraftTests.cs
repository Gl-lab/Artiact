using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class LootingAwareCraftTests
{
    [Fact]
    public async Task FindTargets_WhenMobDropIsMissing_PlansLootBeforeCraft()
    {
        List<ItemDatum> items = new()
        {
            new() { Code = "wolf_hair", Type = "resource", Subtype = "mob" },
            new()
            {
                Code = "wolf_helmet", Type = "helmet", Subtype = "",
                Craft = new Craft
                {
                    Skill = "gearcrafting", Quantity = 1,
                    Items = new List<Item> { new() { Code = "wolf_hair", Quantity = 3 } }
                }
            }
        };
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetItems() ).ReturnsAsync( items );
        CraftChainBuilder chainBuilder = new( gameClient.Object );
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character { Level = 4 } );
        LootTarget lootTarget = new()
        {
            Monster = new MonsterDatum { Code = "wolf", Level = 4 },
            ItemCode = "wolf_hair",
            RequiredQuantity = 3
        };
        Mock<ITargetLootingResolver> resolver = new();
        resolver.Setup( x => x.FindTarget( items[ 0 ], 3, characterService.Object ) )
                .ReturnsAsync( lootTarget );
        Mock<ICraftTargetEvaluator> evaluator = new();
        evaluator.Setup( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>() ) )
                 .Returns( ( List<CraftTarget> targets ) => targets.Single() );
        WearCraftTargetFinder finder = new( gameClient.Object, evaluator.Object, chainBuilder, resolver.Object );

        List<CraftTarget> result = await finder.FindTargets(
            new List<Item>
            {
                new() { Code = "wolf_hair", Quantity = 1 }
            }, characterService.Object );

        CraftTarget target = Assert.Single( result );
        LootTarget plannedLoot = Assert.Single( target.LootTargets );
        Assert.Equal( "wolf", plannedLoot.Monster.Code );
        Assert.Equal( "wolf_hair", plannedLoot.ItemCode );
        Assert.Equal( 3, plannedLoot.RequiredQuantity );
        Assert.Equal( "wolf_helmet", target.FinalItem.Code );
    }
}
