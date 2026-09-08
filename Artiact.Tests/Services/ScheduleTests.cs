using Artiact;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Xunit;
using System.Text.Json.Nodes;

namespace Artiact.Tests.Services;

public class ScheduleTests
{
    [Fact]
    public async Task ReservesAcrossRestartAndDoesNotCatchUpOrDuplicateNotifications()
    {
        using var f = new Fixture();
        await f.Runner().TickAsync();
        var runner = f.Runner();
        Assert.Equal(1, f.Port.Executions);
        f.Clock.Now += TimeSpan.FromHours(1);
        await runner.TickAsync();
        Assert.Equal(2, f.Port.Executions);
        var last = runner.Snapshot();
        Assert.Equal(8, last.ReservedActions);
        await runner.TickAsync();
        Assert.Equal(2, f.Port.Executions);
        Assert.Equal(last.Notifications, runner.Snapshot().Notifications);
        f.Clock.Now += TimeSpan.FromSeconds(60);
        await f.Runner().TickAsync();
        Assert.Equal(2, f.Port.Executions);
    }

    [Theory]
    [InlineData(StrategyStatus.UnknownOutcome)]
    [InlineData(StrategyStatus.Cancelled)]
    [InlineData(StrategyStatus.Blocked)]
    public async Task UnsafeResultNeverArchivesOrStartsNextRun(StrategyStatus status)
    {
        using var f = new Fixture(); f.Port.Result = status;
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal("InterventionRequired", runner.Snapshot().Status);
        Assert.Equal(0, f.Port.Archives);
        f.Clock.Now += TimeSpan.FromHours(1); await f.Runner().TickAsync();
        Assert.Equal(1, f.Port.Executions);
    }

    [Fact]
    public async Task DisabledAndStoppedSeriesNeverExecute()
    {
        using var f = new Fixture(); f.Settings.Enabled = false;
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal(0, f.Port.Executions);
        f.Settings.Enabled = true; runner.RequestStop(); await runner.TickAsync();
        await f.Runner().TickAsync();
        Assert.Equal(0, f.Port.Executions);
    }

    [Fact]
    public async Task BusyCharacterStopsBeforeExecution()
    {
        using var f = new Fixture(); f.Port.Busy = true;
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal("InterventionRequired", runner.Snapshot().Status);
        Assert.Equal(0, f.Port.Executions);
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("missing")]
    [InlineData("running")]
    [InlineData("due")]
    [InlineData("identity")]
    public async Task InvalidOrInterruptedStateNeverRestarts(string fault)
    {
        using var f = new Fixture();
        await f.Runner().TickAsync();
        string path = Path.Combine(f.DirectoryPath, "schedule", "series.json");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        if (fault == "missing") File.Delete(path);
        else if (fault == "corrupt") File.WriteAllText(path, "{");
        else
        {
            if (fault == "running") json["Snapshot"]!["Status"] = "Running";
            if (fault == "due") json["Snapshot"]!["NextDue"] = f.Clock.Now.AddDays(-1);
            if (fault == "identity") f.Settings.SeriesId = "replacement";
            File.WriteAllText(path, json.ToJsonString());
        }
        f.Clock.Now += TimeSpan.FromMinutes(2);
        var restarted = f.Runner(); await restarted.TickAsync();
        Assert.Equal("InterventionRequired", restarted.Snapshot().Status);
        Assert.Equal(1, f.Port.Executions);
    }

    [Fact]
    public async Task AggregateBudgetStopsBeforeRunCountLimit()
    {
        using var f = new Fixture(); f.Settings.MaxTotalActions = 4;
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal("Completed", runner.Snapshot().Status);
        f.Clock.Now += TimeSpan.FromMinutes(2); await f.Runner().TickAsync();
        Assert.Equal(1, f.Port.Executions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InspectionCannotExtendWindowOrChangeProfile(bool changeProfile)
    {
        using var f = new Fixture();
        f.Port.Inspect = () => { if (changeProfile) f.Portfolio.GatherSeconds++; else f.Clock.Now += TimeSpan.FromHours(2); };
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal(0, f.Port.Executions);
        Assert.Equal(0, runner.Snapshot().ReservedRuns);
        Assert.Equal(changeProfile ? "InterventionRequired" : "Completed", runner.Snapshot().Status);
    }

    [Fact]
    public async Task StopDuringExecutionRetainsReservationWithoutArchiveOrRestart()
    {
        using var f = new Fixture(); var runner = f.Runner();
        f.Port.Execute = runner.RequestStop;
        await runner.TickAsync();
        Assert.Equal("Stopped", runner.Snapshot().Status);
        Assert.Equal(4, runner.Snapshot().ReservedActions);
        Assert.Equal(0, f.Port.Archives);
        f.Clock.Now += TimeSpan.FromMinutes(2); await f.Runner().TickAsync();
        Assert.Equal(1, f.Port.Executions);
    }

    [Fact]
    public async Task ConcurrentTicksAndBusySeriesLeaseDoNotDispatchTwice()
    {
        using var f = new Fixture(); var runner = f.Runner();
        await Task.WhenAll(runner.TickAsync(), runner.TickAsync());
        Assert.Equal(1, f.Port.Executions);
        using var lease = new FileStream(Path.Combine(f.DirectoryPath, "schedule", "series.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        f.Clock.Now += TimeSpan.FromMinutes(2); var other = f.Runner(); await other.TickAsync();
        Assert.Equal("InterventionRequired", other.Snapshot().Status);
        Assert.Equal(1, f.Port.Executions);
    }

    [Fact]
    public async Task WorkerShutdownCancelsOwnedRunAndPersistsStop()
    {
        using var f = new Fixture(); var runner = f.Runner();
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Port.Wait = async token => { reached.SetResult(); await Task.Delay(Timeout.Infinite, token); };
        using var worker = new ScheduleWorker(runner);
        await worker.StartAsync(CancellationToken.None);
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await worker.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Stopped", runner.Snapshot().Status);
        Assert.Equal(4, runner.Snapshot().ReservedActions);
        await f.Runner().TickAsync(); Assert.Equal(1, f.Port.Executions); Assert.Equal(0, f.Port.Archives);
    }

    [Fact]
    public async Task FailedReservationWriteNeverDispatchesOrResetsOnRestart()
    {
        using var f = new Fixture();
        f.Port.Inspect = () => Directory.CreateDirectory(Path.Combine(f.DirectoryPath, "schedule", "series.json.tmp"));
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal("InterventionRequired", runner.Snapshot().Status);
        await f.Runner().TickAsync(); Assert.Equal(0, f.Port.Executions);
    }

    [Fact]
    public async Task NotificationsAreBoundedAndKeepStableSequenceOnRestart()
    {
        using var f = new Fixture();
        f.Settings.MaxRuns = 100; f.Settings.MaxTotalActions = 400; f.Settings.MaxTotalDecisions = 600; f.Settings.MaxTotalSeconds = 6000;
        var runner = f.Runner();
        for (int i = 0; i < 100; i++) { await runner.TickAsync(); f.Clock.Now += TimeSpan.FromMinutes(1); }
        var events = runner.Snapshot().Notifications;
        Assert.Equal(100, events.Length); Assert.Equal(2, events[0].Id); Assert.Equal(101, events[^1].Id);
        runner = f.Runner(); await runner.TickAsync(); Assert.Equal(events, runner.Snapshot().Notifications);
        Assert.Equal(100, f.Port.Executions);
    }

    [Fact]
    public void LiveScheduleRequiresSeparateSeriesConsentAndSupportedProfile()
    {
        using var f = new Fixture();
        var execution = new ExecutionSettings { RunDirectory = f.DirectoryPath };
        var api = new ApiSettings { BaseUrl = "https://api.artifactsmmo.com", Character = "fixture", Username = "sentinel", Password = "sentinel" };
        Assert.Throws<ArgumentException>(() => f.Settings.Validate(execution, api, f.Portfolio));
        f.Settings.LiveSeriesApproved = true; f.Settings.Validate(execution, api, f.Portfolio);
        f.Portfolio.Skills = [new("mining", 2, 30)];
        Assert.Throws<ArgumentException>(() => f.Settings.Validate(execution, api, f.Portfolio));
    }

    [Fact]
    public async Task NeedsModeIsRejectedBeforeSeriesExecutes()
    {
        using var f = new Fixture(); f.Portfolio.AutonomousGoals = false;
        f.Portfolio.Needs = new(f.DirectoryPath);
        var runner = f.Runner(); await runner.TickAsync();
        Assert.Equal(0, f.Port.Executions);
        Assert.Equal("InterventionRequired", runner.Snapshot().Status);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Port : IScheduledRun
    {
        public int Executions, Archives; public bool Busy;
        public Action? Inspect, Execute;
        public Func<CancellationToken, Task>? Wait;
        public StrategyStatus Result = StrategyStatus.Stopped;
        private static StrategyDecision Decision(StrategyStatus status, string reason) => new(status, reason, null, null, [], 1, 1, 0, 1);
        public Task<StrategyDecision?> InspectAsync(ExecutionSettings settings, CancellationToken token)
        {
            if (Busy) throw new IOException("busy");
            Inspect?.Invoke();
            return Task.FromResult<StrategyDecision?>(Decision(StrategyStatus.Selected, "InspectOnly"));
        }
        public async Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token)
        { Executions++; Execute?.Invoke(); if (Wait is not null) await Wait(token); return Decision(Result, "AutonomousBudgetExhausted"); }
        public Task ArchiveAsync(ExecutionSettings settings) { Archives++; return Task.CompletedTask; }
    }
    private sealed class Fixture : IDisposable
    {
        public readonly string DirectoryPath = Path.Combine(Path.GetTempPath(), "artiact-schedule-" + Guid.NewGuid().ToString("N"));
        public readonly Clock Clock = new(); public readonly Port Port = new();
        public readonly PortfolioSettings Portfolio = new() { AutonomousGoals = true };
        public ScheduleSettings Settings;
        public Fixture() => Settings = new() { Enabled = true, SeriesId = "test-series", FirstRunUtc = Clock.Now,
            ExpiresUtc = Clock.Now.AddHours(2), IntervalSeconds = 60, MaxRuns = 2, MaxTotalActions = 8, MaxTotalDecisions = 12, MaxTotalSeconds = 120 };
        public ScheduleRunner Runner() => new(Settings, new ExecutionSettings { Mode = "Inspect", AllowActions = true,
                RunDirectory = DirectoryPath, MaxActions = 4, MaxDecisions = 6, MaxNoProgress = 2, MaxSeconds = 60 },
            new ApiSettings { BaseUrl = "http://localhost", Character = "fixture", Username = "sentinel", Password = "sentinel" },
            Portfolio, new OperationState(), Port, Clock);
        public void Dispose() { if (Directory.Exists(DirectoryPath)) Directory.Delete(DirectoryPath, true); }
    }
}
