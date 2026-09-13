using FusionRpg.Core.Delve.Domains;
using FusionRpg.Core.Delve.Encounter;
using FusionRpg.Core.Delve.Roll;
using FusionRpg.Core.Creatures.Generation;
using FusionRpg.Core.Dungeon.Tuning;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Tests.Delve.Encounter;
using FusionRpg.Core.Tests.Dungeon;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Domains;

/// <summary>
/// D4.31 (party-dungeon-todo.md, 2026-09-07) — `DomainEncounterCoverage.Report`, run for real over the
/// six shipped domains. Reuses D4.17 row 6's own already-proven setup (classified corpus, real rooms/
/// encounters) since it is the identical underlying mechanism (`Encounter.Build` -&gt;
/// `SlotFilter.Candidates`) that already refuses on the classified corpus's own thinness — this file
/// proves the SAME real finding from a different acceptance angle, not a new one.
/// </summary>
public class DomainEncounterCoverageTests
{
    static readonly CreatureThreatTuning RealThreat = RealAnchorCorpusFixture.ThreatTuning;
    static readonly EncounterTuning RealEncounterTuning = EncounterTuningHub.Tuning;
    static readonly AptitudeTuning RealAptitudes =
        AptitudeTuningLoader.Parse(File.ReadAllText(Path.Combine(DungeonTestFiles.RepoRoot(), "data", "tuning", "aptitudes.v2.json")));
    static readonly PowerTuning RealPower =
        PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(DungeonTestFiles.RepoRoot(), "data", "tuning", "power-scale.v2.json")));
    static readonly CreatureShapeTuning RealShape =
        CreatureShapeTuningLoader.Parse(File.ReadAllText(Path.Combine(DungeonTestFiles.RepoRoot(), "data", "tuning", "creature-shape.v1.json")));
    static readonly RaidModeTuning Solo = DungeonTuningHub.Tuning.RaidModes["solo"];
    static readonly DifficultyRungTuning Hard = DungeonTuningHub.Tuning.Rungs["hard"];

    static IReadOnlyList<ConcreteAnchor> ClassifiedCorpus() =>
        EncounterCorpusBuilder.Build(DungeonTestFiles.SpeciesDir(), RealAptitudes, RealPower, RealShape, RealThreat)
            .Where(a => a.ThreatBand is not null).ToList();

    [Fact]
    public void Report_null_arguments_throw()
    {
        var domain = new DomainRow("d", "n", "f", "t", "fire", "shallow", "many", "l", "species.x", null, "Lair", null);
        var rooms = new Dictionary<string, RoomPaletteEntry>();
        var refs = new Dictionary<string, string?>();
        var byId = new Dictionary<string, EncounterAnchor>();
        var corpus = ClassifiedCorpus();
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(null!, Array.Empty<string>(), rooms, refs, byId, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, null!, rooms, refs, byId, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), null!, refs, byId, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, null!, byId, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, null!, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, null!, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, corpus, 500, null!, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, corpus, 500, Solo, null!, RealEncounterTuning, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, corpus, 500, Solo, Hard, null!, RealThreat, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, corpus, 500, Solo, Hard, RealEncounterTuning, null!, RealAptitudes, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, null!, RealPower, 32, 81));
        Assert.Throws<ArgumentNullException>(() => DomainEncounterCoverage.Report(domain, Array.Empty<string>(), rooms, refs, byId, corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, null!, 32, 81));
    }

    /// <summary>An empty palette samples nothing — zero distinct cells, budget fails honestly (never a
    /// false "meets budget" on no data), no refusals (nothing was even attempted).</summary>
    [Fact]
    public void An_empty_room_palette_reports_zero_cells_and_fails_budget_honestly()
    {
        var domain = new DomainRow("domain.x", "n", "f", "t", "fire", "shallow", "many", "l", "species.x", null, "Lair", null);
        var report = DomainEncounterCoverage.Report(domain, Array.Empty<string>(),
            new Dictionary<string, RoomPaletteEntry>(), new Dictionary<string, string?>(),
            new Dictionary<string, EncounterAnchor>(), ClassifiedCorpus(), 500, Solo, Hard,
            RealEncounterTuning, RealThreat, RealAptitudes, RealPower, sampleSeeds: 32, budgetTarget: 81);

        Assert.Equal("domain.x", report.DomainId);
        Assert.Equal(0, report.DistinctCells);
        Assert.False(report.MeetsBudget);
        Assert.Empty(report.RoomRefusals);
    }

    /// <summary>Non-`fight`/`elite`/`boss` rooms (e.g. `rest`) are never sampled at all, even when
    /// present in the palette with a real encounterRef — spec §8's own scope.</summary>
    [Fact]
    public void Non_combat_room_kinds_are_never_sampled()
    {
        var domain = new DomainRow("domain.x", "n", "f", "t", "fire", "shallow", "many", "l", "species.x", null, "Lair", null);
        var rooms = new Dictionary<string, RoomPaletteEntry>(StringComparer.Ordinal) { ["room.rest"] = new("room.rest", "rest", null) };
        var refs = new Dictionary<string, string?>(StringComparer.Ordinal) { ["room.rest"] = "encounter.real" };
        var byId = new Dictionary<string, EncounterAnchor>(StringComparer.Ordinal)
        {
            ["encounter.real"] = new EncounterAnchor(Formation.Pack,
                new[] { new EncounterSlot(Posture.Bastion, null, null, "few") },
                new[] { 0 }, ElementSpreadMode.Mono, new ThreatWindow(1, 10), null, null),
        };

        var report = DomainEncounterCoverage.Report(domain, new[] { "room.rest" }, rooms, refs, byId,
            ClassifiedCorpus(), 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower,
            sampleSeeds: 32, budgetTarget: 81);

        Assert.Equal(0, report.DistinctCells);
        Assert.Empty(report.RoomRefusals);
    }

    /// <summary>
    /// The real end-to-end run over all six real domains — proving the SAME real finding D4.17 row 6
    /// already documented (all fight/elite/boss archetypes refuse on the classified corpus's own
    /// thinness), now from D4.31's own acceptance angle. Not asserted to pass — the classified corpus
    /// is real and thin; this reports the true verdict.
    /// </summary>
    [Fact]
    public void Real_domains_report_the_same_classified_corpus_refusal_row_6_already_found()
    {
        var domains = DomainSeedFile.LoadAll(DungeonTestFiles.DomainsDir());
        var palettes = DomainSeedFile.LoadRoomPalettes(DungeonTestFiles.DomainsDir());
        var rooms = RoomPaletteSeedFile.LoadAll(DungeonTestFiles.RoomsDir());
        var refs = RoomEncounterRefSeedFile.LoadAll(DungeonTestFiles.RoomsDir());
        var byId = EncounterSeedFile.LoadAllById(DungeonTestFiles.EncountersDir(), RealThreat);
        var corpus = ClassifiedCorpus();

        Assert.Equal(6, domains.Count);
        foreach (var domain in domains)
        {
            var report = DomainEncounterCoverage.Report(domain, palettes[domain.DomainId], rooms, refs, byId,
                corpus, 500, Solo, Hard, RealEncounterTuning, RealThreat, RealAptitudes, RealPower,
                sampleSeeds: 32, budgetTarget: 81);

            Assert.False(report.MeetsBudget, $"{domain.DomainId} unexpectedly met budget — the classified-corpus gap may have closed");
            Assert.NotEmpty(report.RoomRefusals);
        }
    }
}
