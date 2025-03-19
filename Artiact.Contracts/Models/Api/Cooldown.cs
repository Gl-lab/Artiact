using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class Cooldown
{
    [JsonPropertyName( "total_seconds" )]
    public int TotalSeconds { get; set; }

    [JsonPropertyName( "remaining_seconds" )]
    public int RemainingSeconds { get; set; }

    [JsonPropertyName( "started_at" )]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName( "expiration" )]
    public DateTime Expiration { get; set; }

    [JsonPropertyName( "reason" )]
    public string Reason { get; set; }
}