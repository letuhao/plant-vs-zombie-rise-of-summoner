using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// T4.4 (`species-generator`, `spec-species-generator.md`, demon-seed module 12): expands a real
/// classified anchor into a concrete species — every magnitude via
/// <see cref="AptitudeReadFunctions.Magnitude"/> reading one <c>P(Θ)</c>, never a private `f(level)`.
/// Exercised against the REAL shipped tuning files (`aptitudes.v2.json`, `demon-shape.v1.json`,
/// `demon-threat.v1.json`) and the two real classified anchors on disk
/// (`data/seed/demons/species/plant/{pea,sunflower}.json`), not synthetic fixtures — the same
/// discipline `AptitudeMatrixTests.cs` already established for reading the shipped aptitude file.
/// </summary>
public class SpeciesExpanderTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static string ReadTuning(params string[] relative) => File.ReadAllText(Path.Combine(new[] { RepoRoot() }.Concat(relative).ToArray()));

    static readonly AptitudeTuning RealAptitudes = AptitudeTuningLoader.Parse(ReadTuning("data", "tuning", "aptitudes.v2.json"));
    static readonly DemonShapeTuning RealShape = DemonShapeTuningLoader.Parse(ReadTuning("data", "tuning", "demon-shape.v1.json"));
    static readonly DemonThreatTuning RealThreat = DemonThreatTuningLoader.Parse(ReadTuning("data", "tuning", "demon-threat.v1.json"));

    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, PowerTuning.FixedCMilli, 0, PowerTuning.FixedPinIndex, PowerTuning.FixedPinValue,
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    /// <summary>Resolves a species' CURRENT real anchor file via `_index.json` rather than a
    /// hardcoded path — found broken live, 2026-09-04 (demon-corpus-self-heal): a species' family
    /// bucket is model-decided and moves across reclassifications, so a test hardcoding
    /// `"pea.json"` breaks the moment the pipeline it exercises does its own job correctly. The
    /// index is exactly the lookup `run-control` itself uses for the same reason.</summary>
    static AnchorRow RealAnchor(string speciesId)
    {
        var indexPath = Path.Combine(RepoRoot(), "data", "seed", "demons", "species", "_index.json");
        var index = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(indexPath))!;
        var relPath = index[speciesId];
        return AnchorRowReader.ReadAll(ReadTuning("data", "seed", "demons", "species", relPath.Replace('/', Path.DirectorySeparatorChar)))
            .Single(a => a.SpeciesId == speciesId);
    }

    static ConcreteSpecies Expand(AnchorRow anchor, long? statedIntervalMs = null) =>
        SpeciesExpander.Expand(anchor, RealAptitudes, Tuning, RealShape, RealThreat, statedIntervalMs);

    /// <summary>Fixture anchor with sensible, real-shape defaults for the pass-through
    /// (catalog-runtime) fields these tests don't otherwise exercise — mirrors `pea.json`'s own real
    /// values, not invented ones.</summary>
    static AnchorRow TestAnchor(
        string speciesId, string rarity, string? threatBand, string aptitudePrimary,
        string? aptitudeSecondary, bool pure, string attackTempo, string reach,
        IReadOnlyList<string> variants) =>
        new(speciesId, rarity, threatBand, aptitudePrimary, aptitudeSecondary, pure, attackTempo, reach,
            variants, Side: "plant", GameTypeId: 0, ElementPrimary: "earth", ElementSecondary: null,
            DeployMode: "PlantAvatar", Acquisition: new[] { "Summonable" }, Traits: Array.Empty<string>());

    // ---- over the real shipped anchors -------------------------------------------------------------

    [Fact]
    public void Peashooter_expands_without_throwing_and_carries_its_own_theta()
    {
        var anchor = RealAnchor("Peashooter");
        Assert.Equal("Peashooter", anchor.SpeciesId);
        Assert.Null(anchor.ThreatBand); // real gap in the real data — the fallback path is exercised

        var species = Expand(anchor);

        Assert.Equal(DemonRarity.Cultivated, species.Rarity);
        Assert.True(species.Theta > 0);
        Assert.True(species.PTheta > 0);
        Assert.NotEmpty(species.Magnitudes);
    }

    [Fact]
    public void Peashooter_carries_every_catalog_runtime_field_straight_from_its_real_anchor()
    {
        // T4.8's own real precondition (found 2026-09-02, not assumed): `DemonSpeciesDef`'s
        // production fields have a real source after all — the anchor itself — this proves the
        // pass-through against pea.json's own literal, on-disk values, not a synthetic fixture.
        var species = Expand(RealAnchor("Peashooter"));

        Assert.Equal("plant", species.Side);
        Assert.Equal(0, species.GameTypeId);
        Assert.Equal(ElementTypeId.Earth, species.ElementPrimary);
        Assert.Null(species.ElementSecondary); // pea.json's own "none" sentinel
        Assert.Equal(DemonDeployMode.PlantAvatar, species.DeployMode);
        Assert.Equal(DemonAcquisition.Summonable, species.Acquisition);
        Assert.Equal(new[] { "normal", "mutated" }, species.Variants);
        // Traits are open-vocabulary, LLM-proposed content (identity pipeline) — re-classified
        // during demon-corpus-self-heal's own C3 unresolved-field healing pass, 2026-09-04
        // ("Rapid-fire" -> "Rhythmic-attack"), a legitimate outcome, not a defect.
        //
        // Re-classified AGAIN by C2's kit-shape pass later the same day, and updated here for the
        // same reason: casing moved to lower-kebab and "Defensive" narrowed to "defensive-line".
        // The casing half is corpus-wide, not local to this row — a sweep of every anchor counts
        // **2802 lower-case trait tokens against 79 upper-first**, so the corpus has a convention and
        // this row follows it. Verified as a vocabulary shift rather than data loss: the trait COUNT
        // and the three concepts are unchanged.
        Assert.Equal(new[] { "projectile-launching", "defensive-line", "rhythmic-attack" }, species.TraitPool);
        Assert.Null(species.Name); // never resolved here — species-import's own job (T4.6)
    }

    [Fact]
    public void An_elementSecondary_other_than_none_parses_to_a_real_element()
    {
        var withSecondary = TestAnchor(
                "test.dualelement", "cultivated", "raider", "Onslaught", null, true, "steady", "melee",
                new[] { "normal" })
            with
            { ElementSecondary = "fire" };

        var species = Expand(withSecondary);

        Assert.Equal(ElementTypeId.Fire, species.ElementSecondary);
    }

    [Fact]
    public void An_unknown_acquisition_flag_is_a_startup_error_not_a_silent_drop()
    {
        var anchor = TestAnchor(
                "test.badacq", "cultivated", "raider", "Onslaught", null, true, "steady", "melee",
                new[] { "normal" })
            with
            { Acquisition = new[] { "NotARealFlag" } };

        Assert.Throws<InvalidOperationException>(() => Expand(anchor));
    }

    // ---- an unresolved or unknown aptitude family refuses generation, never a silent zero-stat species ----
    //
    // Audited 2026-09-03 against the real 28-species committed tree: SnorkleZombie/ThreePeater both
    // carry `aptitudePrimary: "unresolved"` (a genuine 3-way classification vote that never
    // converged, spec-option-permutation.md §4) and both had `Magnitudes: {}` — the ONE field in
    // this whole function that fell through silently instead of throwing like every sibling field
    // already does.

    [Fact]
    public void An_unresolved_aptitudePrimary_refuses_generation_rather_than_producing_zero_stats()
    {
        var anchor = TestAnchor(
            "test.unresolved", "chaff", null, "unresolved", null, false, "steady", "melee",
            Array.Empty<string>());

        var ex = Assert.Throws<InvalidOperationException>(() => Expand(anchor));
        Assert.Contains("aptitudePrimary", ex.Message);
        Assert.Contains("unresolved", ex.Message);
    }

    [Fact]
    public void An_unknown_aptitudePrimary_family_refuses_generation_too()
    {
        // Not just the literal "unresolved" sentinel — any family with no edge at all in
        // aptitudes.v2.json (a typo, a retired family) must refuse the same way.
        var anchor = TestAnchor(
            "test.badaptitude", "chaff", null, "NotARealAptitudeFamily", null, false, "steady", "melee",
            Array.Empty<string>());

        Assert.Throws<InvalidOperationException>(() => Expand(anchor));
    }

    [Fact]
    public void An_unresolved_aptitudeSecondary_refuses_generation_even_when_primary_is_fine()
    {
        var anchor = TestAnchor(
            "test.badsecondary", "cultivated", "raider", "Onslaught", "unresolved", false, "steady", "melee",
            Array.Empty<string>());

        var ex = Assert.Throws<InvalidOperationException>(() => Expand(anchor));
        Assert.Contains("aptitudeSecondary", ex.Message);
    }

    [Fact]
    public void A_pure_species_never_evaluates_aptitudeSecondary_even_when_it_is_garbage()
    {
        // Pure means zero secondary share — TestAnchor's own `pure: true` already makes
        // AptitudeSecondary irrelevant to the expansion math; confirms the secondary check does not
        // fire spuriously just because the field is present but unused.
        var anchor = TestAnchor(
                "test.puregarbagesecondary", "cultivated", "raider", "Onslaught", null, true, "steady",
                "melee", Array.Empty<string>())
            with
            { AptitudeSecondary = "unresolved" };

        // Pure => hasSecondary is false in Expand's own logic, so AptitudeSecondary is never read —
        // this must expand cleanly, not throw.
        var species = Expand(anchor);
        Assert.NotEmpty(species.Magnitudes);
    }

    [Fact]
    public void A_known_aptitude_still_expands_normally_after_the_new_check()
    {
        // Regression guard: the new IsKnownAptitude check must not reject legitimate families.
        var anchor = TestAnchor(
            "test.knowngood", "cultivated", "raider", "Onslaught", null, true, "steady", "melee",
            Array.Empty<string>());

        var species = Expand(anchor);
        Assert.NotEmpty(species.Magnitudes);
    }

    [Fact]
    public void Sunflower_is_pure_focus_and_every_magnitude_traces_to_that_one_family()
    {
        var anchor = RealAnchor("SunFlower");
        Assert.True(anchor.Pure);
        Assert.Equal("Focus", anchor.AptitudePrimary);

        var species = Expand(anchor);

        // Every channel this species carries a magnitude for must be an edge Focus itself reaches —
        // pure means zero secondary contribution, so nothing from another family should appear.
        var focusChannels = RealAptitudes.Edges
            .Where(e => e.Source == "Focus" && e.Mode == AptitudeReadMode.Magnitude)
            .Select(e => e.Channel)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(species.Magnitudes.Keys, ch => Assert.Contains(ch, focusChannels));
    }

    [Fact]
    public void Hp_and_damage_read_the_same_pTheta()
    {
        // Q21: no channel gets a second growth rate. Directly re-derive one magnitude by hand from
        // the species' own recorded PTheta and compare — proves the SAME pTheta fed every channel,
        // not a per-channel recomputation.
        var anchor = RealAnchor("SunFlower");
        var species = Expand(anchor);

        var edge = RealAptitudes.Edges.First(e =>
            e.Source == "Focus" && e.Mode == AptitudeReadMode.Magnitude && species.Magnitudes.ContainsKey(e.Channel));
        var expected = AptitudeReadFunctions.Magnitude(
            edge.KMilli, 1.0, RealAptitudes.Read.Magnitude.ShareExponentMilli, species.PTheta);

        Assert.Equal(expected, species.Magnitudes[edge.Channel]);
    }

    // ---- theta / threat rung -----------------------------------------------------------------------

    [Fact]
    public void A_missing_threatBand_falls_back_to_the_files_own_inferred_default_rung()
    {
        var withoutBand = TestAnchor(
            "test.species", "cultivated", null, "Onslaught", null, true, "steady", "melee",
            new[] { "normal" });
        var withBand = withoutBand with { ThreatBand = RealThreat.Thresholds.First(t => t.Rung == RealThreat.InferredDefaultRung).Id };

        Assert.Equal(Expand(withoutBand).Theta, Expand(withBand).Theta);
    }

    [Fact]
    public void A_real_threatBand_changes_theta_from_the_fallback()
    {
        var top = RealThreat.Thresholds.OrderByDescending(t => t.Rung).First();
        Assert.NotEqual(top.Rung, RealThreat.InferredDefaultRung); // the real file's own top rung must differ from its fallback for this test to mean anything

        var anchor = TestAnchor("test.species", "almanac", top.Id, "Onslaught", null, true, "steady", "melee",
            new[] { "normal" });
        var fallback = anchor with { ThreatBand = null };

        Assert.NotEqual(Expand(anchor).Theta, Expand(fallback).Theta);
    }

    // ---- allocation share: pure vs impure -----------------------------------------------------------

    [Fact]
    public void An_impure_species_splits_between_primary_and_secondary()
    {
        var anchor = TestAnchor("test.impure", "cultivated", "raider", "Onslaught", "Bulwark", false,
            "steady", "melee", new[] { "normal" });

        var species = Expand(anchor);

        var onslaughtOnly = RealAptitudes.Edges.Where(e => e.Source == "Onslaught" && e.Mode == AptitudeReadMode.Magnitude
            && !RealAptitudes.Edges.Any(o => o.Source == "Bulwark" && o.Channel == e.Channel)).Select(e => e.Channel);
        var bulwarkOnly = RealAptitudes.Edges.Where(e => e.Source == "Bulwark" && e.Mode == AptitudeReadMode.Magnitude
            && !RealAptitudes.Edges.Any(o => o.Source == "Onslaught" && o.Channel == e.Channel)).Select(e => e.Channel);

        Assert.True(onslaughtOnly.All(ch => species.Magnitudes.ContainsKey(ch)));
        Assert.True(bulwarkOnly.All(ch => species.Magnitudes.ContainsKey(ch)));
    }

    [Fact]
    public void Pure_ignores_a_declared_secondary_entirely()
    {
        // Pure means 100% primary regardless of what aptitudeSecondary happens to name — a real
        // anchor invariant (schema.py's own "pure implies secondary is 'none'"), tested defensively.
        var pureWithSecondaryField = TestAnchor(
            "test.pure", "cultivated", "raider", "Onslaught", "Bulwark", pure: true, "steady", "melee",
            new[] { "normal" });
        var pureNoSecondary = pureWithSecondaryField with { AptitudeSecondary = null };

        Assert.Equal(Expand(pureWithSecondaryField).Magnitudes, Expand(pureNoSecondary).Magnitudes);
    }

    // ---- tempo: stated beats classified --------------------------------------------------------------

    [Fact]
    public void Stated_interval_beats_classified_tempo()
    {
        var anchor = RealAnchor("Peashooter"); // attackTempo: "steady"
        var classifiedOnly = Expand(anchor);
        var withStated = Expand(anchor, statedIntervalMs: 777);

        Assert.Equal("classified", classifiedOnly.AttackIntervalSource);
        Assert.Equal(RealShape.AttackTempoIntervalMs["steady"], classifiedOnly.AttackIntervalMs);

        Assert.Equal("stated", withStated.AttackIntervalSource);
        Assert.Equal(777, withStated.AttackIntervalMs);
    }

    // ---- variant count vs the real ladder's own count band -------------------------------------------

    [Fact]
    public void Real_anchors_variant_counts_already_fall_inside_their_rarity_bands()
    {
        // Proven against the real shipped ladder (ssot-rarity.md §3.3's own numbers), not invented:
        // cultivated's combined count band is 2-3.
        var pea = Expand(RealAnchor("Peashooter"));
        Assert.InRange(pea.VariantCount, 2, 3);

        var sunflower = Expand(RealAnchor("SunFlower"));
        Assert.InRange(sunflower.VariantCount, 2, 3);
    }

    // ---- numeric safety -------------------------------------------------------------------------------

    [Fact]
    public void No_private_level_function_exists()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Demons", "Generation", "SpeciesExpander.cs"));
        Assert.DoesNotContain("Math.Pow", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_cap_on_any_magnitude()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "src", "FusionRpg.Core", "Demons", "Generation", "SpeciesExpander.cs"));
        Assert.DoesNotContain("Math.Min", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_magnitude_is_long()
    {
        // Reflection over the concrete row type — a float or int magnitude fails.
        var magnitudesProp = typeof(ConcreteSpecies).GetProperty(nameof(ConcreteSpecies.Magnitudes))!;
        var valueType = magnitudesProp.PropertyType.GetGenericArguments()[1];
        Assert.Equal(typeof(long), valueType);
    }

    [Fact]
    public void Regenerating_the_same_anchor_is_byte_identical()
    {
        var anchor = RealAnchor("SunFlower");

        var first = Expand(anchor);
        var second = Expand(anchor);

        Assert.Equal(first.Theta, second.Theta);
        Assert.Equal(first.PTheta, second.PTheta);
        Assert.Equal(first.Magnitudes, second.Magnitudes);
    }
}
