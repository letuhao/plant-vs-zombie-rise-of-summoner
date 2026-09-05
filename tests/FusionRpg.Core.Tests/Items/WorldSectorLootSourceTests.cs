using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Power;
using FusionRpg.Core.World;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// The `world-sector` loot source (item module 11 `drop-volume`, P3.1 follow-up).
///
/// <para>`sectorLevel(danger_band)` was tracked as owed by the world program. It is not: it was closed
/// by owner decision 2026-08-23 as `mapLevel(M) = Wm · DangerBand(M)` with `Wm = 5`
/// (ssot-power-scale.md §5.3/§10.3), and spec-content-authoring.md §2.1 names the identical formula
/// for this exact `contentLevel` row. Nothing had implemented it. These tests drive the shipped
/// formula against the REAL `SectorTypeCatalog` bands, the REAL shipped power weight, and the REAL
/// loot corpus — nothing here is synthetic.</para>
/// </summary>
public class WorldSectorLootSourceTests
{
    static PowerTuning Power() => PowerTuningHub.Tuning;

    // ---- the formula, against the real catalog ---------------------------------------------------

    [Fact]
    public void Map_level_is_five_times_the_danger_band_for_every_shipped_sector_type()
    {
        var tuning = Power();

        // Wm = 5 was DERIVED from these eight rows (§5.3), so the derivation is checked against them
        // rather than against a transcription of them.
        foreach (var def in SectorTypeCatalog.All)
            Assert.Equal(5 * def.BaseDangerBand, PowerIndexComposer.MapLevel(def.BaseDangerBand, tuning));

        // And the worked example §5.3 states in prose: a boss lair is worth 30.
        Assert.Equal(6, SectorTypeCatalog.Get("boss-lair").BaseDangerBand);
        Assert.Equal(30, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("boss-lair").BaseDangerBand, tuning));

        // The whole shipped band ladder, row by row, so a catalog edit shows up here.
        Assert.Equal(0, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("homeworld").BaseDangerBand, tuning));
        Assert.Equal(5, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("stable").BaseDangerBand, tuning));
        Assert.Equal(10, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("barren").BaseDangerBand, tuning));
        Assert.Equal(15, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("rich").BaseDangerBand, tuning));
        Assert.Equal(15, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("nexus").BaseDangerBand, tuning));
        Assert.Equal(20, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("storm").BaseDangerBand, tuning));
        Assert.Equal(20, PowerIndexComposer.MapLevel(SectorTypeCatalog.Get("warcamp").BaseDangerBand, tuning));
    }

    [Fact]
    public void Map_level_agrees_with_the_content_axis_it_mirrors()
    {
        // The anti-drift pin. MapLevel is the map-depth term of Θ_content taken alone; ContentExplain
        // sums four axes and rounds ONCE at the sum, so the two are separate code paths on purpose.
        // Wherever the map axis is the only non-zero one they must agree exactly, or one of them has
        // grown a private curve.
        var tuning = Power();
        for (var band = 0; band <= 12; band++)
        {
            var report = PowerIndexComposer.ContentExplain(tuning, new ContentContext(band, 0, 0, 0));
            Assert.Equal(report.Total, PowerIndexComposer.MapLevel(band, tuning));

            var axis = report.Axes.Single(c => c.AxisId == "dangerBand");
            Assert.Equal(PowerIndexComposer.MapLevel(band, tuning), (int)axis.Whole);
        }
    }

    [Fact]
    public void Map_level_reads_the_weight_and_never_a_literal_five()
    {
        // `5` is Wm's shipped VALUE, not the formula. Move the weight and the level moves with it —
        // a hardcoded 5 would pass every assertion above and fail this one.
        var doubled = Tuning(wmMilli: 10_000);
        Assert.Equal(60, PowerIndexComposer.MapLevel(6, doubled));

        // Fractional weights need no float: per-mille in, one rounding at the end.
        var fractional = Tuning(wmMilli: 2_500);
        Assert.Equal(15, PowerIndexComposer.MapLevel(6, fractional));   // 15.0
        Assert.Equal(8, PowerIndexComposer.MapLevel(3, fractional));    // 7.5 → 8, half away from zero
    }

    [Fact]
    public void A_missing_map_weight_throws_rather_than_guessing()
    {
        var missing = Tuning(wmMilli: null);
        var ex = Assert.Throws<PowerWeightMissing>(() => PowerIndexComposer.MapLevel(4, missing));
        Assert.Equal("Wm", ex.Weight);
    }

    [Fact]
    public void Map_level_overflows_by_throwing_never_by_wrapping()
    {
        var absurd = Tuning(wmMilli: long.MaxValue / 3);
        Assert.Throws<OverflowException>(() => PowerIndexComposer.MapLevel(int.MaxValue, absurd));
    }

    [Fact]
    public void A_negative_band_clamps_to_zero_the_same_way_every_other_axis_does()
    {
        // Absence is not corruption (spec-power-index.md §5) — the axis clamps, and the sector is then
        // refused below for the same reason band 0 is.
        Assert.Equal(0, PowerIndexComposer.MapLevel(-3, Power()));
    }

    // ---- the loot source, wired to it ------------------------------------------------------------

    [Fact]
    public void A_sector_clear_resolves_a_loot_source_at_the_decided_level()
    {
        Assert.True(WorldSectorLootSource
            .TryResolve("sector-7", dangerBand: 6, Power(), out var source).IsOk);

        Assert.NotNull(source);
        Assert.Equal("world-sector", source!.SourceKind);
        Assert.Equal("sector-7", source.SourceId);
        Assert.Equal("drop.world.sector-clear", source.TableId);
        Assert.Equal(30, source.ContentLevel);
        Assert.Null(source.FirstClearGrant);
        Assert.Equal("world-sector:sector-7", source.Key);
    }

    [Fact]
    public void A_safe_ground_sector_is_refused_by_name_never_floored_to_one()
    {
        var rejection = WorldSectorLootSource.TryResolve("home", dangerBand: 0, Power(), out var source);

        Assert.False(rejection.IsOk);
        Assert.Equal(AtomRejectionReason.ContentRuleViolated, rejection.Reason);
        Assert.StartsWith("drop.sector-band-safe:", rejection.Detail);
        Assert.Null(source);

        // The refusal exists because the validator would refuse the row anyway — the two agree.
        Assert.Equal(0, PowerIndexComposer.MapLevel(0, Power()));
    }

    [Fact]
    public void A_sector_with_no_id_is_refused_because_the_correlation_id_derives_from_it()
    {
        var rejection = WorldSectorLootSource.TryResolve("  ", dangerBand: 4, Power(), out var source);
        Assert.Equal(AtomRejectionReason.BadParamValue, rejection.Reason);
        Assert.Null(source);
    }

    [Fact]
    public void Two_sectors_of_one_type_are_two_loot_events_not_a_replay()
    {
        // ⛔ The reason the row is resolved at runtime instead of authored per sector TYPE. A static
        // seed row keyed on "boss-lair" would give both of these the same correlation id, and step 1
        // would replay the first clear for the second — minting nothing, silently.
        Assert.True(WorldSectorLootSource.TryResolve("boss-lair-a", 6, Power(), out var a).IsOk);
        Assert.True(WorldSectorLootSource.TryResolve("boss-lair-b", 6, Power(), out var b).IsOk);

        Assert.Equal(a!.ContentLevel, b!.ContentLevel);
        Assert.NotEqual(a.Key, b.Key);
        Assert.NotEqual(
            LootCorrelation.Derive(a.SourceKind, a.SourceId),
            LootCorrelation.Derive(b.SourceKind, b.SourceId));
        Assert.Equal("loot:sector:boss-lair-a", LootCorrelation.Derive(a.SourceKind, a.SourceId));
    }

    [Fact]
    public void The_resolved_source_passes_the_import_validator_beside_the_shipped_corpus()
    {
        // The runtime row is held to exactly the rules an authored row is — including standalone
        // containment, which now has a `world-sector` member for the first time.
        var corpus = DropVolumeCorpusTests.Corpus();
        var sources = corpus.Sources.ToList();

        foreach (var def in SectorTypeCatalog.All)
        {
            if (WorldSectorLootSource.TryResolve($"s-{def.TypeId}", def.BaseDangerBand, Power(), out var row).IsOk)
                sources.Add(row!);
        }

        // Seven of the eight shipped types resolve; homeworld is safe ground and does not.
        Assert.Equal(corpus.Sources.Count + 7, sources.Count);

        var verdict = DropTableValidator.Validate(sources, corpus.Tables, DropVolumeTests.Tuning());
        Assert.True(verdict.IsOk, verdict.ToString());
    }

    // ---- end to end, through the twelve steps ----------------------------------------------------

    [Fact]
    public void A_sector_clear_drops_loot_through_the_whole_pipeline()
    {
        var manifest = ResolveSectorClear("sector-boss-1", dangerBand: 6, seed: 0xB055);

        Assert.Equal("loot:sector:sector-boss-1", manifest.CorrelationId);
        Assert.Equal("drop.world.sector-clear", manifest.TableId);
        Assert.InRange(manifest.ItemLevel, 29, 31);          // mapLevel 30, ±1 jitter
        Assert.False(manifest.Replayed);
        Assert.NotEmpty(manifest.Grants);
        Assert.All(manifest.Grants, g => Assert.Equal(DropEntryKind.Equipment, g.Kind));
        Assert.All(manifest.Grants, g => Assert.Equal(AffixChannels.Drop, g.AffixChannel));
        Assert.All(manifest.Grants, g => Assert.Equal(manifest.ItemLevel, g.ItemLevel));
    }

    [Fact]
    public void A_deeper_sector_drops_higher_level_items()
    {
        // The whole point of the formula: depth is expressed in the item, not in a flat table.
        var shallow = ResolveSectorClear("sector-stable", dangerBand: 1, seed: 0x5EED);
        var deep = ResolveSectorClear("sector-lair", dangerBand: 6, seed: 0x5EED);

        Assert.InRange(shallow.ItemLevel, 4, 6);
        Assert.InRange(deep.ItemLevel, 29, 31);
        Assert.True(deep.ItemLevel > shallow.ItemLevel);
    }

    static LootManifest ResolveSectorClear(string sectorId, int dangerBand, ulong seed)
    {
        Assert.True(WorldSectorLootSource.TryResolve(sectorId, dangerBand, Power(), out var source).IsOk);

        var corpus = DropVolumeCorpusTests.Corpus();
        var baseTypes = DropVolumeCorpusTests.BaseTypes();
        var sources = corpus.Sources.ToDictionary(s => s.Key, StringComparer.Ordinal);
        sources[source!.Key] = source;

        var view = new LootContentView(
            sources,
            corpus.Tables.ToDictionary(t => t.TableId, StringComparer.Ordinal),
            DropVolumeCorpusTests.Ladder(),
            (frame, role) => baseTypes.TryGetValue((frame, role), out var l)
                ? l
                : (IReadOnlyList<string>)Array.Empty<string>());

        var request = new LootRequest("player-1", WorldSectorLootSource.SourceKind, sectorId, seed, ThetaActor: 20);
        var verdict = LootPipeline.Resolve(request, view, DropVolumeTests.Tuning(), LootPityState.Empty, out var manifest);

        Assert.True(verdict.IsOk, verdict.ToString());
        return manifest!;
    }

    static PowerTuning Tuning(long? wmMilli) => PowerTuning.Build(
        schemaVersion: 1, version: 1,
        cMilli: 80_000, bMilli: 400, pinIndex: 20, pinValue: 680,
        wdMilli: 1000, waMilli: 25_000, wrMilli: 250, wzMilli: 1000,
        wmMilli: wmMilli, wwMilli: 5000, wfMilli: 25_000);
}
