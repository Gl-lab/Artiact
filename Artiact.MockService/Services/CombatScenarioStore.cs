using System.Text.Json.Nodes;

namespace Artiact.SmartProxy.Services;

// A deliberately scripted transition kernel, not a combat emulator. Every accepted
// transition commits its character, response and virtual trace together under one lock.
public sealed class CombatScenarioStore(IWebHostEnvironment environment)
{
    private readonly object _sync = new();
    private readonly JsonNode _fixture = JsonNode.Parse(File.ReadAllText(Path.Combine(environment.ContentRootPath, "CombatScenario.json")))!;
    private JsonNode? _character;
    private string? _scenario;
    private int _seconds;
    private readonly JsonArray _trace = [];
    private JsonArray _bank = [];
    private bool IsTraining => _scenario is "skill-preparation" or "resource-preparation" or "capacity-training";
    private bool IsProduction => _scenario is "item-production" or "item-production-bank" or "capacity-production" || IsTraining;
    private bool IsCombatCrafting => _scenario is "combat-crafting" or "combat-preparation";

    public (int Status, JsonNode Body)? Handle(string method, string path, string query, string body)
    {
        lock (_sync)
        {
            string[] parts = path.Split('/');
            if (parts.Length > 2 && parts[1] is "characters" or "my" &&
                string.Equals(parts[2], "researcher", StringComparison.OrdinalIgnoreCase))
            { parts[2] = "researcher"; path = string.Join('/', parts); }
            if (parts.Length == 4 && parts[1] == "__mock" && parts[2] == "state" &&
                string.Equals(parts[3], "researcher", StringComparison.OrdinalIgnoreCase))
            { parts[3] = "researcher"; path = string.Join('/', parts); }
            if (method == "POST" && path == "/__mock/reset")
            {
                string? scenario;
                try
                {
                    using var reset = System.Text.Json.JsonDocument.Parse(body);
                    if (reset.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
                    var properties = reset.RootElement.EnumerateObject().ToArray();
                    if (properties.Length != 1 || properties[0].Name != "scenario" ||
                        properties[0].Value.ValueKind != System.Text.Json.JsonValueKind.String) return null;
                    scenario = properties[0].Value.GetString();
                }
                catch (System.Text.Json.JsonException) { return null; }
                if (scenario is "basic-mining" or "mining-progression") { _scenario = null; return null; }
                if (scenario is not ("combat-progression" or "combat-equipment" or "combat-crafting" or "strategy-portfolio" or "gathering-bank" or "item-production" or "item-production-bank" or "combat-preparation" or "skill-preparation" or "resource-preparation" or "capacity-production" or "capacity-training")) return null;
                _scenario = scenario;
                _character = null;
                _seconds = 0;
                _trace.Clear();
                _bank = [];
                if (_scenario == "item-production-bank") _bank.Add(new JsonObject { ["code"] = "ore", ["quantity"] = 2 });
                return (200, new JsonObject { ["scenario"] = scenario, ["trace_count"] = 0 });
            }
            if (_scenario is null || path == "/token") return null;
            if ((_scenario == "gathering-bank" || IsProduction) && method == "GET" && path == "/my/bank")
                return (200, new JsonObject { ["data"] = new JsonObject { ["slots"] = 50, ["expansions"] = 0, ["gold"] = 0, ["next_expansion_cost"] = 3500 } });
            if ((_scenario == "gathering-bank" || IsProduction) && method == "GET" && path == "/my/bank/items")
                return (200, new JsonObject { ["data"] = _bank.DeepClone(), ["total"] = _bank.Count, ["page"] = 1, ["size"] = 50, ["pages"] = 1 });
            if ((_scenario is "strategy-portfolio" or "gathering-bank" or "combat-preparation" || IsProduction) && method == "GET" && path == "/openapi.json")
                return (200, JsonNode.Parse(File.ReadAllText(Path.Combine(environment.ContentRootPath, "StrategyOpenApiSubset.json")))!);
            if (method == "GET" && path == "/characters/researcher")
            {
                _character ??= Initial();
                return (200, new JsonObject { ["data"] = _character.DeepClone() });
            }
            if (method == "GET" && path == "/__mock/state/researcher")
                return _character is null ? Error(409, "character_not_initialized") :
                    (200, new JsonObject { ["character"] = _character.DeepClone(), ["virtual_seconds"] = _seconds });
            if (method == "GET" && path == "/__mock/trace") return (200, _trace.DeepClone());
            if (method == "GET" && path is "/maps" or "/monsters" or "/items" or "/resources")
            {
                if (query is not ("" or "?page=1")) return Error(400, "invalid_page");
                var data = Catalog(path[1..]);
                return (200, new JsonObject { ["data"] = data, ["total"] = data.AsArray().Count,
                    ["page"] = 1, ["size"] = 50, ["pages"] = 1 });
            }
            if (method != "POST" || !path.StartsWith("/my/researcher/action/", StringComparison.Ordinal))
                return Error(404, "unsupported_route");
            if (_character is null) return Error(409, "character_not_initialized");
            string action = path["/my/researcher/action/".Length..];
            var next = _character.DeepClone();
            var nextBank = _bank.DeepClone().AsArray();
            var dataResult = new JsonObject();
            int duration;
            try
            {
                switch (action)
                {
                    case "bank/deposit/item":
                    case "bank/withdraw/item":
                        if ((_scenario != "gathering-bank" && !IsProduction) || next["map_id"]!.GetValue<int>() != 6) return Error(598, "bank_not_found");
                        var deposits = JsonNode.Parse(body)!.AsArray();
                        if (deposits.Count is < 1 or > 20 || deposits.Select(x => x!["code"]!.GetValue<string>()).Distinct().Count() != deposits.Count)
                            return Error(422, "invalid_deposit");
                        foreach (var deposit in deposits)
                        {
                            string depositCode = deposit!["code"]!.GetValue<string>();
                            int quantity = deposit["quantity"]!.GetValue<int>();
                            if (action == "bank/withdraw/item")
                            {
                                var bankItem = nextBank.SingleOrDefault(x => x!["code"]!.GetValue<string>() == depositCode);
                                if (quantity <= 0 || bankItem is null || bankItem["quantity"]!.GetValue<int>() < quantity ||
                                    Used(next) + quantity > next["inventory_max_items"]!.GetValue<int>()) return Error(478, "withdraw_unavailable");
                                bankItem["quantity"] = bankItem["quantity"]!.GetValue<int>() - quantity;
                                if (bankItem["quantity"]!.GetValue<int>() == 0) nextBank.Remove(bankItem);
                                Add(next, depositCode, quantity); continue;
                            }
                            if (string.IsNullOrWhiteSpace(depositCode) || quantity <= 0 || !Add(next, depositCode, -quantity)) return Error(478, "missing_items");
                            var stock = nextBank.SingleOrDefault(x => x!["code"]!.GetValue<string>() == depositCode);
                            if (stock is null) nextBank.Add(new JsonObject { ["code"] = depositCode, ["quantity"] = quantity });
                            else stock["quantity"] = checked(stock["quantity"]!.GetValue<int>() + quantity);
                        }
                        if (nextBank.Count > 50) return Error(462, "bank_full");
                        dataResult["items"] = deposits.DeepClone(); dataResult["bank"] = nextBank.DeepClone();
                        duration = 3 * deposits.Count; break;
                    case "move":
                        var move = JsonNode.Parse(body);
                        if (move is not JsonObject moveObject || moveObject.Count != 1)
                            return Error(422, "destination_not_found");
                        int mapId = move["map_id"]!.GetValue<int>();
                        if (mapId != 2 && !(mapId == 3 && IsCombatCrafting) &&
                            !((_scenario is "strategy-portfolio" or "gathering-bank" || IsProduction) && mapId is 4 or 5) && !((_scenario == "gathering-bank" || IsProduction) && mapId == 6) && !(IsProduction && mapId == 3)) return Error(422, "destination_not_found");
                        next["map_id"] = mapId; next["x"] = mapId - 1;
                        dataResult["destination"] = Catalog("maps")[mapId - 1]!.DeepClone();
                        duration = 7; break;
                    case "gathering":
                        if ((!IsProduction && _scenario is not ("strategy-portfolio" or "gathering-bank" or "combat-preparation")) || !EmptyRequest(body) || Used(next) >= next["inventory_max_items"]!.GetValue<int>())
                            return Error(422, "gather_not_available");
                        int gatherMap = next["map_id"]!.GetValue<int>();
                        if (gatherMap is not (4 or 5)) return Error(422, "gather_not_available");
                        string skill = gatherMap == 4 ? "mining" : "woodcutting";
                        string output = gatherMap == 4 ? "ore" : "wood";
                        if (_scenario == "resource-preparation" && gatherMap == 5)
                        {
                            skill = "mining"; output = "rare_ore";
                            if (next["mining_level"]!.GetValue<int>() < 2) return Error(422, "skill_too_low");
                        }
                        if (next[skill + "_level"]!.GetValue<int>() >= (_scenario == "capacity-production" ? 50 : _scenario == "gathering-bank" || IsTraining ? 4 : 2)) return Error(422, "gather_not_available");
                        int skillXp = next[skill + "_xp"]!.GetValue<int>() + 5;
                        next[skill + "_level"] = next[skill + "_level"]!.GetValue<int>() + skillXp / 10;
                        next[skill + "_xp"] = skillXp % 10;
                        Add(next, output, 1);
                        dataResult["details"] = new JsonObject { ["xp"] = 5, ["items"] = new JsonArray(new JsonObject {
                            ["code"] = output, ["quantity"] = 1 }) };
                        duration = 5; break;
                    case "fight":
                        if (!EmptyRequest(body)) return Error(422, "invalid_request");
                        if (next["map_id"]!.GetValue<int>() != 2 || next["hp"]!.GetValue<int>() != 20 ||
                            (next["weapon_slot"]!.GetValue<string>() != "quick_blade" &&
                                !(_scenario == "strategy-portfolio" && next["weapon_slot"]!.GetValue<string>() == "old") &&
                                !(IsCombatCrafting && next["weapon_slot"]!.GetValue<string>() == "crafted_blade")) || Used(next) >= 10 ||
                            next["level"]!.GetValue<int>() >= (IsCombatCrafting ? 3 : 2)) return Error(422, "fight_not_available");
                        int xp = next["xp"]!.GetValue<int>() + 5;
                        int finalHp = next["weapon_slot"]!.GetValue<string>() == "crafted_blade" ? 17 :
                            next["weapon_slot"]!.GetValue<string>() == "old" ? 8 : 14;
                        next["xp"] = xp % 10; next["level"] = next["level"]!.GetValue<int>() + xp / 10; next["hp"] = finalHp;
                        Add(next, "feather", 1);
                        if (_scenario == "combat-preparation") Add(next, "shard", 1);
                        dataResult["fight"] = new JsonObject { ["result"] = "win", ["turns"] = finalHp == 8 ? 4 : 2, ["opponent"] = "dummy",
                            ["logs"] = new JsonArray(), ["characters"] = new JsonArray(new JsonObject {
                                ["character_name"] = "researcher", ["xp"] = 5, ["gold"] = 0, ["final_hp"] = finalHp,
                                ["drops"] = new JsonArray(new JsonObject { ["code"] = "feather", ["quantity"] = 1 }) }) };
                        dataResult["characters"] = new JsonArray(next.DeepClone());
                        if (_scenario == "combat-preparation") dataResult["fight"]!["characters"]![0]!["drops"]!.AsArray().Add(new JsonObject { ["code"] = "shard", ["quantity"] = 1 });
                        duration = 8; break;
                    case "rest":
                        if (!EmptyRequest(body)) return Error(422, "invalid_request");
                        int hp = next["hp"]!.GetValue<int>();
                        if (hp >= 20) return Error(422, "rest_not_available");
                        dataResult["hp_restored"] = 20 - hp; next["hp"] = 20;
                        duration = 6; break;
                    case "equip":
                    case "unequip":
                        var request = JsonNode.Parse(body)!.AsArray();
                        if (request.Count != 1 || request[0]!["slot"]!.GetValue<string>() != "weapon" ||
                            request[0]!["quantity"]!.GetValue<int>() != 1) return Error(422, "invalid_equipment");
                        string code;
                        if (action == "unequip")
                        {
                            code = next["weapon_slot"]!.GetValue<string>();
                            if ((code != "old" && !(IsCombatCrafting && code == "quick_blade")) || Used(next) >= 10) return Error(422, "invalid_equipment");
                            Add(next, code, 1); next["weapon_slot"] = ""; next["attack_fire"] = 0;
                        }
                        else
                        {
                            code = request[0]!["code"]!.GetValue<string>();
                            if ((code != "quick_blade" && !(IsCombatCrafting && code == "crafted_blade")) || next["weapon_slot"]!.GetValue<string>() != "" ||
                                !Add(next, code, -1)) return Error(422, "invalid_equipment");
                            next["weapon_slot"] = code; next["attack_fire"] = code == "crafted_blade" ? 20 : 10;
                        }
                        dataResult["items"] = new JsonArray(new JsonObject { ["code"] = code, ["slot"] = "weapon", ["quantity"] = 1 });
                        duration = 3; break;
                    case "crafting":
                        var requestCraft = JsonNode.Parse(body);
                        if (IsProduction)
                        {
                            string product = requestCraft!["code"]!.GetValue<string>();
                            int batch = requestCraft["quantity"]!.GetValue<int>();
                            if (IsTraining)
                            {
                                int required = (_scenario is "skill-preparation" or "capacity-training") && product == "tool" ? 2 : 1;
                                string ingredient = _scenario == "resource-preparation" && product == "tool" ? "rare_ore" : product == "bar" ? "ore" : "bar";
                                if (next["map_id"]!.GetValue<int>() != 3 || batch != 1 || product is not ("bar" or "tool") ||
                                    next["weaponcrafting_level"]!.GetValue<int>() < required || !Add(next, ingredient, -1)) return Error(422, "craft_not_available");
                                Add(next, product, 1);
                                if (Used(next) > next["inventory_max_items"]!.GetValue<int>()) return Error(422, "inventory_full");
                                int trainingXp = product == "bar" ? 5 : 1;
                                int progress = next["weaponcrafting_xp"]!.GetValue<int>() + trainingXp;
                                next["weaponcrafting_level"] = next["weaponcrafting_level"]!.GetValue<int>() + progress / 10;
                                next["weaponcrafting_xp"] = progress % 10;
                                dataResult["details"] = new JsonObject { ["xp"] = trainingXp, ["items"] = new JsonArray(new JsonObject { ["code"] = product, ["quantity"] = 1 }) };
                                duration = 4; break;
                            }
                            if (next["map_id"]!.GetValue<int>() != 3 || batch <= 0 || batch > 10 || product is not ("bar" or "tool") ||
                                !Add(next, product == "bar" ? "ore" : "bar", -batch * (product == "bar" ? 2 : 1))) return Error(422, "craft_not_available");
                            Add(next, product, batch); next["weaponcrafting_xp"] = next["weaponcrafting_xp"]!.GetValue<int>() + batch;
                            dataResult["details"] = new JsonObject { ["xp"] = batch, ["items"] = new JsonArray(new JsonObject { ["code"] = product, ["quantity"] = batch }) };
                            duration = 4; break;
                        }
                        if (!IsCombatCrafting || next["map_id"]!.GetValue<int>() != 3 ||
                            requestCraft is not JsonObject craftObject || craftObject.Count != 2 ||
                            requestCraft["code"]!.GetValue<string>() != "crafted_blade" || requestCraft["quantity"]!.GetValue<int>() != 1 ||
                            !Add(next, "feather", -1)) return Error(422, "craft_not_available");
                        Add(next, "crafted_blade", 1);
                        if (_scenario == "combat-preparation" && !Add(next, "shard", -1)) return Error(478, "missing_shard");
                        next["weaponcrafting_xp"] = next["weaponcrafting_xp"]!.GetValue<int>() + 1;
                        dataResult["details"] = new JsonObject { ["xp"] = 1, ["items"] = new JsonArray(new JsonObject {
                            ["code"] = "crafted_blade", ["quantity"] = 1 }) };
                        duration = 4; break;
                    default: return Error(404, "unsupported_route");
                }
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or NullReferenceException or FormatException)
            { return Error(422, "invalid_request"); }
            var epoch = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            dataResult["cooldown"] = new JsonObject { ["total_seconds"] = duration, ["remaining_seconds"] = 0,
                ["started_at"] = epoch.AddSeconds(_seconds).ToString("O"),
                ["expiration"] = epoch.AddSeconds(_seconds + duration).ToString("O"), ["reason"] = "mock_virtual_elapsed" };
            if (action != "fight") dataResult["character"] = next.DeepClone();
            _character = next;
            _bank = nextBank;
            _seconds += duration;
            _trace.Add(new JsonObject { ["sequence"] = _trace.Count + 1, ["action"] = action,
                ["duration_seconds"] = duration, ["virtual_seconds"] = _seconds });
            return (200, new JsonObject { ["data"] = dataResult });
        }
    }

    private JsonNode Initial()
    {
        var state = _fixture["character"]!.DeepClone();
        if (IsProduction) state["inventory"] = new JsonArray(new JsonObject { ["slot"] = 1, ["code"] = "protected", ["quantity"] = 1 });
        if (IsTraining) state["weaponcrafting_max_xp"] = 10;
        if (_scenario is "gathering-bank" or "capacity-production" or "capacity-training")
        {
            state["inventory_max_items"] = 3;
            state["inventory"] = new JsonArray(new JsonObject { ["slot"] = 1, ["code"] = "protected", ["quantity"] = 1 });
        }
        if (_scenario is "combat-equipment" or "strategy-portfolio")
        {
            state["weapon_slot"] = "old"; state["attack_fire"] = 5;
            Add(state, "quick_blade", 1); Add(state, "heavy_blade", 1);
        }
        return state;
    }
    private JsonArray Catalog(string name)
    {
        var data = _fixture[name]!.DeepClone().AsArray();
        if (_scenario == "combat-preparation")
        {
            if (name == "monsters") data[0]!["drops"]!.AsArray().Add(new JsonObject { ["code"] = "shard", ["rate"] = 1, ["min_quantity"] = 1, ["max_quantity"] = 1 });
            if (name == "items") data.Single(x => x!["code"]!.GetValue<string>() == "crafted_blade")!["craft"]!["items"]!.AsArray().Add(new JsonObject { ["code"] = "shard", ["quantity"] = 1 });
        }
        if (!IsProduction && _scenario is not ("strategy-portfolio" or "gathering-bank" or "combat-preparation")) return data;
        if (name == "maps")
        {
            data.Add(JsonNode.Parse("""{"map_id":4,"name":"Mine","skin":"plain","x":3,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"resource","code":"ore_node"},"transition":null}}"""));
            data.Add(JsonNode.Parse("""{"map_id":5,"name":"Forest","skin":"plain","x":4,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"resource","code":"wood_node"},"transition":null}}"""));
            if (_scenario == "gathering-bank" || IsProduction) data.Add(JsonNode.Parse("""{"map_id":6,"name":"Bank","skin":"plain","x":5,"y":0,"layer":"overworld","access":{"type":"standard","conditions":[]},"interactions":{"content":{"type":"bank","code":"bank"},"transition":null}}"""));
        }
        if (name == "resources")
        {
            data.Add(JsonNode.Parse("""{"name":"Ore","code":"ore_node","skill":"mining","level":1,"drops":[{"code":"ore","rate":1,"min_quantity":1,"max_quantity":1}]}"""));
            data.Add(JsonNode.Parse("""{"name":"Wood","code":"wood_node","skill":"woodcutting","level":1,"drops":[{"code":"wood","rate":1,"min_quantity":1,"max_quantity":1}]}"""));
        }
        if (name == "items" && IsProduction)
        {
            data.Add(JsonNode.Parse("""{"code":"bar","conditions":[],"craft":{"skill":"weaponcrafting","level":1,"quantity":1,"items":[{"code":"ore","quantity":2}]}}"""));
            data.Add(JsonNode.Parse("""{"code":"tool","conditions":[],"craft":{"skill":"weaponcrafting","level":1,"quantity":1,"items":[{"code":"bar","quantity":1}]}}"""));
        }
        if (IsTraining && name == "items")
        {
            var bar = data.Single(x => x!["code"]!.GetValue<string>() == "bar")!;
            bar["level"] = 1; bar["craft"]!["items"]![0]!["quantity"] = 1;
            var tool = data.Single(x => x!["code"]!.GetValue<string>() == "tool")!;
            tool["level"] = 2;
            if (_scenario is "skill-preparation" or "capacity-training") tool["craft"]!["level"] = 2;
            else tool["craft"]!["items"]![0]!["code"] = "rare_ore";
        }
        if (_scenario == "resource-preparation" && name == "maps")
            data[4]!["interactions"]!["content"]!["code"] = "rare_node";
        if (_scenario == "resource-preparation" && name == "resources")
        {
            var rare = data.Single(x => x!["code"]!.GetValue<string>() == "wood_node")!;
            rare["code"] = "rare_node"; rare["skill"] = "mining"; rare["level"] = 2;
            rare["drops"]![0]!["code"] = "rare_ore";
        }
        return data;
    }
    private static int Used(JsonNode state) => state["inventory"]!.AsArray().Sum(x => x!["quantity"]!.GetValue<int>());
    private static bool EmptyRequest(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return true;
        using var document = System.Text.Json.JsonDocument.Parse(body);
        return document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
            !document.RootElement.EnumerateObject().Any();
    }
    private static bool Add(JsonNode state, string code, int delta)
    {
        var inventory = state["inventory"]!.AsArray();
        var item = inventory.SingleOrDefault(x => x!["code"]!.GetValue<string>() == code);
        int quantity = (item?["quantity"]?.GetValue<int>() ?? 0) + delta;
        if (quantity < 0) return false;
        if (item is not null) inventory.Remove(item);
        if (quantity > 0) inventory.Add(new JsonObject { ["slot"] = inventory.Count + 1, ["code"] = code, ["quantity"] = quantity });
        for (int i = 0; i < inventory.Count; i++) inventory[i]!["slot"] = i + 1;
        return true;
    }
    private static (int, JsonNode) Error(int status, string code) => (status, new JsonObject { ["code"] = code });
}
