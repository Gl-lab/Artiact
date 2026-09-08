using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Artiact.Tests.Services;

public class LoggingDefaultsTests
{
    [Theory]
    [InlineData("Microsoft.AspNetCore.Hosting.Diagnostics", false)]
    [InlineData("Microsoft.AspNetCore.Routing.EndpointMiddleware", false)]
    [InlineData("System.Net.Http.HttpClient.Default.LogicalHandler", false)]
    [InlineData("System.Net.Http.HttpClient.Default.ClientHandler", false)]
    [InlineData("Microsoft.Hosting.Lifetime", true)]
    [InlineData("Artiact.Services.Operation.StagedWorker", true)]
    public void RoutineHttpInformationIsQuietButLifecycleAndWarningsRemain(string category, bool information)
    {
        var config = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json").Build();
        using var factory = LoggerFactory.Create(logging => logging
            .AddConfiguration(config.GetSection("Logging")).AddConsole());
        var logger = factory.CreateLogger(category);
        Assert.Equal(information, logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Warning));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.False(logger.IsEnabled(LogLevel.Debug));
    }
}
