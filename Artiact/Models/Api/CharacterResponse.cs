using System.Text.Json.Serialization;

namespace Artiact.Models;

public class CharacterResponse
{
    [JsonPropertyName( "data" )]
    public Character Data { get; set; }
}