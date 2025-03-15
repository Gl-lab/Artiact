namespace Artiact.Client;

public interface ICacheService
{
    Task<T?> GetFromCache<T>() where T : class;
    Task SaveToCache<T>(T data) where T : class;
}
