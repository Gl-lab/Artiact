using System.Text.Json;
using Artiact.Services.Strategy;

namespace Artiact.Services.Operation;

public static class RunLifecycleCommand
{
    public static bool Handles(string[] args) => args.Length > 0 && args[0] is "run-result" or "run-archive";

    public static int Execute(string[] args, TextWriter output, TextWriter error)
    {
        if (!Handles(args) || args.Length != (args[0] == "run-result" ? 3 : 4))
        {
            error.WriteLine("Usage: run-result <directory> <origin/character> | run-archive <directory> <origin/character> <identity-sha256>");
            return 2;
        }
        try
        {
            using var store = new FileRunCheckpointStore(args[1], args[2]);
            if (args[0] == "run-archive") output.WriteLine(store.ArchiveCompleted(args[3]));
            else
            {
                var saved = store.Load() ?? throw new IOException("No active checkpoint.");
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    saved.Identity,
                    IdentityDigest = FileRunCheckpointStore.IdentityDigest(saved.Identity),
                    saved.Started, saved.Finished,
                    ElapsedSeconds = saved.Finished.HasValue ? (double?)(saved.Finished.Value - saved.Started).TotalSeconds : null,
                    saved.Decisions, saved.Attempts, saved.NoProgress,
                    VerifiedCooldownSeconds = saved.Seconds,
                    saved.Terminal, saved.PendingCommand,
                    ChargedResources = saved.Latest?.Context?.Used,
                    InterventionRequired = saved.PendingCommand is not null || saved.Terminal?.Status is StrategyStatus.Blocked or StrategyStatus.UnknownOutcome or StrategyStatus.Cancelled,
                    Initial = Facts(saved.Initial), Latest = Facts(saved.Latest), Verified = Facts(saved.Verified),
                    Changes = Changes(saved.Initial, saved.Latest),
                    Performance = RunPerformance.From(saved),
                    saved.Measurements,
                    saved.Journal
                }, new JsonSerializerOptions { WriteIndented = true }));
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        {
            error.WriteLine("Lifecycle operation refused: checkpoint unavailable, incompatible, unreviewed or not safely completed.");
            return 1;
        }
    }

    private static object? Facts(SavedObservation? value) => value is null ? null : new { value.Character, value.Bank, value.Policy };

    private static object? Changes(SavedObservation? before, SavedObservation? after)
    {
        if (before is null || after is null) return null;
        var progress = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var property in before.Character.EnumerateObject())
            if ((property.Name is "level" or "xp" || property.Name.EndsWith("_level", StringComparison.Ordinal) || property.Name.EndsWith("_xp", StringComparison.Ordinal)) &&
                property.Value.TryGetInt64(out long start) && after.Character.TryGetProperty(property.Name, out var last) && last.TryGetInt64(out long end))
                progress[property.Name] = new { Initial = start, Final = end, Delta = end - start };
        return new
        {
            Progress = progress,
            Inventory = Delta(Inventory(before.Character), Inventory(after.Character)),
            Bank = Delta(before.Bank?.Items, after.Bank?.Items)
        };
    }

    private static Dictionary<string, int>? Inventory(JsonElement character)
    {
        if (!character.TryGetProperty("inventory", out var inventory) || inventory.ValueKind != JsonValueKind.Array) return null;
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in inventory.EnumerateArray())
        {
            if (!item.TryGetProperty("code", out var code) || code.ValueKind != JsonValueKind.String ||
                !item.TryGetProperty("quantity", out var quantity) || !quantity.TryGetInt32(out int count) || count < 0) return null;
            if (count == 0) continue;
            string name = code.GetString()!;
            if (string.IsNullOrWhiteSpace(name) || (long)result.GetValueOrDefault(name) + count > int.MaxValue) return null;
            result[name] = result.GetValueOrDefault(name) + count;
        }
        return result;
    }

    private static SortedDictionary<string, long>? Delta(IReadOnlyDictionary<string, int>? before, IReadOnlyDictionary<string, int>? after)
    {
        if (before is null || after is null) return null;
        var result = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (string key in before.Keys.Union(after.Keys, StringComparer.Ordinal))
            result[key] = (long)after.GetValueOrDefault(key) - before.GetValueOrDefault(key);
        return result;
    }
}
