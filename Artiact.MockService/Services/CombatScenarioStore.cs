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
                if (scenario is not ("combat-progression" or "combat-equipment")) return null;
                _scenario = scenario;
                _character = null;
                _seconds = 0;
                _trace.Clear();
                return (200, new JsonObject { ["scenario"] = scenario, ["trace_count"] = 0 });
            }
            if (_scenario is null || path == "/token") return null;
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
                var data = _fixture[path[1..]]!.DeepClone();
                return (200, new JsonObject { ["data"] = data, ["total"] = data.AsArray().Count,
                    ["page"] = 1, ["size"] = 50, ["pages"] = 1 });
            }
            if (method != "POST" || !path.StartsWith("/my/researcher/action/", StringComparison.Ordinal))
                return Error(404, "unsupported_route");
            if (_character is null) return Error(409, "character_not_initialized");
            string action = path["/my/researcher/action/".Length..];
            var next = _character.DeepClone();
            var dataResult = new JsonObject();
            int duration;
            try
            {
                switch (action)
                {
                    case "move":
                        var move = JsonNode.Parse(body);
                        if (move is not JsonObject moveObject || moveObject.Count != 1 || move["map_id"]!.GetValue<int>() != 2)
                            return Error(422, "destination_not_found");
                        next["map_id"] = 2; next["x"] = 1;
                        dataResult["destination"] = _fixture["maps"]![1]!.DeepClone();
                        duration = 7; break;
                    case "fight":
                        if (!EmptyRequest(body)) return Error(422, "invalid_request");
                        if (next["map_id"]!.GetValue<int>() != 2 || next["hp"]!.GetValue<int>() != 20 ||
                            next["weapon_slot"]!.GetValue<string>() != "quick_blade" || Used(next) >= 10 ||
                            next["level"]!.GetValue<int>() >= 2) return Error(422, "fight_not_available");
                        int xp = next["xp"]!.GetValue<int>() + 5;
                        next["xp"] = xp % 10; next["level"] = 1 + xp / 10; next["hp"] = 14;
                        Add(next, "feather", 1);
                        dataResult["fight"] = new JsonObject { ["result"] = "win", ["turns"] = 2, ["opponent"] = "dummy",
                            ["logs"] = new JsonArray(), ["characters"] = new JsonArray(new JsonObject {
                                ["character_name"] = "researcher", ["xp"] = 5, ["gold"] = 0, ["final_hp"] = 14,
                                ["drops"] = new JsonArray(new JsonObject { ["code"] = "feather", ["quantity"] = 1 }) }) };
                        dataResult["characters"] = new JsonArray(next.DeepClone());
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
                            if (code != "old" || Used(next) >= 10) return Error(422, "invalid_equipment");
                            Add(next, code, 1); next["weapon_slot"] = ""; next["attack_fire"] = 0;
                        }
                        else
                        {
                            code = request[0]!["code"]!.GetValue<string>();
                            if (code != "quick_blade" || next["weapon_slot"]!.GetValue<string>() != "" ||
                                !Add(next, code, -1)) return Error(422, "invalid_equipment");
                            next["weapon_slot"] = code; next["attack_fire"] = 10;
                        }
                        dataResult["items"] = new JsonArray(new JsonObject { ["code"] = code, ["slot"] = "weapon", ["quantity"] = 1 });
                        duration = 3; break;
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
            _seconds += duration;
            _trace.Add(new JsonObject { ["sequence"] = _trace.Count + 1, ["action"] = action,
                ["duration_seconds"] = duration, ["virtual_seconds"] = _seconds });
            return (200, new JsonObject { ["data"] = dataResult });
        }
    }

    private JsonNode Initial()
    {
        var state = _fixture["character"]!.DeepClone();
        if (_scenario == "combat-equipment")
        {
            state["weapon_slot"] = "old"; state["attack_fire"] = 5;
            Add(state, "quick_blade", 1); Add(state, "heavy_blade", 1);
        }
        return state;
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
