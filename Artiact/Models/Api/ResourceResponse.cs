using System.Text.Json.Serialization;

namespace Artiact.Models.Api;

public class ResourceResponse
{
    [JsonPropertyName( "data" )]
    public List<ResourceDatum> Data { get; set; }

    [JsonPropertyName( "total" )]
    public int Total { get; set; }

    [JsonPropertyName( "page" )]
    public int Page { get; set; }

    [JsonPropertyName( "size" )]
    public int Size { get; set; }

    [JsonPropertyName( "pages" )]
    public int Pages { get; set; }
}