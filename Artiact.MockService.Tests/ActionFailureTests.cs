using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class ActionFailureTests : IAsyncLifetime
{
    private MockServiceFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _client = _factory.CreateClient( new WebApplicationFactoryClientOptions { BaseAddress = new Uri( "http://localhost" ), AllowAutoRedirect = false } );
        await ExpectSuccess( "/__mock/reset", "{\"scenario\":\"basic-mining\"}" );
    }

    public static IEnumerable<object?[]> InvalidMoveBodies()
    {
        yield return [ null ];
        yield return [ "{" ];
        yield return [ "[]" ];
        yield return [ "null" ];
        yield return [ "{\"x\":2,\"y\":null}" ];
        yield return [ "{\"x\":2.5,\"y\":0}" ];
        yield return [ "{\"x\":2,\"x\":2,\"y\":0}" ];
        yield return [ "{\"x\":2,\"y\":0,\"z\":0}" ];
    }

    [Theory]
    [MemberData( nameof( InvalidMoveBodies ) )]
    public async Task Malformed_move_bodies_are_rejected_without_mutation( string? body )
    {
        using HttpResponseMessage load = await _client.GetAsync( "/characters/MockHero" );
        load.EnsureSuccessStatusCode();
        await ExpectProblem( "/my/MockHero/action/move", body, HttpStatusCode.BadRequest, "invalid_move_request" );
        using HttpResponseMessage trace = await _client.GetAsync( "/__mock/trace" );
        Assert.Equal( "[]", await trace.Content.ReadAsStringAsync() );
    }

    [Fact]
    public async Task Action_failures_follow_declared_precedence_and_do_not_append_trace()
    {
        await ExpectProblem( "/my/Other/action/move", "{\"x\":2,\"y\":0}", HttpStatusCode.NotFound, "character_not_found" );
        await ExpectProblem( "/my/mockhero/action/move", "{\"x\":2,\"y\":0}", HttpStatusCode.Conflict, "character_not_initialized" );

        using HttpResponseMessage load = await _client.GetAsync( "/characters/MockHero" );
        load.EnsureSuccessStatusCode();
        await ExpectProblem( "/my/MockHero/action/gathering", null, HttpStatusCode.UnprocessableEntity, "gathering_not_available" );
        await ExpectProblem( "/my/MockHero/action/move", "{\"x\":2}", HttpStatusCode.BadRequest, "invalid_move_request" );
        await ExpectProblem( "/my/MockHero/action/move", "{\"x\":1,\"y\":0}", HttpStatusCode.UnprocessableEntity, "destination_not_found" );

        await ExpectSuccess( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        await ExpectProblem( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}", HttpStatusCode.Conflict, "invalid_transition" );
        await ExpectSuccess( "/my/MockHero/action/gathering", null );
        await ExpectProblem( "/my/MockHero/action/gathering", null, HttpStatusCode.Conflict, "invalid_transition" );

        using HttpResponseMessage traceResponse = await _client.GetAsync( "/__mock/trace" );
        using JsonDocument trace = JsonDocument.Parse( await traceResponse.Content.ReadAsStringAsync() );
        Assert.Equal( 2, trace.RootElement.GetArrayLength() );
    }

    private async Task ExpectSuccess( string path, string? json )
    {
        using HttpResponseMessage response = await Post( path, json );
        response.EnsureSuccessStatusCode();
    }

    private async Task ExpectProblem( string path, string? json, HttpStatusCode status, string code )
    {
        using HttpResponseMessage response = await Post( path, json );
        Assert.Equal( status, response.StatusCode );
        Assert.Equal( "application/problem+json", response.Content.Headers.ContentType?.MediaType );
        using JsonDocument problem = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
        Assert.Equal( code, problem.RootElement.GetProperty( "code" ).GetString() );
    }

    private Task<HttpResponseMessage> Post( string path, string? json )
    {
        HttpContent? content = json == null ? null : new StringContent( json, Encoding.UTF8, "application/json" );
        return _client.PostAsync( path, content );
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
