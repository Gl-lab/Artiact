using System.Text.Json;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Services;
using Moq;

namespace Artiact.Tests.Services;

public class MiningDestinationResolverTests
{
    private static ResourceDatum Resource(string code = "copper", int level = 1, string? skill = "mining") =>
        new() { Code = code, Level = level, Skill = skill };
    private static MapPlace Map(string code = "copper", int x = 2, int y = 0, string type = "resource") =>
        new() { X = x, Y = y, Content = new() { Code = code, Type = type } };
    private static Character Character(int level = 1, int x = 0, int y = 0) =>
        new() { MiningLevel = level, X = x, Y = y };

    [Theory]
    [InlineData("mining")]
    [InlineData("Mining")]
    [InlineData("MiNiNg")]
    public void MixedCaseMiningCanSelectMappedResource(string skill)
    {
        var result = MiningDestinationResolver.Rank(Character(), [Resource(skill: skill)], [Map()]);
        Assert.Equal(new MiningDestination("copper", 1, 2, 0), result.Destination);
        Assert.Null(result.Reason);
    }

    [Theory]
    [InlineData("Copper", "resource")]
    [InlineData("copper", "Resource")]
    [InlineData("copper", "monster")]
    [InlineData("", "resource")]
    [InlineData(" ", "resource")]
    [InlineData(null, "resource")]
    public void JoinUsesExactCodeAndType(string? code, string type)
    {
        AssertMissing(MiningDestinationResolver.Rank(Character(), [Resource()], [Map(code!, type: type)]));
    }

    [Fact]
    public void EmptyCatalogsAndEmptyTilesHaveNoDestination()
    {
        AssertMissing(MiningDestinationResolver.Rank(Character(), [], []));
        AssertMissing(MiningDestinationResolver.Rank(Character(), [Resource()], []));
        AssertMissing(MiningDestinationResolver.Rank(Character(), [], [Map()]));
        AssertMissing(MiningDestinationResolver.Rank(Character(), [Resource()], [new() { Content = null! }]));
    }

    public static IEnumerable<object[]> InvalidCatalogs()
    {
        yield return new object[] { null!, new List<MapPlace>() };
        yield return new object[] { new List<ResourceDatum>(), null! };
        yield return new object[] { new List<ResourceDatum> { null! }, new List<MapPlace>() };
        yield return new object[] { new List<ResourceDatum>(), new List<MapPlace> { null! } };
        foreach (string? code in new string?[] { null, "", " " })
            yield return new object[] { new List<ResourceDatum> { Resource(code!) }, new List<MapPlace>() };
        foreach (int level in new[] { 0, -1, int.MinValue })
            yield return new object[] { new List<ResourceDatum> { Resource(level: level, skill: "fishing") }, new List<MapPlace>() };
        yield return new object[] { new List<ResourceDatum> { Resource(), Resource(skill: "fishing") }, new List<MapPlace>() };
        yield return new object[] { new List<ResourceDatum>(), new List<MapPlace> { Map(), Map("other") } };
    }

    [Theory]
    [MemberData(nameof(InvalidCatalogs))]
    public void MalformedCatalogsAreInvalidEvenWithoutCandidates(List<ResourceDatum>? resources, List<MapPlace>? maps)
    {
        var result = MiningDestinationResolver.Rank(Character(), resources, maps);
        Assert.Null(result.Destination);
        Assert.Equal(GoalDecisionReason.InvalidMiningCatalog, result.Reason);
    }

    [Theory]
    [InlineData(0, 1, "mining", true)]
    [InlineData(1, 2, "mining", false)]
    [InlineData(10, 1, "mining", true)]
    [InlineData(11, 1, "mining", false)]
    [InlineData(1, 1, "fishing", false)]
    [InlineData(1, 1, null, false)]
    [InlineData(int.MaxValue, int.MaxValue, "mining", true)]
    public void EligibilityUsesLevelZeroCompatibilityAndWideXpBoundary(int current, int level, string? skill, bool eligible)
    {
        var result = MiningDestinationResolver.Rank(Character(current), [Resource(level: level, skill: skill)], [Map()]);
        if (eligible)
            Assert.Equal(new MiningDestination("copper", level, 2, 0), result.Destination);
        else
            AssertMissing(result);
    }

    [Fact]
    public void UnmappedHighestResourceDoesNotHideMappedLowerResource()
    {
        var result = MiningDestinationResolver.Rank(Character(2), [Resource("iron", 2), Resource()], [Map()]);
        Assert.Equal(new MiningDestination("copper", 1, 2, 0), result.Destination);
    }

    [Fact]
    public void RankingUsesLevelThenDistanceThenOrdinalCodeThenXThenY()
    {
        List<ResourceDatum> resources = [Resource("z", 2), Resource("a", 2), Resource("A", 2), Resource("near", 1)];
        List<MapPlace> maps = [Map("z", 0, 1), Map("a", 0, -1), Map("A", -1, 0), Map("A", 0, 2), Map("near", 0, 0)];
        Assert.Equal(new MiningDestination("A", 2, -1, 0), MiningDestinationResolver.Rank(Character(2), resources, maps).Destination);
        maps.Add(Map("z", 0, 0)); // distance wins over ordinal code
        maps.RemoveAll(map => map.Content.Code == "near");
        Assert.Equal(new MiningDestination("z", 2, 0, 0), MiningDestinationResolver.Rank(Character(2), resources, maps).Destination);
        maps = [Map("A", 1, 0), Map("A", 0, 1), Map("A", 0, -1)];
        Assert.Equal(new MiningDestination("A", 2, 0, -1), MiningDestinationResolver.Rank(Character(2), resources, maps).Destination);
        maps.Add(Map("A", -1, 0));
        Assert.Equal(new MiningDestination("A", 2, -1, 0), MiningDestinationResolver.Rank(Character(2), resources, maps).Destination);
    }

    [Fact]
    public void PermutationsDoNotChangeResultOrMutateInputsAndResultDoesNotRetainDtos()
    {
        ResourceDatum[] resources = [Resource("b", 2), Resource("a", 2), Resource("low")];
        MapPlace[] maps = [Map("b", 1), Map("a", -1), Map("a", 0, -1)];
        foreach (var resourceOrder in Permutations(resources))
        foreach (var mapOrder in Permutations(maps))
        {
            string before = JsonSerializer.Serialize(new { resourceOrder, mapOrder });
            var result = MiningDestinationResolver.Rank(Character(2), resourceOrder, mapOrder);
            Assert.Equal(new MiningDestination("a", 2, -1, 0), result.Destination);
            Assert.Equal(before, JsonSerializer.Serialize(new { resourceOrder, mapOrder }));
        }
        var selected = MiningDestinationResolver.Rank(Character(2), resources, maps).Destination;
        resources[1].Code = "changed";
        maps[1].X = 99;
        Assert.Equal(new MiningDestination("a", 2, -1, 0), selected);
    }

    [Fact]
    public void ExtremeCoordinatesUseWideSubtractionAbsoluteValueAndSum()
    {
        var result = MiningDestinationResolver.Rank(Character(1, int.MinValue, int.MinValue),
            [Resource()], [Map(x: int.MaxValue, y: int.MaxValue), Map(x: int.MinValue, y: int.MaxValue)]);
        Assert.Equal(new MiningDestination("copper", 1, int.MinValue, int.MaxValue), result.Destination);
    }

    [Fact]
    public void ResourceCodesAreOrdinalAndCaseVariantsAreNotDuplicates()
    {
        var result = MiningDestinationResolver.Rank(Character(), [Resource("copper"), Resource("Copper")], [Map("Copper")]);
        Assert.Equal(new MiningDestination("Copper", 1, 2, 0), result.Destination);
    }

    [Fact]
    public async Task ProviderNullIsInvalidButLoadingFailurePropagatesUnchanged()
    {
        Mock<IGameClient> client = new(MockBehavior.Strict);
        client.Setup(x => x.GetResources()).ReturnsAsync((List<ResourceDatum>)null!);
        client.Setup(x => x.GetMap()).ReturnsAsync(new List<MapPlace>());
        var resolver = new MiningDestinationResolver(client.Object);
        Assert.Equal(GoalDecisionReason.InvalidMiningCatalog, (await resolver.ResolveAsync(Character())).Reason);
        InvalidOperationException failure = new("loading failed");
        client.Setup(x => x.GetResources()).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(Character())));
        client.Setup(x => x.GetResources()).ReturnsAsync(new List<ResourceDatum> { Resource() });
        client.Setup(x => x.GetMap()).ThrowsAsync(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync(Character())));
        client.Setup(x => x.GetMap()).ReturnsAsync(new List<MapPlace> { Map() });
        Assert.Equal(new MiningDestination("copper", 1, 2, 0), (await resolver.ResolveAsync(Character())).Destination);
    }

    private static void AssertMissing((MiningDestination? Destination, GoalDecisionReason? Reason) result)
    {
        Assert.Null(result.Destination);
        Assert.Equal(GoalDecisionReason.NoMiningDestination, result.Reason);
    }

    private static IEnumerable<T[]> Permutations<T>(T[] values)
    {
        if (values.Length == 0) { yield return []; yield break; }
        for (int i = 0; i < values.Length; i++)
        foreach (var tail in Permutations(values.Where((_, index) => index != i).ToArray()))
            yield return new[] { values[i] }.Concat(tail).ToArray();
    }
}
