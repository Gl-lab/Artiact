using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class OperatorSnapshotTests
{
    [Fact]
    public void TerminalDiagnosticsSurviveStorageAndExcludePlanningPayloads()
    {
        WithDirectory(directory =>
        {
            var clock = new Clock();
            using var store = new FileRunCheckpointStore(directory, "http://localhost/hero");
            var candidate = new StrategyCandidate("skill:mining:ore", "skill", 1, 1, 0, 0,
                "EstimatedInventoryInsufficient", false, null,
                Discovery: new("discovery-v1", "mining", 10, null, 0, 1, 0, [], "secret-test-value", "secret-test-value"),
                Feasibility: new(57, 80, 23, false, 80, 2, 400, 120));
            store.Save(Checkpoint(clock.Now) with { Terminal = new(StrategyStatus.Blocked, "NoFeasibleCandidate", null, null, [candidate], 1, 1, 0, 5) });
            var view = Reader(directory, clock).Read();
            var detail = view.GetProperty("Run").GetProperty("Candidates")[0];
            Assert.Equal(23, detail.GetProperty("Feasibility").GetProperty("DeficitUnits").GetDecimal());
            Assert.Equal("mining", detail.GetProperty("Skill").GetString());
            Assert.DoesNotContain("secret-test-value", view.GetRawText());
            Assert.DoesNotContain("secret-test-value", JsonSerializer.Serialize(OperatorDecisionProjection.Decision(store.Load()!.Terminal)));
        });
    }
    [Theory]
    [InlineData(StrategyStatus.Completed)]
    [InlineData(StrategyStatus.Stopped)]
    [InlineData(StrategyStatus.Blocked)]
    [InlineData(StrategyStatus.UnknownOutcome)]
    public void DurableTerminalAndFreshnessAreDistinctFromHostActivity(StrategyStatus status)
    {
        WithDirectory(directory =>
        {
            var clock = new Clock();
            using var store = new FileRunCheckpointStore(directory, "http://localhost/hero");
            var saved = Checkpoint(clock.Now);
            store.Save(saved with { Terminal = new(status, "test-reason", null, null, [], 1, 1, 0, 5),
                Latest = saved.Latest! with { ObservedAt = clock.Now.AddSeconds(-31) } });
            var view = Reader(directory, clock).Read();
            Assert.Equal(status.ToString(), view.GetProperty("Executor").GetString());
            Assert.Equal("Stale", view.GetProperty("Freshness").GetString());
            Assert.Equal("Alive", view.GetProperty("Host").GetString());
        });
    }

    [Fact]
    public void WorkerLifecycleIsSeparateFromDurableNonterminalState()
    {
        WithDirectory(directory =>
        {
            var clock = new Clock(); var state = new OperationState();
            using var store = new FileRunCheckpointStore(directory, "http://localhost/hero");
            var saved = Checkpoint(clock.Now);
            store.Save(saved with { Latest = saved.Latest! with { ObservedAt = clock.Now } });
            var reader = new OperatorSnapshotReader(new() { RunDirectory = directory },
                new() { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "test" }, state, clock);
            state.WorkerStarted();
            Assert.Equal("Running", reader.Read().GetProperty("Executor").GetString());
            Assert.Equal("Fresh", reader.Read().GetProperty("Freshness").GetString());
            state.WorkerStopped(); clock.Now = clock.Now.AddSeconds(3);
            Assert.Equal("AwaitingRecovery", reader.Read().GetProperty("Executor").GetString());
            state.Progress("test-run", new(StrategyStatus.Blocked, "CheckpointUnavailableOrInvalid", null, null, [], 1, 1, 0, 5,
                new("Write", "IOException", 32, true)));
            clock.Now = clock.Now.AddSeconds(3);
            Assert.Equal("Blocked", reader.Read().GetProperty("Executor").GetString());
            Assert.Equal("Write", reader.Read().GetProperty("Failure").GetProperty("Operation").GetString());
        });
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ArchiveAvailabilityUsesVerifiedJournalEvenForNoAvailableGoal(bool pending, bool expected)
    {
        WithDirectory(directory =>
        {
            var clock = new Clock();
            using var store = new FileRunCheckpointStore(directory, "http://localhost/hero");
            var saved = Checkpoint(clock.Now) with
            {
                Terminal = new(StrategyStatus.Blocked, "NoFeasibleCandidate", null, null, [], 1, 1, 0, 5),
                Finished = clock.Now,
                PendingCommand = pending ? "Gather:mining" : null
            };
            store.Save(saved);
            var run = Reader(directory, clock).Read().GetProperty("Run");
            Assert.Equal(expected, run.GetProperty("CanArchive").GetBoolean());
            Assert.Equal(pending ? "UnknownOutcome" : "Blocked", run.GetProperty("Status").GetString());
            Assert.Equal("NoFeasibleCandidate", run.GetProperty("Reason").GetString());
        });
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [Fact]
    public void MissingRunReadDoesNotCreateStorage()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-panel-" + Guid.NewGuid().ToString("N"));
        var reader = Reader(directory);
        Assert.Equal("NoRun", reader.Read().GetProperty("Storage").GetString());
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void ReadDuringOwnershipProjectsBudgetAndExcludesArbitraryPayload()
    {
        WithDirectory(directory =>
        {
            var clock = new Clock();
            using var store = new FileRunCheckpointStore(directory, "http://localhost/hero");
            store.Save(Checkpoint(clock.Now));
            var view = Reader(directory, clock).Read();
            Assert.Equal("Available", view.GetProperty("Storage").GetString());
            Assert.Equal("AwaitingRecovery", view.GetProperty("Executor").GetString());
            var run = view.GetProperty("Run");
            Assert.Equal(1, run.GetProperty("RemainingActions").GetInt32());
            Assert.Equal(40, run.GetProperty("RemainingSeconds").GetDouble());
            Assert.Equal("Gather:mining", run.GetProperty("LastConfirmedAction").GetString());
            Assert.DoesNotContain("secret-test-value", view.GetRawText());
            Assert.Equal("Unavailable", view.GetProperty("Freshness").GetString());
            store.Save(Checkpoint(clock.Now) with { Seconds = 9 });
            Assert.Equal(9, store.Load()!.Seconds);
        });
    }

    [Fact]
    public void PollCacheExpiresAndCorruptionReplacesSuccessfulView()
    {
        WithDirectory(directory =>
        {
            var clock = new Clock();
            using var store = new FileRunCheckpointStore(directory, "http://localhost/hero");
            store.Save(Checkpoint(clock.Now));
            var reader = Reader(directory, clock);
            Assert.Equal("Available", reader.Read().GetProperty("Storage").GetString());
            File.WriteAllText(Assert.Single(Directory.GetFiles(directory, "*.json")), "corrupt secret-test-value");
            Assert.Equal("Available", reader.Read().GetProperty("Storage").GetString());
            clock.Now = clock.Now.AddSeconds(3);
            var view = reader.Read();
            Assert.Equal("Unavailable", view.GetProperty("Storage").GetString());
            Assert.Equal(JsonValueKind.Null, view.GetProperty("Run").ValueKind);
            Assert.DoesNotContain("secret-test-value", view.GetRawText());
        });
    }

    private static OperatorSnapshotReader Reader(string directory, TimeProvider? clock = null) =>
        new(new() { RunDirectory = directory }, new() { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "secret-test-value" }, new(), clock);

    private static RunCheckpoint Checkpoint(DateTimeOffset now)
    {
        var observed = new SavedObservation(JsonSerializer.SerializeToElement(new { name = "hero", mining_level = 2, password = "secret-test-value" }),
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "secret-test-value");
        return new(1, JsonSerializer.Serialize(new { RunId = "test-run", Password = "secret-test-value", Limits = new StrategyLimits(Actions: 2, DurationSeconds: 60) }),
            now.AddSeconds(-20), 1, 1, 0, 5, ["before:Gather:mining"], null, null, null, observed,
            [new("Gather:mining", "before", "Verified", "after")], Latest: observed);
    }

    private static void WithDirectory(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-panel-" + Guid.NewGuid().ToString("N"));
        try { action(directory); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
