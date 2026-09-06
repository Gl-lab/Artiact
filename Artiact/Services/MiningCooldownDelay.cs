namespace Artiact.Services;

public interface IMiningCooldownDelay
{
    Task WaitAsync(int totalSeconds, CancellationToken cancellationToken);
}

public sealed class MiningCooldownDelay : IMiningCooldownDelay
{
    public Task WaitAsync(int totalSeconds, CancellationToken cancellationToken) =>
        Task.Delay(TimeSpan.FromSeconds(totalSeconds), cancellationToken);
}
