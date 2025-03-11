using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Artiact.Models;

namespace Artiact.Client;

public interface IGameHttpClient
{
    public Task<HttpResponseMessage> PostAsync( string requestUri, HttpContent? content = null );
    public Task<HttpResponseMessage> GetAsync( string requestUri );
}

public class GameHttpClient : IGameHttpClient
{
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;
    private HttpClient _httpClient;

    public GameHttpClient( string baseUrl,
                           string username,
                           string password,
                           HttpClient httpClient )
    {
        _baseUrl = baseUrl;
        _username = username;
        _password = password;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new( _baseUrl );
    }


    private async Task GetJwtToken()
    {
        string tokenUrl = "/token";
        string authHeader = Convert.ToBase64String( Encoding.UTF8.GetBytes( $"{_username}:{_password}" ) );

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue( "Basic", authHeader );

        HttpResponseMessage response = await _httpClient.PostAsync( tokenUrl, null );
        if ( response.IsSuccessStatusCode )
        {
            string jsonResponse = await response.Content.ReadAsStringAsync();
            // Десериализуем JSON в объект TokenContainer
            TokenContainer? tokenContainer = JsonSerializer.Deserialize<TokenContainer>( jsonResponse );

            // Возвращаем токен


            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue( "Bearer",
                    tokenContainer?.Token ?? throw new AuthenticationException( "Token is missing." ) );
        }
    }

    public async Task<HttpResponseMessage> PostAsync( string requestUri, HttpContent? content = null )
    {
        if ( _httpClient.DefaultRequestHeaders.Authorization?.Scheme != "Bearer" )
        {
            await GetJwtToken();
        }

        return await _httpClient.PostAsync( requestUri, content );
    }

    public async Task<HttpResponseMessage> GetAsync( string requestUri )
    {
        if ( _httpClient.DefaultRequestHeaders.Authorization?.Scheme != "Bearer" )
        {
            await GetJwtToken();
        }

        return await _httpClient.GetAsync( requestUri );
    }
}