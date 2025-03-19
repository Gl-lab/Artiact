using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ActionResponse
{
    [JsonPropertyName( "data" )]
    public ActionData Data { get; set; }
}