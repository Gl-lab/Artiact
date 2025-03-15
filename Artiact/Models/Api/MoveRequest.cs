using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class MoveRequest
{
    [JsonPropertyName( "x" )]
    public int X { get; set; }

    [JsonPropertyName( "y" )]
    public int Y { get; set; }
}