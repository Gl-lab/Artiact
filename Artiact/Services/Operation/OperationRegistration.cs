namespace Artiact.Services.Operation;

public static class OperationRegistration
{
    public static IServiceCollection AddStagedOperation(this IServiceCollection services, IConfiguration configuration)
    {
        var execution = configuration.GetSection("Execution").Get<ExecutionSettings>() ?? new();
        var portfolio = configuration.GetSection("Portfolio").Get<PortfolioSettings>() ?? new();
        services.AddSingleton(execution);
        services.AddSingleton(portfolio);
        services.AddSingleton<OperationState>();
        services.AddScoped<ApiCompatibility>();
        services.AddScoped<StagedExecution>();
        services.AddHttpClient("Artifacts", client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        if (string.Equals(execution.Mode, nameof(ExecutionMode.Legacy), StringComparison.OrdinalIgnoreCase))
        {
            var api = configuration.GetSection("ApiSettings").Get<ApiSettings>() ?? throw new ArgumentException("API configuration required.");
            execution.Validate(api);
            services.AddHostedService<ArtiactBackgroundService>();
        }
        else services.AddHostedService<StagedWorker>();
        return services;
    }
}
