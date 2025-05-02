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
        Assert.Equal( 4, mixedStep.Steps.Count ); // 1 шаг перемещения + 2 шага крафта

        // Проверяем шаг перемещения
        IStep moveStep = mixedStep.Steps[ 0 ];
        Assert.IsType<MoveStep>( moveStep );
        Assert.Equal( workshop.X, ( ( MoveStep )moveStep ).Point.X );
        Assert.Equal( workshop.Y, ( ( MoveStep )moveStep ).Point.Y );

        // Проверяем шаги крафта
        IStep craftingStep1 = mixedStep.Steps[ 1 ];
        Assert.IsType<ActionStep>( craftingStep1 );

        IStep moveStep2 = mixedStep.Steps[ 2 ];
        Assert.IsType<MoveStep>( moveStep );
        Assert.Equal( workshop.X, ( ( MoveStep )moveStep2 ).Point.X );
        Assert.Equal( workshop.Y, ( ( MoveStep )moveStep2 ).Point.Y );

        IStep craftingStep2 = mixedStep.Steps[ 3 ];
        Assert.IsType<ActionStep>( craftingStep2 );
    }
}