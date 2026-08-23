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

    // ---- LegionSupply (spec-loam-legions.md) ----

    /// <summary>
    /// What one bearer contributes to a legion's carrying capacity. Tuned by
    /// `LegionSupplyEconomyTests` against the ideal's 4-8 turn leash target for representative
    /// legion compositions — the same L9-style harness discipline as every other constant here.
    /// </summary>
    public const long CarryPerBearer = 200;

    /// <summary>What one member, fighter or bearer, burns per turn beyond supply. Harness-tuned alongside <see cref="CarryPerBearer"/>.</summary>
    public const long BurnPerMember = 10;

    // ---- loam-structures (spec-loam-structures.md) ----

    /// <summary>
    /// Per-mille, 1000 = unchanged. A well multiplies its own rootbed's seep — a provisional
    /// placeholder like every other constant here, not yet harness-measured against a stated target
    /// (the spec names none for this one, unlike the legion leash's explicit 4-8 turn range).
    /// </summary>
    public const int WellYieldMultiplierMilli = 2000;

    /// <summary>What a well costs to build, spent from the founding legion's own `CarriedLoam`.</summary>
    public const long WellCostMilli = 200;

    /// <summary>What a waystation costs to build. Same spend path as a well's.</summary>
    public const long WaystationCostMilli = 300;

    /// <summary>How many `Production` passes a well takes to finish, decrementing to zero.</summary>
    public const int WellBuildTurns = 2;

    /// <summary>How many `Production` passes a waystation takes to finish — riskier ground, longer build.</summary>
    public const int WaystationBuildTurns = 4;

    /// <summary>
    /// A waystation may only be founded within this many unweighted hops (G5) of a sector the
    /// founder already holds that is itself currently habitable.
    /// </summary>
    public const int WaystationRangeHops = 3;

    // ---- loam-texture (spec-loam-texture.md) ----

    /// <summary>What a granary costs to build.</summary>
    public const long GranaryCostMilli = 150;

    /// <summary>How much a granary raises a sector's storage cap by, on top of <see cref="LoamCapacity"/>.</summary>
    public const long GranaryCapacityBonus = 300;

    /// <summary>How many `Production` passes a granary takes to finish.</summary>
    public const int GranaryBuildTurns = 2;

    /// <summary>How much an actively-fading sector raises `PressureMilli` on each lane-adjacent sector, per turn.</summary>
    public const int ContagionPressurePerTurn = 60;

    /// <summary>The ceiling `PressureMilli` can reach — contagion is a live signal, not an unbounded ratchet.</summary>
    public const int MaxPressureMilli = 300;

    /// <summary>How fast `PressureMilli` decays back toward zero for a sector with no fading neighbour this turn.</summary>
    public const int PressureDecayPerTurn = 40;

    /// <summary>
    /// Per-mille scale on `DecayFor`'s pre-clamp sum while the turn's `CalendarRoll` includes
    /// `Plague`. Applied to the input, never the clamped output — a surge pushes more sectors
    /// toward `MaxDecayMilli`, never past it.
    /// </summary>
    public const int SurgeDecayMultiplierMilli = 1500;

    /// <summary>
    /// How many consecutive turns a `Lost`, barren sector sits neglected before the Unmade spawn
    /// onto it.
    /// </summary>
    public const int UnmadeSpawnAfterTurns = 5;

    /// <summary>How tough a freshly-spawned Unmade warband is, per member.</summary>
    public const int UnmadeMemberHp = 120;

    /// <summary>How many members a freshly-spawned Unmade warband has.</summary>
    public const int UnmadeMemberCount = 2;
}
