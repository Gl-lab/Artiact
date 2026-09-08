using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public class OperatorControlTests : IDisposable
{
    private readonly List<string> _directories = [];

    [Fact]
    public async Task OrdersAreRevisionCheckedAndCannotGrantPermissionsOrReuseReceipt()
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-orders-control-" + Guid.NewGuid().ToString("N")); _directories.Add(directory);
        var execution = new Execution();
        var coordinator = new OperatorCoordinator(new() { RunDirectory = directory, AllowActions = true },
            new() { BaseUrl = "http://localhost", Character = "hero", Username = "mock", Password = "mock" },
            new() { Needs = new(directory) }, new(), execution);
        Assert.Empty(coordinator.Orders().Orders); Assert.False(Directory.Exists(directory));
        var receipt = await coordinator.InspectAsync(Request);
        Assert.True((await coordinator.ChangeOrderAsync(new("order", "tool", 1), 0)).Accepted);
        Assert.False((await coordinator.StartRunAsync(receipt.Receipt!)).Accepted);
        Assert.False((await coordinator.ChangeOrderAsync(new("order", "tool", 1), 0)).Accepted);
        Assert.True((await coordinator.ChangeOrderAsync(new("order", "tool", 1, Revision: 2, Status: "Cancelled"), 1)).Accepted);
        Assert.False((await coordinator.ChangeOrderAsync(new("order", "tool", 1, Revision: 3), 2)).Accepted);
        var profile = coordinator.Profile();
        Assert.False(profile.GetProperty("Permissions").GetProperty("Craft").GetBoolean());
        Assert.False(profile.GetProperty("Needs").TryGetProperty("Directory", out _));
        Assert.Equal(0, execution.Starts);
    }

    [Fact]
    public async Task SixDecisionProtocolDoesNotRequireIncreasingLiveBudget()
    {
        var execution = new Execution(); var coordinator = Create(execution);
        var result = await coordinator.InspectAsync(Request with { MaxDecisions = 6, MaxNoProgress = 3 });
        Assert.True(result.Accepted, result.Reason);
        var settings = new ExecutionSettings { Mode = "Bounded", AllowActions = true, RunId = "small", RunDirectory = _directories[^1],
            MaxActions = 2, MaxDecisions = 6, MaxNoProgress = 3, MaxSeconds = 120 };
        var api = new Artiact.ApiSettings { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "test" };
        Assert.Equal(ExecutionMode.Bounded, settings.Validate(api));
        settings.MaxDecisions = 0; Assert.Throws<ArgumentException>(() => settings.Validate(api));
        settings.MaxDecisions = 2; Assert.Throws<ArgumentException>(() => settings.Validate(api));
    }

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

    [Fact]
    public async Task FinishedCheckpointRefusesStartWithoutLaunchingAnExecutor()
    {
        var execution = new Execution(); var coordinator = Create(execution);
        var inspected = await coordinator.InspectAsync(Request);
        using (var store = new FileRunCheckpointStore(_directories[^1], "http://localhost/hero"))
            store.Save(new(1, "saved-run", DateTimeOffset.UtcNow, 1, 0, 0, 0, [], null, null,
                new(StrategyStatus.Blocked, "NoFeasibleCandidate", null, null, [], 1, 0, 0, 0), null, []));
        var result = await coordinator.StartRunAsync(inspected.Receipt!);
        Assert.False(result.Accepted);
        Assert.Equal("RunFinished", result.Reason);
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
