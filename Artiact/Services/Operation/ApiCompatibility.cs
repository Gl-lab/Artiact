using System.Text.Json;
using Artiact.Client;
using Artiact.Services.Strategy;

namespace Artiact.Services.Operation;

public sealed class ApiCompatibility(IGameHttpClient http, ExecutionSettings settings, OperationState status, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    public DateTimeOffset Now => _time.GetUtcNow();
    public async Task CheckAsync(CancellationToken token, PortfolioPolicy? profile = null)
    {
        try
        {
            using var response = await http.ReadAsync("/openapi.json", token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (!Compatible(document.RootElement, settings.ExpectedApiVersion, profile)) throw new InvalidOperationException("ApiContractDrift");
            status.Probe(settings.ExpectedApiVersion);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception) { status.Set("ApiContractUnavailableOrDrift"); throw new InvalidOperationException("ApiContractUnavailableOrDrift"); }
    }
    public void Observed(DateTimeOffset started, string fingerprint)
    {
        if (Now - started > TimeSpan.FromSeconds(settings.FreshnessSeconds))
        { status.Set("StaleObservation"); throw new InvalidOperationException("StaleObservation"); }
        status.Observed(fingerprint);
    }
    public static bool Compatible(JsonElement root, string version, PortfolioPolicy? profile = null)
    {
        try
        {
            if (root.GetProperty("info").GetProperty("version").GetString() != version) return false;
            var paths = root.GetProperty("paths");
            bool gatheringOnly = profile is { CombatEnabled: false, CombatDiscovery: null };
            foreach (string path in gatheringOnly ? new[] { "/characters/{name}", "/maps", "/resources" } : new[] { "/characters/{name}", "/maps", "/resources", "/items", "/monsters" })
                if (paths.GetProperty(path).GetProperty("get").ValueKind != JsonValueKind.Object) return false;
            var actions = profile?.CombatDiscovery is { } discovered ? new[] { "move", "gathering" }
                .Concat(discovered.AllowFight ? ["fight"] : Array.Empty<string>())
                .Concat(discovered.AllowEquip ? ["equip", "unequip"] : Array.Empty<string>())
                .Concat(discovered.AllowCraft ? ["crafting"] : Array.Empty<string>()) :
                gatheringOnly ? new[] { "move", "gathering" } : new[] { "move", "gathering", "fight", "rest", "equip", "unequip", "crafting" };
            foreach (string action in actions)
                if (paths.GetProperty("/my/{name}/action/" + action).GetProperty("post").ValueKind != JsonValueKind.Object) return false;
            var schemas = root.GetProperty("components").GetProperty("schemas");
            if (profile?.AutonomousGoals == true && paths.GetProperty("/items").GetProperty("get").ValueKind != JsonValueKind.Object) return false;
            if (profile?.Recovery is { } recovery)
            {
                if (!Type(schemas, "CharacterSchema", "hp", "integer") || !Type(schemas, "CharacterSchema", "max_hp", "integer")) return false;
                if (recovery.AllowRest && paths.GetProperty("/my/{name}/action/rest").GetProperty("post").ValueKind != JsonValueKind.Object) return false;
                if (recovery.AllowUse && (paths.GetProperty("/my/{name}/action/use").GetProperty("post").GetProperty("requestBody").GetProperty("content")
                    .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString() != "#/components/schemas/SimpleItemSchema" ||
                    !Reference(schemas, "UseItemSchema", "item") || !Reference(schemas, "UseItemSchema", "character") || !Reference(schemas, "UseItemSchema", "cooldown"))) return false;
                if (recovery.AllowCraft)
                {
                    if (paths.GetProperty("/my/{name}/action/crafting").GetProperty("post").ValueKind != JsonValueKind.Object) return false;
                    foreach (string skill in new[] { "cooking", "fishing" })
                        foreach (string field in new[] { "level", "xp", "max_xp" })
                            if (!Type(schemas, "CharacterSchema", skill + "_" + field, "integer")) return false;
                }
                if (recovery.AllowBankWithdrawal && paths.GetProperty("/my/{name}/action/bank/withdraw/item").GetProperty("post")
                    .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array") return false;
            }
            if (profile?.Needs is { } needs)
            {
                if (needs.AllowCraft && paths.GetProperty("/my/{name}/action/crafting").GetProperty("post").ValueKind != JsonValueKind.Object) return false;
                if (needs.AllowBankWithdrawal && paths.GetProperty("/my/{name}/action/bank/withdraw/item").GetProperty("post")
                    .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array") return false;
            }
            if (profile?.Preparation is not null || profile?.Needs?.AllowCraft == true)
                foreach (string skill in new[] { "mining", "weaponcrafting", "cooking", "fishing" })
                    foreach (string field in new[] { "level", "xp", "max_xp" })
                        if (!Type(schemas, "CharacterSchema", skill + "_" + field, "integer")) return false;
            if (profile?.Consumable is not null &&
                (paths.GetProperty("/my/{name}/action/use").GetProperty("post").GetProperty("requestBody").GetProperty("content")
                    .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString() != "#/components/schemas/SimpleItemSchema" ||
                 !Reference(schemas, "UseItemSchema", "item") || !Reference(schemas, "UseItemSchema", "character") || !Reference(schemas, "UseItemSchema", "cooldown") ||
                 !Type(schemas, "CharacterSchema", "hp", "integer") || !Type(schemas, "CharacterSchema", "max_hp", "integer") ||
                 profile.Consumable.AllowRest && paths.GetProperty("/my/{name}/action/rest").GetProperty("post").ValueKind != JsonValueKind.Object)) return false;
            if ((profile?.AutonomousCombat is not null || profile?.CombatDiscovery is not null) && !Type(schemas, "CharacterSchema", "shield_slot", "string")) return false;
            if (profile?.CombatDiscovery is { } combat)
            {
                if (combat.AllowCraft)
                    foreach (string field in new[] { "weaponcrafting_level", "weaponcrafting_xp", "weaponcrafting_max_xp" })
                        if (!Type(schemas, "CharacterSchema", field, "integer")) return false;
                if (combat.AllowBankWithdrawal && paths.GetProperty("/my/{name}/action/bank/withdraw/item").GetProperty("post")
                    .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array") return false;
            }
            if (profile is not null && (!profile.Items.IsDefaultOrEmpty || profile.PrepareEquipment || profile.AutonomousCombat is not null))
            {
                if (paths.GetProperty("/items").GetProperty("get").ValueKind != JsonValueKind.Object ||
                    paths.GetProperty("/my/{name}/action/crafting").GetProperty("post").ValueKind != JsonValueKind.Object) return false;
                if (profile.Bank is not null && paths.GetProperty("/my/{name}/action/bank/withdraw/item").GetProperty("post")
                    .GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array") return false;
            }
            if (profile?.Bank is not null)
            {
                foreach (string path in new[] { "/my/bank", "/my/bank/items" })
                    if (paths.GetProperty(path).GetProperty("get").ValueKind != JsonValueKind.Object) return false;
                if (paths.GetProperty("/my/{name}/action/bank/deposit/item").GetProperty("post").GetProperty("requestBody").GetProperty("content")
                    .GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array" ||
                    !Type(schemas, "BankSchema", "slots", "integer") || !Type(schemas, "BankItemTransactionSchema", "bank", "array") ||
                    !Type(schemas, "BankItemTransactionSchema", "items", "array") || !Reference(schemas, "BankItemTransactionSchema", "character") ||
                    !Reference(schemas, "BankItemTransactionSchema", "cooldown")) return false;
            }
            foreach (string field in gatheringOnly ? new[] { "map_id", "inventory_max_items" }.Concat(profile!.Skills.SelectMany(s => new[] { s.Skill + "_level", s.Skill + "_xp", s.Skill + "_max_xp" })) : new[] { "level", "xp", "hp", "max_hp", "map_id" })
                if (!Type(schemas, "CharacterSchema", field, "integer")) return false;
            foreach (string field in gatheringOnly ? new[] { "name", "layer" } : new[] { "name", "layer", "weapon_slot" })
                if (!Type(schemas, "CharacterSchema", field, "string")) return false;
            if (!Type(schemas, "CharacterSchema", "inventory", "array") || !Type(schemas, "MapSchema", "map_id", "integer") ||
                !Type(schemas, "MapSchema", "layer", "string") || !Reference(schemas, "MapSchema", "access") || !Reference(schemas, "MapSchema", "interactions") ||
                !gatheringOnly && (!Type(schemas, "CharacterFightDataSchema", "characters", "array") || !Reference(schemas, "CharacterFightDataSchema", "fight") ||
                !Reference(schemas, "CharacterFightDataSchema", "cooldown"))) return false;
            foreach (string field in new[] { "character", "cooldown", "details" }) if (!Reference(schemas, "SkillDataSchema", field)) return false;
            foreach (string action in gatheringOnly || profile?.CombatDiscovery is { AllowEquip: false } ? Array.Empty<string>() : new[] { "equip", "unequip" })
                if (paths.GetProperty("/my/{name}/action/" + action).GetProperty("post").GetProperty("requestBody").GetProperty("content")
                    .GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array") return false;
            var move = schemas.GetProperty("DestinationSchema").GetProperty("properties").GetProperty("map_id");
            return move.TryGetProperty("type", out var type) && type.GetString() == "integer" ||
                move.TryGetProperty("anyOf", out var any) && any.EnumerateArray().Any(x => x.TryGetProperty("type", out var t) && t.GetString() == "integer");
        }
        catch (Exception) { return false; }
    }
    private static bool Type(JsonElement schemas, string schema, string field, string expected)
    {
        var property = schemas.GetProperty(schema).GetProperty("properties").GetProperty(field);
        if (property.TryGetProperty("$ref", out var reference))
        {
            const string prefix = "#/components/schemas/";
            if (reference.GetString() is not { } path || !path.StartsWith(prefix, StringComparison.Ordinal)) return false;
            property = schemas.GetProperty(path[prefix.Length..]);
        }
        return property.GetProperty("type").GetString() == expected;
    }
    private static bool Reference(JsonElement schemas, string schema, string field)
    {
        var property = schemas.GetProperty(schema).GetProperty("properties").GetProperty(field);
        return property.TryGetProperty("$ref", out var reference) && reference.GetString() is { } path &&
            path.StartsWith("#/components/schemas/", StringComparison.Ordinal) &&
            schemas.GetProperty(path["#/components/schemas/".Length..]).GetProperty("type").GetString() == "object";
    }
}
