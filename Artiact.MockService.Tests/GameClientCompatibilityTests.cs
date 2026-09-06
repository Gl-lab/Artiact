using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Contracts.Models;
using Artiact.Contracts.Models.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class GameClientCompatibilityTests : IAsyncLifetime
{
    private MockServiceFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _ = _factory.Server;
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Real_game_client_completes_the_cold_cache_basic_mining_slice_in_TestServer()
    {
        RecordingHandler recorder = new( _factory.Server.CreateHandler() );
        Uri authority = MockServiceFactory.RequireLoopbackAuthority( new Uri( "http://localhost" ) );
        HttpClient transport = new( recorder ) { BaseAddress = authority };
        ApiSettings settings = new()
        {
            BaseUrl = authority.ToString(),
            Username = "mock-user",
            Password = "mock-password",
            Character = "MockHero"
        };
        InMemoryCache cache = new();
        IGameHttpClient http = new GameHttpClient( new SingleClientFactory( transport ), settings );
        IGameClient game = new GameClient( http, settings, NullLogger<IGameClient>.Instance, cache, new ActivitySource( "MockCompatibility" ) );

        using StringContent resetBody = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage reset = await transport.PostAsync( "/__mock/reset", resetBody );
        reset.EnsureSuccessStatusCode();

        using HttpResponseMessage tokenResponse = await transport.PostAsync( "/token", null );
        tokenResponse.EnsureSuccessStatusCode();
        TokenContainer? sentinelToken = System.Text.Json.JsonSerializer.Deserialize<TokenContainer>( await tokenResponse.Content!.ReadAsStringAsync() );
        Assert.Equal( "mock-token", sentinelToken?.Token );

        await game.WarmUpCache();
        int catalogRequestsAfterFirstWarmup = recorder.Paths.Count( IsCatalog );
        await game.WarmUpCache();
        Character character = await game.GetCharacter();
        ActionResponse move = await game.Move( new MapPoint { X = 2, Y = 0 } );
        ActionResponse gather = await game.Gathering();
        using HttpResponseMessage stateResponse = await transport.GetAsync( "/__mock/state/MockHero" );
        using HttpResponseMessage traceResponse = await transport.GetAsync( "/__mock/trace" );
        string stateJson = await stateResponse.Content!.ReadAsStringAsync();
        string traceJson = await traceResponse.Content!.ReadAsStringAsync();

        Assert.Equal( 4, catalogRequestsAfterFirstWarmup );
        Assert.Equal( 4, recorder.Paths.Count( IsCatalog ) );
        Assert.Equal( 4, cache.SaveCount );
        Assert.Equal( "MockHero", character.Name );
        Assert.Equal( 7, move.Data!.Cooldown!.TotalSeconds );
        Assert.Equal( 5, gather.Data!.Cooldown!.TotalSeconds );
        Assert.Equal( 6, gather.Data!.Character!.MiningXp );
        Assert.Equal( "copper_ore", gather.Data!.Character!.Inventory![ 0 ].Code );
        Assert.Contains( "\"phase\":\"Gathered\"", stateJson, StringComparison.Ordinal );
        Assert.Contains( "\"virtual_time\":\"2000-01-01T00:00:12.0000000Z\"", stateJson, StringComparison.Ordinal );
        Assert.Contains( "\"sequence\":1", traceJson, StringComparison.Ordinal );
        Assert.Contains( "\"sequence\":2", traceJson, StringComparison.Ordinal );

        using StringContent replayResetBody = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage replayReset = await transport.PostAsync( "/__mock/reset", replayResetBody );
        replayReset.EnsureSuccessStatusCode();
        _ = await game.GetCharacter();
        ActionResponse replayMove = await game.Move( new MapPoint { X = 2, Y = 0 } );
        ActionResponse replayGather = await game.Gathering();
        string replayState = await transport.GetStringAsync( "/__mock/state/MockHero" );
        string replayTrace = await transport.GetStringAsync( "/__mock/trace" );
        Assert.Equal( move.Data!.Cooldown!.TotalSeconds, replayMove.Data!.Cooldown!.TotalSeconds );
        Assert.Equal( move.Data!.Cooldown!.StartedAt, replayMove.Data!.Cooldown!.StartedAt );
        Assert.Equal( gather.Data!.Cooldown!.Expiration, replayGather.Data!.Cooldown!.Expiration );
        Assert.Equal( NormalizeGeneration( stateJson ), NormalizeGeneration( replayState ) );
        Assert.Equal( NormalizeGeneration( traceJson ), NormalizeGeneration( replayTrace ) );
        Assert.All( recorder.Uris, uri => Assert.Equal( "http://localhost", uri.GetLeftPart( UriPartial.Authority ) ) );
        Assert.Contains( "/token", recorder.Paths );
    }

    private static bool IsCatalog( string path ) => path is "/maps" or "/resources" or "/items" or "/monsters";

    private static string NormalizeGeneration( string json )
    {
        JsonNode node = JsonNode.Parse( json )!;
        Normalize( node );
        return node.ToJsonString();
    }

    private static void Normalize( JsonNode node )
    {
        if ( node is JsonObject obj )
        {
            if ( obj.ContainsKey( "generation" ) ) obj[ "generation" ] = 0;
            foreach ( JsonNode? child in obj.Select( pair => pair.Value ).Where( value => value != null ) ) Normalize( child! );
        }
        else if ( node is JsonArray array )
        {
            foreach ( JsonNode? child in array.Where( value => value != null ) ) Normalize( child! );
        }
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private sealed class SingleClientFactory( HttpClient client ) : IHttpClientFactory
    {
        public HttpClient CreateClient( string name ) => client;
    }

    private sealed class RecordingHandler( HttpMessageHandler inner ) : DelegatingHandler( inner )
    {
        public List<Uri> Uris { get; } = [];
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken cancellationToken )
        {
            Uri uri = request.RequestUri ?? throw new InvalidOperationException( "Absolute request URI is required." );
            Assert.True( uri.IsAbsoluteUri );
            Assert.Equal( "http://localhost", uri.GetLeftPart( UriPartial.Authority ) );
            Uris.Add( uri );
            Paths.Add( uri.AbsolutePath );
            return base.SendAsync( request, cancellationToken );
        }
    }

    private sealed class InMemoryCache : ICacheService
    {
        private readonly Dictionary<Type, object> _values = [];
        public int SaveCount { get; private set; }

        public Task<T?> GetFromCache<T>() where T : class
        {
            return Task.FromResult( _values.TryGetValue( typeof( T ), out object? value ) ? (T)value : null );
        }

        public Task SaveToCache<T>( T data ) where T : class
        {
            _values[ typeof( T ) ] = data;
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
