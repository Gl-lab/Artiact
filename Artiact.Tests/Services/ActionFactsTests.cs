using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class ActionFactsTests
{
    private static StrategyObservation State(int level, int xp, int ore, int bank = 0) => new(JsonSerializer.SerializeToElement(new
    {
        name = "hero", level, xp, max_xp = 10, inventory = new[] { new { code = "ore", quantity = ore } }
    }), ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "p", new(10, ImmutableDictionary<string, int>.Empty.Add("ore", bank)));
    private static StrategyCandidate Candidate(string command) => new("item:ore", "item", 1, 1, 0, 0, null, false,
        new(command, "", true, _ => true, _ => throw new NotSupportedException()),
        Path: new(new("item", "ore", 2), command, 2, 1, 1, 0, 0, 0, ImmutableDictionary<string, int>.Empty, "test"));
    [Theory]
    [InlineData(1, 8, 3L)]
    [InlineData(2, 2, 7L)]
    [InlineData(3, 2, null)]
    public void XpAcrossUnobservedMultipleLevelsRemainsUnknown(int level, int xp, long? expected)
    {
        var result = ActionFacts.Read(State(1, 5, 1), State(level, xp, 1), Candidate("Fight"), 1, 2);
        Assert.Equal(expected, result.Skills["combat"].Xp); Assert.Equal(level - 1, result.Skills["combat"].Levels);
    }
    [Fact]
    public void BankTransferIsZeroCreationAndCraftConsumptionIsExplicit()
    {
        var transfer = ActionFacts.Read(State(1, 0, 2), State(1, 0, 0, 2), Candidate("Deposit:ore:2"), 1, 2);
        Assert.Equal(0, transfer.UsefulProgress); Assert.Empty(transfer.Inputs); Assert.Equal(0, transfer.StockDelta!["ore"]);
        var craft = ActionFacts.Read(State(1, 0, 2), State(1, 1, 0), Candidate("Craft:tool:1"), 1, 2);
        Assert.Equal(2, craft.Inputs["ore"]); Assert.Null(craft.WaitSeconds);
    }
}
