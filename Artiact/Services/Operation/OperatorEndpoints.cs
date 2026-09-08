namespace Artiact.Services.Operation;

public sealed class OperatorSettings
{
    public bool Enabled { get; set; }
    public bool ControlsEnabled { get; set; }
}

public static class OperatorEndpoints
{
    public static void MapOperator(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<OperatorSettings>();
        if (!options.Enabled) return;
        string controlToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var group = app.MapGroup("/operator");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (HttpMethods.IsPost(http.Request.Method) && (!SameOrigin(http) || http.Request.Headers["X-Artiact-Control"] != controlToken))
                return Results.StatusCode(403);
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            return await next(context);
        });
        group.MapGet("", () => Asset("panel.html", "text/html; charset=utf-8"));
        group.MapGet("/panel.css", () => Asset("panel.css", "text/css"));
        group.MapGet("/panel.js", () => Asset("panel.js", "text/javascript"));
        group.MapGet("/snapshot", (OperatorSnapshotReader reader) => Results.Json(reader.Read()));
        group.MapGet("/controls", () => Results.Json(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            Enabled = options.ControlsEnabled, Token = options.ControlsEnabled ? controlToken : null,
            Profile = options.ControlsEnabled ? (System.Text.Json.JsonElement?)app.Services.GetRequiredService<OperatorCoordinator>().Profile() : null
        })));
        var schedule = app.Services.GetService<ScheduleSettings>();
        group.MapGet("/schedule", () => Results.Json(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            Enabled = schedule?.Enabled == true,
            Token = schedule?.Enabled == true ? controlToken : null,
            SeriesId = schedule?.Enabled == true ? schedule.SeriesId : null,
            Limits = schedule?.Enabled == true ? new { schedule.MaxRuns, schedule.MaxTotalActions, schedule.MaxTotalDecisions, schedule.MaxTotalSeconds, schedule.ExpiresUtc } : null,
            State = schedule?.Enabled == true ? app.Services.GetRequiredService<ScheduleRunner>().Snapshot() : null
        })));
        if (schedule?.Enabled == true)
            group.MapPost("/schedule/stop", (ScheduleRunner runner) => { runner.RequestStop(); return Results.Accepted(); });
        if (!options.ControlsEnabled) return;
        group.MapPost("/inspect", async (OperatorRunRequest request, OperatorCoordinator coordinator) => Control(await coordinator.InspectAsync(request)));
        group.MapPost("/start", async (StartRequest request, OperatorCoordinator coordinator) => Control(await coordinator.StartRunAsync(request.Receipt)));
        group.MapPost("/stop", async (OperatorCoordinator coordinator) => Control(await coordinator.RequestStopAsync()));
        group.MapPost("/archive", async (ArchiveRequest request, OperatorCoordinator coordinator) => Control(await coordinator.ArchiveAsync(request.IdentityDigest)));
    }

    public sealed record StartRequest(string Receipt);
    public sealed record ArchiveRequest(string IdentityDigest);
    private static IResult Control(OperatorControlResult result) => Results.Json(System.Text.Json.JsonSerializer.SerializeToElement(result), statusCode: result.Accepted ? 200 : 409);

    public static bool SameOrigin(HttpContext context)
    {
        string origin = context.Request.Headers.Origin.ToString();
        string site = context.Request.Headers["Sec-Fetch-Site"].ToString();
        return (string.IsNullOrEmpty(origin) || string.Equals(origin, context.Request.Scheme + "://" + context.Request.Host, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(site) || site is "same-origin" or "none");
    }

    public static bool IsLocal(HttpContext context) => context.Connection.RemoteIpAddress is { } address &&
        System.Net.IPAddress.IsLoopback(address) && (context.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        System.Net.IPAddress.TryParse(context.Request.Host.Host.Trim('[', ']'), out var host) && System.Net.IPAddress.IsLoopback(host));

    private static IResult Asset(string name, string contentType)
    {
        using var stream = typeof(OperatorEndpoints).Assembly.GetManifestResourceStream("Artiact.Operator." + name)!;
        using var reader = new StreamReader(stream);
        return Results.Content(reader.ReadToEnd(), contentType);
    }
}
