using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed class StrategyObservation
{
    public JsonElement Character { get; }
    public ImmutableDictionary<string, ImmutableArray<JsonElement>> Catalogs { get; }
    public string Policy { get; }
    public string Fingerprint { get; }
    public string WorldFingerprint { get; }
    public string Name => Character.GetProperty("name").GetString()!;

    public StrategyObservation(JsonElement character, IReadOnlyDictionary<string, ImmutableArray<JsonElement>> catalogs, string policy)
    {
        Character = character.Clone();
        Catalogs = catalogs.ToImmutableDictionary(x => x.Key, x => x.Value.Select(v => v.Clone()).ToImmutableArray(), StringComparer.Ordinal);
        Policy = policy;
        WorldFingerprint = Hash(JsonSerializer.SerializeToElement(new { catalogs = Catalogs, policy }));
        Fingerprint = Hash(JsonSerializer.SerializeToElement(new { character = Character, world = WorldFingerprint }));
    }

    public StrategyObservation WithCharacter(JsonElement character) => new(character, Catalogs, Policy);
    public bool SameWorld(StrategyObservation other) => Name == other.Name && WorldFingerprint == other.WorldFingerprint;
    public static string Hash(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, value);
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            var properties = value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
            if (properties.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                throw new InvalidOperationException("Duplicate observation property.");
            foreach (var property in properties) { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray()) Write(writer, item);
            writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
