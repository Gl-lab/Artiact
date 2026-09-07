using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact.Services.Combat;

namespace Artiact.Tests.Services;

public class EquipmentProjectionTests
{
    private static JsonElement Item(string code, string type, string effects) => JsonSerializer.SerializeToElement(JsonNode.Parse(
        "{\"code\":\"" + code + "\",\"type\":\"" + type + "\",\"level\":1,\"conditions\":[],\"effects\":" + effects + "}"));

    [Fact]
    public void MixedWeaponReplacementSubtractsOldChannelsAndKeepsDefense()
    {
        var state = JsonNode.Parse(CombatObservationTests.CharacterJson)!;
        state["attack_water"] = 4; state["res_air"] = 25;
        var old = Item("quick_blade", "weapon", "[{\"code\":\"attack_fire\",\"value\":10},{\"code\":\"attack_water\",\"value\":4}]");
        var replacement = Item("mixed", "weapon", "[{\"code\":\"attack_earth\",\"value\":8},{\"code\":\"attack_water\",\"value\":6},{\"code\":\"dmg_water\",\"value\":50}]");
        var result = EquipmentProjection.Replace(JsonSerializer.SerializeToElement(state), [old, replacement], "weapon", "mixed")!.Value;
        Assert.Equal(0, result.GetProperty("attack_fire").GetInt32()); Assert.Equal(6, result.GetProperty("attack_water").GetInt32());
        Assert.Equal(8, result.GetProperty("attack_earth").GetInt32()); Assert.Equal(25, result.GetProperty("res_air").GetInt32());
        Assert.Equal(50, result.GetProperty("dmg_water").GetInt32());
        Assert.Equal(CombatViability.Safe, CombatPrediction.Evaluate(CombatObservation.Read(result)!.Stats, new(17, 3)).Viability);
    }

    [Theory]
    [InlineData("hp", 10)]
    [InlineData("poison", 1)]
    [InlineData("attack_water", -1)]
    public void UnsupportedEquipmentCannotBeProjected(string effect, int value)
    {
        Assert.False(EquipmentProjection.Supported(Item("bad", "shield", "[{\"code\":\"" + effect + "\",\"value\":" + value + "}]"), "shield", 1));
    }

    [Fact]
    public void InconsistentOldStatsCannotInventNegativeBaseline()
    {
        var old = Item("quick_blade", "weapon", "[{\"code\":\"attack_fire\",\"value\":11}]");
        Assert.Null(EquipmentProjection.Replace(JsonDocument.Parse(CombatObservationTests.CharacterJson).RootElement, [old], "weapon", ""));
    }
}
