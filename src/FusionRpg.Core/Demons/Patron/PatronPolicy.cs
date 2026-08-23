namespace FusionRpg.Core.Demons.Patron;

/// <summary>Computed patron aura — per-mille combat bonuses on the patron's element channels.
/// Primary carries full power + half defense; a secondary element gets half of each.</summary>
public sealed record PatronAura(
    string ElementPrimary, string? ElementSecondary,
    int PowerMilli, int DefenseMilli,
    int SecondaryPowerMilli, int SecondaryDefenseMilli);

/// <summary>
/// Patron rules (spec-patron-demon.md, owner locks 2026-08-21). Numbers are spec-locked; tuning
/// is ask-first. The kill-earn shape here applies ONLY when a patron is set — the audited
/// SoulEarnPolicy.KillEarn path stays byte-identical otherwise, and the 50-SOUL cap holds in
/// both shapes (the bonus only reaches it sooner).
/// </summary>
public static class PatronPolicy
{
    static PatronTuning? _tuning;

    /// <summary>Host-only (Injector/Server startup, or a test's inline construction).</summary>
    public static void Configure(PatronTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static PatronTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "PatronPolicy.Configure(...) has not run. Every patron rule reads data/tuning/patron.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static long SwitchCostSouls => Tuning.SwitchCostSouls;
    public static int AuraClampMilli => Tuning.AuraClampMilli;
    public static int PerStarMilli => Tuning.PerStarMilli;

    /// <summary>The per-match kill-soul ceiling — mirrors the audited earn-v2 cap. Named for
    /// deletion by caps-reconcile (power-plan.md T3.6, not yet authorized).</summary>
    public static int KillSoulCap => Tuning.KillSoulCap;

    public static int RarityBaseMilli(DemonRarity rarity) =>
        Tuning.RarityBaseMilli.TryGetValue(rarity, out var v) ? v : Tuning.RarityBaseMilli[DemonRarity.Legendary];

    public static int AuraMilli(DemonRarity rarity, int star, long level) =>
        (int)Math.Clamp(RarityBaseMilli(rarity) + (long)PerStarMilli * star + level, 0, AuraClampMilli);

    public static PatronAura Aura(
        DemonRarity rarity, int star, long level, string elementPrimary, string? elementSecondary)
    {
        var power = AuraMilli(rarity, star, level);
        var hasSecondary = !string.IsNullOrWhiteSpace(elementSecondary);
        return new PatronAura(
            elementPrimary,
            hasSecondary ? elementSecondary : null,
            power,
            power / 2,
            hasSecondary ? power / 2 : 0,
            hasSecondary ? power / 4 : 0);
    }

    /// <summary>
    /// Kill-earn delta for the (counted+1)-th earning kill with a patron set: +1 base, +1 on
    /// every 10th, expressed as a running-total difference so the 50-soul cap is exact at the
    /// boundary instead of overshooting on a bonus kill.
    /// </summary>
    public static long KillEarnWithPatron(int countedKills)
    {
        static long SoulsAfter(int earningKills) =>
            Math.Min(KillSoulCap, earningKills + earningKills / 10);
        return SoulsAfter(countedKills + 1) - SoulsAfter(countedKills);
    }
}
