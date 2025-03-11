using System.Text.Json.Serialization;

namespace Artiact.Models;

public class MonstersResponse
{
    [JsonPropertyName("data")]
    public List<MonsterDatum> Data { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("pages")]
    public int Pages { get; set; }
}