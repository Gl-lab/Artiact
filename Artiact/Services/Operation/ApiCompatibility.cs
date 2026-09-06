using System.Text.Json;
using Artiact.Client;

namespace Artiact.Services.Operation;

public sealed class ApiCompatibility(IGameHttpClient http, ExecutionSettings settings, OperationState status, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    public DateTimeOffset Now => _time.GetUtcNow();
    public async Task CheckAsync(CancellationToken token)
    {
        try
        {
            using var response = await http.ReadAsync("/openapi.json", token);
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (!Compatible(document.RootElement, settings.ExpectedApiVersion)) throw new InvalidOperationException("ApiContractDrift");
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
    public static bool Compatible(JsonElement root, string version)
    {
        try
        {
            if (root.GetProperty("info").GetProperty("version").GetString() != version) return false;
            var paths = root.GetProperty("paths");
            foreach (string path in new[] { "/characters/{name}", "/maps", "/resources", "/items", "/monsters" })
                if (paths.GetProperty(path).GetProperty("get").ValueKind != JsonValueKind.Object) return false;
            foreach (string action in new[] { "move", "gathering", "fight", "rest", "equip", "unequip", "crafting" })
                if (paths.GetProperty("/my/{name}/action/" + action).GetProperty("post").ValueKind != JsonValueKind.Object) return false;
            var schemas = root.GetProperty("components").GetProperty("schemas");
            foreach (string field in new[] { "level", "xp", "hp", "max_hp", "map_id" })
                if (!Type(schemas, "CharacterSchema", field, "integer")) return false;
            foreach (string field in new[] { "name", "layer", "weapon_slot" })
                if (!Type(schemas, "CharacterSchema", field, "string")) return false;
            if (!Type(schemas, "CharacterSchema", "inventory", "array") || !Type(schemas, "MapSchema", "map_id", "integer") ||
                !Type(schemas, "MapSchema", "layer", "string") || !Reference(schemas, "MapSchema", "access") || !Reference(schemas, "MapSchema", "interactions") ||
                !Type(schemas, "CharacterFightDataSchema", "characters", "array") || !Reference(schemas, "CharacterFightDataSchema", "fight") ||
                !Reference(schemas, "CharacterFightDataSchema", "cooldown")) return false;
            foreach (string field in new[] { "character", "cooldown", "details" }) if (!Reference(schemas, "SkillDataSchema", field)) return false;
            foreach (string action in new[] { "equip", "unequip" })
                if (paths.GetProperty("/my/{name}/action/" + action).GetProperty("post").GetProperty("requestBody").GetProperty("content")
                    .GetProperty("application/json").GetProperty("schema").GetProperty("type").GetString() != "array") return false;
            var move = schemas.GetProperty("DestinationSchema").GetProperty("properties").GetProperty("map_id");
            return move.TryGetProperty("type", out var type) && type.GetString() == "integer" ||
                move.TryGetProperty("anyOf", out var any) && any.EnumerateArray().Any(x => x.TryGetProperty("type", out var t) && t.GetString() == "integer");
        }
        catch (Exception) { return false; }
    }
    private static bool Type(JsonElement schemas, string schema, string field, string expected) =>
        schemas.GetProperty(schema).GetProperty("properties").GetProperty(field).GetProperty("type").GetString() == expected;
    private static bool Reference(JsonElement schemas, string schema, string field)
    {
        var property = schemas.GetProperty(schema).GetProperty("properties").GetProperty(field);
        return property.TryGetProperty("$ref", out var reference) && reference.GetString() is { } path &&
            path.StartsWith("#/components/schemas/", StringComparison.Ordinal) &&
            schemas.GetProperty(path["#/components/schemas/".Length..]).GetProperty("type").GetString() == "object";
    }
}
