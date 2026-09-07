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

public class BoundedGatherLiveTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "RealApiBounded")]
    public async Task GatherResumeAndStopWithDurableBudget()
    {
        if (Environment.GetEnvironmentVariable("ARTIACT_BOUNDED_ROLLOUT") != "gllab:mining2:max4")
            throw new InvalidOperationException("Explicit bounded gathering opt-in required.");
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Artiact.sln"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root unavailable.");
        var config = RealApiConfiguration.Resolve(DotenvParser.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, ".env"))));
        if (config.Character != "gllab") throw new InvalidOperationException("Unsupported rollout character.");
        var origin = DestinationValidator.Validate(config.BaseUri);
        string directory = Path.Combine(root.FullName, ".artiact-runs", "gllab-mining2-rollout");
        const string owner = "https://api.artifactsmmo.com/gllab";
        const string identity = "gllab-mining2-max4-600s-v1";
        var limits = new StrategyLimits(20, 3, 4, 600);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(600));
        using var http = new HttpClient(ReadOnlyApiVerifier.CreatePrimaryHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        var verifier = new ReadOnlyApiVerifier(http);
        string token = await verifier.AuthenticateAsync(origin, config, deadline.Token);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path)
        {
            using var request = new HttpRequestMessage(method, new Uri(origin, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            try
            {
                var response = await verifier.SendAsync(request, "bounded-gather", method == HttpMethod.Post ? CancellationToken.None : deadline.Token);
                if (!response.IsSuccessStatusCode) output.WriteLine($"HTTP failure method={method} status={(int)response.StatusCode}");
                return response;
            }
            catch (RealApiVerificationException ex) { output.WriteLine(ex.Message); throw; }
        }
        var reads = new InspectionTransport("gllab", path => Send(HttpMethod.Get, path));
        int sent = 0;
        var transport = new BoundedGatherTransport(reads, async () =>
        {
            // Keep the field trial at the already verified copper site.
            using var response = await reads.GetAsync("/characters/gllab");
            response.EnsureSuccessStatusCode();
            using var snapshot = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (snapshot.RootElement.GetProperty("data").GetProperty("map_id").GetInt32() != 277)
                throw new InvalidOperationException("Character left the rollout site.");
            sent++;
            return await Send(HttpMethod.Post, "/my/gllab/action/gathering");
        });
        using var activity = new ActivitySource("Artiact.BoundedRollout");
        var client = new GameClient(transport, new ApiSettings { BaseUrl = origin.AbsoluteUri, Character = "gllab", Username = "", Password = "" },
            NullLogger<IGameClient>.Instance, new NoCache(), activity);
        var state = new OperationState();
        var policy = new PortfolioSettings { Skills = [new("mining", 2, 30)] }.Policy();
        var factory = new StrategySessionFactory(client, new CombatCatalog(transport), new CharacterService(), new MiningCooldownDelay(),
            new ApiCompatibility(transport, new ExecutionSettings { FreshnessSeconds = 120 }, state));
        for (int phase = 0; phase < 2; phase++)
        {
            using var store = new FileRunCheckpointStore(directory, owner);
            if (phase == 0 && store.Load() is not null)
                throw new InvalidOperationException("Existing rollout must be inspected, not restarted by the acceptance command.");
            var run = factory.Create(policy, limits, store, identity);
            var decision = await run.TickAsync(deadline.Token);
            output.WriteLine($"Phase {phase}: " + JsonSerializer.Serialize(decision));
            output.WriteLine("Health: " + JsonSerializer.Serialize(state.Snapshot(120)));
            Assert.Equal("CommandVerified", decision.Reason);
            Assert.Equal(phase + 1, decision.Attempts);
            Assert.Equal("Gather:mining", decision.Command);
            if (phase == 1)
            {
                state.RequestStop();
                var stopped = await run.TickAsync(state.StopToken);
                Assert.Equal(StrategyStatus.Cancelled, stopped.Status);
                output.WriteLine("Stopped: " + JsonSerializer.Serialize(stopped));
            }
        }
        using (var store = new FileRunCheckpointStore(directory, owner))
        {
            var reopened = await factory.Create(policy, limits, store, identity).TickAsync(deadline.Token);
            Assert.Equal(StrategyStatus.Cancelled, reopened.Status);
            Assert.Equal(2, reopened.Attempts);
            Assert.Equal(2, sent);
            var saved = store.Load()!;
            Assert.Null(saved.PendingCommand);
            Assert.All(saved.Journal, entry => Assert.Equal("Verified", entry.Status));
            output.WriteLine($"Reopened stopped checkpoint: attempts={saved.Attempts} journal={saved.Journal.Length} sent={sent}");
        }
        using var final = await reads.GetAsync("/characters/gllab");
        final.EnsureSuccessStatusCode();
        using var finalSnapshot = JsonDocument.Parse(await final.Content.ReadAsStringAsync());
        var character = finalSnapshot.RootElement.GetProperty("data");
        output.WriteLine($"Observed map={character.GetProperty("map_id").GetInt32()} mining_level={character.GetProperty("mining_level").GetInt32()} mining_xp={character.GetProperty("mining_xp").GetInt32()}");
    }
    private sealed class NoCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => throw new InvalidOperationException("No cache writes in rollout.");
    }
}
