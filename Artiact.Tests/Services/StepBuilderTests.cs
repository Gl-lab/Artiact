using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models.Steps;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class StepBuilderTests
{
    private readonly Mock<ICharacterService> _characterServiceMock;
    private readonly Mock<IGameClient> _gameClientMock;
    private readonly Mock<IMapService> _mapServiceMock;
    private readonly StepBuilder _stepBuilder;

    public StepBuilderTests()
    {
        _gameClientMock = new Mock<IGameClient>();
        _mapServiceMock = new Mock<IMapService>();
        _characterServiceMock = new Mock<ICharacterService>();
        _stepBuilder = new StepBuilder( _gameClientMock.Object, _mapServiceMock.Object );
    }

    [Fact]
    public async Task BuildCraftingSteps_WithValidCraftTarget_ShouldCreateCorrectSteps()
    {
        // Arrange
        Character character = new() { X = 0, Y = 0 };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( character );

        MapPoint workshop = new() { X = 1, Y = 1 };
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode( It.IsAny<ContentCode>() ) )
                       .ReturnsAsync( workshop );

        CraftTarget craftTarget = new()
        {
            FinalItem = new ItemDatum
            {
                Code = "copper_dagger",
                Craft = new Craft
                {
                    Skill = "weaponcrafting",
                    Items = new List<Item>
                    {
                        new() { Code = "copper", Quantity = 6 }
                    }
                }
            },
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = new ItemDatum
                    {
                        Code = "copper",
                        Craft = new Craft
                        {
                            Skill = "mining",
                            Items = new List<Item>
                            {
                                new() { Code = "copper_ore", Quantity = 10 }
                            }
                        }
                    },
                    Quantity = 6,
                    RequiredItems = new List<Item>
                    {
                        new() { Code = "copper_ore", Quantity = 60 }
                    }
                }
            }
        };

        GearCraftingGoal goal = new( craftTarget );

        // Act
        IStep result = await _stepBuilder.BuildStep( goal, _characterServiceMock.Object );

        // Assert
        Assert.NotNull( result );
        Assert.IsType<MixedStep>( result );

        MixedStep mixedStep = ( MixedStep )result;
        mixedStep = ( MixedStep )mixedStep.Steps.First();
        Assert.Equal( 3, mixedStep.Steps.Count ); // 1 шаг перемещения + 2 шага крафта

        // Проверяем шаг перемещения
        IStep moveStep = mixedStep.Steps[ 0 ];
        Assert.IsType<MoveStep>( moveStep );
        Assert.Equal( workshop.X, ( ( MoveStep )moveStep ).Point.X );
        Assert.Equal( workshop.Y, ( ( MoveStep )moveStep ).Point.Y );

        // Проверяем шаги крафта
        Assert.IsType<ActionStep>( mixedStep.Steps[ 1 ] );
        Assert.IsType<ActionStep>( mixedStep.Steps[ 2 ] );
    }

    [Fact]
    public async Task BuildCraftingSteps_WithLootTarget_MovesAndFightsBeforeCrafting()
    {
        Character character = new() { X = 4, Y = 5, Inventory = new List<Inventory>() };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( () => character );
        _characterServiceMock.Setup( x => x.SaveCharacter( It.IsAny<Character>() ) )
                             .Callback<Character>( updated => character = updated );
        MapPoint monsterPoint = new() { X = 2, Y = 3 };
        MapPoint workshop = new() { X = 4, Y = 5 };
        _mapServiceMock.Setup( x => x.GetByContentCode( It.Is<ContentCode>( code => code.ToString() == "wolf" ) ) )
                       .ReturnsAsync( monsterPoint );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode( It.IsAny<ContentCode>() ) )
                       .ReturnsAsync( workshop );
        ItemDatum helmet = new()
        {
            Code = "wolf_helmet",
            Craft = new Craft
            {
                Skill = "gearcrafting", Quantity = 1,
                Items = new List<Item> { new() { Code = "wolf_hair", Quantity = 3 } }
            }
        };
        CraftTarget target = new()
        {
            FinalItem = helmet,
            Steps = new List<CraftStep>
            {
                new() { Item = helmet, Quantity = 1, RequiredItems = helmet.Craft.Items }
            },
            LootTargets = new List<LootTarget>
            {
                new()
                {
                    Monster = new MonsterDatum { Code = "wolf" },
                    ItemCode = "wolf_hair",
                    RequiredQuantity = 3
                }
            }
        };

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( target ), _characterServiceMock.Object );

        MixedStep outer = Assert.IsType<MixedStep>( result );
        MixedStep inner = Assert.IsType<MixedStep>( Assert.Single( outer.Steps ) );
        Assert.Equal( 4, inner.Steps.Count );
        MoveStep lootMove = Assert.IsType<MoveStep>( inner.Steps[ 0 ] );
        Assert.Equal( monsterPoint.X, lootMove.Point.X );
        Assert.Equal( monsterPoint.Y, lootMove.Point.Y );
        Assert.IsType<ActionStep>( inner.Steps[ 1 ] );
        MoveStep craftMove = Assert.IsType<MoveStep>( inner.Steps[ 2 ] );
        Assert.Equal( workshop.X, craftMove.Point.X );
        Assert.Equal( workshop.Y, craftMove.Point.Y );
        Assert.IsType<ActionStep>( inner.Steps[ 3 ] );

        Character atMonster = new() { X = 2, Y = 3, Inventory = new List<Inventory>() };
        Character afterFirstFight = new() { X = 2, Y = 3, Inventory = new List<Inventory>() };
        Character afterSecondFight = new()
        {
            X = 2, Y = 3,
            Inventory = new List<Inventory> { new() { Code = "wolf_hair", Quantity = 3 } }
        };
        _gameClientMock.Setup( x => x.Move( monsterPoint ) ).ReturnsAsync( Response( atMonster ) );
        _gameClientMock.SetupSequence( x => x.Fight() )
                       .ReturnsAsync( Response( afterFirstFight ) )
                       .ReturnsAsync( Response( afterSecondFight ) )
                       .ReturnsAsync( Response( afterSecondFight ) );

        await inner.Steps[ 0 ].Execute( _gameClientMock.Object );
        await inner.Steps[ 1 ].Execute( _gameClientMock.Object );

        _gameClientMock.Verify( x => x.Fight(), Times.Exactly( 2 ) );

        _gameClientMock.Invocations.Clear();
        await inner.Steps[ 1 ].Execute( _gameClientMock.Object );
        _gameClientMock.Verify( x => x.Fight(), Times.Never );
    }

    private static ActionResponse Response( Character character ) => new()
    {
        Data = new ActionData { Character = character, Cooldown = new Cooldown { TotalSeconds = 0 } }
    };
}