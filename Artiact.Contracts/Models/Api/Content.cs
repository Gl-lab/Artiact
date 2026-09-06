using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class Content
{
    [JsonPropertyName( "type" )]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName( "code" )]
    public string Code { get; set; } = string.Empty;
}