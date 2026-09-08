using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.Services.Strategy;

public sealed record SavedObservation(JsonElement Character,
    ImmutableDictionary<string, ImmutableArray<JsonElement>> Catalogs, string Policy, Artiact.Contracts.Models.Api.BankSnapshot? Bank = null,
    StrategyRunContext? Context = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? ObservedAt = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NeedOrderBook? Orders = null)
{
    public StrategyObservation Restore() => new(Character, Catalogs, Policy, Bank, Context, Orders);
    public static SavedObservation From(StrategyObservation state, DateTimeOffset? observedAt = null) => new(state.Character, state.Catalogs, state.Policy, state.Bank, state.Context, observedAt, state.Orders);
}
public sealed record RunCheckpoint(int Version, string Identity, DateTimeOffset Started,
    int Decisions, int Attempts, int NoProgress, long Seconds, string[] Consumed,
    string? PendingCommand, SavedObservation? Baseline, StrategyDecision? Terminal,
    SavedObservation? Verified, ImmutableArray<JournalCommand> Journal,
    ImmutableDictionary<string, ActionMeasurement>? Measurements = null, string? Incumbent = null,
    SavedObservation? Initial = null, SavedObservation? Latest = null, DateTimeOffset? Finished = null, string? PendingCandidate = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] AutonomousRunState? Autonomous = null);
public sealed record JournalCommand(string Command, string SourceFingerprint, string Status, string? ResultFingerprint = null,
    ImmutableDictionary<string, int>? Charges = null, string? RefillCode = null, bool? Refilling = null,
    string? Candidate = null, string? MeasurementContext = null, ActionFacts? Facts = null);
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
        string key = CharacterKey(characterIdentity);
        _lease = new FileStream(Path.Combine(directory, key + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        _path = Path.Combine(directory, key + ".json");
        _history = Path.Combine(directory, "history", key);
        // This release manages one character per store directory. Do not silently bypass
        // an earlier case-sensitive checkpoint when adopting a canonical ownership key.
        try
        {
            if (Directory.EnumerateFiles(directory, "*.json").Any(path => !string.Equals(path, _path, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Existing checkpoint requires operator migration.");
        }
        catch { _lease.Dispose(); throw; }
    }
    public RunCheckpoint? Load()
    {
        try { return JsonSerializer.Deserialize<RunCheckpoint>(File.ReadAllBytes(_path)) ?? throw new IOException("Invalid checkpoint."); }
        catch (FileNotFoundException) { return null; }
    }
    public void Save(RunCheckpoint checkpoint)
    {
        if (File.Exists(Path.Combine(_history, IdentityDigest(checkpoint.Identity) + ".json")) ||
            RunId(checkpoint.Identity) is { } runId && Directory.Exists(_history) && Directory.EnumerateFiles(_history, "*.json")
                .Any(path => RunId((JsonSerializer.Deserialize<RunCheckpoint>(File.ReadAllBytes(path)) ?? throw new IOException("Invalid archive.")).Identity) == runId))
            throw new IOException("Archived run identity cannot be reused.");
        string temporary = _path + ".tmp";
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, checkpoint); file.Flush(true); }
        File.Move(temporary, _path, true);
    }
    public static string IdentityDigest(string identity) => Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
    public static string CharacterKey(string characterIdentity) => IdentityDigest(characterIdentity.ToUpperInvariant());
    private static string? RunId(string identity)
    {
        try
        {
            using var json = JsonDocument.Parse(identity);
            return json.RootElement.TryGetProperty("RunId", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
    public string ArchiveCompleted(string expectedIdentityDigest)
    {
        var saved = Load() ?? throw new IOException("No active checkpoint.");
        if (!string.Equals(IdentityDigest(saved.Identity), expectedIdentityDigest, StringComparison.Ordinal) || !CanArchive(saved))
            throw new IOException("Run does not have a verified, archivable outcome.");
        Directory.CreateDirectory(_history);
        string destination = Path.Combine(_history, IdentityDigest(saved.Identity) + ".json");
        File.Move(_path, destination, false);
        return destination;
    }
    public static bool CanArchive(RunCheckpoint saved)
    {
        var terminal = saved.Terminal;
        bool manual = saved.Version == 1 && saved.Autonomous is null && terminal is { Status: StrategyStatus.Completed, Reason: "TargetsReached" } &&
            !terminal.Candidates.IsDefaultOrEmpty && terminal.Candidates.All(x => x.Complete);
        bool autonomous = saved.Version == 2 && saved.Autonomous is { Valid: true } &&
            terminal is { Status: StrategyStatus.Stopped, Reason: "AutonomousBudgetExhausted" or "NoUsefulSupportedGoals" } && saved.Initial is not null &&
            saved.PendingCandidate is null && saved.Latest is not null && CharacterObservation.Read(saved.Latest.Character) is not null &&
            saved.Autonomous.History.Where(x => x.Outcome == "Completed").All(x => x.Goal.Skill == "recovery" ? x.ObservedHp >= x.Goal.Target :
                saved.Latest.Character.TryGetProperty(x.Goal.Skill == "combat" ? "level" : x.Goal.Skill + "_level", out var level) && level.TryGetInt32(out int value) && value >= x.Goal.Target) &&
            (terminal.Reason != "NoUsefulSupportedGoals" || saved.Autonomous.Active is null && ObservedCaps(saved.Latest) &&
                terminal.Candidates.Length is 4 or 5 && terminal.Candidates.All(x => x.Rejection == "SupportedSkillCapReached") &&
                (terminal.Candidates.Length == 4 || saved.Latest.Character.GetProperty("level").GetInt32() == 50));
        bool noGoal = terminal is { Status: StrategyStatus.Blocked, Reason: "NoFeasibleCandidate", Failure: null } &&
            (saved.Version == 1 && saved.Autonomous is null || saved.Version == 2 && saved.Autonomous is { Valid: true }) &&
            saved.PendingCandidate is null;
        bool noNeeds = saved.Version == 2 && saved.Autonomous is { Valid: true, Active: null } &&
            terminal is { Status: StrategyStatus.Stopped, Reason: "NoActiveSupportedNeeds", Failure: null } &&
            saved.PendingCandidate is null && saved.Initial is not null && saved.Latest?.Orders is { Valid: true } && ObservedNoNeeds(saved);
        if (!(manual || autonomous || noGoal || noNeeds) || saved.PendingCommand is not null || terminal is null ||
            saved.Decisions <= 0 || saved.Attempts < 0 || saved.Attempts > saved.Decisions || saved.NoProgress < 0 || saved.Seconds < 0 ||
            saved.Journal.IsDefault || saved.Journal.Length != saved.Attempts || saved.Consumed is null ||
            saved.Consumed.Length != saved.Attempts || saved.Consumed.Distinct(StringComparer.Ordinal).Count() != saved.Attempts ||
            terminal.Attempts != saved.Attempts || terminal.Decisions != saved.Decisions || terminal.CooldownSeconds != saved.Seconds ||
            terminal.NoProgress != saved.NoProgress ||
            saved.Latest is null || saved.Finished is null || saved.Finished < saved.Started || saved.Finished > DateTimeOffset.UtcNow ||
            saved.Journal.Any(x => x.Status is not ("Verified" or "Reconciled") || string.IsNullOrEmpty(x.ResultFingerprint)) ||
            saved.Journal.Any(x => !saved.Consumed.Contains(x.SourceFingerprint + ":" + x.Command, StringComparer.Ordinal)))
            return false;
        return true;
    }

    private static bool ObservedNoNeeds(RunCheckpoint saved)
    {
        try
        {
            using var identity = JsonDocument.Parse(saved.Identity);
            var policy = JsonSerializer.Deserialize<PortfolioPolicy>(identity.RootElement.GetProperty("Policy").GetString()!);
            if (policy?.Needs is not { } needs || saved.Latest is null) return false;
            var state = saved.Latest.Restore();
            if (state.Orders!.Orders.Any(x => x.Status == "Active" && !NeedsGoalDiscovery.Satisfied(state, x, needs))) return false;
            return policy.Recovery is null || (long)state.Character.GetProperty("hp").GetInt32() * 100 >=
                (long)state.Character.GetProperty("max_hp").GetInt32() * policy.Recovery.HpBelowPercent;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) { return false; }
    }

    private static bool ObservedCaps(SavedObservation saved)
    {
        try
        {
            return saved.Catalogs.ContainsKey("items") && saved.Catalogs.ContainsKey("resources") && !saved.Catalogs["maps"].IsDefaultOrEmpty &&
                new[] { "alchemy", "fishing", "mining", "woodcutting" }.All(skill =>
                    saved.Character.GetProperty(skill + "_level").GetInt32() == AutonomousGoalDiscovery.SkillCap &&
                    saved.Character.GetProperty(skill + "_xp").GetInt32() >= 0 &&
                    saved.Character.GetProperty(skill + "_max_xp").GetInt32() >= saved.Character.GetProperty(skill + "_xp").GetInt32());
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException) { return false; }
    }
    public void Dispose() => _lease.Dispose();
}
