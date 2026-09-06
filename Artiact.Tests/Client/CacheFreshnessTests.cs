using Artiact.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.Tests.Client;

public class CacheFreshnessTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "artiact-cache-tests-" + Guid.NewGuid());
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private CacheService Cache(string version = "v1", TimeProvider? clock = null) => new(NullLogger<ICacheService>.Instance,
        TimeSpan.FromSeconds(30), new("https://api.artifactsmmo.com", version, _root), clock);
    [Fact]
    public async Task RoundTripIsVersionIsolatedAndExpires()
    {
        var clock = new Clock(); var cache = Cache(clock: clock);
        await cache.SaveToCache(new[] { "known" }); Assert.Equal(new[] { "known" }, await cache.GetFromCache<string[]>());
        Assert.Null(await Cache("v2", clock).GetFromCache<string[]>());
        clock.Now = clock.Now.AddSeconds(31); Assert.Null(await cache.GetFromCache<string[]>());
    }
    [Fact]
    public async Task CorruptAndFutureDatedEntriesAreMisses()
    {
        var clock = new Clock(); var cache = Cache(clock: clock); await cache.SaveToCache(new[] { "known" });
        clock.Now = clock.Now.AddHours(-1); Assert.Null(await cache.GetFromCache<string[]>());
        var file = Assert.Single(Directory.GetFiles(_root, "*.json", SearchOption.AllDirectories));
        await File.WriteAllTextAsync(file, "{"); Assert.Null(await cache.GetFromCache<string[]>());
    }
    [Fact]
    public async Task ConcurrentReplacementNeverReturnsPartialPayload()
    {
        var cache = Cache(); await cache.SaveToCache(Enumerable.Repeat("a", 200).ToArray());
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async i =>
        {
            await cache.SaveToCache(Enumerable.Repeat(i % 2 == 0 ? "a" : "b", 200).ToArray());
            var read = await cache.GetFromCache<string[]>();
            if (read is not null) { Assert.Equal(200, read.Length); Assert.Single(read.Distinct()); }
        }));
        Assert.NotNull(await cache.GetFromCache<string[]>());
    }
    public void Dispose()
    {
        // This test owns the literal GUID temp root; never uses the repository cache.
        string target = Path.GetFullPath(_root);
        if (!target.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(target).StartsWith("artiact-cache-tests-", StringComparison.Ordinal)) throw new InvalidOperationException("Invalid test cleanup root.");
        if (Directory.Exists(target)) Directory.Delete(target, true);
    }
}
