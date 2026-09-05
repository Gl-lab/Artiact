using System.Net;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Models.Api;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Artiact.MockService.Tests;

public sealed class CatalogScenarioTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web );
    private MockServiceFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new MockServiceFactory();
        _client = _factory.CreateClient( new WebApplicationFactoryClientOptions { BaseAddress = new Uri( "http://localhost" ) } );
        using StringContent reset = new( "{\"scenario\":\"basic-mining\"}", Encoding.UTF8, "application/json" );
        using HttpResponseMessage response = await _client.PostAsync( "/__mock/reset", reset );
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Page_one_catalogs_contain_the_exact_basic_mining_data()
    {
        using JsonDocument maps = await Get( "/maps?page=1" );
        Assert.Equivalent( ExpectedScenario.Maps(), JsonSerializer.Deserialize<Map>( maps.RootElement.GetRawText(), JsonOptions ), strict: true );
        AssertEnvelope( maps.RootElement, 2 );
        JsonElement mapData = maps.RootElement.GetProperty( "data" );
        Assert.Equal( "Origin", mapData[ 0 ].GetProperty( "name" ).GetString() );
        Assert.Equal( 0, mapData[ 0 ].GetProperty( "x" ).GetInt32() );
        Assert.Equal( "Copper Rocks", mapData[ 1 ].GetProperty( "name" ).GetString() );
        Assert.Equal( 2, mapData[ 1 ].GetProperty( "x" ).GetInt32() );
        Assert.Equal( "resource", mapData[ 1 ].GetProperty( "content" ).GetProperty( "type" ).GetString() );
        Assert.Equal( "copper_rocks", mapData[ 1 ].GetProperty( "content" ).GetProperty( "code" ).GetString() );

        using JsonDocument resources = await Get( "/resources?page=1" );
        Assert.Equivalent( ExpectedScenario.Resources(), JsonSerializer.Deserialize<ResourceResponse>( resources.RootElement.GetRawText(), JsonOptions ), strict: true );
        AssertEnvelope( resources.RootElement, 1 );
        JsonElement resource = resources.RootElement.GetProperty( "data" )[ 0 ];
        Assert.Equal( "copper_rocks", resource.GetProperty( "code" ).GetString() );
        Assert.Equal( "mining", resource.GetProperty( "skill" ).GetString() );
        Assert.Equal( "copper_ore", resource.GetProperty( "drops" )[ 0 ].GetProperty( "code" ).GetString() );
        Assert.Equal( 1, resource.GetProperty( "drops" )[ 0 ].GetProperty( "rate" ).GetInt32() );

        using JsonDocument items = await Get( "/items?page=1" );
        Assert.Equivalent( ExpectedScenario.Items(), JsonSerializer.Deserialize<ItemsResponse>( items.RootElement.GetRawText(), JsonOptions ), strict: true );
        AssertEnvelope( items.RootElement, 1 );
        JsonElement item = items.RootElement.GetProperty( "data" )[ 0 ];
        Assert.Equal( "copper_ore", item.GetProperty( "code" ).GetString() );
        Assert.Equal( "Basic mining ore.", item.GetProperty( "description" ).GetString() );
        Assert.False( item.GetProperty( "tradeable" ).GetBoolean() );

        using JsonDocument monsters = await Get( "/monsters?page=1" );
        Assert.Equivalent( ExpectedScenario.Monsters(), JsonSerializer.Deserialize<MonstersResponse>( monsters.RootElement.GetRawText(), JsonOptions ), strict: true );
        AssertEnvelope( monsters.RootElement, 0 );
    }

    [Theory]
    [InlineData( "/maps" )]
    [InlineData( "/maps?page=2" )]
    [InlineData( "/maps?page=0" )]
    [InlineData( "/maps?page=-1" )]
    [InlineData( "/resources?page=no" )]
    [InlineData( "/items?page=1&page=1" )]
    public async Task Invalid_catalog_page_has_stable_failure( string path )
    {
        using HttpResponseMessage response = await _client.GetAsync( path );
        Assert.Equal( HttpStatusCode.BadRequest, response.StatusCode );
        Assert.Equal( "application/problem+json", response.Content.Headers.ContentType?.MediaType );
        using JsonDocument problem = JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
        Assert.Equal( "invalid_page", problem.RootElement.GetProperty( "code" ).GetString() );
    }

    private async Task<JsonDocument> Get( string path )
    {
        using HttpResponseMessage response = await _client.GetAsync( path );
        Assert.Equal( HttpStatusCode.OK, response.StatusCode );
        return JsonDocument.Parse( await response.Content.ReadAsStringAsync() );
    }

    private static void AssertEnvelope( JsonElement root, int count )
    {
        Assert.Equal( count, root.GetProperty( "data" ).GetArrayLength() );
        Assert.Equal( count, root.GetProperty( "total" ).GetInt32() );
        Assert.Equal( 1, root.GetProperty( "page" ).GetInt32() );
        Assert.Equal( count, root.GetProperty( "size" ).GetInt32() );
        Assert.Equal( 1, root.GetProperty( "pages" ).GetInt32() );
    }


    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
