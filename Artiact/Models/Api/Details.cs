using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class Details
{
    [JsonPropertyName( "xp" )]
    public int Xp { get; set; }

    [JsonPropertyName( "items" )]
    public List<Item> Items { get; set; }
}