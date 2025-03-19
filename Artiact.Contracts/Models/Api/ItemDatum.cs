using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ItemDatum
{
    [JsonPropertyName( "name" )]
    public string Name { get; set; }

    [JsonPropertyName( "code" )]
    public string Code { get; set; }

    [JsonPropertyName( "level" )]
    public int Level { get; set; }

    [JsonPropertyName( "type" )]
    public string Type { get; set; }

    [JsonPropertyName( "subtype" )]
    public string Subtype { get; set; }

    [JsonPropertyName( "description" )]
    public string Description { get; set; }

    [JsonPropertyName( "effects" )]
    public List<Effect> Effects { get; set; }

    [JsonPropertyName( "craft" )]
    public Craft? Craft { get; set; }

    [JsonPropertyName( "tradeable" )]
    public bool Tradeable { get; set; }
}