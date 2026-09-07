using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public class StagedOperationTests
{
    [Fact]
    public async Task BoundedMiningCompletesAndRestartDoesNotDispatchAgain()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-bounded-" + Guid.NewGuid().ToString("N"));
        var settings = new ExecutionSettings { Mode = "Bounded", AllowActions = true, RunId = "mining-test", RunDirectory = directory };
        try
        {
            await using (var h = new Harness(settings, miningOnly: true))
            {
                await h.Reset(); h.Handler.MiningOnly = true;
                var result = await h.Runner.RunAsync(CancellationToken.None);
                Assert.Equal(StrategyStatus.Completed, result!.Status); Assert.Equal(3, h.Handler.Actions);
            }
            await using (var h = new Harness(settings, miningOnly: true))
            {
                Assert.Equal(StrategyStatus.Completed, (await h.Runner.RunAsync(CancellationToken.None))!.Status);
                Assert.Equal(0, h.Handler.Actions);
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public async Task BoundedSecondExecutorDoesNotDispatch()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-owner-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var lease = new FileRunCheckpointStore(directory, "http://localhost/researcher");
            await using var h = new Harness(new() { Mode = "Bounded", AllowActions = true, RunId = "test", RunDirectory = directory }, miningOnly: true);
            Assert.Null(await h.Runner.RunAsync(CancellationToken.None)); Assert.Equal(0, h.Handler.Actions);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("Inspect", 0)]
    [InlineData("OneShot", 1)]
    public async Task MiningOnlyIgnoresCombatModelAndCombatCatalogs(string mode, int actions)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true;
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal("skill:mining", result.Candidate);
        Assert.Single(result.Candidates);
        Assert.Equal(actions, h.Handler.Actions);
        Assert.DoesNotContain("/items", h.Handler.Reads);
        Assert.DoesNotContain("/monsters", h.Handler.Reads);
    }

    [Fact]
    public async Task MiningOnlyMovesThenGathersWithoutCombatNormalization()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true;
        var run = h.Factory.Create(new([new("mining", 2, 30)], 0, "", ""));
        Assert.Equal("Move:4", (await run.TickAsync()).Command);
        Assert.Equal("Gather:mining", (await run.TickAsync()).Command);
        Assert.Equal("Gather:mining", (await run.TickAsync()).Command);
        Assert.Equal(StrategyStatus.Completed, (await run.TickAsync()).Status);
        Assert.Equal(3, h.Handler.Actions);
        Assert.Equal(2, run.State!.Character.GetProperty("mining_level").GetInt32());
    }

    [Theory]
    [InlineData("missing-skill")]
    [InlineData("inventory")]
    [InlineData("access")]
    [InlineData("stale")]
    [InlineData("skill-schema")]
    public async Task MiningOnlyInvalidInputsNeverDispatch(string corruption)
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }, miningOnly: true);
        await h.Reset(); h.Handler.MiningOnly = true; h.Handler.Corruption = corruption;
        Assert.Equal(StrategyStatus.Blocked, (await h.Runner.RunAsync(CancellationToken.None))!.Status);
        Assert.Equal(0, h.Handler.Actions);
    }

    [Theory]
    [InlineData("Inspect", false, 0)]
    [InlineData("OneShot", true, 1)]
    public async Task StagedModesHaveExplicitActionBudgetAndExpiringReadiness(string mode, bool allow, int actions)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = allow }); await h.Reset();
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.NotNull(result); Assert.Equal(StrategyStatus.Selected, result.Status);
        Assert.Equal(actions, h.Handler.Actions); Assert.True(h.State.Snapshot(30).Ready);
        h.Clock.Advance(31); Assert.False(h.State.Snapshot(30).Ready); Assert.Equal("StaleObservation", h.State.Snapshot(30).State);
    }
    [Theory]
    [InlineData("nonsense", true, "http://localhost")]
    [InlineData("OneShot", false, "http://localhost")]
    [InlineData("OneShot", true, "https://api.artifactsmmo.com")]
    [InlineData("OneShot", true, "https://other.invalid")]
    [InlineData("Inspect", false, "http://api.artifactsmmo.com")]
    public async Task InvalidModeConsentOrOriginDispatchesNothing(string mode, bool allow, string url)
    {
        await using var h = new Harness(new() { Mode = mode, AllowActions = allow }, url);
        Assert.Null(await h.Runner.RunAsync(CancellationToken.None)); Assert.Equal(0, h.Handler.Actions);
        Assert.Equal("ConfigurationRequiredOrInvalid", h.State.Snapshot(30).State); Assert.False(h.State.Snapshot(30).Ready);
    }
    [Theory]
    [InlineData("version")]
    [InlineData("schema")]
    [InlineData("route")]
    [InlineData("malformed")]
    [InlineData("stale")]
    public async Task DriftAndStaleObservationBlockBeforeDispatch(string corruption)
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        h.Handler.Corruption = corruption;
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.Equal(StrategyStatus.Blocked, result!.Status); Assert.Equal(0, h.Handler.Actions); Assert.False(h.State.Snapshot(30).Ready);
        Assert.Equal(corruption == "stale" ? "StaleObservation" : "ApiContractUnavailableOrDrift", h.State.Snapshot(30).State);
    }
    [Fact]
    public async Task OneShotLostResponseOnlyReconcilesReadOnly()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        h.Handler.Corruption = "loss";
        var result = await h.Runner.RunAsync(CancellationToken.None);
        Assert.Equal(StrategyStatus.Reconciled, result!.Status); Assert.Equal(1, h.Handler.Actions);
    }
    [Fact]
    public async Task OneShotCancelledDuringAuthNeverDispatchesAction()
    {
        await using var h = new Harness(new() { Mode = "OneShot", AllowActions = true }); await h.Reset();
        using var cancel = new CancellationTokenSource(); h.Handler.Cancel = cancel;
        var result = await h.Runner.RunAsync(cancel.Token);
        Assert.Equal(StrategyStatus.Cancelled, result!.Status); Assert.Equal(0, h.Handler.Actions); Assert.False(h.State.Snapshot(30).Ready);
    }
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 6, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds) => _now = _now.AddSeconds(seconds);
    }
    private sealed class Harness : IAsyncDisposable
    {
        private readonly MockServiceFactory _factory = new();
        private readonly HttpClient _transport;
        public readonly Clock Clock = new();
        public readonly Handler Handler;
        public readonly OperationState State;
        public readonly StagedExecution Runner;
        public readonly StrategySessionFactory Factory;
        public Harness(ExecutionSettings execution, string origin = "http://localhost", bool miningOnly = false)
        {
            State = new(Clock); Handler = new(_factory.Server.CreateHandler(), Clock);
            _transport = new(Handler);
            var api = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
            var http = new GameHttpClient(new ClientFactory(_transport), api, Clock, (_, _) => Task.CompletedTask);
            var client = new GameClient(http, api, NullLogger<IGameClient>.Instance, new EmptyCache(), new ActivitySource("Staged"));
            var factory = new StrategySessionFactory(client, new CombatCatalog(http), new CharacterService(), new NoDelay(), new ApiCompatibility(http, execution, State, Clock));
            Factory = factory;
            api.BaseUrl = origin;
            Runner = new(execution, api, miningOnly ? new() { Skills = [new("mining", 2, 30)] } : new() { Skills = [new("mining", 2, 30), new("woodcutting", 2, 20)], CombatTarget = 2, Monster = "dummy", Equipment = "quick_blade" }, factory, State);
        }
        public async Task Reset()
        {
            using var response = await _transport.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"strategy-portfolio\"}", Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        public async ValueTask DisposeAsync() { _transport.Dispose(); await _factory.DisposeAsync(); }
    }
    private sealed class Handler(HttpMessageHandler inner, Clock clock) : DelegatingHandler(inner)
    {
        public int Actions;
        public bool MiningOnly;
        public List<string> Reads = [];
        public string? Corruption;
        public CancellationTokenSource? Cancel;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get) Reads.Add(path);
            if (MiningOnly && (path.StartsWith("/characters/", StringComparison.Ordinal) || path.Contains("/action/")))
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                var character = path.Contains("/action/") ? node["data"]!["character"]! : node["data"]!;
                character["attack_water"] = 25;
                if (Corruption == "missing-skill") character.AsObject().Remove("mining_xp");
                if (Corruption == "inventory") character["inventory_max_items"] = character["inventory"]!.AsArray().Sum(x => x!["quantity"]!.GetValue<int>());
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && path == "/openapi.json")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var action in new[] { "fight", "rest", "equip", "unequip", "crafting" })
                    node["paths"]!.AsObject().Remove("/my/{name}/action/" + action);
                node["paths"]!.AsObject().Remove("/items"); node["paths"]!.AsObject().Remove("/monsters");
                node["components"]!["schemas"]!["MapLayer"] = JsonNode.Parse("{\"type\":\"string\",\"enum\":[\"overworld\",\"underground\",\"interior\"]}");
                foreach (var schema in new[] { "CharacterSchema", "MapSchema" })
                    node["components"]!["schemas"]![schema]!["properties"]!["layer"] = JsonNode.Parse("{\"$ref\":\"#/components/schemas/MapLayer\"}");
                if (Corruption == "skill-schema") node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!.AsObject().Remove("mining_xp");
                response.Content = new StringContent(node.ToJsonString());
            }
            if (MiningOnly && path == "/maps" && Corruption == "access")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                foreach (var map in node["data"]!.AsArray()) map!["access"]!["type"] = "conditional";
                response.Content = new StringContent(node.ToJsonString());
            }
            if (path == "/token") Cancel?.Cancel();
            if (path.Contains("/action/"))
            { Actions++; if (Corruption == "loss") { response.Dispose(); throw new HttpRequestException(); } }
            if (path.StartsWith("/characters/", StringComparison.Ordinal) && Corruption == "stale") clock.Advance(31);
            if (path == "/openapi.json" && Corruption is "version" or "schema" or "route" or "malformed")
            {
                var node = JsonNode.Parse(await response.Content!.ReadAsStringAsync(token))!;
                if (Corruption == "version") node["info"]!["version"] = "changed";
                if (Corruption == "schema") node["components"]!["schemas"]!["CharacterSchema"]!["properties"]!["hp"]!["type"] = "string";
                if (Corruption == "route") node["paths"]!.AsObject().Remove("/my/{name}/action/fight");
                response.Content = new StringContent(Corruption == "malformed" ? "{" : node.ToJsonString());
            }
            return response;
        }
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class EmptyCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => Task.CompletedTask;
    }
    private sealed class NoDelay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
}
