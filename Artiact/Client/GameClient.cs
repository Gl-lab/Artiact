using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Artiact.Models;
using Microsoft.Extensions.Logging;

namespace Artiact.Client;

public class GameClient : IGameClient
{
    private readonly IGameHttpClient _httpClient;

    private readonly string _characterName;
    private readonly ILogger<IGameClient> _logger;
    private readonly string _cacheFilePath = "map_cache.json";
    private readonly TimeSpan _cacheDuration = TimeSpan.FromHours( 1 );
    
    public GameClient( IGameHttpClient httpClient,
                       string characterName,
                       ILogger<IGameClient> logger )
    {
        _httpClient = httpClient;

        _characterName = characterName;
        _logger = logger;
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
        if ( File.Exists( _cacheFilePath ) )
        {
            FileInfo cacheInfo = new( _cacheFilePath );
            if ( DateTime.UtcNow - cacheInfo.LastWriteTimeUtc < _cacheDuration )
            {
                string cachedData = await File.ReadAllTextAsync( _cacheFilePath );
                _logger.LogInformation( "GetMap use cache" );
                return JsonSerializer.Deserialize<List<MapPlace>>( cachedData ) ??
                    throw new InvalidOperationException();
            }
        }

        Map map = await GetMapPage( 1 );
        List<MapPlace> result = map.Data;
        for ( int i = 2; i < map.Pages; i++ )
        {
            map = await GetMapPage( i );
            result.AddRange( map.Data );
        }

        await File.WriteAllTextAsync( _cacheFilePath, JsonSerializer.Serialize( result ) );
        return result ?? throw new InvalidOperationException();
    }

    private async Task<Map> GetMapPage( int page )
    {
        string? requestUri = $"/maps?page={page}";
        _logger.LogInformation( requestUri );
        HttpResponseMessage response = await _httpClient.GetAsync( requestUri );
        if ( !response.IsSuccessStatusCode )
        {
            throw new Exception();
        }

        Map map =
            JsonSerializer.Deserialize<Map>( await response.Content.ReadAsStringAsync() ) ?? throw new
                InvalidOperationException();
        return map;
    }


    public async Task<List<ResourceDatum>> GetResources()
    {
        string? requestUri = "/resources";
        _logger.LogInformation( requestUri );
        HttpResponseMessage response = await _httpClient.GetAsync( requestUri );
        if ( !response.IsSuccessStatusCode )
        {
            throw new Exception();
        }

        ResourceResponse map =
            JsonSerializer.Deserialize<ResourceResponse>( await response.Content.ReadAsStringAsync() ) ?? throw new
                InvalidOperationException();

        return map.Data ?? throw new InvalidOperationException();
    }

    public Task<List<ItemDatum>> GetItems()
    {
        throw new NotImplementedException();
    }

    public Task<MonsterDatum> GetMonsters()
    {
        throw new NotImplementedException();
    }

    private async Task<ActionResponse> GetAction( string detailsUrl, HttpContent? content = null )
    {
        _logger.LogInformation( detailsUrl );
        HttpResponseMessage response = await _httpClient.PostAsync( detailsUrl, content );

        if ( !response.IsSuccessStatusCode )
        {
            throw new Exception( $"Unable : {detailsUrl}" );
        }

        ActionResponse? actionResponse =
            JsonSerializer.Deserialize<ActionResponse>( await response.Content.ReadAsStringAsync() );

        return actionResponse ?? throw new InvalidOperationException();
    }
}