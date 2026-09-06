using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ActionResponse
{
    [JsonPropertyName( "data" )]
    public ActionData? Data { get; set; }
    public ActionData RequireData() => Data ?? throw new InvalidOperationException("Action data is missing.");
    public Character RequireCharacter() => RequireData().Character ?? throw new InvalidOperationException("Action character is missing.");
    public Cooldown RequireCooldown() => RequireData().Cooldown ?? throw new InvalidOperationException("Action cooldown is missing.");
}
