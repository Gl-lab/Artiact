using System.Diagnostics;
using System.Net;
using System.Security.Authentication;
using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Artiact.Tests.Client;

public class SingleDispatchTests
{
    [Theory]
    [InlineData(502)]
    [InlineData(504)]
    [InlineData(500)]
    [InlineData(422)]
    public async Task FailedActionIsDispatchedOnceWithoutResponseBodyLeak(int status)
    {
        var http = new Mock<IGameHttpClient>();
        http.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()))
            .ReturnsAsync(() => new HttpResponseMessage((HttpStatusCode)status)
                { Content = new StringContent("private-response-sentinel") });
        var error = await Record.ExceptionAsync(() => Client(http.Object).Gathering());
        var failure = Assert.IsType<ActionFailureException>(error);
        Assert.Equal(status >= 500 ? ActionFailureKind.UnknownOutcome : ActionFailureKind.Rejected, failure.Kind);
        Assert.Equal(status, failure.StatusCode);
        http.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()), Times.Once);
        Assert.DoesNotContain("private-response-sentinel", error.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostResponseIsNeverRetried(bool timeout)
    {
        var http = new Mock<IGameHttpClient>();
        http.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()))
            .ThrowsAsync(timeout ? new TaskCanceledException() : new HttpRequestException());
        var error = await Assert.ThrowsAsync<ActionFailureException>(() => Client(http.Object).Rest());
        Assert.Equal(ActionFailureKind.UnknownOutcome, error.Kind);
        http.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()), Times.Once);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"data\":{}}")]
    public async Task MissingActionStateIsNotSuccessful(string json)
    {
        var http = new Mock<IGameHttpClient>();
        http.Setup(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        Assert.NotNull(await Record.ExceptionAsync(() => Client(http.Object).Gathering()));
        http.Verify(x => x.PostAsync(It.IsAny<string>(), It.IsAny<HttpContent?>()), Times.Once);
    }

    [Fact]
    public async Task FailedTokenRequestPreventsActionDispatch()
    {
        using var handler = new RejectedTokenHandler();
        using var transport = new HttpClient(handler);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient(It.IsAny<string>())).Returns(transport);
        var http = new GameHttpClient(factory.Object, Settings());
        await Assert.ThrowsAsync<AuthenticationException>(() => http.PostAsync("/my/test/action/fight"));
        Assert.Equal(new[] { "/token" }, handler.Paths);
    }

    internal static GameClient Client(IGameHttpClient http) => new(http, Settings(),
        NullLogger<IGameClient>.Instance, Mock.Of<ICacheService>(), new ActivitySource("single-dispatch-test"));

    private static Artiact.ApiSettings Settings() => new()
        { BaseUrl = "https://test.invalid", Character = "test", Username = "fixture", Password = "fixture" };

    [Theory]
    [InlineData(ActionFailureKind.UnknownOutcome)]
    [InlineData(ActionFailureKind.Rejected)]
    [InlineData(ActionFailureKind.Defeat)]
    public async Task WorkerDoesNotRepeatFailedActionCycle(ActionFailureKind kind)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var action = new Mock<IActionService>();
        action.Setup(x => x.InitializeAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        action.Setup(x => x.ExecuteCycleAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ActionFailureException(kind));
        using var provider = new ServiceCollection().AddSingleton(action.Object).BuildServiceProvider();
        int delays = 0;
        using var worker = new ArtiactBackgroundService(NullLogger<ArtiactBackgroundService>.Instance,
            provider, (_, _) => { delays++; stop.Cancel(); return Task.CompletedTask; });
        await worker.StartAsync(stop.Token);
        await worker.ExecuteTask!;
        action.Verify(x => x.ExecuteCycleAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(0, delays);
    }

    private sealed class RejectedTokenHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        }
    }
}
