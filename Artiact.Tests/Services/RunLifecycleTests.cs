using System.Collections.Immutable;
using System.Text.Json;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class RunLifecycleTests
{
    [Fact]
    public void ArchivedRunIdCannotBeReusedWithDifferentPolicyOrLimits()
    {
        WithDirectory(directory =>
        {
            using var store = new FileRunCheckpointStore(directory, "hero");
            var checkpoint = Completed() with { Identity = JsonSerializer.Serialize(new { RunId = "same", Policy = "old", Limit = 1 }) };
            store.Save(checkpoint); store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(checkpoint.Identity));
            Assert.Throws<IOException>(() => store.Save(checkpoint with { Identity = JsonSerializer.Serialize(new { RunId = "same", Policy = "new", Limit = 100 }) }));
            store.Save(checkpoint with { Identity = JsonSerializer.Serialize(new { RunId = "new", Policy = "new", Limit = 100 }) });
        });
    }
    private static RunCheckpoint Completed()
    {
        var state = new SavedObservation(JsonSerializer.SerializeToElement(new { name = "hero", mining_level = 2, mining_xp = 4 }),
            ImmutableDictionary<string, ImmutableArray<JsonElement>>.Empty, "mining:2");
        var terminal = new StrategyDecision(StrategyStatus.Completed, "TargetsReached", null, null,
            [new("skill:mining", "skill", 1, 1, 0, 0, null, true, null)], 2, 1, 0, 5);
        return new(1, "run1-policy-limits", DateTimeOffset.UtcNow.AddSeconds(-10), 2, 1, 0, 5, ["before:Gather:mining"],
            null, state, terminal, state, [new("Gather:mining", "before", "Verified", "after")],
            Initial: state, Latest: state, Finished: DateTimeOffset.UtcNow);
    }

    [Fact]
    public void ArchivePreservesExactBytesAndRefusesReusingRunIdentity()
    {
        WithDirectory(directory =>
        {
            var checkpoint = Completed();
            using var store = new FileRunCheckpointStore(directory, "hero");
            store.Save(checkpoint);
            var original = File.ReadAllBytes(Assert.Single(Directory.GetFiles(directory, "*.json")));
            string archive = store.ArchiveCompleted(FileRunCheckpointStore.IdentityDigest(checkpoint.Identity));
            Assert.Equal(original, File.ReadAllBytes(archive));
            Assert.Null(store.Load());
            Assert.Throws<IOException>(() => store.Save(checkpoint));
            store.Save(checkpoint with { Identity = "run2-policy-limits" });
            Assert.Equal("run2-policy-limits", store.Load()!.Identity);
            Assert.Equal(original, File.ReadAllBytes(archive));
        });
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("cancelled")]
    [InlineData("blocked")]
    [InlineData("unknown")]
    [InlineData("counter")]
    [InlineData("outcome")]
    [InlineData("identity")]
    [InlineData("unfinished")]
    [InlineData("missing-result")]
    [InlineData("consumed")]
    [InlineData("version")]
    public void UnsafeArchiveLeavesCheckpointUnchanged(string failure)
    {
        WithDirectory(directory =>
        {
            var checkpoint = Completed();
            checkpoint = failure switch
            {
                "pending" => checkpoint with { PendingCommand = "Gather:mining" },
                "cancelled" => checkpoint with { Terminal = checkpoint.Terminal! with { Status = StrategyStatus.Cancelled } },
                "blocked" => checkpoint with { Terminal = checkpoint.Terminal! with { Status = StrategyStatus.Blocked } },
                "unknown" => checkpoint with { Terminal = checkpoint.Terminal! with { Status = StrategyStatus.UnknownOutcome } },
                "counter" => checkpoint with { Attempts = 2 },
                "outcome" => checkpoint with { Journal = [new("Gather:mining", "before", "InvalidPostcondition")] },
                "unfinished" => checkpoint with { Terminal = null },
                "missing-result" => checkpoint with { Latest = null },
                "consumed" => checkpoint with { Consumed = ["different-command"] },
                "version" => checkpoint with { Version = 50 },
                _ => checkpoint
            };
            using var store = new FileRunCheckpointStore(directory, "hero"); store.Save(checkpoint);
            string path = Assert.Single(Directory.GetFiles(directory, "*.json"));
            byte[] before = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => store.ArchiveCompleted(failure == "identity" ? "wrong" : FileRunCheckpointStore.IdentityDigest(checkpoint.Identity)));
            Assert.Equal(before, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void CommandsReportFactsAndRefuseConcurrentOwnerOrCorruptData()
    {
        WithDirectory(directory =>
        {
            using (var store = new FileRunCheckpointStore(directory, "hero"))
            {
                store.Save(Completed());
                Assert.Equal(1, RunLifecycleCommand.Execute(["run-result", directory, "hero"], new StringWriter(), new StringWriter()));
            }
            var output = new StringWriter();
            Assert.Equal(0, RunLifecycleCommand.Execute(["run-result", directory, "hero"], output, new StringWriter()));
            using var report = JsonDocument.Parse(output.ToString());
            Assert.Equal(5, report.RootElement.GetProperty("VerifiedCooldownSeconds").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("Performance").GetProperty("VerifiedFactCount").GetInt32());
            Assert.Equal(0, report.RootElement.GetProperty("Performance").GetProperty("CompletedWaitCount").GetInt32());
            Assert.Equal(2, report.RootElement.GetProperty("Latest").GetProperty("Character").GetProperty("mining_level").GetInt32());
            Assert.False(report.RootElement.GetProperty("InterventionRequired").GetBoolean());
            File.WriteAllText(Assert.Single(Directory.GetFiles(directory, "*.json")), "{broken");
            Assert.Equal(1, RunLifecycleCommand.Execute(["run-archive", directory, "hero", "digest"], new StringWriter(), new StringWriter()));
        });
    }

    [Fact]
    public void HistoricalCheckpointDoesNotInventInitialFacts()
    {
        WithDirectory(directory =>
        {
            using (var store = new FileRunCheckpointStore(directory, "hero")) store.Save(Completed() with { Initial = null, Latest = null, Finished = null });
            var output = new StringWriter();
            Assert.Equal(0, RunLifecycleCommand.Execute(["run-result", directory, "hero"], output, new StringWriter()));
            using var report = JsonDocument.Parse(output.ToString());
            Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("Initial").ValueKind);
            Assert.Equal(JsonValueKind.Null, report.RootElement.GetProperty("ElapsedSeconds").ValueKind);
        });
    }

    private static void WithDirectory(Action<string> action)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-lifecycle-" + Guid.NewGuid().ToString("N"));
        try { action(directory); }
        finally
        {
            if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup escaped temporary directory.");
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
