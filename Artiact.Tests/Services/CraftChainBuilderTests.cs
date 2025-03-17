using Artiact.Models.Api;
using Artiact.Services;

namespace Artiact.Tests.Services;

public class CraftChainBuilderTests
{
    private readonly List<ItemDatum> _allItems;
    private readonly CraftChainBuilder _builder;

    public CraftChainBuilderTests()
    {
        _allItems = new List<ItemDatum>
        {
            new()
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
            new()
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
            new()
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

        _builder = new CraftChainBuilder( _allItems );
    }

    [Fact]
    public void TryCreateCraftChain_WithEnoughResources_ShouldCreateValidChain()
    {
        // Arrange
        ItemDatum targetItem = _allItems[ 2 ]; // copper_dagger
        Dictionary<string, int> availableResources = new Dictionary<string, int>
        {
            { "copper_ore", 100 } // Достаточно для создания меди и кинжала
        };

        // Act
        CraftTarget? result = _builder.TryCreateCraftChain( targetItem, availableResources );

        // Assert
        Assert.NotNull( result );
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
    }

    [Fact]
    public void TryCreateCraftChain_WithNotEnoughResources_ShouldReturnNull()
    {
        // Arrange
        ItemDatum targetItem = _allItems[ 2 ]; // copper_dagger
        Dictionary<string, int> availableResources = new Dictionary<string, int>
        {
            { "copper_ore", 50 } // Недостаточно для создания меди и кинжала
        };

        // Act
        CraftTarget? result = _builder.TryCreateCraftChain( targetItem, availableResources );

        // Assert
        Assert.Null( result );
    }

    [Fact]
    public void TryCreateCraftChain_WithCircularDependency_ShouldReturnNull()
    {
        // Arrange
        ItemDatum circularItem = new ItemDatum
        {
            Name = "Circular Item",
            Code = "circular_item",
            Level = 1,
            Type = "weapon",
            Craft = new Craft
            {
                Items = new List<Item>
                {
                    new() { Code = "circular_item", Quantity = 1 }
                }
            }
        };

        Dictionary<string, int> availableResources = new Dictionary<string, int>();
        CraftChainBuilder builder = new CraftChainBuilder( new List<ItemDatum> { circularItem } );

        // Act
        CraftTarget? result = builder.TryCreateCraftChain( circularItem, availableResources );

        // Assert
        Assert.Null( result );
    }
}