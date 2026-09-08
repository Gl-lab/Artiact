using Artiact.Services.Strategy;

namespace Artiact.Tests.Services;

public sealed class NeedOrderStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "artiact-orders-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public void ReadDoesNotCreateStorageAndTombstoneSurvivesRunArchive()
    {
        var store = new NeedOrderStore(_directory, "hero");
        Assert.Empty(store.Read().Orders); Assert.False(Directory.Exists(_directory));
        store.Put(new("order", "tool", 3), 0);
        store.Put(new("order", "tool", 3, Revision: 2, Status: "Cancelled"), 1);
        var reopened = new NeedOrderStore(_directory, "HERO");
        Assert.Equal("Cancelled", Assert.Single(reopened.Read().Orders).Status);
        Assert.Throws<InvalidOperationException>(() => reopened.Put(new("order", "tool", 3, Revision: 3), 2));
        Assert.Empty(Directory.GetFiles(_directory, "*.json"));
    }
    [Fact]
    public void StaleRevisionAndCorruptionCannotOverwriteBook()
    {
        var store = new NeedOrderStore(_directory, "hero"); store.Put(new("order", "tool", 3), 0);
        Assert.Throws<InvalidOperationException>(() => store.Put(new("order", "tool", 4), 0));
        Assert.Equal(3, Assert.Single(store.Read().Orders).Quantity);
        string file = Assert.Single(Directory.GetFiles(Path.Combine(_directory, "orders"), "*.json"));
        File.WriteAllText(file, "{}");
        Assert.Throws<IOException>(() => store.Read());
        Assert.Throws<IOException>(() => store.Put(new("another", "tool", 1), 0));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
