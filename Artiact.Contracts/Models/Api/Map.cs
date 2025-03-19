using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class Map
{
    [JsonPropertyName( "data" )]
    public List<MapPlace> Data { get; set; }

    [JsonPropertyName( "total" )]
    public int Total { get; set; }

    [JsonPropertyName( "page" )]
    public int Page { get; set; }

    [JsonPropertyName( "size" )]
    public int Size { get; set; }

    [JsonPropertyName( "pages" )]
    public int Pages { get; set; }
}