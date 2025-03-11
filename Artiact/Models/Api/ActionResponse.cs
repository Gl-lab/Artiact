using System.Text.Json.Serialization;

namespace Artiact.Models;

public class ActionResponse
{
    [JsonPropertyName( "data" )]
    public ActionData Data { get; set; }
}