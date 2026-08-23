namespace FusionRpg.Core.World.Loam;

/// <summary>
/// Every tunable loam constant, in one place with its reasoning — the `MovementPolicy` precedent.
/// Every number here is a provisional placeholder: L9's economy harness, not this file, decides
/// what they should actually be. Picking them here is what makes the harness have something to run
/// against; picking them *well* is L9's job, not L7's.
/// </summary>
public static class LoamPolicy
{
    /// <summary>What one rootbed slot seeps per turn, untended (map finding A10, wave 1's only source).</summary>
    public const long SeepPerTurn = 50;

    /// <summary>
    /// Per-sector storage ceiling (spec-loam-model.md: "a single policy constant applied
    /// uniformly" until granaries exist and vary it). Not read by any calculator in this module —
    /// capping happens in `loam-turn`'s Production phase (L12) — but the economy harness (L9)
    /// applies it locally to demonstrate why an uncapped producer would otherwise show a
    /// perpetually-positive net flow, which P1 calls unhealthy on its own.
    /// </summary>
    public const long LoamCapacity = 300;

    /// <summary>What holding any sector costs, before garrison, development or danger are counted.</summary>
    public const long BaseUpkeepPerSector = 10;

    /// <summary>What one standing garrison member costs per turn — more mouths, more upkeep.</summary>
    public const long GarrisonUpkeepPerMember = 2;

    /// <summary>What one level of development adds — a built-up sector costs more to sustain.</summary>
    public const long DevelopmentUpkeepPerLevel = 5;

    /// <summary>What one band of danger adds — dangerous ground costs more to hold, not just to take.</summary>
    public const long DangerUpkeepPerBand = 3;

    /// <summary>
    /// The `f(DevelopmentLevel, DangerBand)` term from spec-loam-calc.md #3. Its own named function
    /// because "a sector's built-up-ness and its danger both raise the price of holding it" is a
    /// design claim, not an implementation detail, and a claim deserves a name.
    /// </summary>
    public static long DevelopmentAndDangerUpkeep(int developmentLevel, int dangerBand) =>
        (long)developmentLevel * DevelopmentUpkeepPerLevel + (long)dangerBand * DangerUpkeepPerBand;

    // ---- FadePolicy (spec-loam-calc.md #5) ----

    /// <summary>What a surplus sector recovers each turn, fixed — never the fast side of the pair.</summary>
    public const int RecoveryMilli = 20;

    /// <summary>The minimum decay any shortfall causes, even a shortfall of one unit.</summary>
    public const int BaseDecayMilli = 40;

    /// <summary>How much extra decay each unit of deficit adds, before the ceiling.</summary>
    public const int DecayPerDeficitUnitMilli = 1;

    /// <summary>How many loam units of deficit one extra milli of decay costs.</summary>
    public const long DecayScaleDivisor = 5;

    /// <summary>
    /// The ceiling on a single turn's decay — a deficit does not get to zero a sector's stability
    /// in one step no matter how deep it runs; that is what makes the fade a countdown a player can
    /// react to rather than a trap that springs once.
    /// </summary>
    public const int MaxDecayMilli = 300;

    /// <summary>
    /// The `Abandon` rule's horizon (spec-loam-ai-survival.md, "still open" — found by measurement,
    /// not chosen here): how many turns of runway, at the current burn rate, before a doomed
    /// component's weakest holding is worth releasing early rather than left to fade on its own.
    /// A short horizon so the AI does not evacuate ground that could still recover on its own.
    /// </summary>
    public const int AbandonmentHorizonTurns = 3;
}
