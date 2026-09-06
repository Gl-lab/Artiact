using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public class CombatProgressionFlowTests
{
    [Theory]
    [InlineData("combat-progression", 5, 29)]
    [InlineData("combat-equipment", 7, 35)]
    public async Task RealClientsCompleteAndReplayCombatScenario(string scenario, int count, int seconds)
    {
        await using var factory = new MockServiceFactory();
        using var recorder = new CombatRecorder(factory.Server.CreateHandler());
        using var transport = new HttpClient(recorder) { BaseAddress = new Uri("http://localhost") };
        var settings = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var http = new GameHttpClient(new ClientFactory(transport), settings);
        string? firstState = null, firstTrace = null, firstDecisions = null;
        for (int replay = 0; replay < 2; replay++)
        {
            using var reset = await transport.PostAsync("/__mock/reset", new StringContent(
                JsonSerializer.Serialize(new { scenario }), Encoding.UTF8, "application/json"));
            reset.EnsureSuccessStatusCode();
            recorder.Responses.Clear();
            var client = new GameClient(http, settings, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("CombatFlow"));
            var characters = new CharacterService();
            await client.WarmUpCache();
            characters.SaveCharacter(await client.GetCharacter());
            var initial = Assert.IsType<CombatObservation>(CombatObservation.Read(client.LastCharacterPayload!.Value));
            var world = await new CombatCatalog(http).ResolveAsync(initial, "dummy", CancellationToken.None);
            Assert.NotNull(world.Destination);
            Assert.Equal(scenario == "combat-equipment" ? "quick_blade" : null, world.Gear?.Code);
            if (scenario == "combat-equipment")
            {
                var bothEligible = await new CombatCatalog(http).ResolveAsync(initial with { Level = 2 }, "dummy", CancellationToken.None);
                Assert.Equal("quick_blade", bothEligible.Gear?.Code);
            }
            var run = await new CombatSessionFactory(client, new CombatCatalog(http), characters, new NoDelay())
                .CreateAsync(new(2), "dummy", new CombatLimits(NoProgress: 5));
            var decisions = new List<CombatDecision>();
            for (int i = 0; i < count; i++) decisions.Add(await run.ExecuteCycleAsync());
            var final = decisions[^1];
            Assert.Equal(CombatStatus.Completed, final.Status);
            Assert.Equal(seconds, final.VirtualSeconds);
            Assert.Equal(2, final.State!.Level);
            Assert.Equal(0, final.State.Xp);
            Assert.Equal(14, final.State.Stats.Hp);
            Assert.Equal(2, final.State.MapId);
            Assert.Equal(2, final.State.Inventory["feather"]);
            Assert.Equal("quick_blade", final.State.Weapon);
            Assert.Equal(scenario == "combat-equipment" ? 6 : 8, final.State.FreeUnits);
            if (scenario == "combat-equipment") Assert.Equal(1, final.State.Inventory["old"]);
            Assert.Same(final, await run.ExecuteCycleAsync());
            ExpectedCombat.AssertResponses(scenario == "combat-equipment", recorder.Responses);
            string decisionJson = JsonSerializer.Serialize(decisions);
            string state = await transport.GetStringAsync("/__mock/state/researcher");
            string trace = await transport.GetStringAsync("/__mock/trace");
            if (replay == 0) { firstState = state; firstTrace = trace; firstDecisions = decisionJson; }
            else { Assert.Equal(firstState, state); Assert.Equal(firstTrace, trace); Assert.Equal(firstDecisions, decisionJson); }
        }
    }

    [Theory]
    [InlineData("fight", "{}")]
    [InlineData("move", "{\"map_id\":99}")]
    [InlineData("equip", "[{\"slot\":\"weapon\",\"code\":\"missing\",\"quantity\":1}]")]
    [InlineData("rest", "{}")]
    [InlineData("gathering", "{}")]
    public async Task RejectedCombatActionsAreAtomic(string action, string body)
    {
        await using var factory = new MockServiceFactory();
        using var http = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = new Uri("http://localhost") };
        using var reset = await http.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"combat-progression\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        await http.GetStringAsync("/characters/RESEARCHER");
        string before = await http.GetStringAsync("/__mock/state/researcher");
        using var failed = await http.PostAsync("/my/Researcher/action/" + action, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.False(failed.IsSuccessStatusCode);
        Assert.Equal(before, await http.GetStringAsync("/__mock/state/researcher"));
        Assert.Equal("[]", await http.GetStringAsync("/__mock/trace"));
    }

    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    [Theory]
    [InlineData("move", "missing_stats", CombatReason.InvalidPostcondition, 1)]
    [InlineData("move", "wrong_map", CombatReason.InvalidPostcondition, 1)]
    [InlineData("move", "lost", CombatReason.UnknownOutcome, 1)]
    [InlineData("move", "cancel", CombatReason.Cancelled, 1)]
    [InlineData("fight", "opponent", CombatReason.InvalidPostcondition, 2)]
    [InlineData("fight", "final_hp", CombatReason.InvalidPostcondition, 2)]
    [InlineData("fight", "drop", CombatReason.InvalidPostcondition, 2)]
    [InlineData("rest", "restored", CombatReason.InvalidPostcondition, 3)]
    [InlineData("unequip", "slot", CombatReason.InvalidPostcondition, 1)]
    public async Task CorruptedOrLostHttpReplyStopsWithoutFollowUp(string action, string corruption, CombatReason reason, int expectedActions)
    {
        await using var factory = new MockServiceFactory();
        using var stop = new CancellationTokenSource();
        using var handler = new CorruptReplyHandler(factory.Server.CreateHandler(), action, corruption, stop);
        using var transport = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var settings = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var http = new GameHttpClient(new ClientFactory(transport), settings);
        string scenario = action == "unequip" ? "combat-equipment" : "combat-progression";
        using var reset = await transport.PostAsync("/__mock/reset", new StringContent(JsonSerializer.Serialize(new { scenario }), Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        var client = new GameClient(http, settings, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("CombatFailure"));
        var state = new CharacterService();
        var run = await new CombatSessionFactory(client, new CombatCatalog(http), state, new NoDelay())
            .CreateAsync(new(2), "dummy", new CombatLimits(NoProgress: 5));
        CombatDecision? decision = null;
        for (int i = 0; i < 10; i++)
        {
            decision = await run.ExecuteCycleAsync(stop.Token);
            if (decision.Status != CombatStatus.Selected) break;
        }
        Assert.Equal(reason, decision!.Reason);
        Assert.Equal(expectedActions, handler.Actions);
        Assert.Same(decision, await run.ExecuteCycleAsync());
        Assert.Equal(expectedActions, handler.Actions);
        if (corruption == "cancel") Assert.Equal(1, state.GetCharacter().X);
    }

    private sealed class CorruptReplyHandler(HttpMessageHandler inner, string action, string corruption, CancellationTokenSource stop) : DelegatingHandler(inner)
    {
        public int Actions { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            if (!request.RequestUri!.AbsolutePath.Contains("/action/", StringComparison.Ordinal)) return response;
            Actions++;
            if (!request.RequestUri.AbsolutePath.EndsWith("/" + action, StringComparison.Ordinal)) return response;
            if (corruption == "lost") { response.Dispose(); throw new HttpRequestException("Synthetic lost reply"); }
            if (corruption == "cancel") { stop.Cancel(); return response; }
            var root = JsonNode.Parse(await response.Content.ReadAsStringAsync(token))!;
            var data = root["data"]!;
            switch (corruption)
            {
                case "missing_stats": data["character"]!.AsObject().Remove("attack_fire"); break;
                case "wrong_map": data["character"]!["map_id"] = 99; break;
                case "opponent": data["fight"]!["opponent"] = "other"; break;
                case "final_hp": data["fight"]!["characters"]![0]!["final_hp"] = 99; break;
                case "drop": data["fight"]!["characters"]![0]!["drops"]![0]!["quantity"] = -1; break;
                case "restored": data["hp_restored"] = 999; break;
                case "slot": data["items"]![0]!["slot"] = "shield"; break;
            }
            response.Content.Dispose();
            response.Content = new StringContent(root.ToJsonString(), Encoding.UTF8, "application/json");
            return response;
        }
    }
    private sealed class CombatRecorder(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<(string Path, string Body)> Responses { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            if (request.RequestUri!.AbsolutePath.Contains("/action/", StringComparison.Ordinal))
                Responses.Add((request.RequestUri.AbsolutePath, await response.Content.ReadAsStringAsync(token)));
            return response;
        }
    }

    [Theory]
    [InlineData("fight", "not-json")]
    [InlineData("fight", "[]")]
    [InlineData("fight", "{\"participants\":[\"other\"]}")]
    [InlineData("rest", "not-json")]
    [InlineData("rest", "[]")]
    public async Task MalformedAvailableActionDoesNotMutate(string action, string body)
    {
        await using var factory = new MockServiceFactory();
        using var http = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = new Uri("http://localhost") };
        using var reset = await http.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"combat-progression\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        await http.GetStringAsync("/characters/researcher");
        using var move = await http.PostAsync("/my/researcher/action/move", new StringContent("{\"map_id\":2}", Encoding.UTF8, "application/json"));
        move.EnsureSuccessStatusCode();
        if (action == "rest")
        {
            using var fight = await http.PostAsync("/my/researcher/action/fight", null);
            fight.EnsureSuccessStatusCode();
        }
        string before = await http.GetStringAsync("/__mock/state/researcher");
        string trace = await http.GetStringAsync("/__mock/trace");
        using var failed = await http.PostAsync("/my/researcher/action/" + action, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.False(failed.IsSuccessStatusCode);
        Assert.Equal(before, await http.GetStringAsync("/__mock/state/researcher"));
        Assert.Equal(trace, await http.GetStringAsync("/__mock/trace"));
    }
    private sealed class NoDelay : IMiningCooldownDelay
    {
        public Task WaitAsync(int totalSeconds, CancellationToken cancellationToken) => Task.CompletedTask;
    }
    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => Task.CompletedTask;
    }
}
