using Artiact;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;

namespace Artiact.RealApiTests;

public class OperatorResultViewTests
{
    [Fact]
    [Trait("Category", "OperatorResultView")]
    public async Task ShowRetainedResultWithoutApiOrExecutor()
    {
        if (Environment.GetEnvironmentVariable("ARTIACT_OPERATOR_RESULT_VIEW") != "1")
            throw new InvalidOperationException("Explicit local result view required.");
        string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".artiact-runs", ScopedOperatorHost.RunId);
        if (!Directory.Exists(directory) || !Directory.EnumerateFiles(directory, "*.json").Any())
            throw new InvalidOperationException("Retained result unavailable.");
        await using var app = ScopedOperatorHost.Build(new ExecutionSettings { RunDirectory = directory },
            new ApiSettings { BaseUrl = "https://api.artifactsmmo.com/", Character = "gllab", Username = "", Password = "" },
            new PortfolioSettings(), new OperationState(), new DisabledExecution(), "http://127.0.0.1:5189", controlsEnabled: false);
        await app.StartAsync();
        try { await Task.Delay(TimeSpan.FromMinutes(2)); }
        finally { await app.StopAsync(); }
    }

    private sealed class DisabledExecution : IOperatorExecution
    {
        public Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token) =>
            throw new InvalidOperationException("Result viewer has no executor.");
    }
}
