using Moq;
using Artiact.Services;
using Artiact.Models;
using Microsoft.Extensions.Options;

namespace Artiact.Tests.Services;

internal static class TestMining
{
    public static MiningRunState State(int cycles = 100, int noProgress = 3) =>
        new(Options.Create(new MiningProgressionSettings { MaxCycles = cycles, MaxConsecutiveNoProgress = noProgress }));
    public static void Catalog(Moq.Mock<Artiact.Contracts.Client.IGameClient> client, int level = 19, int x = 0)
    {
        client.Setup(c => c.GetResources()).ReturnsAsync(new List<Artiact.Contracts.Models.Api.ResourceDatum>
            { new() { Code = "best", Skill = "mining", Level = level } });
        client.Setup(c => c.GetMap()).ReturnsAsync(new List<Artiact.Contracts.Models.Api.MapPlace>
            { new() { X = x, Content = new() { Type = "resource", Code = "best" } } });
    }
    public static IMiningCooldownDelay Delay() => new MiningCooldownDelay();
}
