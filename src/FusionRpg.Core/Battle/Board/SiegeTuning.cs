using System.Text.Json;

namespace FusionRpg.Core.Battle.Board;

/// <summary>
/// base-defense `siege-board` (spec-siege-board.md) — the board's balance surface
/// (tunables-ssot.md T1). `MaxCells` rides the same file even though it is structural, not balance
/// (an allocation/perf bound on one board, AGENTS.md's exemption for structural limits) — kept as a
/// config row rather than a hidden `const`, per tunables-ssot.md's own preference for one balance
/// surface over a scattered one. See <see cref="SiegeTuningPolicy"/>.
/// </summary>
public sealed record SiegeTuning(
    int SchemaVersion, int Version,
    int MoveCostOpen, int MoveCostRough, int DiagonalSurcharge, int MaxCells,
    DistrictTuning District, StructureTuning Structure, SiegeObjectiveTuning Objective,
    SiegeWavesTuning Waves, Siege.SiegeShootingTuning Shooting, ConstructionTuning Construction,
    EconomyTuning Economy, Siege.AiTuning Ai);

/// <summary>
/// base-defense `siege-economy` (spec-siege-economy.md). <see cref="NodeYieldPerRoundLoam"/>/
/// <see cref="NodeYieldPerRoundIronwork"/> are flat per node per round -- deliberately NOT on
/// `P(Θ)` (principle P2: a node's yield is a property of the ground, so it cannot outrun the sinks
/// it feeds). <see cref="DepotSeedMilli"/> is how much of the DEFENDER's sector stockpile is
/// actually reachable during a siege. <see cref="CaptureRecoveryMilli"/> is F11's scaling factor on
/// top of the HP proportion.
/// </summary>
public sealed record EconomyTuning(
    long NodeYieldPerRoundLoam, long NodeYieldPerRoundIronwork,
    int DepotSeedMilli, int CaptureRecoveryMilli);

/// <summary>
/// base-defense `siege-construction` (spec-siege-construction.md), decisions 16/17/28/34.
/// <see cref="ShardVeinYieldPerTurn"/>/<see cref="MaterialSeamYieldPerTurn"/> are "4" and "3" because
/// that is what the guards already say -- `shard-vein` carries GuardHeavy x4, `material-seam`
/// GuardMedium x3. <see cref="RefineYieldMilli"/> is LOSSY by decision 28 (a bounded ratio, exempt
/// from the progression-ceiling rule). <see cref="RefinePerTurnCap"/> is decision 29's gate,
/// deliberately -1 (unset) until a real board exists to measure on.
/// </summary>
public sealed record ConstructionTuning(
    long ShardVeinYieldPerTurn, long MaterialSeamYieldPerTurn,
    long RefineRubblePerIronwork, int RefineYieldMilli, long RefinePerTurnCap);

/// <summary>
/// base-defense `siege-waves` (spec-siege-waves.md). <see cref="BatchIntervalTicks"/>/
/// <see cref="BatchSize"/> are deliberately -1/unset per decision 29 (pacing numbers stay unset until
/// a real board exists to measure on) — <see cref="MaxArrivalsPerRound"/> and
/// <see cref="FieldClearedThreshold"/> are real, authored values.
/// </summary>
public sealed record SiegeWavesTuning(
    int MaxArrivalsPerRound, long BatchIntervalTicks, int FieldClearedThreshold, int BatchSize);

/// <summary>
/// base-defense `siege-objective` (spec-siege-objective.md §2-4, §7). Force-size numbers
/// (<see cref="LegionSlotsPerSide"/>, <see cref="MaxLegionMembers"/>, <see cref="FieldCapMaxLivingPerSide"/>)
/// are deliberately -1/unset per decision 29 ("the difficulty dial... kept unset until a real board
/// exists to measure on") — every other field here is a real, authored value, not deferred.
/// </summary>
public sealed record SiegeObjectiveTuning(
    int FieldCapMaxLivingPerSide,
    int LegionSlotsPerSide, int LegionSlotsPerDevelopmentLevel,
    int MaxLegionMembers,
    int DefenseSlotsAtDevelopmentZero, int DefenseSlotsPerDevelopmentLevel, int DefenseSlotsGridCapacityPoint,
    int DistrictDefenderBonusMilli);

/// <summary>
/// base-defense `structure-state` (spec-structure-state.md) — the whole module's balance surface.
/// <see cref="TierMultiplierMilli"/> is keyed by the AUTHORED material-tier ordinal (decision 32): the
/// ordinal itself is content (a model writes "stone"/"iron"/…), every number behind it is not.
/// <see cref="StorageCapacityPerDevelopmentLevel"/> is F12's fix — capacity must grow with
/// `DevelopmentLevel` alongside decision 21's slot growth, or a new slot's whole output is wasted
/// overflow.
/// </summary>
public sealed record StructureTuning(
    int RepairCostRatioMilli,
    IReadOnlyDictionary<int, int> TierMultiplierMilli,
    long StorageCapacityPerDevelopmentLevel,
    int DepletionPerHarvestMilli);

/// <summary>
/// base-defense `district-layout` (spec-district-layout.md §2/§3). <see cref="SideByBaseTier"/> is a
/// LOOKUP keyed by <c>WorldSector.DevelopmentLevel</c> (base-defense-ideal.md: "Seat / tier ...
/// DevelopmentLevel is exactly this"), never a formula — a DevelopmentLevel past the highest
/// authored key plateaus at that key's side. <see cref="CoreSideMilli"/> is a per-mille RATIO
/// (0..1000), exempt from the progression-ceiling rule for the same reason any ratio is.
/// </summary>
public sealed record DistrictTuning(
    IReadOnlyDictionary<int, int> SideByBaseTier,
    int CoreSideMilli, int GateCount, int RampartThickness, int FortressRampartBonus,
    int ApproachDepth, int ApproachDepthPerWardLevel);

public sealed class SiegeTuningRejection : Exception
{
    public SiegeTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class SiegeTuningLoader
{
    public static SiegeTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SiegeTuningRejection("siege tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new SiegeTuningRejection($"siege tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var board = Obj(root, "board");
            var moveCost = Obj(board, "moveCost");

            var open = Int(moveCost, "open");
            if (open <= 0)
                throw new SiegeTuningRejection($"siege tuning: board.moveCost.open must be > 0; got {open}");

            var rough = Int(moveCost, "rough");
            if (rough <= 0)
                throw new SiegeTuningRejection($"siege tuning: board.moveCost.rough must be > 0; got {rough}");

            var diagonal = Int(moveCost, "diagonalSurcharge");
            if (diagonal < 0)
                throw new SiegeTuningRejection($"siege tuning: board.moveCost.diagonalSurcharge must be >= 0; got {diagonal}");

            var maxCells = Int(board, "maxCells");
            if (maxCells <= 0)
                throw new SiegeTuningRejection($"siege tuning: board.maxCells must be > 0; got {maxCells}");

            var district = Obj(root, "district");

            var sideByBaseTier = new Dictionary<int, int>();
            foreach (var prop in Obj(district, "sideByBaseTier").EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var tier) || tier < 0)
                    throw new SiegeTuningRejection($"siege tuning: district.sideByBaseTier key '{prop.Name}' is not a non-negative integer");
                if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var side) || side <= 0)
                    throw new SiegeTuningRejection($"siege tuning: district.sideByBaseTier.{prop.Name} must be a positive integer");
                sideByBaseTier[tier] = side;
            }
            if (sideByBaseTier.Count == 0)
                throw new SiegeTuningRejection("siege tuning: district.sideByBaseTier must name at least one tier");

            var coreSideMilli = Int(district, "coreSideMilli");
            if (coreSideMilli is <= 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: district.coreSideMilli must be in (0, 1000]; got {coreSideMilli}");

            var gateCount = Int(district, "gateCount");
            if (gateCount <= 0)
                throw new SiegeTuningRejection($"siege tuning: district.gateCount must be > 0; got {gateCount}");

            var rampartThickness = Int(district, "rampartThickness");
            if (rampartThickness <= 0)
                throw new SiegeTuningRejection($"siege tuning: district.rampartThickness must be > 0; got {rampartThickness}");

            var fortressBonus = Int(district, "fortressRampartBonus");
            if (fortressBonus < 0)
                throw new SiegeTuningRejection($"siege tuning: district.fortressRampartBonus must be >= 0; got {fortressBonus}");

            var approachDepth = Int(district, "approachDepth");
            if (approachDepth < 0)
                throw new SiegeTuningRejection($"siege tuning: district.approachDepth must be >= 0; got {approachDepth}");

            var approachPerWard = Int(district, "approachDepthPerWardLevel");
            if (approachPerWard < 0)
                throw new SiegeTuningRejection($"siege tuning: district.approachDepthPerWardLevel must be >= 0; got {approachPerWard}");

            var structure = Obj(root, "structure");
            var storage = Obj(root, "storage");

            var repairRatio = Int(structure, "repairCostRatioMilli");
            if (repairRatio is < 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: structure.repairCostRatioMilli must be in [0, 1000]; got {repairRatio}");

            var tierMultiplierMilli = new Dictionary<int, int>();
            foreach (var prop in Obj(structure, "tierMultiplierMilli").EnumerateObject())
            {
                if (!int.TryParse(prop.Name, out var tier) || tier <= 0)
                    throw new SiegeTuningRejection($"siege tuning: structure.tierMultiplierMilli key '{prop.Name}' is not a positive integer");
                if (prop.Value.ValueKind != JsonValueKind.Number || !prop.Value.TryGetInt32(out var mult) || mult <= 0)
                    throw new SiegeTuningRejection($"siege tuning: structure.tierMultiplierMilli.{prop.Name} must be a positive integer");
                tierMultiplierMilli[tier] = mult;
            }
            if (tierMultiplierMilli.Count == 0)
                throw new SiegeTuningRejection("siege tuning: structure.tierMultiplierMilli must name at least one tier");

            var depletionPerHarvest = Int(structure, "depletionPerHarvestMilli");
            if (depletionPerHarvest is <= 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: structure.depletionPerHarvestMilli must be in (0, 1000]; got {depletionPerHarvest}");

            var capacityPerLevel = Long(storage, "capacityPerDevelopmentLevel");
            if (capacityPerLevel < 0)
                throw new SiegeTuningRejection($"siege tuning: storage.capacityPerDevelopmentLevel must be >= 0; got {capacityPerLevel}");

            var field = Obj(root, "field");
            var fieldCap = Int(field, "maxLivingPerSide");
            if (fieldCap < -1)
                throw new SiegeTuningRejection($"siege tuning: field.maxLivingPerSide must be >= -1; got {fieldCap}");

            var slots = Obj(root, "slots");
            var legionSlots = Obj(slots, "legion");
            var legionPerSide = Int(legionSlots, "perSide");
            if (legionPerSide <= 0 || legionPerSide % 2 != 0)
                throw new SiegeTuningRejection($"siege tuning: slots.legion.perSide must be a positive even integer (decision 4); got {legionPerSide}");
            var legionPerLevel = Int(legionSlots, "perDevelopmentLevel");
            if (legionPerLevel < 0)
                throw new SiegeTuningRejection($"siege tuning: slots.legion.perDevelopmentLevel must be >= 0; got {legionPerLevel}");

            var defenseSlots = Obj(slots, "defense");
            var defenseAtZero = Int(defenseSlots, "atDevelopmentZero");
            if (defenseAtZero < 0)
                throw new SiegeTuningRejection($"siege tuning: slots.defense.atDevelopmentZero must be >= 0; got {defenseAtZero}");
            var defensePerLevel = Int(defenseSlots, "perDevelopmentLevel");
            if (defensePerLevel < 0)
                throw new SiegeTuningRejection($"siege tuning: slots.defense.perDevelopmentLevel must be >= 0; got {defensePerLevel}");
            var gridCapacityPoint = Int(defenseSlots, "gridCapacityPoint");
            if (gridCapacityPoint < 0)
                throw new SiegeTuningRejection($"siege tuning: slots.defense.gridCapacityPoint must be >= 0; got {gridCapacityPoint}");

            var legion = Obj(root, "legion");
            var maxMembers = Int(legion, "maxMembers");
            if (maxMembers < -1 || maxMembers == 0)
                throw new SiegeTuningRejection($"siege tuning: legion.maxMembers must be -1 (unset) or a positive integer; got {maxMembers}");

            var defense = Obj(root, "defense");
            var districtDefenderBonus = Int(defense, "districtDefenderBonusMilli");
            if (districtDefenderBonus < 0)
                throw new SiegeTuningRejection($"siege tuning: defense.districtDefenderBonusMilli must be >= 0; got {districtDefenderBonus}");

            var waves = Obj(root, "waves");
            var maxArrivalsPerRound = Int(waves, "maxArrivalsPerRound");
            if (maxArrivalsPerRound <= 0)
                throw new SiegeTuningRejection($"siege tuning: waves.maxArrivalsPerRound must be > 0; got {maxArrivalsPerRound}");
            var batchIntervalTicks = Long(waves, "batchIntervalTicks");
            if (batchIntervalTicks < -1)
                throw new SiegeTuningRejection($"siege tuning: waves.batchIntervalTicks must be >= -1; got {batchIntervalTicks}");
            var fieldClearedThreshold = Int(waves, "fieldClearedThreshold");
            if (fieldClearedThreshold < 0)
                throw new SiegeTuningRejection($"siege tuning: waves.fieldClearedThreshold must be >= 0; got {fieldClearedThreshold}");
            var batchSize = Int(waves, "batchSize");
            if (batchSize < -1 || batchSize == 0)
                throw new SiegeTuningRejection($"siege tuning: waves.batchSize must be -1 (unset) or a positive integer; got {batchSize}");

            var shooting = Obj(root, "shooting");
            var rangeThreshold = Int(shooting, "rangeThresholdMilli");
            if (rangeThreshold is <= 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: shooting.rangeThresholdMilli must be in (0, 1000]; got {rangeThreshold}");
            var rangePower = Int(shooting, "rangePowerMilli");
            if (rangePower < 0)
                throw new SiegeTuningRejection($"siege tuning: shooting.rangePowerMilli must be >= 0; got {rangePower}");
            var obstructionPower = Int(shooting, "obstructionPowerMilli");
            if (obstructionPower < 0)
                throw new SiegeTuningRejection($"siege tuning: shooting.obstructionPowerMilli must be >= 0; got {obstructionPower}");
            var obstructionFloor = Int(shooting, "obstructionFloorMilli");
            if (obstructionFloor < 0)
                throw new SiegeTuningRejection($"siege tuning: shooting.obstructionFloorMilli must be >= 0; got {obstructionFloor}");
            var meleeLockPower = Int(shooting, "meleeLockPowerMilli");
            if (meleeLockPower < 0)
                throw new SiegeTuningRejection($"siege tuning: shooting.meleeLockPowerMilli must be >= 0; got {meleeLockPower}");

            var construction = Obj(root, "construction");
            var shardVeinYield = Long(construction, "shardVeinYieldPerTurn");
            if (shardVeinYield < 0)
                throw new SiegeTuningRejection($"siege tuning: construction.shardVeinYieldPerTurn must be >= 0; got {shardVeinYield}");
            var materialSeamYield = Long(construction, "materialSeamYieldPerTurn");
            if (materialSeamYield < 0)
                throw new SiegeTuningRejection($"siege tuning: construction.materialSeamYieldPerTurn must be >= 0; got {materialSeamYield}");
            var rubblePerIronwork = Long(construction, "refineRubblePerIronwork");
            if (rubblePerIronwork <= 0)
                throw new SiegeTuningRejection($"siege tuning: construction.refineRubblePerIronwork must be > 0; got {rubblePerIronwork}");
            var refineYieldMilli = Int(construction, "refineYieldMilli");
            if (refineYieldMilli is <= 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: construction.refineYieldMilli must be in (0, 1000] -- decision 28 requires lossy; got {refineYieldMilli}");
            var refinePerTurnCap = Long(construction, "refinePerTurnCap");
            if (refinePerTurnCap < -1)
                throw new SiegeTuningRejection($"siege tuning: construction.refinePerTurnCap must be -1 (unset) or >= 0; got {refinePerTurnCap}");

            var economy = Obj(root, "economy");
            var nodeYieldLoam = Long(economy, "nodeYieldPerRoundLoam");
            if (nodeYieldLoam < 0)
                throw new SiegeTuningRejection($"siege tuning: economy.nodeYieldPerRoundLoam must be >= 0; got {nodeYieldLoam}");
            var nodeYieldIronwork = Long(economy, "nodeYieldPerRoundIronwork");
            if (nodeYieldIronwork < 0)
                throw new SiegeTuningRejection($"siege tuning: economy.nodeYieldPerRoundIronwork must be >= 0; got {nodeYieldIronwork}");
            var depotSeedMilli = Int(economy, "depotSeedMilli");
            if (depotSeedMilli is <= 0 or > 1000)
                throw new SiegeTuningRejection($"siege tuning: economy.depotSeedMilli must be in (0, 1000]; got {depotSeedMilli}");
            var captureRecoveryMilli = Int(economy, "captureRecoveryMilli");
            if (captureRecoveryMilli < 0)
                throw new SiegeTuningRejection($"siege tuning: economy.captureRecoveryMilli must be >= 0; got {captureRecoveryMilli}");

            var ai = Obj(root, "ai");
            var wHitChance = Int(ai, "weightHitChance");
            var wObjective = Int(ai, "weightObjective");
            var wKill = Int(ai, "weightKill");
            var wLowHp = Int(ai, "weightLowHp");
            var wCannotCounter = Int(ai, "weightCannotCounter");
            var wRound = Int(ai, "weightRound");
            var wRisk = Int(ai, "weightRisk");
            foreach (var (name, value) in new[]
                     {
                         ("weightHitChance", wHitChance), ("weightObjective", wObjective), ("weightKill", wKill),
                         ("weightLowHp", wLowHp), ("weightCannotCounter", wCannotCounter), ("weightRound", wRound),
                         ("weightRisk", wRisk)
                     })
            {
                if (value < 0) throw new SiegeTuningRejection($"siege tuning: ai.{name} must be >= 0; got {value}");
            }

            var stanceDefaultRaw = Str(ai, "stanceDefault");
            if (!Enum.TryParse<Siege.Stance>(stanceDefaultRaw, out var stanceDefault))
                throw new SiegeTuningRejection($"siege tuning: ai.stanceDefault '{stanceDefaultRaw}' is not one of Hold/Guard/Engage");

            var autoResolveHandicapMilli = Int(ai, "autoResolveHandicapMilli");
            if (autoResolveHandicapMilli < 0)
                throw new SiegeTuningRejection($"siege tuning: ai.autoResolveHandicapMilli must be >= 0; got {autoResolveHandicapMilli}");
            var retargetLatencyTicks = Long(ai, "retargetLatencyTicks");
            if (retargetLatencyTicks < 0)
                throw new SiegeTuningRejection($"siege tuning: ai.retargetLatencyTicks must be >= 0; got {retargetLatencyTicks}");
            var aggressionRange = Int(ai, "aggressionRange");
            if (aggressionRange <= 0)
                throw new SiegeTuningRejection($"siege tuning: ai.aggressionRange must be > 0; got {aggressionRange}");
            var maxCandidatesScored = Int(ai, "maxCandidatesScored");
            if (maxCandidatesScored <= 0)
                throw new SiegeTuningRejection($"siege tuning: ai.maxCandidatesScored must be > 0; got {maxCandidatesScored}");

            return new SiegeTuning(
                SchemaVersion: Int(root, "schemaVersion"),
                Version: Int(root, "version"),
                MoveCostOpen: open,
                MoveCostRough: rough,
                DiagonalSurcharge: diagonal,
                MaxCells: maxCells,
                District: new DistrictTuning(
                    SideByBaseTier: sideByBaseTier,
                    CoreSideMilli: coreSideMilli,
                    GateCount: gateCount,
                    RampartThickness: rampartThickness,
                    FortressRampartBonus: fortressBonus,
                    ApproachDepth: approachDepth,
                    ApproachDepthPerWardLevel: approachPerWard),
                Structure: new StructureTuning(
                    RepairCostRatioMilli: repairRatio,
                    TierMultiplierMilli: tierMultiplierMilli,
                    StorageCapacityPerDevelopmentLevel: capacityPerLevel,
                    DepletionPerHarvestMilli: depletionPerHarvest),
                Objective: new SiegeObjectiveTuning(
                    FieldCapMaxLivingPerSide: fieldCap,
                    LegionSlotsPerSide: legionPerSide,
                    LegionSlotsPerDevelopmentLevel: legionPerLevel,
                    MaxLegionMembers: maxMembers,
                    DefenseSlotsAtDevelopmentZero: defenseAtZero,
                    DefenseSlotsPerDevelopmentLevel: defensePerLevel,
                    DefenseSlotsGridCapacityPoint: gridCapacityPoint,
                    DistrictDefenderBonusMilli: districtDefenderBonus),
                Waves: new SiegeWavesTuning(
                    MaxArrivalsPerRound: maxArrivalsPerRound,
                    BatchIntervalTicks: batchIntervalTicks,
                    FieldClearedThreshold: fieldClearedThreshold,
                    BatchSize: batchSize),
                Shooting: new Siege.SiegeShootingTuning(
                    RangeThresholdMilli: rangeThreshold,
                    RangePowerMilli: rangePower,
                    ObstructionPowerMilli: obstructionPower,
                    ObstructionFloorMilli: obstructionFloor,
                    MeleeLockPowerMilli: meleeLockPower),
                Construction: new ConstructionTuning(
                    ShardVeinYieldPerTurn: shardVeinYield,
                    MaterialSeamYieldPerTurn: materialSeamYield,
                    RefineRubblePerIronwork: rubblePerIronwork,
                    RefineYieldMilli: refineYieldMilli,
                    RefinePerTurnCap: refinePerTurnCap),
                Economy: new EconomyTuning(
                    NodeYieldPerRoundLoam: nodeYieldLoam,
                    NodeYieldPerRoundIronwork: nodeYieldIronwork,
                    DepotSeedMilli: depotSeedMilli,
                    CaptureRecoveryMilli: captureRecoveryMilli),
                Ai: new Siege.AiTuning(
                    WeightHitChance: wHitChance, WeightObjective: wObjective, WeightKill: wKill,
                    WeightLowHp: wLowHp, WeightCannotCounter: wCannotCounter, WeightRound: wRound,
                    WeightRisk: wRisk, StanceDefault: stanceDefault,
                    AutoResolveHandicapMilli: autoResolveHandicapMilli, RetargetLatencyTicks: retargetLatencyTicks,
                    AggressionRange: aggressionRange, MaxCandidatesScored: maxCandidatesScored));
        }
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new SiegeTuningRejection($"siege tuning: missing or non-object '{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new SiegeTuningRejection($"siege tuning: missing or non-integer '{key}'");
        return v;
    }

    static string Str(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String || el.GetString() is not { } s)
            throw new SiegeTuningRejection($"siege tuning: missing or non-string '{key}'");
        return s;
    }

    static long Long(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var v))
            throw new SiegeTuningRejection($"siege tuning: missing or non-integer '{key}'");
        return v;
    }
}

/// <summary>See <see cref="SiegeTuning"/>.</summary>
public static class SiegeTuningPolicy
{
    static SiegeTuning? _tuning;

    public static void Configure(SiegeTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    static SiegeTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "SiegeTuningPolicy.Configure(...) has not run. GridSpec/BoardState read " +
        "data/tuning/siege.v1.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");

    public static int MoveCostOpen => Tuning.MoveCostOpen;
    public static int MoveCostRough => Tuning.MoveCostRough;
    public static int DiagonalSurcharge => Tuning.DiagonalSurcharge;

    /// <summary>Structural, not balance — see <see cref="SiegeTuning"/>'s own doc comment. Enforced
    /// loudly at <see cref="GridSpec"/> construction, never at render.</summary>
    public static int MaxCells => Tuning.MaxCells;

    public static DistrictTuning District => Tuning.District;
    public static StructureTuning Structure => Tuning.Structure;
    public static SiegeObjectiveTuning Objective => Tuning.Objective;
    public static SiegeWavesTuning Waves => Tuning.Waves;
    public static Siege.SiegeShootingTuning Shooting => Tuning.Shooting;
    public static ConstructionTuning Construction => Tuning.Construction;
    public static EconomyTuning Economy => Tuning.Economy;
    public static Siege.AiTuning Ai => Tuning.Ai;
}
