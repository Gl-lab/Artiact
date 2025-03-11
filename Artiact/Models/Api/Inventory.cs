using System.Text.Json.Serialization;

namespace Artiact.Models;

public class Inventory
{
    [JsonPropertyName( "slot" )]
    public int Slot { get; set; }

    [JsonPropertyName( "code" )]
    public string Code { get; set; }

    [JsonPropertyName( "quantity" )]
    public int Quantity { get; set; }
}