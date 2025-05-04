using System.Diagnostics;
using System.Diagnostics.Metrics;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Artiact;

internal class Program
{
    private static async Task Main( string[] args )
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

        // Создаем метрики
        Meter meter = new( "Artiact.Application" );
        
        // Конфигурация
        string environment = builder.Environment.EnvironmentName;
        builder.Configuration
               .SetBasePath( AppContext.BaseDirectory )
               .AddJsonFile( "appsettings.json", false, true )
               .AddJsonFile( $"appsettings.{environment.ToLower()}.json", true, true )
               .AddUserSecrets<Program>()
               .Build();

        // Добавляем сервисы
        builder.Services.AddEndpointsApiExplorer();

        // Логирование
        builder.Services.AddLogging( loggingBuilder =>
        {
            loggingBuilder.AddConsole();
            loggingBuilder.AddNLog( builder.Configuration );
        } );

        // HTTP клиент
        builder.Services.AddHttpClient();

        // Телеметрия
        string artiactName = "Artiact.Client";
        builder.Services.AddOpenTelemetry()
               .WithMetrics( metrics => metrics
                                       .AddMeter( "Artiact.Application" )
                                       .AddPrometheusExporter() )
               .WithTracing( tracing => tracing
                                       .AddSource( artiactName )
                                       .AddConsoleExporter() );

        // Настройки API
        IConfigurationSection apiSettings = builder.Configuration.GetSection( "ApiSettings" );
        builder.Services.Configure<ApiSettings>( apiSettings );
        builder.Services.AddSingleton( resolver => resolver.GetRequiredService<IOptions<ApiSettings>>().Value );

        // Регистрация сервисов
        builder.Services.AddScoped<ICacheService, CacheService>();
        builder.Services.AddScoped<IGameHttpClient, GameHttpClient>();
        builder.Services.AddScoped<IGameClient, GameClient>();
        builder.Services.AddScoped<IGoalService, GoalService>();
        builder.Services.AddScoped<IMapService, MapService>();
        builder.Services.AddScoped<IStepBuilder, StepBuilder>();
        builder.Services.AddScoped<ICharacterService, CharacterService>();
        builder.Services.AddScoped<ICraftTargetEvaluator, CraftTargetEvaluator>();
        builder.Services.AddScoped<ICraftChainBuilder, CraftChainBuilder>();
        builder.Services.AddScoped<IWearCraftTargetFinder, WearCraftTargetFinder>();
        builder.Services.AddScoped<IActionService, ActionService>();
        builder.Services.AddScoped<IGoalDecomposer, GoalDecomposer>();
        builder.Services.AddSingleton( new ActivitySource( artiactName ) );

        // Добавляем фоновый сервис
        builder.Services.AddHostedService<ArtiactBackgroundService>();

        WebApplication app = builder.Build();

        // Добавляем эндпоинт для метрик Prometheus
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        // Добавляем эндпоинт для информации о состоянии
        app.MapGet( "/health", () => Results.Ok( new
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow
        } ) );

        await app.RunAsync();
    }
}

public class ApiSettings
{
    public string BaseUrl { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string Character { get; set; }
}