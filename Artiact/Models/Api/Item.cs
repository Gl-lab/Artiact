using System.Text.Json.Serialization;

namespace Artiact.Models;

public class Item
{
    [JsonPropertyName( "code" )]
    public string Code { get; set; }

    [JsonPropertyName( "quantity" )]
    public int Quantity { get; set; }
}