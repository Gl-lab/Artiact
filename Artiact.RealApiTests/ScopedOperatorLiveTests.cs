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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Artiact.RealApiTests;

public class ScopedOperatorLiveTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "RealApiOperatorLive")]
    public async Task ExplicitScopedOperatorCycle()
    {
        ScopedOperatorHost.RequireApproval(Environment.GetEnvironmentVariable("ARTIACT_OPERATOR_LIVE_APPROVAL"));
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Artiact.sln"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root unavailable.");
        var config = RealApiConfiguration.Resolve(DotenvParser.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, ".env"))));
        if (config.Character != "gllab") throw new InvalidOperationException("Scoped character mismatch.");
        var origin = DestinationValidator.Validate(config.BaseUri);
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".artiact-runs", ScopedOperatorHost.RunId);
        Directory.CreateDirectory(directory);
        if (Directory.EnumerateFileSystemEntries(directory).Any()) throw new InvalidOperationException("Existing trial directory must not be reused.");
        using (var marker = new FileStream(Path.Combine(directory, "host-started.intent"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { marker.Write("Scoped operator harness started; never remove to replay.\n"u8); marker.Flush(true); }

        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
        using var http = new HttpClient(ReadOnlyApiVerifier.CreatePrimaryHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        var verifier = new ReadOnlyApiVerifier(http);
        string bearer = await verifier.AuthenticateAsync(origin, config, deadline.Token);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, HttpContent? content)
        {
            using var request = new HttpRequestMessage(method, new Uri(origin, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            // Copy content because HttpRequestMessage owns it; the caller retains its original.
            if (content is not null) request.Content = new StringContent(await content.ReadAsStringAsync(), System.Text.Encoding.UTF8, "application/json");
            return await verifier.SendAsync(request, "scoped-operator", method == HttpMethod.Post ? CancellationToken.None : deadline.Token);
        }
        var state = new OperationState();
        var portfolio = new PortfolioSettings { AutonomousGoals = true };
        var policy = portfolio.Policy();
        var reads = new InspectionTransport("gllab", path => Send(HttpMethod.Get, path, null), includeItems: true);
        var transport = new ScopedAutonomousTransport(reads,
            "13D88836A152ACD94031164B9167CBAA879D42C3A80AE5E9128C41D54CA02488",
            "5B26B5CE443FE734ADBE9AD87E2B35D8534CB3235991B7686F4C3D21AD16C273",
            policy.Identity, (path, body) => Send(HttpMethod.Post, path, body), state.RequestStop);
        // Authentication is already performed by the pinned verifier; no credentials enter the host.
        var api = new ApiSettings { BaseUrl = origin.AbsoluteUri, Character = "gllab", Username = "external-auth", Password = "external-auth" };
        var settings = new ExecutionSettings
        {
            Mode = "Inspect", AllowActions = true, LiveActionsApproved = true, FreshnessSeconds = 30,
            RunId = ScopedOperatorHost.RunId, RunDirectory = directory,
            MaxActions = 16, MaxDecisions = 20, MaxNoProgress = 3, MaxSeconds = 900
        };
        using var activity = new ActivitySource("Artiact.ScopedOperator");
        var execution = new Execution(transport, api, portfolio, state, activity);
        await using var app = ScopedOperatorHost.Build(settings, api, portfolio, state, execution, "http://127.0.0.1:5189");
        await app.StartAsync(deadline.Token);
        output.WriteLine("Operator ready at http://127.0.0.1:5189/operator; Inspect then Start through the panel.");
        try
        {
            while (!execution.BoundedFinished) await Task.Delay(250, deadline.Token);
            await app.Services.GetRequiredService<OperatorCoordinator>().WaitForIdleAsync(deadline.Token);
            await transport.SealAsync();
            int sent = transport.Sent;
            Assert.InRange(sent, 1, 16);
            using (var beforeReopen = new FileRunCheckpointStore(directory, origin.GetLeftPart(UriPartial.Authority) + "/gllab"))
            {
                var terminal = beforeReopen.Load();
                Assert.NotNull(terminal?.Terminal);
                Assert.Null(terminal.PendingCommand);
                Assert.Contains(terminal.Terminal.Status, new[] { StrategyStatus.Cancelled, StrategyStatus.Stopped, StrategyStatus.Completed, StrategyStatus.Blocked });
            }
            var bounded = new ExecutionSettings
            {
                Mode = "Bounded", AllowActions = true, LiveActionsApproved = true, FreshnessSeconds = 30,
                RunId = settings.RunId, RunDirectory = directory, MaxActions = 16, MaxDecisions = 20, MaxNoProgress = 3, MaxSeconds = 900
            };
            var reopened = await execution.ExecuteAsync(bounded, deadline.Token);
            Assert.Equal(sent, transport.Sent);
            Assert.NotNull(reopened);
            Assert.Contains(reopened.Status, new[] { StrategyStatus.Cancelled, StrategyStatus.Stopped, StrategyStatus.Completed, StrategyStatus.Blocked });
            Assert.Equal(sent, reopened.Attempts);
            using var store = new FileRunCheckpointStore(directory, origin.GetLeftPart(UriPartial.Authority) + "/gllab");
            var saved = store.Load()!;
            Assert.Null(saved.PendingCommand);
            Assert.All(saved.Journal, entry => Assert.Equal("Verified", entry.Status));
            output.WriteLine(JsonSerializer.Serialize(new { RunId = settings.RunId, Sent = sent, Decision = reopened, JournalEntries = saved.Journal.Length }));
        }
        finally { await app.StopAsync(CancellationToken.None); }
    }

    private sealed class Execution(ScopedAutonomousTransport transport, ApiSettings api, PortfolioSettings portfolio,
        OperationState state, ActivitySource activity) : IOperatorExecution
    {
        private volatile bool _boundedFinished;
        public bool BoundedFinished => _boundedFinished;
        public async Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token)
        {
            if (settings.RunId != ScopedOperatorHost.RunId || settings.MaxActions != 16 || settings.MaxSeconds != 900 ||
                settings.MaxDecisions != 20 || settings.MaxNoProgress != 3)
                throw new ArgumentException("Exact reviewed field-trial scope required.");
            try
            {
                var client = new GameClient(transport, api, NullLogger<IGameClient>.Instance, new NoCache(), activity);
                var factory = new StrategySessionFactory(client, new CombatCatalog(transport), new CharacterService(),
                    new MiningCooldownDelay(), new ApiCompatibility(transport, settings, state));
                return await new StagedExecution(settings, api, portfolio, factory, state).RunAsync(token);
            }
            finally { if (settings.Mode == "Bounded") _boundedFinished = true; }
        }
    }

    private sealed class NoCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => throw new InvalidOperationException("No field-trial cache writes.");
    }
}
