using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Artiact.Services.Strategy;
using Xunit;

namespace Artiact.MockService.Tests;

public class RunProcessRecoveryTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private TaskCompletionSource _hostFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    [Theory]
    [InlineData("before-acceptance", 0, "UnknownOutcome", false)]
    [InlineData("after-acceptance", 1, "Blocked", false)]
    [InlineData("verified-before-cooldown", 1, "Blocked", false)]
    [InlineData("before-acceptance", 0, "UnknownOutcome", true)]
    [InlineData("after-acceptance", 2, "Stopped", true)]
    [InlineData("verified-before-cooldown", 2, "Stopped", true)]
    public async Task KilledHostRecoversWithoutRepeatingPendingPost(string point, int accepted, string terminal, bool autonomous)
    {
        string directory = Path.Combine(Path.GetTempPath(), "artiact-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        await using var mock = new MockServiceFactory();
        using var upstream = new HttpClient(mock.Server.CreateHandler()) { BaseAddress = new("http://localhost") };
        using var reset = await upstream.PostAsync("/__mock/reset", new StringContent(JsonSerializer.Serialize(new { scenario = autonomous ? "discovery-new" : "gathering-bank" }), Encoding.UTF8, "application/json"));
        reset.EnsureSuccessStatusCode();
        await using var proxy = new GateProxy(upstream, point);
        Process? process = null;
        try
        {
            process = StartHost(directory, proxy.Origin, autonomous);
            await proxy.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            if (point == "verified-before-cooldown")
            {
                // Do not hold an observation handle over Windows atomic replacement.
                // The real seven-second cooldown provides a bounded inspection window.
                await Task.Delay(500);
                await WaitForCheckpoint(directory, c => c.Attempts == 1 && c.PendingCommand is null && c.Verified is not null && c.Terminal is null);
            }
            else
            {
                var pending = await WaitForCheckpoint(directory, c => c.PendingCommand is not null);
                Assert.Equal(autonomous ? "Gather:woodcutting" : "Move:4", pending.PendingCommand);
            }
            await Kill(process); process.Dispose(); process = null;
            proxy.Release.TrySetResult();
            process = StartHost(directory, proxy.Origin, autonomous);
            await _hostFinished.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var restored = await WaitForCheckpoint(directory, c => c.Terminal is not null);
            Assert.Equal(terminal, restored.Terminal!.Status.ToString());
            int attempts = autonomous && point != "before-acceptance" ? 2 : 1;
            Assert.Equal(attempts, restored.Attempts);
            Assert.Equal(accepted, proxy.Accepted);
            Assert.Equal(attempts, proxy.Posts);
            Assert.Equal(point == "before-acceptance" ? "Intent" : point == "after-acceptance" ? "Reconciled" : "Verified", restored.Journal[0].Status);
            Assert.Equal(autonomous ? point == "before-acceptance" ? 0 : point == "after-acceptance" ? 5 : 10 : point == "verified-before-cooldown" ? 7 : 0, restored.Seconds);
            if (autonomous && attempts == 2) Assert.Equal(2, Assert.Single(restored.Autonomous!.History).Goal.Target);
        }
        finally
        {
            if (process is not null) { await Kill(process); process.Dispose(); }
            if (!Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cleanup escaped temporary directory.");
            Directory.Delete(directory, true);
        }
    }

    private Process StartHost(string directory, string origin, bool autonomous = false)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = directory,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Artiact.dll"));
        foreach (var arg in new[]
        {
            "--urls=http://127.0.0.1:0", "--Execution:Mode=Bounded", "--Execution:AllowActions=true",
            "--Execution:LiveActionsApproved=false", "--Execution:RunId=process-test", "--Execution:RunDirectory=" + directory,
            "--Execution:MaxActions=" + (autonomous ? 2 : 1), "--Execution:MaxDecisions=20", "--Execution:MaxSeconds=60",
            "--ApiSettings:BaseUrl=" + origin, "--ApiSettings:Character=researcher",
            "--ApiSettings:Username=mock", "--ApiSettings:Password=mock",
            "--Portfolio:CombatTarget=0", "--Portfolio:Equipment=", "--Portfolio:Monster=",
            "--Telemetry:Endpoint=" + origin + "/v1/traces"
        }) start.ArgumentList.Add(arg);
        if (autonomous) start.ArgumentList.Add("--Portfolio:AutonomousGoals=true");
        else foreach (string arg in new[] { "--Portfolio:Skills:0:Skill=mining", "--Portfolio:Skills:0:Target=4", "--Portfolio:Skills:0:Value=30" }) start.ArgumentList.Add(arg);
        start.Environment["LOCALAPPDATA"] = directory;
        start.Environment["XDG_DATA_HOME"] = directory;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        var process = Process.Start(start)!;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _hostFinished = finished;
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                output.WriteLine(e.Data);
                if (e.Data.Contains("Staged decision", StringComparison.Ordinal)) finished.TrySetResult();
            }
        };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.WriteLine(e.Data); };
        process.BeginOutputReadLine(); process.BeginErrorReadLine();
        return process;
    }

    private static async Task Kill(Process process)
    {
        if (!process.HasExited) process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }

    private static async Task<RunCheckpoint> WaitForCheckpoint(string directory, Func<RunCheckpoint, bool> predicate)
    {
        var timeout = Stopwatch.StartNew();
        RunCheckpoint? last = null;
        while (timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            foreach (string path in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var saved = JsonSerializer.Deserialize<RunCheckpoint>(file);
                    last = saved;
                    if (saved is not null && predicate(saved)) return saved;
                }
                catch (IOException) { }
            }
            await Task.Delay(25);
        }
        throw new TimeoutException($"Host checkpoint timeout: attempts={last?.Attempts}, pending={last?.PendingCommand}, reason={last?.Terminal?.Reason}, journal={string.Join(',', last?.Journal.Select(x => x.Status) ?? [])}.");
    }

    private sealed class GateProxy : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly HttpClient _upstream;
        private readonly string _point;
        private readonly Task _loop;
        private readonly List<Task> _requests = [];
        public string Origin { get; }
        public int Posts, Accepted;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public GateProxy(HttpClient upstream, string point)
        {
            _upstream = upstream; _point = point;
            using var port = new TcpListener(IPAddress.Loopback, 0); port.Start();
            Origin = "http://127.0.0.1:" + ((IPEndPoint)port.LocalEndpoint).Port;
            port.Stop();
            _listener.Prefixes.Add(Origin + "/"); _listener.Start();
            _loop = Listen();
        }
        private async Task Listen()
        {
            try
            {
                while (_listener.IsListening) _requests.Add(Serve(await _listener.GetContextAsync()));
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException) { }
        }
        private async Task Serve(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.RawUrl!;
                if (path == "/v1/traces") { context.Response.StatusCode = 200; return; }
                bool action = path.Contains("/action/", StringComparison.Ordinal);
                if (action)
                {
                    Interlocked.Increment(ref Posts);
                    if (_point == "before-acceptance" && Posts == 1)
                    {
                        Reached.TrySetResult(); await Release.Task;
                        context.Response.StatusCode = 503; return;
                    }
                }
                using var request = new HttpRequestMessage(new HttpMethod(context.Request.HttpMethod), path);
                if (context.Request.HasEntityBody)
                {
                    using var reader = new StreamReader(context.Request.InputStream);
                    request.Content = new StringContent(await reader.ReadToEndAsync(), Encoding.UTF8, "application/json");
                }
                if (context.Request.Headers["Authorization"] is { } auth) request.Headers.TryAddWithoutValidation("Authorization", auth);
                using var response = await _upstream.SendAsync(request);
                byte[] body = await response.Content.ReadAsByteArrayAsync();
                if (action)
                {
                    response.EnsureSuccessStatusCode(); Interlocked.Increment(ref Accepted);
                    Reached.TrySetResult();
                    if (_point == "after-acceptance" && Posts == 1) await Release.Task;
                }
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body);
            }
            catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException) { }
            finally
            {
                try { context.Response.Close(); }
                catch (HttpListenerException) { }
            }
        }
        public async ValueTask DisposeAsync()
        {
            Release.TrySetResult(); _listener.Close();
            await _loop; await Task.WhenAll(_requests);
        }
    }
}
