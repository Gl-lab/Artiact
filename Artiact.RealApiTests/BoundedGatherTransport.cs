using Artiact.Client;

namespace Artiact.RealApiTests;

internal sealed class BoundedGatherTransport(IGameHttpClient reads, Func<Task<HttpResponseMessage>> gather) : IGameHttpClient
{
    public Task<HttpResponseMessage> GetAsync(string path) => reads.GetAsync(path);
    public Task<HttpResponseMessage> PostAsync(string path, HttpContent? content = null) =>
        path == "/my/gllab/action/gathering" && content is null ? gather() :
            throw new RealApiVerificationException("Only bodyless gllab gathering is allowed.");
}
