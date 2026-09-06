using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Artiact.Services.Strategy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public class StrategyPortfolioFlowTests
{
    internal static PortfolioPolicy Policy => new([new("mining", 2, 30), new("woodcutting", 2, 20)], 2, "dummy", "quick_blade");
    [Fact]
    public async Task PortfolioCompletesTwelveAtomicActionsAndReplays()
    {
        await using var factory = new MockServiceFactory();
        using var recorder = new Recorder(factory.Server.CreateHandler());
        using var transport = new HttpClient(recorder) { BaseAddress = new("http://localhost") };
        var settings = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var http = new GameHttpClient(new ClientFactory(transport), settings);
        string? first = null;
        for (int replay = 0; replay < 2; replay++)
        {
            using var reset = await transport.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"strategy-portfolio\"}", Encoding.UTF8, "application/json"));
            reset.EnsureSuccessStatusCode(); recorder.Responses.Clear();
            var client = new GameClient(http, settings, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("Portfolio"));
            var run = new StrategySessionFactory(client, new CombatCatalog(http), new CharacterService(), new NoDelay()).Create(Policy);
            var decisions = new List<StrategyDecision>();
            for (int i = 0; i < 13; i++)
            {
                int before = recorder.Responses.Count;
                decisions.Add(await run.TickAsync());
                Assert.InRange(recorder.Responses.Count - before, 0, 1);
            }
            Assert.Equal(StrategyStatus.Completed, decisions[^1].Status);
            Assert.Equal(12, decisions[^1].Attempts); Assert.Equal(13, decisions[^1].Decisions); Assert.Equal(69, decisions[^1].CooldownSeconds);
            Assert.Equal(new string?[] { "Unequip", "Equip", "Move:4", "Gather:mining", "Gather:mining", "Move:5",
                "Gather:woodcutting", "Gather:woodcutting", "Move:2", "Fight", "Rest", "Fight", null }, decisions.Select(x => x.Command));
            var candidates = decisions[0].Candidates;
            Assert.Equal(4, candidates.Count(x => x.Score.HasValue));
            Assert.Equal(100m / 6, candidates.Single(x => x.Id == "equipment").Score);
            Assert.Equal(30m / 12, candidates.Single(x => x.Id == "skill:mining").Score);
            Assert.Equal(10m / 21, candidates.Single(x => x.Id == "combat").Score);
            var final = run.State!.Character;
            Assert.Equal(2, final.GetProperty("level").GetInt32()); Assert.Equal(2, final.GetProperty("mining_level").GetInt32());
            Assert.Equal(2, final.GetProperty("woodcutting_level").GetInt32()); Assert.Equal(14, final.GetProperty("hp").GetInt32());
            Assert.Equal(2, final.GetProperty("map_id").GetInt32()); Assert.Equal(2, CombatObservation.Read(final)!.FreeUnits);
            ExpectedCombat.AssertPortfolioResponses(recorder.Responses);
            string snapshot = JsonSerializer.Serialize(decisions) + await transport.GetStringAsync("/__mock/state/researcher") + await transport.GetStringAsync("/__mock/trace");
            if (replay == 0) first = snapshot; else Assert.Equal(first, snapshot);
        }
    }
    [Theory]
    [InlineData(100, 10, 30, "equipment")]
    [InlineData(1, 1000, 30, "combat")]
    [InlineData(1, 10, 300, "skill:mining")]
    public async Task ConfiguredValuesSelectEachFeasibleCategory(decimal equipment, decimal combat, decimal mining, string expected)
    {
        await using var factory = new MockServiceFactory();
        using var transport = new HttpClient(factory.Server.CreateHandler()) { BaseAddress = new("http://localhost") };
        var settings = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var http = new GameHttpClient(new ClientFactory(transport), settings);
        using var reset = await transport.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"strategy-portfolio\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        var client = new GameClient(http, settings, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("Portfolio"));
        var policy = Policy with { EquipmentValue = equipment, CombatValue = combat, Skills = [new("mining", 2, mining), new("woodcutting", 2, 20)] };
        var run = new StrategySessionFactory(client, new CombatCatalog(http), new CharacterService(), new NoDelay()).Create(policy);
        var result = await run.TickAsync();
        Assert.Equal(expected, result.Candidate); Assert.Equal(StrategyStatus.Selected, result.Status);
        var productive = await run.TickAsync();
        Assert.Equal(StrategyStatus.Selected, productive.Status);
        Assert.Equal(expected == "equipment" ? "Equip" : expected == "combat" ? "Fight" : "Gather:mining", productive.Command);
        if (expected == "combat")
        {
            Assert.Equal(8, run.State!.Character.GetProperty("hp").GetInt32());
            Assert.Equal(5, run.State.Character.GetProperty("xp").GetInt32());
            Assert.Equal(4, client.LastActionPayload!.Value.GetProperty("fight").GetProperty("turns").GetInt32());
        }
    }
    [Theory]
    [InlineData("equip", "loss", StrategyStatus.UnknownOutcome)]
    [InlineData("gathering", "loss", StrategyStatus.UnknownOutcome)]
    [InlineData("fight", "loss", StrategyStatus.UnknownOutcome)]
    [InlineData("equip", "invalid", StrategyStatus.Blocked)]
    [InlineData("gathering", "invalid", StrategyStatus.Blocked)]
    [InlineData("fight", "invalid", StrategyStatus.Blocked)]
    [InlineData("gathering", "details", StrategyStatus.Blocked)]
    [InlineData("fight", "defeat", StrategyStatus.Blocked)]
    [InlineData("equip", "cancel", StrategyStatus.Cancelled)]
    [InlineData("gathering", "cancel", StrategyStatus.Cancelled)]
    [InlineData("fight", "cancel", StrategyStatus.Cancelled)]
    public async Task BoundaryFailureNeverReplaysCompletedCommands(string action, string mode, StrategyStatus expected)
    {
        await using var factory = new MockServiceFactory();
        using var cancel = new CancellationTokenSource();
        using var recorder = new Recorder(factory.Server.CreateHandler()) { Target = action, Mode = mode, Cancel = cancel };
        using var transport = new HttpClient(recorder) { BaseAddress = new("http://localhost") };
        var settings = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
        var http = new GameHttpClient(new ClientFactory(transport), settings);
        using var reset = await transport.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"strategy-portfolio\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        var client = new GameClient(http, settings, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("Portfolio"));
        var run = new StrategySessionFactory(client, new CombatCatalog(http), new CharacterService(), new NoDelay()).Create(Policy);
        StrategyDecision? result = null;
        for (int i = 0; i < 12 && !recorder.Injected; i++) result = await run.TickAsync(cancel.Token);
        Assert.True(recorder.Injected); Assert.Equal(expected, result!.Status);
        int attempts = recorder.Responses.Count;
        var next = await run.TickAsync();
        Assert.Equal(attempts, recorder.Responses.Count);
        if (mode == "loss") Assert.Equal(StrategyStatus.Reconciled, next.Status);
        else Assert.Same(result, next);
        Assert.NotNull(run.State);
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class NoDelay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult(default(T));
        public Task SaveToCache<T>(T value) where T : class => Task.CompletedTask;
    }
    private sealed class Recorder(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<(string Path, string Body)> Responses { get; } = [];
        public string? Target, Mode;
        public bool Injected;
        public CancellationTokenSource? Cancel;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            if (request.RequestUri!.AbsolutePath.Contains("/action/"))
            {
                string body = await response.Content.ReadAsStringAsync(token);
                Responses.Add((request.RequestUri.AbsolutePath, body));
                if (!Injected && request.RequestUri.AbsolutePath.EndsWith("/" + Target, StringComparison.Ordinal))
                {
                    Injected = true;
                    if (Mode == "loss") { response.Dispose(); throw new HttpRequestException("Synthetic lost response"); }
                    if (Mode == "cancel") Cancel!.Cancel();
                    if (Mode is "invalid" or "details" or "defeat")
                    {
                        var node = JsonNode.Parse(body)!;
                        if (Mode == "defeat") node["data"]!["fight"]!["result"] = "loss";
                        else if (Mode == "details") node["data"]!["details"]!["items"]![0]!["code"] = "wrong_output";
                        else (Target == "fight" ? node["data"]!["characters"]![0]! : node["data"]!["character"]!)["inventory_max_items"] = -1;
                        response.Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json");
                    }
                }
            }
            return response;
        }
    }
}
