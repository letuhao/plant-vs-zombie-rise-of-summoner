using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// `species-build` G1/G2 (casing-bug fix, 2026-09-05) — the one test that was missing and let the
/// bug ship. Every existing caller of <see cref="SpeciesBuildPlanCatalog"/> (e.g.
/// <c>FusionRpg.Server.Tests.SpeciesBuildEndpointsTests</c>) configures it with a hand-built fixture
/// dictionary that is ALREADY keyed with the correct lowercase runtime id, so none of them could
/// ever notice that the real committed file used a completely different key space (seedsmith-anchor
/// PascalCase, e.g. <c>"FumeShroom"</c>, vs. <see cref="DemonSpeciesCatalog"/>'s lowercase runtime id,
/// e.g. <c>"fumeshroom"</c> — zero exact-string overlap between the two). This file loads the REAL
/// committed <c>data/generated/demons/_species-build-plan.json</c> through the real
/// <see cref="SpeciesBuildPlanReader"/>, against the real compiled <see cref="DemonSpeciesCatalog.All"/>
/// roster — no hand-built fixture anywhere in it, matching <c>SpeciesCatalogDiffTests</c>' own
/// established <c>RepoRoot()</c> convention.
/// </summary>
public class SpeciesBuildPlanCatalogRealFileTests
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

    static IReadOnlyDictionary<string, IReadOnlyDictionary<string, long>> LoadRealPlan()
    {
        var path = Path.Combine(RepoRoot(), "data", "generated", "demons", "_species-build-plan.json");
        return SpeciesBuildPlanReader.Parse(File.ReadAllText(path));
    }

    [Fact]
    public void Real_plan_resolves_a_real_shipped_species_to_a_real_non_empty_vector()
    {
        // The exact defect (found live, 2026-09-05): the committed plan's keys were seedsmith-anchor
        // PascalCase ("FumeShroom"), while DemonSpeciesCatalog's runtime id is lowercase
        // ("fumeshroom") — SharesFor("fumeshroom") silently returned EmptyShares (all-zero
        // aptitudes) for every live species, indistinguishable from the legitimate "not yet
        // classified" case. `tools/DemonBuildPlanGen` now keys the plan by the runtime speciesId,
        // joined via the game's own stable (Side, GameTypeId) identity. This proves the fix against
        // the file actually on disk, not a re-derived expectation.
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        SpeciesBuildPlanCatalog.Configure(LoadRealPlan());

        var shares = SpeciesBuildPlanCatalog.SharesFor("fumeshroom");

        Assert.NotEmpty(shares);
        Assert.Equal(1000, shares.Values.Sum());
    }

    [Fact]
    public void Real_plan_keys_are_all_real_runtime_species_ids_never_anchor_text()
    {
        // A regression guard for the whole BUG CLASS, not just the one species above: every key in
        // the committed file must be a live DemonSpeciesCatalog id. A PascalCase (or any other
        // unrecognised) key sneaking back in means the generator's (Side, GameTypeId) join broke.
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        var plan = LoadRealPlan();

        var unknownKeys = plan.Keys.Where(k => !DemonSpeciesCatalog.IsKnown(k)).ToList();

        Assert.True(unknownKeys.Count == 0,
            $"plan has {unknownKeys.Count} key(s) that are not real runtime species ids: " +
            string.Join(", ", unknownKeys));
    }

    /// <summary>
    /// G2 — a species with no plan entry is legitimate BY DESIGN (still <c>unresolved</c> on a voted
    /// classification field — <see cref="SpeciesBuildPlanCatalog.SharesFor"/>'s own documented
    /// contract), but which species that is must be a named, checked-in fact, not something that can
    /// silently grow. If this set ever changes, this test fails and NAMES exactly what changed,
    /// instead of staying green through a regression of G1's own bug class or a new species shipping
    /// with no plan behind it — this is the test that would have caught G1 before it shipped.
    ///
    /// <para>Investigated 2026-09-05 (tasks/species-build-todo.md, G3 findings): of these 17,
    /// <c>allpeater</c> has a real anchor (<c>AllPeater</c>, plant/1347) that is unresolved on
    /// <c>aptitudePrimary</c>; the other 16 have NO matching anchor at all in the raw seed corpus by
    /// (Side, GameTypeId) — a bigger gap than "unresolved," never authored for these specific
    /// (side, gameTypeId) slots at all.</para>
    /// </summary>
    static readonly IReadOnlyList<string> KnownMissingPlanSpecies = new[]
    {
        "allpeater", "cherrygatling", "cherrypaperzombie", "cornpot", "dancepolzombie", "dolldiamond",
        "dollsilver", "doublecherry", "doublesnow", "driverzombie", "hypnojalapeno", "hypnopeashooter",
        "icecaltrop", "ironpeazombie", "jalagatling", "jalapeno", "jalastar",
    };

    [Fact]
    public void Species_with_no_real_plan_entry_matches_the_named_checked_in_allowlist()
    {
        DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        var plan = LoadRealPlan();

        var missing = DemonSpeciesCatalog.All
            .Select(s => s.SpeciesId)
            .Where(id => !plan.ContainsKey(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var expected = KnownMissingPlanSpecies.OrderBy(id => id, StringComparer.Ordinal).ToList();

        var newlyMissing = missing.Except(expected).ToList();
        var newlyCovered = expected.Except(missing).ToList();

        Assert.True(newlyMissing.Count == 0 && newlyCovered.Count == 0,
            "the set of shipped species with no species-build plan entry has changed since " +
            "tasks/species-build-todo.md G3 was last investigated. " +
            (newlyMissing.Count > 0 ? $"NEWLY MISSING, investigate why (regression or new unclassified species): {string.Join(", ", newlyMissing)}. " : "") +
            (newlyCovered.Count > 0 ? $"NEWLY COVERED (update KnownMissingPlanSpecies above — one of these got classified/planned): {string.Join(", ", newlyCovered)}. " : ""));
    }
}
