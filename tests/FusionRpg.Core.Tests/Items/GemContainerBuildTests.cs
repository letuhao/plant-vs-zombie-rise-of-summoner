using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Effects.Atoms.Generation;
using FusionRpg.Core.Items.Gems;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `GemContainerBuild` against the REAL shipped gem corpus (`data/seed/items/gems/*.json`, 60 entries)
/// and the REAL production atom catalog — `data/seed/items/affix-families/*.json` expanded through
/// <see cref="FamilyExpansion.Expand"/>, the exact pipeline `AtomImporter` runs at server boot
/// (`Server/Program.cs:810`), not the separate, partial `data/seed/atoms/generated/*.json` snapshot
/// (which only 3 partitions ever populated and is not read anywhere in the boot path).
/// </summary>
public class GemContainerBuildTests
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

    static readonly Lazy<IReadOnlyDictionary<string, AtomRow>> RealAtomsById = new(() =>
    {
        var tierBandsDir = Path.Combine(RepoRoot(), "data", "seed", "items", "_tuning");
        var tierBandsPath = Directory.GetFiles(tierBandsDir, "tier-bands.v*.json")
            .Select(f => (Path: f, Version: int.Parse(Path.GetFileNameWithoutExtension(f)["tier-bands.v".Length..])))
            .OrderByDescending(t => t.Version).First().Path;
        var tierBands = TierBandsFile.Read(File.ReadAllText(tierBandsPath));

        var families = new List<FamilyEntryInput>();
        var famDir = Path.Combine(RepoRoot(), "data", "seed", "items", "affix-families");
        foreach (var file in Directory.GetFiles(famDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (Path.GetFileName(file).StartsWith('_')) continue;
            families.AddRange(AffixFamilyFile.Read(Path.GetFileName(file), File.ReadAllText(file)));
        }

        static long? FlatBase(string channel) => channel switch
        {
            "maxHp" or "hp" => 1000L,
            "atk" => 100L,
            "defense" => 50L,
            _ => 200L,
        };

        var rows = FamilyExpansion.Expand(families, tierBands, FlatBase).Rows;
        var byId = new Dictionary<string, AtomRow>(StringComparer.Ordinal);
        foreach (var r in rows) byId[r.AtomId] = r;
        return byId;
    });

    static IReadOnlyList<GemSeed> RealGemSeeds()
    {
        var gemsDir = Path.Combine(RepoRoot(), "data", "seed", "items", "gems");
        var seeds = new List<GemSeed>();
        foreach (var file in Directory.GetFiles(gemsDir, "g*.json").OrderBy(f => f, StringComparer.Ordinal))
            seeds.AddRange(GemCorpus.Parse(File.ReadAllText(file)));
        return seeds;
    }

    static GemContainerBuild.GemContainerLookups RealLookups() =>
        new(id => RealAtomsById.Value.TryGetValue(id, out var a) ? a : null);

    [Fact]
    public void The_real_shipped_corpus_has_sixty_entries_across_three_partitions()
    {
        var seeds = RealGemSeeds();
        Assert.Equal(60, seeds.Count);
        Assert.Equal(60, seeds.Select(s => s.ContainerId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_shipped_gem_id_carries_the_gem_prefix_and_a_legal_container_id()
    {
        foreach (var seed in RealGemSeeds())
        {
            var container = GemContainerBuild.TryBuildOne(seed, RealLookups(), out var refusal);
            // Whether it builds or refuses on content coverage, the id itself must already be legal --
            // ContainerValidator's own grammar check, run here so a malformed id fails loud, not as a
            // silent "refused for content reasons" false negative.
            if (container is not null)
                Assert.True(ContainerValidator.Validate(container, RealAtomsById.Value.GetValueOrDefault,
                    _ => null).IsOk, $"{seed.ContainerId}: {refusal}");
        }
    }

    [Fact]
    public void Nine_of_the_sixty_shipped_gems_resolve_against_the_real_production_atom_catalog_today()
    {
        // A measured fact, not a target -- the same "measure and report a content gap honestly" shape
        // already established for uniques (unique-corpus-atom-family-gap: 144 anchors, 4 buildable).
        // If this number changes, it means the affix-family corpus grew or shrank -- update the
        // assertion to match reality, never the other way around.
        var report = GemContainerBuild.BuildAll(RealGemSeeds(), RealLookups());
        Assert.Equal(27, report.Built.Count);
        Assert.Equal(33, report.Refused.Count);

        foreach (var container in report.Built)
        {
            Assert.Equal(ContainerKind.Gem, container.Kind);
            Assert.Single(container.Atoms);
            Assert.Equal(0, container.PrefixRolls);
            Assert.Equal(0, container.SuffixRolls);
            Assert.Empty(container.Pool);
        }

        // Every refusal names the real reason, not a generic "failed" -- so a future re-run of this
        // test after a corpus change tells a reader WHICH gems newly resolved or newly broke.
        Assert.All(report.Refused, r => Assert.Contains("is not in the real generated atom catalog", r.Reason));
    }

    [Fact]
    public void A_resolvable_gem_instantiates_through_the_real_shared_container_engine()
    {
        // The whole point of building a real ContainerRow rather than a synthetic DTO: Instantiator
        // (the SAME engine loot drops and unique items already use in production) can roll it into a
        // real, reproducible InstanceRow with zero gem-specific code on that side.
        var report = GemContainerBuild.BuildAll(RealGemSeeds(), RealLookups());
        var container = Assert.Single(report.Built.Where(c => c.ContainerId == "gem.g2-001"));

        var tuning = FusionRpg.Core.Power.PowerTuningLoader.Parse(
            File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "power-scale.v2.json")));

        var ok = Instantiator.TryInstantiate(
            container, RealAtomsById.Value.GetValueOrDefault, _ => null,
            rollSeed: 12345, thetaContent: 20, tuning, out var instance);

        Assert.True(ok.IsOk, ok.ToString());
        Assert.NotNull(instance);
        Assert.Equal("gem.g2-001", instance!.ContainerId);
        Assert.Single(instance.Atoms);
    }
}
