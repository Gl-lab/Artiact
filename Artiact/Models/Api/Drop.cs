using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class Drop
{
    [JsonPropertyName( "code" )]
    public string Code { get; set; }

    [JsonPropertyName( "rate" )]
    public int Rate { get; set; }

    [JsonPropertyName( "min_quantity" )]
    public int MinQuantity { get; set; }

    [JsonPropertyName( "max_quantity" )]
    public int MaxQuantity { get; set; }
}