using FusionRpg.Core.Delve.Domains;
using FusionRpg.Core.Delve.Encounter;
using FusionRpg.Core.Delve.Events;
using FusionRpg.Core.Delve.Roll;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Dungeon.Registry;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Data.Tests.Delve.Domains;

/// <summary>
/// D4.30 (party-dungeon-todo.md, 2026-09-07) — "run the six first-ship domains through the pipeline,"
/// the one piece of that task nobody had actually done yet even after its own content half (the six
/// real `data/seed/dungeon/domains/*.json` files) and D4.16's own real writer (`RpgStore.
/// ImportDungeonDomains`) both shipped: every existing preflight-bridge test (D4.17 rows 4/6/7's own
/// files) isolates ONE row at a time with the other nine stubbed to trivially pass, and D4.16's own
/// `DomainImportTests.cs` proves the WRITER's mechanics against a hand-built, fully-passing fixture,
/// never the real content. Nobody had composed the REAL bridges together and pushed the REAL six
/// domains through the REAL `ImportDungeonDomains` end to end. This file does exactly that.
///
/// <para><b>Rows 1/2 use real values; rows 4/6/7 use the real, already-shipped production bridges;
/// rows 3/5/8/10 are honestly stubbed to trivially pass — named here, not hidden, because those rows
/// have no real bridge built yet</b> (row 3's own `CellsLayoutCanPlace`/`CellsPaletteFills` needs a
/// layout→cell legality adapter nobody has built; row 5 needs the curio-verb-derivation rule this
/// program's own D4.17 row 5 entry names as having no stated rule anywhere; row 8 needs
/// `QuestPreflight`'s own domain-catalog-dependent sweep, D4.13's own still-open half; row 10 needs
/// `RungOffer.For`'s five real parameters including the still-blocked `ParentWorldTerms`). Stubbing
/// them to always-pass does not weaken this test's own real finding: row 6 is already independently
/// proven (D4.17 row 6, D4.31) to refuse every one of the six real domains on the threat-audit corpus's
/// own thinness, and rows 4-8 are concatenated by `DomainPreflight.Run` itself — ANY one of them
/// refusing is enough, so a domain that fails row 6 never needs rows 3/5/8/10 to be real to prove the
/// SAME true verdict `ImportDungeonDomains` reaches in production.</para>
/// </summary>
public class DomainRealPipelineTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public DomainRealPipelineTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-domain-real-pipeline-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "dungeon"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }

    static string DomainsDir() => Path.Combine(RepoRoot(), "data", "seed", "dungeon", "domains");
    static string RoomsDir() => Path.Combine(RepoRoot(), "data", "seed", "dungeon", "rooms");
    static string LayoutsDir() => Path.Combine(RepoRoot(), "data", "seed", "dungeon", "layouts");
    static string EventsDir() => Path.Combine(RepoRoot(), "data", "seed", "dungeon", "events");
    static string EncountersDir() => Path.Combine(RepoRoot(), "data", "seed", "dungeon", "encounters");
    static string SpeciesDir() => Path.Combine(RepoRoot(), "data", "seed", "demons", "species");

    static readonly DungeonTuning RealDungeonTuning = DungeonTuningHub.Tuning;
    static readonly EncounterTuning RealEncounterTuning = EncounterTuningHub.Tuning;
    static readonly DemonThreatTuning RealThreat =
        DemonThreatTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "demon-threat.v1.json")));
    static readonly AptitudeTuning RealAptitudes =
        AptitudeTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "aptitudes.v2.json")));
    static readonly PowerTuning RealPower =
        PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "power-scale.v2.json")));
    static readonly DemonShapeTuning RealShape =
        DemonShapeTuningLoader.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "demon-shape.v1.json")));

    static LayoutTemplateCatalog RealLayoutCatalog()
    {
        var rows = LayoutSeedFile.LoadAll(LayoutsDir());
        var bandDefs = new Dictionary<string, BandDef>
        {
            ["depthBand"] = new BandDef { BandName = "depthBand", Members = RealDungeonTuning.DepthBandRows.Keys.ToList() },
            ["widthBand"] = new BandDef { BandName = "widthBand", Members = RealDungeonTuning.WidthBandCols.Keys.ToList() },
            ["branchiness"] = new BandDef { BandName = "branchiness", Members = RealDungeonTuning.BranchinessPathWalks.Keys.ToList() },
            ["density"] = new BandDef
            {
                BandName = "density",
                Members = RealDungeonTuning.GateDensityPerRoomMilli.Keys
                    .Union(RealDungeonTuning.SecretDensityPerRoomMilli.Keys).Union(RealDungeonTuning.OneWayDensityPerRoomMilli.Keys).ToList(),
            },
        };
        var load = LayoutTemplateCatalog.Load(rows, bandDefs, RealDungeonTuning.RaidModes.Keys.ToList());
        Assert.Empty(load.Rejections);
        return load.Catalog;
    }

    static EventCatalog RealEventCatalog()
    {
        var eventKinds = BandCatalog.Get("eventKind").Members;
        var repeatScopes = BandCatalog.Get("repeatScope").Members;
        var outcomeOrdinals = BandCatalog.Get("outcomeOrdinal").Members;
        var overrideTags = OverrideTagCatalog.All;
        var dropBandsPath = Path.Combine(RepoRoot(), "data", "seed", "items", "_registry", "bands.v1.json");
        using var dropBandsDoc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dropBandsPath));
        var dropBands = dropBandsDoc.RootElement.GetProperty("dropBand").GetProperty("enum")
            .EnumerateArray().Select(e => e.GetString()!).ToList();

        var rows = EventSeedFile.LoadAll(EventsDir());
        var result = EventCatalog.Load(rows, eventKinds, repeatScopes, outcomeOrdinals, dropBands, overrideTags, _ => -1);
        Assert.Empty(result.Rejections);
        return result.Catalog;
    }

    /// <summary>Row 6's own required pre-filter — `SlotFilter.Candidates` refuses eagerly on ANY
    /// unclassified corpus entry, so the caller filters first, matching `DomainEncounterPreflightBridgeTests`'s
    /// own established convention exactly.</summary>
    static IReadOnlyList<ConcreteAnchor> ClassifiedCorpus() =>
        EncounterCorpusBuilder.Build(SpeciesDir(), RealAptitudes, RealPower, RealShape, RealThreat)
            .Where(a => a.ThreatBand is not null).ToList();

    /// <summary>The real six domains' own row 1/2 facts, derived from the domains themselves — the
    /// same "known set = what the real content actually names" convention the three existing per-row
    /// bridge test files already establish (row 2's job is cross-reference existence, not independent
    /// knowledge of every legal id in the game).</summary>
    static DomainPreflightInputs RealInputs(IReadOnlyList<DomainRow> domains)
    {
        var checkGraphs = DomainGraphPreflight.Build(
            RoomPaletteSeedFile.LoadAll(RoomsDir()), DomainSeedFile.LoadRoomPalettes(DomainsDir()), RealLayoutCatalog(), RealDungeonTuning);
        var checkEncounters = DomainEncounterPreflight.Build(
            DomainSeedFile.LoadRoomPalettes(DomainsDir()), RoomEncounterRefSeedFile.LoadAll(RoomsDir()),
            EncounterSeedFile.LoadAllById(EncountersDir(), RealThreat), ClassifiedCorpus(), RealEncounterTuning);
        var checkEvents = DomainEventPreflight.Build(
            DomainSeedFile.LoadRoomPalettes(DomainsDir()), RoomPaletteSeedFile.LoadAll(RoomsDir()),
            RoomEventPoolSeedFile.LoadAll(RoomsDir()), RealEventCatalog(), RealDungeonTuning);

        return new DomainPreflightInputs(
            DangerBandOrdinals: new Dictionary<string, int>(StringComparer.Ordinal) { ["shallow"] = 2 },
            KnownLayoutIds: domains.Select(d => d.LayoutTemplateId).ToHashSet(StringComparer.Ordinal),
            KnownSpeciesIds: domains.Select(d => d.BossSpeciesRef).ToHashSet(StringComparer.Ordinal),
            ThreatBandOrdinalFor: _ => 99,
            BossFloorRungOrdinal: 0,
            // Row 3: no layout->cell legality adapter exists yet (honest, separately-named gap) --
            // an empty demand set is a structural no-op, never a false pass on a real check.
            CellsLayoutCanPlace: _ => Array.Empty<(string, string)>(),
            CellsPaletteFills: _ => Array.Empty<(string, string)>(),
            CheckGraphs: checkGraphs,
            // Row 5: the curio-verb-derivation rule has no stated rule anywhere (D4.17 row 5's own
            // entry) -- stubbed, not guessed.
            CheckObjects: _ => Array.Empty<DomainRefusal>(),
            CheckEncounters: checkEncounters,
            CheckEvents: checkEvents,
            // Row 8: needs QuestPreflight's own domain-catalog-dependent sweep (D4.13's own still-open
            // half) -- stubbed, not guessed.
            CheckQuests: _ => Array.Empty<DomainRefusal>(),
            KnownDropTableIds: Array.Empty<string>(),
            BoundLootKinds: Array.Empty<string>(),
            LootBindingFor: _ => new Dictionary<string, string>(StringComparer.Ordinal),
            // Row 10: needs RungOffer.For's own five real parameters, ParentWorldTerms among them,
            // still genuinely blocked (D4.17 row 10's own entry) -- stubbed, not guessed.
            OfferedRungCountFor: _ => 1);
    }

    [Fact]
    public void The_real_six_domains_load_without_throwing_and_are_exactly_six()
    {
        var domains = DomainSeedFile.LoadAll(DomainsDir());
        Assert.Equal(6, domains.Count);
        Assert.Equal(6, domains.Select(d => d.DomainId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void ImportDungeonDomains_against_the_real_six_domains_refuses_on_row_6_for_every_domain_and_writes_nothing()
    {
        var domains = DomainSeedFile.LoadAll(DomainsDir());
        var inputs = RealInputs(domains);
        var roomPools = DomainSeedFile.LoadRoomPalettes(DomainsDir());
        var questPools = DomainSeedFile.LoadQuestPools(DomainsDir());
        var lootBindings = DomainSeedFile.LoadLootBindings(DomainsDir());
        var provenance = domains.ToDictionary(
            d => d.DomainId,
            d => DomainSeedFile.LoadProvenanceJson(Path.Combine(DomainsDir(), d.DomainId + ".json")),
            StringComparer.Ordinal);

        var outcome = _store.ImportDungeonDomains(domains, inputs, roomPools, questPools, lootBindings, provenance, preflightSampleSeeds: 8);

        Assert.False(outcome.IsOk, "expected the real content to refuse — a pass here would mean either the threat-audit gap silently closed, or this test's own real-content wiring is broken");
        Assert.False(outcome.Committed);
        Assert.Empty(_store.ReadDomains());

        // The load-bearing assertion: EVERY one of the six real domains refuses, and every refusal
        // traces to one of the two already-documented, independent root causes — never a different,
        // newly-introduced defect this test would otherwise mask. `DomainPreflight.Run` concatenates
        // ALL FIVE delegated rows (4-8) before checking the combined count, so a domain that fails
        // BOTH row 6 (encounter) and row 7 (event) reports both refusals in one pass, not just the
        // first — a real, independent DOUBLE confirmation of two previously-separately-proven content
        // gaps (D4.17 row 6's threat-audit corpus thinness; row 7's event-pool-authoring-depth gap,
        // "every curio archetype has only 1 pool entry against events.noRepeatRooms=3"), now shown
        // together, through the real production writer, for the first time.
        var refusedDomainIds = outcome.Refusals.Select(r => r.DomainId).Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(domains.Select(d => d.DomainId).OrderBy(x => x, StringComparer.Ordinal),
            refusedDomainIds.OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(outcome.Refusals, r => Assert.True(r.Rule.StartsWith("domain.encounter:", StringComparison.Ordinal)
                || r.Rule.StartsWith("domain.event:", StringComparison.Ordinal),
            $"unexpected refusal rule '{r.Rule}' for {r.DomainId} — expected only the two already-documented root causes"));
        // Each real domain fails BOTH independent checks, not just one — the precise, doubled shape
        // this run actually produces, asserted exactly rather than loosely.
        foreach (var domainId in domains.Select(d => d.DomainId))
        {
            var rulesForDomain = outcome.Refusals.Where(r => r.DomainId == domainId).Select(r => r.Rule).ToList();
            Assert.Contains(rulesForDomain, r => r.StartsWith("domain.encounter:", StringComparison.Ordinal));
            Assert.Contains(rulesForDomain, r => r == "domain.event:cell-headroom");
        }
    }

    [Fact]
    public void DryRun_against_the_real_six_domains_reports_the_same_refusals_as_a_real_run()
    {
        var domains = DomainSeedFile.LoadAll(DomainsDir());
        var inputs = RealInputs(domains);
        var roomPools = DomainSeedFile.LoadRoomPalettes(DomainsDir());
        var questPools = DomainSeedFile.LoadQuestPools(DomainsDir());
        var lootBindings = DomainSeedFile.LoadLootBindings(DomainsDir());
        var provenance = new Dictionary<string, string>(StringComparer.Ordinal);

        var dryRun = _store.ImportDungeonDomains(domains, inputs, roomPools, questPools, lootBindings, provenance, preflightSampleSeeds: 8, dryRun: true);
        var realRun = _store.ImportDungeonDomains(domains, inputs, roomPools, questPools, lootBindings, provenance, preflightSampleSeeds: 8, dryRun: false);

        Assert.Equal(dryRun.Refusals.Select(r => (r.DomainId, r.Rule)), realRun.Refusals.Select(r => (r.DomainId, r.Rule)));
        Assert.Empty(_store.ReadDomains());
    }
}
