using FusionRpg.Core.Vfx;
using Xunit;

namespace FusionRpg.Core.Tests.Vfx;

/// <summary>Locks vfx-ssot.md §7 constants and §6 catalog validity.</summary>
public class VfxRulesAndCatalogTests
{
    [Fact]
    public void Rules_constants_match_ssot_locks()
    {
        Assert.Equal(64, VfxRules.FloaterCap);
        Assert.Equal(24, VfxRules.BurstCap);
        Assert.Equal(0.9f, VfxRules.FloaterLifeSeconds);
        Assert.Equal(0.55f, VfxRules.BurstLifeSeconds);
        Assert.Equal(56f, VfxRules.RisePixels);
        Assert.Equal(0.05f, VfxRules.FloaterRateLimitSeconds);
        Assert.Equal(0.15f, VfxRules.BurstRateLimitSeconds);
        Assert.Equal(32, VfxRules.GlobalCuePerTickCap);
        Assert.Equal(1.25f, VfxRules.CritFontScale);
    }

    [Fact]
    public void Seed_catalog_contains_v1_roster_and_validates()
    {
        var catalog = new VfxCatalog();
        catalog.ReplaceAll(VfxSeedCatalog.CreateAll());
        Assert.True(catalog.TryGet(VfxCueIds.CombatHit, out var hit));
        Assert.True(catalog.TryGet(VfxCueIds.CombatHeal, out var heal));
        Assert.True(catalog.TryGet(VfxCueIds.DebugProbe, out var probe));
        Assert.Equal(3, catalog.Ids.Count);

        // combat.hit = floater + burst; heal = floater only; probe = fixed-color burst.
        Assert.Equal(2, hit.Primitives.Count);
        Assert.Equal(VfxPrimitiveKind.Floater, hit.Primitives[0].Kind);
        Assert.Equal(VfxPrimitiveKind.Burst, hit.Primitives[1].Kind);
        Assert.Single(heal.Primitives);
        Assert.Equal(VfxPrimitiveKind.Floater, heal.Primitives[0].Kind);
        Assert.Single(probe.Primitives);
        Assert.Equal(VfxColorSourceKind.Fixed, probe.Primitives[0].Color);
        Assert.Equal(VfxSeedCatalog.ProbeOrange, probe.Primitives[0].FixedRgb);
    }

    [Fact]
    public void Catalog_lookup_is_case_insensitive_and_misses_cleanly()
    {
        var catalog = new VfxCatalog();
        catalog.ReplaceAll(VfxSeedCatalog.CreateAll());
        Assert.True(catalog.TryGet("COMBAT.HIT", out _));
        Assert.False(catalog.TryGet("status.unknown.apply", out _));
        Assert.False(catalog.TryGet("", out _));
        Assert.False(catalog.TryGet(null, out _));
    }

    [Fact]
    public void Validate_rejects_broken_recipes()
    {
        Assert.Throws<ArgumentException>(() => VfxCatalog.Validate(new VfxRecipe { CueId = "" }));
        Assert.Throws<ArgumentException>(() => VfxCatalog.Validate(new VfxRecipe { CueId = "x" }));
        Assert.Throws<ArgumentException>(() => VfxCatalog.Validate(new VfxRecipe
        {
            CueId = "x",
            Primitives = new[] { new VfxPrimitiveSpec { LifeSeconds = 0f } }
        }));
        Assert.Throws<ArgumentException>(() => VfxCatalog.Validate(new VfxRecipe
        {
            CueId = "x",
            Primitives = new[] { new VfxPrimitiveSpec { LifeSeconds = 1f, Count = 0 } }
        }));
        Assert.Throws<ArgumentException>(() => VfxCatalog.Validate(new VfxRecipe
        {
            CueId = "x",
            Primitives = new[] { new VfxPrimitiveSpec { LifeSeconds = 1f, Count = 100 } }
        }));
    }
}
