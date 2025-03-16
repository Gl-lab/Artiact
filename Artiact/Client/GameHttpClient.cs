using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Artiact.Models.Api;

namespace Artiact.Client;

public class GameHttpClient : IGameHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _password;
    private readonly string _username;


    public GameHttpClient(
        IHttpClientFactory httpClientFactory,
        ApiSettings settings )
    {
        _username = settings.Username;
        _password = settings.Password;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = new Uri( settings.BaseUrl );
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
}