namespace Artiact.Client;

public interface IGameHttpClient
{
    public Task<HttpResponseMessage> PostAsync( string requestUri, HttpContent? content = null );
    public Task<HttpResponseMessage> GetAsync( string requestUri );
}