using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Artiact.Services.Strategy;

public sealed record NeedsPolicy(string Directory, bool AllowCraft = false, bool AllowBankWithdrawal = false,
    ImmutableDictionary<string, int>? Reserved = null)
{
    public string Version => "needs-v1";
}
public sealed record NeedOrder(string Id, string Code, int Quantity, int Priority = 1, int Revision = 1, string Status = "Active");
public sealed record NeedOrderBook(int Version, ImmutableArray<NeedOrder> Orders)
{
    public static NeedOrderBook Empty => new(1, []);
    public bool Valid => Version == 1 && !Orders.IsDefault && Orders.Length <= 128 &&
        Orders.All(x => x is not null) && Orders.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == Orders.Length && Orders.All(x =>
            Regex.IsMatch(x.Id ?? "", "^[A-Za-z0-9_-]{1,80}$") && Regex.IsMatch(x.Code ?? "", "^[a-z0-9_]{1,100}$") &&
            x.Quantity is > 0 and <= 10000 && x.Priority is > 0 and <= 100 && x.Revision > 0 && x.Status is "Active" or "Cancelled" or "Completed");
}

public sealed class NeedOrderStore(string directory, string character)
{
    private string PathName => Path.Combine(Path.GetFullPath(directory), "orders", FileRunCheckpointStore.CharacterKey(character) + ".json");
    public NeedOrderBook Read()
    {
        try
        {
            using var stream = new FileStream(PathName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length > 1024 * 1024) throw new IOException("Order book exceeds bound.");
            var book = JsonSerializer.Deserialize<NeedOrderBook>(stream);
            return book is { Valid: true } ? book : throw new IOException("Invalid order book.");
        }
        catch (FileNotFoundException) { return NeedOrderBook.Empty; }
        catch (DirectoryNotFoundException) when (!File.Exists(directory)) { return NeedOrderBook.Empty; }
    }
    public NeedOrderBook Put(NeedOrder order, int expectedRevision)
    {
        string path = PathName;
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var book = Read();
        var old = book.Orders.SingleOrDefault(x => x.Id == order.Id);
        if ((old?.Revision ?? 0) != expectedRevision || old is { Status: not "Active" } ||
            old is null && order.Status != "Active" || order.Revision != expectedRevision + 1)
            throw new InvalidOperationException("Order revision or lifecycle conflict.");
        var next = new NeedOrderBook(1, book.Orders.Where(x => x.Id != order.Id).Append(order).OrderBy(x => x.Id, StringComparer.Ordinal).ToImmutableArray());
        if (!next.Valid) throw new ArgumentException("Invalid order.");
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, next); stream.Flush(true); }
        File.Move(temporary, path, true);
        return next;
    }
}
