using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Artiact.Client;

public class CacheService : ICacheService
{
    private const string CacheDirectory = "cache";
    private readonly TimeSpan _cacheDuration;
    private readonly ILogger<ICacheService> _logger;

    public CacheService( ILogger<ICacheService> logger, TimeSpan? cacheDuration = null )
    {
        _logger = logger;
        _cacheDuration = cacheDuration ?? TimeSpan.FromHours( 48 );

        if ( !Directory.Exists( CacheDirectory ) )
        {
            Directory.CreateDirectory( CacheDirectory );
        }
    }

    public async Task<T?> GetFromCache<T>() where T : class
    {
        string name = GetName<T>();
        string cacheFilePath = Path.Combine( CacheDirectory, $"{name}.json" );

        if ( File.Exists( cacheFilePath ) )
        {
            FileInfo cacheInfo = new( cacheFilePath );
            if ( DateTime.UtcNow - cacheInfo.LastWriteTimeUtc < _cacheDuration )
            {
                string cachedData = await File.ReadAllTextAsync( cacheFilePath );
                _logger.LogInformation( $"Retrieved {name} from cache" );
                return JsonSerializer.Deserialize<T>( cachedData );
            }
        }

        return null;
    }

    public async Task SaveToCache<T>( T data ) where T : class
    {
        string name = GetName<T>();
        string cacheFilePath = Path.Combine( CacheDirectory, $"{name}.json" );
        await File.WriteAllTextAsync( cacheFilePath, JsonSerializer.Serialize( data ) );
        _logger.LogInformation( $"Saved {name} to cache" );
    }

    private static string GetName<T>() where T : class
    {
        return typeof( T ).IsGenericType ? typeof( T ).GenericTypeArguments.First().Name : typeof( T ).Name;
    }
}