namespace Artiact.RealApiTests;

public class ApprovedMoveTransportTests
{
    [Theory]
    [Trait("Category", "RealApiOffline")]
    [InlineData("/my/gllab/action/gathering", "{}")]
    [InlineData("/my/other/action/move", "{\"map_id\":277}")]
    [InlineData("/my/gllab/action/move", "{\"map_id\":278}")]
    [InlineData("/my/gllab/action/move", "{\"map_id\":277,\"x\":2}")]
    public async Task RejectsUnapprovedActionBeforeDispatch(string path, string body)
    {
        int calls = 0;
        string marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".intent");
        var transport = new ApprovedMoveTransport(null!, marker, (_, _) => { calls++; return Task.FromResult(new HttpResponseMessage()); });
        try
        {
            await Assert.ThrowsAsync<RealApiVerificationException>(() => transport.PostAsync(path, new StringContent(body)));
            Assert.Equal(0, calls);
            Assert.False(File.Exists(marker));
        }
        finally { File.Delete(marker); }
    }

    [Fact]
    [Trait("Category", "RealApiOffline")]
    public async Task LostResponseConsumesApprovalAcrossTransportInstances()
    {
        int calls = 0;
        string marker = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".intent");
        Task<HttpResponseMessage> Send(string path, HttpContent content) { calls++; throw new HttpRequestException(); }
        try
        {
            var first = new ApprovedMoveTransport(null!, marker, Send);
            await Assert.ThrowsAsync<HttpRequestException>(() => first.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":277}")));
            var second = new ApprovedMoveTransport(null!, marker, Send);
            await Assert.ThrowsAsync<IOException>(() => second.PostAsync("/my/gllab/action/move", new StringContent("{\"map_id\":277}")));
            Assert.Equal(1, calls);
        }
        finally { File.Delete(marker); }
    }
}
