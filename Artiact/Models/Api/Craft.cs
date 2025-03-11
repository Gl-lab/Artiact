using System.Text.Json.Serialization;

namespace Artiact.Models;

public class Craft
{
    [JsonPropertyName("skill")]
    public string Skill { get; set; }

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("items")]
    public List<Item> Items { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }
}