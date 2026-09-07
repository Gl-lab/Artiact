using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class GatheringDropTests
{
    private static readonly JsonElement Map = JsonSerializer.SerializeToElement(new
    {
        map_id = 1, layer = "overworld", access = new { type = "standard", conditions = Array.Empty<object>() },
        interactions = new { content = new { type = "resource", code = "rocks" }, transition = (object?)null }
    });
    private static StrategyObservation World(int ore = 0, int rare = 0, int xp = 0, int capacity = 10, int protectedStock = 1)
    {
        var character = JsonSerializer.SerializeToElement(new { name = "hero", map_id = 1, layer = "overworld", inventory_max_items = capacity,
            mining_level = 1, mining_xp = xp, mining_max_xp = 100,
            inventory = new[] { new { code = "ore", quantity = ore }, new { code = "rare", quantity = rare }, new { code = "protected", quantity = protectedStock } } });
        var resource = JsonSerializer.SerializeToElement(new { code = "rocks", skill = "mining", level = 1,
            drops = new[] { new { code = "ore", rate = 1, min_quantity = 1, max_quantity = 2 }, new { code = "rare", rate = 200, min_quantity = 1, max_quantity = 1 } } });
        return new(character, ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty.Add("maps", [Map]).Add("resources", [resource]), "test");
    }
    private static StrategyCandidate Evaluate(StrategyObservation observation)
    {
        var policy = new PortfolioSettings { Skills = [new("mining", 2, 30)] }.Policy();
        return new GatheringStrategy(policy.Skills[0], policy, new StrategyActionPort(null!, null!)).Evaluate(observation);
    }
    [Theory]
    [InlineData(1, 0, 1, true)]
    [InlineData(2, 1, 1, true)]
    [InlineData(0, 1, 1, false)]
    [InlineData(3, 0, 1, false)]
    [InlineData(1, 2, 1, false)]
    [InlineData(1, 0, 0, false)]
    public void VerifiesGuaranteedOptionalAndProtectedStock(int ore, int rare, int protectedStock, bool valid)
    {
        var candidate = Evaluate(World());
        Assert.NotNull(candidate.Command);
        Assert.Equal(valid, candidate.Command.Postcondition(World(ore, rare, 1, protectedStock: protectedStock)));
    }
    [Fact]
    public void ReservesSimultaneousMaximumDrops()
    {
        Assert.Equal("InventoryPressure", Evaluate(World(capacity: 3)).Rejection);
        Assert.NotNull(Evaluate(World(capacity: 4)).Command);
    }
    [Theory]
    [InlineData("code", "\"rare\"")]
    [InlineData("rate", "0")]
    [InlineData("rate", "200")]
    [InlineData("min_quantity", "0")]
    [InlineData("max_quantity", "0")]
    public void InvalidOrRareOnlyTableIsRejected(string field, string value)
    {
        var world = World();
        var resource = System.Text.Json.Nodes.JsonNode.Parse(world.Catalogs["resources"][0].GetRawText())!;
        resource["drops"]![0]![field] = System.Text.Json.Nodes.JsonNode.Parse(value);
        var changed = new StrategyObservation(world.Character, world.Catalogs.SetItem("resources", [JsonSerializer.SerializeToElement(resource)]), world.Policy);
        Assert.Equal("NoSupportedResource", Evaluate(changed).Rejection);
    }
}
