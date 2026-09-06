namespace Artiact.Client;

internal static class CancellableHttp
{
    public static Task<HttpResponseMessage> ReadAsync(this IGameHttpClient http, string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return http is GameHttpClient client ? client.GetAsync(path, token) : http.GetAsync(path);
    }
    public static Task<HttpResponseMessage> SendAsync(this IGameHttpClient http, string path, HttpContent? content, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return http is GameHttpClient client ? client.PostAsync(path, content, token) : http.PostAsync(path, content);
    }
}
