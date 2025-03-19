using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class TokenContainer
{
    [JsonPropertyName( "token" )]
    public string? Token { get; set; }
}