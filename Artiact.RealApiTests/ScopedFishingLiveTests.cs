using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Artiact;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Artiact.Services.Combat;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Artiact.RealApiTests;

public class ScopedFishingLiveTests(ITestOutputHelper output)
{
    private const string RunId = "gllab-fishing2-20260909-v1";

    [Fact]
    [Trait("Category", "FishingResultReview")]
    public async Task RetainedTerminalReopensOfflineWithoutObservingOrDispatching()
    {
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".artiact-runs", RunId);
        RunCheckpoint saved;
        using (var store = new FileRunCheckpointStore(directory, "https://api.artifactsmmo.com/gllab")) saved = store.Load()!;
        Assert.NotNull(saved.Terminal); Assert.Null(saved.PendingCommand);
        var memory = new SnapshotStore(saved);
        var reopened = await new StrategySession(new NoObservation(), [], new MiningCooldownDelay(), new(20, 3, 16, 900),
            checkpoints: memory, identity: saved.Identity, autonomous: true).TickAsync();
        Assert.Equal(saved.Terminal.Status, reopened.Status); Assert.Equal(saved.Terminal.Reason, reopened.Reason);
        Assert.Equal(saved.Attempts, reopened.Attempts);
        var last = saved.Latest!.Character;
        output.WriteLine(JsonSerializer.Serialize(new { RunId, saved.Started, saved.Finished,
            Status = reopened.Status.ToString(), reopened.Reason, saved.Attempts, saved.Decisions, saved.NoProgress,
            Verified = saved.Journal.Count(x => x.Status == "Verified"), Rejected = saved.Journal.Count(x => x.Status == "Rejected"),
            CooldownSeconds = saved.Seconds, FishingLevel = last.GetProperty("fishing_level").GetInt32(),
            FishingXp = last.GetProperty("fishing_xp").GetInt32(), Stock = CharacterObservation.Read(last)!.Inventory,
            InitialWorld = saved.Initial!.Restore().WorldFingerprint, LatestWorld = saved.Latest.Restore().WorldFingerprint,
            CanArchive = FileRunCheckpointStore.CanArchive(saved), ReopenedWithoutObservation = true }));
    }

    private sealed class SnapshotStore(RunCheckpoint saved) : IRunCheckpointStore
    {
        public RunCheckpoint? Load() => saved;
        public void Save(RunCheckpoint checkpoint) { }
    }
    private sealed class NoObservation : IStrategyObserver
    {
        public Task<StrategyObservation> ObserveAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Offline terminal review must not observe.");
    }

    [Fact]
    [Trait("Category", "RealApiFishingLive")]
    public async Task ApprovedFishingMilestoneAndTerminalReopen()
    {
        if (Environment.GetEnvironmentVariable("ARTIACT_FISHING_LIVE_APPROVAL") != "gllab:fishing2:385:16:900:20260909-v1")
            throw new InvalidOperationException("Exact fishing scope approval required.");
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Artiact.sln"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root unavailable.");
        var config = RealApiConfiguration.Resolve(DotenvParser.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, ".env"))));
        if (config.Character != "gllab") throw new InvalidOperationException("Scoped character mismatch.");
        var origin = DestinationValidator.Validate(config.BaseUri);
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".artiact-runs", RunId);
        Directory.CreateDirectory(directory);
        if (Directory.EnumerateFileSystemEntries(directory).Any()) throw new InvalidOperationException("Existing trial must not be replayed.");
        using (var marker = new FileStream(Path.Combine(directory, "host-started.intent"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { marker.Write("Approved fishing trial started; retain this marker.\n"u8); marker.Flush(true); }

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        using var http = new HttpClient(ReadOnlyApiVerifier.CreatePrimaryHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        var verifier = new ReadOnlyApiVerifier(http);
        var report = await verifier.InspectReportAsync(config, deadline.Token, true, [new(20, 3, 16, 900)]);
        var preview = report.BudgetMatrix![0].Decision;
        if (report.Character != "gllab" || report.MapId != 379 || preview.Status != StrategyStatus.Selected || preview.Command != "Move:385" ||
            preview.Candidate != "skill:fishing:gudgeon_spot:intermediate" || preview.Attempts != 0)
            throw new InvalidOperationException("Fresh Inspect does not match approved fishing scope.");
        output.WriteLine(JsonSerializer.Serialize(new { Phase = "Inspect", report.ObservedAt, report.Fingerprint, report.WorldFingerprint,
            report.PolicyDigest, report.FreeUnits, report.Stock, preview.Candidate, preview.Command, report.GetRequests, report.AcquisitionSeconds }));

        string bearer = await verifier.AuthenticateAsync(origin, config, deadline.Token);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, HttpContent? content)
        {
            using var request = new HttpRequestMessage(method, new Uri(origin, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            if (content is not null) request.Content = new StringContent(await content.ReadAsStringAsync(), System.Text.Encoding.UTF8, "application/json");
            return await verifier.SendAsync(request, "scoped-fishing", method == HttpMethod.Post ? CancellationToken.None : deadline.Token);
        }
        var state = new OperationState();
        var portfolio = new PortfolioSettings { AutonomousGoals = true };
        var reads = new InspectionTransport("gllab", path => Send(HttpMethod.Get, path, null), includeItems: true);
        var transport = new ScopedFishingTransport(reads, report.Fingerprint!, report.WorldFingerprint!, portfolio.Policy().Identity,
            (path, body) => Send(HttpMethod.Post, path, body), state.RequestStop);
        var api = new ApiSettings { BaseUrl = origin.AbsoluteUri, Character = "gllab", Username = "external-auth", Password = "external-auth" };
        var settings = new ExecutionSettings { Mode = "Bounded", AllowActions = true, LiveActionsApproved = true, FreshnessSeconds = 30,
            RunId = RunId, RunDirectory = directory, MaxActions = 16, MaxDecisions = 20, MaxNoProgress = 3, MaxSeconds = 900 };
        using var activity = new ActivitySource("Artiact.ScopedFishing");
        StrategySessionFactory Factory()
        {
            var client = new GameClient(transport, api, NullLogger<IGameClient>.Instance, new NoCache(), activity);
            return new(client, new CombatCatalog(transport), new CharacterService(), new MiningCooldownDelay(), new ApiCompatibility(transport, settings, state));
        }
        try
        {
            var result = await new StagedExecution(settings, api, portfolio, Factory(), state).RunAsync(deadline.Token);
            await transport.SealAsync();
            int sent = transport.Sent;
            Assert.InRange(sent, 1, 16);
            using (var store = new FileRunCheckpointStore(directory, origin.GetLeftPart(UriPartial.Authority) + "/gllab"))
            {
                var saved = store.Load()!;
                Assert.NotNull(saved.Terminal); Assert.Null(saved.PendingCommand);
                Assert.All(saved.Journal, entry => Assert.Equal("Verified", entry.Status));
                var last = saved.Latest!.Character;
                output.WriteLine(JsonSerializer.Serialize(new { Phase = "Terminal", RunId, Sent = sent, result!.Reason,
                    Status = result.Status.ToString(), saved.Decisions, saved.NoProgress, CooldownSeconds = saved.Seconds,
                    Map = last.GetProperty("map_id").GetInt32(), FishingLevel = last.GetProperty("fishing_level").GetInt32(),
                    FishingXp = last.GetProperty("fishing_xp").GetInt32(), Stock = CharacterObservation.Read(last)!.Inventory }));
                Assert.Equal(2, last.GetProperty("fishing_level").GetInt32());
            }
            var reopened = await new StagedExecution(settings, api, portfolio, Factory(), new OperationState()).RunAsync(deadline.Token);
            Assert.Equal(sent, transport.Sent); Assert.Equal(sent, reopened!.Attempts);
            Assert.Equal(result!.Reason, reopened.Reason); Assert.Equal(result.Status, reopened.Status);
            output.WriteLine($"Terminal reopened without another POST; sent={sent}; directory={directory}");
        }
        finally { await transport.SealAsync(); state.RequestStop(); }
    }

    private sealed class NoCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => throw new InvalidOperationException("No live cache writes.");
    }
}
