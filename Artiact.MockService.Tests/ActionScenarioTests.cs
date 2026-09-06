using System.Net;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class ActionScenarioTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web );
    private MockServiceFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _client = _factory.CreateClient( new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri( "http://localhost" ),
            AllowAutoRedirect = false
        } );
        await PostJson( "/__mock/reset", "{\"scenario\":\"basic-mining\"}" );
        using HttpResponseMessage loaded = await _client.GetAsync( "/characters/MockHero" );
        loaded.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Move_commits_exact_virtual_cooldown_state_and_trace()
    {
        using HttpResponseMessage response = await PostJson( "/my/mockhero/action/move", "{\"x\":2,\"y\":0}" );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        using JsonDocument action = JsonDocument.Parse( await response.Content!.ReadAsStringAsync() );
        JsonElement data = action.RootElement.GetProperty( "data" );
        JsonElement cooldown = data.GetProperty( "cooldown" );
        Assert.Equal( 7, cooldown.GetProperty( "total_seconds" ).GetInt32() );
        Assert.Equal( 0, cooldown.GetProperty( "remaining_seconds" ).GetInt32() );
        Assert.Equal( "2000-01-01T00:00:00.0000000Z", cooldown.GetProperty( "started_at" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:07.0000000Z", cooldown.GetProperty( "expiration" ).GetString() );
        Assert.Equal( "mock_virtual_elapsed", cooldown.GetProperty( "reason" ).GetString() );
        Assert.Equal( "Copper Rocks", data.GetProperty( "destination" ).GetProperty( "name" ).GetString() );
        Assert.Equal( "rocks", data.GetProperty( "destination" ).GetProperty( "skin" ).GetString() );
        Assert.Equal( 2, data.GetProperty( "destination" ).GetProperty( "x" ).GetInt32() );
        Assert.Equal( 0, data.GetProperty( "destination" ).GetProperty( "y" ).GetInt32() );
        Assert.Equal( "resource", data.GetProperty( "destination" ).GetProperty( "content" ).GetProperty( "type" ).GetString() );
        Assert.Equal( "copper_rocks", data.GetProperty( "destination" ).GetProperty( "content" ).GetProperty( "code" ).GetString() );
        Assert.Equal( 0, data.GetProperty( "details" ).GetProperty( "xp" ).GetInt32() );
        Assert.Empty( data.GetProperty( "details" ).GetProperty( "items" ).EnumerateArray() );
        Assert.Equal( 2, data.GetProperty( "character" ).GetProperty( "x" ).GetInt32() );
        Assert.Equal( 0, data.GetProperty( "character" ).GetProperty( "cooldown" ).GetInt32() );
        Assert.Equal( "2000-01-01T00:00:00.0000000Z", data.GetProperty( "character" ).GetProperty( "cooldown_expiration" ).GetString() );
        Character expectedCharacter = ExpectedScenario.Character();
        expectedCharacter.X = 2;
        ActionResponse actionDto = JsonSerializer.Deserialize<ActionResponse>( action.RootElement.GetRawText(), JsonOptions )!;
        ScenarioAssertions.CharacterEquals( expectedCharacter, actionDto.Data!.Character! );

        using HttpResponseMessage traceResponse = await _client.GetAsync( "/__mock/trace" );
        traceResponse.EnsureSuccessStatusCode();
        using JsonDocument trace = JsonDocument.Parse( await traceResponse.Content!.ReadAsStringAsync() );
        Assert.Equal( 1, trace.RootElement.GetArrayLength() );
        Assert.Equal( "move", trace.RootElement[ 0 ].GetProperty( "action" ).GetString() );
        Assert.Equal( 1, trace.RootElement[ 0 ].GetProperty( "sequence" ).GetInt64() );
        Assert.Equal( 1, trace.RootElement[ 0 ].GetProperty( "generation" ).GetInt64() );
        Assert.Equal( "MockHero", trace.RootElement[ 0 ].GetProperty( "character" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:00.0000000Z", trace.RootElement[ 0 ].GetProperty( "virtual_started_at" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:07.0000000Z", trace.RootElement[ 0 ].GetProperty( "virtual_completed_at" ).GetString() );
        Assert.Equal( 7, trace.RootElement[ 0 ].GetProperty( "duration_seconds" ).GetInt32() );
        Assert.Equal( 0, trace.RootElement[ 0 ].GetProperty( "from_x" ).GetInt32() );
        Assert.Equal( 0, trace.RootElement[ 0 ].GetProperty( "from_y" ).GetInt32() );
        Assert.Equal( 2, trace.RootElement[ 0 ].GetProperty( "to_x" ).GetInt32() );
        Assert.Equal( 0, trace.RootElement[ 0 ].GetProperty( "to_y" ).GetInt32() );
        Assert.Equal( 0, trace.RootElement[ 0 ].GetProperty( "mining_xp_delta" ).GetInt32() );
        Assert.Equal( JsonValueKind.Null, trace.RootElement[ 0 ].GetProperty( "item_code" ).ValueKind );
        Assert.Equal( 0, trace.RootElement[ 0 ].GetProperty( "item_quantity_delta" ).GetInt32() );
    }

    [Fact]
    public async Task Gathering_after_move_commits_exact_yield_virtual_time_and_trace()
    {
        using HttpResponseMessage move = await PostJson( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        move.EnsureSuccessStatusCode();

        using HttpResponseMessage response = await _client.PostAsync( "/my/mockhero/action/gathering", null );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        using JsonDocument action = JsonDocument.Parse( await response.Content!.ReadAsStringAsync() );
        JsonElement data = action.RootElement.GetProperty( "data" );
        JsonElement cooldown = data.GetProperty( "cooldown" );
        Assert.Equal( 5, cooldown.GetProperty( "total_seconds" ).GetInt32() );
        Assert.Equal( 0, cooldown.GetProperty( "remaining_seconds" ).GetInt32() );
        Assert.Equal( "2000-01-01T00:00:07.0000000Z", cooldown.GetProperty( "started_at" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:12.0000000Z", cooldown.GetProperty( "expiration" ).GetString() );
        Assert.Equal( "mock_virtual_elapsed", cooldown.GetProperty( "reason" ).GetString() );
        Assert.Equal( JsonValueKind.Null, data.GetProperty( "destination" ).ValueKind );
        Assert.Equal( 6, data.GetProperty( "character" ).GetProperty( "mining_xp" ).GetInt32() );
        JsonElement slot = data.GetProperty( "character" ).GetProperty( "inventory" )[ 0 ];
        Assert.Equal( "copper_ore", slot.GetProperty( "code" ).GetString() );
        Assert.Equal( 1, slot.GetProperty( "quantity" ).GetInt32() );
        Assert.Equal( 6, data.GetProperty( "details" ).GetProperty( "xp" ).GetInt32() );
        Assert.Equal( "copper_ore", data.GetProperty( "details" ).GetProperty( "items" )[ 0 ].GetProperty( "code" ).GetString() );
        Assert.Equal( 1, data.GetProperty( "details" ).GetProperty( "items" )[ 0 ].GetProperty( "quantity" ).GetInt32() );
        Character expectedCharacter = ExpectedScenario.Character();
        expectedCharacter.X = 2;
        expectedCharacter.MiningXp = 6;
        expectedCharacter.Inventory![ 0 ].Code = "copper_ore";
        expectedCharacter.Inventory![ 0 ].Quantity = 1;
        ActionResponse actionDto = JsonSerializer.Deserialize<ActionResponse>( action.RootElement.GetRawText(), JsonOptions )!;
        ScenarioAssertions.CharacterEquals( expectedCharacter, actionDto.Data!.Character! );

        using HttpResponseMessage traceResponse = await _client.GetAsync( "/__mock/trace" );
        using JsonDocument trace = JsonDocument.Parse( await traceResponse.Content!.ReadAsStringAsync() );
        Assert.Equal( 2, trace.RootElement.GetArrayLength() );
        Assert.Equal( "gathering", trace.RootElement[ 1 ].GetProperty( "action" ).GetString() );
        Assert.Equal( 2, trace.RootElement[ 1 ].GetProperty( "sequence" ).GetInt64() );
        Assert.Equal( 1, trace.RootElement[ 1 ].GetProperty( "generation" ).GetInt64() );
        Assert.Equal( "MockHero", trace.RootElement[ 1 ].GetProperty( "character" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:07.0000000Z", trace.RootElement[ 1 ].GetProperty( "virtual_started_at" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:12.0000000Z", trace.RootElement[ 1 ].GetProperty( "virtual_completed_at" ).GetString() );
        Assert.Equal( 5, trace.RootElement[ 1 ].GetProperty( "duration_seconds" ).GetInt32() );
        Assert.Equal( 2, trace.RootElement[ 1 ].GetProperty( "from_x" ).GetInt32() );
        Assert.Equal( 0, trace.RootElement[ 1 ].GetProperty( "from_y" ).GetInt32() );
        Assert.Equal( 2, trace.RootElement[ 1 ].GetProperty( "to_x" ).GetInt32() );
        Assert.Equal( 0, trace.RootElement[ 1 ].GetProperty( "to_y" ).GetInt32() );
        Assert.Equal( 6, trace.RootElement[ 1 ].GetProperty( "mining_xp_delta" ).GetInt32() );
        Assert.Equal( "copper_ore", trace.RootElement[ 1 ].GetProperty( "item_code" ).GetString() );
        Assert.Equal( 1, trace.RootElement[ 1 ].GetProperty( "item_quantity_delta" ).GetInt32() );
    }

    [Fact]
    public async Task State_after_complete_slice_is_the_committed_gathered_snapshot()
    {
        using HttpResponseMessage move = await PostJson( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        move.EnsureSuccessStatusCode();
        using HttpResponseMessage gather = await _client.PostAsync( "/my/MockHero/action/gathering", null );
        gather.EnsureSuccessStatusCode();

        using HttpResponseMessage response = await _client.GetAsync( "/__mock/state/mockhero" );

        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        using JsonDocument state = JsonDocument.Parse( await response.Content!.ReadAsStringAsync() );
        Assert.Equal( "basic-mining", state.RootElement.GetProperty( "scenario" ).GetString() );
        Assert.Equal( 1, state.RootElement.GetProperty( "generation" ).GetInt64() );
        Assert.Equal( "Gathered", state.RootElement.GetProperty( "phase" ).GetString() );
        Assert.Equal( "2000-01-01T00:00:12.0000000Z", state.RootElement.GetProperty( "virtual_time" ).GetString() );
        Assert.Equal( "MockHero", state.RootElement.GetProperty( "character" ).GetProperty( "name" ).GetString() );
        Assert.Equal( 6, state.RootElement.GetProperty( "character" ).GetProperty( "mining_xp" ).GetInt32() );
        Character expectedCharacter = ExpectedScenario.Character();
        expectedCharacter.X = 2;
        expectedCharacter.MiningXp = 6;
        expectedCharacter.Inventory![ 0 ].Code = "copper_ore";
        expectedCharacter.Inventory![ 0 ].Quantity = 1;
        StateSummary stateDto = JsonSerializer.Deserialize<StateSummary>( state.RootElement.GetRawText(), JsonOptions )!;
        ScenarioAssertions.CharacterEquals( expectedCharacter, stateDto.Character! );
    }

    private async Task<HttpResponseMessage> PostJson( string path, string json )
    {
        StringContent request = new( json, Encoding.UTF8, "application/json" );
        HttpResponseMessage response = await _client.PostAsync( path, request );
        request.Dispose();
        return response;
    }


    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
