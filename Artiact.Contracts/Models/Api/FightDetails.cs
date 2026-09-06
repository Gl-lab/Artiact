using System.Text.Json;
using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public sealed class FightDetails
{
    [JsonPropertyName("result")]
    public required string Result { get; set; }

    [JsonPropertyName("turns")]
    public required int Turns { get; set; }

    [JsonPropertyName("opponent")]
    public required string Opponent { get; set; }

    [JsonPropertyName("logs")]
    public required List<string> Logs { get; set; }

    [JsonPropertyName("characters")]
    public required JsonElement Characters { get; set; }
}
