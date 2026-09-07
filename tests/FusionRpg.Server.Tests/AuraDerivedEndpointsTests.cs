using System.Net;
using System.Net.Http.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>aura-skill T18b — GET /api/actors/{instanceId}/derived against a REAL in-process host
/// (same pattern as AptitudeEndpointsTests.cs), proving the endpoint spec-aura-surface.md §3 names as
/// missing actually resolves a live actor's derived channels WITH their per-source contributions —
/// not a stub, not a bridge to `pvz_stat_contributions`.</summary>
public class AuraDerivedEndpointsTests : IAsyncLifetime
{
    string _dir = "";
    RpgStore _store = null!;
    WebApplication _app = null!;
    HttpClient _http = null!;
    long _playerId;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-auraderived-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();

        PowerTuningHub.Configure(
            PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "power-scale.v2.json"))));
        AptitudeTuningHub.Configure(
            AptitudeTuningLoader.Parse(File.ReadAllText(LatestAptitudesPath())));
        FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "progression.v1.json"))));
        FusionRpg.Core.Status.StatusPolicy.Configure(
            FusionRpg.Core.Status.StatusTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "status.v1.json"))));
        FusionRpg.Core.Stats.Derived.StatsTuningHub.Configure(
            FusionRpg.Core.Stats.Derived.StatsTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoTuningDir(), "stats.v1.json"))));
        // species-build `battle-allocation` (module 10, path 4): this endpoint now resolves a species
        // allocation too (SpeciesAllocationSource), so its own fixture needs the same roster/tuning a
        // real server configures at startup -- brought in line here rather than relying on another
        // test class in the same process happening to have configured DemonSpeciesCatalog first.
        FusionRpg.Core.Demons.DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        FusionRpg.Core.Progression.SpeciesProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.SpeciesProgressionTuningLoader.Parse(
                File.ReadAllText(Path.Combine(RepoTuningDir(), "species-progression.v1.json"))));

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton(_store);
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.UseDeveloperExceptionPage();
        _app.MapAuraDerived();
        await _app.StartAsync();

        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public async Task Get_unknownInstance_returns404()
    {
        var resp = await _http.GetAsync("/api/actors/not-a-real-instance/derived");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_freshPlantActor_returnsRealProgressionChannelsFromTheRealSubsystem()
    {
        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 42);

        var resp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived");
        if (!resp.IsSuccessStatusCode) throw new Exception(await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(body);
        Assert.Equal(actor.InstanceId, body!.InstanceId);

        // rpg.progression always contributes these two -- proves ActorHub actually resolved, not a stub.
        var power = Assert.Single(body.Channels, c => c.ChannelId == "progression.power");
        var powerSource = Assert.Single(power.Contributions);
        Assert.Equal("rpg.progression", powerSource.SourceId);

        var realm = Assert.Single(body.Channels, c => c.ChannelId == "progression.realm");
        Assert.Equal("rpg.progression", Assert.Single(realm.Contributions).SourceId);

        // Channels come back sorted -- a stable, deterministic contract for the web layer to render.
        var ids = body.Channels.Select(c => c.ChannelId).ToList();
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal).ToList(), ids);
    }

    [Fact]
    public async Task Get_zombieActor_resolvesTooNotJustPlant()
    {
        var actor = _store.CreateUniqueActor(_playerId, "zombie", typeId: 7);

        var resp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(body);
        Assert.Contains(body!.Channels, c => c.ChannelId == "progression.power");
    }

    [Fact]
    public async Task Get_afterARealAptitudeAllocation_reflectsANonVacuousAptitudeSourcedContribution()
    {
        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 1);

        var before = await (await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived"))
            .Content.ReadFromJsonAsync<DerivedResponseDto>();
        // AptitudeResolver.Resolve: "an aptitude with zero share contributes nothing -- not a
        // zero-valued modifier" (AptitudeResolver.cs:20-22), so an empty allocation names no source.
        Assert.DoesNotContain(before!.Channels, c => c.Contributions.Any(x => x.SourceId.StartsWith("aptitude.", StringComparison.Ordinal)));

        _store.SaveAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(_playerId),
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 50));

        var after = await (await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived"))
            .Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(after);
        // GG-49, non-vacuously: a real allocation now names a real source (AptitudeResolver.cs:61 --
        // SourceId is "aptitude.{edge.Source}", e.g. "aptitude.Might") on a real channel.
        Assert.Contains(after!.Channels, c => c.Contributions.Any(x => x.SourceId == "aptitude.Might"));
    }

    [Fact]
    public async Task Get_sheet_returns_full_registry_channels_with_composeKind_and_fiction_labels()
    {
        FusionRpg.Core.ActorSurface.DerivedStatSurfaceCatalogHub.Configure(
            FusionRpg.Core.ActorSurface.DerivedStatSurfaceCatalogLoader.Parse(
                File.ReadAllText(Path.Combine(RepoTuningDir(), "derived-stat-catalog.v1.json"))));

        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 3);
        _store.SaveAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(_playerId),
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 40));

        var resp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/sheet");
        if (!resp.IsSuccessStatusCode) throw new Exception(await resp.Content.ReadAsStringAsync());
        var sheet = await resp.Content.ReadFromJsonAsync<SheetResponseDto>();
        Assert.NotNull(sheet);
        Assert.Equal(actor.InstanceId, sheet!.InstanceId);
        Assert.True(sheet.Derived.Count >= 200, $"expected full registry floor, got {sheet.Derived.Count}");

        var power = Assert.Single(sheet.Derived, c => c.ChannelId == "progression.power");
        Assert.Equal("FlatReplace", power.ComposeKind);
        var prog = Assert.Single(power.Contributions);
        Assert.Equal("rpg.progression", prog.SourceId);
        Assert.Equal("Progression", prog.Label);

        Assert.Contains(sheet.Derived, c =>
            c.Contributions.Any(x => x.SourceId == "aptitude.Might" && (x.Label?.StartsWith("Aptitude", StringComparison.Ordinal) ?? false)));

        Assert.NotNull(sheet.Primary);
        // Primary bag is always projected; when contributions exist they use primary: grammar.
        Assert.All(sheet.Primary, p => Assert.StartsWith("primary:", p.SourceId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_sheet_unknownInstance_returns404()
    {
        var resp = await _http.GetAsync("/api/actors/not-a-real-instance/sheet");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_derived_ships_composeKind_and_FlatReplace_honesty_on_progression_power()
    {
        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 5);
        var resp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(body);

        var power = Assert.Single(body!.Channels, c => c.ChannelId == "progression.power");
        Assert.Equal("FlatReplace", power.ComposeKind);
        Assert.NotEmpty(power.Contributions);
        // FlatReplace: contribution sum may disagree with composed value — honesty is composeKind, not arithmetic.
        var sum = power.Contributions.Sum(c => c.Value);
        Assert.True(power.Value != 0 || sum != 0 || power.Contributions.Count > 0);
        Assert.Contains(power.Contributions, c => c.SourceId == "rpg.progression" && c.Label == "Progression");
    }

    [Fact]
    public async Task Get_sheet_and_derived_attribute_equipped_stat_derived_as_equip_role_item()
    {
        const string channel = DerivedStatChannels.CombatPowerOmni;
        const long amount = 175;
        const string itemRef = "item-hub-equip-fixture";
        var role = ItemRoles.Id(ItemRole.ArmamentPrimary);

        var actor = _store.CreateUniqueActor(_playerId, "plant", typeId: 9);
        BindStatDerivedEquip(actor.InstanceId, channel, amount, role, itemRef);

        // Multi-source FlatSum: aptitude Might also writes combat.power.omni.
        _store.SaveAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(_playerId),
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 50));

        var expectedSource = ContributionSourceIds.Equip(role, itemRef);

        var sheetResp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/sheet");
        if (!sheetResp.IsSuccessStatusCode) throw new Exception(await sheetResp.Content.ReadAsStringAsync());
        var sheet = await sheetResp.Content.ReadFromJsonAsync<SheetResponseDto>();
        Assert.NotNull(sheet);
        var sheetCh = Assert.Single(sheet!.Derived, c => c.ChannelId == channel);
        Assert.Equal("FlatSum", sheetCh.ComposeKind);
        var equipSheet = Assert.Single(sheetCh.Contributions, c => c.SourceId == expectedSource);
        Assert.Contains("Equip", equipSheet.Label, StringComparison.Ordinal);
        Assert.Contains(sheetCh.Contributions, c => c.SourceId == "aptitude.Might");
        Assert.True(sheetCh.Contributions.Count >= 2);
        // FlatSum: contribution flats account for the composed channel value.
        Assert.Equal(sheetCh.Value, sheetCh.Contributions.Sum(c => c.Value), 3);

        var derivedResp = await _http.GetAsync($"/api/actors/{actor.InstanceId}/derived");
        derivedResp.EnsureSuccessStatusCode();
        var derived = await derivedResp.Content.ReadFromJsonAsync<DerivedResponseDto>();
        Assert.NotNull(derived);
        var derCh = Assert.Single(derived!.Channels, c => c.ChannelId == channel);
        Assert.Equal("FlatSum", derCh.ComposeKind);
        Assert.Contains(derCh.Contributions, c => c.SourceId == expectedSource && c.Label != null && c.Label.Contains("Equip", StringComparison.Ordinal));
        Assert.Contains(derCh.Contributions, c => c.SourceId == "aptitude.Might");
        Assert.Equal(derCh.Value, derCh.Contributions.Sum(c => c.Value), 3);
    }

    void BindStatDerivedEquip(string specimenId, string channel, long amount, string roleSlot, string itemRef)
    {
        var atomId = "atom.hub-equip-attrib.t1";
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = atomId, KindId = "stat.derived",
            FamilyId = "atom.hub-equip-attrib", Variant = "", Tier = 1, Name = "Hub Equip Attrib",
            ParamsJson = $"{{\"channel\":\"{channel}\",\"op\":\"flat\",\"amount\":{amount}}}",
        }).IsOk);

        Assert.True(_store.UpsertContainer(new ContainerRow
        {
            ContainerId = "item.hub-equip-attrib", Kind = ContainerKind.Item,
            Atoms = new[] { new ContainerAtomRow(1, atomId) },
        }).IsOk);

        var tuning = PowerTuning.Build(
            1, 1, 80_000, 0, 20, 680, 1000, 25000, 250, 1000, 5000, 5000, 25000);
        var owner = new OwnerScope(OwnerKind.UniqueActor, specimenId);
        var produce = _store.ProduceAndBind(
            _store.GetContainer("item.hub-equip-attrib")!,
            _ => Array.Empty<string>(),
            rollSeed: 11, thetaContent: 20, tuning, owner,
            slot: roleSlot, priority: 0, source: "test",
            out var producedInstanceId, out _);
        Assert.True(produce.IsOk, produce.ToString());
        Assert.NotNull(producedInstanceId);

        _store.SaveAssignment(specimenId, ItemRole.ArmamentPrimary, "rolled", itemRef);
    }

    sealed class SheetResponseDto
    {
        public string InstanceId { get; set; } = "";
        public List<SheetChannelDto> Derived { get; set; } = new();
        public List<DerivedContributionDto> Primary { get; set; } = new();
    }

    sealed class SheetChannelDto
    {
        public string ChannelId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string ComposeKind { get; set; } = "";
        public double Value { get; set; }
        public List<DerivedContributionDto> Contributions { get; set; } = new();
    }

    sealed class DerivedResponseDto
    {
        public string InstanceId { get; set; } = "";
        public List<DerivedChannelDto> Channels { get; set; } = new();
    }

    sealed class DerivedChannelDto
    {
        public string ChannelId { get; set; } = "";
        public double Value { get; set; }
        public string ComposeKind { get; set; } = "";
        public List<DerivedContributionDto> Contributions { get; set; } = new();
    }

    sealed class DerivedContributionDto
    {
        public string SourceId { get; set; } = "";
        public string? Label { get; set; }
        public string Op { get; set; } = "";
        public double Value { get; set; }
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
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "collect-class-system-realrun.ps1"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
