using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class OperatorControlTests : IDisposable
{
    private readonly List<string> _directories = [];

    [Fact]
    public async Task ChangedServerPolicyInvalidatesInspectReceipt()
    {
        var execution = new Execution();
        var portfolio = new PortfolioSettings { AutonomousGoals = true };
        string directory = Path.Combine(Path.GetTempPath(), "artiact-control-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        var coordinator = new OperatorCoordinator(new() { AllowActions = true, RunDirectory = directory },
            new() { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "test" }, portfolio, new(), execution);
        var inspected = await coordinator.InspectAsync(Request);
        portfolio.Recovery = new(AllowRest: true);
        try { Assert.False((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted); }
        finally { execution.Finish.TrySetResult(); await coordinator.WaitForIdleAsync(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task InspectDoesNotGrantActionOrLivePermission(bool allowActions, bool live)
    {
        var execution = new Execution();
        var coordinator = Create(execution, allowActions: allowActions, live: live);
        var inspected = await coordinator.InspectAsync(Request);
        Assert.True(inspected.Accepted);
        Assert.False((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted);
        Assert.Equal(0, execution.Starts);
    }

    [Fact]
    public async Task ExternalOwnerAndCorruptStateRefuseBeforeExecution()
    {
        var execution = new Execution(); var coordinator = Create(execution);
        var inspected = await coordinator.InspectAsync(Request);
        string directory = _directories[^1];
        using (var owner = new FileRunCheckpointStore(directory, "http://localhost/hero"))
            Assert.False((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted);
        File.WriteAllText(Path.Combine(directory, FileRunCheckpointStore.CharacterKey("http://localhost/hero") + ".json"), "corrupt");
        Assert.False((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted);
        Assert.Equal(0, execution.Starts);
    }

    [Fact]
    public async Task BusyExecutorRefusesNewInspectAndShutdownWaitsForCancellation()
    {
        var execution = new Execution(); var coordinator = Create(execution);
        var inspected = await coordinator.InspectAsync(Request);
        await coordinator.StartRunAsync(inspected.Receipt!);
        await execution.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("ExecutorBusy", (await coordinator.InspectAsync(Request with { RunId = "second" })).Reason);
        await coordinator.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, execution.Starts);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Execution : IOperatorExecution
    {
        public int Starts, Inspects;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token)
        {
            if (settings.Mode == "Inspect") { Inspects++; return new(StrategyStatus.Selected, "InspectOnly", "skill:mining", "Gather:mining", [], 0, 0, 0, 0); }
            Interlocked.Increment(ref Starts); Started.TrySetResult();
            await Finish.Task.WaitAsync(token);
            return new(StrategyStatus.Completed, "TargetsReached", null, null, [], 1, 1, 0, 5);
        }
    }
    private static readonly OperatorRunRequest Request = new("test-run", 2, 60, 20, 3);

    [Fact]
    public async Task DuplicateStartsUseOneFreshInspectAndOneExecution()
    {
        var execution = new Execution();
        var coordinator = Create(execution);
        var inspected = await coordinator.InspectAsync(Request);
        Assert.True(inspected.Accepted);
        Assert.Equal(0, execution.Starts);
        var replies = await Task.WhenAll(coordinator.StartRunAsync(inspected.Receipt!), coordinator.StartRunAsync(inspected.Receipt!));
        Assert.All(replies, x => Assert.True(x.Accepted));
        await execution.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, execution.Starts);
        execution.Finish.TrySetResult();
    }

    [Fact]
    public async Task ExpiredReceiptAndExcessBudgetCannotStart()
    {
        var execution = new Execution(); var clock = new Clock();
        var coordinator = Create(execution, clock);
        Assert.False((await coordinator.InspectAsync(Request with { MaxActions = 101 })).Accepted);
        var inspected = await coordinator.InspectAsync(Request);
        Assert.True(inspected.Accepted);
        clock.Now = clock.Now.AddSeconds(31);
        Assert.False((await coordinator.StartRunAsync(inspected.Receipt!)).Accepted);
        Assert.Equal(0, execution.Starts);
    }

    private OperatorCoordinator Create(Execution execution, TimeProvider? clock = null, bool allowActions = true, bool live = false)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-control-" + Guid.NewGuid().ToString("N"));
        _directories.Add(directory);
        return new(new() { AllowActions = allowActions, RunDirectory = directory },
            new() { BaseUrl = live ? "https://api.artifactsmmo.com" : "http://localhost", Character = "hero", Username = "test", Password = "test" },
            new() { AutonomousGoals = true }, new(), execution, clock);
    }

    public void Dispose()
    {
        foreach (string directory in _directories)
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
