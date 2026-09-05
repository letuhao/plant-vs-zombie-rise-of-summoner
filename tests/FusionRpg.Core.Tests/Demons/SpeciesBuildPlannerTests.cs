using FusionRpg.Core.Demons.Generation;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>`species-build` T1.5/T1.6 (module 4, `redistribution-plan`) — the closed-form planner.
/// Pure and Core-only (no file IO in the type under test); this file's own real-corpus test is the one
/// place file IO appears, mirroring `SpeciesCatalogDiffTests`' established `RepoRoot()` convention.</summary>
public class SpeciesBuildPlannerTests
{
    static readonly SpeciesBuildTuning Tuning = new(
        SchemaVersion: 1, Version: 1,
        ParityFloorPermille: 50, ParityCeilingPermille: 200,
        LeanMinPermille: 350, LeanMaxPermille: 600,
        CrowdingFactor: 633, SecondarySharePermille: 300,
        MaxAptitudesPerSpecies: 5, MinAptitudesPerSpecies: 2,
        RespecBasePrice: 50, RespecEscalationPermille: 500, RespecDecayDays: 3);

    static AnchorRow Anchor(
        string speciesId, string primary, string? secondary = null, bool pure = true,
        string side = "plant", int gameTypeId = 1) => new(
        SpeciesId: speciesId, Rarity: "chaff", ThreatBand: null,
        AptitudePrimary: primary, AptitudeSecondary: secondary, Pure: pure,
        AttackTempo: "steady", Reach: "melee", Variants: Array.Empty<string>(),
        Side: side, GameTypeId: gameTypeId, ElementPrimary: "fire", ElementSecondary: null,
        DeployMode: "PlantAvatar", Acquisition: new[] { "Summonable" }, Traits: Array.Empty<string>());

    /// <summary>A small synthetic corpus with the SAME shape of imbalance the spec's own audit finding
    /// A7 describes: one aptitude ("Onslaught") massively over-represented, four barely present at
    /// all — deliberately not uniform, so a planner that ignored crowding would fail Phase 3 here too.</summary>
    static List<AnchorRow> SkewedCorpus(int count = 200)
    {
        var species = new List<AnchorRow>();
        for (var i = 0; i < count; i++)
        {
            // ~40% Onslaught, the rest spread thinly across the other eleven (including four that get
            // almost none), matching the real corpus's own measured skew (39.5% / four sharing 2.3%).
            var primary = i switch
            {
                _ when i % 100 < 40 => "Onslaught",
                _ when i % 100 < 42 => "Vigor",
                _ when i % 100 < 44 => "Might",
                _ when i % 100 < 46 => "Composure",
                _ when i % 100 < 48 => "Ferocity",
                _ => new[] { "Fortitude", "Agility", "Pierce", "Focus", "Bulwark", "Retribution", "Precision" }[i % 7]
            };
            species.Add(Anchor($"synth-{i:D4}", primary, pure: true));
        }
        return species;
    }

    [Fact]
    public void Determinism_two_runs_over_the_same_corpus_are_byte_identical()
    {
        var corpus = SkewedCorpus();
        var a = SpeciesBuildPlanner.Plan(corpus, Tuning);
        var b = SpeciesBuildPlanner.Plan(corpus, Tuning);
        Assert.Equal(SpeciesBuildPlanSerializer.Canonical(a.Vectors), SpeciesBuildPlanSerializer.Canonical(b.Vectors));
    }

    [Fact]
    public void Determinism_shuffled_input_order_produces_the_same_plan()
    {
        var corpus = SkewedCorpus();
        var shuffled = corpus.AsEnumerable().Reverse().ToList();
        // A second, differently-shuffled copy, not just reversed-once, so this isn't accidentally
        // insertion-order-preserving by luck of a single swap pattern.
        var rng = new Random(1234);
        var shuffled2 = corpus.OrderBy(_ => rng.Next()).ToList();

        var ordered = SpeciesBuildPlanner.Plan(corpus, Tuning);
        var reversed = SpeciesBuildPlanner.Plan(shuffled, Tuning);
        var random = SpeciesBuildPlanner.Plan(shuffled2, Tuning);

        var canonical = SpeciesBuildPlanSerializer.Canonical(ordered.Vectors);
        Assert.Equal(canonical, SpeciesBuildPlanSerializer.Canonical(reversed.Vectors));
        Assert.Equal(canonical, SpeciesBuildPlanSerializer.Canonical(random.Vectors));
    }

    [Fact]
    public void No_single_primary_every_vector_has_at_least_minAptitudesPerSpecies_non_zero_entries()
    {
        // An all-pure synthetic corpus (decision 3's own stated failure mode) must still satisfy it.
        var corpus = SkewedCorpus();
        var result = SpeciesBuildPlanner.Plan(corpus, Tuning);
        foreach (var v in result.Vectors)
            Assert.True(v.SharePermille.Count >= Tuning.MinAptitudesPerSpecies,
                $"{v.SpeciesId} has only {v.SharePermille.Count} non-zero entries");
    }

    [Fact]
    public void Every_vector_sums_to_exactly_1000_including_awkward_remainders()
    {
        // An odd species count (997) forces non-round crowding/remainder fractions everywhere.
        var corpus = SkewedCorpus(997);
        var result = SpeciesBuildPlanner.Plan(corpus, Tuning);
        foreach (var v in result.Vectors)
            Assert.Equal(1000, v.SharePermille.Values.Sum());
    }

    [Fact]
    public void The_favour_is_never_overridden_every_vectors_top_share_is_its_classified_primary()
    {
        var corpus = SkewedCorpus();
        var byId = corpus.ToDictionary(a => a.SpeciesId, a => a.AptitudePrimary, StringComparer.Ordinal);
        var result = SpeciesBuildPlanner.Plan(corpus, Tuning);
        foreach (var v in result.Vectors)
        {
            var top = v.SharePermille.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First();
            Assert.Equal(byId[v.SpeciesId], top.Key);
        }
    }

    [Fact]
    public void Pure_anchor_that_echoes_its_primary_as_secondary_never_corrupts_the_vector_sum()
    {
        // Real corpus defect found running the CLI for real (HypnoCattailGirl/ObsidianWallNut):
        // pure=true anchors that still set aptitudeSecondary == aptitudePrimary rather than the
        // "none" sentinel. Trusting AptitudeSecondary alone overwrites vector[primary] with a smaller
        // secondary share via the same dictionary key, corrupting the sum below 1000.
        var corpus = SkewedCorpus();
        corpus.Add(Anchor("echo-secondary", "Vigor", secondary: "Vigor", pure: true));
        var result = SpeciesBuildPlanner.Plan(corpus, Tuning);
        var vector = result.Vectors.Single(v => v.SpeciesId == "echo-secondary");
        Assert.Equal(1000, vector.SharePermille.Values.Sum());
        Assert.Equal("Vigor", vector.SharePermille.OrderByDescending(kv => kv.Value).First().Key);
    }

    [Fact]
    public void Refusal_deliberately_infeasible_tunables_name_the_offending_aptitudes()
    {
        // A ceiling far below what Onslaught's crowding alone forces even at leanMin.
        var infeasible = Tuning with { ParityCeilingPermille = 50 };
        var corpus = SkewedCorpus();
        var ex = Assert.Throws<SpeciesBuildRefusal>(() => SpeciesBuildPlanner.Plan(corpus, infeasible));
        Assert.Contains("Onslaught", ex.OffendingShares.Keys);
        Assert.Contains("Onslaught", ex.Message);
    }

    [Fact]
    public void Crowding_behaves_a_crowded_primary_leans_measurably_less_than_a_rare_one()
    {
        var corpus = SkewedCorpus();
        var result = SpeciesBuildPlanner.Plan(corpus, Tuning);

        var crowdedLean = result.Vectors.First(v => v.SharePermille.ContainsKey("Onslaught")
            && v.SharePermille["Onslaught"] == v.SharePermille.Values.Max()).SharePermille["Onslaught"];
        // Ferocity is one of the four barely-represented primaries (2/100 of the corpus).
        var rareVector = result.Vectors.First(v => corpus.Single(a => a.SpeciesId == v.SpeciesId).AptitudePrimary == "Ferocity");
        var rareLean = rareVector.SharePermille["Ferocity"];

        Assert.True(rareLean > crowdedLean,
            $"rare-primary lean ({rareLean}) should exceed crowded-primary lean ({crowdedLean})");
    }

    [Fact]
    public void Overflow_an_extreme_corpus_throws_rather_than_wraps()
    {
        // Not an extreme species COUNT (that's just slow) but an extreme TUNING value multiplied
        // against a real permille, forcing the widened multiply in Phase 1 past long range.
        var corpus = SkewedCorpus(10);
        var extreme = Tuning with { CrowdingFactor = long.MaxValue / 10 };
        Assert.Throws<OverflowException>(() => SpeciesBuildPlanner.Plan(corpus, extreme));
    }

    [Fact]
    public void Band_is_satisfied_on_the_real_corpus()
    {
        // The acceptance test (spec's own success criterion #2) — pass/fail, not a report. Reads the
        // real classified anchors and the real shipped tuning, exactly as tools/DemonBuildPlanGen does.
        var repoRoot = RepoRoot();
        var seedRoot = Path.Combine(repoRoot, "data", "seed", "demons", "species");
        var realTuning = SpeciesBuildTuningLoader.Parse(
            File.ReadAllText(Path.Combine(repoRoot, "data", "tuning", "species-build.v1.json")));

        var anchors = new List<AnchorRow>();
        foreach (var file in Directory.GetFiles(seedRoot, "*.json", SearchOption.AllDirectories)
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).StartsWith('_')) continue;
            anchors.AddRange(AnchorRowReader.ReadAll(File.ReadAllText(file)));
        }
        var resolved = anchors.Where(a => SpeciesExpander.UnresolvedFields(a).Count == 0).ToList();
        Assert.NotEmpty(resolved);

        // Throws SpeciesBuildRefusal (test failure) if the real corpus falls outside the shipped band.
        var result = SpeciesBuildPlanner.Plan(resolved, realTuning);
        Assert.Equal(12, result.CorpusSharePermille.Count);
    }

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
}
