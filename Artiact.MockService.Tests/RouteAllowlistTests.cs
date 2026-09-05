using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class RouteAllowlistTests : IAsyncLifetime
{
    private MockServiceFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _client = _factory.CreateClient( new WebApplicationFactoryClientOptions { BaseAddress = new Uri( "http://localhost" ), AllowAutoRedirect = false } );
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData( "/my/MockHero/action/crafting" )]
    [InlineData( "/my/MockHero/action/fight" )]
    [InlineData( "/unknown" )]
    public async Task Unsupported_routes_return_stable_404_without_fallback( string path )
    {
        using StringContent content = new( "{}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage response = await _client.PostAsync( path, content );

        Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
        Assert.Equal( "application/problem+json", response.Content.Headers.ContentType?.MediaType );
        using JsonDocument problem = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
        Assert.Equal( "unsupported_route", problem.RootElement.GetProperty( "code" ).GetString() );
    }

    public static IEnumerable<object[]> WrongMethods()
    {
        yield return [ HttpMethod.Get, "/token" ];
        yield return [ HttpMethod.Post, "/maps?page=1" ];
        yield return [ HttpMethod.Get, "/my/MockHero/action/move" ];
    }

    [Theory]
    [MemberData( nameof( WrongMethods ) )]
    public async Task Wrong_methods_are_unsupported_404_and_do_not_mutate( HttpMethod method, string path )
    {
        using StringContent resetBody = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage reset = await _client.PostAsync( "/__mock/reset", resetBody );
        reset.EnsureSuccessStatusCode();
        using HttpResponseMessage load = await _client.GetAsync( "/characters/MockHero" );
        load.EnsureSuccessStatusCode();
        string before = await _client.GetStringAsync( "/__mock/state/MockHero" );

        using HttpRequestMessage request = new( method, path );
        if ( method == HttpMethod.Post ) request.Content = new StringContent( "{}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage response = await _client.SendAsync( request );

        Assert.Equal( HttpStatusCode.NotFound, response.StatusCode );
        Assert.Equal( "application/problem+json", response.Content.Headers.ContentType?.MediaType );
        using JsonDocument problem = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
        Assert.Equal( "unsupported_route", problem.RootElement.GetProperty( "code" ).GetString() );
        Assert.Equal( before, await _client.GetStringAsync( "/__mock/state/MockHero" ) );
        Assert.Equal( "[]", await _client.GetStringAsync( "/__mock/trace" ) );
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
