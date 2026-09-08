using Artiact.Client;
using Artiact.Contracts.Client;
using Artiact.Services.Strategy;
using System.Collections.Immutable;
using System.Text.Json;

namespace Artiact.RealApiTests;

internal sealed class ScopedAutonomousTransport(IGameHttpClient reads, string initialFingerprint, string worldFingerprint,
    string policyIdentity, Func<string, HttpContent?, Task<HttpResponseMessage>> send, Action stop, TimeProvider? time = null) : IGameHttpClient
{
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, List<JsonElement>> _catalogs = [];
    private readonly Dictionary<string, int> _nextPage = [], _lastPage = [];
    private StrategyObservation? _observed;
    private DateTimeOffset _observedAt;
    private bool _latched;
    public int Sent { get; private set; }

    public async Task SealAsync()
    {
        await _gate.WaitAsync();
        try { _latched = true; }
        finally { _gate.Release(); }
    }

    public async Task<HttpResponseMessage> GetAsync(string path)
    {
        await _gate.WaitAsync();
        HttpResponseMessage? response = null;
        try
        {
            _observed = null;
            response = await reads.GetAsync(path);
            response.EnsureSuccessStatusCode();
            if (path == "/openapi.json") return response;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            if (path == "/characters/gllab")
            {
                if (_catalogs.Count != 3 || _nextPage.Any(x => x.Value != _lastPage[x.Key] + 1))
                    throw new InvalidOperationException("Complete catalogs required.");
                _observed = new(root.GetProperty("data"), _catalogs.ToImmutableDictionary(x => x.Key, x => x.Value.ToImmutableArray()), policyIdentity);
                _observedAt = _time.GetUtcNow();
            }
            else
            {
                var match = System.Text.RegularExpressions.Regex.Match(path, @"\A/(maps|resources|items)\?page=([1-9][0-9]{0,2})\z");
                if (!match.Success) throw new InvalidOperationException("Unsupported scope read.");
                string name = match.Groups[1].Value;
                int page = int.Parse(match.Groups[2].Value), pages = root.GetProperty("pages").GetInt32();
                if (name == "maps" && page == 1) { _catalogs.Clear(); _nextPage.Clear(); _lastPage.Clear(); }
                if (page == 1) { _catalogs[name] = []; _nextPage[name] = 1; _lastPage[name] = pages; }
                if (pages is < 1 or > 999 || root.GetProperty("page").GetInt32() != page ||
                    !_nextPage.TryGetValue(name, out int next) || next != page || _lastPage[name] != pages || page > pages)
                    throw new InvalidOperationException("Incomplete scope catalog.");
                _catalogs[name].AddRange(root.GetProperty("data").EnumerateArray().Select(x => x.Clone()));
                _nextPage[name]++;
            }
            return response;
        }
        catch { response?.Dispose(); _observed = null; throw; }
        finally { _gate.Release(); }
    }

    public async Task<HttpResponseMessage> PostAsync(string path, HttpContent? content = null)
    {
        await _gate.WaitAsync();
        try
        {
            bool move = path == "/my/gllab/action/move";
            bool gather = path == "/my/gllab/action/gathering";
            if (_latched || Sent >= 16 || _observed is null || (!move && !gather) ||
                _time.GetUtcNow() < _observedAt || _time.GetUtcNow() - _observedAt > TimeSpan.FromSeconds(30) ||
                _observed.WorldFingerprint != worldFingerprint || Sent == 0 && _observed.Fingerprint != initialFingerprint)
                throw new ActionFailureException(ActionFailureKind.Rejected);
            try
            {
                var state = CharacterObservation.Read(_observed.Character);
                if (state is null || state.Name != "gllab" || state.Layer != "overworld" || state.Inventory.GetValueOrDefault("copper_ore") < 2 ||
                    _observed.Character.GetProperty("alchemy_level").GetInt32() != 1 || state.FreeUnits < 1 ||
                    (move ? state.MapId != 277 || Sent != 0 : state.MapId != 379))
                    throw new ActionFailureException(ActionFailureKind.Rejected);
                if (move)
                {
                    if (content is null) throw new ActionFailureException(ActionFailureKind.Rejected);
                    using var body = JsonDocument.Parse(await content.ReadAsStringAsync());
                    if (body.RootElement.ValueKind != JsonValueKind.Object || body.RootElement.EnumerateObject().Count() != 1 ||
                        !body.RootElement.TryGetProperty("map_id", out var id) || !id.TryGetInt32(out int map) || map != 379)
                        throw new ActionFailureException(ActionFailureKind.Rejected);
                }
                else if (content is not null) throw new ActionFailureException(ActionFailureKind.Rejected);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
            { throw new ActionFailureException(ActionFailureKind.Rejected); }

            _observed = null;
            _catalogs.Clear(); _nextPage.Clear(); _lastPage.Clear();
            Sent++;
            HttpResponseMessage? response = null;
            try
            {
                response = await send(path, content);
                if (!response.IsSuccessStatusCode) { _latched = true; return response; }
                using var reply = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var character = reply.RootElement.GetProperty("data").GetProperty("character");
                var after = CharacterObservation.Read(character);
                if (after is null || after.Name != "gllab" || after.MapId != 379 || after.Layer != "overworld" ||
                    after.Inventory.GetValueOrDefault("copper_ore") < 2)
                    throw new ActionFailureException(ActionFailureKind.UnknownOutcome);
                if (character.GetProperty("alchemy_level").GetInt32() >= 2) { _latched = true; stop(); }
                return response;
            }
            catch { _latched = true; response?.Dispose(); throw; }
        }
        finally { _gate.Release(); }
    }
}
