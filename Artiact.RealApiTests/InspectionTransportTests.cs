namespace Artiact.RealApiTests;

public class InspectionTransportTests
{
    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData("/my/hero/action/move", false)]
    [InlineData("/maps?page=1", true)]
    [InlineData("/characters/other", false)]
    [InlineData("https://example.com/maps", false)]
    [InlineData("/maps?page=1&redirect=elsewhere", false)]
    [InlineData("/maps?page=0", false)]
    public async Task ForbiddenRequestsNeverReachNetwork(string path, bool post)
    {
        int requests = 0;
        var transport = new InspectionTransport("hero", _ => { requests++; return Task.FromResult(new HttpResponseMessage()); });
        await Assert.ThrowsAsync<RealApiVerificationException>(() => post ? transport.PostAsync(path) : transport.GetAsync(path));
        Assert.Equal(0, requests);
    }

    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData("/characters/hero")]
    [InlineData("/openapi.json")]
    [InlineData("/maps?page=2")]
    [InlineData("/resources?page=1")]
    public async Task ExactInspectionReadsAreAllowed(string path)
    {
        var paths = new List<string>();
        var transport = new InspectionTransport("hero", request => { paths.Add(request); return Task.FromResult(new HttpResponseMessage()); });
        using var response = await transport.GetAsync(path);
        Assert.Equal(new[] { path }, paths);
    }
}
