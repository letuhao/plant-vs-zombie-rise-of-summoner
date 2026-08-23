using System.Text.Json;
using FusionRpg.CheatCore;
using FusionRpg.Contracts;
using FusionRpg.Core;
using FusionRpg.Data;
using FusionRpg.Data.Abstractions;
using FusionRpg.Server;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
var urls = Environment.GetEnvironmentVariable("FUSIONRPG_URLS");
var listenUrl = string.IsNullOrWhiteSpace(urls) ? "http://127.0.0.1:5088" : urls.Trim();
builder.WebHost.UseUrls(listenUrl);
if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FUSIONRPG_DATA")))
    builder.WebHost.UseContentRoot(AppContext.BaseDirectory);

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(origin =>
         origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
         || origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase))
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));
builder.Services.AddSignalR().AddJsonProtocol();
// Default: {ServerExeDir}/data/{rpg-hot,rpg-media}.sqlite — override with FUSIONRPG_DATA only for tests/special runs.
var dataDir = Environment.GetEnvironmentVariable("FUSIONRPG_DATA");
if (string.IsNullOrWhiteSpace(dataDir))
    dataDir = Path.Combine(AppContext.BaseDirectory, "data");
Directory.CreateDirectory(dataDir);
Console.WriteLine($"[data] {dataDir} (hot=rpg-hot.sqlite media=rpg-media.sqlite)");
builder.Services.AddSingleton(new RpgStore(dataDir));
builder.Services.AddSingleton(sp => new TypeIconStore(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<IColdArchiveWriter>(sp => new ColdArchiveWriter(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<IColdArchiveCatalog>(sp => new ColdArchiveCatalog(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<IHotCompactor>(sp => new HotCompactor(sp.GetRequiredService<RpgStore>()));
builder.Services.AddSingleton<CompactionWorker>();
builder.Services.AddSingleton<EventIngest>();
builder.Services.AddSingleton<InjectorCommandInbox>();
builder.Services.AddSingleton<FusionRpg.Core.Effects.EffectGrantSession>();
builder.Services.AddSingleton<FusionRpg.Core.Effects.SimEffectHost>();
builder.Services.AddSingleton<UniqueActorService>();
builder.Services.AddSingleton<PerfWindowBuffer>();
builder.Services.AddSingleton<WebMatchService>();
builder.Services.AddSingleton<ExpeditionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<EventIngest>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<CompactionWorker>());
builder.Services.AddHostedService<UniqueActorDeployWatchdog>();
if (SimFlags.Enabled)
{
    builder.Services.AddSingleton<SimService>();
    builder.Services.AddHostedService<SimHeartbeatHost>();
}

var app = builder.Build();
app.Services.GetRequiredService<RpgStore>().Init();
// E20: without this, ElementTable/PowerTables.Current never move off their shipped code copy, and
// an imported roster or coefficient row changes the content hash and nothing else (completeness
// audit A2). A store with nothing imported behaves exactly as before.
app.Services.GetRequiredService<RpgStore>().LoadContentIntoRuntime();
// Fail fast on demon content errors: the catalogs are lazy, and a bad species surfacing on the
// first request would permanently poison WaveCatalog's static initializer (review I6).
_ = FusionRpg.Core.Demons.DemonSpeciesCatalog.All;
_ = FusionRpg.Core.Battle.WaveCatalog.All;
_ = FusionRpg.Core.Demons.Fusion.DemonRecipeCatalog.All; // recipe graph must fail fast, not poison lazily
// Boot sweep: web matches logged but never ingested (crash window) re-resolve deterministically.
var sweptMatches = app.Services.GetRequiredService<WebMatchService>().SweepUnresolved();
if (sweptMatches > 0)
    Console.WriteLine($"[web-match] boot sweep re-ingested {sweptMatches} logged matches");
var portraits = app.Services.GetRequiredService<TypeIconStore>().BackfillPortraitsFromDumps();
if (portraits > 0)
    Console.WriteLine($"[icons] backfilled {portraits} portraits from dump layer 'image'");
// C1 (completeness-audit.md): entity: bindings are never durable — IL2CPP reuses the pointer, so
// one surviving a restart would attach to whatever object takes its address next. A fresh boot is
// exactly the moment every entity: binding from the previous process is guaranteed stale, whether
// the last shutdown was clean or a crash. A no-op today (nothing binds one yet — completeness-audit
// A4), and the cheap place for this to already be correct once something does.
var clearedBindings = app.Services.GetRequiredService<RpgStore>().ClearSessionScopedBindings();
if (clearedBindings > 0)
    Console.WriteLine($"[atoms] boot sweep cleared {clearedBindings} stale session-scoped binding(s)");
var orphanInstances = app.Services.GetRequiredService<RpgStore>().CountOrphanInstances();
if (orphanInstances > 0)
    Console.WriteLine($"[atoms] {orphanInstances} orphan instance(s) remain after the boot sweep");
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapStorageEndpoints();
app.MapUniqueActors();
app.MapDemons();
app.MapSouls();
app.MapExpeditions();
app.MapFusion();
app.MapPatron();
app.MapContracts();
app.MapWorld();
PatronEndpoints.RefreshRuntimeState(app.Services.GetRequiredService<RpgStore>()); // SIM plugins read it

app.MapGet("/health", (RpgStore store, EventIngest ingest) => ingest.Decorate(store.ToHealth(SimFlags.Enabled)));

app.MapGet("/api/players", (RpgStore store) => new PlayersListDto
{
    Items = store.ListPlayers(),
    CurrentPlayerId = store.GetCurrentPlayerId()
});
app.MapPost("/api/players", (CreatePlayerRequest body, RpgStore store) =>
{
    if (string.IsNullOrWhiteSpace(body.Name))
        return Results.BadRequest(new { error = "name required" });
    return Results.Ok(store.CreatePlayer(body.Name));
});
app.MapGet("/api/players/current", (RpgStore store) =>
{
    var p = store.GetCurrentPlayer();
    return p is null ? Results.NotFound() : Results.Ok(p);
});
app.MapPut("/api/players/current", (SelectPlayerRequest body, RpgStore store) =>
    store.SetCurrentPlayer(body.Id) ? Results.Ok(store.GetCurrentPlayer()) : Results.NotFound());

// --- PvzStats: player-bound modifier SSOT + derived sheet ---
app.MapGet("/api/pvz-stats/current", (RpgStore store) =>
{
    var id = store.GetCurrentPlayerId();
    var sheet = store.GetPvzStatsSheet(id);
    return sheet is null ? Results.NotFound() : Results.Ok(sheet);
});
app.MapGet("/api/pvz-stats/{playerId:long}", (long playerId, RpgStore store) =>
{
    var sheet = store.GetPvzStatsSheet(playerId);
    return sheet is null ? Results.NotFound() : Results.Ok(sheet);
});
app.MapGet("/api/pvz-stats/{playerId:long}/channels/{channel}", (long playerId, string channel, RpgStore store) =>
{
    var detail = store.GetPvzStatsChannel(playerId, channel);
    return detail is null ? Results.NotFound() : Results.Ok(detail);
});
app.MapGet("/api/pvz-stats/{playerId:long}/modifiers", (long playerId, RpgStore store) =>
{
    var mods = store.GetPvzStatsModifiers(playerId);
    return mods is null ? Results.NotFound() : Results.Ok(mods);
});
app.MapPost("/api/pvz-stats/{playerId:long}/modifiers/upsert", async (long playerId, PvzStatModifierDto body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    try
    {
        var sheet = store.UpsertPvzStatModifier(playerId, body);
        await BroadcastPvzStats(store, hub, playerId, sheet.Revision);
        await SendInjectorCommand(hub, inbox, new CommandDto { Name = "pvz.stats.reload", Payload = new { playerId, revision = sheet.Revision } });
        return Results.Ok(sheet);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});
app.MapPost("/api/pvz-stats/{playerId:long}/modifiers/withdraw", async (long playerId, JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    try
    {
        var sourceKind = body.TryGetProperty("sourceKind", out var sk) ? sk.GetString() : null;
        var sourceId = body.TryGetProperty("sourceId", out var sid) ? sid.GetString() : null;
        var channel = body.TryGetProperty("channel", out var ch) ? ch.GetString() : null;
        var op = body.TryGetProperty("op", out var opEl) ? opEl.GetString() : null;
        var pluginId = body.TryGetProperty("pluginId", out var pl) ? pl.GetString() : null;
        var sheet = store.WithdrawPvzStatModifiers(playerId, sourceKind, sourceId, channel, op, pluginId);
        await BroadcastPvzStats(store, hub, playerId, sheet.Revision);
        await SendInjectorCommand(hub, inbox, new CommandDto { Name = "pvz.stats.reload", Payload = new { playerId, revision = sheet.Revision } });
        return Results.Ok(sheet);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});
app.MapPost("/api/pvz-stats/{playerId:long}/modifiers/reset", async (long playerId, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    try
    {
        var sheet = store.ResetPvzStats(playerId);
        await BroadcastPvzStats(store, hub, playerId, sheet.Revision);
        await SendInjectorCommand(hub, inbox, new CommandDto { Name = "pvz.stats.reload", Payload = new { playerId, revision = sheet.Revision } });
        return Results.Ok(sheet);
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

// --- PvzActivity: append-only facts + rollup cache ---
app.MapGet("/api/pvz-activity/current", (RpgStore store) =>
{
    var id = store.GetCurrentPlayerId();
    var rollup = store.GetPvzActivityRollup(id);
    return rollup is null ? Results.NotFound() : Results.Ok(rollup);
});
app.MapGet("/api/pvz-activity/{playerId:long}", (long playerId, RpgStore store) =>
{
    var rollup = store.GetPvzActivityRollup(playerId);
    return rollup is null ? Results.NotFound() : Results.Ok(rollup);
});
app.MapGet("/api/pvz-activity/{playerId:long}/facts", (long playerId, RpgStore store, string? kind, long? runId, int limit = 100) =>
{
    var page = store.ListPvzActivityFacts(playerId, kind, runId, limit);
    return page is null ? Results.NotFound() : Results.Ok(page);
});
app.MapPost("/api/pvz-activity/{playerId:long}/facts/append", async (long playerId, PvzActivityAppendRequest body, RpgStore store, IHubContext<RpgHub> hub) =>
{
    try
    {
        var result = store.AppendPvzActivityFact(playerId, body);
        await BroadcastPvzActivity(store, hub, playerId, result.Rollup.Revision);
        foreach (var d in result.Progression)
            await BroadcastRpgProgression(hub, d.PlayerId, d.Kind, d.TypeId, d.Revision);
        return Results.Ok(result.Rollup);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

// --- RpgProgression ---
app.MapGet("/api/rpg/progression/{playerId:long}/summary", (long playerId, RpgStore store) =>
{
    var s = store.GetRpgProgressionSummary(playerId);
    return s is null ? Results.NotFound() : Results.Ok(s);
});
app.MapGet("/api/rpg/progression/{playerId:long}/stats", (long playerId, RpgStore store) =>
{
    var s = store.GetRpgProgressionStats(playerId);
    return s is null ? Results.NotFound() : Results.Ok(s);
});
app.MapGet("/api/rpg/progression/{playerId:long}", (long playerId, RpgStore store, string? kind, string sort = "level", int limit = 200, int offset = 0) =>
{
    var list = store.ListRpgProgression(playerId, kind, sort, limit, offset);
    return list is null ? Results.NotFound() : Results.Ok(list);
});
app.MapGet("/api/rpg/progression/{playerId:long}/ledger", (long playerId, RpgStore store, string? kind, int? typeId, string? reason, int limit = 100, long? afterId = null) =>
{
    var page = store.ListRpgXpLedger(playerId, kind, typeId, reason, limit, afterId);
    return page is null ? Results.NotFound() : Results.Ok(page);
});
app.MapGet("/api/rpg/progression/{playerId:long}/{kind}/{typeId:int}", (long playerId, string kind, int typeId, RpgStore store) =>
{
    var row = store.GetRpgActor(playerId, kind, typeId);
    return row is null ? Results.NotFound() : Results.Ok(row);
});
app.MapPost("/api/rpg/progression/{playerId:long}/{kind}/{typeId:int}/clear-demotion", async (long playerId, string kind, int typeId, RpgStore store, IHubContext<RpgHub> hub) =>
{
    var row = store.ClearRpgDemotion(playerId, kind, typeId);
    if (row is null) return Results.NotFound();
    await BroadcastRpgProgression(hub, playerId, kind, typeId, row.Revision);
    return Results.Ok(row);
});

// --- PvzIntent: game write commands ---
app.MapPost("/api/pvz-intent/spawn-extra", async (PvzSpawnExtraRequest body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox, HttpContext http) =>
{
    var playerId = body.PlayerId ?? store.GetCurrentPlayerId();
    if (!store.PlayerExists(playerId)) return Results.NotFound();
    var correlationId = string.IsNullOrWhiteSpace(body.CorrelationId)
        ? Guid.NewGuid().ToString("N")
        : body.CorrelationId.Trim();
    var reason = string.IsNullOrWhiteSpace(body.Reason) ? "extra" : body.Reason.Trim();
    var side = string.IsNullOrWhiteSpace(body.Side) ? "zombie" : body.Side.Trim().ToLowerInvariant();
    try
    {
        var (rollup, inserted) = store.RecordExtraSpawnIntent(playerId, correlationId, body.TypeId, reason, side);
        if (inserted)
            await BroadcastPvzActivity(store, hub, playerId, rollup.Revision);

        if (inserted)
        {
            var payload = new
            {
                typeId = body.TypeId,
                col = body.Col,
                row = body.Row,
                reason,
                correlationId,
                side,
                playerId,
                source = "extra"
            };
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Id = correlationId,
                Name = "pvz.spawn.extra",
                Payload = payload
            });

            // No live injector: drive sim so CI can prove source=extra spawn without Unity.
            if (!store.LiveInjector && SimFlags.Enabled)
            {
                var sim = http.RequestServices.GetService<SimService>();
                if (sim is not null)
                {
                    await sim.RunAsync(stats => sim.Engine.SpawnZombie(stats, new SimSpawnZombieRequest
                    {
                        Type = body.TypeId,
                        Source = "extra",
                        Ptr = "extra-" + correlationId
                    }));
                }
            }
        }

        return Results.Ok(new { ok = true, correlationId, inserted, rollup });
    }
    catch (InvalidOperationException)
    {
        return Results.NotFound();
    }
});

app.MapGet("/api/stats", (RpgStore store) => store.GetStats());
app.MapPut("/api/stats", async (StatsConfig body, RpgStore store, IHubContext<RpgHub> hub) =>
{
    store.PutStats(body);
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("StatsUpdated", body);
    return Results.Ok(body);
});

app.MapPost("/api/events", (JsonElement body, EventIngest ingest) =>
{
    var accepted = 0;
    var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("events", out var arr) && arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
            accepted += AcceptOne(item, ingest, json);
    }
    else
    {
        accepted += AcceptOne(body, ingest, json);
    }
    return Results.Ok(new { accepted });
});

app.MapGet("/api/events", (RpgStore store, int limit = 100, long afterId = 0, long? playerId = null) =>
    new { items = store.ListEvents(limit, afterId, playerId) });

app.MapGet("/api/runs", (RpgStore store, long? playerId) => new { items = store.ListRuns(playerId) });
app.MapGet("/api/types", (RpgStore store, string? side) => new { items = store.ListTypes(side) });

app.MapGet("/api/icons/dump", (TypeIconStore icons, string? side) =>
    Results.Ok(new { items = icons.ListDumps(side) }));

app.MapGet("/api/icons/dump/{side}/{typeId:int}", (string side, int typeId, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var dump = icons.GetDump(side, typeId);
    return dump is null ? Results.NotFound() : Results.Ok(dump);
});

app.MapGet("/api/icons/dump/{side}/{typeId:int}/layer/{layer}", (string side, int typeId, string layer, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var png = icons.GetLayerPng(side, typeId, layer);
    return png is null ? Results.NotFound() : Results.File(png, "image/png");
});

app.MapPut("/api/icons/dump/{side}/{typeId:int}", async (string side, int typeId, HttpRequest req, TypeIconStore icons, IHubContext<RpgHub> hub) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest(new { error = "bad side/typeId" });
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        var (created, layerCount, url, portraitSet) = await icons.SaveDumpAsync(side, typeId, doc.RootElement);
        await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("TypeIconUpdated", new
        {
            side = icons.NormalizeSide(side),
            typeId,
            dump = true,
            created,
            layerCount,
            portraitSet,
            url
        });
        return Results.Ok(new
        {
            created,
            layerCount,
            portraitSet,
            url,
            side = icons.NormalizeSide(side),
            typeId,
            composedUrl = portraitSet ? $"/api/icons/{icons.NormalizeSide(side)}/{typeId}.png" : null
        });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/icons/{side}/{typeId:int}.png", (string side, int typeId, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var png = icons.GetComposedPng(side, typeId);
    return png is null ? Results.NotFound() : Results.File(png, "image/png");
});

app.MapGet("/api/icons/{side}/{typeId:int}", (string side, int typeId, TypeIconStore icons) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var composed = icons.GetComposedPng(side, typeId);
    if (composed != null)
        return Results.Ok(new { side = icons.NormalizeSide(side), typeId, url = $"/api/icons/{icons.NormalizeSide(side)}/{typeId}.png", composed = true });
    var dump = icons.GetDump(side, typeId);
    if (dump is null) return Results.NotFound();
    return Results.Ok(new { side = icons.NormalizeSide(side), typeId, dump = true, layers = dump.Layers.Count, url = $"/api/icons/dump/{icons.NormalizeSide(side)}/{typeId}" });
});

app.MapGet("/api/recipes", (RpgStore store) => new { items = store.ListRecipes() });

app.MapGet("/api/almanac/dump", (RpgStore store, string? side) =>
    Results.Ok(new { items = store.ListAlmanacTextDumps(side) }));

app.MapGet("/api/almanac/dump/{side}/{typeId:int}", (string side, int typeId, RpgStore store) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var dump = store.GetAlmanacTextDump(side, typeId);
    return dump is null ? Results.NotFound() : Results.Ok(dump);
});

app.MapPut("/api/almanac/dump/{side}/{typeId:int}", async (string side, int typeId, HttpRequest req, RpgStore store, IHubContext<RpgHub> hub) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest(new { error = "bad side/typeId" });
    try
    {
        using var doc = await JsonDocument.ParseAsync(req.Body);
        if (!doc.RootElement.TryGetProperty("fields", out var fieldsEl) || fieldsEl.ValueKind != JsonValueKind.Object)
            return Results.BadRequest(new { error = "fields required" });

        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in fieldsEl.EnumerateObject())
            fields[p.Name] = p.Value.ValueKind == JsonValueKind.Null ? null : p.Value.GetString();

        Dictionary<string, string>? sources = null;
        if (doc.RootElement.TryGetProperty("sources", out var srcEl) && srcEl.ValueKind == JsonValueKind.Object)
        {
            sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in srcEl.EnumerateObject())
            {
                var v = p.Value.GetString();
                if (!string.IsNullOrWhiteSpace(v)) sources[p.Name] = v!;
            }
        }

        if (fields.Count == 0) return Results.BadRequest(new { error = "empty fields" });
        var created = !store.HasAlmanacTextDump(side, typeId);
        store.UpsertAlmanacTextDump(side, typeId, fields, sources);
        var sNorm = side.Trim().ToLowerInvariant();
        await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("AlmanacTextUpdated", new
        {
            side = sNorm,
            typeId,
            created,
            fieldCount = fields.Count
        });
        return Results.Ok(new { created, fieldCount = fields.Count, side = sNorm, typeId, url = $"/api/almanac/dump/{sNorm}/{typeId}" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});
app.MapGet("/api/almanac/seed", (RpgStore store, string? side) =>
    Results.Ok(new { contractVersion = RpgStore.AlmanacSeedContractVersion, items = store.ListAlmanacSeed(side) }));

app.MapGet("/api/almanac/seed/{side}/{typeId:int}", (string side, int typeId, RpgStore store) =>
{
    if (!TypeIconStore.IsValidSide(side) || typeId < 0) return Results.BadRequest();
    var dto = store.GetAlmanacSeed(side, typeId);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
});

app.MapPost("/api/almanac/seed/rebuild", (RpgStore store) => Results.Ok(store.RebuildAlmanacSeed()));

app.MapPost("/api/almanac/seed/enrich", (RpgStore store) =>
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data", "seed", "external-reference", "almanac-enrichment", "pvz-fusion-almanac-3.6.1.json")))
        dir = dir.Parent;
    if (dir is null)
        return Results.Problem("enrichment export file not found (data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json)", statusCode: 404);

    var path = Path.Combine(dir.FullName, "data", "seed", "external-reference", "almanac-enrichment", "pvz-fusion-almanac-3.6.1.json");
    List<AlmanacEnrichmentImportRow>? rows;
    try
    {
        var json = File.ReadAllText(path);
        rows = JsonSerializer.Deserialize<List<AlmanacEnrichmentImportRow>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
    catch (Exception ex)
    {
        return Results.Problem("failed to read/parse enrichment export: " + ex.Message, statusCode: 400);
    }
    if (rows is null) return Results.Problem("enrichment export deserialized to null", statusCode: 400);

    var summary = store.ImportAlmanacEnrichment(rows, "pvz-fusion-almanac-3.6.1");
    return Results.Ok(new { matched = summary.Matched, unmatched = summary.Unmatched });
});

app.MapGet("/api/runs/{id:long}/spawns", (long id, RpgStore store) => new { items = store.ListSpawnStats(id) });
app.MapGet("/api/metrics", (RpgStore store) => new { items = store.ListMetrics() });
app.MapPost("/api/heartbeat", (HeartbeatDto? body, RpgStore store) =>
{
    store.Heartbeat(body?.Source);
    return Results.Ok(new { ok = true, source = store.Source });
});
app.MapPost("/api/metrics", (JsonElement body, RpgStore store) =>
{
    if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in arr.EnumerateArray())
        {
            var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (string.IsNullOrWhiteSpace(name)) continue;
            var value = item.TryGetProperty("value", out var v) && v.TryGetDouble(out var d) ? d : 0;
            store.UpsertMetric(name, value);
        }
    }
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/commands/reload-stats", async (RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var stats = store.GetStats();
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("StatsUpdated", stats);
    await SendInjectorCommand(hub, inbox, new CommandDto { Name = "reload-stats" });
    return Results.Ok(new { ok = true });
});

// Injector polls this when SignalR server→client Command delivery fails.
app.MapGet("/api/cheats/commands/pending", (InjectorCommandInbox inbox) =>
    Results.Ok(new { items = inbox.Drain(64), remaining = inbox.Count }));

app.MapGet("/api/cheats", (RpgStore store) =>
{
    var json = store.GetCheatsJson();
    if (string.IsNullOrWhiteSpace(json))
        return Results.Ok(new { menuEnabled = true, entries = Array.Empty<object>(), catalog = new { } });
    return Results.Content(json, "application/json");
});

app.MapPut("/api/cheats", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    store.PutCheatsJson(body.GetRawText());
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.set",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

// Injector telemetry only — catalog merge, never overwrite entries, never push CheatsUpdated to web.
// One-way cheats: web/server → injector → game (not game → FE).
app.MapPut("/api/cheats/mirror", (JsonElement body, RpgStore store) =>
{
    store.MergeCheatsCatalog(body);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/cheats/action", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var actionName = body.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
    if (actionName is "reset-all")
    {
        store.PutCheatsJson("""{"menuEnabled":false,"revision":0,"entries":[]}""");
        await BroadcastCheats(store, hub);
    }
    else if (actionName is "reset-group")
    {
        var prefix = body.TryGetProperty("prefix", out var p) ? p.GetString() ?? "" : "";
        var raw = store.GetCheatsJsonRaw() ?? """{"menuEnabled":false,"revision":0,"entries":[]}""";
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(raw);
            var dict = new Dictionary<string, object?>();
            long revision = 0;
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.NameEquals("entries")) continue;
                if (prop.NameEquals("revision") && prop.Value.TryGetInt64(out var r)) revision = r;
                else dict[prop.Name] = prop.Value.Clone();
            }
            var kept = new List<object>();
            if (doc.RootElement.TryGetProperty("entries", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(prefix) && id.StartsWith(prefix, StringComparison.Ordinal))
                        continue;
                    kept.Add(item.Clone());
                }
            }
            dict["entries"] = kept;
            dict["revision"] = revision + 1;
            dict["updatedAt"] = DateTime.UtcNow.ToString("o");
            dict["menuEnabled"] = false;
            // Already bumped once — do not bump again inside PutCheatsJson.
            store.PutCheatsJson(System.Text.Json.JsonSerializer.Serialize(dict), bumpRevision: false);
            await BroadcastCheats(store, hub);
        }
        catch { /* still send command */ }
    }
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.action",
        Payload = body
    });
    return Results.Ok(new { ok = true, queued = inbox.Count });
});

app.MapPost("/api/cheats/toggle", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    var enabled = body.TryGetProperty("enabled", out var en) && en.GetBoolean();
    store.MergeCheatField(id, enabled, null);
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.toggle",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/cheats/set-float", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    double? fv = body.TryGetProperty("value", out var v) && v.TryGetDouble(out var d) ? d : null;
    store.MergeCheatField(id, true, fv);
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.set-float",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/cheats/clear-field", async (JsonElement body, RpgStore store, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var id = body.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
    store.ClearCheatField(id);
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.clear-field",
        Payload = body
    });
    await BroadcastCheats(store, hub);
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/cheats/schema", () =>
    Results.Ok(new
    {
        fields = CheatSchema.All.Select(f => new
        {
            id = f.Id,
            role = f.Role.ToString(),
            kind = f.Kind,
            channel = f.Channel,
            op = f.Op,
            displayDefault = f.DisplayDefault,
            toggleDefault = f.ToggleDefault,
            groupPrefix = f.GroupPrefix
        }).ToList()
    }));

app.MapGet("/api/cheats/packs", () =>
    Results.Ok(new { items = ProbePacks.All.Select(ProbePacks.ToApiDto).ToList() }));

app.MapPost("/api/cheats/probe", async (JsonElement body, EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var packId = body.TryGetProperty("packId", out var pidEl) ? pidEl.GetString() ?? "" : "";
    var pack = ProbePacks.Get(packId);
    if (pack == null)
        return Results.NotFound(new { error = "unknown pack", packId });

    var probeId = body.TryGetProperty("probeId", out var pr) && pr.ValueKind == JsonValueKind.String
        ? pr.GetString()!
        : Guid.NewGuid().ToString("N");

    ingest.Enqueue(new EventEnvelope
    {
        T = DateTime.UtcNow.ToString("o"),
        Kind = "probe.start",
        Payload = new Dictionary<string, object>
        {
            ["probeId"] = probeId,
            ["packId"] = pack.Id,
            ["label"] = pack.Label,
            ["hint"] = pack.Hint,
            ["expectedKinds"] = pack.ExpectedKinds.ToList()
        }
    });

    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.probe-begin",
        Payload = new { probeId, packId = pack.Id }
    });

    foreach (var step in pack.Steps)
    {
        var corr = Guid.NewGuid().ToString("N");
        if (step.Op is "toggle")
        {
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Name = "cheat.toggle",
                Payload = new
                {
                    id = step.Id,
                    enabled = step.Enabled ?? true,
                    probeId,
                    packId = pack.Id,
                    correlationId = corr,
                    source = "pack"
                }
            });
        }
        else if (step.Op is "set-float")
        {
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Name = "cheat.set-float",
                Payload = new
                {
                    id = step.Id,
                    value = step.Value ?? 0,
                    probeId,
                    packId = pack.Id,
                    correlationId = corr,
                    source = "pack"
                }
            });
        }
        else if (step.Op is "action")
        {
            var payload = new Dictionary<string, object?>
            {
                ["action"] = step.Action,
                ["probeId"] = probeId,
                ["packId"] = pack.Id,
                ["correlationId"] = corr,
                ["source"] = "pack"
            };
            if (step.Which != null) payload["which"] = step.Which;
            if (step.Value is { } sv) payload["value"] = sv;
            if (step.Add is { } sa) payload["add"] = sa;
            await SendInjectorCommand(hub, inbox, new CommandDto
            {
                Name = "cheat.action",
                Payload = payload
            });
        }
    }

    return Results.Ok(new
    {
        probeId,
        packId = pack.Id,
        label = pack.Label,
        hint = pack.Hint,
        expectedKinds = pack.ExpectedKinds,
        steps = pack.Steps.Count
    });
});

app.MapPost("/api/cheats/probe/end", async (JsonElement body, EventIngest ingest, IHubContext<RpgHub> hub, InjectorCommandInbox inbox) =>
{
    var probeId = body.TryGetProperty("probeId", out var pr) ? pr.GetString() : null;
    var reason = body.TryGetProperty("reason", out var r) ? r.GetString() ?? "web" : "web";
    ingest.Enqueue(new EventEnvelope
    {
        T = DateTime.UtcNow.ToString("o"),
        Kind = "probe.end",
        Payload = new Dictionary<string, object?>
        {
            ["probeId"] = probeId ?? "",
            ["reason"] = reason
        }
    });
    await SendInjectorCommand(hub, inbox, new CommandDto
    {
        Name = "cheat.probe-end",
        Payload = new { probeId, reason }
    });
    return Results.Ok(new { ok = true, probeId, reason });
});

if (SimFlags.Enabled)
    app.MapSimAndProbes();

app.MapSimEffect();
// Debug endpoints are spawn/kill/mods control — loopback-only unless explicitly forced
// (2026-08-21 review I3: a 0.0.0.0 rebind must not expose them to the LAN unauthenticated).
var debugAllowed = listenUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                   || listenUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(Environment.GetEnvironmentVariable("FUSIONRPG_DEBUG_REMOTE"), "1", StringComparison.Ordinal);
if (debugAllowed)
    app.MapDebug();
else
    Console.WriteLine("[debug] endpoints disabled on non-loopback bind (set FUSIONRPG_DEBUG_REMOTE=1 to force)");
app.MapPerf();

app.MapHub<RpgHub>("/hub/rpg");
app.MapFallbackToFile("index.html");

try
{
    if (Environment.GetEnvironmentVariable("FUSIONRPG_NO_BROWSER") != "1")
    {
        var openUrl = listenUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "http://127.0.0.1:5088";
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(openUrl) { UseShellExecute = true });
    }
}
catch { /* no default browser */ }

app.Run();

static async Task SendInjectorCommand(IHubContext<RpgHub> hub, InjectorCommandInbox inbox, CommandDto cmd)
{
    inbox.Enqueue(cmd);
    try
    {
        await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("Command", cmd);
    }
    catch
    {
        /* inbox poll is the reliable path */
    }
}

static async Task BroadcastCheats(RpgStore store, IHubContext<RpgHub> hub)
{
    var json = store.GetCheatsJson();
    if (string.IsNullOrWhiteSpace(json)) return;
    try
    {
        using var doc = JsonDocument.Parse(json);
        await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("CheatsUpdated", doc.RootElement.Clone());
    }
    catch { /* ignore bad json */ }
}

static async Task BroadcastPvzStats(RpgStore store, IHubContext<RpgHub> hub, long playerId, long revision)
{
    var sheet = store.GetPvzStatsSheet(playerId);
    if (sheet is null) return;
    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PvzStatsUpdated", sheet);
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("PvzStatsUpdated", new { playerId, revision });
}

static async Task BroadcastPvzActivity(RpgStore store, IHubContext<RpgHub> hub, long playerId, long revision)
{
    var rollup = store.GetPvzActivityRollup(playerId);
    if (rollup is null) return;
    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PvzActivityUpdated", rollup);
    await hub.Clients.Group(RpgConstants.InjectorGroup).SendAsync("PvzActivityUpdated", new { playerId, revision });
}

static async Task BroadcastRpgProgression(IHubContext<RpgHub> hub, long playerId, string kind, int typeId, long revision)
{
    await hub.Clients.Group(RpgConstants.WebGroup).SendAsync("RpgProgressionUpdated", new { playerId, kind, typeId, revision });
}

static int AcceptOne(JsonElement item, EventIngest ingest, JsonSerializerOptions json)
{
    var env = item.Deserialize<EventEnvelope>(json);
    if (env is null || string.IsNullOrWhiteSpace(env.Kind)) return 0;
    return ingest.Enqueue(env);
}

public partial class Program { }
