using FusionRpg.Core.Delve.Events;
using FusionRpg.Core.Tests.Dungeon;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Events;

public class SupplyOverrideTagSeedFileTests
{
    static string SuppliesDir() => Path.Combine(DungeonTestFiles.RepoRoot(), "data", "seed", "dungeon", "supplies");

    [Fact]
    public void LoadAllOverrideTags_null_directory_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SupplyOverrideTagSeedFile.LoadAllOverrideTags(null!));
    }

    [Fact]
    public void LoadAllOverrideTags_a_missing_directory_returns_empty_never_throws()
    {
        Assert.Empty(SupplyOverrideTagSeedFile.LoadAllOverrideTags(Path.Combine(DungeonTestFiles.RepoRoot(), "does-not-exist")));
    }

    /// <summary>Real, measured 2026-09-07: all 31 real shipped supply-extension anchors carry an empty
    /// `overrideTags` array — none has ever authored one yet. A real, honest finding, not a test bug —
    /// this test becomes load-bearing the day a real override tag ships.</summary>
    [Fact]
    public void LoadAllOverrideTags_over_the_real_31_shipped_supplies_is_currently_empty()
    {
        var tags = SupplyOverrideTagSeedFile.LoadAllOverrideTags(SuppliesDir());
        Assert.Empty(tags);
    }
}
