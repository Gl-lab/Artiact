using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Artiact.Models;
using Artiact.Services;
using Artiact.SmartProxy.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public class MiningProgressionFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiveCyclesSixActionsAndThirtyFourSecondsReplayThroughRealClient(bool worker)
    {
        await using MockServiceFactory factory = new();
        List<string> replays = [];
        for(int replay = 0; replay < 2; replay++)
        {
            using Harness h = new(factory);
            await h.Reset();
            using var scope = h.Provider.CreateScope();
            var action = scope.ServiceProvider.GetRequiredService<IActionService>();
            List<GoalDecision> decisions = [];
            if(worker)
            {
                var bounded = new BoundedDriver(action, decisions);
                using var workerProvider = new ServiceCollection().AddSingleton<IActionService>(bounded).BuildServiceProvider();
                using var service = new ArtiactBackgroundService(NullLogger<ArtiactBackgroundService>.Instance, workerProvider,
                    (_, _) => throw new InvalidOperationException("No recovery delay expected."));
                await service.StartAsync(default); await service.ExecuteTask!;
            }
            else
            {
                await action.InitializeAsync(default);
                for(int cycle = 0; cycle < 5; cycle++)
                {
                    var decision = await action.ExecuteCycleAsync(default); decisions.Add(decision);
                    if(decision.Status != GoalDecisionStatus.Selected) break;
                }
            }
            Assert.Equal(new[] { GoalDecisionStatus.Selected, GoalDecisionStatus.Selected, GoalDecisionStatus.Selected, GoalDecisionStatus.Selected, GoalDecisionStatus.Completed }, decisions.Select(d => d.Status));
            Assert.Equal(new[] { 7, 5, 5, 7, 5, 5 }, h.Delay.Seconds);
            Assert.Equal(6, h.Handler.Actions.Count);
            for(int i = 0; i < 6; i++) ExpectedProgression.AssertAction(i, h.Handler.Actions[i]);
            var state = (await h.Transport.GetFromJsonAsync<StateSummary>("/__mock/state/MockHero"))!;
            var trace = (await h.Transport.GetFromJsonAsync<List<TraceEntry>>("/__mock/trace"))!;
            Assert.Equal(Enumerable.Range(0,6).Select(i => ExpectedProgression.Trace(i,replay + 1)), trace);
            Assert.Equal("2000-01-01T00:00:34.0000000Z", state.VirtualTime); Assert.Equal("Gathered", state.Phase);
            ScenarioAssertions.CharacterEquals(ExpectedProgression.Character(4,3,4,2,2), state.Character!);
            ScenarioAssertions.CharacterEquals(state.Character!, scope.ServiceProvider.GetRequiredService<ICharacterService>().GetCharacter());
            Assert.Equal(4, h.Cache.SaveCount); Assert.Equal(4, h.Handler.Paths.Count(p => p is "/resources" or "/maps" or "/items" or "/monsters"));
            Assert.Equal(5, h.Logger.Events.Count);
            Assert.Equal(new[] { "copper_rocks", "copper_rocks", "iron_rocks", "iron_rocks" }, h.Logger.Events.Take(4).Select(e => e["goal.mining.resource_code"]));
            for(int i = 0; i < 4; i++)
            {
                Assert.Equal(i + 1, h.Logger.Events[i]["goal.mining.attempted_cycles"]);
                Assert.Equal(10, h.Logger.Events[i]["goal.mining.max_cycles"]);
                Assert.Equal(0, h.Logger.Events[i]["goal.mining.consecutive_no_progress"]);
            }
            Assert.Equal(4, h.Logger.Events[4].Count);
            replays.Add(JsonSerializer.Serialize(new { State = state with { Generation = 0 },
                Trace = trace.Select(t => t with { Generation = 0 }), Actions = h.Handler.Actions,
                Events = h.Logger.Events, Waits = h.Delay.Seconds, Decisions = decisions }));
        }
        Assert.Equal(replays[0], replays[1]);
    }

    [Fact]
    public async Task OneCycleBudgetStopsAfterOneMoveAndGather()
    {
        await using MockServiceFactory factory = new(); using Harness h = new(factory, 1, 1);
        await h.Reset(); using var scope = h.Provider.CreateScope(); var action = scope.ServiceProvider.GetRequiredService<IActionService>();
        await action.InitializeAsync(default);
        Assert.Equal(GoalDecisionStatus.Selected, (await action.ExecuteCycleAsync(default)).Status);
        Assert.Equal(GoalDecisionReason.MiningCycleLimit, (await action.ExecuteCycleAsync(default)).Reason);
        Assert.Equal(2, h.Handler.Actions.Count); Assert.Equal(new[] { 7, 5 }, h.Delay.Seconds);
        ScenarioAssertions.CharacterEquals(ExpectedProgression.Character(2,1,6,1), scope.ServiceProvider.GetRequiredService<ICharacterService>().GetCharacter());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NullResourcePayloadIsLoadingFailureWithoutFabricatedDecision(bool cold)
    {
        await using MockServiceFactory factory = new(); using Harness h = new(factory, 1, 1);
        await h.Reset(); using var scope = h.Provider.CreateScope(); var action = scope.ServiceProvider.GetRequiredService<IActionService>();
        if(!cold) await action.InitializeAsync(default);
        h.Cache.Clear(); h.Handler.NullResources = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => cold ? action.InitializeAsync(default) : action.ExecuteCycleAsync(default));
        Assert.Empty(h.Logger.Events); Assert.Empty(h.Handler.Actions); Assert.Empty(h.Delay.Seconds);
        Assert.Equal(cold ? 0 : 1, scope.ServiceProvider.GetRequiredService<MiningRunState>().AttemptedCycles);
        if(cold) Assert.DoesNotContain("/characters/MockHero", h.Handler.Paths);
        else Assert.Equal(GoalDecisionReason.MiningCycleLimit, (await action.ExecuteCycleAsync(default)).Reason);
    }

    [Theory]
    [InlineData("target", GoalDecisionReason.MiningTargetReached)]
    [InlineData("pressure", GoalDecisionReason.InventoryPressure)]
    [InlineData("invalid", GoalDecisionReason.InvalidInventorySnapshot)]
    public async Task InitialTerminalStateMakesNoActionAndWorkerHasNoRecovery(string condition, GoalDecisionReason expected)
    {
        await using MockServiceFactory factory = new(); using Harness h = new(factory);
        await h.Reset(); using var scope = h.Provider.CreateScope(); var action = scope.ServiceProvider.GetRequiredService<IActionService>();
        await action.InitializeAsync(default);
        var character = scope.ServiceProvider.GetRequiredService<ICharacterService>().GetCharacter();
        if(condition == "target") character.MiningLevel = 3;
        else if(condition == "pressure") character.InventoryMaxItems = 9;
        else character.Inventory = null!;
        List<GoalDecision> decisions = [];
        using var provider = new ServiceCollection().AddSingleton<IActionService>(new BoundedDriver(action, decisions, initialized: true)).BuildServiceProvider();
        using var worker = new ArtiactBackgroundService(NullLogger<ArtiactBackgroundService>.Instance, provider,
            (_,_) => throw new InvalidOperationException("Unexpected recovery."));
        await worker.StartAsync(default); await worker.ExecuteTask!;
        Assert.Equal(expected, Assert.Single(decisions).Reason); Assert.Empty(h.Handler.Actions); Assert.Empty(h.Delay.Seconds);
    }

    [Theory]
    [InlineData("unchanged", GoalDecisionReason.MiningNoProgress, 4, 4)]
    [InlineData("wrong_move", GoalDecisionReason.MiningDestinationNotReached, 2, 1)]
    [InlineData("pressure", GoalDecisionReason.InventoryPressure, 2, 2)]
    [InlineData("invalid_xp", GoalDecisionReason.InvalidMiningProgress, 2, 2)]
    public async Task ControlledResponsesTerminateWorkerWithoutRecovery(string change, GoalDecisionReason reason, int cycleCount, int actionCount)
    {
        await using MockServiceFactory factory = new(); using Harness h = new(factory);
        await h.Reset(); h.Handler.ChangeResponse = change;
        using var scope = h.Provider.CreateScope(); var action = scope.ServiceProvider.GetRequiredService<IActionService>();
        List<GoalDecision> decisions = [];
        using var provider = new ServiceCollection().AddSingleton<IActionService>(new BoundedDriver(action, decisions)).BuildServiceProvider();
        using var worker = new ArtiactBackgroundService(NullLogger<ArtiactBackgroundService>.Instance, provider,
            (_,_) => throw new InvalidOperationException("Terminal decision must not recover."));
        await worker.StartAsync(default); await worker.ExecuteTask!;
        Assert.Equal(cycleCount, decisions.Count); Assert.Equal(reason, decisions.Last().Reason);
        Assert.Equal(actionCount, h.Handler.Actions.Count); Assert.Equal(actionCount, h.Delay.Seconds.Count);
    }

    [Fact]
    public async Task ControlledCooldownCancellationRetainsMoveAndStartsNoGather()
    {
        await using MockServiceFactory factory = new(); using Harness h = new(factory);
        await h.Reset(); using var scope = h.Provider.CreateScope(); var action = scope.ServiceProvider.GetRequiredService<IActionService>();
        await action.InitializeAsync(default); using CancellationTokenSource stop = new();
        h.Delay.OnWait = () => stop.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => action.ExecuteCycleAsync(stop.Token));
        Assert.Single(h.Handler.Actions); Assert.Equal(new[] { 7 }, h.Delay.Seconds);
        ScenarioAssertions.CharacterEquals(ExpectedProgression.Character(2), scope.ServiceProvider.GetRequiredService<ICharacterService>().GetCharacter());
        Assert.Equal(1, scope.ServiceProvider.GetRequiredService<MiningRunState>().AttemptedCycles);
    }

    private sealed class BoundedDriver(IActionService action, List<GoalDecision> decisions, bool initialized = false) : IActionService
    {
        public Task InitializeAsync(CancellationToken token) => initialized ? Task.CompletedTask : action.InitializeAsync(token);
        public async Task<GoalDecision> ExecuteCycleAsync(CancellationToken token)
        {
            Assert.True(decisions.Count < 5, "Unexpected sixth cycle.");
            var decision = await action.ExecuteCycleAsync(token); decisions.Add(decision); return decision;
        }
    }

    private sealed class Harness : IDisposable
    {
        public RecordingHandler Handler { get; }
        public HttpClient Transport { get; }
        public MemoryCache Cache { get; } = new();
        public InstantDelay Delay { get; } = new();
        public EventLogger Logger { get; } = new();
        public ServiceProvider Provider { get; }
        private readonly ActivitySource _source = new("ProgressionFlow");
        public Harness(MockServiceFactory factory, int cycles = 10, int noProgress = 3)
        {
            Handler = new(factory.Server.CreateHandler());
            Transport = new(Handler) { BaseAddress = MockServiceFactory.RequireLoopbackAuthority(new Uri("http://localhost")) };
            ApiSettings settings = new() { BaseUrl = "http://localhost", Username = "mock-user", Password = "mock-password", Character = "MockHero" };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["GoalSelection:MiningTargetLevel"] = "3", ["MiningProgression:MaxCycles"] = cycles.ToString(),
                ["MiningProgression:MaxConsecutiveNoProgress"] = noProgress.ToString()
            }).Build();
            var services = new ServiceCollection().AddLogging().AddGoalSelection(configuration).AddMiningProgression(configuration);
            services.AddSingleton(settings); services.AddSingleton(_source);
            services.AddSingleton<IHttpClientFactory>(new SingleClientFactory(Transport));
            services.AddSingleton<ICacheService>(Cache); services.AddScoped<IGameHttpClient,GameHttpClient>(); services.AddScoped<IGameClient,GameClient>();
            services.AddSingleton<IMiningCooldownDelay>(Delay); services.AddSingleton<ILogger<ActionService>>(Logger);
            services.AddScoped<ICharacterService,CharacterService>(); services.AddScoped<IStepBuilder,StepBuilder>();
            services.AddScoped<IMapService,MapService>(); services.AddScoped<IGoalDecomposer,GoalDecomposer>();
            services.AddScoped<ICraftTargetEvaluator,CraftTargetEvaluator>(); services.AddScoped<ICraftChainBuilder,CraftChainBuilder>();
            services.AddScoped<ITargetLootingResolver,TargetLootingResolver>(); services.AddScoped<IWearCraftTargetFinder,WearCraftTargetFinder>();
            services.AddScoped<IActionService,ActionService>(); Provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        }
        public async Task Reset()
        {
            using var resetClient = new HttpClient(Handler, disposeHandler: false) { BaseAddress = Transport.BaseAddress };
            (await resetClient.PostAsJsonAsync("/__mock/reset", new { scenario = "mining-progression" })).EnsureSuccessStatusCode();
        }
        public void Dispose() { Provider.Dispose(); Transport.Dispose(); _source.Dispose(); }
    }
    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class MemoryCache : ICacheService
    {
        private readonly Dictionary<Type,object> _values = [];
        public int SaveCount { get; private set; }
        public void Clear() => _values.Clear();
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult(_values.TryGetValue(typeof(T), out var value) ? (T)value : null);
        public Task SaveToCache<T>(T data) where T : class { _values[typeof(T)] = data; SaveCount++; return Task.CompletedTask; }
    }
    private sealed class InstantDelay : IMiningCooldownDelay
    {
        public List<int> Seconds { get; } = [];
        public Action? OnWait { get; set; }
        public Task WaitAsync(int seconds, CancellationToken token) { token.ThrowIfCancellationRequested(); Seconds.Add(seconds); OnWait?.Invoke(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    }
    private sealed class EventLogger : ILogger<ActionService>
    {
        public List<Dictionary<string,object?>> Events { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState,Exception?,string> formatter)
        { Assert.Equal("GoalDecision", id.Name); Events.Add(((IEnumerable<KeyValuePair<string,object?>>)state!).ToDictionary(p => p.Key,p => p.Value)); }
    }
    private sealed class RecordingHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public List<string> Paths { get; } = [];
        public List<ActionResponse> Actions { get; } = [];
        public bool NullResources { get; set; }
        public string? ChangeResponse { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Assert.Equal("http://localhost", request.RequestUri!.GetLeftPart(UriPartial.Authority));
            Paths.Add(request.RequestUri.AbsolutePath);
            if(NullResources && request.RequestUri.AbsolutePath == "/resources")
                return new(HttpStatusCode.OK) { Content = new StringContent("{\"data\":null,\"page\":1,\"pages\":1,\"size\":0,\"total\":0}", Encoding.UTF8, "application/json") };
            var response = await base.SendAsync(request, token);
            if(response.IsSuccessStatusCode && request.RequestUri.AbsolutePath.Contains("/action/", StringComparison.Ordinal))
            {
                var body = JsonSerializer.Deserialize<ActionResponse>(await response.Content!.ReadAsStringAsync(token))!;
                bool gather = request.RequestUri.AbsolutePath.EndsWith("/gathering", StringComparison.Ordinal);
                if(ChangeResponse == "wrong_move" && !gather) body.Data!.Character!.X = 0;
                if(ChangeResponse == "unchanged" && gather) { body.Data!.Character!.MiningLevel = 1; body.Data!.Character!.MiningXp = 0; }
                if(ChangeResponse == "pressure" && gather) body.Data!.Character!.Inventory![0].Quantity = 11;
                if(ChangeResponse == "invalid_xp" && gather) body.Data!.Character!.MiningXp = -1;
                if(ChangeResponse is not null)
                {
                    response.Content!.Dispose();
                    response.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                }
                Actions.Add(body);
            }
            return response;
        }
    }
}
