using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons;

/// <summary>One banner definition (spec-demon-summoning.md).</summary>
public sealed record SummonBannerDef(
    string BannerId,
    int CostPerPull,
    int CostPerTen,
    bool HasElementFocus,
    double FocusWeightMultiplier);

public static class SummonBannerCatalog
{
    public const string StandardRift = "standard-rift";
    public const string ElementFocus = "element-focus";

    public static readonly IReadOnlyList<SummonBannerDef> All = new[]
    {
        new SummonBannerDef(StandardRift, 100, 900, HasElementFocus: false, FocusWeightMultiplier: 1.0),
        new SummonBannerDef(ElementFocus, 120, 1_080, HasElementFocus: true, FocusWeightMultiplier: 3.0)
    };

    public static SummonBannerDef? TryGet(string? bannerId) =>
        All.FirstOrDefault(b => string.Equals(b.BannerId, bannerId, StringComparison.Ordinal));

    /// <summary>
    /// Deterministic focus rotation for the element-focus banner: pure function of the UTC date
    /// (ISO day number / 7 → weekly), so the server resolves it at pull time and records it —
    /// replay returns stored results and never re-derives.
    /// </summary>
    public static ElementTypeId FocusFor(DateOnly utcDate)
    {
        var week = utcDate.DayNumber / 7;
        return ElementRoster.Concrete[week % ElementRoster.Concrete.Count];
    }
}
