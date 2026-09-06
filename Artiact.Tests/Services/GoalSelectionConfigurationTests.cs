using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Artiact.Tests.Services;

public class GoalSelectionConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public void StartupValidation_RejectsMissingOrInvalidTarget(string? value)
    {
        using ServiceProvider provider=Build(value);
        Assert.Empty(provider.GetServices<IHostedService>());
        IStartupValidator validator=provider.GetRequiredService<IStartupValidator>();
        if(value == "abc")
            Assert.Throws<InvalidOperationException>(()=>validator.Validate());
        else
            Assert.Throws<OptionsValidationException>(()=>validator.Validate());
    }
    [Theory]
    [InlineData("20",20)]
    [InlineData("27",27)]
    public void SharedRegistration_BindsTargetAndRegistersSelector(string value,int expected)
    {
        using ServiceProvider provider=Build(value);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Empty(provider.GetServices<IHostedService>());
        using IServiceScope scope=provider.CreateScope();
        Assert.Single(scope.ServiceProvider.GetServices<IGoalService>());
        Assert.Equal(expected,scope.ServiceProvider.GetRequiredService<IGoalService>()
            .Evaluate(GoalServiceTests.Snapshot()).MiningTargetLevel);
        Assert.Equal(expected,provider.GetRequiredService<IOptions<GoalSelectionSettings>>().Value.MiningTargetLevel);
    }
    private static ServiceProvider Build(string? value)
    {
        IConfiguration config=new ConfigurationBuilder().AddInMemoryCollection(
            value is null ? new Dictionary<string,string?>() : new(){["GoalSelection:MiningTargetLevel"]=value}).Build();
        return new ServiceCollection().AddGoalSelection(config).BuildServiceProvider();
    }
}
