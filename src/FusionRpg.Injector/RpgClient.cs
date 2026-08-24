using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FusionRpg.Contracts;
using Microsoft.AspNetCore.SignalR.Client;

using FusionRpg.Injector.Host;

namespace FusionRpg.Injector;

public sealed class RpgClient
{
    // Config-backed (tunables-ssot.md T1) — data/tuning/net.v1.json's client.
    public static int QueueCap => FusionRpg.Core.Net.NetPolicy.Tuning.Client.QueueCap;
    public static int DrainSize => FusionRpg.Core.Net.NetPolicy.Tuning.Client.DrainSize;
    public static int FlushMs => FusionRpg.Core.Net.NetPolicy.Tuning.Client.FlushMs;

    private readonly string _base;
    private HttpClient? _http;
    private HubConnection? _hub;
    private readonly ConcurrentQueue<EventEnvelope> _queue = new();
    private long _bullets;
    private int _queued;
    private int _dropped;
    private int _inFlight;
    private long _lastSendMs = Environment.TickCount64;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public StatsConfig Stats { get; set; } = new();
    public bool SignalROk { get; private set; }
    public string LastError { get; private set; } = "";
    public int QueueCount => Volatile.Read(ref _queued);
    public int Dropped => Volatile.Read(ref _dropped);
    public bool InFlight => Volatile.Read(ref _inFlight) != 0;

    public RpgClient(string baseUrl)
    {
        _base = baseUrl.TrimEnd('/');
    }

    HttpClient Http()
    {
        if (_http != null) return _http;
        _http = CreateHttp();
        return _http;
    }

    static HttpClient CreateHttp()
    {
        try { HttpClient.DefaultProxy = new WebProxy(); } catch { /* IL2CPP WinHTTP */ }
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(3)
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
    }

    public async Task StartAsync()
    {
        await RefreshStatsAsync().ConfigureAwait(false);
        await RefreshPvzStatsAsync().ConfigureAwait(false);
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(_base + "/hub/rpg", o =>
                {
                    o.HttpMessageHandlerFactory = inner =>
                    {
                        if (inner is SocketsHttpHandler sockets)
                        {
                            sockets.UseProxy = false;
                            return sockets;
                        }
                        return new SocketsHttpHandler { UseProxy = false };
                    };
                })
                .WithAutomaticReconnect()
                .Build();
            _hub.On<StatsConfig>("StatsUpdated", s =>
            {
                Stats = s;
                CheatCommandRunner.Enqueue(new CommandDto { Name = "reload-stats" });
            });
            _hub.On<object>("PvzStatsUpdated", _ =>
            {
                CheatCommandRunner.Enqueue(new CommandDto { Name = "pvz.stats.reload" });
            });
            _hub.On<CommandDto>("Command", cmd =>
            {
                try { RpgHost.Log.Info("[cheat-cmd] signalr " + (cmd?.Name ?? "?")); } catch { }
                // Patron designation is state, not a cheat action — cache it here and keep it
                // out of the cheat runner (spec-patron-demon.md; applies from the NEXT match).
                if (string.Equals(cmd?.Name, "patron.aura", StringComparison.OrdinalIgnoreCase))
                {
                    try { Effects.PatronCommand.Apply(cmd!); } catch (Exception ex) { RpgHost.Log.Warning("patron.aura: " + ex.Message); }
                    return;
                }

                CheatCommandRunner.Enqueue(cmd!);
            });
            _hub.Reconnected += async _ =>
            {
                try
                {
                    await _hub.InvokeAsync("Join", RpgConstants.InjectorGroup).ConfigureAwait(false);
                    await _hub.InvokeAsync("Hello", new HelloDto { Game = RpgHost.GameProfileId, Version = "1.0.0" }).ConfigureAwait(false);
                    RpgHost.Log.Info("SignalR reconnected + re-joined + Hello (grant rehydrate)");
                }
                catch (Exception ex)
                {
                    RpgHost.Log.Warning("SignalR re-join/Hello failed: " + ex.Message);
                }
            };
            await _hub.StartAsync().ConfigureAwait(false);
            await _hub.InvokeAsync("Join", RpgConstants.InjectorGroup).ConfigureAwait(false);
            await _hub.InvokeAsync("Hello", new HelloDto { Game = RpgHost.GameProfileId, Version = "1.0.0" }).ConfigureAwait(false);
            SignalROk = true;
            RpgHost.Log.Info("SignalR connected");
        }
        catch (Exception ex)
        {
            SignalROk = false;
            LastError = ex.Message;
            RpgHost.Log.Warning("SignalR failed, using HTTP fallback: " + ex.Message);
        }
        GameHooks.RequestTypeCatalog();
    }

    /// <summary>Pull queued cheat commands over HTTP (reliable path when SignalR group send fails).</summary>
    public async Task PullPendingCommandsAsync()
    {
        try
        {
            var json = await Http().GetStringAsync(_base + "/api/cheats/commands/pending").ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return;
            foreach (var el in items.EnumerateArray())
            {
                var cmd = el.Deserialize<CommandDto>(Json);
                if (cmd != null)
                    CheatCommandRunner.Enqueue(cmd);
            }
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public void BumpBullet() => Interlocked.Increment(ref _bullets);

    /// <summary>Fire-and-forget almanac layer dump (not the event queue).</summary>
    public void EnqueueAlmanacTextDump(
        string side,
        int typeId,
        Dictionary<string, string?> fields,
        Dictionary<string, string> sources)
    {
        if (fields == null || fields.Count == 0) return;
        _ = UploadAlmanacTextDumpAsync(side, typeId, fields, sources);
    }

    async Task UploadAlmanacTextDumpAsync(
        string side,
        int typeId,
        Dictionary<string, string?> fields,
        Dictionary<string, string> sources)
    {
        try
        {
            try
            {
                var head = await Http().GetAsync($"{_base}/api/almanac/dump/{side}/{typeId}").ConfigureAwait(false);
                if (head.IsSuccessStatusCode)
                {
                    RpgHost.Log.Info($"[almanac-text] skip (cached) {side}/{typeId}");
                    return;
                }
            }
            catch { /* upload */ }

            var payload = new { fields, sources };
            var body = JsonSerializer.Serialize(payload, Json);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await Http().PutAsync($"{_base}/api/almanac/dump/{side}/{typeId}", content).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                RpgHost.Log.Warning($"[almanac-text] upload {side}/{typeId} -> {(int)resp.StatusCode}");
            else
                RpgHost.Log.Info($"[almanac-text] uploaded {side}/{typeId} fields={fields.Count}");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            try { RpgHost.Log.Warning("[almanac-text] upload failed: " + ex.Message); } catch { }
        }
    }

    public void EnqueueIconDump(string side, int typeId, List<TypeIconCapture.Layer> layers)
    {
        if (layers == null || layers.Count == 0) return;
        _ = UploadIconDumpAsync(side, typeId, layers);
    }

    async Task UploadIconDumpAsync(string side, int typeId, List<TypeIconCapture.Layer> layers)
    {
        try
        {
            // Skip if server already has this dump (DB cache).
            try
            {
                var head = await Http().GetAsync($"{_base}/api/icons/dump/{side}/{typeId}").ConfigureAwait(false);
                if (head.IsSuccessStatusCode)
                {
                    RpgHost.Log.Info($"[icon] dump skip (cached) {side}/{typeId}");
                    return;
                }
            }
            catch { /* continue upload */ }

            var payload = new
            {
                layers = layers.Select(l => new
                {
                    name = l.Name,
                    source = l.Source,
                    width = l.Width,
                    height = l.Height,
                    pngBase64 = Convert.ToBase64String(l.Png)
                }).ToList()
            };
            var body = JsonSerializer.Serialize(payload, Json);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var resp = await Http().PutAsync($"{_base}/api/icons/dump/{side}/{typeId}", content).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                RpgHost.Log.Warning($"[icon] dump upload {side}/{typeId} -> {(int)resp.StatusCode}");
            else
                RpgHost.Log.Info($"[icon] dump uploaded {side}/{typeId} layers={layers.Count}");
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            try { RpgHost.Log.Warning("[icon] dump upload failed: " + ex.Message); } catch { }
        }
    }

    public void Enqueue(string kind, object? payload, string? matchKey = null)
    {
        var n = Volatile.Read(ref _queued);
        if (n >= QueueCap && RpgConstants.IsDroppableWhenFull(kind))
        {
            Interlocked.Increment(ref _dropped);
            return;
        }
        _queue.Enqueue(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Game = RpgHost.GameProfileId,
            Kind = kind,
            MatchKey = matchKey,
            Payload = payload
        });
        Interlocked.Increment(ref _queued);
    }

    public void TryFlush()
    {
        var n = Volatile.Read(ref _queued);
        if (n == 0) return;
        if (Volatile.Read(ref _inFlight) != 0) return;
        var elapsedMs = Environment.TickCount64 - _lastSendMs;
        if (n < DrainSize && elapsedMs < FlushMs) return;
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0) return;

        var batch = new List<EventEnvelope>(DrainSize);
        while (batch.Count < DrainSize && _queue.TryDequeue(out var e))
            batch.Add(e);
        if (batch.Count == 0)
        {
            Volatile.Write(ref _inFlight, 0);
            return;
        }
        Interlocked.Add(ref _queued, -batch.Count);
        _lastSendMs = Environment.TickCount64;
        _ = SendBatch(batch);
    }

    public async Task RefreshStatsAsync()
    {
        try
        {
            var json = await Http().GetStringAsync(_base + "/api/stats").ConfigureAwait(false);
            var s = JsonSerializer.Deserialize<StatsConfig>(json, Json);
            if (s != null) Stats = s;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public async Task RefreshPvzStatsAsync()
    {
        try
        {
            var playerJson = await Http().GetStringAsync(_base + "/api/players/current").ConfigureAwait(false);
            using var playerDoc = JsonDocument.Parse(playerJson);
            var playerId = playerDoc.RootElement.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var pid)
                ? pid
                : 0L;
            if (playerId <= 0) return;
            var json = await Http().GetStringAsync(_base + "/api/pvz-stats/" + playerId + "/modifiers").ConfigureAwait(false);
            var dto = JsonSerializer.Deserialize<PvzStatsModifiersDto>(json, Json);
            if (dto == null) return;
            CheatState.ApplyPvzStatsModifiers(dto.PlayerId, dto.Revision, dto.Modifiers);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }

    public async Task PushCheatSnapshotAsync()
    {
        try
        {
            var snap = CheatState.Snapshot();
            snap["catalog"] = new Dictionary<string, object>
            {
                ["plants"] = SpawnCatalog.List("plant").Take(80).Select(e => new Dictionary<string, object>
                {
                    ["type"] = e.Type,
                    ["typeName"] = e.TypeName,
                    ["displayName"] = e.DisplayName ?? "",
                    ["spawnOk"] = e.SpawnOk
                }).ToList(),
                ["zombies"] = SpawnCatalog.List("zombie").Take(80).Select(e => new Dictionary<string, object>
                {
                    ["type"] = e.Type,
                    ["typeName"] = e.TypeName,
                    ["displayName"] = e.DisplayName ?? "",
                    ["spawnOk"] = e.SpawnOk
                }).ToList()
            };
            var body = JsonSerializer.Serialize(snap, Json);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            // Mirror catalog only (server endpoint must not overwrite entries or CheatsUpdated → web).
            await Http().PutAsync(_base + "/api/cheats/mirror", content).ConfigureAwait(false);
        }
        catch
        {
            /* ignore push failures */
        }
    }

    public async Task HeartbeatAsync()
    {
        try
        {
            var metrics = new List<MetricItem>
            {
                new()
                {
                    Name = "bullets_spawned",
                    Value = Interlocked.Read(ref _bullets),
                    Ts = DateTime.UtcNow.ToString("o")
                }
            };
            if (SignalROk && _hub?.State == HubConnectionState.Connected)
            {
                await _hub.InvokeAsync("Heartbeat", new HelloDto()).ConfigureAwait(false);
                await _hub.InvokeAsync("Metrics", metrics).ConfigureAwait(false);
                return;
            }
            using var hb = new StringContent("{}", Encoding.UTF8, "application/json");
            await Http().PostAsync(_base + "/api/heartbeat", hb).ConfigureAwait(false);
            var body = JsonSerializer.Serialize(new { items = metrics }, Json);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            await Http().PostAsync(_base + "/api/metrics", content).ConfigureAwait(false);
        }
        catch
        {
            SignalROk = false;
        }
    }

    /// <summary>Ship one PerfProbe window — best-effort, off the frame path.</summary>
    public async Task PostPerfAsync(object window)
    {
        try
        {
            var body = JsonSerializer.Serialize(window, Json);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            await Http().PostAsync(_base + "/api/perf", content).ConfigureAwait(false);
        }
        catch
        {
            /* perf telemetry is never allowed to fail loudly */
        }
    }

    private async Task SendBatch(List<EventEnvelope> batch)
    {
        try
        {
            if (SignalROk && _hub?.State == HubConnectionState.Connected)
            {
                await _hub.InvokeAsync("Events", batch).ConfigureAwait(false);
                return;
            }
            var body = JsonSerializer.Serialize(new EventBatch { Events = batch }, Json);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            await Http().PostAsync(_base + "/api/events", content).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SignalROk = false;
        }
        finally
        {
            Volatile.Write(ref _inFlight, 0);
        }
    }
}
