using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Models.Api;

namespace Artiact.RealApiTests;

internal sealed class ReadOnlyApiVerifier( HttpClient httpClient )
{
    public async Task<Artiact.Services.Strategy.StrategyDecision> InspectAsync(
        RealApiConfiguration configuration, CancellationToken cancellationToken)
    {
        Uri baseUri = DestinationValidator.Validate(configuration.BaseUri);
        string token = await AuthenticateAsync(baseUri, configuration, cancellationToken);
        var transport = new InspectionTransport(configuration.Character, async path =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, path));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await SendAsync(request, "inspection", cancellationToken);
        });
        var settings = new Artiact.Services.Operation.ExecutionSettings();
        var status = new Artiact.Services.Operation.OperationState();
        var compatibility = new Artiact.Services.Operation.ApiCompatibility(transport, settings, status);
        using var activity = new System.Diagnostics.ActivitySource("Artiact.ReadOnlyInspection");
        var client = new Artiact.Client.GameClient(transport,
            new Artiact.ApiSettings { Character = configuration.Character, BaseUrl = baseUri.AbsoluteUri, Username = "", Password = "" },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Artiact.Contracts.Client.IGameClient>.Instance,
            new InspectionCache(), activity);
        var factory = new Artiact.Services.Strategy.StrategySessionFactory(client,
            new Artiact.Services.Combat.CombatCatalog(transport), new Artiact.Services.CharacterService(),
            new Artiact.Services.MiningCooldownDelay(), compatibility);
        var policy = new Artiact.Services.Operation.PortfolioSettings
        {
            Skills = [new("mining", 2, 30)]
        }.Policy();
        return await factory.Create(policy).InspectAsync(cancellationToken);
    }

    private sealed class InspectionCache : Artiact.Client.ICacheService
    {
        public Task<T?> GetFromCache<T>() where T : class => Task.FromResult<T?>(null);
        public Task SaveToCache<T>(T data) where T : class => throw new InvalidOperationException("Inspection does not write caches.");
    }

    public async Task<VerificationSummary> VerifyAsync(
        RealApiConfiguration configuration,
        CancellationToken cancellationToken )
    {
        Uri baseUri = DestinationValidator.Validate( configuration.BaseUri );
        string token = await AuthenticateAsync( baseUri, configuration, cancellationToken );

        CharacterResponse character = await GetAsync<CharacterResponse>(
            baseUri,
            $"/characters/{Uri.EscapeDataString( configuration.Character )}",
            "character",
            token,
            cancellationToken );
        _ = RequireData( character.Data, "character", nameof( CharacterResponse ) );
        Map maps = await GetAsync<Map>( baseUri, "/maps?page=1", "maps", token, cancellationToken );
        var mapData = RequireData( maps.Data, "maps", nameof( Map ) );
        ResourceResponse resources = await GetAsync<ResourceResponse>(
            baseUri, "/resources?page=1", "resources", token, cancellationToken );
        var resourceData = RequireData( resources.Data, "resources", nameof( ResourceResponse ) );
        ItemsResponse items = await GetAsync<ItemsResponse>(
            baseUri, "/items?page=1", "items", token, cancellationToken );
        var itemData = RequireData( items.Data, "items", nameof( ItemsResponse ) );
        MonstersResponse monsters = await GetAsync<MonstersResponse>(
            baseUri, "/monsters?page=1", "monsters", token, cancellationToken );
        var monsterData = RequireData( monsters.Data, "monsters", nameof( MonstersResponse ) );

        return new VerificationSummary(
            mapData.Count,
            resourceData.Count,
            itemData.Count,
            monsterData.Count );
    }

    public static HttpClientHandler CreatePrimaryHandler()
    {
        return new HttpClientHandler { AllowAutoRedirect = false };
    }

    private async Task<string> AuthenticateAsync(
        Uri baseUri,
        RealApiConfiguration configuration,
        CancellationToken cancellationToken )
    {
        using HttpRequestMessage request = new( HttpMethod.Post, new Uri( baseUri, "/token" ) );
        string encodedCredentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes( $"{configuration.Username}:{configuration.Password}" ) );
        request.Headers.Authorization = new AuthenticationHeaderValue( "Basic", encodedCredentials );
        using HttpResponseMessage response = await SendAsync( request, "authentication", cancellationToken );
        EnsureSuccess( response, "authentication" );
        TokenContainer container = await DeserializeAsync<TokenContainer>(
            response, "authentication", cancellationToken );
        if ( string.IsNullOrWhiteSpace( container.Token ) )
        {
            throw new RealApiVerificationException( "Authentication contract is invalid." );
        }

        return container.Token;
    }

    private async Task<T> GetAsync<T>(
        Uri baseUri,
        string relativePath,
        string operation,
        string token,
        CancellationToken cancellationToken )
    {
        if ( relativePath.Contains( "/action/", StringComparison.OrdinalIgnoreCase ) )
        {
            throw new RealApiVerificationException( "Read-only request allowlist rejected an action path." );
        }

        using HttpRequestMessage request = new( HttpMethod.Get, new Uri( baseUri, relativePath ) );
        request.Headers.Authorization = new AuthenticationHeaderValue( "Bearer", token );
        using HttpResponseMessage response = await SendAsync( request, operation, cancellationToken );
        EnsureSuccess( response, operation );
        return await DeserializeAsync<T>( response, operation, cancellationToken );
    }

    private static void EnsureSuccess( HttpResponseMessage response, string operation )
    {
        if ( !response.IsSuccessStatusCode )
        {
            throw new RealApiVerificationException(
                $"{operation} failed with HTTP {(int)response.StatusCode}." );
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string operation,
        CancellationToken cancellationToken )
    {
        try
        {
            return await httpClient.SendAsync( request, cancellationToken );
        }
        catch ( HttpRequestException )
        {
            throw new RealApiVerificationException( $"{operation} network request failed." );
        }
        catch ( OperationCanceledException ) when ( !cancellationToken.IsCancellationRequested )
        {
            throw new RealApiVerificationException( $"{operation} network request timed out." );
        }
    }

    private static async Task<T> DeserializeAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken )
    {
        try
        {
            await using Stream content = await response.Content.ReadAsStreamAsync( cancellationToken );
            return await JsonSerializer.DeserializeAsync<T>( content, cancellationToken: cancellationToken ) ??
                   throw new JsonException();
        }
        catch ( JsonException )
        {
            throw new RealApiVerificationException(
                $"{operation} response does not match {typeof( T ).Name}." );
        }
    }

    private static T RequireData<T>( T? data, string operation, string contract )
        where T : class
    {
        return data ?? throw new RealApiVerificationException(
            $"{operation} response does not match {contract}." );
    }
}

internal sealed record VerificationSummary(
    int MapCount,
    int ResourceCount,
    int ItemCount,
    int MonsterCount );

internal sealed class RealApiVerificationException( string message ) : Exception( message );
