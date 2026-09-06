using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Uniques;
using FusionRpg.Core.Power;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>D4.24 (spec-unique-pipeline.md §2) — `UniqueContainerBuild.From`: a build golden against
/// the real shipped corpus, the roll count is exactly one (or zero, for the 8 of 144 anchors that
/// author no variance slot), and the built container actually round-trips through the REAL
/// `Instantiator.TryInstantiate` — proving compatibility with the real pipeline, not just a
/// disconnected `ContainerRow` shape.
///
/// <para><b>A major, previously-unexercised finding surfaced while writing these tests:</b> the 144
/// unique anchors author 68 distinct atom families; the real shipped `data/seed/atoms/` catalog has
/// only 28 families total, and just 7 of those (`bulwark, ferocity, fortitude, mending, might,
/// savagery, vitality`) overlap with what the uniques corpus actually names (confirmed by extracting
/// both family lists directly and diffing them). No existing code ever noticed, because nothing before
/// this task resolved a unique's `family × powerBand` into a real atom id — `UniqueParityMetric`'s own
/// pricing only ever needs the TIER NUMBER `TierOfPowerBand` derives, never the atom itself. Of the 144
/// real anchors, only 4 (`rot-bloom-30-002`, `thorned-chassis-30-004`, `verdant-graft-90-005`,
/// `windswept-spore-50-001`) name EXCLUSIVELY real families and can be concretely built today — every
/// other anchor correctly REFUSES with `unique.corpus-malformed`, naming the exact missing atom id,
/// which is the honest, correct behaviour for a seed authored ahead of the atom catalog that backs it
/// (the same "seed → concrete" law this whole session's own established discipline names explicitly),
/// not a defect in this task's own build function.</para></summary>
public class UniqueContainerBuildTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static string Seed(params string[] parts) => Path.Combine(new[] { RepoRoot(), "data", "seed" }.Concat(parts).ToArray());

    static readonly IReadOnlyList<UniqueSeed> Corpus = LoadCorpus();
    static readonly IReadOnlyList<AtomRow> Atoms = LoadAtoms();
    static readonly IReadOnlyDictionary<string, AtomRow> AtomsById = Atoms.ToDictionary(a => a.AtomId, StringComparer.Ordinal);
    static readonly IReadOnlyDictionary<string, RarityRungWindow> Windows = LoadWindows();

    static IReadOnlyList<UniqueSeed> LoadCorpus()
    {
        var all = new List<UniqueSeed>();
        foreach (var f in Directory.GetFiles(Seed("items", "uniques"), "*.json").OrderBy(x => x, StringComparer.Ordinal))
            all.AddRange(UniqueCorpus.Parse(File.ReadAllText(f)));
        return all;
    }

    static IReadOnlyList<AtomRow> LoadAtoms()
    {
        var files = new[] { "atoms", "containers" }
            .Select(d => Path.Combine(RepoRoot(), "data", "seed", d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.json", SearchOption.AllDirectories))
            // data/seed/atoms/vocabulary.json is a pre-existing, already-documented defect (an empty
            // "kind" field) unrelated to this task -- excluded here rather than fixed, matching the
            // established workaround for the same file in every other affected test this session.
            .Where(f => !string.Equals(Path.GetFileName(f), "vocabulary.json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (f, File.ReadAllText(f)))
            .ToArray();
        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));
        return collected.Content.Atoms;
    }

    static IReadOnlyDictionary<string, RarityRungWindow> LoadWindows()
    {
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Seed("rarity", "ladder.v1.json")));
        return doc.RootElement.GetProperty("entries").EnumerateArray().ToDictionary(
            e => e.GetProperty("id").GetString()!,
            e => new RarityRungWindow(
                e.GetProperty("id").GetString()!,
                e.GetProperty("minTier").GetInt32(),
                e.GetProperty("maxTier").GetInt32(),
                e.GetProperty("prefixRolls").GetInt32() + e.GetProperty("suffixRolls").GetInt32()),
            StringComparer.Ordinal);
    }

    static readonly IReadOnlyDictionary<string, AffixRow> AffixesById =
        AffixLibraryGenerator.Generate(Atoms).ToDictionary(a => a.AffixId, StringComparer.Ordinal);

    static UniqueContainerLookups Lookups() => new(
        LookupAtom: id => AtomsById.TryGetValue(id, out var a) ? a : null,
        AtomsInFamily: family => Atoms.Where(a => string.Equals(a.FamilyId, family, StringComparison.Ordinal)).ToList());

    /// <summary>One of exactly 4 real anchors (of 144) naming only atom families the shipped catalog
    /// actually has — see this file's own class doc comment for the full finding.</summary>
    const string GoldenSeedId = "unique.rot-bloom-30-002";

    /// <summary>`cultivated`'s real ordinal (`data/seed/rarity/ladder.v1.json`) — well below both the
    /// D4.26 extend-slot thresholds (80/90), so every pre-D4.26 test below that passes this stays
    /// exactly as it was: no atom appended, no roll drawn, byte-identical container shape.</summary>
    const int BelowExtendSlotThreshold = 40;

    // ---- the build golden ---------------------------------------------------------------------------

    [Fact]
    public void Building_a_real_anchor_with_a_variance_slot_produces_the_expected_shape()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId);
        var window = Windows[anchor.RarityId];

        var (container, dropped) = UniqueContainerBuild.From(
            anchor, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups());

        Assert.Equal("item.rot-bloom-30-002", container.ContainerId);
        Assert.Equal(ContainerKind.Item, container.Kind);
        Assert.Equal("cultivated", container.Rarity);
        Assert.Equal(2, container.Atoms.Count); // fortitude/medium, mending/low
        Assert.Equal(new[] { "atom.fortitude.t3", "atom.mending.t2" }, container.Atoms.Select(a => a.AtomId));
        Assert.Equal(1, container.PrefixRolls + container.SuffixRolls); // exactly one roll
        Assert.NotEmpty(container.Pool); // atom.vitality candidates at the rung's reference tier
        Assert.Equal(container.MinTier, container.MaxTier); // "MinTier == MaxTier at the authored tier"
        Assert.Equal(2, container.MinTier); // cultivated's window is [1,3]; ReferenceTier = (1+3)/2 = 2, pinned exactly
        Assert.Empty(dropped); // atom.vitality is not known to mix affix classes
    }

    [Fact]
    public void An_anchor_with_no_variance_slot_authors_zero_rolls_and_an_empty_pool()
    {
        // thorned-chassis-30-004: one of the 8 no-variance-slot anchors, and one of the 4 total whose
        // atoms are all real (fortitude, vitality) -- see this file's own class doc comment.
        var anchor = Corpus.Single(s => s.SeedId == "unique.thorned-chassis-30-004");
        Assert.Null(anchor.VarianceSlot);
        var window = Windows[anchor.RarityId];

        var (container, _) = UniqueContainerBuild.From(
            anchor, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups());

        Assert.Equal(0, container.PrefixRolls + container.SuffixRolls);
        Assert.Empty(container.Pool);
        Assert.Null(container.MinTier);
        Assert.Null(container.MaxTier);
    }

    // ---- "the roll count is exactly one" (or zero) — over the WHOLE real corpus, honestly -----------

    /// <summary>Pins the finding this file's own class doc comment names: of 144 real anchors, exactly
    /// 4 name only real atom families and build; every other one correctly REFUSES rather than
    /// silently producing a wrong or partial container. A number that goes DOWN over time (fewer
    /// buildable anchors) would mean a regression; a number that goes UP means the atom catalog closed
    /// part of the gap — either way this test forces the change to be seen and explained, not silent.</summary>
    [Fact]
    public void Exactly_four_of_the_real_144_anchors_build_today_every_other_refuses_naming_the_missing_atom()
    {
        var built = 0;
        var refusedForMissingAtom = 0;
        foreach (var anchor in Corpus)
        {
            var window = Windows[anchor.RarityId];
            try
            {
                // rungOrdinal held BELOW the D4.26 extend-slot thresholds for every anchor here,
                // deliberately independent of each anchor's own real rarity ordinal: this test's own
                // claim is about which FIXED ATOMS resolve, not about rung-threshold behavior (that
                // is Rung_90_or_above_carries_the_extend_slot_atom_unconditionally's own claim, below).
                var (container, _) = UniqueContainerBuild.From(
                    anchor, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups());
                var totalRolls = container.PrefixRolls + container.SuffixRolls;
                Assert.True(totalRolls <= UniqueLimits.MaxTotalRolls, $"{anchor.SeedId}: {totalRolls} rolls exceeds MaxTotalRolls");
                Assert.Equal(anchor.TotalRolls, totalRolls); // the seed's own TotalRolls property agrees with the built container
                built++;
            }
            catch (UniqueCorpusRejection ex) when (ex.Rejection.Detail.Contains("is not in the atom catalog", StringComparison.Ordinal))
            {
                refusedForMissingAtom++;
            }
        }

        Assert.Equal(144, Corpus.Count);
        Assert.Equal(4, built);
        Assert.Equal(140, refusedForMissingAtom);
    }

    // ---- the built container is not just shaped right -- it actually instantiates -------------------

    [Fact]
    public void A_built_container_round_trips_through_the_real_Instantiator()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId);
        var window = Windows[anchor.RarityId];
        var (container, _) = UniqueContainerBuild.From(
            anchor, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups());

        var rejection = Instantiator.TryInstantiate(
            container, id => AtomsById.TryGetValue(id, out var a) ? a : null,
            id => AffixesById.TryGetValue(id, out var af) ? af : null,
            rollSeed: 12345, thetaContent: 20, PowerTuningHub.Tuning, out var instance);

        Assert.True(rejection.IsOk, rejection.Detail);
        Assert.NotNull(instance);
        Assert.Equal(3, instance!.Atoms.Count); // 2 fixed-core atoms + 1 drawn from the pool (the container's one roll)
    }

    /// <summary>Proves a REFUSED build (the 140-of-144 case) never reaches `Instantiator` at all —
    /// "refuses before writing", not "writes something wrong".</summary>
    [Fact]
    public void An_anchor_naming_a_missing_atom_family_refuses_before_Instantiator_ever_runs()
    {
        var anchor = Corpus.Single(s => s.SeedId == "unique.ember-harvest-30-001"); // fixedAtoms include atom.hit-mend, confirmed absent from the real catalog
        var window = Windows[anchor.RarityId];

        var ex = Assert.Throws<UniqueCorpusRejection>(() => UniqueContainerBuild.From(
            anchor, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups()));
        Assert.Contains("atom.hit-mend.t2", ex.Rejection.Detail);
        Assert.Contains("is not in the atom catalog", ex.Rejection.Detail);
    }

    // ---- guards --------------------------------------------------------------------------------------

    [Fact]
    public void A_mismatched_rarity_window_is_refused()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId); // rarity: cultivated
        var wrongWindow = Windows["heirloom"];

        Assert.Throws<ArgumentException>(() => UniqueContainerBuild.From(
            anchor, wrongWindow, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups()));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId);
        var window = Windows[anchor.RarityId];
        Assert.Throws<ArgumentNullException>(() => UniqueContainerBuild.From(
            null!, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, Lookups()));
        Assert.Throws<ArgumentNullException>(() => UniqueContainerBuild.From(
            anchor, window, BelowExtendSlotThreshold, rollSeed: 0, extendSlotChanceMicro: 0, null!));
    }

    // ---- D4.26: the extend-action-slot grant's carriage by rung ---------------------------------------

    /// <summary>One of the 4 real, buildable anchors, and genuinely rarity `sunwoven` (ordinal 90) in
    /// production data — proves rung >= 90 using real content, not a synthetic override.</summary>
    const string Rung90SeedId = "unique.verdant-graft-90-005";

    [Fact]
    public void Rung_90_or_above_carries_the_extend_slot_atom_unconditionally()
    {
        var anchor = Corpus.Single(s => s.SeedId == Rung90SeedId);
        var window = Windows[anchor.RarityId]; // sunwoven

        // extendSlotChanceMicro: 0 -- proves the atom is carried WITHOUT drawing on the roll at all,
        // "never rolled" being the literal acceptance line, not merely "usually appended".
        var (container, _) = UniqueContainerBuild.From(anchor, window, rungOrdinal: 90, rollSeed: 0, extendSlotChanceMicro: 0, Lookups());

        Assert.Contains(container.Atoms, a => a.AtomId == ExtendSlotAtom.Id);
    }

    [Fact]
    public void Rung_80_with_a_guaranteed_hit_carries_the_extend_slot_atom()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId);
        var window = Windows[anchor.RarityId];

        var (container, _) = UniqueContainerBuild.From(
            anchor, window, rungOrdinal: 80, rollSeed: 999, extendSlotChanceMicro: 1_000_000, Lookups());

        Assert.Contains(container.Atoms, a => a.AtomId == ExtendSlotAtom.Id);
    }

    [Fact]
    public void Rung_80_with_a_guaranteed_miss_never_carries_the_extend_slot_atom()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId);
        var window = Windows[anchor.RarityId];

        var (container, _) = UniqueContainerBuild.From(
            anchor, window, rungOrdinal: 80, rollSeed: 999, extendSlotChanceMicro: 0, Lookups());

        Assert.DoesNotContain(container.Atoms, a => a.AtomId == ExtendSlotAtom.Id);
    }

    /// <summary>The rung-80 gate is a genuine THRESHOLD, not a "chance always applies" rule — below
    /// 80, the roll must never even be attempted, proven here by a 100% chance still producing nothing.</summary>
    [Fact]
    public void Rung_below_80_never_carries_the_atom_even_at_a_100_percent_chance()
    {
        var anchor = Corpus.Single(s => s.SeedId == GoldenSeedId);
        var window = Windows[anchor.RarityId];

        var (container, _) = UniqueContainerBuild.From(
            anchor, window, rungOrdinal: 79, rollSeed: 999, extendSlotChanceMicro: 1_000_000, Lookups());

        Assert.DoesNotContain(container.Atoms, a => a.AtomId == ExtendSlotAtom.Id);
    }

    [Fact]
    public void A_missing_extend_slot_atom_in_the_catalog_refuses_naming_it()
    {
        var anchor = Corpus.Single(s => s.SeedId == Rung90SeedId);
        var window = Windows[anchor.RarityId];
        var lookupsWithoutExtendSlot = new UniqueContainerLookups(
            LookupAtom: id => id == ExtendSlotAtom.Id ? null : AtomsById.TryGetValue(id, out var a) ? a : null,
            AtomsInFamily: family => Atoms.Where(a => string.Equals(a.FamilyId, family, StringComparison.Ordinal)).ToList());

        var ex = Assert.Throws<UniqueCorpusRejection>(() => UniqueContainerBuild.From(
            anchor, window, rungOrdinal: 90, rollSeed: 0, extendSlotChanceMicro: 0, lookupsWithoutExtendSlot));

        Assert.Contains(ExtendSlotAtom.Id, ex.Rejection.Detail);
        Assert.Contains("is not in the atom catalog", ex.Rejection.Detail);
    }
}
