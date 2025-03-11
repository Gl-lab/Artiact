using System.Text.Json.Serialization;

namespace Artiact.Models;


public class ResourceDatum
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("skill")]
    public string Skill { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("drops")]
    public List<Drop> Drops { get; set; }
}