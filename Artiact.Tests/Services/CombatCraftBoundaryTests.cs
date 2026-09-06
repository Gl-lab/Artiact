using System.Collections.Immutable;
using Artiact.Services.Combat;
using Moq;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using System.Net;

namespace Artiact.Tests.Services;

public class CombatCraftBoundaryTests
{
    [Theory]
    [InlineData(0, CombatReason.InvalidTarget)]
    [InlineData(1, CombatReason.TargetReached)]
    public async Task TerminalCraftTargetDoesNotReadCatalogs(int target, CombatReason expected)
    {
        var http = new Mock<IGameHttpClient>(MockBehavior.Strict);
        http.Setup(x => x.GetAsync("/characters/test")).ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"data\":" + CombatObservationTests.CharacterJson + "}") });
        var client = Artiact.Tests.Client.SingleDispatchTests.Client(http.Object);
        var factory = new CombatSessionFactory(client, new CombatCatalog(http.Object), new CharacterService(), new CombatRunTests.NoDelay());
        var run = await factory.CreateCraftingAsync(new(target), "dummy", "crafted", new());
        Assert.Equal(expected, (await run.ExecuteCycleAsync()).Reason);
        http.Verify(x => x.GetAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ZeroYieldNestedRecipeFailsBeforeLootPlanning()
    {
        var leaf = new ItemDatum { Code = "feather", Subtype = "mob" };
        var intermediate = new ItemDatum { Code = "part", Craft = new Craft { Quantity = 0, Items = [new() { Code = "feather", Quantity = 1 }] } };
        var target = new ItemDatum { Code = "crafted", Type = "weapon", Craft = new Craft { Quantity = 1, Items = [new() { Code = "part", Quantity = 1 }] } };
        var client = new Mock<IGameClient>(); client.Setup(x => x.GetItems()).ReturnsAsync([leaf, intermediate, target]);
        var resolver = new Mock<ITargetLootingResolver>(MockBehavior.Strict);
        var finder = new WearCraftTargetFinder(client.Object, new CraftTargetEvaluator(), new CraftChainBuilder(client.Object), resolver.Object);
        Assert.Null(await finder.FindTargetAsync("crafted", [], new CharacterService()));
        resolver.VerifyNoOtherCalls();
    }
    [Fact]
    public async Task CraftThatFreesSpaceCanExecuteWithFullInventory()
    {
        var state = CombatRunTests.Initial() with { MapId = 3, Capacity = 2,
            Inventory = ImmutableDictionary<string, int>.Empty.Add("feather", 2) };
        var command = new CombatCraftCommand("crafted", 1, 1, ImmutableDictionary<string, int>.Empty.Add("feather", 2), 3, "overworld");
        var plan = new CombatCraftPlan([command], "feather", 2, new("crafted", new(20, 20)));
        var port = new Mock<ICombatActionPort>();
        port.Setup(x => x.DispatchAsync(CombatCommand.Craft, It.IsAny<CombatDestination>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CombatReply(state with { Inventory = ImmutableDictionary<string, int>.Empty.Add("crafted", 1) }, 4));
        var result = await new CombatRun(new(3), state, CombatRunTests.Destination(), port.Object,
            new CombatRunTests.NoDelay(), craftPlan: plan).ExecuteCycleAsync();
        Assert.Equal(CombatCommand.Craft, result.Command);
        Assert.Equal(1, result.State!.FreeUnits);
    }
}
