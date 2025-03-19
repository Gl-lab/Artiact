using System.Text;
using System.Text.Json;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Microsoft.Extensions.Logging;

namespace Artiact.Client;

public class GameClient : IGameClient
{
    private readonly ICacheService _cacheService;

    private readonly string _characterName;
    private readonly IGameHttpClient _httpClient;
    private readonly ILogger<IGameClient> _logger;

    public GameClient( IGameHttpClient httpClient,
                       ApiSettings apiSettings,
                       ILogger<IGameClient> logger,
                       ICacheService cacheService )
    {
        _httpClient = httpClient;
        _characterName = apiSettings.Character;
        _logger = logger;
        _cacheService = cacheService;
    }

    public async Task<Character> GetCharacter()
    {
        string detailsUrl = $"/characters/{_characterName}";

        _logger.LogInformation( detailsUrl );
        HttpResponseMessage response = await _httpClient.GetAsync( detailsUrl );
        if ( response.IsSuccessStatusCode )
        {
            string details = await response.Content.ReadAsStringAsync();
            CharacterResponse? characterResponse = JsonSerializer.Deserialize<CharacterResponse>( details );

            return characterResponse.Data ?? throw new InvalidOperationException();
        }

        throw new Exception( $"Unable to get character: {_characterName}" );
    }


    public async Task<ActionResponse> Move( MapPoint target )
    {
        string detailsUrl = $"/my/{_characterName}/action/move";
        MoveRequest request = new()
        {
            X = target.X,
            Y = target.Y
        };

        StringContent content = new( JsonSerializer.Serialize( request ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> Gathering()
    {
        string detailsUrl = $"/my/{_characterName}/action/gathering";
        return await GetAction( detailsUrl );
    }

    public async Task<ActionResponse> Fight()
    {
        string detailsUrl = $"/my/{_characterName}/action/fight";
        return await GetAction( detailsUrl );
    }

    public async Task<ActionResponse> Rest()
    {
        string detailsUrl = $"/my/{_characterName}/action/rest";
        return await GetAction( detailsUrl );
    }

    public async Task<ActionResponse> Crafting( Item item )
    {
        string detailsUrl = $"/my/{_characterName}/action/crafting";

        StringContent content = new( JsonSerializer.Serialize( item ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> EquipItem( Inventory inventory )
    {
        string detailsUrl = $"/my/{_characterName}/action/equip";

        StringContent content = new( JsonSerializer.Serialize( inventory ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> UnequipItem( Inventory inventory )
    {
        string detailsUrl = $"/my/{_characterName}/action/unequip";

        StringContent content = new( JsonSerializer.Serialize( inventory ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> UseItem( Item item )
    {
        string detailsUrl = $"/my/{_characterName}/action/use";

        StringContent content = new( JsonSerializer.Serialize( item ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> Recycling( Item item )
    {
        string detailsUrl = $"/my/{_characterName}/action/recycling";

        StringContent content = new( JsonSerializer.Serialize( item ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> DeleteItem( Item item )
    {
        string detailsUrl = $"/my/{_characterName}/action/delete";

        StringContent content = new( JsonSerializer.Serialize( item ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<List<MapPlace>> GetMap()
    {
        List<MapPlace>? cachedMap = await _cacheService.GetFromCache<List<MapPlace>>();
        if ( cachedMap != null )
        {
            _logger.LogTrace( "GetMap use cache" );
            return cachedMap;
        }

        Map map = await GetPage<Map>( "maps", 1 );
        List<MapPlace> result = map.Data;
        for ( int i = 2; i <= map.Pages; i++ )
        {
            map = await GetPage<Map>( "maps", i );
            result.AddRange( map.Data );
        }

        await _cacheService.SaveToCache( result );
        return result;
    }


    public async Task<List<ResourceDatum>> GetResources()
    {
        List<ResourceDatum>? cachedResources = await _cacheService.GetFromCache<List<ResourceDatum>>();
        if ( cachedResources != null )
        {
            _logger.LogTrace( "GetResources use cache" );
            return cachedResources;
        }

        ResourceResponse resourceResponse = await GetPage<ResourceResponse>( "resources", 1 );
        List<ResourceDatum> result = resourceResponse.Data ?? throw new InvalidOperationException();

        for ( int i = 2; i <= resourceResponse.Pages; i++ )
        {
            resourceResponse = await GetPage<ResourceResponse>( "resources", i );
            result.AddRange( resourceResponse.Data ?? throw new InvalidOperationException() );
        }

        await _cacheService.SaveToCache( result );
        return result;
    }

    private async Task<T> GetPage<T>( string endpoint, int page )
    {
        string requestUri = $"/{endpoint}?page={page}";
        _logger.LogDebug( requestUri );
        HttpResponseMessage response = await _httpClient.GetAsync( requestUri );
        if ( !response.IsSuccessStatusCode )
        {
            throw new Exception( $"Unable to get {endpoint}" );
        }

        return JsonSerializer.Deserialize<T>( await response.Content.ReadAsStringAsync() ) ??
            throw new InvalidOperationException();
    }

    public async Task<List<ItemDatum>> GetItems()
    {
        List<ItemDatum>? cachedItems = await _cacheService.GetFromCache<List<ItemDatum>>();
        if ( cachedItems != null )
        {
            _logger.LogTrace( "GetItems use cache" );
            return cachedItems;
        }

        ItemsResponse itemResponse = await GetPage<ItemsResponse>( "items", 1 );
        List<ItemDatum> result = itemResponse.Data ?? throw new InvalidOperationException();

        for ( int i = 2; i <= itemResponse.Pages; i++ )
        {
            itemResponse = await GetPage<ItemsResponse>( "items", i );
            result.AddRange( itemResponse.Data ?? throw new InvalidOperationException() );
        }

        await _cacheService.SaveToCache( result );
        return result;
    }

    public async Task<List<MonsterDatum>> GetMonsters()
    {
        List<MonsterDatum>? cachedMonsters = await _cacheService.GetFromCache<List<MonsterDatum>>();
        if ( cachedMonsters != null )
        {
            _logger.LogTrace( "GetMonsters use cache" );
            return cachedMonsters;
        }

        MonstersResponse monsterResponse = await GetPage<MonstersResponse>( "monsters", 1 );
        List<MonsterDatum> result = monsterResponse.Data ?? throw new InvalidOperationException();

        for ( int i = 2; i <= monsterResponse.Pages; i++ )
        {
            monsterResponse = await GetPage<MonstersResponse>( "monsters", i );
            result.AddRange( monsterResponse.Data ?? throw new InvalidOperationException() );
        }

        await _cacheService.SaveToCache( result );
        return result;
    }

    private async Task<ActionResponse> GetAction( string detailsUrl, HttpContent? content = null )
    {
        const int maxRetries = 3;
        const int retryDelayMs = 1000;
        
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug( detailsUrl );
                HttpResponseMessage response = await _httpClient.PostAsync( detailsUrl, content );

                if ( response.IsSuccessStatusCode )
                {
                    ActionResponse? actionResponse =
                        JsonSerializer.Deserialize<ActionResponse>( await response.Content.ReadAsStringAsync() );

                    return actionResponse ?? throw new InvalidOperationException();
                }

                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError( "Request failed: {Url}, Status: {StatusCode}, Response: {ErrorContent}", 
                    detailsUrl, response.StatusCode, errorContent);

                if (response.StatusCode == System.Net.HttpStatusCode.GatewayTimeout || 
                    response.StatusCode == System.Net.HttpStatusCode.BadGateway)
                {
                    if (attempt < maxRetries)
                    {
                        _logger.LogWarning( "Retrying request after {Delay}ms (attempt {Attempt}/{MaxRetries})", 
                            retryDelayMs, attempt, maxRetries);
                        await Task.Delay(retryDelayMs);
                        continue;
                    }
                }

                throw new Exception( $"Request failed: {detailsUrl}, Status: {response.StatusCode}, Response: {errorContent}" );
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                if (attempt < maxRetries)
                {
                    _logger.LogWarning( ex, "Network error occurred. Retrying request after {Delay}ms (attempt {Attempt}/{MaxRetries})", 
                        retryDelayMs, attempt, maxRetries);
                    await Task.Delay(retryDelayMs);
                    continue;
                }
                throw;
            }
        }

        throw new Exception($"Request failed after {maxRetries} attempts: {detailsUrl}");
    }
}