using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services.Strategy;

namespace Artiact.RealApiTests;

public class ScopedAutonomousTransportTests
{
    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData("/my/other/action/gathering", null)]
    [InlineData("/my/gllab/action/fight", null)]
    [InlineData("/my/gllab/action/move", "{\"map_id\":277}")]
    [InlineData("/my/gllab/action/move", "{\"map_id\":379,\"x\":1}")]
    [InlineData("/my/gllab/action/gathering", "{}")]
    public async Task RejectsOutsideScopeBeforeSending(string path, string? body)
    {
        var fixture = new Fixture();
        await fixture.Observe();
        await Assert.ThrowsAsync<ActionFailureException>(() => fixture.Transport.PostAsync(path,
            body is null ? null : new StringContent(body)));
        Assert.Equal(0, fixture.Sent);
    }

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task RequiresNewObservationAndStopsAtMilestone()
    {
        var f = new Fixture();
        await f.Observe();
        using var move = await f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}"));
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/gathering"));
        f.Map = 379;
        await f.Observe();
        f.ReplyLevel = 2;
        using var gather = await f.Transport.PostAsync("/my/gllab/action/gathering");
        Assert.True(f.Stopped);
        f.Level = 2;
        await f.Observe();
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/gathering"));
        Assert.Equal(2, f.Sent);
    }

    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData(1, 277, 1)]
    [InlineData(2, 277, 2)]
    [InlineData(1, 999, 2)]
    public async Task RejectsChangedInitialState(int level, int map, int copper)
    {
        var f = new Fixture { Level = level, Map = map, Copper = copper };
        await f.Observe();
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}")));
        Assert.Equal(0, f.Sent);
    }

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task LostReplyLatchesEvenAfterNewObservation()
    {
        var f = new Fixture { LoseReply = true };
        await f.Observe();
        await Assert.ThrowsAsync<HttpRequestException>(() => f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}")));
        await f.Observe();
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}")));
        Assert.Equal(1, f.Sent);
    }

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task RejectsStaleOrChangedWorld()
    {
        var f = new Fixture();
        await f.Observe();
        f.Clock.Now += TimeSpan.FromSeconds(31);
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}")));
        f.ChangedWorld = true;
        await f.Observe();
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}")));
        Assert.Equal(0, f.Sent);
    }

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task StopsAtSixteenDispatchesEvenIfMilestoneNeverArrives()
    {
        var f = new Fixture();
        await f.Observe();
        using (await f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}"))) { }
        f.Map = 379;
        for (int i = 0; i < 15; i++)
        {
            await f.Observe();
            using (await f.Transport.PostAsync("/my/gllab/action/gathering")) { }
        }
        await f.Observe();
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/gathering"));
        Assert.Equal(16, f.Sent);
    }

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task CharacterOnlyReadCannotReuseCatalogsAfterActionAndSealBlocksReplay()
    {
        var f = new Fixture();
        await f.Observe();
        using (await f.Transport.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":379}"))) { }
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Transport.GetAsync("/characters/gllab"));
        f.Map = 379;
        await f.Observe();
        await f.Transport.SealAsync();
        await Assert.ThrowsAsync<ActionFailureException>(() => f.Transport.PostAsync("/my/gllab/action/gathering"));
        Assert.Equal(1, f.Sent);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Fixture : IGameHttpClient
    {
        public int Map = 277, Level = 1, Copper = 2, ReplyLevel = 1, Sent;
        public bool Stopped, LoseReply, ChangedWorld;
        public Clock Clock { get; } = new();
        public ScopedAutonomousTransport Transport { get; }
        private static readonly ImmutableDictionary<string, ImmutableArray<JsonElement>> Catalogs =
            new[] { "maps", "resources", "items" }.ToImmutableDictionary(x => x, _ => ImmutableArray<JsonElement>.Empty);
        private JsonElement Character(int? map = null, int? level = null) => JsonSerializer.SerializeToElement(new
        { name = "gllab", map_id = map ?? Map, layer = "overworld", inventory_max_items = 100,
            inventory = new[] { new { code = "copper_ore", quantity = Copper } }, alchemy_level = level ?? Level });
        public Fixture()
        {
            var baseline = new StrategyObservation(Character(), Catalogs, "fixture");
            Transport = new(this, baseline.Fingerprint, baseline.WorldFingerprint, "fixture", async (path, _) =>
            {
                Sent++;
                if (LoseReply) throw new HttpRequestException();
                await Task.CompletedTask;
                return Reply(new { data = new { character = Character(379, ReplyLevel) } });
            }, () => Stopped = true, Clock);
        }
        public async Task Observe()
        {
            foreach (var name in new[] { "maps", "resources", "items" })
                using (await Transport.GetAsync($"/{name}?page=1")) { }
            using (await Transport.GetAsync("/characters/gllab")) { }
        }
        public Task<HttpResponseMessage> GetAsync(string path) => Task.FromResult(path == "/characters/gllab"
            ? Reply(new { data = Character() }) : Reply(new { data = ChangedWorld ? new object[] { new { changed = true } } : Array.Empty<object>(), pages = 1, page = 1 }));
        public Task<HttpResponseMessage> PostAsync(string path, HttpContent? content = null) => throw new InvalidOperationException();
        private static HttpResponseMessage Reply(object value) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
