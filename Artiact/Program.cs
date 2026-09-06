using System.Diagnostics;
using System.Diagnostics.Metrics;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Models;
using Artiact.Services;
using Artiact.Services.Operation;
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
               .AddEnvironmentVariables()
               .AddCommandLine(args)
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
        builder.Services.Configure<ApiSettings>( apiSettings );
        builder.Services.AddSingleton( resolver => resolver.GetRequiredService<IOptions<ApiSettings>>().Value );

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
                                       .AddOtlpExporter( options =>
                                        {
                                            options.Endpoint = new Uri(builder.Configuration["Telemetry:Endpoint"] ?? "http://localhost:4318/v1/traces");
                                            options.Protocol = OtlpExportProtocol.HttpProtobuf;
                                        } ) );

        // Регистрация сервисов
        builder.Services.AddScoped<ICacheService>(services => new CacheService(services.GetRequiredService<ILogger<ICacheService>>(),
            identity: new CacheIdentity(services.GetRequiredService<ApiSettings>().BaseUrl,
                services.GetRequiredService<ExecutionSettings>().ExpectedApiVersion)));
        builder.Services.AddScoped<IGameHttpClient, GameHttpClient>();
        builder.Services.AddScoped<GameClient>();
        builder.Services.AddScoped<IGameClient>(services => services.GetRequiredService<GameClient>());
        builder.Services.AddScoped<Artiact.Services.Combat.CombatCatalog>();
        builder.Services.AddScoped<Artiact.Services.Combat.CombatSessionFactory>();
        builder.Services.AddScoped<Artiact.Services.Strategy.StrategySessionFactory>();
        builder.Services.AddGoalSelection( builder.Configuration );
        builder.Services.AddMiningProgression( builder.Configuration );
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
        builder.Services.AddStagedOperation(builder.Configuration);

        WebApplication app = builder.Build();

        // Добавляем эндпоинт для метрик Prometheus
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        // Добавляем эндпоинт для информации о состоянии
        app.MapGet("/health/live", () => Results.Ok(new { Status = "Alive" }));
        app.MapGet("/health/ready", (OperationState state, ExecutionSettings settings) =>
        {
            var snapshot = state.Snapshot(settings.FreshnessSeconds);
            return Results.Json(snapshot, statusCode: snapshot.Ready ? 200 : 503);
        });
        app.MapGet("/health", (OperationState state, ExecutionSettings settings) =>
        {
            var snapshot = state.Snapshot(settings.FreshnessSeconds);
            return Results.Json(snapshot, statusCode: snapshot.Ready ? 200 : 503);
        });

        await app.RunAsync();
    }
}
