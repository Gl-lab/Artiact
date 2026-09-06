using System.Text.Json;
using Artiact.Contracts.Models.Api;

namespace CombatResearch;

public static class ParticipantProbe
{
    // Contract experiment only: validates participant identity/HP, not a complete fight envelope.
    public static int? ReadHp( string json, string name )
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse( json );
            JsonElement characters = document.RootElement.GetProperty( "data" ).GetProperty( "characters" );
            if ( characters.ValueKind != JsonValueKind.Array ) return null;
            int matches = 0;
            int? hp = null;
            foreach ( JsonElement character in characters.EnumerateArray() )
            {
                if ( character.GetProperty( "name" ).GetString() != name ) continue;
                matches++;
                if ( character.GetProperty( "hp" ).TryGetInt32( out int value ) && value >= 0 ) hp = value;
            }
            return matches == 1 ? hp : null;
        }
        catch ( Exception ex ) when ( ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException )
        {
            return null;
        }
    }
}

public class PayloadProbeTests
{
    private const string Fight = """
        {"data":{"cooldown":{"total_seconds":7},"fight":{"result":"win","turns":2,"opponent":"synthetic","logs":[],"characters":[]},
        "characters":[{"name":"other","hp":18},{"name":"researcher","hp":14}]}}
        """;

    [Fact]
    public void CurrentFightShapeDoesNotPopulateLegacyCharacter()
    {
        for ( int replay = 0; replay < 2; replay++ )
        {
            ActionResponse response = JsonSerializer.Deserialize<ActionResponse>( Fight )!;
            Assert.Null( response.Data!.Character! );
            Assert.Equal( 7, response.Data!.Cooldown!.TotalSeconds );
            Assert.Null( response.Data!.Details );
        }
    }

    [Theory]
    [InlineData( "{\"data\":{\"characters\":[{\"name\":\"researcher\",\"hp\":14}]}}", 14 )]
    [InlineData( "{\"data\":{\"characters\":[{\"name\":\"researcher\",\"hp\":0}]}}", 0 )]
    [InlineData( "{\"data\":{\"characters\":[{\"name\":\"researcher\",\"hp\":14},{\"name\":\"researcher\",\"hp\":1}]}}", null )]
    [InlineData( "{\"data\":{\"characters\":[{\"name\":\"Researcher\",\"hp\":14}]}}", null )]
    [InlineData( "{\"data\":{\"characters\":[{\"name\":\"researcher\"}]}}", null )]
    [InlineData( "{\"data\":{\"characters\":[{\"name\":\"researcher\",\"hp\":-1}]}}", null )]
    [InlineData( "{\"data\":{\"characters\":null}}", null )]
    [InlineData( "{\"data\":{\"characters\":[null]}}", null )]
    [InlineData( "{", null )]
    public void ParticipantSelectionIsExactAndFailClosed( string json, int? expected )
    {
        Assert.Equal( expected, ParticipantProbe.ReadHp( json, "researcher" ) );
        Assert.Equal( expected, ParticipantProbe.ReadHp( json, "researcher" ) );
    }

    [Fact]
    public void ParticipantIsSelectedByNameInsteadOfArrayPosition()
    {
        Assert.Equal( 14, ParticipantProbe.ReadHp( Fight, "researcher" ) );
        Assert.Equal( 14, ParticipantProbe.ReadHp( Fight, "researcher" ) );
    }

    [Fact]
    public void EquipmentRequestHasBothContainerAndSlotMismatch()
    {
        for ( int replay = 0; replay < 2; replay++ )
        {
            string legacy = JsonSerializer.Serialize( new Inventory { Code = "synthetic", Slot = 1, Quantity = 1 } );
            using JsonDocument document = JsonDocument.Parse( legacy );
            Assert.Equal( JsonValueKind.Object, document.RootElement.ValueKind );
            Assert.Equal( JsonValueKind.Number, document.RootElement.GetProperty( "slot" ).ValueKind );
            Assert.Throws<JsonException>( () => JsonSerializer.Deserialize<Inventory>( "{\"code\":\"synthetic\",\"slot\":\"weapon\"}" ) );
        }
    }

    [Fact]
    public void CurrentMapShapeLosesContentAndMonsterOmissionStaysUnknown()
    {
        for ( int replay = 0; replay < 2; replay++ )
        {
            MapPlace map = JsonSerializer.Deserialize<MapPlace>( """
                {"map_id":2,"x":1,"y":0,"layer":"overworld","access":{"type":"standard"},
                 "interactions":{"content":{"type":"monster","code":"synthetic"}}}
                """ )!;
            Assert.Null( map.Content );
            MonsterDatum monster = JsonSerializer.Deserialize<MonsterDatum>( "{\"hp\":20,\"attack_fire\":3}" )!;
            Assert.Null( monster.Effects );
            Assert.Equal( 0, monster.CriticalStrike ); // Missing required data silently defaults.
        }
    }

    [Theory]
    [InlineData( "hp_restored", "13" )]
    [InlineData( "items", "[]" )]
    public void RestAndEquipmentRetainCharacterButDiscardSpecificDetails( string field, string value )
    {
        string json = "{\"data\":{\"character\":{\"hp\":20},\"cooldown\":{\"total_seconds\":13},\"" + field + "\":" + value + "}}";
        for ( int replay = 0; replay < 2; replay++ )
        {
            ActionResponse response = JsonSerializer.Deserialize<ActionResponse>( json )!;
            Assert.Equal( 20, response.Data!.Character!.Hp );
            Assert.Equal( 13, response.Data!.Cooldown!.TotalSeconds );
            Assert.Null( response.Data!.Details );
        }
    }
}
