using System.Text.Json;
using FusionRpg.Core.Items;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `affix-legality` (item module 8) against the REAL, shipped affix families (98 at module 8's
/// build, 100 since `g-punisher.json` landed 2026-09-06), `family-overrides.v1.json` and
/// `role-relocation.v1.json`.
/// </summary>
public class RoleFamilyTableTests
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

    static List<AffixFamilySource> LoadFamilies()
    {
        var dir = Path.Combine(RepoRoot(), "data", "seed", "items", "affix-families");
        var result = new List<AffixFamilySource>();
        foreach (var path in Directory.EnumerateFiles(dir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
            {
                var roles = e.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).ToList();
                var frames = e.GetProperty("frames").EnumerateArray().Select(r => r.GetString()!).ToList();
                result.Add(new AffixFamilySource(
                    e.GetProperty("id").GetString()!, roles, frames,
                    e.GetProperty("side").GetString()!, e.GetProperty("kindId").GetString()!));
            }
        }

        return result;
    }

    static FamilyOverrides LoadOverrides() =>
        FamilyOverrides.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "seed", "items", "_registry", "family-overrides.v1.json")));

    static RoleRelocationTable LoadRelocation() =>
        RoleRelocationTable.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "data", "seed", "items", "_registry", "role-relocation.v1.json")));

    static IReadOnlyList<RoleFamilyCell> Derive() => RoleFamilyTable.Derive(LoadFamilies(), LoadOverrides(), LoadRelocation());

    /// <summary>
    /// A corpus-count pin, and it exists to catch a family going MISSING. It is expected to move
    /// upward when the affix-authoring lane ships one: 98 at module 8's build (2026-09-04), 100 after
    /// `g-punisher.json` landed (2026-09-06, commit 5864231), 109 after the phantom-closure pass the
    /// same day authored the nine families real content already referenced (seven `status.apply` into
    /// `g-affliction.json`, two `stat.derived` into `g-elem-power.json`). Bump it deliberately, with the
    /// corpus counted rather than quoted from a stale note — a DROP is the defect this guards.
    /// </summary>
    [Fact]
    public void The_whole_shipped_affix_family_corpus_loads()
    {
        Assert.Equal(109, LoadFamilies().Count);
    }

    [Fact]
    public void The_relocation_artefact_exists_and_covers_every_dropped_family_with_zero_orphans()
    {
        var relocation = LoadRelocation();
        // 619 over the 98-family corpus; 631 once `g-punisher.json`'s two `sense`-legal families got
        // their 6 surviving hybrid-core hosts each (2026-09-06); 673 once the phantom-closure pass
        // added seven more `sense`-legal `g-affliction` families the same day, at the same 6 hosts
        // each (+42). The artefact is DERIVED from the corpus, so this number tracks it -- see
        // `RoleRelocationRowMissing` in ItemSeedValidator, the check that now refuses a corpus family
        // with no row rather than letting it keep t5, and which is what caught the 42.
        Assert.Equal(673, relocation.RowCount);
        Assert.Equal(new[] { "head-guard", "sense", "ward-array" }, relocation.DroppedRoles.OrderBy(s => s));
    }

    [Fact]
    public void A_relocated_family_carries_a_reduced_max_tier_on_its_host()
    {
        var relocation = LoadRelocation();
        // ward-array's atom.shield-capacity is legal on armament-secondary and jewel-major (its own
        // roles list carries no core-guard cell -- ssot-equip-slots.md §4.2's core-guard example is
        // the mechanism's precedent, not a literal claim about this family's roles). Both surviving
        // hosts carry the same reduced ceiling, matching that precedent's shape (5 -> 3).
        Assert.Equal(3, relocation.ReducedMaxTier("armament-secondary", "atom.shield-capacity"));
        Assert.Equal(3, relocation.ReducedMaxTier("jewel-major", "atom.shield-capacity"));
    }

    [Fact]
    public void Head_guard_legal_families_are_45_over_seven_groups()
    {
        var families = LoadFamilies();
        var headGuardFamilies = families.Where(f => f.Roles.Contains("head-guard")).ToList();
        Assert.Equal(45, headGuardFamilies.Count);
    }

    [Fact]
    public void Both_minor_jewels_cap_every_family_at_tier_3()
    {
        var cells = Derive();
        foreach (var role in new[] { "jewel-minor-a", "jewel-minor-b" })
        {
            var roleCells = cells.Where(c => c.RoleId == role).ToList();
            Assert.NotEmpty(roleCells);
            Assert.All(roleCells, c => Assert.True(c.MaxTier <= 3, $"{role}/{c.FamilyId} has max tier {c.MaxTier}"));
        }
    }

    [Fact]
    public void Bulwark_and_savagery_are_absent_from_both_minor_jewels()
    {
        var cells = Derive();
        foreach (var role in new[] { "jewel-minor-a", "jewel-minor-b" })
        {
            Assert.DoesNotContain(cells, c => c.RoleId == role && c.FamilyId == "atom.bulwark");
            Assert.DoesNotContain(cells, c => c.RoleId == role && c.FamilyId == "atom.savagery");
        }
    }

    [Fact]
    public void Jewel_major_is_untouched_by_the_minor_jewel_override()
    {
        // The role-cap and removedFamilies overrides target the twins only. jewel-major's own
        // atom.bulwark cell is separately reduced to 3 by the D3 relocation (bulwark is ALSO legal on
        // both head-guard and sense, two of the three dropped roles) -- that reduction is real and
        // expected, and distinct from the minor-jewel override this test isolates.
        var overrides = LoadOverrides();
        Assert.Null(overrides.RoleCap("jewel-major"));
        Assert.False(overrides.IsRemoved("jewel-major", "atom.bulwark"));
        Assert.False(overrides.IsRemoved("jewel-major", "atom.savagery"));
    }

    [Fact]
    public void The_only_max_tier_overrides_are_the_declared_list()
    {
        var overrides = LoadOverrides();
        Assert.Equal(3, overrides.RoleCap("jewel-minor-a"));
        Assert.Equal(3, overrides.RoleCap("jewel-minor-b"));
        Assert.Null(overrides.RoleCap("jewel-major"));
        Assert.Null(overrides.RoleCap("armament-primary"));
    }

    [Fact]
    public void Item_role_family_is_derived_with_no_authored_cells()
    {
        // 731 (role, family) pairs come straight from the 109 families' own roles lists, before any
        // override narrows it -- reproduced here against the raw corpus, not through Derive(), which
        // additionally applies the minor-jewel removal (2 families x 2 roles = 4 fewer pairs, 727).
        // Was 656/652 over the 98-family corpus at module 8's build; `g-punisher.json`'s two families
        // added 7 roles each -> 670/666 (2026-09-06); the phantom-closure pass the same day added
        // seven `g-affliction` families at 7 roles each (49) plus two `g-elem-power` families at 6
        // each (12) -> +61. Moves with the corpus, same rule as the count pin above.
        var families = LoadFamilies();
        var rawPairs = families.SelectMany(f => f.Roles.Select(r => (Role: r, f.FamilyId))).Distinct().Count();
        Assert.Equal(731, rawPairs);

        var derivedPairs = Derive().Select(c => (c.RoleId, c.FamilyId)).Distinct().Count();
        Assert.Equal(727, derivedPairs);
    }

    [Fact]
    public void The_role_group_matrix_has_sixteen_distinct_roles()
    {
        var families = LoadFamilies();
        var roles = families.SelectMany(f => f.Roles).Distinct(StringComparer.Ordinal).ToHashSet();
        Assert.Equal(16, roles.Count);
        Assert.Contains("standard", roles);
    }

    [Fact]
    public void No_family_is_orphaned_by_the_three_dropped_hybrid_roles()
    {
        var families = LoadFamilies();
        var hybridCore = new HashSet<string>
        {
            "armament-primary", "core-guard", "armament-secondary", "jewel-major", "manipulator",
            "mantle", "girdle", "footing", "infusion", "retinue", "jewel-minor-a", "jewel-minor-b",
        };

        foreach (var dropped in new[] { "ward-array", "head-guard", "sense" })
            foreach (var f in families.Where(f => f.Roles.Contains(dropped)))
                Assert.True(f.Roles.Any(hybridCore.Contains), $"{f.FamilyId} (dropped via '{dropped}') has no surviving hybrid-core host");
    }
}
