using System.Text.Json.Nodes;
using Xunit;

namespace Artiact.MockService.Tests;

// Independently authored wire oracle. Never loads the production scenario file or
// computes XP/damage/equipment effects using the transition kernel or predictor.
internal static class ExpectedCombat
{
    private const string Character = """
        {"name":"researcher","account":"synthetic","skin":"man1","level":1,"xp":0,"max_xp":10,"gold":0,"speed":0,
        "mining_level":1,"mining_xp":0,"mining_max_xp":10,"woodcutting_level":1,"woodcutting_xp":0,"woodcutting_max_xp":10,
        "fishing_level":1,"fishing_xp":0,"fishing_max_xp":10,"weaponcrafting_level":1,"weaponcrafting_xp":0,"weaponcrafting_max_xp":10,
        "gearcrafting_level":1,"gearcrafting_xp":0,"gearcrafting_max_xp":10,"jewelrycrafting_level":1,"jewelrycrafting_xp":0,"jewelrycrafting_max_xp":10,
        "cooking_level":1,"cooking_xp":0,"cooking_max_xp":10,"alchemy_level":1,"alchemy_xp":0,"alchemy_max_xp":10,
        "hp":20,"max_hp":20,"haste":0,"critical_strike":0,"wisdom":0,"prospecting":0,"initiative":0,"threat":0,
        "attack_fire":10,"attack_earth":0,"attack_water":0,"attack_air":0,"dmg":0,"dmg_fire":0,"dmg_earth":0,"dmg_water":0,"dmg_air":0,
        "res_fire":0,"res_earth":0,"res_water":0,"res_air":0,"effects":[],"x":0,"y":0,"layer":"overworld","map_id":1,
        "cooldown":0,"cooldown_expiration":"2000-01-01T00:00:00Z","weapon_slot":"quick_blade","rune_slot":"","shield_slot":"",
        "helmet_slot":"","body_armor_slot":"","leg_armor_slot":"","boots_slot":"","ring1_slot":"","ring2_slot":"","amulet_slot":"",
        "artifact1_slot":"","artifact2_slot":"","artifact3_slot":"","utility1_slot":"","utility1_slot_quantity":0,"utility2_slot":"","utility2_slot_quantity":0,
        "bag_slot":"","task":"","task_type":"","task_progress":0,"task_total":0,"inventory_max_items":10,"inventory":[]}
        """;

    private sealed record Frame(string Action, int Start, int End, int Level, int Xp, int Hp, int Map,
        string Weapon, int Attack, string Inventory, int MiningLevel = 1, int MiningXp = 0, int WoodLevel = 1, int WoodXp = 0);

    public static void AssertPortfolioResponses(IReadOnlyList<(string Path, string Body)> responses) => AssertFrames(
    [
        new("unequip",0,3,1,0,20,1,"",0,"[{\"slot\":1,\"code\":\"quick_blade\",\"quantity\":1},{\"slot\":2,\"code\":\"heavy_blade\",\"quantity\":1},{\"slot\":3,\"code\":\"old\",\"quantity\":1}]"),
        new("equip",3,6,1,0,20,1,"quick_blade",10,PortfolioInventory(0,0,0)),
        new("move",6,13,1,0,20,4,"quick_blade",10,PortfolioInventory(0,0,0)),
        new("gathering",13,18,1,0,20,4,"quick_blade",10,PortfolioInventory(1,0,0),1,5),
        new("gathering",18,23,1,0,20,4,"quick_blade",10,PortfolioInventory(2,0,0),2,0),
        new("move",23,30,1,0,20,5,"quick_blade",10,PortfolioInventory(2,0,0),2,0),
        new("gathering",30,35,1,0,20,5,"quick_blade",10,PortfolioInventory(2,1,0),2,0,1,5),
        new("gathering",35,40,1,0,20,5,"quick_blade",10,PortfolioInventory(2,2,0),2,0,2,0),
        new("move",40,47,1,0,20,2,"quick_blade",10,PortfolioInventory(2,2,0),2,0,2,0),
        new("fight",47,55,1,5,14,2,"quick_blade",10,PortfolioInventory(2,2,1),2,0,2,0),
        new("rest",55,61,1,5,20,2,"quick_blade",10,PortfolioInventory(2,2,1),2,0,2,0),
        new("fight",61,69,2,0,14,2,"quick_blade",10,PortfolioInventory(2,2,2),2,0,2,0)
    ], responses);

    private static string PortfolioInventory(int ore, int wood, int feather)
    {
        var stock = JsonNode.Parse(GearInventory(0))!.AsArray();
        foreach (var item in new[] { ("ore", ore), ("wood", wood), ("feather", feather) })
            if (item.Item2 > 0) stock.Add(new JsonObject { ["slot"] = stock.Count + 1, ["code"] = item.Item1, ["quantity"] = item.Item2 });
        return stock.ToJsonString();
    }

    public static void AssertResponses(bool gear, IReadOnlyList<(string Path, string Body)> responses)
    {
        Frame[] frames = gear ?
        [
            new("unequip",0,3,1,0,20,1,"",0,"[{\"slot\":1,\"code\":\"quick_blade\",\"quantity\":1},{\"slot\":2,\"code\":\"heavy_blade\",\"quantity\":1},{\"slot\":3,\"code\":\"old\",\"quantity\":1}]"),
            new("equip",3,6,1,0,20,1,"quick_blade",10,GearInventory(0)),
            new("move",6,13,1,0,20,2,"quick_blade",10,GearInventory(0)),
            new("fight",13,21,1,5,14,2,"quick_blade",10,GearInventory(1)),
            new("rest",21,27,1,5,20,2,"quick_blade",10,GearInventory(1)),
            new("fight",27,35,2,0,14,2,"quick_blade",10,GearInventory(2))
        ] :
        [
            new("move",0,7,1,0,20,2,"quick_blade",10,"[]"),
            new("fight",7,15,1,5,14,2,"quick_blade",10,"[{\"slot\":1,\"code\":\"feather\",\"quantity\":1}]"),
            new("rest",15,21,1,5,20,2,"quick_blade",10,"[{\"slot\":1,\"code\":\"feather\",\"quantity\":1}]"),
            new("fight",21,29,2,0,14,2,"quick_blade",10,"[{\"slot\":1,\"code\":\"feather\",\"quantity\":2}]")
        ];
        AssertFrames(frames, responses);
    }

    public static void AssertCraftResponses(IReadOnlyList<(string Path, string Body)> responses) => AssertFrames(
    [
        new("move",0,7,1,0,20,2,"quick_blade",10,"[]"),
        new("fight",7,15,1,5,14,2,"quick_blade",10,"[{\"slot\":1,\"code\":\"feather\",\"quantity\":1}]"),
        new("move",15,22,1,5,14,3,"quick_blade",10,"[{\"slot\":1,\"code\":\"feather\",\"quantity\":1}]"),
        new("crafting",22,26,1,5,14,3,"quick_blade",10,"[{\"slot\":1,\"code\":\"crafted_blade\",\"quantity\":1}]"),
        new("unequip",26,29,1,5,14,3,"",0,"[{\"slot\":1,\"code\":\"crafted_blade\",\"quantity\":1},{\"slot\":2,\"code\":\"quick_blade\",\"quantity\":1}]"),
        new("equip",29,32,1,5,14,3,"crafted_blade",20,CraftInventory(0)),
        new("rest",32,38,1,5,20,3,"crafted_blade",20,CraftInventory(0)),
        new("move",38,45,1,5,20,2,"crafted_blade",20,CraftInventory(0)),
        new("fight",45,53,2,0,17,2,"crafted_blade",20,CraftInventory(1)),
        new("rest",53,59,2,0,20,2,"crafted_blade",20,CraftInventory(1)),
        new("fight",59,67,2,5,17,2,"crafted_blade",20,CraftInventory(2)),
        new("rest",67,73,2,5,20,2,"crafted_blade",20,CraftInventory(2)),
        new("fight",73,81,3,0,17,2,"crafted_blade",20,CraftInventory(3))
    ], responses, crafting: true);

    private static string CraftInventory(int feathers) => feathers switch
    {
        0 => """[{"slot":1,"code":"quick_blade","quantity":1}]""",
        1 => """[{"slot":1,"code":"quick_blade","quantity":1},{"slot":2,"code":"feather","quantity":1}]""",
        2 => """[{"slot":1,"code":"quick_blade","quantity":1},{"slot":2,"code":"feather","quantity":2}]""",
        3 => """[{"slot":1,"code":"quick_blade","quantity":1},{"slot":2,"code":"feather","quantity":3}]""",
        _ => throw new ArgumentOutOfRangeException(nameof(feathers))
    };

    private static void AssertFrames(Frame[] frames, IReadOnlyList<(string Path, string Body)> responses, bool crafting = false)
    {
        Assert.Equal(frames.Length, responses.Count);
        for (int i = 0; i < frames.Length; i++)
        {
            var frame = frames[i];
            Assert.Equal("/my/researcher/action/" + frame.Action, responses[i].Path);
            var character = JsonNode.Parse(Character)!;
            character["level"] = frame.Level; character["xp"] = frame.Xp; character["hp"] = frame.Hp;
            character["map_id"] = frame.Map; character["x"] = frame.Map - 1;
            character["mining_level"] = frame.MiningLevel; character["mining_xp"] = frame.MiningXp;
            character["woodcutting_level"] = frame.WoodLevel; character["woodcutting_xp"] = frame.WoodXp;
            if (crafting && frame.End >= 26) character["weaponcrafting_xp"] = 1;
            character["weapon_slot"] = frame.Weapon; character["attack_fire"] = frame.Attack;
            character["inventory"] = JsonNode.Parse(frame.Inventory);
            var data = new JsonObject { ["cooldown"] = new JsonObject {
                ["total_seconds"] = frame.End - frame.Start, ["remaining_seconds"] = 0,
                ["started_at"] = Timestamp(frame.Start),
                ["expiration"] = Timestamp(frame.End), ["reason"] = "mock_virtual_elapsed" } };
            if (frame.Action == "fight")
            {
                data["characters"] = new JsonArray(character);
                data["fight"] = JsonNode.Parse("""
                    {"result":"win","turns":2,"opponent":"dummy","logs":[],"characters":[{"character_name":"researcher","xp":5,"gold":0,"final_hp":14,"drops":[{"code":"feather","quantity":1}]}]}
                    """);
                data["fight"]!["characters"]![0]!["final_hp"] = frame.Hp;
            }
            else data["character"] = character;
            if (frame.Action == "rest") data["hp_restored"] = crafting && frame.Start > 38 ? 3 : 6;
            if (frame.Action is "equip" or "unequip") data["items"] = new JsonArray(new JsonObject {
                ["code"] = crafting ? frame.Action == "equip" ? "crafted_blade" : "quick_blade" :
                    frame.Action == "equip" ? "quick_blade" : "old", ["slot"] = "weapon", ["quantity"] = 1 });
            if (frame.Action == "move") data["destination"] = JsonNode.Parse("""
                {"map_id":2,"name":"Arena","skin":"plain","x":1,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"monster","code":"dummy"},"transition":null}}
                """);
            if (frame.Action == "move" && frame.Map == 3) data["destination"] = JsonNode.Parse("""
                {"map_id":3,"name":"Workshop","skin":"plain","x":2,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"workshop","code":"weaponcrafting"},"transition":null}}
                """);
            if (frame.Action == "crafting") data["details"] = JsonNode.Parse("""{"xp":1,"items":[{"code":"crafted_blade","quantity":1}]}""");
            if (frame.Action == "move" && frame.Map == 4) data["destination"] = JsonNode.Parse("""
                {"map_id":4,"name":"Mine","skin":"plain","x":3,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"resource","code":"ore_node"},"transition":null}}
                """);
            if (frame.Action == "move" && frame.Map == 5) data["destination"] = JsonNode.Parse("""
                {"map_id":5,"name":"Forest","skin":"plain","x":4,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"resource","code":"wood_node"},"transition":null}}
                """);
            if (frame.Action == "gathering") data["details"] = new JsonObject { ["xp"] = 5,
                ["items"] = new JsonArray(new JsonObject { ["code"] = frame.Map == 4 ? "ore" : "wood", ["quantity"] = 1 }) };
            Assert.True(JsonNode.DeepEquals(new JsonObject { ["data"] = data }, JsonNode.Parse(responses[i].Body)),
                $"Complete response mismatch at command {i + 1}: {frame.Action}");
        }
    }

    private static string Timestamp(int seconds) => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds).ToString("O");
    private static string GearInventory(int feathers) => feathers switch
    {
        0 => """[{"slot":1,"code":"heavy_blade","quantity":1},{"slot":2,"code":"old","quantity":1}]""",
        1 => """[{"slot":1,"code":"heavy_blade","quantity":1},{"slot":2,"code":"old","quantity":1},{"slot":3,"code":"feather","quantity":1}]""",
        2 => """[{"slot":1,"code":"heavy_blade","quantity":1},{"slot":2,"code":"old","quantity":1},{"slot":3,"code":"feather","quantity":2}]""",
        _ => throw new ArgumentOutOfRangeException(nameof(feathers))
    };
}
