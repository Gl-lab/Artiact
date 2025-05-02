using System.Diagnostics;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Artiact;

internal class Program
{
    private static async Task Main( string[] args )
    {
        string environment = Environment.GetEnvironmentVariable( "ASPNETCORE_ENVIRONMENT" ) ?? "Production";

        IConfigurationRoot configuration = new ConfigurationBuilder()
                                          .SetBasePath( AppContext.BaseDirectory )
                                          .AddJsonFile( "appsettings.json", false, true )
                                          .AddJsonFile( $"appsettings.{environment.ToLower()}.json", true,
                                               true )
                                          .AddUserSecrets<Program>()
                                          .Build();

        // Создаем провайдер сервисов
        await using ServiceProvider serviceProvider = ConfigureServices( configuration );

        // Получаем сервис и запускаем приложение
        IActionService actionService = serviceProvider.GetRequiredService<IActionService>();
        await actionService.Initialize();
        await actionService.Action();
    }

    private static ServiceProvider ConfigureServices( IConfigurationRoot configuration )
    {
        ServiceCollection services = new();

        // Логирование
        services.AddLogging( builder =>
        {
            builder.AddConsole();
            builder.AddNLog( configuration );
        } );

        // HTTP клиент
        services.AddHttpClient();
        string artiactName = "Artiact.Client";
        // Создаём TracerProvider
        TracerProvider tracerProvider = Sdk.CreateTracerProviderBuilder()
                                           .AddSource( artiactName )
                                           .AddConsoleExporter()
                                           .Build();

        // Регистрируем его в DI
        services.AddSingleton( tracerProvider );

        // Настройки API
        IConfigurationSection apiSettings = configuration.GetSection( "ApiSettings" );
        services.Configure<ApiSettings>( apiSettings );
        services.AddSingleton( resolver => resolver.GetRequiredService<IOptions<ApiSettings>>().Value );

        // Регистрация сервисов
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IGameHttpClient, GameHttpClient>();
        services.AddScoped<IGameClient, GameClient>();
        services.AddScoped<IGoalService, GoalService>();
        services.AddScoped<IMapService, MapService>();
        services.AddScoped<IStepBuilder, StepBuilder>();
        services.AddScoped<ICharacterService, CharacterService>();

        // Регистрация сервисов крафта
        services.AddScoped<ICraftTargetEvaluator, CraftTargetEvaluator>();
        services.AddScoped<ICraftChainBuilder, CraftChainBuilder>();
        services.AddScoped<IWearCraftTargetFinder, WearCraftTargetFinder>();

        services.AddScoped<IActionService, ActionService>();
        services.AddScoped<IGoalDecomposer, GoalDecomposer>();
        services.AddSingleton( new ActivitySource( artiactName ) );
        return services.BuildServiceProvider();
    }
}

public class ApiSettings
{
    public string BaseUrl { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Character { get; set; }
}