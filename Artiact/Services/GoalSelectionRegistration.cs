using Artiact.Models;

namespace Artiact.Services;

public static class GoalSelectionRegistration
{
    public static IServiceCollection AddGoalSelection(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GoalSelectionSettings>()
            .Bind(configuration.GetSection("GoalSelection"))
            .Validate(settings => settings.MiningTargetLevel > 0, "MiningTargetLevel must be positive.")
            .ValidateOnStart();
        services.AddScoped<IGoalService, GoalService>();
        return services;
    }
}
