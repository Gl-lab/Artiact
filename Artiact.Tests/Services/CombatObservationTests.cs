using System.Text.Json;
using System.Text.Json.Nodes;
using Artiact.Services.Combat;

namespace Artiact.Tests.Services;

public class CombatObservationTests
{
    internal const string CharacterJson = """
        {"name":"researcher","level":1,"xp":0,"max_xp":10,"hp":20,"max_hp":20,
         "map_id":1,"layer":"overworld","weapon_slot":"quick_blade","inventory_max_items":10,"inventory":[],
         "attack_fire":10,"attack_earth":0,"attack_water":0,"attack_air":0,
         "dmg":0,"dmg_fire":0,"dmg_earth":0,"dmg_water":0,"dmg_air":0,
         "res_fire":0,"res_earth":0,"res_water":0,"res_air":0,"critical_strike":0,"effects":[]}
        """;

    [Fact]
    public void CompleteSupportedObservationRetainsFacts()
    {
        using var doc = JsonDocument.Parse(CharacterJson);
        var state = Assert.IsType<CombatObservation>(CombatObservation.Read(doc.RootElement));
        Assert.Equal(10, state.Stats.Attack);
        Assert.Equal(10, state.FreeUnits);
        Assert.Equal(1, state.MapId);
    }

    [Theory]
    [InlineData("attack_fire")]
    [InlineData("res_fire")]
    [InlineData("critical_strike")]
    [InlineData("map_id")]
    [InlineData("weapon_slot")]
    [InlineData("inventory")]
    public void MissingRequiredFieldDoesNotBecomeZero(string key)
    {
        var json = JsonNode.Parse(CharacterJson)!;
        json.AsObject().Remove(key);
        using var doc = JsonDocument.Parse(json.ToJsonString());
        Assert.Null(CombatObservation.Read(doc.RootElement));
    }

    [Theory]
    [InlineData("attack_air", "1")]
    [InlineData("effects", "[{\"code\":\"poison\",\"value\":1}]")]
    [InlineData("inventory", "[{\"code\":\"ore\",\"quantity\":11}]")]
    [InlineData("inventory", "[{\"code\":\"ore\",\"quantity\":-1}]")]
    [InlineData("max_hp", "0")]
    public void UnsupportedOrInvalidStateFailsClosed(string key, string value)
    {
        var json = JsonNode.Parse(CharacterJson)!;
        json[key] = JsonNode.Parse(value);
        using var doc = JsonDocument.Parse(json.ToJsonString());
        Assert.Null(CombatObservation.Read(doc.RootElement));
    }
}
