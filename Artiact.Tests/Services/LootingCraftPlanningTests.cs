using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class LootingCraftPlanningTests
{
    [Fact]
    public async Task FindTargets_MissingMobDrop_CarriesLootPrerequisiteIntoCraftTarget()
    {
        ItemDatum wolfHair = new() { Code = "wolf_hair", Type = "resource", Subtype = "mob" };
        ItemDatum wolfSword = new()
        {
            Code = "wolf_sword",
            Type = "weapon",
            Craft = new Craft
            {
                Skill = "weaponcrafting",
                Quantity = 1,
                Items = new List<Item> { new() { Code = wolfHair.Code, Quantity = 3 } }
            }
        };
        LootPrerequisite wolfLoot = new()
        {
            MonsterCode = "wolf",
            MonsterPoint = new MapPoint { X = 2, Y = 2 },
            ItemCode = wolfHair.Code,
            RequiredQuantity = 3
        };
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetItems() ).ReturnsAsync( new List<ItemDatum> { wolfHair, wolfSword } );
        Mock<ICraftChainBuilder> chainBuilder = new();
        CraftTarget chain = new()
        {
            FinalItem = wolfSword,
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = wolfSword,
                    Quantity = 1,
                    RequiredItems = new List<Item> { new() { Code = wolfHair.Code, Quantity = 3 } }
                }
            }
        };
        chainBuilder.Setup( x => x.TryCreateCraftChain(
                              wolfSword,
                              It.Is<Dictionary<string, int>>( resources => resources.GetValueOrDefault( wolfHair.Code ) == 3 ) ) )
                    .ReturnsAsync( chain );
        Mock<ITargetLootingResolver> resolver = new();
        Mock<ICharacterService> characterService = new();
        resolver.Setup( x => x.Resolve( wolfHair, 3, characterService.Object ) ).ReturnsAsync( wolfLoot );
        Mock<ICraftTargetEvaluator> evaluator = new();
        evaluator.Setup( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>(), characterService.Object ) )
                 .Returns( chain );
        WearCraftTargetFinder finder = new(
            gameClient.Object,
            evaluator.Object,
            chainBuilder.Object,
            resolver.Object );

        List<CraftTarget> results = await finder.FindTargets( new List<Item>(), characterService.Object );

        CraftTarget result = Assert.Single( results );
        Assert.NotNull( result.LootPrerequisite );
        Assert.Equal( "wolf", result.LootPrerequisite.MonsterCode );
        Assert.Equal( wolfHair.Code, result.LootPrerequisite.ItemCode );
        Assert.Equal( 3, result.LootPrerequisite.RequiredQuantity );
        chainBuilder.VerifyAll();
    }

    [Fact]
    public async Task FindTargets_LootTargetConsumesInventory_ContinuesPlanningUntilInventoryIsConsumed()
    {
        ItemDatum wolfHair = new() { Code = "wolf_hair", Type = "resource", Subtype = "mob" };
        ItemDatum copper = new() { Code = "copper", Type = "resource", Subtype = "bar" };
        ItemDatum wolfSword = new()
        {
            Code = "wolf_sword",
            Type = "weapon",
            Craft = new Craft
            {
                Skill = "weaponcrafting",
                Quantity = 1,
                Items = new List<Item>
                {
                    new() { Code = wolfHair.Code, Quantity = 3 },
                    new() { Code = copper.Code, Quantity = 1 }
                }
            }
        };
        CraftTarget chain = new()
        {
            FinalItem = wolfSword,
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = wolfSword,
                    Quantity = 1,
                    RequiredItems = wolfSword.Craft.Items
                }
            }
        };
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetItems() )
                  .ReturnsAsync( new List<ItemDatum> { wolfHair, copper, wolfSword } );
        Mock<ICraftChainBuilder> chainBuilder = new();
        chainBuilder.Setup( x => x.TryCreateCraftChain(
                              wolfSword,
                              It.Is<Dictionary<string, int>>( resources =>
                                  resources.GetValueOrDefault( wolfHair.Code ) == 3 &&
                                  resources.GetValueOrDefault( copper.Code ) >= 1 ) ) )
                    .ReturnsAsync( chain );
        Mock<ICharacterService> characterService = new();
        Mock<ITargetLootingResolver> resolver = new();
        resolver.Setup( x => x.Resolve( wolfHair, 3, characterService.Object ) )
                .ReturnsAsync( new LootPrerequisite
                {
                    MonsterCode = "wolf",
                    MonsterPoint = new MapPoint(),
                    ItemCode = wolfHair.Code,
                    RequiredQuantity = 3
                } );
        Mock<ICraftTargetEvaluator> evaluator = new();
        evaluator.Setup( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>(), characterService.Object ) )
                 .Returns( chain );
        WearCraftTargetFinder finder = new(
            gameClient.Object,
            evaluator.Object,
            chainBuilder.Object,
            resolver.Object );

        List<CraftTarget> results = await finder.FindTargets(
            new List<Item> { new() { Code = copper.Code, Quantity = 2 } },
            characterService.Object );

        Assert.Equal( 2, results.Count );
    }

    [Fact]
    public async Task FindTargets_NestedCraftMissingMobLeaf_UsesRealCraftChainWithRequiredQuantity()
    {
        ItemDatum wolfHair = new() { Code = "wolf_hair", Type = "resource", Subtype = "mob" };
        ItemDatum hilt = Craftable( "wolf_hilt", "componentcrafting", 2,
            new Item { Code = wolfHair.Code, Quantity = 3 } );
        ItemDatum sword = Craftable( "wolf_sword", "weaponcrafting", 1,
            new Item { Code = hilt.Code, Quantity = 3 } );
        sword.Type = "weapon";
        List<ItemDatum> items = new() { wolfHair, hilt, sword };
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetItems() ).ReturnsAsync( items );
        CraftChainBuilder chainBuilder = new( gameClient.Object );
        Mock<ITargetLootingResolver> resolver = new();
        Mock<ICharacterService> characterService = new();
        resolver.Setup( x => x.Resolve( wolfHair, 6, characterService.Object ) )
                .ReturnsAsync( new LootPrerequisite
                {
                    MonsterCode = "wolf",
                    MonsterPoint = new MapPoint { X = 2, Y = 2 },
                    ItemCode = wolfHair.Code,
                    RequiredQuantity = 6
                } );
        Mock<ICraftTargetEvaluator> evaluator = new();
        evaluator.Setup( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>(), characterService.Object ) )
                 .Returns( ( List<CraftTarget> targets, ICharacterService _ ) => targets.First() );
        WearCraftTargetFinder finder = new(
            gameClient.Object,
            evaluator.Object,
            chainBuilder,
            resolver.Object );

        List<CraftTarget> results = await finder.FindTargets( new List<Item>(), characterService.Object );

        CraftTarget result = Assert.Single( results );
        Assert.NotNull( result.LootPrerequisite );
        Assert.Equal( wolfHair.Code, result.LootPrerequisite.ItemCode );
        Assert.Equal( 6, result.LootPrerequisite.RequiredQuantity );
        Assert.Equal( new[] { hilt.Code, sword.Code }, result.Steps.Select( step => step.Item.Code ) );
        Assert.Equal( 2, result.Steps[ 0 ].Quantity );
    }

    [Fact]
    public async Task FindTargets_NestedCraftWithTwoDistinctMissingMobLeaves_FailsClosed()
    {
        ItemDatum wolfHair = new() { Code = "wolf_hair", Type = "resource", Subtype = "mob" };
        ItemDatum goblinTooth = new() { Code = "goblin_tooth", Type = "resource", Subtype = "mob" };
        ItemDatum core = Craftable( "mixed_core", "componentcrafting", 1,
            new Item { Code = wolfHair.Code, Quantity = 2 },
            new Item { Code = goblinTooth.Code, Quantity = 1 } );
        ItemDatum sword = Craftable( "mixed_sword", "weaponcrafting", 1,
            new Item { Code = core.Code, Quantity = 1 } );
        sword.Type = "weapon";
        List<ItemDatum> items = new() { wolfHair, goblinTooth, core, sword };
        Mock<IGameClient> gameClient = new();
        gameClient.Setup( x => x.GetItems() ).ReturnsAsync( items );
        Mock<ITargetLootingResolver> resolver = new();
        Mock<ICharacterService> characterService = new();
        Mock<ICraftTargetEvaluator> evaluator = new();
        WearCraftTargetFinder finder = new(
            gameClient.Object,
            evaluator.Object,
            new CraftChainBuilder( gameClient.Object ),
            resolver.Object );

        List<CraftTarget> results = await finder.FindTargets( new List<Item>(), characterService.Object );

        Assert.Empty( results );
        resolver.Verify( x => x.Resolve( It.IsAny<ItemDatum>(), It.IsAny<int>(),
            It.IsAny<ICharacterService>() ), Times.Never );
    }

    private static ItemDatum Craftable( string code, string skill, int outputQuantity, params Item[] items )
    {
        return new ItemDatum
        {
            Code = code,
            Type = "resource",
            Craft = new Craft
            {
                Skill = skill,
                Quantity = outputQuantity,
                Items = items.ToList()
            }
        };
    }
}
