using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Artiact.Client;

public sealed record CacheIdentity(string Endpoint, string Version, string? Directory = null);
public class CacheService : ICacheService
{
    private readonly string _directory, _identity;
    private readonly TimeSpan _duration;
    private readonly TimeProvider _time;
    private readonly ILogger<ICacheService> _logger;
    public CacheService(ILogger<ICacheService> logger, TimeSpan? cacheDuration = null, CacheIdentity? identity = null, TimeProvider? time = null)
    {
        identity ??= new("https://api.artifactsmmo.com", "8.2.3");
        _identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new Uri(identity.Endpoint).GetComponents(UriComponents.SchemeAndServer, UriFormat.SafeUnescaped) + "|" + identity.Version)));
        _directory = Path.Combine(identity.Directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Artiact", "cache"), _identity);
        _duration = cacheDuration ?? TimeSpan.FromHours(48);
        _time = time ?? TimeProvider.System;
        _logger = logger;
    }
    private sealed record Entry<T>(int Format, string Identity, DateTimeOffset CreatedAt, T? Data);
    private string FileName<T>() => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(typeof(T).FullName!))) + ".json");
    public async Task<T?> GetFromCache<T>() where T : class
    {
        try
        {
            await using var file = new FileStream(FileName<T>(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var entry = await JsonSerializer.DeserializeAsync<Entry<T>>(file);
            var age = _time.GetUtcNow() - entry?.CreatedAt;
            return entry is { Format: 1 } && entry.Identity == _identity && age >= TimeSpan.Zero && age < _duration ? entry.Data : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { _logger.LogDebug("Cache unavailable or invalid; treating as miss"); return null; }
    }
    public async Task SaveToCache<T>(T data) where T : class
    {
        if (_duration <= TimeSpan.Zero) return;
        Directory.CreateDirectory(_directory);
        string path = FileName<T>(), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(new Entry<T>(1, _identity, _time.GetUtcNow(), data)));
            File.Move(temporary, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { _logger.LogDebug("Cache replacement unavailable; retaining previous entry"); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
