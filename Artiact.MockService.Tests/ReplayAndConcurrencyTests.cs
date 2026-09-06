using System.Net;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class ReplayAndConcurrencyTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web );
    private MockServiceFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _client = _factory.CreateClient( new WebApplicationFactoryClientOptions { BaseAddress = new Uri( "http://localhost" ), AllowAutoRedirect = false } );
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Explicit_reset_replays_identical_normalized_state_and_trace()
    {
        var first = await RunScenario();
        var second = await RunScenario();

        Assert.Equivalent( first.reset with { Generation = 0 }, second.reset with { Generation = 0 }, strict: true );
        Assert.Equivalent( first.move, second.move, strict: true );
        ScenarioAssertions.CharacterEquals( first.move.Data!.Character!, second.move.Data!.Character! );
        Assert.Equivalent( first.gather, second.gather, strict: true );
        ScenarioAssertions.CharacterEquals( first.gather.Data!.Character!, second.gather.Data!.Character! );
        Assert.Equivalent( first.state with { Generation = 0 }, second.state with { Generation = 0 }, strict: true );
        ScenarioAssertions.CharacterEquals( first.state.Character!, second.state.Character! );
        Assert.Equal( first.trace.Length, second.trace.Length );
        for ( int index = 0; index < first.trace.Length; index++ )
        {
            Assert.Equivalent( first.trace[ index ] with { Generation = 0 }, second.trace[ index ] with { Generation = 0 }, strict: true );
        }
    }

    [Fact]
    public async Task Competing_moves_commit_once_without_duplicate_trace_sequence()
    {
        await ResetAndLoad();
        Task<HttpResponseMessage> first = Post( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        Task<HttpResponseMessage> second = Post( "/my/mockhero/action/move", "{\"x\":2,\"y\":0}" );

        HttpResponseMessage[] responses = await Task.WhenAll( first, second );

        Assert.Single( responses, response => response.StatusCode == HttpStatusCode.OK );
        Assert.Single( responses, response => response.StatusCode == HttpStatusCode.Conflict );
        using HttpResponseMessage traceResponse = await _client.GetAsync( "/__mock/trace" );
        using JsonDocument trace = JsonDocument.Parse( await traceResponse.Content!.ReadAsStringAsync() );
        Assert.Single( trace.RootElement.EnumerateArray() );
        Assert.Equal( 1, trace.RootElement[ 0 ].GetProperty( "sequence" ).GetInt64() );
        foreach ( HttpResponseMessage response in responses ) response.Dispose();
    }

    [Fact]
    public async Task Reset_competing_with_move_leaves_one_complete_new_generation()
    {
        await ResetAndLoad();
        Task<HttpResponseMessage> moveTask = Post( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        Task<HttpResponseMessage> resetTask = Post( "/__mock/reset", "{\"scenario\":\"basic-mining\"}" );
        HttpResponseMessage[] responses = await Task.WhenAll( moveTask, resetTask );

        Assert.Contains( responses, response => response.StatusCode == HttpStatusCode.OK );
        using HttpResponseMessage state = await _client.GetAsync( "/__mock/state/MockHero" );
        Assert.Equal( HttpStatusCode.Conflict, state.StatusCode );
        using HttpResponseMessage traceResponse = await _client.GetAsync( "/__mock/trace" );
        Assert.Equal( "[]", await traceResponse.Content!.ReadAsStringAsync() );
        foreach ( HttpResponseMessage response in responses ) response.Dispose();
    }

    [Fact]
    public async Task Read_competing_with_move_observes_ready_or_moved_but_never_partial_state()
    {
        await ResetAndLoad();
        Task<HttpResponseMessage> readTask = _client.GetAsync( "/__mock/state/MockHero" );
        Task<HttpResponseMessage> moveTask = Post( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        HttpResponseMessage[] responses = await Task.WhenAll( readTask, moveTask );

        using HttpResponseMessage read = responses[ 0 ];
        using JsonDocument snapshot = JsonDocument.Parse( await read.Content!.ReadAsStringAsync() );
        string phase = snapshot.RootElement.GetProperty( "phase" ).GetString()!;
        int x = snapshot.RootElement.GetProperty( "character" ).GetProperty( "x" ).GetInt32();
        Assert.True( ( phase == "Ready" && x == 0 ) || ( phase == "Moved" && x == 2 ) );

        using HttpResponseMessage finalState = await _client.GetAsync( "/__mock/state/MockHero" );
        string finalJson = await finalState.Content!.ReadAsStringAsync();
        Assert.Contains( "\"phase\":\"Moved\"", finalJson, StringComparison.Ordinal );
        Assert.Contains( "\"x\":2", finalJson, StringComparison.Ordinal );
        responses[ 1 ].Dispose();
    }

    private async Task<(ResetSummary reset, ActionResponse move, ActionResponse gather, StateSummary state, TraceEntry[] trace)> RunScenario()
    {
        ResetSummary reset = await ResetAndLoad();
        using HttpResponseMessage move = await Post( "/my/MockHero/action/move", "{\"x\":2,\"y\":0}" );
        move.EnsureSuccessStatusCode();
        using HttpResponseMessage gather = await Post( "/my/MockHero/action/gathering", null );
        gather.EnsureSuccessStatusCode();
        return (
            reset,
            Deserialize<ActionResponse>( await move.Content!.ReadAsStringAsync() ),
            Deserialize<ActionResponse>( await gather.Content!.ReadAsStringAsync() ),
            Deserialize<StateSummary>( await _client.GetStringAsync( "/__mock/state/MockHero" ) ),
            Deserialize<TraceEntry[]>( await _client.GetStringAsync( "/__mock/trace" ) ) );
    }

    private async Task<ResetSummary> ResetAndLoad()
    {
        using HttpResponseMessage reset = await Post( "/__mock/reset", "{\"scenario\":\"basic-mining\"}" );
        reset.EnsureSuccessStatusCode();
        ResetSummary summary = Deserialize<ResetSummary>( await reset.Content!.ReadAsStringAsync() );
        using HttpResponseMessage load = await _client.GetAsync( "/characters/MockHero" );
        load.EnsureSuccessStatusCode();
        return summary;
    }

    private Task<HttpResponseMessage> Post( string path, string? json )
    {
        HttpContent? content = json == null ? null : new StringContent( json, Encoding.UTF8, "application/json" );
        return _client.PostAsync( path, content );
    }

    private static T Deserialize<T>( string json ) =>
        JsonSerializer.Deserialize<T>( json, JsonOptions ) ?? throw new InvalidOperationException( $"Unable to deserialize {typeof( T ).Name}." );

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
