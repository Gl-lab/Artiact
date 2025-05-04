using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artiact.Services;

public class ArtiactBackgroundService : BackgroundService
{
    private readonly ILogger<ArtiactBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ArtiactBackgroundService(
        ILogger<ArtiactBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting Artiact background service");
            
            // Создаем scope для получения scoped сервисов
            using var scope = _serviceProvider.CreateScope();
            var actionService = scope.ServiceProvider.GetRequiredService<IActionService>();
            
            await actionService.Initialize();
            _logger.LogInformation("Initialization completed");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await actionService.Action();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during action execution");
                    // Ждем некоторое время перед следующей попыткой
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error in background service");
            throw;
        }
    }
} 