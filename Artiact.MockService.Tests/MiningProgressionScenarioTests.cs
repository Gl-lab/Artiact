using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Artiact.SmartProxy.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Artiact.MockService.Tests;

public class MiningProgressionScenarioTests
{
    [Fact]
    public async Task NamedProgressionScenarioCanReset()
    {
        await using MockServiceFactory factory = new(); using var client = factory.CreateClient();
        foreach(string scenario in new[] { "mining-progression", "basic-mining", "mining-progression" })
        {
            var response = await client.PostAsJsonAsync("/__mock/reset", new { scenario }); response.EnsureSuccessStatusCode();
            bool progression = scenario == "mining-progression";
            var expected = ExpectedProgression.Definition(progression);
            Assert.Equal(JsonSerializer.Serialize(expected.Maps), JsonSerializer.Serialize(await client.GetFromJsonAsync<Map>("/maps?page=1")));
            Assert.Equal(JsonSerializer.Serialize(expected.Resources), JsonSerializer.Serialize(await client.GetFromJsonAsync<ResourceResponse>("/resources?page=1")));
            Assert.Equal(JsonSerializer.Serialize(expected.Items), JsonSerializer.Serialize(await client.GetFromJsonAsync<ItemsResponse>("/items?page=1")));
            Assert.Equal(JsonSerializer.Serialize(expected.Monsters), JsonSerializer.Serialize(await client.GetFromJsonAsync<MonstersResponse>("/monsters?page=1")));
            var character = await client.GetFromJsonAsync<CharacterResponse>("/characters/MockHero");
            ScenarioAssertions.CharacterEquals(expected.Character, character!.Data);
            if(!progression)
            {
                (await client.PostAsJsonAsync("/my/MockHero/action/move", new { x = 2, y = 0 })).EnsureSuccessStatusCode();
                (await client.PostAsync("/my/MockHero/action/gathering", null)).EnsureSuccessStatusCode();
                Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/my/MockHero/action/gathering", null)).StatusCode);
            }
        }
    }

    [Fact]
    public async Task FullResponsesAndTraceMatchLiteralOraclesIncludingIronAndOrigin()
    {
        await using MockServiceFactory factory = new(); using var client = factory.CreateClient();
        var snapshots = new List<string>();
        for(int replay = 0; replay < 2; replay++)
        {
            (await client.PostAsJsonAsync("/__mock/reset", new { scenario = "mining-progression" })).EnsureSuccessStatusCode();
            (await client.GetAsync("/characters/MockHero")).EnsureSuccessStatusCode();
            for(int i = 0; i < 7; i++)
            {
                var response = i is 0 or 3 or 6
                    ? await client.PostAsJsonAsync("/my/MockHero/action/move", new { x = i == 0 ? 2 : i == 3 ? 4 : 0, y = 0 })
                    : await client.PostAsync("/my/MockHero/action/gathering", null);
                response.EnsureSuccessStatusCode(); ExpectedProgression.AssertAction(i, (await response.Content.ReadFromJsonAsync<ActionResponse>())!);
                var trace = (await client.GetFromJsonAsync<List<TraceEntry>>("/__mock/trace"))!;
                Assert.Equal(Enumerable.Range(0, i + 1).Select(n => ExpectedProgression.Trace(n, replay + 1)), trace);
            }
            var state = (await client.GetFromJsonAsync<StateSummary>("/__mock/state/MockHero"))!;
            Assert.Equal("mining-progression", state.Scenario);
            Assert.Equal("Moved", state.Phase); Assert.Equal("2000-01-01T00:00:41.0000000Z", state.VirtualTime);
            snapshots.Add(JsonSerializer.Serialize(state with { Generation = 0 }));
        }
        Assert.Equal(snapshots[0], snapshots[1]);
    }

    [Theory]
    [InlineData(1, 4, 6, 2, 0)]
    [InlineData(1, 6, 6, 2, 2)]
    [InlineData(1, 9, 26, 4, 5)]
    public void SyntheticAwardsHandleThresholdCarryAndMultipleLevels(int level, int xp, int award, int nextLevel, int nextXp) =>
        Assert.Equal((nextLevel, nextXp), MockScenarioStore.AwardXp(level, xp, award));

    [Theory]
    [InlineData("capacity", "inventory_full")]
    [InlineData("slots", "inventory_full")]
    [InlineData("level", "insufficient_mining_level")]
    [InlineData("xp_overflow", "invalid_transition")]
    [InlineData("time_overflow", "invalid_transition")]
    public void RejectionIsAtomicAcrossCompleteStateAndTrace(string cause, string code)
    {
        var definition = ExpectedProgression.Definition();
        if(cause == "capacity") definition.Character.InventoryMaxItems = 0;
        if(cause == "slots") { definition.Character.Inventory = [new() { Slot = 1, Code = "other", Quantity = 1 }]; }
        if(cause == "xp_overflow") { definition.Character.MiningLevel = int.MaxValue; definition.Character.MiningXp = 8; }
        var store = new MockScenarioStore(ExpectedProgression.Definition(false), definition);
        store.Reset("mining-progression"); store.GetCharacter("MockHero");
        Assert.NotNull(store.Move("MockHero", cause == "level" ? "{\"x\":4,\"y\":0}" : "{\"x\":2,\"y\":0}").Value);
        if(cause == "time_overflow") typeof(MockScenarioStore).GetField("_virtualTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(store, DateTime.MaxValue);
        string before = Snapshot(store); var rejected = store.Gather("MockHero");
        Assert.Equal(code, rejected.Code); Assert.Equal(code == "invalid_transition" ? 409 : 422, rejected.Status);
        Assert.Equal(before, Snapshot(store));
    }

    [Theory]
    [InlineData("resource_code")]
    [InlineData("coordinates")]
    [InlineData("slots")]
    [InlineData("item_reference")]
    [InlineData("map_reference")]
    [InlineData("xp")]
    [InlineData("capacity")]
    public void InvalidFixturesFailBeforeServing(string cause)
    {
        var definition = ExpectedProgression.Definition();
        switch(cause)
        {
            case "resource_code": definition.Resources.Data[1].Code = "copper_rocks"; break;
            case "coordinates": definition.Maps.Data[1].X = 0; break;
            case "slots": definition.Character.Inventory[1].Slot = 1; break;
            case "item_reference": definition.Resources.Data[0].Drops![0].Code = "unknown"; break;
            case "map_reference": definition.Maps.Data[1].Content.Code = "unknown"; break;
            case "xp": definition.Character.MiningXp = 10; break;
            case "capacity": definition.Character.InventoryMaxItems = -1; break;
        }
        Assert.Throws<InvalidOperationException>(() => new MockScenarioStore(ExpectedProgression.Definition(false), definition));
    }

    [Fact]
    public void ExistingItemWinsOverEarlierEmptySlotAndObservationsAreCopies()
    {
        var definition = ExpectedProgression.Definition(); definition.Character.Inventory[2].Code = "copper_ore";
        definition.Character.Inventory[2].Quantity = 1;
        var store = new MockScenarioStore(ExpectedProgression.Definition(false), definition); store.Reset("mining-progression");
        store.GetCharacter("MockHero"); store.Move("MockHero", "{\"x\":2,\"y\":0}");
        var response = store.Gather("MockHero").Value!;
        Assert.Equal("", response.Data.Character.Inventory[0].Code); Assert.Equal(2, response.Data.Character.Inventory[2].Quantity);
        string before = Snapshot(store); response.Data.Character.Inventory.Clear(); store.GetMaps().Value!.Data.Clear();
        Assert.Equal(before, Snapshot(store));
    }

    [Fact]
    public async Task ConcurrentGathersAndReadsObserveOnlyCompleteTransitions()
    {
        var store = new MockScenarioStore(ExpectedProgression.Definition(false), ExpectedProgression.Definition());
        store.Reset("mining-progression"); store.GetCharacter("MockHero"); store.Move("MockHero", "{\"x\":2,\"y\":0}");
        var before = store.GetState("MockHero").Value!;
        var read = Task.Run(() => store.GetState("MockHero").Value!);
        await Task.WhenAll(Task.Run(() => store.Gather("MockHero")), Task.Run(() => store.Gather("MockHero")));
        var observed = await read;
        var possible = new[] { before, before with { Phase = "Gathered", VirtualTime = "2000-01-01T00:00:12.0000000Z", Character = ExpectedProgression.Character(2,1,6,1) },
            before with { Phase = "Gathered", VirtualTime = "2000-01-01T00:00:17.0000000Z", Character = ExpectedProgression.Character(2,2,2,2) } };
        Assert.Contains(JsonSerializer.Serialize(observed), possible.Select(s => JsonSerializer.Serialize(s)));
        Assert.Equal(Enumerable.Range(0,3).Select(i => ExpectedProgression.Trace(i,1)), store.GetTrace().Value);
    }
    [Fact]
    public async Task ResetRacingGatherAndCatalogReadKeepsScenarioAtomic()
    {
        for(int iteration = 0; iteration < 10; iteration++)
        {
            var store = new MockScenarioStore(ExpectedProgression.Definition(false), ExpectedProgression.Definition());
            store.Reset("mining-progression"); store.GetCharacter("MockHero"); store.Move("MockHero", "{\"x\":2,\"y\":0}");
            var catalog = Task.Run(() => store.GetMaps().Value!);
            var gather = Task.Run(() => store.Gather("MockHero"));
            await Task.Run(() => store.Reset("basic-mining"));
            var result = await gather; var observed = await catalog;
            Assert.True(result.Value is not null || result.Code == "character_not_initialized");
            Assert.Contains(JsonSerializer.Serialize(observed), new[] { JsonSerializer.Serialize(ExpectedScenario.Maps()), JsonSerializer.Serialize(ExpectedProgression.Definition().Maps) });
            Assert.Empty(store.GetTrace().Value!);
            ScenarioAssertions.CharacterEquals(ExpectedScenario.Character(), store.GetCharacter("MockHero").Value!);
            var state = store.GetState("MockHero").Value!;
            Assert.Equal("basic-mining", state.Scenario); Assert.Equal(2, state.Generation); Assert.Equal("Ready", state.Phase);
            Assert.Equal("2000-01-01T00:00:00.0000000Z", state.VirtualTime);
        }
    }

    [Fact]
    public async Task ProgressionRejectsUnknownResetMovesAndOriginGatherWithoutMutation()
    {
        await using MockServiceFactory factory = new(); using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/__mock/reset", new { scenario = "mining-progression" }); await client.GetAsync("/characters/MockHero");
        var store = (MockScenarioStore)factory.Services.GetRequiredService<IMockScenarioStore>();
        string before = Snapshot(store);
        var reset = await client.PostAsJsonAsync("/__mock/reset", new { scenario = "Mining-progression" });
        Assert.Equal(HttpStatusCode.NotFound, reset.StatusCode); Assert.Equal(before, Snapshot(store));
        Assert.Equal("gathering_not_available", store.Gather("MockHero").Code); Assert.Equal(before, Snapshot(store));
        Assert.Equal("invalid_transition", store.Move("MockHero", "{\"x\":0,\"y\":0}").Code); Assert.Equal(before, Snapshot(store));
        Assert.Equal("destination_not_found", store.Move("MockHero", "{\"x\":9,\"y\":0}").Code); Assert.Equal(before, Snapshot(store));
    }

    private static string Snapshot(MockScenarioStore store) => JsonSerializer.Serialize(new { State = store.GetState("MockHero"), Trace = store.GetTrace() });
}
