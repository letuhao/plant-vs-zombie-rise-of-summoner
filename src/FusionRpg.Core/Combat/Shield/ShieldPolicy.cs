namespace FusionRpg.Core.Combat.Shield;

/// <summary>
/// Shield policy constants — shield-system-spec.md §2.4/§2.5, owner decisions 8–9.
/// Permille ints; changing any value is an Ask-first (spec §8) and breaks the math goldens.
/// </summary>
public static class ShieldPolicy
{
    /// <summary>Matchup share K at permille scale (0.25). Own constant, decoupled from combat MatchupShareK.</summary>
    public const long MatchupShareKPm = 250;

    /// <summary>Chip floor (0.10× input): toughness saturates at 10× efficiency — immunity impossible.</summary>
    public const long ChipFloorKPm = 100;

    /// <summary>Pen cap (3× input): pen at best triples shield burn.</summary>
    public const long PenCapKPm = 3000;

    public const int MaxShieldsPerActor = 3;

    // Default drain priorities (HIGHER drains first) — outer-to-core, owner decision 9.
    public const int PriorityAura = 30;
    public const int PrioritySkill = 20;
    public const int PriorityInnate = 10;
}
