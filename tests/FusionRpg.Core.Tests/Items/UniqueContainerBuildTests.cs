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
/// disconnected `ContainerRow` shape.</summary>
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

    static UniqueContainerLookups Lookups() => new(
        LookupAtom: id => AtomsById.TryGetValue(id, out var a) ? a : null,
        AtomsInFamily: family => Atoms.Where(a => string.Equals(a.FamilyId, family, StringComparison.Ordinal)).ToList());

    // ---- the build golden ---------------------------------------------------------------------------

    [Fact]
    public void Building_a_real_anchor_with_a_variance_slot_produces_the_expected_shape()
    {
        var anchor = Corpus.Single(s => s.SeedId == "unique.ember-harvest-30-001");
        var window = Windows[anchor.RarityId];

        var (container, dropped) = UniqueContainerBuild.From(anchor, window, Lookups());

        Assert.Equal("item.ember-harvest-30-001", container.ContainerId);
        Assert.Equal(ContainerKind.Item, container.Kind);
        Assert.Equal("grafted", container.Rarity);
        Assert.Equal(2, container.Atoms.Count); // this anchor's own two fixedAtoms
        Assert.Equal(1, container.PrefixRolls + container.SuffixRolls); // exactly one roll
        Assert.NotEmpty(container.Pool);
        Assert.Equal(container.MinTier, container.MaxTier); // "MinTier == MaxTier at the authored tier"
        Assert.Empty(dropped); // this family is not known to mix affix classes
    }

    [Fact]
    public void An_anchor_with_no_variance_slot_authors_zero_rolls_and_an_empty_pool()
    {
        var anchor = Corpus.First(s => s.VarianceSlot is null);
        var window = Windows[anchor.RarityId];

        var (container, _) = UniqueContainerBuild.From(anchor, window, Lookups());

        Assert.Equal(0, container.PrefixRolls + container.SuffixRolls);
        Assert.Empty(container.Pool);
        Assert.Null(container.MinTier);
        Assert.Null(container.MaxTier);
    }

    // ---- "the roll count is exactly one" (or zero) — over the WHOLE real corpus ---------------------

    [Fact]
    public void Every_one_of_the_real_144_anchors_builds_and_never_exceeds_one_roll()
    {
        var withSlot = 0;
        var withoutSlot = 0;
        foreach (var anchor in Corpus)
        {
            var window = Windows[anchor.RarityId];
            var (container, _) = UniqueContainerBuild.From(anchor, window, Lookups());

            var totalRolls = container.PrefixRolls + container.SuffixRolls;
            Assert.True(totalRolls <= UniqueLimits.MaxTotalRolls, $"{anchor.SeedId}: {totalRolls} rolls exceeds MaxTotalRolls");
            Assert.Equal(anchor.TotalRolls, totalRolls); // the seed's own TotalRolls property agrees with the built container

            if (anchor.VarianceSlot is null) withoutSlot++; else withSlot++;
        }

        Assert.Equal(144, Corpus.Count);
        Assert.Equal(136, withSlot);
        Assert.Equal(8, withoutSlot);
    }

    // ---- the built container is not just shaped right -- it actually instantiates -------------------

    [Fact]
    public void A_built_container_round_trips_through_the_real_Instantiator()
    {
        var anchor = Corpus.Single(s => s.SeedId == "unique.ember-harvest-30-001");
        var window = Windows[anchor.RarityId];
        var (container, _) = UniqueContainerBuild.From(anchor, window, Lookups());

        var rejection = Instantiator.TryInstantiate(
            container, id => AtomsById.TryGetValue(id, out var a) ? a : null,
            _ => null, rollSeed: 12345, thetaContent: 20, PowerTuningHub.Tuning, out var instance);

        Assert.True(rejection.IsOk, rejection.Detail);
        Assert.NotNull(instance);
        Assert.Equal(2, instance!.Atoms.Count); // the two fixed-core atoms; the pool draw needs a real affix library to resolve, out of this test's own scope
    }

    // ---- guards --------------------------------------------------------------------------------------

    [Fact]
    public void A_mismatched_rarity_window_is_refused()
    {
        var anchor = Corpus.Single(s => s.SeedId == "unique.ember-harvest-30-001"); // rarity: grafted
        var wrongWindow = Windows["heirloom"];

        Assert.Throws<ArgumentException>(() => UniqueContainerBuild.From(anchor, wrongWindow, Lookups()));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var anchor = Corpus.Single(s => s.SeedId == "unique.ember-harvest-30-001");
        var window = Windows[anchor.RarityId];
        Assert.Throws<ArgumentNullException>(() => UniqueContainerBuild.From(null!, window, Lookups()));
        Assert.Throws<ArgumentNullException>(() => UniqueContainerBuild.From(anchor, window, null!));
    }
}
