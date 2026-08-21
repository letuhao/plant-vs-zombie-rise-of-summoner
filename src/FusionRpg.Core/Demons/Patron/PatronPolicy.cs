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
    public const int SwitchCostSouls = 100;
    public const int AuraClampMilli = 150;
    public const int PerStarMilli = 10;

    /// <summary>The per-match kill-soul ceiling — mirrors the audited earn-v2 cap.</summary>
    public const int KillSoulCap = 50;

    public static int RarityBaseMilli(DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => 20,
        DemonRarity.Rare => 30,
        DemonRarity.Epic => 45,
        _ => 60
    };

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
    public static int KillEarnWithPatron(int countedKills)
    {
        static int SoulsAfter(int earningKills) =>
            Math.Min(KillSoulCap, earningKills + earningKills / 10);
        return SoulsAfter(countedKills + 1) - SoulsAfter(countedKills);
    }
}
