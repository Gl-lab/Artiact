using System.Diagnostics;
using Artiact.Contracts.Models;
using Artiact.Models;
using Artiact.Models.Steps;
using Artiact.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace Artiact.Tests.Services;

public class MiningProgressionTests
{
    [Fact]
    public async Task InvalidXpBlocksBeforeExecution()
    {
        var client = MiningStepTests.Client();
        CharacterService character = new();
        character.SaveCharacter(MiningStepTests.Snapshot(xp: -1));
        Mock<IStepBuilder> builder = new();
        builder.Setup(x => x.BuildStep(It.IsAny<Goal>(), character)).ReturnsAsync(Mock.Of<IStep>());
        var action = new ActionService(TestMining.State(), client.Object,
            new GoalService(Options.Create(new GoalSelectionSettings { MiningTargetLevel = 3 })),
            builder.Object, Mock.Of<IGoalDecomposer>(), character, new ActivitySource("MiningProgressionTests"));
        Assert.Equal(GoalDecisionReason.InvalidMiningProgress, (await action.ExecuteCycleAsync(default)).Reason);
    }
}
