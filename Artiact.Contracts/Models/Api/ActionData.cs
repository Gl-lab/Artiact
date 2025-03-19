using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ActionData
{
    [JsonPropertyName( "cooldown" )]
    public Cooldown Cooldown { get; set; }

    [JsonPropertyName( "destination" )]
    public Destination Destination { get; set; }

    [JsonPropertyName( "details" )]
    public Details Details { get; set; }

    [JsonPropertyName( "character" )]
    public Character Character { get; set; }
}