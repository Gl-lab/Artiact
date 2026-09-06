using Artiact.Models;

namespace Artiact.Services;

public static class MiningProgressionRegistration
{
    public static IServiceCollection AddMiningProgression(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MiningProgressionSettings>()
            .Bind(configuration.GetSection("MiningProgression"))
            .Validate(settings => settings.MaxCycles > 0 && settings.MaxConsecutiveNoProgress > 0 &&
                settings.MaxConsecutiveNoProgress <= settings.MaxCycles, "Mining progression limits must be positive and no-progress must not exceed cycles.")
            .ValidateOnStart();
        services.AddScoped<MiningRunState>();
        services.AddScoped<MiningDestinationResolver>();
        services.AddScoped<IMiningCooldownDelay, MiningCooldownDelay>();
        return services;
    }
}
