using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FusionRpg.Core.Creatures;
using FusionRpg.Core.Creatures.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using FusionRpg.Data.Sqlite;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>aptitude-sheet AS-3.1 / AS-3.2 — <c>/api/aptitude-presets</c> against a real in-process host.</summary>
public class AptitudePresetEndpointsTests : IAsyncLifetime
{
    const int FumeshroomCreatureTypeId = 60007;

    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    long _playerId;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-aptpreset-ep-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();

        PowerTuningHub.Configure(
            PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "power-scale.v2.json"))));
        AptitudeTuningHub.Configure(
            AptitudeTuningLoader.Parse(File.ReadAllText(LatestAptitudesPath())));
        AptitudePresetTuningHub.Configure(
            AptitudePresetTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "aptitude-presets.v1.json"))));
        FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(
                File.ReadAllText(Path.Combine(RepoTuningDir(), "progression.v1.json"))));
        FusionRpg.Core.Progression.SpeciesProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.SpeciesProgressionTuningLoader.Parse(
                File.ReadAllText(Path.Combine(RepoTuningDir(), "species-progression.v1.json"))));
        CreatureSpeciesCatalog.ConfigureFromCompiledDefault();
        SpeciesBuildPlanCatalog.Configure(new Dictionary<string, IReadOnlyDictionary<string, long>>(StringComparer.Ordinal)
        {
            ["fumeshroom"] = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["Might"] = 500, ["Vigor"] = 300, ["Fortitude"] = 200
            }
        });
        SpeciesBuildTuningHub.Configure(new SpeciesBuildTuning(
            SchemaVersion: 1, Version: 1,
            ParityFloorPermille: 50, ParityCeilingPermille: 200,
            LeanMinPermille: 350, LeanMaxPermille: 600,
            CrowdingFactor: 633, SecondarySharePermille: 300,
            MaxAptitudesPerSpecies: 5, MinAptitudesPerSpecies: 2,
            RespecBasePrice: 50, RespecEscalationPermille: 500, RespecDecayDays: 3));

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(_store);
        builder.Services.AddSingleton<IPowerIndexProvider>(sp =>
            new FusionRpg.Server.Power.ServerPowerIndexProvider(sp.GetRequiredService<RpgStore>(), PowerTuningHub.Tuning));
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapAptitudes();
        _app.MapAptitudePresets();
        _app.MapSpeciesBuild();
        await _app.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp */ }
    }

    static List<object> EvenRows()
    {
        var list = new List<object>();
        var i = 0;
        foreach (var apt in AptitudeCatalog.All)
        {
            var pm = 83L + (i < 4 ? 1L : 0L);
            list.Add(new { aptitudeId = apt.Id, targetPermille = pm });
            i++;
        }
        return list;
    }

    /// <summary>All 1000‰ on Might — survives low commander budgets where an even split floors to zero.</summary>
    static List<object> MightOnlyRows()
    {
        return AptitudeCatalog.All.Select(apt => (object)new
        {
            aptitudeId = apt.Id,
            targetPermille = apt.Id == "Might" ? 1000L : 0L
        }).ToList();
    }

    async Task<string> CreateEvenPresetAsync(string name = "Even")
    {
        var resp = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name,
            kind = "player",
            rows = EvenRows()
        });
        var text = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, text);
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("presetId").GetString()!;
    }

    async Task<string> CreateMightPresetAsync(string name = "Might")
    {
        var resp = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name,
            kind = "player",
            rows = MightOnlyRows()
        });
        var text = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, text);
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.GetProperty("presetId").GetString()!;
    }

    [Fact]
    public async Task Post_rejects_sum_not_1000()
    {
        var rows = EvenRows();
        rows[0] = new { aptitudeId = "Might", targetPermille = 999L };
        var resp = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name = "Bad",
            rows
        });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("presets.targetPermille.sum", body);
    }

    [Fact]
    public async Task Materialize_returns_leftover_and_refuses_loGtHi()
    {
        var rows = EvenRows();
        rows[0] = new { aptitudeId = "Might", targetPermille = 84L, maxAbs = 5L };
        // fix sum: Might was 84 already in EvenRows for i=0 — leave others; EvenRows[0] is Might=84
        // Rebuild carefully: Might 84 with maxAbs 5
        var custom = new List<object>();
        var i = 0;
        foreach (var apt in AptitudeCatalog.All)
        {
            var pm = 83L + (i < 4 ? 1L : 0L);
            if (apt.Id == "Might")
                custom.Add(new { aptitudeId = apt.Id, targetPermille = pm, maxAbs = 5L });
            else
                custom.Add(new { aptitudeId = apt.Id, targetPermille = pm });
            i++;
        }
        var create = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name = "Clamp",
            rows = custom
        });
        create.EnsureSuccessStatusCode();
        var presetId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presetId").GetString()!;

        var mat = await _http.PostAsJsonAsync("/api/aptitude-presets/materialize", new
        {
            playerId = _playerId,
            presetId,
            budget = 1000L
        });
        mat.EnsureSuccessStatusCode();
        var matBody = await mat.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(matBody.GetProperty("leftover").GetInt64() > 0);
        Assert.Equal(5, matBody.GetProperty("shares").GetProperty("Might").GetInt64());

        var conflictRows = new List<object>();
        i = 0;
        foreach (var apt in AptitudeCatalog.All)
        {
            var pm = 83L + (i < 4 ? 1L : 0L);
            if (apt.Id == "Might")
                conflictRows.Add(new { aptitudeId = apt.Id, targetPermille = pm, minAbs = 50L, maxAbs = 10L });
            else
                conflictRows.Add(new { aptitudeId = apt.Id, targetPermille = pm });
            i++;
        }
        var conflictCreate = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name = "Conflict",
            rows = conflictRows
        });
        conflictCreate.EnsureSuccessStatusCode();
        var conflictId = (await conflictCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presetId").GetString()!;
        var bad = await _http.PostAsJsonAsync("/api/aptitude-presets/materialize", new
        {
            presetId = conflictId,
            budget = 1000L
        });
        Assert.Equal(HttpStatusCode.Conflict, bad.StatusCode);
        Assert.Contains("presets.materialize.loGtHi", await bad.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Favour_returns_permille_or_empty()
    {
        var known = await _http.GetAsync("/api/aptitude-presets/favour/fumeshroom");
        known.EnsureSuccessStatusCode();
        var knownBody = await known.Content.ReadFromJsonAsync<JsonElement>();
        var shares = knownBody.GetProperty("sharesPermille");
        Assert.Equal(500, shares.GetProperty("Might").GetInt64());
        Assert.Equal(300, shares.GetProperty("Vigor").GetInt64());
        Assert.Equal(200, shares.GetProperty("Fortitude").GetInt64());

        var empty = await _http.GetAsync("/api/aptitude-presets/favour/not-a-planned-species");
        empty.EnsureSuccessStatusCode();
        var emptyBody = await empty.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Object, emptyBody.GetProperty("sharesPermille").ValueKind);
        Assert.Empty(emptyBody.GetProperty("sharesPermille").EnumerateObject().ToArray());
    }

    [Fact]
    public async Task SoftMax_create_past_cap_returns_conflict()
    {
        AptitudePresetTuningHub.Configure(new AptitudePresetTuning(1, 1, SoftMaxPresets: 1, DefaultRowAbsMax: 1000));
        var first = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name = "One",
            rows = EvenRows()
        });
        first.EnsureSuccessStatusCode();
        var second = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name = "Two",
            rows = EvenRows()
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("presets.softMax", await second.Content.ReadAsStringAsync());
        // restore shipped soft max for sibling tests in this class (new fixture per class instance)
        AptitudePresetTuningHub.Configure(
            AptitudePresetTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "aptitude-presets.v1.json"))));
    }

    [Fact]
    public async Task Activate_commander_sets_active_and_allocation()
    {
        var presetId = await CreateMightPresetAsync();
        var resp = await _http.PostAsJsonAsync("/api/aptitude-presets/activate", new
        {
            playerId = _playerId,
            presetId,
            scope = "commander",
            scopeKey = ""
        });
        var text = await resp.Content.ReadAsStringAsync();
        Assert.True(resp.IsSuccessStatusCode, text);

        var active = await _http.GetAsync($"/api/aptitude-presets/active?playerId={_playerId}&scope=commander&scopeKey=");
        active.EnsureSuccessStatusCode();
        var activeBody = await active.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(presetId, activeBody.GetProperty("presetId").GetString());

        var apt = await (await _http.GetAsync($"/api/aptitudes/{_playerId}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(apt.GetProperty("spent").GetInt64() > 0);
        Assert.True(apt.GetProperty("shares").GetProperty("Might").GetInt64() > 0);
    }

    [Fact]
    public async Task Activate_unique_round_trips()
    {
        var actor = _store.EnsureUniqueActorForAudit(_playerId, "ua-preset-act", "plant", typeId: 1, level: 10);
        var presetId = await CreateEvenPresetAsync("UniqueEven");
        var resp = await _http.PostAsJsonAsync("/api/aptitude-presets/activate", new
        {
            playerId = _playerId,
            presetId,
            scope = "unique",
            scopeKey = actor.InstanceId
        });
        resp.EnsureSuccessStatusCode();
        var unique = await (await _http.GetAsync($"/api/aptitudes/unique/{actor.InstanceId}"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(unique.GetProperty("spent").GetInt64() > 0);
    }

    [Fact]
    public async Task Activate_species_uses_priced_respec_and_rolls_back_on_insufficient_souls()
    {
        SeedSpeciesLevel(_playerId, FumeshroomCreatureTypeId, level: 21, "fumeshroom");
        var presetId = await CreateEvenPresetAsync("SpeciesEven");

        // First activate is free (first override) — establish an override so the next is priced.
        var first = await _http.PostAsJsonAsync("/api/aptitude-presets/activate", new
        {
            playerId = _playerId,
            presetId,
            scope = "species",
            scopeKey = "fumeshroom",
            correlationId = "preset-act-1"
        });
        var firstText = await first.Content.ReadAsStringAsync();
        Assert.True(first.IsSuccessStatusCode, firstText);

        // Build a different preset so the second activate is a priced replacement.
        var rows2 = EvenRows();
        // Flip remainder onto last four so shares differ from first even split.
        rows2 = new List<object>();
        var i = 0;
        foreach (var apt in AptitudeCatalog.All)
        {
            var pm = 83L + (i >= 8 ? 1L : 0L);
            rows2.Add(new { aptitudeId = apt.Id, targetPermille = pm });
            i++;
        }
        var create2 = await _http.PostAsJsonAsync("/api/aptitude-presets", new
        {
            playerId = _playerId,
            name = "SpeciesAlt",
            rows = rows2
        });
        create2.EnsureSuccessStatusCode();
        var preset2 = (await create2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("presetId").GetString()!;

        // Drain souls so priced respec fails.
        var balance = _store.GetSoulBalance(_playerId).Balance;
        if (balance > 0)
            _store.TrySpendSouls(_playerId, balance, "test.drain", "drain-" + Guid.NewGuid().ToString("N"));

        var beforeActive = _store.GetAptitudePresetActive(_playerId, "species", "fumeshroom");
        Assert.Equal(presetId, beforeActive!.PresetId);
        var beforeAlloc = _store.LoadAllocation(AllocationScope.CreatureType,
            SpeciesAllocation.ScopeKey(_playerId, "fumeshroom"));

        var second = await _http.PostAsJsonAsync("/api/aptitude-presets/activate", new
        {
            playerId = _playerId,
            presetId = preset2,
            scope = "species",
            scopeKey = "fumeshroom",
            correlationId = "preset-act-2"
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("souls.insufficient", await second.Content.ReadAsStringAsync());

        var afterActive = _store.GetAptitudePresetActive(_playerId, "species", "fumeshroom");
        Assert.Equal(presetId, afterActive!.PresetId); // unchanged — no half-active
        var afterAlloc = _store.LoadAllocation(AllocationScope.CreatureType,
            SpeciesAllocation.ScopeKey(_playerId, "fumeshroom"));
        Assert.Equal(beforeAlloc.TotalForScope(AllocationScope.CreatureType),
            afterAlloc.TotalForScope(AllocationScope.CreatureType));
    }

    void SeedSpeciesLevel(long playerId, int creatureTypeId, long level, string scopeKey)
    {
        using var db = SqliteConnectionFactory.Open(_store.HotPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_actor_progression(
              player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc, scope_key)
            VALUES ($p, 'species', $tid, $lvl, 0, $lvl, 0, 0, $now, $sk);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$tid", creatureTypeId);
        cmd.Parameters.AddWithValue("$lvl", level);
        cmd.Parameters.AddWithValue("$sk", scopeKey);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    static string RepoTuningDir() => Path.Combine(FindRepoRoot(), "data", "tuning");

    static string LatestAptitudesPath()
    {
        var dir = RepoTuningDir();
        var best = Directory.EnumerateFiles(dir, "aptitudes.v*.json")
            .Select(Path.GetFileName)
            .Select(n => (Name: n!, Match: System.Text.RegularExpressions.Regex.Match(n!, @"^aptitudes\.v(\d+)\.json$")))
            .Where(x => x.Match.Success)
            .OrderByDescending(x => int.Parse(x.Match.Groups[1].Value))
            .First();
        return Path.Combine(dir, best.Name);
    }

    static int GetFreeTcpPort()
    {
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "collect-class-system-realrun.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
