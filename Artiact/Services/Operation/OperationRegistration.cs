namespace Artiact.Services.Operation;

public static class OperationRegistration
{
    public static IServiceCollection AddStagedOperation(this IServiceCollection services, IConfiguration configuration)
    {
        var execution = configuration.GetSection("Execution").Get<ExecutionSettings>() ?? new();
        var portfolio = configuration.GetSection("Portfolio").Get<PortfolioSettings>() ?? new();
        if (portfolio.AutonomousGoals && !string.Equals(execution.Mode, nameof(ExecutionMode.Inspect), StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(execution.Mode, nameof(ExecutionMode.Bounded), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("AutonomousGoalsRequireInspectOrBounded");
        services.AddSingleton(execution);
        services.AddSingleton(portfolio);
        services.AddSingleton<OperationState>();
        var panel = configuration.GetSection("Operator").Get<OperatorSettings>() ?? new();
        if (panel.ControlsEnabled && (!panel.Enabled || !string.Equals(execution.Mode, "Inspect", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Operator control requires an enabled panel and Inspect startup mode.");
        services.AddSingleton(panel);
        services.AddSingleton<OperatorSnapshotReader>();
        services.AddSingleton<IOperatorExecution, ScopedOperatorExecution>();
        services.AddSingleton<OperatorCoordinator>();
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
        else if (panel.ControlsEnabled) services.AddHostedService(services => services.GetRequiredService<OperatorCoordinator>());
        else services.AddHostedService<StagedWorker>();
        return services;
    }
}
