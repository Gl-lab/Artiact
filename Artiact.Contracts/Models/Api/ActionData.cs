using System.Text.Json.Serialization;

namespace Artiact.Contracts.Models.Api;

public class ActionData
{
    [JsonPropertyName( "cooldown" )]
    public Cooldown Cooldown { get; set; }

    [JsonPropertyName( "destination" )]
    public Destination Destination { get; set; }

    [JsonPropertyName( "details" )]
    public Details Details { get; set; }

    [JsonPropertyName( "character" )]
    public Character Character { get; set; }

    [JsonPropertyName( "characters" )]
    public List<Character>? Characters { get; set; }

    [JsonPropertyName( "fight" )]
    public FightDetails? Fight { get; set; }

    [JsonPropertyName( "hp_restored" )]
    public int? HpRestored { get; set; }

    [JsonPropertyName( "items" )]
    public System.Text.Json.JsonElement? EquipmentItems { get; set; }
}
