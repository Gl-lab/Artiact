using System.Text.Json;
using Artiact.Services.Strategy;

namespace Artiact.Services.Operation;

public sealed class OperatorSnapshotReader(ExecutionSettings settings, ApiSettings api, OperationState state, TimeProvider? time = null)
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly object _sync = new();
    private DateTimeOffset _readAt;
    private JsonElement? _cached;

    public JsonElement Read()
    {
        lock (_sync)
        {
            var now = _time.GetUtcNow();
            if (_cached.HasValue && now >= _readAt && now - _readAt < TimeSpan.FromSeconds(2)) return _cached.Value;
            _readAt = now;
            try
            {
                if (string.IsNullOrWhiteSpace(settings.RunDirectory)) return Cache("NotConfigured", null, [], now);
                string directory = Path.GetFullPath(settings.RunDirectory);
                string key = FileRunCheckpointStore.CharacterKey(new Uri(api.BaseUrl).GetLeftPart(UriPartial.Authority) + "/" + api.Character);
                RunCheckpoint? saved;
                try { saved = ReadFile(Path.Combine(directory, key + ".json")); }
                catch (DirectoryNotFoundException) when (!Directory.Exists(directory) && !File.Exists(directory)) { saved = null; }
                var historyDirectory = Path.Combine(directory, "history", key);
                var history = Directory.Exists(historyDirectory) ? Directory.EnumerateFiles(historyDirectory, "*.json")
                    .OrderByDescending(File.GetLastWriteTimeUtc).Take(20).Select(ReadFile).Where(x => x is not null).Select(x => Project(x!, now)).ToArray() : [];
                return Cache(saved is null ? "NoRun" : "Available", saved, history, now);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or NotSupportedException)
            { return Cache("Unavailable", null, [], now); }
        }
    }

    private JsonElement Cache(string storage, RunCheckpoint? saved, object[] history, DateTimeOffset now)
    {
        var observed = saved?.Latest?.ObservedAt;
        var worker = state.WorkerSnapshot();
        string executor = worker.Running ? "Running" : saved is null ? "Idle" : saved.PendingCommand is not null ? "UnknownOutcome" :
            worker.Decision?.Status.ToString() ?? saved.Terminal?.Status.ToString() ?? "AwaitingRecovery";
        _cached = JsonSerializer.SerializeToElement(new
        {
            Storage = storage, Executor = executor, Host = "Alive", ReadAt = now,
            Freshness = observed is null ? "Unavailable" : observed > now || now - observed > TimeSpan.FromSeconds(settings.FreshnessSeconds) ? "Stale" : "Fresh",
            ObservedAt = observed, worker.StopRequested,
            Failure = worker.Decision?.Failure,
            RuntimeState = state.Snapshot(settings.FreshnessSeconds).State,
            Run = saved is null ? null : Project(saved, now), History = history
        });
        return _cached.Value;
    }

    private static RunCheckpoint? ReadFile(string path)
    {
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (file.Length > 8 * 1024 * 1024) throw new IOException("Checkpoint exceeds observation bound.");
            var saved = JsonSerializer.Deserialize<RunCheckpoint>(file) ?? throw new IOException("Invalid checkpoint.");
            if (saved.Version is not (1 or 2) || saved.Journal.IsDefault || saved.Attempts < 0 || saved.Decisions < saved.Attempts ||
                saved.Journal.Length != saved.Attempts || saved.Seconds < 0 || saved.Version == 2 && saved.Autonomous is not { Valid: true })
                throw new IOException("Invalid checkpoint.");
            return saved;
        }
        catch (FileNotFoundException) { return null; }
    }

    private static object Project(RunCheckpoint saved, DateTimeOffset now)
    {
        string? runId = null;
        StrategyLimits? limits = null;
        try
        {
            using var identity = JsonDocument.Parse(saved.Identity);
            if (identity.RootElement.TryGetProperty("RunId", out var id) && id.ValueKind == JsonValueKind.String) runId = id.GetString();
            if (identity.RootElement.TryGetProperty("Limits", out var bounds)) limits = bounds.Deserialize<StrategyLimits>();
        }
        catch (JsonException) { }
        var latest = saved.Latest?.Character;
        var character = new SortedDictionary<string, JsonElement>(StringComparer.Ordinal);
        if (latest is { ValueKind: JsonValueKind.Object } raw)
            foreach (string field in new[] { "name", "hp", "max_hp", "level", "xp", "max_xp", "map_id", "x", "y", "inventory_max_items",
                "mining_level", "mining_xp", "woodcutting_level", "woodcutting_xp", "fishing_level", "fishing_xp", "alchemy_level", "alchemy_xp" })
                if (raw.TryGetProperty(field, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number) character[field] = value.Clone();
        var goal = saved.Autonomous?.Active;
        return new
        {
            RunId = runId, IdentityDigest = FileRunCheckpointStore.IdentityDigest(saved.Identity), saved.Started, saved.Finished,
            CanArchive = FileRunCheckpointStore.CanArchive(saved),
            Status = saved.PendingCommand is not null ? "UnknownOutcome" : saved.Terminal?.Status.ToString() ?? "Nonterminal",
            Reason = saved.Terminal?.Reason, Goal = goal is null ? saved.Autonomous is null ? saved.Journal.LastOrDefault()?.Candidate : null : goal.Skill + ":" + goal.Target,
            Candidates = OperatorDecisionProjection.Candidates(saved.Terminal?.Candidates ?? []),
            Origin = saved.Autonomous is null ? "Configured" : "Autonomous",
            Explanation = goal,
            LastConfirmedAction = saved.Journal.LastOrDefault(x => x.Status == "Verified")?.Command,
            saved.PendingCommand, UsedActions = saved.Attempts, MaxActions = limits?.Actions,
            RemainingActions = limits is null ? (int?)null : Math.Max(0, limits.Actions - saved.Attempts),
            saved.Decisions, saved.NoProgress, MaxSeconds = limits?.DurationSeconds, MaxDecisions = limits?.Decisions, MaxNoProgress = limits?.NoProgress,
            RemainingSeconds = limits is null ? (double?)null : Math.Max(0, limits.DurationSeconds - Math.Max(0, ((saved.Finished ?? now) - saved.Started).TotalSeconds)),
            VerifiedCooldownSeconds = saved.Seconds,
            TimingComplete = saved.Journal.All(x => x.Status == "Verified" && x.Facts?.WaitSeconds is not null),
            Character = character, ChargedResources = saved.Latest?.Context?.Used,
            Milestones = saved.Autonomous?.History.Select(x => new { x.Goal.Skill, x.Goal.Target, x.Outcome }).ToArray(),
            InterventionRequired = saved.PendingCommand is not null || saved.Terminal?.Status is StrategyStatus.Blocked or StrategyStatus.UnknownOutcome or StrategyStatus.Cancelled
        };
    }
}
