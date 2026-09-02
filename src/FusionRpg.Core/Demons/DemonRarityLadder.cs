namespace FusionRpg.Core.Demons;

/// <summary>
/// Named ordinal arithmetic over <see cref="DemonRarity"/> (spec-rarity-migration.md §3, §7 step 3).
/// A bare <c>(int)r Â± n</c> or <c>r &gt;= DemonRarity.X</c> compiles at any enum width and silently
/// changes what fraction of the ladder it covers when the ladder widens — these helpers exist so that
/// intent survives a width change instead of being re-derived from the ordinal by eye. A guard test
/// forbids the bare forms outside this file.
/// </summary>
public static class DemonRarityLadder
{
    public const int RungCount = 10;

    /// <summary>The next rung up. Throws at the top — promotion logic must check
    /// <see cref="IsTopRung"/> first, matching every existing call site's own guard.</summary>
    public static DemonRarity OneRungAbove(DemonRarity rarity)
    {
        var next = (int)rarity + 1;
        if (next >= RungCount)
            throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "already the top rung (Almanac)");
        return (DemonRarity)next;
    }

    /// <summary>The rung directly below. Throws at the bottom.</summary>
    public static DemonRarity OneRungBelow(DemonRarity rarity) => RungsBelow(rarity, 1);

    /// <summary>Clamped at the bottom (Chaff) rather than throwing — the shape
    /// <c>DemonRecipeCatalog</c>'s "eligible input rarity" search needs: a recipe for a low-rung
    /// output should still resolve to Chaff, not fail, when asking for "N rungs below."</summary>
    public static DemonRarity RungsBelow(DemonRarity rarity, int rungs)
    {
        var target = (int)rarity - rungs;
        return (DemonRarity)Math.Max(0, target);
    }

    /// <summary>True at the top of the ladder (Almanac) — the replacement for every
    /// <c>== DemonRarity.Legendary</c>/<c>!= DemonRarity.Legendary</c> "is this the cap" check.</summary>
    public static bool IsTopRung(DemonRarity rarity) => rarity == DemonRarity.Almanac;

    /// <summary>True at the bottom of the ladder (Chaff).</summary>
    public static bool IsBottomRung(DemonRarity rarity) => rarity == DemonRarity.Chaff;

    /// <summary>Ordinal-safe "at least this rung" — the direct replacement for
    /// <c>rarity &gt;= DemonRarity.X</c>. Named so a reader sees the THRESHOLD, not an inlined
    /// ordinal comparison that silently covers a different fraction of the ladder once it widens.</summary>
    public static bool AtLeast(DemonRarity rarity, DemonRarity threshold) => (int)rarity >= (int)threshold;

    /// <summary>Ordinal-safe "at most this rung."</summary>
    public static bool AtMost(DemonRarity rarity, DemonRarity threshold) => (int)rarity <= (int)threshold;

    public static IReadOnlyList<DemonRarity> All { get; } =
        Enum.GetValues<DemonRarity>().OrderBy(r => (int)r).ToArray();
}
