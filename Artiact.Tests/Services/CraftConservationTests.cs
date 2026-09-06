using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class CraftConservationTests
{
    [Fact]
    public async Task SharedCraftDependencyIsNotARecursiveCycle()
    {
        var ore = new ItemDatum { Code = "ore" };
        var bar = Recipe("bar", ("ore", 1));
        var left = Recipe("left", ("bar", 1));
        var right = Recipe("right", ("bar", 1));
        var target = Recipe("target", ("left", 1), ("right", 1));
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetItems()).ReturnsAsync([ore, bar, left, right, target]);
        var stock = new Dictionary<string, int> { ["ore"] = 2 };
        var result = await new CraftChainBuilder(client.Object).TryCreateCraftChain(target, stock);
        Assert.NotNull(result);
        Assert.Equal(new[] { "bar", "left", "bar", "right", "target" }, result.Steps.Select(x => x.Item.Code));
        Assert.Equal(2, stock["ore"]);
        Assert.Single(stock);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task DistinctBranchesCannotSpendTheSameOreTwice(int quantity, bool possible)
    {
        var ore = new ItemDatum { Code = "ore" };
        var left = Recipe("left", ("ore", 1));
        var right = Recipe("right", ("ore", 1));
        var target = Recipe("target", ("left", 1), ("right", 1));
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetItems()).ReturnsAsync([ore, left, right, target]);
        var stock = new Dictionary<string, int> { ["ore"] = quantity };
        var result = await new CraftChainBuilder(client.Object).TryCreateCraftChain(target, stock);
        Assert.Equal(possible, result is not null);
        Assert.Single(stock);
        Assert.Equal(quantity, stock["ore"]);
    }

    [Fact]
    public async Task ActualRecursiveRecipeFailsWithoutChangingCallerStock()
    {
        var item = Recipe("cycle", ("cycle", 1));
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetItems()).ReturnsAsync([item]);
        Assert.Null(await new CraftChainBuilder(client.Object).TryCreateCraftChain(item, []));
    }

    [Fact]
    public async Task BatchSurplusIsConsumedByTheOtherBranch()
    {
        var ore = new ItemDatum { Code = "ore" };
        var bar = Recipe("bar", ("ore", 1));
        bar.Craft!.Quantity = 2;
        var left = Recipe("left", ("bar", 1));
        var right = Recipe("right", ("bar", 1));
        var target = Recipe("target", ("left", 1), ("right", 1));
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetItems()).ReturnsAsync([ore, bar, left, right, target]);
        var result = await new CraftChainBuilder(client.Object).TryCreateCraftChain(target, new() { ["ore"] = 1 });
        Assert.NotNull(result);
        Assert.Equal(new[] { "bar", "left", "right", "target" }, result.Steps.Select(x => x.Item.Code));
        Assert.Equal(1, result.Steps[0].Quantity);
    }

    private static ItemDatum Recipe(string code, params (string Code, int Quantity)[] items) => new()
    {
        Code = code, Craft = new Craft { Quantity = 1, Skill = "weaponcrafting", Level = 1,
            Items = items.Select(x => new Item { Code = x.Code, Quantity = x.Quantity }).ToList() }
    };

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task InvalidOrOverflowingRecipeFailsClosed(int amount)
    {
        var ore = new ItemDatum { Code = "ore" };
        var bar = Recipe("bar", ("ore", 2));
        var target = Recipe("target", ("bar", amount));
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetItems()).ReturnsAsync([ore, bar, target]);
        Assert.Null(await new CraftChainBuilder(client.Object).TryCreateCraftChain(target, new() { ["ore"] = int.MaxValue }));
    }

    [Fact]
    public async Task PreOwnedIntermediateReducesRequiredBatches()
    {
        var ore = new ItemDatum { Code = "ore" };
        var bar = Recipe("bar", ("ore", 1));
        bar.Craft!.Quantity = 2;
        var target = Recipe("target", ("bar", 6));
        var client = new Mock<IGameClient>();
        client.Setup(x => x.GetItems()).ReturnsAsync([ore, bar, target]);
        var result = await new CraftChainBuilder(client.Object).TryCreateCraftChain(target, new() { ["ore"] = 2, ["bar"] = 2 });
        Assert.NotNull(result);
        Assert.Equal(2, result.Steps[0].Quantity);
        Assert.Equal(2, result.Steps[0].RequiredItems[0].Quantity);
        Assert.Equal(6, result.Steps[1].RequiredItems[0].Quantity);
    }
}
