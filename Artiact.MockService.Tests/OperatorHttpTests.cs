using System.Net;
using Artiact;
using Artiact.Services.Operation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Artiact.MockService.Tests;

public class OperatorHttpTests
{
    private sealed class NoExecution : IOperatorExecution
    {
        public Task<Artiact.Services.Strategy.StrategyDecision?> ExecuteAsync(ExecutionSettings settings, CancellationToken token) =>
            throw new InvalidOperationException("Stop must not start execution.");
    }

    [Theory]
    [InlineData(false, "http://localhost", 403, false)]
    [InlineData(true, "http://attacker.example", 403, false)]
    [InlineData(true, "http://localhost", 200, false)]
    [InlineData(true, "http://artiact.lan:5080", 200, true)]
    [InlineData(false, "http://artiact.lan:5080", 403, true)]
    [InlineData(true, "http://attacker.example", 403, true)]
    public async Task ControlPostsRequireSameOriginAndReceiptHeader(bool token, string origin, int expected, bool lan)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new OperatorSettings { Enabled = true, ControlsEnabled = true });
        builder.Services.AddSingleton(new ExecutionSettings());
        builder.Services.AddSingleton(new ApiSettings { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "secret-test-value" });
        builder.Services.AddSingleton(new PortfolioSettings { AutonomousGoals = true });
        builder.Services.AddSingleton<OperationState>(); builder.Services.AddSingleton<OperatorSnapshotReader>();
        builder.Services.AddSingleton<IOperatorExecution, NoExecution>(); builder.Services.AddSingleton<OperatorCoordinator>();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = lan ? IPAddress.Parse("192.168.1.50") : IPAddress.Loopback; return next(context); });
        app.MapOperator(); await app.StartAsync();
        using var client = app.GetTestClient();
        if (lan) client.BaseAddress = new Uri("http://artiact.lan:5080");
        using var response = await client.GetAsync("/operator/controls");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var controls = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (token) client.DefaultRequestHeaders.Add("X-Artiact-Control", controls.RootElement.GetProperty("Token").GetString());
        client.DefaultRequestHeaders.Add("Origin", origin);
        var stopped = await client.PostAsync("/operator/stop", null);
        Assert.Equal(expected, (int)stopped.StatusCode);
    }

    [Theory]
    [InlineData(false, "http://localhost", 403, false)]
    [InlineData(true, "http://attacker.example", 403, false)]
    [InlineData(true, "http://localhost", 202, false)]
    [InlineData(true, "http://artiact.lan:5080", 202, true)]
    [InlineData(false, "http://artiact.lan:5080", 403, true)]
    [InlineData(true, "http://attacker.example", 403, true)]
    public async Task ScheduleStopRequiresSameOriginAndTokenWithoutManualControls(bool token, string origin, int expected, bool lan)
    {
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new OperatorSettings { Enabled = true });
        builder.Services.AddSingleton(new ScheduleSettings { Enabled = true, SeriesId = "http-test" });
        builder.Services.AddSingleton(new ExecutionSettings());
        builder.Services.AddSingleton(new ApiSettings { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "secret-test-value" });
        builder.Services.AddSingleton(new PortfolioSettings { AutonomousGoals = true });
        builder.Services.AddSingleton<OperationState>(); builder.Services.AddSingleton<OperatorSnapshotReader>();
        builder.Services.AddSingleton<IOperatorExecution, NoExecution>(); builder.Services.AddSingleton<IScheduledRun, ScheduledRun>();
        builder.Services.AddSingleton<ScheduleRunner>();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = lan ? IPAddress.Parse("192.168.1.50") : IPAddress.Loopback; return next(context); });
        app.MapOperator(); await app.StartAsync(); using var client = app.GetTestClient();
        if (lan) client.BaseAddress = new Uri("http://artiact.lan:5080");
        string body = await client.GetStringAsync("/operator/schedule"); Assert.DoesNotContain("secret-test-value", body);
        using var json = System.Text.Json.JsonDocument.Parse(body);
        if (token) client.DefaultRequestHeaders.Add("X-Artiact-Control", json.RootElement.GetProperty("Token").GetString());
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var stopped = await client.PostAsync("/operator/schedule/stop", null);
        Assert.Equal(expected, (int)stopped.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/operator/start", null)).StatusCode);
    }

    [Theory]
    [InlineData(true, "127.0.0.1", "localhost", 200)]
    [InlineData(false, "127.0.0.1", "localhost", 404)]
    [InlineData(true, "192.168.1.50", "192.168.1.10:5080", 200)]
    [InlineData(true, "192.168.1.50", "artiact.lan:5080", 200)]
    [InlineData(false, "192.168.1.50", "artiact.lan:5080", 404)]
    public async Task PanelReadIsExplicitAndSupportsServerAddresses(bool enabled, string remote, string host, int expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(new OperatorSettings { Enabled = enabled });
        builder.Services.AddSingleton(new ExecutionSettings());
        builder.Services.AddSingleton(new ApiSettings { BaseUrl = "http://localhost", Character = "hero", Username = "test", Password = "secret-test-value" });
        builder.Services.AddSingleton<OperationState>();
        builder.Services.AddSingleton<OperatorSnapshotReader>();
        await using var app = builder.Build();
        app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(remote); return next(context); });
        app.MapOperator();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.DefaultRequestHeaders.Host = host;
        foreach (string path in new[] { "/operator", "/operator/snapshot", "/operator/panel.js", "/operator/panel.css" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(expected, (int)response.StatusCode);
            Assert.DoesNotContain("secret-test-value", await response.Content.ReadAsStringAsync());
            if (expected == 200) Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }
}
