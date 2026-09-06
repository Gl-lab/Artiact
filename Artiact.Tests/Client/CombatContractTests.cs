using System.Net;
using Artiact.Client;
using Artiact.Contracts.Client;
using Moq;
using Artiact.Contracts.Models.Api;
using System.Text.Json;

namespace Artiact.Tests.Client;

public class CombatContractTests
{
    [Fact]
    public async Task FightSelectsControlledParticipantInsteadOfFirstCharacter()
    {
        var client = FromJson("""
            {"data":{"cooldown":{"total_seconds":8},
             "fight":{"result":"win","turns":2,"opponent":"dummy","logs":[],"characters":[]},
             "characters":[{"name":"other","hp":99},{"name":"test","hp":14}]}}
            """);
        var response = await client.Fight();
        Assert.Equal("test", response.Data.Character.Name);
        Assert.Equal(14, response.Data.Character.Hp);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"name\":\"Test\"}]")]
    [InlineData("[{\"name\":\"test\"},{\"name\":\"test\"}]")]
    public async Task MissingOrDuplicateIdentityIsUnknown(string participants)
    {
        var client = FromJson("{\"data\":{\"cooldown\":{},\"fight\":{\"result\":\"win\",\"turns\":2,\"opponent\":\"dummy\",\"logs\":[],\"characters\":[]},\"characters\":" + participants + "}}");
        var error = await Assert.ThrowsAsync<ActionFailureException>(() => client.Fight());
        Assert.Equal(ActionFailureKind.UnknownOutcome, error.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EquipmentUsesNamedSlotArrayAndPreservesTransaction(bool unequip)
    {
        string? body = null;
        var http = new Mock<IGameHttpClient>();
        http.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()))
            .Returns(async (string path, HttpContent? content) =>
            {
                Assert.EndsWith(unequip ? "/unequip" : "/equip", path);
                body = await content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""
                    {"data":{"cooldown":{},"character":{"name":"test"},"items":[{"code":"blade","slot":"weapon","quantity":1}]}}
                    """) };
            });
        var client = SingleDispatchTests.Client(http.Object);
        var response = unequip ? await client.UnequipItem(new UnequipRequest("weapon"))
            : await client.EquipItem(new EquipRequest("blade", "weapon"));
        using var parsed = JsonDocument.Parse(body!);
        var item = Assert.Single(parsed.RootElement.EnumerateArray());
        Assert.Equal("weapon", item.GetProperty("slot").GetString());
        Assert.Equal(1, item.GetProperty("quantity").GetInt32());
        Assert.Equal(!unequip, item.TryGetProperty("code", out _));
        Assert.Equal("blade", response.Data.EquipmentItems!.Value[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task RestPreservesRestoredHp()
    {
        var response = await FromJson("""
            {"data":{"cooldown":{},"character":{"name":"test","hp":20},"hp_restored":6}}
            """).Rest();
        Assert.Equal(6, response.Data.HpRestored);
        Assert.Equal(20, response.Data.Character.Hp);
    }

    private static GameClient FromJson(string json)
    {
        var http = new Mock<IGameHttpClient>();
        http.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        return SingleDispatchTests.Client(http.Object);
    }
}
