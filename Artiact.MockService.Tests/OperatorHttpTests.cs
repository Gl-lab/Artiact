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
    [Theory]
    [InlineData(true, "127.0.0.1", "localhost", 200)]
    [InlineData(false, "127.0.0.1", "localhost", 404)]
    [InlineData(true, "192.0.2.1", "localhost", 403)]
    [InlineData(true, "127.0.0.1", "attacker.example", 403)]
    public async Task PanelReadIsExplicitAndLocal(bool enabled, string remote, string host, int expected)
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
        foreach (string path in new[] { "/operator", "/operator/snapshot" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(expected, (int)response.StatusCode);
            Assert.DoesNotContain("secret-test-value", await response.Content.ReadAsStringAsync());
            if (expected == 200) Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }
}
