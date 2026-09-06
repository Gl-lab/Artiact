using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class MapPlace
{
    [JsonPropertyName( "name" )]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName( "skin" )]
    public string Skin { get; set; } = string.Empty;

    [JsonPropertyName( "x" )]
    public int X { get; set; }

    [JsonPropertyName( "y" )]
    public int Y { get; set; }

    [JsonPropertyName( "content" )]
    public Content? Content { get; set; }
}