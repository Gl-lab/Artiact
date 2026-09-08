using Artiact.Services;
using Artiact.Services.Operation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Artiact.Tests.Services;

public class OperationRegistrationTests
{
    [Fact]
    public void DefaultRegistrationUsesInspectWorkerAndBoundHttpClient()
    {
        var services = new ServiceCollection().AddLogging().AddStagedOperation(new ConfigurationBuilder().Build());
        Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(StagedWorker));
        Assert.DoesNotContain(services, x => x.ImplementationType == typeof(ArtiactBackgroundService));
        using var provider = services.BuildServiceProvider();
        Assert.Equal("Inspect", provider.GetRequiredService<ExecutionSettings>().Mode);
        Assert.False(provider.GetRequiredService<ScheduleSettings>().Enabled);
        Assert.DoesNotContain(services, x => x.ImplementationType == typeof(ScheduleWorker));
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Artifacts");
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
    }
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void ScheduleRequiresObservationPanelAndReplacesAutomaticWorker(bool panel, bool controls, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Schedule:Enabled"] = "true", ["Schedule:SeriesId"] = "registration", ["Schedule:FirstRunUtc"] = "2026-09-08T00:00:00Z",
            ["Schedule:ExpiresUtc"] = "2026-09-08T01:00:00Z", ["Schedule:MaxRuns"] = "2", ["Schedule:MaxTotalActions"] = "10",
            ["Schedule:MaxTotalDecisions"] = "20", ["Schedule:MaxTotalSeconds"] = "600", ["Execution:RunDirectory"] = "registration-fixture",
            ["Operator:Enabled"] = panel.ToString(), ["Operator:ControlsEnabled"] = controls.ToString(), ["Portfolio:AutonomousGoals"] = "true",
            ["ApiSettings:BaseUrl"] = "http://localhost" }).Build();
        var services = new ServiceCollection();
        if (!valid) { Assert.Throws<ArgumentException>(() => services.AddStagedOperation(config)); return; }
        services.AddStagedOperation(config);
        Assert.Contains(services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationType == typeof(ScheduleWorker));
        Assert.DoesNotContain(services, x => x.ImplementationType == typeof(StagedWorker));
    }
    [Fact]
    public void LegacyRegistrationWithoutOptInFailsBeforeWorkerConstruction()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Execution:Mode"] = "Legacy", ["ApiSettings:BaseUrl"] = "http://localhost", ["ApiSettings:Username"] = "mock",
            ["ApiSettings:Password"] = "mock", ["ApiSettings:Character"] = "researcher" }).Build();
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddStagedOperation(config));
    }
}
