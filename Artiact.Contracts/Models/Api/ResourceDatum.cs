using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ResourceDatum
{
    [JsonPropertyName( "name" )]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName( "code" )]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName( "skill" )]
    public string? Skill { get; set; }

    [JsonPropertyName( "level" )]
    public int Level { get; set; }

    [JsonPropertyName( "drops" )]
    public List<Drop>? Drops { get; set; }
}