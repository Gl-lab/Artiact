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
    private readonly Mock<IMapService> _mapServiceMock;
    private readonly StepBuilder _stepBuilder;

    public StepBuilderTests()
    {
        Mock<IGameClient> gameClientMock = new();
        _mapServiceMock = new Mock<IMapService>();
        _characterServiceMock = new Mock<ICharacterService>();
        _stepBuilder = new StepBuilder( gameClientMock.Object, _mapServiceMock.Object );
    }

    [Fact]
    public async Task BuildCraftingSteps_WithValidCraftTarget_ShouldCreateCorrectSteps()
    {
        // Arrange
        Character character = new() { X = 0, Y = 0 };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( character );

        MapPoint miningWorkshop = new() { X = 1, Y = 1 };
        MapPoint weaponWorkshop = new() { X = 2, Y = 2 };
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "mining" ) ) )
                       .ReturnsAsync( miningWorkshop );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "weaponcrafting" ) ) )
                       .ReturnsAsync( weaponWorkshop );

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
                },
                new()
                {
                    Item = new ItemDatum
                    {
                        Code = "copper_dagger",
                        Craft = new Craft
                        {
                            Skill = "weaponcrafting",
                            Items = new List<Item> { new() { Code = "copper", Quantity = 6 } }
                        }
                    },
                    Quantity = 1,
                    RequiredItems = new List<Item> { new() { Code = "copper", Quantity = 6 } }
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
        Assert.Equal( 4, mixedStep.Steps.Count ); // 1 шаг перемещения + 2 шага крафта

        // Проверяем условный шаг перемещения
        IStep moveStep = mixedStep.Steps[ 0 ];
        Assert.IsType<ConditionalStep>( moveStep );

        // Проверяем шаги крафта
        IStep craftingStep1 = mixedStep.Steps[ 1 ];
        Assert.IsType<ActionStep>( craftingStep1 );

        IStep moveStep2 = mixedStep.Steps[ 2 ];
        Assert.IsType<ConditionalStep>( moveStep2 );

        IStep craftingStep2 = mixedStep.Steps[ 3 ];
        Assert.IsType<ActionStep>( craftingStep2 );
    }

    [Fact]
    public async Task BuildCraftingSteps_WithAlternatingSkills_ExecutesCraftTargetStepsInOrder()
    {
        Character character = new() { X = 0, Y = 0 };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( character );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "skill1" ) ) )
                       .ReturnsAsync( new MapPoint { X = 1, Y = 1 } );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "skill2" ) ) )
                       .ReturnsAsync( new MapPoint { X = 2, Y = 2 } );
        CraftTarget craftTarget = new()
        {
            FinalItem = CraftItem( "c", "skill1" ),
            Steps = new List<CraftStep>
            {
                CraftStepFor( "a", "skill1" ),
                CraftStepFor( "b", "skill2" ),
                CraftStepFor( "c", "skill1" )
            }
        };
        List<string> actions = new();
        Mock<IGameClient> client = new();
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) )
              .Callback<MapPoint>( point => actions.Add( $"move:{point.X},{point.Y}" ) )
              .ReturnsAsync( Response( character ) );
        client.Setup( x => x.Crafting( It.IsAny<Item>() ) )
              .Callback<Item>( item => actions.Add( $"craft:{item.Code}" ) )
              .ReturnsAsync( Response( character ) );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( craftTarget ),
            _characterServiceMock.Object );
        await Assert.IsType<MixedStep>( Assert.Single( Assert.IsType<MixedStep>( result ).Steps ) )
                    .Execute( client.Object );

        Assert.Equal( new[]
        {
            "move:1,1", "craft:a", "move:2,2", "craft:b", "move:1,1", "craft:c"
        }, actions );
    }

    [Fact]
    public async Task BuildCraftingSteps_InitiallyAtLaterWorkshop_ReturnsToItAfterEarlierCraft()
    {
        Character character = new() { X = 2, Y = 2 };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( character );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "skill1" ) ) )
                       .ReturnsAsync( new MapPoint { X = 1, Y = 1 } );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "skill2" ) ) )
                       .ReturnsAsync( new MapPoint { X = 2, Y = 2 } );
        CraftTarget craftTarget = new()
        {
            FinalItem = CraftItem( "b", "skill2" ),
            Steps = new List<CraftStep>
            {
                CraftStepFor( "a", "skill1" ),
                CraftStepFor( "b", "skill2" )
            }
        };
        List<string> actions = new();
        Mock<IGameClient> client = new();
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) )
              .Callback<MapPoint>( point =>
              {
                  actions.Add( $"move:{point.X},{point.Y}" );
                  character.X = point.X;
                  character.Y = point.Y;
              } )
              .ReturnsAsync( () => Response( character ) );
        client.Setup( x => x.Crafting( It.IsAny<Item>() ) )
              .Callback<Item>( item => actions.Add( $"craft:{item.Code}" ) )
              .ReturnsAsync( () => Response( character ) );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( craftTarget ),
            _characterServiceMock.Object );
        await Assert.IsType<MixedStep>( Assert.Single( Assert.IsType<MixedStep>( result ).Steps ) )
                    .Execute( client.Object );

        Assert.Equal( new[]
        {
            "move:1,1", "craft:a", "move:2,2", "craft:b"
        }, actions );
    }

    [Fact]
    public async Task BuildCraftingSteps_WithLootPrerequisite_FightsUntilRequiredQuantityBeforeCrafting()
    {
        Character currentCharacter = new() { X = 3, Y = 3, Inventory = new List<Inventory>() };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( () => currentCharacter );
        _characterServiceMock.Setup( x => x.SaveCharacter( It.IsAny<Character>() ) )
                             .Callback<Character>( character => currentCharacter = character );
        MapPoint monsterPoint = new() { X = 2, Y = 2 };
        MapPoint workshop = new() { X = 3, Y = 3 };
        _mapServiceMock.Setup( x => x.GetByContentCode( It.Is<ContentCode>( code => code.ToString() == "wolf" ) ) )
                       .ReturnsAsync( monsterPoint );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode(
                            It.Is<ContentCode>( code => code.ToString() == "weaponcrafting" ) ) )
                       .ReturnsAsync( workshop );
        ItemDatum sword = new()
        {
            Code = "wolf_sword",
            Craft = new Craft
            {
                Skill = "weaponcrafting",
                Quantity = 1,
                Items = new List<Item> { new() { Code = "wolf_hair", Quantity = 3 } }
            }
        };
        CraftTarget craftTarget = new()
        {
            FinalItem = sword,
            LootPrerequisite = new LootPrerequisite
            {
                MonsterCode = "wolf",
                MonsterPoint = monsterPoint,
                ItemCode = "wolf_hair",
                RequiredQuantity = 3
            },
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = sword,
                    Quantity = 1,
                    RequiredItems = new List<Item> { new() { Code = "wolf_hair", Quantity = 3 } }
                }
            }
        };
        List<string> actions = new();
        Mock<IGameClient> client = new();
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) )
              .Callback<MapPoint>( point => actions.Add( $"move:{point.X},{point.Y}" ) )
              .ReturnsAsync( () => Response( currentCharacter ) );
        Queue<Character> fightResults = new( new[]
        {
            CharacterWithLoot( 1 ),
            CharacterWithLoot( 3 )
        } );
        client.Setup( x => x.Fight() )
              .Callback( () => actions.Add( "fight" ) )
              .ReturnsAsync( () => Response( fightResults.Dequeue() ) );
        client.Setup( x => x.Crafting( It.IsAny<Item>() ) )
              .Callback<Item>( item => actions.Add( $"craft:{item.Code}" ) )
              .ReturnsAsync( () => Response( currentCharacter ) );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( craftTarget ),
            _characterServiceMock.Object );
        MixedStep outerStep = Assert.IsType<MixedStep>( result );
        MixedStep craftingStep = Assert.IsType<MixedStep>( Assert.Single( outerStep.Steps ) );
        await craftingStep.Execute( client.Object );

        Assert.Equal( new[]
        {
            "move:2,2", "fight", "fight", "move:3,3", "craft:wolf_sword"
        }, actions );
    }

    [Fact]
    public async Task BuildCraftingSteps_LootingWithoutInventoryProgress_AbortsAfterTenAttempts()
    {
        Character currentCharacter = new() { X = 2, Y = 2, Inventory = new List<Inventory>() };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( () => currentCharacter );
        _characterServiceMock.Setup( x => x.SaveCharacter( It.IsAny<Character>() ) )
                             .Callback<Character>( character => currentCharacter = character );
        MapPoint monsterPoint = new() { X = 2, Y = 2 };
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode( It.IsAny<ContentCode>() ) )
                       .ReturnsAsync( new MapPoint { X = 3, Y = 3 } );
        Mock<IGameClient> client = new();
        int fightCalls = 0;
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) ).ReturnsAsync( () => Response( currentCharacter ) );
        client.Setup( x => x.Fight() ).ReturnsAsync( () =>
        {
            fightCalls++;
            if ( fightCalls > 10 )
            {
                throw new TimeoutException( "Unbounded fight loop" );
            }

            return Response( currentCharacter );
        } );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( LootCraftTarget( monsterPoint ) ),
            _characterServiceMock.Object );
        MixedStep craftingStep = Assert.IsType<MixedStep>( Assert.Single( Assert.IsType<MixedStep>( result ).Steps ) );

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => craftingStep.Execute( client.Object ) );

        Assert.Contains( "10 attempts", exception.Message );
        client.Verify( x => x.Fight(), Times.Exactly( 10 ) );
        client.Verify( x => x.Crafting( It.IsAny<Item>() ), Times.Never );
    }

    [Fact]
    public async Task BuildCraftingSteps_LootAvailableWhenBuiltButConsumedBeforeExecution_FightsUntilSufficient()
    {
        Character currentCharacter = CharacterWithLoot( 3 );
        currentCharacter.X = 3;
        currentCharacter.Y = 3;
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( () => currentCharacter );
        _characterServiceMock.Setup( x => x.SaveCharacter( It.IsAny<Character>() ) )
                             .Callback<Character>( character => currentCharacter = character );
        MapPoint monsterPoint = new() { X = 2, Y = 2 };
        MapPoint workshop = new() { X = 3, Y = 3 };
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode( It.IsAny<ContentCode>() ) )
                       .ReturnsAsync( workshop );
        CraftTarget craftTarget = LootCraftTarget( monsterPoint );
        List<string> actions = new();
        Mock<IGameClient> client = new();
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) )
              .Callback<MapPoint>( point => actions.Add( $"move:{point.X},{point.Y}" ) )
              .ReturnsAsync( () => Response( currentCharacter ) );
        client.Setup( x => x.Fight() )
              .Callback( () => actions.Add( "fight" ) )
              .ReturnsAsync( () => Response( CharacterWithLoot( 3 ) ) );
        client.Setup( x => x.Crafting( It.IsAny<Item>() ) )
              .Callback<Item>( item => actions.Add( $"craft:{item.Code}" ) )
              .ReturnsAsync( () => Response( currentCharacter ) );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( craftTarget ),
            _characterServiceMock.Object );
        currentCharacter = new Character { X = 3, Y = 3, Inventory = new List<Inventory>() };
        await Assert.IsType<MixedStep>( Assert.Single( Assert.IsType<MixedStep>( result ).Steps ) )
                    .Execute( client.Object );

        Assert.Equal( new[]
        {
            "move:2,2", "fight", "move:3,3", "craft:wolf_sword"
        }, actions );
    }

    [Fact]
    public async Task BuildCraftingSteps_LootNeededAtMonsterPoint_DoesNotMoveToSameCoordinates()
    {
        MapPoint monsterPoint = new() { X = 2, Y = 2 };
        Character currentCharacter = new() { X = 3, Y = 3, Inventory = new List<Inventory>() };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( () => currentCharacter );
        _characterServiceMock.Setup( x => x.SaveCharacter( It.IsAny<Character>() ) )
                             .Callback<Character>( character => currentCharacter = character );
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode( It.IsAny<ContentCode>() ) )
                       .ReturnsAsync( new MapPoint { X = 3, Y = 3 } );
        List<string> actions = new();
        Mock<IGameClient> client = new();
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) )
              .Callback<MapPoint>( point => actions.Add( $"move:{point.X},{point.Y}" ) )
              .ReturnsAsync( () => Response( currentCharacter ) );
        client.Setup( x => x.Fight() ).Callback( () => actions.Add( "fight" ) ).ReturnsAsync( () =>
        {
            Character character = CharacterWithLoot( 3 );
            character.X = monsterPoint.X;
            character.Y = monsterPoint.Y;
            return Response( character );
        } );
        client.Setup( x => x.Crafting( It.IsAny<Item>() ) )
              .Callback<Item>( item => actions.Add( $"craft:{item.Code}" ) )
              .ReturnsAsync( () => Response( currentCharacter ) );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( LootCraftTarget( monsterPoint ) ),
            _characterServiceMock.Object );
        currentCharacter = new Character { X = 2, Y = 2, Inventory = new List<Inventory>() };
        await Assert.IsType<MixedStep>( Assert.Single( Assert.IsType<MixedStep>( result ).Steps ) )
                    .Execute( client.Object );

        Assert.Equal( new[] { "fight", "move:3,3", "craft:wolf_sword" }, actions );
    }

    [Fact]
    public async Task BuildCraftingSteps_LootAcquiredBeforeExecution_PerformsZeroFights()
    {
        Character currentCharacter = new() { X = 3, Y = 3, Inventory = new List<Inventory>() };
        _characterServiceMock.Setup( x => x.GetCharacter() ).Returns( () => currentCharacter );
        _characterServiceMock.Setup( x => x.SaveCharacter( It.IsAny<Character>() ) )
                             .Callback<Character>( character => currentCharacter = character );
        MapPoint workshop = new() { X = 3, Y = 3 };
        _mapServiceMock.Setup( x => x.GetWorkshopBySkillCode( It.IsAny<ContentCode>() ) )
                       .ReturnsAsync( workshop );
        CraftTarget craftTarget = LootCraftTarget( new MapPoint { X = 2, Y = 2 } );
        Mock<IGameClient> client = new();
        client.Setup( x => x.Move( It.IsAny<MapPoint>() ) ).ReturnsAsync( () => Response( currentCharacter ) );
        client.Setup( x => x.Crafting( It.IsAny<Item>() ) ).ReturnsAsync( () => Response( currentCharacter ) );

        IStep result = await _stepBuilder.BuildStep( new GearCraftingGoal( craftTarget ),
            _characterServiceMock.Object );
        currentCharacter = CharacterWithLoot( 3 );
        currentCharacter.X = 3;
        currentCharacter.Y = 3;
        await Assert.IsType<MixedStep>( Assert.Single( Assert.IsType<MixedStep>( result ).Steps ) )
                    .Execute( client.Object );

        client.Verify( x => x.Move( It.IsAny<MapPoint>() ), Times.Never );
        client.Verify( x => x.Fight(), Times.Never );
        client.Verify( x => x.Crafting( It.Is<Item>( item => item.Code == "wolf_sword" ) ), Times.Once );
    }

    private static CraftTarget LootCraftTarget( MapPoint monsterPoint )
    {
        ItemDatum sword = new()
        {
            Code = "wolf_sword",
            Craft = new Craft
            {
                Skill = "weaponcrafting",
                Quantity = 1,
                Items = new List<Item> { new() { Code = "wolf_hair", Quantity = 3 } }
            }
        };
        return new CraftTarget
        {
            FinalItem = sword,
            LootPrerequisite = new LootPrerequisite
            {
                MonsterCode = "wolf",
                MonsterPoint = monsterPoint,
                ItemCode = "wolf_hair",
                RequiredQuantity = 3
            },
            Steps = new List<CraftStep>
            {
                new()
                {
                    Item = sword,
                    Quantity = 1,
                    RequiredItems = new List<Item> { new() { Code = "wolf_hair", Quantity = 3 } }
                }
            }
        };
    }

    private static CraftStep CraftStepFor( string code, string skill )
    {
        return new CraftStep
        {
            Item = CraftItem( code, skill ),
            Quantity = 1,
            RequiredItems = new List<Item>()
        };
    }

    private static ItemDatum CraftItem( string code, string skill )
    {
        return new ItemDatum
        {
            Code = code,
            Craft = new Craft
            {
                Skill = skill,
                Items = new List<Item>()
            }
        };
    }

    private static Character CharacterWithLoot( int quantity )
    {
        return new Character
        {
            Inventory = new List<Inventory> { new() { Code = "wolf_hair", Quantity = quantity } }
        };
    }

    private static ActionResponse Response( Character character )
    {
        return new ActionResponse
        {
            Data = new ActionData
            {
                Character = character,
                Cooldown = new Cooldown { TotalSeconds = 0 }
            }
        };
    }
}