using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SavedObservation(JsonElement Character,
    ImmutableDictionary<string, ImmutableArray<JsonElement>> Catalogs, string Policy, Artiact.Contracts.Models.Api.BankSnapshot? Bank = null)
{
    public StrategyObservation Restore() => new(Character, Catalogs, Policy, Bank);
    public static SavedObservation From(StrategyObservation state) => new(state.Character, state.Catalogs, state.Policy, state.Bank);
}
public sealed record RunCheckpoint(int Version, string Identity, DateTimeOffset Started,
    int Decisions, int Attempts, int NoProgress, long Seconds, string[] Consumed,
    string? PendingCommand, SavedObservation? Baseline, StrategyDecision? Terminal,
    SavedObservation? Verified, ImmutableArray<JournalCommand> Journal,
    ImmutableDictionary<string, ActionMeasurement>? Measurements = null, string? Incumbent = null);
public sealed record JournalCommand(string Command, string SourceFingerprint, string Status, string? ResultFingerprint = null);
public interface IRunCheckpointStore
{
    RunCheckpoint? Load();
    void Save(RunCheckpoint checkpoint);
}

public sealed class FileRunCheckpointStore : IRunCheckpointStore, IDisposable
{
    private readonly FileStream _lease;
    private readonly string _path;
    public FileRunCheckpointStore(string directory, string characterIdentity)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(characterIdentity.ToUpperInvariant())));
        _lease = new FileStream(Path.Combine(directory, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _path = Path.Combine(directory, key + ".json");
        // This release manages one character per store directory. Do not silently bypass
        // an earlier case-sensitive checkpoint when adopting a canonical ownership key.
        if (Directory.EnumerateFiles(directory, "*.json").Any(path => !string.Equals(path, _path, StringComparison.OrdinalIgnoreCase)))
        { _lease.Dispose(); throw new IOException("Existing checkpoint requires operator migration."); }
    }
    public RunCheckpoint? Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<RunCheckpoint>(File.ReadAllBytes(_path)) ?? throw new IOException("Invalid checkpoint.") : null;
    public void Save(RunCheckpoint checkpoint)
    {
        string temporary = _path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, checkpoint); file.Flush(true); }
        File.Move(temporary, _path, true);
    }
    public void Dispose() => _lease.Dispose();
}
