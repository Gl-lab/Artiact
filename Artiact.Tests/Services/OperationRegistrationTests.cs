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
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Artifacts");
        Assert.Equal(TimeSpan.FromSeconds(30), client.Timeout);
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
