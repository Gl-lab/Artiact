using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class WearCraftTargetFinderTests
{
    private readonly Mock<ICraftChainBuilder> _chainBuilderMock;
    private readonly WearCraftTargetFinder _finder;
    private readonly Mock<IGameClient> _gameClientMock;
    private readonly Mock<ICraftTargetEvaluator> _targetEvaluatorMock;

    public WearCraftTargetFinderTests()
    {
        _gameClientMock = new Mock<IGameClient>();
        _targetEvaluatorMock = new Mock<ICraftTargetEvaluator>();
        _chainBuilderMock = new Mock<ICraftChainBuilder>();
        _finder = new WearCraftTargetFinder(
            _gameClientMock.Object,
            _targetEvaluatorMock.Object,
            _chainBuilderMock.Object );
    }

    [Fact]
    public async Task FindTargets_WithCopperOre_ShouldCreateCopperDaggerCraftChain()
    {
        // Arrange
        List<Item> availableItems = new()
        {
            new Item { Code = "copper_ore", Quantity = 92 }
        };

        List<ItemDatum> allItems = new()
        {
            new ItemDatum
            {
                Name = "Copper Ore",
                Code = "copper_ore",
                Level = 1,
                Type = "resource",
                Subtype = "mining",
                Description = "",
                Effects = new List<Effect>(),
                Craft = null,
                Tradeable = true
            },
            new ItemDatum
            {
                Name = "Copper",
                Code = "copper",
                Level = 1,
                Type = "resource",
                Subtype = "bar",
                Description = "",
                Effects = new List<Effect>(),
                Craft = new Craft
                {
                    Skill = "mining",
                    Level = 1,
                    Items = new List<Item>
                    {
                        new() { Code = "copper_ore", Quantity = 10 }
                    },
                    Quantity = 1
                },
                Tradeable = true
            },
            new ItemDatum
            {
                Name = "Copper Dagger",
                Code = "copper_dagger",
                Level = 1,
                Type = "weapon",
                Subtype = "",
                Description = "",
                Effects = new List<Effect>
                {
                    new() { Code = "attack_air", Value = 6 },
                    new() { Code = "critical_strike", Value = 35 }
                },
                Craft = new Craft
                {
                    Skill = "weaponcrafting",
                    Level = 1,
                    Items = new List<Item>
                    {
                        new() { Code = "copper", Quantity = 6 }
                    },
                    Quantity = 1
                },
                Tradeable = true
            }
        };

        CraftTarget expectedCraftTarget = new()
        {
            FinalItem = allItems[ 2 ], // copper_dagger
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = allItems[ 1 ], // copper
                    Quantity = 6,
                    RequiredItems = new List<Item>
                    {
                        new() { Code = "copper_ore", Quantity = 60 }
                    }
                },
                new()
                {
                    Item = allItems[ 2 ], // copper_dagger
                    Quantity = 1,
                    RequiredItems = new List<Item>
                    {
                        new() { Code = "copper", Quantity = 6 }
                    }
                }
            }
        };

        _gameClientMock.Setup( x => x.GetItems() ).ReturnsAsync( allItems );
        _chainBuilderMock.Setup( x => x.TryCreateCraftChain(
                              It.Is<ItemDatum>( i => i.Code == "copper_dagger" ),
                              It.IsAny<Dictionary<string, int>>() ) )
                         .ReturnsAsync( expectedCraftTarget );
        _targetEvaluatorMock.Setup( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>() ) )
                            .Returns( expectedCraftTarget );

        // Act
        List<CraftTarget> results = await _finder.FindTargets( availableItems );

        // Assert
        Assert.NotEmpty( results );
        CraftTarget result = results.First();
        Assert.Equal( "copper_dagger", result.FinalItem.Code );
        Assert.Equal( 2, result.Steps.Count );

        // Проверяем первый шаг (создание меди)
        CraftStep copperStep = result.Steps[ 0 ];
        Assert.Equal( "copper", copperStep.Item.Code );
        Assert.Equal( 6, copperStep.Quantity );
        Assert.Single( copperStep.RequiredItems );
        Assert.Equal( "copper_ore", copperStep.RequiredItems[ 0 ].Code );
        Assert.Equal( 60, copperStep.RequiredItems[ 0 ].Quantity );

        // Проверяем второй шаг (создание кинжала)
        CraftStep daggerStep = result.Steps[ 1 ];
        Assert.Equal( "copper_dagger", daggerStep.Item.Code );
        Assert.Equal( 1, daggerStep.Quantity );
        Assert.Single( daggerStep.RequiredItems );
        Assert.Equal( "copper", daggerStep.RequiredItems[ 0 ].Code );
        Assert.Equal( 6, daggerStep.RequiredItems[ 0 ].Quantity );

        // Проверяем, что все моки были вызваны
        _gameClientMock.Verify( x => x.GetItems(), Times.Once );
        _chainBuilderMock.Verify( x => x.TryCreateCraftChain(
            It.Is<ItemDatum>( i => i.Code == "copper_dagger" ),
            It.IsAny<Dictionary<string, int>>() ), Times.Once );
        _targetEvaluatorMock.Verify( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>() ), Times.Once );
    }

    [Fact]
    public async Task FindTargets_WithCopperOreAndCopper_ShouldCreateCopperDaggerCraftChain()
    {
        // Arrange
        List<Item> availableItems = new()
        {
            new Item { Code = "copper_ore", Quantity = 35 },
            new Item { Code = "copper", Quantity = 3 }
        };

        List<ItemDatum> allItems = new()
        {
            new ItemDatum
            {
                Name = "Copper Ore",
                Code = "copper_ore",
                Level = 1,
                Type = "resource",
                Subtype = "mining",
                Description = "",
                Effects = new List<Effect>(),
                Craft = null,
                Tradeable = true
            },
            new ItemDatum
            {
                Name = "Copper",
                Code = "copper",
                Level = 1,
                Type = "resource",
                Subtype = "bar",
                Description = "",
                Effects = new List<Effect>(),
                Craft = new Craft
                {
                    Skill = "mining",
                    Level = 1,
                    Items = new List<Item>
                    {
                        new() { Code = "copper_ore", Quantity = 10 }
                    },
                    Quantity = 1
                },
                Tradeable = true
            },
            new ItemDatum
            {
                Name = "Copper Dagger",
                Code = "copper_dagger",
                Level = 1,
                Type = "weapon",
                Subtype = "",
                Description = "",
                Effects = new List<Effect>
                {
                    new() { Code = "attack_air", Value = 6 },
                    new() { Code = "critical_strike", Value = 35 }
                },
                Craft = new Craft
                {
                    Skill = "weaponcrafting",
                    Level = 1,
                    Items = new List<Item>
                    {
                        new() { Code = "copper", Quantity = 6 }
                    },
                    Quantity = 1
                },
                Tradeable = true
            }
        };

        CraftTarget expectedCraftTarget = new()
        {
            FinalItem = allItems[ 2 ], // copper_dagger
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = allItems[ 1 ], // copper
                    Quantity = 3,
                    RequiredItems = new List<Item>
                    {
                        new() { Code = "copper_ore", Quantity = 30 }
                    }
                },
                new()
                {
                    Item = allItems[ 2 ], // copper_dagger
                    Quantity = 1,
                    RequiredItems = new List<Item>
                    {
                        new() { Code = "copper", Quantity = 6 }
                    }
                }
            }
        };

        _gameClientMock.Setup( x => x.GetItems() ).ReturnsAsync( allItems );
        _chainBuilderMock.Setup( x => x.TryCreateCraftChain(
                              It.Is<ItemDatum>( i => i.Code == "copper_dagger" ),
                              It.IsAny<Dictionary<string, int>>() ) )
                         .ReturnsAsync( expectedCraftTarget );
        _targetEvaluatorMock.Setup( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>() ) )
                            .Returns( expectedCraftTarget );

        // Act
        List<CraftTarget> results = await _finder.FindTargets( availableItems );

        // Assert
        Assert.NotEmpty( results );
        CraftTarget result = results.First();
        Assert.Equal( "copper_dagger", result.FinalItem.Code );
        Assert.Equal( 2, result.Steps.Count );

        // Проверяем первый шаг (создание меди)
        CraftStep copperStep = result.Steps[ 0 ];
        Assert.Equal( "copper", copperStep.Item.Code );
        Assert.Equal( 3, copperStep.Quantity );
        Assert.Single( copperStep.RequiredItems );
        Assert.Equal( "copper_ore", copperStep.RequiredItems[ 0 ].Code );
        Assert.Equal( 30, copperStep.RequiredItems[ 0 ].Quantity );

        // Проверяем второй шаг (создание кинжала)
        CraftStep daggerStep = result.Steps[ 1 ];
        Assert.Equal( "copper_dagger", daggerStep.Item.Code );
        Assert.Equal( 1, daggerStep.Quantity );
        Assert.Single( daggerStep.RequiredItems );
        Assert.Equal( "copper", daggerStep.RequiredItems[ 0 ].Code );
        Assert.Equal( 6, daggerStep.RequiredItems[ 0 ].Quantity );

        // Проверяем, что все моки были вызваны
        _gameClientMock.Verify( x => x.GetItems(), Times.Once );
        _chainBuilderMock.Verify( x => x.TryCreateCraftChain(
            It.Is<ItemDatum>( i => i.Code == "copper_dagger" ),
            It.IsAny<Dictionary<string, int>>() ), Times.Once );
        _targetEvaluatorMock.Verify( x => x.SelectBestTarget( It.IsAny<List<CraftTarget>>() ), Times.Once );
    }
}