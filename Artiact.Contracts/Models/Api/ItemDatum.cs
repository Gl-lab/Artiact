using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ItemDatum
{
    [JsonPropertyName( "name" )]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName( "code" )]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName( "level" )]
    public int Level { get; set; }

    [JsonPropertyName( "type" )]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName( "subtype" )]
    public string Subtype { get; set; } = string.Empty;

    [JsonPropertyName( "description" )]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName( "effects" )]
    public List<Effect>? Effects { get; set; }

    [JsonPropertyName( "craft" )]
    public Craft? Craft { get; set; }

    [JsonPropertyName( "tradeable" )]
    public bool Tradeable { get; set; }
}