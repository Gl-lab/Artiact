using Artiact.Client;

namespace Artiact.RealApiTests;

internal sealed class ApprovedMoveTransport(IGameHttpClient reads, string intentPath,
    Func<string, HttpContent, Task<HttpResponseMessage>> send) : IGameHttpClient
{
    public Task<HttpResponseMessage> GetAsync(string path) => reads.GetAsync(path);
    public async Task<HttpResponseMessage> PostAsync(string path, HttpContent? content = null)
    {
        if (path != "/my/gllab/action/move" || content is null)
            throw new RealApiVerificationException("Action is outside the approved movement.");
        using var body = System.Text.Json.JsonDocument.Parse(await content.ReadAsStringAsync());
        if (body.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
            body.RootElement.EnumerateObject().Count() != 1 ||
            !body.RootElement.TryGetProperty("map_id", out var map) || !map.TryGetInt32(out int id) || id != 277)
            throw new RealApiVerificationException("Body is outside the approved movement.");
        using (var intent = new FileStream(intentPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            intent.Write(System.Text.Encoding.UTF8.GetBytes("Approved gllab Move:277 intent; do not replay.\n"));
            intent.Flush(true);
        }
        return await send(path, content);
    }
}
