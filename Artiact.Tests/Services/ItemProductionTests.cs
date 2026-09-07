using System.Text.Json;
using Artiact.Services.Strategy;
using Xunit;

namespace Artiact.Tests.Services;

public class ItemProductionTests
{
    private static JsonElement Json(string value) => JsonDocument.Parse(value).RootElement.Clone();
    [Fact]
    public void SharedNestedIngredientsReserveStockOnceAndRetainBatchSurplus()
    {
        var items = new[] {
            Json("""{"code":"tool","craft":{"skill":"weaponcrafting","level":1,"quantity":1,"items":[{"code":"bar","quantity":1},{"code":"ore","quantity":1}]}}"""),
            Json("""{"code":"bar","craft":{"skill":"weaponcrafting","level":1,"quantity":2,"items":[{"code":"ore","quantity":2}]}}""") };
        var resources = new[] { Json("""{"code":"ore_node","skill":"mining","drops":[{"code":"ore","rate":1,"min_quantity":1,"max_quantity":1}]}""") };
        var plan = ItemProductionPlan.Build("tool", 2, new Dictionary<string, int> { ["ore"] = 2 }, new Dictionary<string, int>(), items, resources);
        Assert.Null(plan.Rejection);
        Assert.Equal(new[] { "Craft:bar:1", "Gather:ore:2", "Craft:tool:2" }, plan.Steps.Select(x => $"{x.Kind}:{x.Code}:{x.Quantity}"));
    }
    [Fact]
    public void CyclesAndUnavailableLeavesAreRejected()
    {
        var cycle = new[] { Json("""{"code":"a","craft":{"skill":"mining","level":1,"quantity":1,"items":[{"code":"a","quantity":1}]}}""") };
        Assert.NotNull(ItemProductionPlan.Build("a", 1, new Dictionary<string, int>(), new Dictionary<string, int>(), cycle, []).Rejection);
        Assert.NotNull(ItemProductionPlan.Build("missing", 1, new Dictionary<string, int>(), new Dictionary<string, int>(), [], []).Rejection);
    }
    [Fact]
    public void BankIngredientIsReservedOnceAcrossSiblings()
    {
        var recipes = new[] { Json("""{"code":"tool","craft":{"skill":"mining","level":1,"quantity":1,"items":[{"code":"ore","quantity":3}]}}""") };
        var plan = ItemProductionPlan.Build("tool", 1, new Dictionary<string, int> { ["ore"] = 1 }, new Dictionary<string, int> { ["ore"] = 2 }, recipes, []);
        Assert.Null(plan.Rejection); Assert.Equal("Withdraw", plan.Steps[0].Kind); Assert.Equal(2, plan.Steps[0].Quantity);
        Assert.Equal(3, plan.Steps[1].Ingredients!["ore"]);
    }
}
