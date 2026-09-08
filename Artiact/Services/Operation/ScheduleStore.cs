using System.Text.Json;

namespace Artiact.Services.Operation;

internal sealed record ScheduleRecord(int Version, string Identity, long Sequence, DateTimeOffset UpdatedAt, ScheduleSnapshot Snapshot);

internal sealed class ScheduleStore : IDisposable
{
    private readonly string _path, _marker;
    private readonly FileStream _lease;
    public ScheduleStore(string runDirectory)
    {
        string directory = Path.Combine(Path.GetFullPath(runDirectory), "schedule");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "series.json");
        _marker = Path.Combine(directory, "initialized");
        _lease = new FileStream(Path.Combine(directory, "series.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    public ScheduleRecord? Load()
    {
        try
        {
            using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > 1024 * 1024) throw new IOException("Series state too large.");
            return JsonSerializer.Deserialize<ScheduleRecord>(file) ?? throw new IOException("Invalid series state.");
        }
        catch (FileNotFoundException)
        {
            if (File.Exists(_marker) || File.Exists(_path + ".tmp")) throw new IOException("Missing initialized series state.");
            return null;
        }
    }
    public void Save(ScheduleRecord record)
    {
        if (!File.Exists(_marker))
        {
            using var marker = new FileStream(_marker, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            marker.Write("Finite schedule initialized; do not reset to replay.\n"u8); marker.Flush(true);
        }
        using (var file = new FileStream(_path + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, record); file.Flush(true); }
        File.Move(_path + ".tmp", _path, true);
    }
    public void Dispose() => _lease.Dispose();
}
