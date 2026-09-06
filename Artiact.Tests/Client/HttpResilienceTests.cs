using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Artiact.Client;
using Moq;
using Xunit;

namespace Artiact.Tests.Client;

public class HttpResilienceTests
{
    private sealed class Handler(Func<HttpRequestMessage, int, HttpResponseMessage> handle) : HttpMessageHandler
    {
        public int Tokens, Gets, Posts;
        public List<(string Path, string? Scheme)> Requests = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri!.AbsolutePath;
            Requests.Add((path, request.Headers.Authorization?.Scheme));
            int count = path == "/token" ? ++Tokens : request.Method == HttpMethod.Get ? ++Gets : ++Posts;
            return Task.FromResult(handle(request, count));
        }
    }
    private static HttpResponseMessage Response(int status, string body = "{}") => new((HttpStatusCode)status) { Content = new StringContent(body) };
    private static GameHttpClient Client(Handler handler, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        var factory = new Mock<IHttpClientFactory>(); factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        return new(factory.Object, new ApiSettings { BaseUrl = "http://localhost", Username = "user", Password = "private-password", Character = "hero" }, delay: delay ?? ((_, _) => Task.CompletedTask));
    }
    [Fact]
    public async Task Read401RefreshesOnceAndKeepsBasicOnTokenOnly()
    {
        using var handler = new Handler((r, n) => r.RequestUri!.AbsolutePath == "/token" ? Response(200, "{\"token\":\"opaque\"}") : Response(n == 1 ? 401 : 200));
        using var result = await Client(handler).GetAsync("/characters/hero");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode); Assert.Equal(2, handler.Gets); Assert.Equal(2, handler.Tokens);
        Assert.All(handler.Requests, r => Assert.Equal(r.Path == "/token" ? "Basic" : "Bearer", r.Scheme));
    }
    [Theory]
    [InlineData(401, 503)]
    [InlineData(503, 401)]
    [InlineData(401, 401)]
    public async Task MixedFailuresShareTwoGetBudget(int first, int second)
    {
        using var handler = new Handler((r, n) => r.RequestUri!.AbsolutePath == "/token" ? Response(200, "{\"token\":\"opaque\"}") : Response(n == 1 ? first : second));
        using var result = await Client(handler).GetAsync("/maps");
        Assert.Equal((HttpStatusCode)second, result.StatusCode); Assert.Equal(2, handler.Gets);
        Assert.Equal(first == 401 ? 2 : 1, handler.Tokens);
    }
    [Fact]
    public async Task Action401IsNotRepeatedButNextExplicitReadRefreshes()
    {
        using var handler = new Handler((r, n) => r.RequestUri!.AbsolutePath == "/token" ? Response(200, "{\"token\":\"opaque\"}") : Response(r.Method == HttpMethod.Post ? 401 : 200));
        var client = Client(handler);
        using var action = await client.PostAsync("/my/hero/action/fight");
        using var read = await client.GetAsync("/characters/hero");
        Assert.Equal(1, handler.Posts); Assert.Equal(2, handler.Tokens);
    }
    [Fact]
    public async Task JwtExpiryRefreshesBeforeNextDispatch()
    {
        string expired = "e30." + Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"exp\":1}")).TrimEnd('=').Replace('+', '-').Replace('/', '_') + ".signature";
        using var handler = new Handler((r, n) => r.RequestUri!.AbsolutePath == "/token" ? Response(200, JsonSerializer.Serialize(new { token = n == 1 ? expired : "opaque" })) : Response(200));
        var client = Client(handler); using var first = await client.GetAsync("/maps"); using var second = await client.GetAsync("/items");
        Assert.Equal(2, handler.Tokens);
    }
    [Fact]
    public async Task CrossOriginRequestIsRejectedBeforeAuthentication()
    {
        using var handler = new Handler((r, n) => Response(200, "{\"token\":\"opaque\"}"));
        await Assert.ThrowsAsync<ArgumentException>(() => Client(handler).GetAsync("https://elsewhere.invalid/maps"));
        Assert.Empty(handler.Requests);
    }
    [Fact]
    public async Task CancellationDuringTokenAcquisitionPreventsAction()
    {
        using var cancel = new CancellationTokenSource();
        using var handler = new Handler((r, n) => { cancel.Cancel(); return Response(200, "{\"token\":\"opaque\"}"); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(handler).PostAsync("/my/hero/action/fight", null, cancel.Token));
        Assert.Equal(0, handler.Posts); Assert.Equal(1, handler.Tokens);
    }
    [Fact]
    public async Task CancellationDuringBackoffPreventsRetry()
    {
        using var cancel = new CancellationTokenSource();
        using var handler = new Handler((r, n) => r.RequestUri!.AbsolutePath == "/token" ? Response(200, "{\"token\":\"opaque\"}") : Response(503));
        var client = Client(handler, (_, _) => { cancel.Cancel(); return Task.CompletedTask; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("/maps", cancel.Token));
        Assert.Equal(1, handler.Gets);
    }
    [Theory]
    [InlineData("2", 2, 2)]
    [InlineData("100", 1, -1)]
    [InlineData("nonsense", 2, 1)]
    [InlineData(null, 2, 1)]
    public async Task RetryAfterUsesBoundedDelay(string? retryAfter, int gets, int delay)
    {
        var waits = new List<TimeSpan>();
        using var handler = new Handler((r, n) =>
        {
            if (r.RequestUri!.AbsolutePath == "/token") return Response(200, "{\"token\":\"opaque\"}");
            var response = Response(n == 1 ? 429 : 200);
            if (retryAfter is not null) response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            return response;
        });
        using var result = await Client(handler, (wait, _) => { waits.Add(wait); return Task.CompletedTask; }).GetAsync("/maps");
        Assert.Equal(gets, handler.Gets);
        if (delay == -1) Assert.Empty(waits); else Assert.Equal(TimeSpan.FromSeconds(delay), Assert.Single(waits));
    }
    [Fact]
    public async Task ConcurrentReadsAcquireOneToken()
    {
        using var handler = new Handler((r, n) => r.RequestUri!.AbsolutePath == "/token" ? Response(200, "{\"token\":\"opaque\"}") : Response(200));
        var client = Client(handler);
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.GetAsync("/maps")));
        foreach (var response in responses) response.Dispose();
        Assert.Equal(1, handler.Tokens); Assert.Equal(8, handler.Gets);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyStepCancellationDuringAuthPreventsDispatch(bool move)
    {
        using var cancel = new CancellationTokenSource();
        using var handler = new Handler((r, n) => { cancel.Cancel(); return Response(200, "{\"token\":\"opaque\"}"); });
        var client = new GameClient(Client(handler), new ApiSettings { BaseUrl = "http://localhost", Username = "mock", Password = "mock", Character = "hero" },
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Artiact.Contracts.Client.IGameClient>.Instance,
            new Mock<ICacheService>().Object, new System.Diagnostics.ActivitySource("LegacyCancel"));
        var character = new Artiact.Services.CharacterService();
        Artiact.Models.Steps.IStep step = move
            ? new Artiact.Models.Steps.MoveStep(new Artiact.Contracts.Models.MapPoint { X = 1, Y = 1 }, character)
            : new Artiact.Models.Steps.ActionStep(character, c => c.Rest());
        Assert.NotNull(await Record.ExceptionAsync(() => step.Execute(client, cancel.Token)));
        Assert.Equal(0, handler.Posts);
    }
}
