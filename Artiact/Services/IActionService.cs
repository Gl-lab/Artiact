namespace Artiact.Services;

public interface IActionService
{
    Task InitializeAsync( CancellationToken cancellationToken );
    Task ExecuteCycleAsync( CancellationToken cancellationToken );
}