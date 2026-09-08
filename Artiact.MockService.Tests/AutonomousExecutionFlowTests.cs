using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;
using System.Collections.Immutable;
using System.Diagnostics;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Microsoft.Extensions.Logging.Abstractions;

namespace Artiact.MockService.Tests;

public class AutonomousExecutionFlowTests
{
    private sealed class Store : IRunCheckpointStore
    {
        public RunCheckpoint? Saved;
        public RunCheckpoint? Load() => Saved;
        public void Save(RunCheckpoint value) => Saved = value;
    }
    private sealed class FailingStore(Func<RunCheckpoint, bool> failure) : IRunCheckpointStore
    {
        public RunCheckpoint? Saved;
        public bool Failed;
        public RunCheckpoint? Load() => Saved;
        public void Save(RunCheckpoint checkpoint)
        {
            if (!Failed && failure(checkpoint)) { Failed = true; throw new IOException("Injected durable write failure"); }
            Saved = checkpoint;
        }
    }
    private sealed class ObservedStore(IRunCheckpointStore inner) : IRunCheckpointStore
    {
        public Exception? Failure;
        public RunCheckpoint? Load() { try { return inner.Load(); } catch (Exception ex) { Failure = ex; throw; } }
        public void Save(RunCheckpoint value) { try { inner.Save(value); } catch (Exception ex) { Failure = ex; throw; } }
    }
    private sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
    private sealed class Delay : IMiningCooldownDelay { public Task WaitAsync(int seconds, CancellationToken token) => Task.CompletedTask; }
    private sealed class Cache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult(default(T));
        public Task SaveToCache<T>(T value) where T : class => Task.CompletedTask;
    }
    private sealed class Transport(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        public int Posts, Moves;
        public bool LoseNext;
        public bool ChangeCatalog;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            bool action = request.RequestUri!.AbsolutePath.Contains("/action/", StringComparison.Ordinal);
            if (action)
            {
                Assert.True(request.RequestUri.AbsolutePath.EndsWith("/move", StringComparison.Ordinal) || request.RequestUri.AbsolutePath.EndsWith("/gathering", StringComparison.Ordinal));
                Posts++; if (request.RequestUri.AbsolutePath.EndsWith("/move", StringComparison.Ordinal)) Moves++;
            }
            var result = await base.SendAsync(request, token);
            if (ChangeCatalog && request.RequestUri.AbsolutePath == "/resources")
            {
                var body = JsonNode.Parse(await result.Content.ReadAsStringAsync(token))!;
                body["data"]![0]!["name"] = "Changed catalog revision";
                result.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            }
            if (action && LoseNext) { LoseNext = false; result.Dispose(); throw new HttpRequestException("Lost reply after acceptance"); }
            return result;
        }
    }
    private sealed class Harness : IDisposable
    {
        public readonly MockServiceFactory Mock = new();
        public readonly Transport Guard;
        public readonly HttpClient Http;
        public readonly GameClient Client;
        public readonly GameHttpClient GameHttp;
        public readonly CharacterService Characters = new();
        public readonly Store Store = new();
        public readonly PortfolioPolicy Policy;
        public Harness(bool bank = false)
        {
            Guard = new(Mock.Server.CreateHandler()); Http = new(Guard) { BaseAddress = new("http://localhost") };
            var settings = new ApiSettings { BaseUrl = "http://localhost", Character = "researcher", Username = "mock", Password = "mock" };
            GameHttp = new(new ClientFactory(Http), settings);
            Client = new(GameHttp, settings, NullLogger<IGameClient>.Instance, new Cache(), new ActivitySource("AutonomousFlow"));
            Policy = new PortfolioSettings { AutonomousGoals = true, BankRetain = bank ? new() { ["ore"] = 10 } : null }.Policy();
        }
        public async Task Reset(string scenario)
        {
            using var response = await Http.PostAsync("/__mock/reset", new StringContent(JsonSerializer.Serialize(new { scenario }), Encoding.UTF8, "application/json"));
            response.EnsureSuccessStatusCode();
        }
        public StrategySession Run(StrategyLimits? limits = null, IRunCheckpointStore? store = null) =>
            new StrategySessionFactory(Client, new CombatCatalog(GameHttp), Characters, new Delay()).Create(Policy, limits ?? new(40, 10, 20, 300), store ?? Store, "autonomous-test");
        public void Dispose() { Http.Dispose(); Mock.Dispose(); }
    }

    [Fact]
    public async Task TwoMilestonesPersistAcrossEveryTickAndBudgetStopSurvivesRestart()
    {
        using var h = new Harness(); await h.Reset("discovery-new");
        StrategyDecision? result = null;
        for (int i = 0; i < 10; i++)
        {
            result = await h.Run(new(40, 10, 6, 300)).TickAsync();
            if (result.Status == StrategyStatus.Stopped) break;
            Assert.Equal(StrategyStatus.Selected, result.Status);
        }
        Assert.Equal("AutonomousBudgetExhausted", result!.Reason);
        Assert.Equal(4, result.Attempts); Assert.Equal(20, result.CooldownSeconds);
        Assert.Equal(new[] { 2, 3 }, h.Store.Saved!.Autonomous!.History.Select(x => x.Goal.Target));
        Assert.All(h.Store.Saved.Autonomous.History, x => Assert.Equal("Completed", x.Outcome));
        Assert.Equal(2, h.Store.Saved.Version);
        Assert.Equal(result, await h.Run(new(40, 10, 6, 300)).TickAsync());
        Assert.Equal(4, h.Guard.Posts);
    }

    [Fact]
    public async Task LostReplyReconstructsOriginalGoalBeforeContinuing()
    {
        using var h = new Harness(); await h.Reset("discovery-new");
        h.Guard.LoseNext = true;
        Assert.Equal(StrategyStatus.UnknownOutcome, (await h.Run().TickAsync()).Status);
        Assert.Equal(2, h.Store.Saved!.Autonomous!.Active!.Target);
        Assert.Equal(StrategyStatus.Reconciled, (await h.Run().TickAsync()).Status);
        Assert.Equal(1, h.Guard.Posts);
        Assert.Empty(h.Store.Saved!.Measurements!);
        Assert.Equal(StrategyStatus.Selected, (await h.Run().TickAsync()).Status);
        Assert.Equal(2, h.Guard.Posts);
        Assert.Equal(2, Assert.Single(h.Store.Saved!.Autonomous!.History).Goal.Target);
    }

    [Fact]
    public async Task FailedSelectionSaveCannotDispatchAndPreservesDecisionCharge()
    {
        using var h = new Harness(); await h.Reset("discovery-new");
        var store = new FailingStore(c => c.Autonomous?.Active is not null && c.Attempts == 0);
        Assert.Equal("CheckpointUnavailableOrInvalid", (await h.Run(store: store).TickAsync()).Reason);
        Assert.Equal(0, h.Guard.Posts); Assert.Equal(1, store.Saved!.Decisions);
        Assert.Equal(StrategyStatus.Selected, (await h.Run(store: store).TickAsync()).Status);
        Assert.Equal(2, store.Saved!.Decisions); Assert.Equal(1, h.Guard.Posts);
    }

    [Fact]
    public async Task FailedCompletedTransitionSaveReconcilesWithoutDuplicatingPost()
    {
        using var h = new Harness(); await h.Reset("discovery-new");
        var store = new FailingStore(c => c.Autonomous?.History.Length == 1);
        Assert.Equal(StrategyStatus.Selected, (await h.Run(store: store).TickAsync()).Status);
        Assert.Equal("CheckpointUnavailableOrInvalid", (await h.Run(store: store).TickAsync()).Reason);
        Assert.Equal(2, h.Guard.Posts); Assert.NotNull(store.Saved!.PendingCommand);
        Assert.Equal(StrategyStatus.Reconciled, (await h.Run(store: store).TickAsync()).Status);
        Assert.Equal(2, h.Guard.Posts); Assert.Single(store.Saved!.Autonomous!.History);
    }

    [Fact]
    public async Task ChangedCatalogRejectsOldGoalAndSuppressesItsRegeneration()
    {
        using var h = new Harness(); await h.Reset("discovery-new");
        await h.Run().TickAsync(); h.Guard.ChangeCatalog = true;
        var result = await h.Run().TickAsync();
        Assert.Equal(StrategyStatus.Selected, result.Status); Assert.Equal("Move:4", result.Command);
        var rejected = Assert.Single(h.Store.Saved!.Autonomous!.History);
        Assert.Equal("Rejected", rejected.Outcome); Assert.Equal("woodcutting", rejected.Goal.Skill);
        Assert.Equal(1, result.NoProgress);
        Assert.Contains(result.Candidates, x => x.Rejection == "PreviouslyRejectedMilestone");
    }

    [Fact]
    public async Task CatalogChangeDuringUnknownPostCannotTriggerNewGoalOrPost()
    {
        using var h = new Harness(); await h.Reset("discovery-new"); h.Guard.LoseNext = true;
        await h.Run().TickAsync(); h.Guard.ChangeCatalog = true;
        Assert.Equal(StrategyStatus.UnknownOutcome, (await h.Run().TickAsync()).Status);
        Assert.Equal(1, h.Guard.Posts); Assert.Empty(h.Store.Saved!.Autonomous!.History);
        Assert.Equal("woodcutting", h.Store.Saved.Autonomous.Active!.Skill);
    }

    internal sealed class Lowest(PortfolioPolicy policy, StrategyActionPort port) : IProgressionStrategy
    {
        private SkillMilestone? _goal;
        public StrategyCandidate Evaluate(StrategyObservation state) => EvaluateAll(state).First();
        public IEnumerable<StrategyCandidate> EvaluateAll(StrategyObservation state)
        {
            if (_goal is null || state.Character.GetProperty(_goal.Skill + "_level").GetInt32() >= _goal.Target)
            {
                string skill = state.Catalogs["resources"].Select(x => x.GetProperty("skill").GetString()!).Distinct(StringComparer.Ordinal)
                    .Where(s => state.Character.TryGetProperty(s + "_level", out _))
                    .OrderBy(s => state.Character.GetProperty(s + "_level").GetInt32()).ThenBy(s => s, StringComparer.Ordinal).First();
                _goal = new(skill, state.Character.GetProperty(skill + "_level").GetInt32() + 1, 1);
            }
            var derived = policy with { Skills = [_goal] };
            return new FullPathStrategy(new ResourceAlternatives(_goal, derived, port), derived).EvaluateAll(state);
        }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("algorithm")]
    [InlineData("world")]
    public async Task IncompatibleCheckpointIsNotOverwrittenOrExecuted(string failure)
    {
        using var h = new Harness(); await h.Reset("discovery-new"); await h.Run().TickAsync();
        h.Store.Saved = failure switch
        {
            "version" => h.Store.Saved! with { Version = 1 },
            "algorithm" => h.Store.Saved! with { Autonomous = h.Store.Saved!.Autonomous! with { Algorithm = "future" } },
            _ => h.Store.Saved! with { Autonomous = h.Store.Saved!.Autonomous! with { ActiveWorld = null } }
        };
        var before = h.Store.Saved;
        Assert.Equal("CheckpointUnavailableOrInvalid", (await h.Run().TickAsync()).Reason);
        Assert.Same(before, h.Store.Saved); Assert.Equal(1, h.Guard.Posts);
    }

    [Theory]
    [InlineData("discovery-new", 6, 34, true)]
    [InlineData("discovery-bank", 6, 34, true)]
    [InlineData("discovery-uneven", 20, 104, true)]
    [InlineData("discovery-locked", 2, 10, true)]
    public async Task PreregisteredHttpComparisonsMeetCeilingsAndReplay(string scenario, int lowestActions, long lowestCooldown, bool lowestAchieves)
    {
        async Task<(int Actions, long Seconds, int Moves, bool Achieved, string State)> Execute(string strategy)
        {
            using var h = new Harness(scenario == "discovery-bank"); await h.Reset(scenario);
            int miningTarget = scenario is "discovery-uneven" or "discovery-locked" ? 10 : 2;
            var manual = h.Policy with { AutonomousGoals = false, Skills = [new("mining", miningTarget, 1), new("woodcutting", 2, 1)] };
            var observer = new HttpStrategyObserver(h.Client, new CombatCatalog(h.GameHttp), h.Characters, manual.Identity, profile: manual);
            var run = strategy == "auto" ? h.Run() : strategy == "fixed" ?
                new StrategySessionFactory(h.Client, new CombatCatalog(h.GameHttp), h.Characters, new Delay()).Create(manual, new(40, 10, 20, 300)) :
                new StrategySession(observer, [new Lowest(manual, new(h.Client, h.Characters))], new Delay(), new(40, 10, 20, 300), selection: manual.Measurement);
            string usefulSkill = scenario == "discovery-uneven" ? "mining" : "woodcutting";
            int usefulTarget = scenario == "discovery-uneven" ? 10 : 2;
            StrategyDecision? result = null; bool achieved = false;
            for (int i = 0; i < 40; i++)
            {
                result = await run.TickAsync();
                achieved = run.State?.Character.GetProperty(usefulSkill + "_level").GetInt32() >= usefulTarget;
                if (achieved || result.Status is StrategyStatus.Blocked or StrategyStatus.Stopped or StrategyStatus.Completed) break;
                Assert.Equal(StrategyStatus.Selected, result.Status);
            }
            if (scenario == "discovery-bank") Assert.Equal(10, run.State!.Bank!.Items["ore"]);
            return (result!.Attempts, result.CooldownSeconds, h.Guard.Moves, achieved, await h.Http.GetStringAsync("/__mock/state/researcher"));
        }
        var auto = await Execute("auto"); var fixedRun = await Execute("fixed"); var lowest = await Execute("lowest");
        Assert.Equal((2, 10L, 0, true), (auto.Actions, auto.Seconds, auto.Moves, auto.Achieved));
        Assert.Equal((2, 10L, 0, true), (fixedRun.Actions, fixedRun.Seconds, fixedRun.Moves, fixedRun.Achieved));
        Assert.Equal(lowestActions, lowest.Actions); Assert.Equal(lowestCooldown, lowest.Seconds); Assert.Equal(lowestAchieves, lowest.Achieved);
        Assert.Equal(auto, await Execute("auto")); Assert.Equal(fixedRun, await Execute("fixed")); Assert.Equal(lowest, await Execute("lowest"));
    }

    [Theory]
    [InlineData("discovery-capped", "NoUsefulSupportedGoals", 0)]
    [InlineData("discovery-new", "AutonomousBudgetExhausted", 2)]
    public async Task TerminalRunReportsArchivesAndCannotRefundBudget(string scenario, string reason, int actions)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-autonomous-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var h = new Harness(); await h.Reset(scenario);
            RunCheckpoint saved;
            using (var store = new FileRunCheckpointStore(directory, "researcher"))
            {
                var observed = new ObservedStore(store);
                StrategyDecision? result = null;
                for (int i = 0; i < 4; i++)
                {
                    result = await h.Run(new(40, 10, 2, 300), observed).TickAsync();
                    if (result.Status == StrategyStatus.Stopped) break;
                }
                Assert.True(reason == result!.Reason, $"Expected {reason}, actual {result.Reason}; store={observed.Failure}; checkpoint={JsonSerializer.Serialize(store.Load())}; now={DateTimeOffset.UtcNow:O}"); Assert.Equal(actions, result.Attempts);
                var restored = await h.Run(new(40, 10, 2, 300), observed).TickAsync();
                Assert.True(reason == restored.Reason, $"Expected {reason}, actual {restored.Reason}; store={observed.Failure}; started={store.Load()?.Started:O}; now={DateTimeOffset.UtcNow:O}; valid={store.Load()?.Autonomous?.Valid}");
                Assert.Equal(actions, h.Guard.Posts);
                saved = store.Load()!;
            }
            var output = new StringWriter();
            Assert.Equal(0, RunLifecycleCommand.Execute(["run-result", directory, "researcher"], output, new StringWriter()));
            using var report = JsonDocument.Parse(output.ToString());
            Assert.Equal(reason, report.RootElement.GetProperty("Terminal").GetProperty("Reason").GetString());
            Assert.False(report.RootElement.GetProperty("InterventionRequired").GetBoolean());
            using (var store = new FileRunCheckpointStore(directory, "researcher"))
            {
                var bytes = File.ReadAllBytes(Assert.Single(Directory.GetFiles(directory, "*.json")));
                var archive = store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(saved.Identity));
                Assert.Equal(bytes, File.ReadAllBytes(archive));
                Assert.Null(store.Load()); Assert.Throws<IOException>(() => store.Save(saved));
            }
        }
        finally
        {
            if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("unknown")]
    [InlineData("version")]
    [InlineData("counter")]
    [InlineData("state")]
    [InlineData("algorithm")]
    public async Task UnsafeAutonomousArchivePreservesCheckpoint(string failure)
    {
        using var h = new Harness(); await h.Reset("discovery-capped");
        await h.Run().TickAsync();
        var saved = h.Store.Saved!;
        saved = failure switch
        {
            "pending" => saved with { PendingCommand = "Gather:mining" },
            "unknown" => saved with { Terminal = saved.Terminal! with { Status = StrategyStatus.UnknownOutcome } },
            "version" => saved with { Version = 1 },
            "counter" => saved with { Attempts = 1 },
            "algorithm" => saved with { Autonomous = saved.Autonomous! with { Algorithm = "future" } },
            "state" => saved with { Latest = saved.Latest! with { Character = JsonSerializer.SerializeToElement(new { name = "researcher", map_id = 5, layer = "overworld", inventory_max_items = 100, inventory = Array.Empty<object>() }) } },
            _ => saved
        };
        string directory = Path.Combine(Path.GetTempPath(), "artiact-autonomous-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var store = new FileRunCheckpointStore(directory, "researcher"); store.Save(saved);
            var file = Assert.Single(Directory.GetFiles(directory, "*.json")); var bytes = File.ReadAllBytes(file);
            Assert.Throws<IOException>(() => store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(saved.Identity)));
            Assert.Equal(bytes, File.ReadAllBytes(file));
        }
        finally
        {
            if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
    [Fact]
    public async Task NewScenarioStartsAtPreregisteredWoodMapAndThreshold()
    {
        await using var factory = new MockServiceFactory();
        using var client = factory.CreateClient();
        using var reset = await client.PostAsync("/__mock/reset", new StringContent("{\"scenario\":\"discovery-new\"}", Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        using var state = JsonDocument.Parse(await client.GetStringAsync("/characters/researcher"));
        Assert.Equal(5, state.RootElement.GetProperty("data").GetProperty("map_id").GetInt32());
        Assert.Equal(26, state.RootElement.GetProperty("data").GetProperty("woodcutting_max_xp").GetInt32());
    }
}
