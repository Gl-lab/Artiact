using System.Diagnostics;
using System.Diagnostics.Metrics;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Models;
using Artiact.Services;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;

namespace Artiact;

internal class Program
{
    private static async Task Main( string[] args )
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder( args );

        // Создаем метрики
        Meter meter = new( "Artiact.Application" );
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
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

        // Настройки API и Zipkin
        IConfigurationSection apiSettings = builder.Configuration.GetSection( "ApiSettings" );
        IConfigurationSection zipkinSettings = builder.Configuration.GetSection( "ZipkinSettings" );
        builder.Services.Configure<ApiSettings>( apiSettings );
        builder.Services.Configure<ZipkinSettings>( zipkinSettings );
        builder.Services.AddSingleton( resolver => resolver.GetRequiredService<IOptions<ApiSettings>>().Value );
        builder.Services.AddSingleton( resolver => resolver.GetRequiredService<IOptions<ZipkinSettings>>().Value );

        // Телеметрия
        string artiactClientSourceName = "Artiact.Client";
        string serviceName = "Artiact";
        string serviceVersion = "1.0.0";

        builder.Services.AddOpenTelemetry()
               .ConfigureResource(resource => resource
                   .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                   .AddTelemetrySdk()
                   .AddEnvironmentVariableDetector())
               .WithMetrics( metrics => metrics
                                       .AddMeter( "Artiact.Application" )
                                       .AddPrometheusExporter() )
               .WithTracing( tracing => tracing
                                       .AddSource( artiactClientSourceName )
                                       .AddAspNetCoreInstrumentation()
                                       .AddHttpClientInstrumentation()
                                       .AddConsoleExporter()
                                       .AddZipkinExporter( options =>
                                        {
                                            ZipkinSettings zipkinConfig = ServiceCollectionContainerBuilderExtensions
                                                                         .BuildServiceProvider( builder.Services )
                                                                         .GetRequiredService<ZipkinSettings>();
                                            options.Endpoint = new Uri( zipkinConfig.Endpoint );
                                            options.ExportProcessorType = ExportProcessorType.Simple;
                                        } ) );

        // Регистрация сервисов
        builder.Services.AddScoped<ICacheService, CacheService>();
        builder.Services.AddScoped<IGameHttpClient, GameHttpClient>();
        builder.Services.AddScoped<IGameClient, GameClient>();
        builder.Services.AddGoalSelection( builder.Configuration );
        builder.Services.AddScoped<IMapService, MapService>();
        builder.Services.AddScoped<IStepBuilder, StepBuilder>();
        builder.Services.AddScoped<ICharacterService, CharacterService>();
        builder.Services.AddScoped<ICraftTargetEvaluator, CraftTargetEvaluator>();
        builder.Services.AddScoped<ICraftChainBuilder, CraftChainBuilder>();
        builder.Services.AddScoped<ITargetLootingResolver, TargetLootingResolver>();
        builder.Services.AddScoped<IWearCraftTargetFinder, WearCraftTargetFinder>();
        builder.Services.AddScoped<IActionService, ActionService>();
        builder.Services.AddScoped<IGoalDecomposer, GoalDecomposer>();
        builder.Services.AddSingleton( new ActivitySource( artiactClientSourceName ) );

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