using Artiact;
using Artiact.Services.Operation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Artiact.RealApiTests;

internal static class ScopedOperatorHost
{
    public const string Approval = "gllab:alchemy2:16:900:R19-v1";
    public const string RunId = "r19-gllab-alchemy2-v1";
    public static void RequireApproval(string? value)
    {
        if (value != Approval) throw new InvalidOperationException("Exact scoped operator live approval required.");
    }

    public static WebApplication Build(ExecutionSettings settings, ApiSettings api, PortfolioSettings portfolio,
        OperationState state, IOperatorExecution execution, string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || address.Scheme != "http" || !address.IsLoopback ||
            address.AbsolutePath != "/" || address.UserInfo.Length != 0 || address.Query.Length != 0 || address.Fragment.Length != 0)
            throw new ArgumentException("Loopback host required.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Configuration.Sources.Clear();
        builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, address.Port));
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton(api);
        builder.Services.AddSingleton(portfolio);
        builder.Services.AddSingleton(state);
        builder.Services.AddSingleton(execution);
        builder.Services.AddSingleton(new OperatorSettings { Enabled = true, ControlsEnabled = true });
        builder.Services.AddSingleton<OperatorSnapshotReader>();
        builder.Services.AddSingleton<OperatorCoordinator>();
        builder.Services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(services => services.GetRequiredService<OperatorCoordinator>());
        var app = builder.Build();
        app.MapOperator();
        return app;
    }
}
