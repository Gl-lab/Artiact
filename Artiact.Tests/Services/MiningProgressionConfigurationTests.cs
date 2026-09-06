using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Artiact.Tests.Services;

public class MiningProgressionConfigurationTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("100", null)]
    [InlineData(null, "3")]
    [InlineData("abc", "3")]
    [InlineData("100", "1.5")]
    [InlineData("0", "3")]
    [InlineData("-1", "3")]
    [InlineData("100", "0")]
    [InlineData("100", "-1")]
    [InlineData("2", "3")]
    public void InvalidLimitsFailStartupValidation(string? cycles, string? noProgress)
    {
        using var provider = Provider(cycles, noProgress);
        Assert.ThrowsAny<Exception>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void ProductionRegistrationCapturesLimitsAndIsolatesScopes()
    {
        using var provider = Provider("100", "3");
        provider.GetRequiredService<IStartupValidator>().Validate();
        using var first = provider.CreateScope(); using var second = provider.CreateScope();
        var state = first.ServiceProvider.GetRequiredService<MiningRunState>();
        Assert.Same(state, first.ServiceProvider.GetRequiredService<MiningRunState>());
        state.ReserveAttempt();
        Assert.Equal(0, second.ServiceProvider.GetRequiredService<MiningRunState>().AttemptedCycles);
        Assert.Equal(100, state.MaxCycles); Assert.Equal(3, state.MaxNoProgress);
        provider.GetRequiredService<IOptions<MiningProgressionSettings>>().Value.MaxCycles = 1;
        Assert.Equal(100, state.MaxCycles);
        Assert.IsType<MiningCooldownDelay>(first.ServiceProvider.GetRequiredService<IMiningCooldownDelay>());
    }

    private static ServiceProvider Provider(string? cycles, string? noProgress) => new ServiceCollection()
        .AddMiningProgression(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MiningProgression:MaxCycles"] = cycles, ["MiningProgression:MaxConsecutiveNoProgress"] = noProgress
        }).Build()).BuildServiceProvider();
}
