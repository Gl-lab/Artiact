using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Models.Steps;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class FightDefeatTests
{
    [Fact]
    public async Task DefeatRetainsReturnedCharacterAndDoesNotRepeat()
    {
        var state = new CharacterService();
        var returned = new Character { Name = "test", Hp = 5, X = 7, Y = 8 };
        var client = new Mock<IGameClient>();
        client.Setup(x => x.Fight()).ReturnsAsync(new ActionResponse { Data = new ActionData
        {
            Character = returned, Cooldown = new Cooldown { TotalSeconds = 0 },
            Fight = new FightDetails { Result = "loss", Turns = 2, Opponent = "dummy", Logs = [],
                Characters = System.Text.Json.JsonSerializer.SerializeToElement(Array.Empty<object>()) }
        } });
        var step = new ActionStep(state, x => x.Fight(), _ => true, maxAttempts: 2);
        var error = await Assert.ThrowsAsync<ActionFailureException>(() => step.Execute(client.Object, CancellationToken.None));
        Assert.Equal(ActionFailureKind.Defeat, error.Kind);
        Assert.Same(returned, state.GetCharacter());
        client.Verify(x => x.Fight(), Times.Once);
    }
}
