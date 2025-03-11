using System.Text.Json.Serialization;

namespace Artiact.Models;

public class TokenContainer
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}