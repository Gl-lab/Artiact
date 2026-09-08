namespace Artiact.Services.Operation;

public sealed class OperatorSettings
{
    public bool Enabled { get; set; }
}

public static class OperatorEndpoints
{
    public static void MapOperator(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<OperatorSettings>().Enabled) return;
        var group = app.MapGroup("/operator");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!IsLocal(http)) return Results.StatusCode(403);
            http.Response.Headers.CacheControl = "no-store";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            http.Response.Headers.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; frame-ancestors 'none'; base-uri 'none'";
            return await next(context);
        });
        group.MapGet("", () => Asset("panel.html", "text/html; charset=utf-8"));
        group.MapGet("/panel.css", () => Asset("panel.css", "text/css"));
        group.MapGet("/panel.js", () => Asset("panel.js", "text/javascript"));
        group.MapGet("/snapshot", (OperatorSnapshotReader reader) => Results.Json(reader.Read()));
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
