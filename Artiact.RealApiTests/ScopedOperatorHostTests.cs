using Artiact;
using Artiact.Services.Operation;
using Artiact.Services.Strategy;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Artiact.RealApiTests;

public class ScopedOperatorHostTests
{
    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("gllab:alchemy2:16:900")]
    public void RequiresExactScopeBeforeCredentialLoading(string? value) =>
        Assert.Throws<InvalidOperationException>(() => ScopedOperatorHost.RequireApproval(value));

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task HostsRealPanelWithoutAutomaticExecution()
    {
        var execution = new NeverExecute();
        await using var app = ScopedOperatorHost.Build(new ExecutionSettings
        { RunId = ScopedOperatorHost.RunId, MaxActions = 16, MaxDecisions = 20, MaxNoProgress = 3, MaxSeconds = 900 },
            new ApiSettings { Character = "gllab", BaseUrl = "https://api.artifactsmmo.com", Username = "sentinel", Password = "sentinel" },
            new PortfolioSettings { AutonomousGoals = true }, new OperationState(), execution, "http://127.0.0.1:0");
        await app.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
        using var controls = await http.GetAsync("/operator/controls");
        Assert.True(controls.IsSuccessStatusCode);
        Assert.Contains("gllab", await controls.Content.ReadAsStringAsync());
        using var panel = await http.GetAsync("/operator");
        Assert.True(panel.IsSuccessStatusCode);
        Assert.Equal(0, execution.Calls);
        using var refused = await http.PostAsync("/operator/start", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(0, execution.Calls);
    }

    private sealed class NeverExecute : IOperatorExecution
    {
        public int Calls;
        public Task<StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token)
        { Calls++; throw new InvalidOperationException("No automatic execution."); }
    }
}
