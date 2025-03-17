using Artiact.Client;
using Artiact.Models;
using Artiact.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class WearCraftTargetFinderTests
{
    private readonly Mock<IGameClient> _gameClientMock;
    private readonly WearCraftTargetFinder _finder;

    public WearCraftTargetFinderTests()
    {
        _gameClientMock = new Mock<IGameClient>();
        _finder = new WearCraftTargetFinder(_gameClientMock.Object);
    }

    [Fact]
    public async Task FindTarget_WithCopperOre_ShouldCreateCopperDaggerCraftChain()
    {
        // Arrange
        List<Item> availableItems = new List<Item>
        {
            new() { Code = "copper_ore", Quantity = 92 }
        };

        List<ItemDatum> allItems = new List<ItemDatum>
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

        _gameClientMock.Setup(x => x.GetItems()).ReturnsAsync(allItems);

        // Act
        CraftTarget? result = _finder.FindTarget(availableItems);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("copper_dagger", result.FinalItem.Code);
        Assert.Equal(2, result.Steps.Count);

        // Проверяем первый шаг (создание меди)
        CraftStep copperStep = result.Steps[0];
        Assert.Equal("copper", copperStep.Item.Code);
        Assert.Equal(6, copperStep.Quantity); // Нужно создать 6 меди
        Assert.Single(copperStep.RequiredItems);
        Assert.Equal("copper_ore", copperStep.RequiredItems[0].Code);
        Assert.Equal(60, copperStep.RequiredItems[0].Quantity); // 10 руды * 6 меди

        // Проверяем второй шаг (создание кинжала)
        CraftStep daggerStep = result.Steps[1];
        Assert.Equal("copper_dagger", daggerStep.Item.Code);
        Assert.Equal(1, daggerStep.Quantity); // Нужно создать 1 кинжал
        Assert.Single(daggerStep.RequiredItems);
        Assert.Equal("copper", daggerStep.RequiredItems[0].Code);
        Assert.Equal(6, daggerStep.RequiredItems[0].Quantity);
    }
} 