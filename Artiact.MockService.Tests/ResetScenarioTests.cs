using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class ResetScenarioTests : IAsyncLifetime
{
    private MockServiceFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _client = _factory.CreateClient( new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri( "http://localhost" ),
            AllowAutoRedirect = false
        } );
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Reset_basic_mining_initializes_the_exact_epoch_and_empty_trace()
    {
        using StringContent request = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );

        using HttpResponseMessage response = await _client.PostAsync( "/__mock/reset", request );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        using JsonDocument document = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
        JsonElement root = document.RootElement;
        Assert.Equal( "basic-mining", root.GetProperty( "scenario" ).GetString() );
        Assert.Equal( 1, root.GetProperty( "generation" ).GetInt64() );
        Assert.Equal( "2000-01-01T00:00:00.0000000Z", root.GetProperty( "virtual_time" ).GetString() );
        Assert.Equal( 0, root.GetProperty( "trace_count" ).GetInt32() );
    }

    public static TheoryData<string?, HttpStatusCode, string> InvalidResetRequests => new()
    {
        { null, HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "[]", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{}", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{\"scenario\":null}", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{\"scenario\":1}", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{\"scenario\":\"\"}", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{\"scenario\":\"basic-mining\",\"scenario\":\"basic-mining\"}", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{\"scenario\":\"basic-mining\",\"extra\":true}", HttpStatusCode.BadRequest, "invalid_reset_request" },
        { "{\"scenario\":\"other\"}", HttpStatusCode.NotFound, "scenario_not_found" }
    };

    [Theory]
    [MemberData( nameof( InvalidResetRequests ) )]
    public async Task Rejected_reset_has_stable_problem_code_and_does_not_advance_generation(
        string? json,
        HttpStatusCode expectedStatus,
        string expectedCode )
    {
        using HttpRequestMessage rejectedRequest = new( HttpMethod.Post, "/__mock/reset" );
        if ( json != null )
        {
            rejectedRequest.Content = new StringContent( json, Encoding.UTF8, "application/json" );
        }

        using HttpResponseMessage rejected = await _client.SendAsync( rejectedRequest );

        Assert.Equal( expectedStatus, rejected.StatusCode );
        Assert.Equal( "application/problem+json", rejected.Content.Headers.ContentType?.MediaType );
        using JsonDocument problem = JsonDocument.Parse( await rejected.Content.ReadAsStringAsync() );
        Assert.Equal( expectedCode, problem.RootElement.GetProperty( "code" ).GetString() );

        using StringContent validRequest = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage valid = await _client.PostAsync( "/__mock/reset", validRequest );
        using JsonDocument summary = JsonDocument.Parse( await valid.Content.ReadAsStringAsync() );
        Assert.Equal( 1, summary.RootElement.GetProperty( "generation" ).GetInt64() );
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
