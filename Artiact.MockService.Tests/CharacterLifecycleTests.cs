using System.Net;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class CharacterLifecycleTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web );
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
    public async Task Character_requires_reset_and_initializes_only_canonical_mock_hero()
    {
        using HttpResponseMessage beforeReset = await _client.GetAsync( "/characters/MockHero" );
        AssertProblem( beforeReset, HttpStatusCode.Conflict, "scenario_not_initialized" );

        await Reset();

        using HttpResponseMessage unknown = await _client.GetAsync( "/characters/Other" );
        AssertProblem( unknown, HttpStatusCode.NotFound, "character_not_found" );

        using HttpResponseMessage loaded = await _client.GetAsync( "/characters/mockhero" );
        Assert.Equal( HttpStatusCode.OK, loaded.StatusCode );
        using JsonDocument character = JsonDocument.Parse( await loaded.Content.ReadAsStringAsync() );
        JsonElement data = character.RootElement.GetProperty( "data" );
        Assert.Equal( "MockHero", data.GetProperty( "name" ).GetString() );
        Assert.Equal( 0, data.GetProperty( "x" ).GetInt32() );
        Assert.Equal( 0, data.GetProperty( "y" ).GetInt32() );
        Assert.Equal( 20, data.GetProperty( "inventory" ).GetArrayLength() );
        Assert.Equal( 1, data.GetProperty( "inventory" )[ 0 ].GetProperty( "slot" ).GetInt32() );
        CharacterResponse loadedDto = JsonSerializer.Deserialize<CharacterResponse>( character.RootElement.GetRawText(), JsonOptions )!;
        ScenarioAssertions.CharacterEquals( ExpectedScenario.Character(), loadedDto.Data );

        using HttpResponseMessage repeated = await _client.GetAsync( "/characters/MockHero" );
        CharacterResponse repeatedDto = JsonSerializer.Deserialize<CharacterResponse>( await repeated.Content.ReadAsStringAsync(), JsonOptions )!;
        ScenarioAssertions.CharacterEquals( ExpectedScenario.Character(), repeatedDto.Data );
    }

    [Fact]
    public async Task Scenario_routes_reject_reads_before_reset_and_state_before_character_load()
    {
        using HttpResponseMessage catalog = await _client.GetAsync( "/maps?page=1" );
        AssertProblem( catalog, HttpStatusCode.Conflict, "scenario_not_initialized" );
        using HttpResponseMessage trace = await _client.GetAsync( "/__mock/trace" );
        AssertProblem( trace, HttpStatusCode.Conflict, "scenario_not_initialized" );
        using HttpResponseMessage action = await _client.PostAsync( "/my/MockHero/action/gathering", null );
        AssertProblem( action, HttpStatusCode.Conflict, "scenario_not_initialized" );

        await Reset();
        using HttpResponseMessage state = await _client.GetAsync( "/__mock/state/mockhero" );
        AssertProblem( state, HttpStatusCode.Conflict, "character_not_initialized" );
        using HttpResponseMessage unknownState = await _client.GetAsync( "/__mock/state/Other" );
        AssertProblem( unknownState, HttpStatusCode.NotFound, "character_not_found" );
    }

    private async Task Reset()
    {
        using StringContent request = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage response = await _client.PostAsync( "/__mock/reset", request );
        response.EnsureSuccessStatusCode();
    }

    private static void AssertProblem( HttpResponseMessage response, HttpStatusCode status, string code )
    {
        Assert.Equal( status, response.StatusCode );
        Assert.Equal( "application/problem+json", response.Content.Headers.ContentType?.MediaType );
        using JsonDocument problem = JsonDocument.Parse( response.Content.ReadAsStringAsync().GetAwaiter().GetResult() );
        Assert.Equal( code, problem.RootElement.GetProperty( "code" ).GetString() );
    }


    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
