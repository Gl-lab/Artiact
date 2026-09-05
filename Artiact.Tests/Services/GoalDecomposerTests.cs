using System.Diagnostics;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artiact.Tests.Services;

public class GoalDecomposerTests
{
    [Fact]
    public async Task DecomposeGoal_WithoutActivityListener_StillDecomposesGatheringGoal()
    {
        Mock<ICharacterService> characterService = new();
        characterService.Setup( x => x.GetCharacter() ).Returns( new Character
        {
            Name = "TestCharacter",
            InventoryMaxItems = 20,
            Inventory = new List<Inventory>()
        } );
        GoalDecomposer decomposer = new(
            Mock.Of<ILogger<GoalDecomposer>>(),
            Mock.Of<IWearCraftTargetFinder>(),
            new ActivitySource( "GoalDecomposerTests.NoListener" ) );

        await decomposer.DecomposeGoal( new GatheringGoal( 20 ), characterService.Object );
    }
}
