namespace Artiact.SmartProxy.Services;

public sealed class CombatScenarioMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, CombatScenarioStore store)
    {
        string body = "";
        if (context.Request.Method == "POST")
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            body = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }
        var result = store.Handle(context.Request.Method, context.Request.Path.Value!, context.Request.QueryString.Value ?? "", body);
        if (result is null) { await next(context); return; }
        context.Response.StatusCode = result.Value.Status;
        context.Response.ContentType = result.Value.Status == 200 ? "application/json" : "application/problem+json";
        await context.Response.WriteAsync(result.Value.Body.ToJsonString());
    }
}
