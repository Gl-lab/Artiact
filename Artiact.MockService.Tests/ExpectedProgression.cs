using Artiact.Contracts.Models.Api;
using Artiact.SmartProxy.Models;
using Xunit;

namespace Artiact.MockService.Tests;

internal static class ExpectedProgression
{
    public static Character Character(int x = 0, int level = 1, int xp = 0, int copper = 0, int iron = 0)
    {
        var character = ExpectedScenario.Character();
        character.MiningMaxXp = 10; character.X = x; character.MiningLevel = level; character.MiningXp = xp;
        if(copper > 0) { character.Inventory![0].Code = "copper_ore"; character.Inventory![0].Quantity = copper; }
        if(iron > 0) { character.Inventory![1].Code = "iron_ore"; character.Inventory![1].Quantity = iron; }
        return character;
    }
    public static BasicMiningDefinition Definition(bool progression = true)
    {
        var maps = ExpectedScenario.Maps(); var resources = ExpectedScenario.Resources(); var items = ExpectedScenario.Items();
        if(progression)
        {
            maps.Data!.Add(new() { Name = "Iron Rocks", Skin = "rocks", X = 4, Y = 0, Content = new() { Type = "resource", Code = "iron_rocks" } });
            maps.Total = maps.Size = 3;
            resources.Data!.Add(new() { Name = "Iron Rocks", Code = "iron_rocks", Skill = "mining", Level = 2,
                Drops = [new() { Code = "iron_ore", Rate = 1, MinQuantity = 1, MaxQuantity = 1 }] });
            resources.Total = resources.Size = 2;
            items.Data!.Add(new() { Name = "Iron Ore", Code = "iron_ore", Level = 2, Type = "resource", Subtype = "mining",
                Description = "Progression mining ore.", Effects = [], Craft = null, Tradeable = false });
            items.Total = items.Size = 2;
        }
        return new() { Character = progression ? Character() : ExpectedScenario.Character(), Maps = maps,
            Resources = resources, Items = items, Monsters = ExpectedScenario.Monsters() };
    }

    // Literal after-state/cooldown oracles, independently specified for each successful action.
    private static readonly (int X, int Level, int Xp, int Copper, int Iron, int Start, int End, string? Ore)[] Actions =
    [
        (2, 1, 0, 0, 0, 0, 7, null),
        (2, 1, 6, 1, 0, 7, 12, "copper_ore"),
        (2, 2, 2, 2, 0, 12, 17, "copper_ore"),
        (4, 2, 2, 2, 0, 17, 24, null),
        (4, 2, 8, 2, 1, 24, 29, "iron_ore"),
        (4, 3, 4, 2, 2, 29, 34, "iron_ore"),
        (0, 3, 4, 2, 2, 34, 41, null)
    ];
    private static DateTime Time(int seconds) => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
    public static ActionResponse Action(int index)
    {
        var a = Actions[index];
        return new() { Data = new()
        {
            Character = Character(a.X, a.Level, a.Xp, a.Copper, a.Iron),
            Cooldown = new() { TotalSeconds = a.End - a.Start, RemainingSeconds = 0,
                StartedAt = Time(a.Start), Expiration = Time(a.End), Reason = "mock_virtual_elapsed" },
            Details = new() { Xp = a.Ore is null ? 0 : 6, Items = a.Ore is null ? [] : [new() { Code = a.Ore, Quantity = 1 }] },
            Destination = a.Ore is not null ? null! : new()
            {
                Name = a.X == 0 ? "Origin" : a.X == 2 ? "Copper Rocks" : "Iron Rocks",
                Skin = a.X == 0 ? "forest" : "rocks", X = a.X, Y = 0,
                Content = new() { Type = a.X == 0 ? "" : "resource", Code = a.X == 0 ? "" : a.X == 2 ? "copper_rocks" : "iron_rocks" }
            }
        } };
    }
    public static TraceEntry Trace(int index, long generation)
    {
        var a = Actions[index];
        return new(index + 1, generation, a.Ore is null ? "move" : "gathering", "MockHero",
            Time(a.Start).ToString("O"), Time(a.End).ToString("O"), a.End - a.Start,
            index == 0 ? 0 : Actions[index - 1].X, 0, a.X, 0, a.Ore is null ? 0 : 6, a.Ore, a.Ore is null ? 0 : 1);
    }
    public static void AssertAction(int index, ActionResponse actual)
    {
        var expected = Action(index);
        Assert.Equivalent(expected, actual, strict: true);
        ScenarioAssertions.CharacterEquals(expected.Data!.Character!, actual.Data!.Character!);
        Assert.Equal(expected.Data!.Details!.Items.Select(i => (i.Code, i.Quantity)), actual.Data!.Details!.Items.Select(i => (i.Code, i.Quantity)));
    }
}
