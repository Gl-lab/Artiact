using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class Inventory
{
    [JsonPropertyName( "slot" )]
    public int Slot { get; set; }

    [JsonPropertyName( "code" )]
    public string Code { get; set; }

    [JsonPropertyName( "quantity" )]
    public int Quantity { get; set; }
}