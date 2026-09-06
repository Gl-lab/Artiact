using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Artiact.Services;

public class ArtiactBackgroundService : BackgroundService
{
    private readonly ILogger<ArtiactBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> _recoveryDelay;

    public ArtiactBackgroundService(
        ILogger<ArtiactBackgroundService> logger,
        IServiceProvider serviceProvider,
        Func<TimeSpan, CancellationToken, Task>? recoveryDelay = null)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _recoveryDelay = recoveryDelay ?? Task.Delay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Starting Artiact background service");
            
            // Создаем scope для получения scoped сервисов
            using var scope = _serviceProvider.CreateScope();
            var actionService = scope.ServiceProvider.GetRequiredService<IActionService>();
            
            await actionService.InitializeAsync(stoppingToken);
            _logger.LogInformation("Initialization completed");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var decision = await actionService.ExecuteCycleAsync(stoppingToken);
                    if (decision.Status != Artiact.Models.GoalDecisionStatus.Selected)
                        return;
                }
                catch (Exception) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Artiact.Contracts.Client.ActionFailureException ex)
                {
                    _logger.LogError(ex, "Action failed; autonomous execution stopped pending state inspection");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during action execution");
                    // Ждем некоторое время перед следующей попыткой
                    await _recoveryDelay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Stopping Artiact background service");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical error in background service");
            throw;
        }
    }
}
