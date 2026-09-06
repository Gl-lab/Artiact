using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public sealed record EquipRequest(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("slot")] string Slot,
    [property: JsonPropertyName("quantity")] int Quantity = 1);

public sealed record UnequipRequest(
    [property: JsonPropertyName("slot")] string Slot,
    [property: JsonPropertyName("quantity")] int Quantity = 1);
