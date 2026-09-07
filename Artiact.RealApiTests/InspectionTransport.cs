using Artiact.Client;

namespace Artiact.RealApiTests;

internal sealed class InspectionTransport(string character, Func<string, Task<HttpResponseMessage>> read) : IGameHttpClient
{
    public Task<HttpResponseMessage> GetAsync(string requestUri)
    {
        bool allowed = requestUri == "/openapi.json" || requestUri == "/characters/" + Uri.EscapeDataString(character) ||
            System.Text.RegularExpressions.Regex.IsMatch(requestUri, @"\A/(maps|resources)\?page=[1-9][0-9]{0,2}\z");
        return allowed ? read(requestUri) : throw new RealApiVerificationException("Inspection read allowlist rejected request.");
    }
    public Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent? content = null) =>
        throw new RealApiVerificationException("Inspection prohibits action dispatch.");
}
