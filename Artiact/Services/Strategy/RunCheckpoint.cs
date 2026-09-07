using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SavedObservation(JsonElement Character,
    ImmutableDictionary<string, ImmutableArray<JsonElement>> Catalogs, string Policy, Artiact.Contracts.Models.Api.BankSnapshot? Bank = null,
    StrategyRunContext? Context = null)
{
    public StrategyObservation Restore() => new(Character, Catalogs, Policy, Bank, Context);
    public static SavedObservation From(StrategyObservation state) => new(state.Character, state.Catalogs, state.Policy, state.Bank, state.Context);
}
public sealed record RunCheckpoint(int Version, string Identity, DateTimeOffset Started,
    int Decisions, int Attempts, int NoProgress, long Seconds, string[] Consumed,
    string? PendingCommand, SavedObservation? Baseline, StrategyDecision? Terminal,
    SavedObservation? Verified, ImmutableArray<JournalCommand> Journal,
    ImmutableDictionary<string, ActionMeasurement>? Measurements = null, string? Incumbent = null,
    SavedObservation? Initial = null, SavedObservation? Latest = null, DateTimeOffset? Finished = null, string? PendingCandidate = null);
public sealed record JournalCommand(string Command, string SourceFingerprint, string Status, string? ResultFingerprint = null,
    ImmutableDictionary<string, int>? Charges = null, string? RefillCode = null, bool? Refilling = null);
public interface IRunCheckpointStore
{
    RunCheckpoint? Load();
    void Save(RunCheckpoint checkpoint);
}

public sealed class FileRunCheckpointStore : IRunCheckpointStore, IDisposable
{
    private readonly FileStream _lease;
    private readonly string _path;
    private readonly string _history;
    public FileRunCheckpointStore(string directory, string characterIdentity)
    {
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        string key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(characterIdentity.ToUpperInvariant())));
        _lease = new FileStream(Path.Combine(directory, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _path = Path.Combine(directory, key + ".json");
        _history = Path.Combine(directory, "history", key);
        // This release manages one character per store directory. Do not silently bypass
        // an earlier case-sensitive checkpoint when adopting a canonical ownership key.
        if (Directory.EnumerateFiles(directory, "*.json").Any(path => !string.Equals(path, _path, StringComparison.OrdinalIgnoreCase)))
        { _lease.Dispose(); throw new IOException("Existing checkpoint requires operator migration."); }
    }
    public RunCheckpoint? Load() => File.Exists(_path)
        ? JsonSerializer.Deserialize<RunCheckpoint>(File.ReadAllBytes(_path)) ?? throw new IOException("Invalid checkpoint.") : null;
    public void Save(RunCheckpoint checkpoint)
    {
        if (File.Exists(Path.Combine(_history, IdentityDigest(checkpoint.Identity) + ".json")))
            throw new IOException("Archived run identity cannot be reused.");
        string temporary = _path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, checkpoint); file.Flush(true); }
        File.Move(temporary, _path, true);
    }
    public static string IdentityDigest(string identity) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
    public string ArchiveCompleted(string expectedIdentityDigest)
    {
        var saved = Load() ?? throw new IOException("No active checkpoint.");
        if (!string.Equals(IdentityDigest(saved.Identity), expectedIdentityDigest, StringComparison.Ordinal) ||
            saved.Version != 1 || saved.PendingCommand is not null || saved.Terminal is not { Status: StrategyStatus.Completed, Reason: "TargetsReached" } terminal ||
            saved.Decisions <= 0 || saved.Attempts < 0 || saved.Attempts > saved.Decisions || saved.NoProgress < 0 || saved.Seconds < 0 ||
            saved.Journal.IsDefault || saved.Journal.Length != saved.Attempts || saved.Consumed is null ||
            saved.Consumed.Length != saved.Attempts || saved.Consumed.Distinct(StringComparer.Ordinal).Count() != saved.Attempts ||
            terminal.Attempts != saved.Attempts || terminal.Decisions != saved.Decisions || terminal.CooldownSeconds != saved.Seconds ||
            terminal.NoProgress != saved.NoProgress || terminal.Candidates.IsDefaultOrEmpty || terminal.Candidates.Any(x => !x.Complete) ||
            saved.Latest is null || saved.Finished is null || saved.Finished < saved.Started || saved.Finished > DateTimeOffset.UtcNow ||
            saved.Journal.Any(x => x.Status is not ("Verified" or "Reconciled") || string.IsNullOrEmpty(x.ResultFingerprint)) ||
            saved.Journal.Any(x => !saved.Consumed.Contains(x.SourceFingerprint + ":" + x.Command, StringComparer.Ordinal)))
            throw new IOException("Run is not a reviewed, verified completion.");
        Directory.CreateDirectory(_history);
        string destination = Path.Combine(_history, IdentityDigest(saved.Identity) + ".json");
        File.Move(_path, destination, false);
        return destination;
    }
    public void Dispose() => _lease.Dispose();
}
