using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Artiact.Contracts.Models.Api;

namespace Artiact.Client;

public class GameHttpClient : IGameHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _password, _username;
    private readonly SemaphoreSlim _authentication = new(1, 1);
    private readonly TimeProvider _time;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private string? _token;
    private DateTimeOffset _expires = DateTimeOffset.MinValue;

    public GameHttpClient(IHttpClientFactory httpClientFactory, ApiSettings settings,
        TimeProvider? time = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _username = settings.Username; _password = settings.Password;
        _httpClient = httpClientFactory.CreateClient("Artifacts");
        _httpClient.BaseAddress = new Uri(settings.BaseUrl);
        _time = time ?? TimeProvider.System;
        _delay = delay ?? Task.Delay;
    }

    public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent? content = null) => PostAsync(requestUri, content, CancellationToken.None);
    public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent? content, CancellationToken cancellationToken)
    {
        ValidatePath(requestUri);
        string token = await TokenAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var request = Request(HttpMethod.Post, requestUri, token);
        request.Content = content;
        try
        {
            // After dispatch, retain a successful authoritative response even if the caller cancels.
            // HttpClient timeout still bounds the request; no second POST is attempted.
            var response = await _httpClient.SendAsync(request, CancellationToken.None);
            if (response.StatusCode == HttpStatusCode.Unauthorized) Invalidate(token);
            return response;
        }
        finally { request.Content = null; }
    }
    public Task<HttpResponseMessage> GetAsync(string requestUri) => GetAsync(requestUri, CancellationToken.None);
    public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
    {
        ValidatePath(requestUri);
        for (int attempt = 0; ; attempt++)
        {
            string token = await TokenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            using var request = Request(HttpMethod.Get, requestUri, token);
            HttpResponseMessage response;
            try { response = await _httpClient.SendAsync(request, cancellationToken); }
            catch (HttpRequestException) when (attempt == 0)
            { await _delay(TimeSpan.FromSeconds(1), cancellationToken); continue; }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Invalidate(token);
                if (attempt == 0) { response.Dispose(); continue; }
                return response;
            }
            if (attempt == 0 && (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500))
            {
                var wait = response.Headers.RetryAfter?.Delta ??
                    (response.Headers.RetryAfter?.Date is { } date ? date - _time.GetUtcNow() : TimeSpan.FromSeconds(1));
                if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
                if (wait > TimeSpan.FromSeconds(5)) return response;
                response.Dispose();
                await _delay(wait, cancellationToken);
                continue;
            }
            return response;
        }
    }
    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        await _authentication.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _expires > _time.GetUtcNow()) return _token;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/token");
            request.Headers.Authorization = new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}")));
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw new AuthenticationException("Token request failed.");
            TokenContainer? token;
            try { token = JsonSerializer.Deserialize<TokenContainer>(await response.Content.ReadAsStringAsync(cancellationToken)); }
            catch (JsonException) { throw new AuthenticationException("Invalid token response."); }
            if (string.IsNullOrWhiteSpace(token?.Token)) throw new AuthenticationException("Token is missing.");
            cancellationToken.ThrowIfCancellationRequested();
            _token = token.Token; _expires = Expiration(token.Token);
            return _token;
        }
        finally { _authentication.Release(); }
    }
    private DateTimeOffset Expiration(string token)
    {
        try
        {
            string[] parts = token.Split('.');
            if (parts.Length != 3) return DateTimeOffset.MaxValue;
            string payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty("exp", out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()).Subtract(TimeSpan.FromSeconds(30)) : DateTimeOffset.MaxValue;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException or ArgumentOutOfRangeException)
        { return DateTimeOffset.MaxValue; }
    }
    private void Invalidate(string token) { if (_token == token) _expires = DateTimeOffset.MinValue; }
    private static HttpRequestMessage Request(HttpMethod method, string path, string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
    private static void ValidatePath(string path)
    {
        if (!path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\') ||
            path.Contains('#') || path.Contains('\r') || path.Contains('\n')) throw new ArgumentException("Only local API paths are supported.");
    }
}
