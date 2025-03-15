using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class Destination
{
    [JsonPropertyName( "name" )]
    public string Name { get; set; }

    [JsonPropertyName( "skin" )]
    public string Skin { get; set; }

    [JsonPropertyName( "x" )]
    public int X { get; set; }

    [JsonPropertyName( "y" )]
    public int Y { get; set; }

    [JsonPropertyName( "content" )]
    public Content Content { get; set; }
}