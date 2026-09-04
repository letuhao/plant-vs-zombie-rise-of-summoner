namespace FusionRpg.Core.World.Loam;

/// <summary>
/// Every tunable loam constant, in one place with its reasoning — the `MovementPolicy` precedent.
/// Every number here is a provisional placeholder: L9's economy harness, not this file, decides
/// what they should actually be. Picking them here is what makes the harness have something to run
/// against; picking them *well* is L9's job, not L7's. Values live in
/// <c>data/tuning/loam.v{n}.json</c> (tunables-ssot.md T1); <see cref="Configure"/> must run before
/// any rule below is read.
/// </summary>
public static class LoamPolicy
{
    static LoamTuning? _tuning;

    /// <summary>Host-only (Injector/Server startup, or a test's inline construction).</summary>
    public static void Configure(LoamTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static LoamTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "LoamPolicy.Configure(...) has not run. Every loam rule reads data/tuning/loam.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.");

    /// <summary>What one rootbed slot seeps per turn, untended (map finding A10, wave 1's only source).</summary>
    public static long SeepPerTurn => Tuning.Upkeep.SeepPerTurn;

    /// <summary>
    /// Per-sector storage ceiling (spec-loam-model.md: "a single policy constant applied
    /// uniformly" until granaries exist and vary it). Not read by any calculator in this module —
    /// capping happens in `loam-turn`'s Production phase (L12) — but the economy harness (L9)
    /// applies it locally to demonstrate why an uncapped producer would otherwise show a
    /// perpetually-positive net flow, which P1 calls unhealthy on its own.
    /// </summary>
    public static long LoamCapacity => Tuning.Upkeep.LoamCapacity;

    /// <summary>What holding any sector costs, before garrison, development or danger are counted.</summary>
    public static long BaseUpkeepPerSector => Tuning.Upkeep.BaseUpkeepPerSector;

    /// <summary>What one standing garrison member costs per turn — more mouths, more upkeep.</summary>
    public static long GarrisonUpkeepPerMember => Tuning.Upkeep.GarrisonUpkeepPerMember;

    /// <summary>What one level of development adds — a built-up sector costs more to sustain.</summary>
    public static long DevelopmentUpkeepPerLevel => Tuning.Upkeep.DevelopmentUpkeepPerLevel;

    /// <summary>What one band of danger adds — dangerous ground costs more to hold, not just to take.</summary>
    public static long DangerUpkeepPerBand => Tuning.Upkeep.DangerUpkeepPerBand;

    /// <summary>
    /// The `f(DevelopmentLevel, DangerBand)` term from spec-loam-calc.md #3. Its own named function
    /// because "a sector's built-up-ness and its danger both raise the price of holding it" is a
    /// design claim, not an implementation detail, and a claim deserves a name.
    /// </summary>
    public static long DevelopmentAndDangerUpkeep(int developmentLevel, int dangerBand) =>
        (long)developmentLevel * DevelopmentUpkeepPerLevel + (long)dangerBand * DangerUpkeepPerBand;

    // ---- sector-development (spec-sector-development.md §3, empire-economy-ssot.md A8) ----

    /// <summary>
    /// What one level of development adds to a sector's own loam yield — the yield half of A8's
    /// invariant ("development must raise yield faster than it raises upkeep"), read by
    /// <see cref="Growth.DevelopmentYield"/>. Lives beside <see cref="DevelopmentUpkeepPerLevel"/> in
    /// the same tuning file (`data/tuning/loam.v{n}.json`'s `development` block) so the comparison is
    /// readable by inspection, not split across two files.
    /// </summary>
    public static long DevelopmentYieldPerLevel => Tuning.Development.YieldPerLevel;

    // ---- FadePolicy (spec-loam-calc.md #5) ----

    /// <summary>What a surplus sector recovers each turn, fixed — never the fast side of the pair.</summary>
    public static int RecoveryMilli => Tuning.Fade.RecoveryMilli;

    /// <summary>The minimum decay any shortfall causes, even a shortfall of one unit.</summary>
    public static int BaseDecayMilli => Tuning.Fade.BaseDecayMilli;

    /// <summary>How much extra decay each unit of deficit adds, before the ceiling.</summary>
    public static int DecayPerDeficitUnitMilli => Tuning.Fade.DecayPerDeficitUnitMilli;

    /// <summary>How many loam units of deficit one extra milli of decay costs.</summary>
    public static long DecayScaleDivisor => Tuning.Fade.DecayScaleDivisor;

    /// <summary>
    /// The ceiling on a single turn's decay — a deficit does not get to zero a sector's stability
    /// in one step no matter how deep it runs; that is what makes the fade a countdown a player can
    /// react to rather than a trap that springs once.
    /// </summary>
    public static int MaxDecayMilli => Tuning.Fade.MaxDecayMilli;

    /// <summary>
    /// The `Abandon` rule's horizon (spec-loam-ai-survival.md, "still open" — found by measurement,
    /// not chosen here): how many turns of runway, at the current burn rate, before a doomed
    /// component's weakest holding is worth releasing early rather than left to fade on its own.
    /// A short horizon so the AI does not evacuate ground that could still recover on its own.
    /// </summary>
    public static int AbandonmentHorizonTurns => Tuning.Fade.AbandonmentHorizonTurns;

    // ---- LegionSupply (spec-loam-legions.md) ----

    /// <summary>
    /// What one bearer contributes to a legion's carrying capacity. Tuned by
    /// `LegionSupplyEconomyTests` against the ideal's 4-8 turn leash target for representative
    /// legion compositions — the same L9-style harness discipline as every other constant here.
    /// </summary>
    public static long CarryPerBearer => Tuning.LegionSupply.CarryPerBearer;

    /// <summary>What one member, fighter or bearer, burns per turn beyond supply. Harness-tuned alongside <see cref="CarryPerBearer"/>.</summary>
    public static long BurnPerMember => Tuning.LegionSupply.BurnPerMember;

    // ---- loam-structures (spec-loam-structures.md) ----

    /// <summary>
    /// Per-mille, 1000 = unchanged. A well multiplies its own rootbed's seep — a provisional
    /// placeholder like every other constant here, not yet harness-measured against a stated target
    /// (the spec names none for this one, unlike the legion leash's explicit 4-8 turn range).
    /// </summary>
    public static int WellYieldMultiplierMilli => Tuning.Structures.WellYieldMultiplierMilli;

    /// <summary>What a well costs to build, spent from the founding legion's own `CarriedLoam`.</summary>
    public static long WellCostMilli => Tuning.Structures.WellCostMilli;

    /// <summary>What a waystation costs to build. Same spend path as a well's.</summary>
    public static long WaystationCostMilli => Tuning.Structures.WaystationCostMilli;

    /// <summary>How many `Production` passes a well takes to finish, decrementing to zero.</summary>
    public static int WellBuildTurns => Tuning.Structures.WellBuildTurns;

    /// <summary>How many `Production` passes a waystation takes to finish — riskier ground, longer build.</summary>
    public static int WaystationBuildTurns => Tuning.Structures.WaystationBuildTurns;

    /// <summary>
    /// A waystation may only be founded within this many unweighted hops (G5) of a sector the
    /// founder already holds that is itself currently habitable.
    /// </summary>
    public static int WaystationRangeHops => Tuning.Structures.WaystationRangeHops;

    // ---- loam-texture (spec-loam-texture.md) ----

    /// <summary>What a granary costs to build.</summary>
    public static long GranaryCostMilli => Tuning.Structures.GranaryCostMilli;

    /// <summary>How much a granary raises a sector's storage cap by, on top of <see cref="LoamCapacity"/>.</summary>
    public static long GranaryCapacityBonus => Tuning.Structures.GranaryCapacityBonus;

    /// <summary>How many `Production` passes a granary takes to finish.</summary>
    public static int GranaryBuildTurns => Tuning.Structures.GranaryBuildTurns;

    /// <summary>How much an actively-fading sector raises `PressureMilli` on each lane-adjacent sector, per turn.</summary>
    public static int ContagionPressurePerTurn => Tuning.Texture.ContagionPressurePerTurn;

    /// <summary>The ceiling `PressureMilli` can reach — contagion is a live signal, not an unbounded ratchet.</summary>
    public static int MaxPressureMilli => Tuning.Texture.MaxPressureMilli;

    /// <summary>How fast `PressureMilli` decays back toward zero for a sector with no fading neighbour this turn.</summary>
    public static int PressureDecayPerTurn => Tuning.Texture.PressureDecayPerTurn;

    /// <summary>
    /// Per-mille scale on `DecayFor`'s pre-clamp sum while the turn's `CalendarRoll` includes
    /// `Plague`. Applied to the input, never the clamped output — a surge pushes more sectors
    /// toward `MaxDecayMilli`, never past it.
    /// </summary>
    public static int SurgeDecayMultiplierMilli => Tuning.Texture.SurgeDecayMultiplierMilli;

    /// <summary>
    /// How many consecutive turns a `Lost`, barren sector sits neglected before the Unmade spawn
    /// onto it.
    /// </summary>
    public static int UnmadeSpawnAfterTurns => Tuning.Texture.UnmadeSpawnAfterTurns;

    /// <summary>How tough a freshly-spawned Unmade warband is, per member.</summary>
    public static long UnmadeMemberHp => Tuning.Texture.UnmadeMemberHp;

    /// <summary>How many members a freshly-spawned Unmade warband has.</summary>
    public static int UnmadeMemberCount => Tuning.Texture.UnmadeMemberCount;
}
