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

public class ApprovedMoveLiveTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "RealApiOneShot")]
    public async Task ApprovedGllabMove277Once()
    {
        if (Environment.GetEnvironmentVariable("ARTIACT_APPROVED_ONESHOT") != "gllab:Move:277")
            throw new InvalidOperationException("Explicit approved movement opt-in required.");
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Artiact.sln"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Repository root unavailable.");
        var configuration = RealApiConfiguration.Resolve(DotenvParser.Parse(await File.ReadAllTextAsync(Path.Combine(root.FullName, ".env"))));
        if (configuration.Character != "gllab") throw new InvalidOperationException("Character is outside approval.");
        var origin = DestinationValidator.Validate(configuration.BaseUri);
        string directory = Path.Combine(root.FullName, ".artiact-runs");
        Directory.CreateDirectory(directory);
        string marker = Path.Combine(directory, "approved-gllab-move-277.intent");
        if (File.Exists(marker)) throw new InvalidOperationException("Movement intent already exists; only read reconciliation is allowed.");
        using var http = new HttpClient(ReadOnlyApiVerifier.CreatePrimaryHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        var verifier = new ReadOnlyApiVerifier(http);
        string bearer = await verifier.AuthenticateAsync(origin, configuration, deadline.Token);
        async Task<HttpResponseMessage> Send(HttpMethod method, string path, HttpContent? body, CancellationToken token)
        {
            using var request = new HttpRequestMessage(method, new Uri(origin, path)) { Content = body };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
            return await verifier.SendAsync(request, "approved-move", token);
        }
        var reads = new InspectionTransport("gllab", path => Send(HttpMethod.Get, path, null, deadline.Token));
        var transport = new ApprovedMoveTransport(reads, marker, (path, body) => Send(HttpMethod.Post, path, body, CancellationToken.None));
        using var activity = new ActivitySource("Artiact.ApprovedOneShot");
        var api = new ApiSettings { BaseUrl = origin.AbsoluteUri, Character = "gllab", Username = "transport-owned", Password = "transport-owned" };
        var client = new GameClient(transport, api, NullLogger<IGameClient>.Instance, new NoCache(), activity);
        var settings = new ExecutionSettings { Mode = "OneShot", AllowActions = true, LiveActionsApproved = true };
        var state = new OperationState();
        var portfolio = new PortfolioSettings { Skills = [new("mining", 2, 30)] };
        var factory = new StrategySessionFactory(client, new CombatCatalog(transport), new CharacterService(),
            new MiningCooldownDelay(), new ApiCompatibility(transport, settings, state));
        var preview = await factory.Create(portfolio.Policy()).InspectAsync(deadline.Token);
        output.WriteLine("Preflight " + JsonSerializer.Serialize(preview));
        Assert.Equal("Move:277", preview.Command);
        var decision = await new StagedExecution(settings, api, portfolio, factory, state).RunAsync(deadline.Token);
        output.WriteLine("OneShot " + JsonSerializer.Serialize(decision));
        // Independent read even when reply validation failed; never re-run the action test.
        using var read = await reads.GetAsync("/characters/gllab");
        Assert.True(read.IsSuccessStatusCode);
        using var snapshot = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
        var character = snapshot.RootElement.GetProperty("data");
        int mapId = character.GetProperty("map_id").GetInt32();
        output.WriteLine($"Observed map_id={mapId} x={character.GetProperty("x").GetInt32()} y={character.GetProperty("y").GetInt32()}");
        Assert.Equal(277, mapId);
        Assert.NotNull(decision);
        Assert.Equal(1, decision.Attempts);
        Assert.True(decision.Reason == "CommandVerified" || decision.Status == StrategyStatus.Reconciled,
            "Movement state must be reviewed; no repeat dispatch.");
    }

    private sealed class NoCache : ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => throw new InvalidOperationException("No cache writes in rollout.");
    }
}
