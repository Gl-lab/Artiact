using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class MiningStepTests
{
    [Fact]
    public async Task OneSelectedCycleGathersAtMostOnce()
    {
        CharacterService character = new();
        character.SaveCharacter(Snapshot());
        Mock<IGameClient> client = Client();
        int gathers = 0;
        client.Setup(x => x.Gathering()).ReturnsAsync(() => Response(Snapshot(++gathers == 1 ? 1 : 3, 6)));
        Mock<IMapService> map = new();
        map.Setup(x => x.GetByContentCode(It.IsAny<ContentCode>())).ReturnsAsync(new MapPoint { X = 2 });
        var step = await new StepBuilder(TestMining.State(), TestMining.Delay(), client.Object, map.Object).BuildStep(new GatheringGoal(3), character);
        await step.Execute(client.Object, CancellationToken.None);
        Assert.Equal(1, gathers);
    }

    internal static Character Snapshot(int level = 1, int xp = 0, int x = 2) => new()
    {
        MiningLevel = level, MiningXp = xp, MiningMaxXp = 10,
        X = x, InventoryMaxItems = 20, Inventory = []
    };
    internal static ActionResponse Response(Character character, int seconds = 0) => new()
    {
        Data = new() { Character = character, Cooldown = new() { TotalSeconds = seconds } }
    };
    internal static Mock<IGameClient> Client()
    {
        Mock<IGameClient> client = new(MockBehavior.Strict);
        client.Setup(x => x.GetResources()).ReturnsAsync(new List<ResourceDatum> { new() { Code = "copper", Level = 1, Skill = "mining" } });
        client.Setup(x => x.GetMap()).ReturnsAsync(new List<MapPlace> { new() { X = 2, Content = new() { Type = "resource", Code = "copper" } } });
        return client;
    }
}
