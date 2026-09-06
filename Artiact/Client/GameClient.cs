using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;

namespace Artiact.Client;

public class GameClient : IGameClient
{
    private readonly AsyncLocal<CancellationToken> _operationToken = new();
    public IDisposable BeginOperation(CancellationToken token)
    {
        var previous = _operationToken.Value;
        _operationToken.Value = token;
        return new OperationScope(() => _operationToken.Value = previous);
    }
    private sealed class OperationScope(Action restore) : IDisposable { public void Dispose() => restore(); }
    // Exact wire snapshot for presence-aware combat normalization; never log this payload.
    public JsonElement? LastCharacterPayload { get; private set; }
    public JsonElement? LastActionPayload { get; private set; }
    private readonly ActivitySource _activitySource;
    private readonly ICacheService _cacheService;

    private readonly string _characterName;
    private readonly IGameHttpClient _httpClient;
    private readonly ILogger<IGameClient> _logger;

    public GameClient( IGameHttpClient httpClient,
                       ApiSettings apiSettings,
                       ILogger<IGameClient> logger,
                       ICacheService cacheService,
                       ActivitySource activitySource )
    {
        _httpClient = httpClient;
        _characterName = apiSettings.Character;
        _logger = logger;
        _cacheService = cacheService;
        _activitySource = activitySource;
    }

    public async Task<Character> GetCharacter()
    {
        string detailsUrl = $"/characters/{_characterName}";

        _logger.LogInformation( detailsUrl );
        using HttpResponseMessage response = await _httpClient.ReadAsync( detailsUrl, _operationToken.Value );
        if ( response.IsSuccessStatusCode )
        {
            string details = await response.Content.ReadAsStringAsync();
            CharacterResponse? characterResponse = JsonSerializer.Deserialize<CharacterResponse>( details );
            using var document = JsonDocument.Parse(details);
            LastCharacterPayload = document.RootElement.GetProperty("data").Clone();
            return characterResponse?.Data ?? throw new InvalidOperationException();
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
        return await GetAction( detailsUrl, fight: true );
    }

    public async Task<ActionResponse> MoveToMap(int mapId)
    {
        if (mapId <= 0) throw new ArgumentOutOfRangeException(nameof(mapId));
        using var content = new StringContent(JsonSerializer.Serialize(new { map_id = mapId }), Encoding.UTF8, "application/json");
        return await GetAction($"/my/{_characterName}/action/move", content);
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

    public async Task<ActionResponse> EquipItem( EquipRequest equipment )
    {
        string detailsUrl = $"/my/{_characterName}/action/equip";

        StringContent content = new( JsonSerializer.Serialize( new[] { equipment } ), Encoding.UTF8, "application/json" );
        return await GetAction( detailsUrl, content );
    }

    public async Task<ActionResponse> UnequipItem( UnequipRequest equipment )
    {
        string detailsUrl = $"/my/{_characterName}/action/unequip";

        StringContent content = new( JsonSerializer.Serialize( new[] { equipment } ), Encoding.UTF8, "application/json" );
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
        List<MapPlace> result = map.Data ?? throw new InvalidOperationException("Map data is missing.");
        for ( int i = 2; i <= map.Pages; i++ )
        {
            map = await GetPage<Map>( "maps", i );
            result.AddRange( map.Data ?? throw new InvalidOperationException("Map data is missing.") );
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

    public async Task WarmUpCache()
    {
        _logger.LogInformation( "Начинаем прогрев кеша" );
        using Activity? activity = _activitySource.StartActivity("WarmUpCache");
        try
        {
            await GetMap();
            activity?.AddEvent( new ActivityEvent( "Map cached" ) );

            await GetResources();
            activity?.AddEvent( new ActivityEvent( "Resources cached" ) );

            await GetItems();
            activity?.AddEvent( new ActivityEvent( "Items cached" ) );

            await GetMonsters();
            activity?.AddEvent( new ActivityEvent( "Monsters cached" ) );


            _logger.LogInformation( "Прогрев кеша успешно завершен" );
        }
        catch ( Exception ex )
        {
            activity?.SetStatus( ActivityStatusCode.Error, ex.Message );
            _logger.LogError( ex, "Ошибка при прогреве кеша" );
            throw;
        }
    }

    private async Task<T> GetPage<T>( string endpoint, int page )
    {
        string requestUri = $"/{endpoint}?page={page}";
        _logger.LogDebug( requestUri );
        using HttpResponseMessage response = await _httpClient.ReadAsync( requestUri, _operationToken.Value );
        if ( !response.IsSuccessStatusCode )
        {
            throw new Exception( $"Unable to get {endpoint}" );
        }

        return JsonSerializer.Deserialize<T>( await response.Content.ReadAsStringAsync() ) ??
            throw new InvalidOperationException();
    }

    private async Task<ActionResponse> GetAction( string detailsUrl, HttpContent? content = null, bool fight = false )
    {
        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync( detailsUrl, content, _operationToken.Value );
            if ( !response.IsSuccessStatusCode )
                throw new ActionFailureException( (int)response.StatusCode >= 500
                    ? ActionFailureKind.UnknownOutcome : ActionFailureKind.Rejected, (int)response.StatusCode );

            string body = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ActionResponse>( body );
            if ( fight )
            {
                var matches = result?.Data?.Characters?.Where(c => c is not null &&
                    string.Equals(c.Name, _characterName, StringComparison.Ordinal)).ToArray();
                if ( matches?.Length != 1 || result?.Data?.Fight is not { Result: "win" or "loss" } )
                    throw new ActionFailureException( ActionFailureKind.UnknownOutcome );
                result.Data.Character = matches[0];
            }
            if ( result?.Data?.Character is null || result.Data.Cooldown is null )
                throw new ActionFailureException( ActionFailureKind.UnknownOutcome );
            using var document = JsonDocument.Parse(body);
            var data = document.RootElement.GetProperty("data");
            LastActionPayload = data.Clone();
            LastCharacterPayload = fight
                ? data.GetProperty("characters").EnumerateArray().Single(c => c.ValueKind == JsonValueKind.Object &&
                    c.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String && name.GetString() == _characterName).Clone()
                : data.GetProperty("character").Clone();
            return result;
        }
        catch ( Exception ex ) when ( ex is HttpRequestException or OperationCanceledException or JsonException or IOException )
        {
            throw new ActionFailureException( ActionFailureKind.UnknownOutcome );
        }
    }
}
