namespace Artiact.RealApiTests;

public class BoundedGatherTransportTests
{
    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData("/my/other/action/gathering", false)]
    [InlineData("/my/gllab/action/move", false)]
    [InlineData("/my/gllab/action/gathering", true)]
    public async Task RejectsAnythingExceptBodylessGather(string path, bool hasBody)
    {
        int calls = 0;
        var transport = new BoundedGatherTransport(null!, () => { calls++; return Task.FromResult(new HttpResponseMessage()); });
        await Assert.ThrowsAsync<RealApiVerificationException>(() => transport.PostAsync(path, hasBody ? new StringContent("{}") : null));
        Assert.Equal(0, calls);
    }
}
