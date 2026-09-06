using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.World.Siege;

namespace FusionRpg.Core.World;

/// <summary>
/// A category, not a building (spec-structure-substrate.md §New state). One value this wave —
/// enough to express both a Well and a Waystation, which `loam-structures` adds next.
/// </summary>
public enum StructureKind
{
    LoamSource,

    /// <summary>Raises capacity — a real third thing a structure can do (spec-loam-texture.md).</summary>
    Storage,

    /// <summary>
    /// world-map W56 (spec-sector-development.md §3): the yield kinds the reward layer needs — a
    /// soul conduit, extractors, a hatchery on a lair. This module's own structures produce **loam
    /// and recruits only**; the reward layer itself (souls, essence, materials) is unassigned and
    /// stays out (spec-sector-development.md's own fourth open question) — these rows are the
    /// mechanism the reward layer will eventually attach to, not that layer itself.
    /// </summary>
    Yield,

    /// <summary>base-defense `siege-construction` decision 28: refines `WorldSector.RubbleStock` into
    /// `IronworkStock` at a lossy, gated rate — the third thing a structure can do, following
    /// <see cref="Storage"/>'s own precedent. Gated by a WORKING BUILDING on a slot, not a cooldown, so
    /// the refine rate is something a player builds toward rather than waits out.</summary>
    Refinery,

    /// <summary>base-defense `siege-obstacles`/`siege-construction` (2026-09-06): does none of the
    /// above — no yield, no capacity, no refine. Occupies its cell and blocks (via
    /// <see cref="StructureDef.BlocksMovement"/>/<see cref="StructureDef.BlocksLineOfFire"/>) and
    /// nothing else. The fourth thing a structure can do is deliberately "nothing economic" — every
    /// obstacle row (`ObstacleKind` on <see cref="StructureDef.Obstacle"/> says WHICH obstacle; this
    /// says the row has no OTHER behavior to gate on `Kind`), the first of which is the moat.</summary>
    Obstacle
}

/// <summary>One structure type. Mirrors <see cref="SlotTypeDef"/>'s own shape exactly.</summary>
public sealed record StructureDef
{
    public string StructureId { get; init; } = "";
    public string Name { get; init; } = "";
    public StructureKind Kind { get; init; }

    /// <summary>What kind of slot this structure must sit on.</summary>
    public SlotKind RequiredSlotKind { get; init; }

    /// <summary>Upfront build cost, spent from the building legion's own <c>CarriedLoam</c>.</summary>
    public long Cost { get; init; }

    /// <summary>
    /// Per-mille, 1000 = unchanged. A Well multiplies its Rootbed's existing yield; a Waystation
    /// multiplies its Seat's zero — one field expresses both (spec-structure-substrate.md).
    /// </summary>
    public int YieldMultiplierMilli { get; init; } = 1000;

    /// <summary>
    /// How many `Production` passes construction takes, decrementing to zero, before the structure
    /// is active (spec-loam-structures.md). Zero means never under construction — instant.
    /// </summary>
    public int BuildTurns { get; init; }

    /// <summary>Read only by `Storage`-kind structures — how much a granary raises a sector's cap by (spec-loam-texture.md).</summary>
    public long CapacityBonus { get; init; }

    /// <summary>
    /// world-map W56 (spec-sector-development.md §3): a flat per-turn loam add, structure-only —
    /// the field `spec-structure-substrate.md` explicitly deferred to "a new field added when there
    /// is a real row to test it against." Read by <c>LoamProduction.For</c> for *every* active
    /// structure regardless of the slot kind it sits on (unlike <see cref="YieldMultiplierMilli"/>,
    /// which only ever multiplies a Rootbed's own seep) — additive to the existing sum, never a
    /// replacement. Defaults to 0, so every structure minted before this task is untouched.
    /// </summary>
    public long FlatYieldPerTurn { get; init; }

    /// <summary>
    /// base-defense `siege-construction` §2 (decision 27's `Built` path): rubble spent to place this
    /// structure during an active siege. Distinct from <see cref="Cost"/> — peacetime `Build` spends
    /// the founding legion's own `CarriedLoam`; siege-time `Built` spends the SECTOR's world-scoped
    /// `RubbleStock`/`IronworkStock` instead (decision 18: construction-only, never loam-denominated).
    /// Defaults to 0, so every structure minted before this task can still be sieged-constructed for
    /// free until a real cost is authored — a structurally honest default, not a guessed number.
    /// </summary>
    public long ConstructRubbleCost { get; init; }

    /// <summary>See <see cref="ConstructRubbleCost"/> — the worked-material half of the same cost.</summary>
    public long ConstructIronworkCost { get; init; }

    /// <summary>
    /// base-defense `structure-state` (decision 32): the MATERIAL TIER ordinal, not a hit-point count.
    ///
    /// <para><b>An ordinal, because a model picks it.</b> "we will use llm to generate variant like
    /// stone wall, iron wall that iron wall have more defense than stone wall." Seedsmith Law 2
    /// exactly: the model writes IDENTITY (stone, iron, …) and deterministic code writes MAGNITUDE. A
    /// model has no calibrated sense of scale, so a number it picks is a plausible-looking guess that
    /// survives review because nothing looks wrong with it.</para>
    ///
    /// <para>Zero means <b>indestructible by damage</b> — a real content state, not an oversight. The
    /// seven shipped loam rows above predate any notion of a siege and stay at 0, unaffected.</para>
    /// </summary>
    public int MaterialTier { get; init; }

    /// <summary>
    /// Whether this structure blocks movement through its cell. A wall does; a granary you can walk
    /// around does not. Default false — every shipped row keeps today's behaviour.
    /// </summary>
    public bool BlocksMovement { get; init; }

    /// <summary>
    /// Whether this structure blocks line of fire through its cell (base-defense decision 25: an
    /// unoccupied building "occupies its cell, blocks movement AND FIRE, and has HP. It simply does
    /// not act."). Separate from <see cref="BlocksMovement"/> on purpose — a moat blocks movement and
    /// not fire; a smoke-filled ruin could block fire and not movement — this is the field that
    /// finally gives `ActionRow.RequiresLineOfSight` a reader (`siege-obstacles`/`siege-cover`).
    /// </summary>
    public bool BlocksLineOfFire { get; init; }

    /// <summary>
    /// Effective hit points. Decision 32: <c>P(Θ_development) × tierMultiplier</c>, where Θ is the
    /// SECTOR'S <see cref="WorldSector.DevelopmentLevel"/> — a structure has no level of its own, and
    /// a developed city has stronger walls.
    ///
    /// <para><b>long, from <c>P(Θ)</c></b> — the one power ladder; there is no private <c>f(level)</c>
    /// here. Widen before multiplying; divide by 1000 last, exactly once; <c>checked</c>.</para>
    /// </summary>
    public static long MaxHpOf(StructureDef def, int developmentLevel)
    {
        if (def.MaterialTier <= 0) return 0;
        var pTheta = new FusionRpg.Core.Power.PowerLadder(FusionRpg.Core.Power.PowerTuningHub.Tuning)
            .Value(Math.Max(0, developmentLevel));
        return checked(pTheta * StructurePolicy.TierMultiplierMilli(def.MaterialTier) / 1000);
    }

    /// <summary>
    /// base-defense `siege-obstacles` §1: which of §5.18's five rows this structure IS, if any. Not a
    /// parallel system — an obstacle is a `StructureDef` facet, so it inherits HP, destructibility and
    /// everything else `structure-state` already built. Default `None`, so every shipped loam row
    /// (predating any notion of a siege) is unaffected and no golden moves.
    /// </summary>
    public ObstacleKind Obstacle { get; init; } = ObstacleKind.None;

    /// <summary>
    /// base-defense `siege-obstacles` §7, decision 27, §5.24: which of the four acquisition paths can
    /// produce this structure. A structure no path can produce is a catalog row that can never appear
    /// on a board — validated non-empty at load, like every other catalog rule (a bad row is a startup
    /// error, never a runtime surprise).
    /// </summary>
    public IReadOnlyList<AcquisitionPath> AcquisitionPaths { get; init; } = Array.Empty<AcquisitionPath>();

    /// <summary>base-defense `siege-obstacles` §2: a Trench's cover value — flat contest points, never
    /// `P(Θ)` (§5.17: a contest is linear, difference-based, not a magnitude). Zero for every non-cover
    /// structure. Consumed by `siege-cover` as DATA, never a call into that module — this is the field
    /// that used to look like a dependency and turned out to be one line apart.</summary>
    public int CoverPowerMilli { get; init; }

    /// <summary>base-defense `siege-obstacles` §2: how far this structure's cover reaches, in cells.
    /// Zero for every non-cover structure. See <see cref="CoverPowerMilli"/>'s own note on why this is
    /// data, not a dependency on `siege-cover`.</summary>
    public int CoverRadius { get; init; }

    /// <summary>
    /// base-defense `siege-obstacles` §4: per-mille multiplier on the STAMINA cost of entering this
    /// structure's cell. 1000 = unchanged. Stamina, never movement cost (§5.18's own correction to the
    /// original `siege-board` draft) — doubling a movement cost makes the cell a longer walk the
    /// pathfinder simply routes around; taxing stamina makes the SHORT route expensive, which is the
    /// decision Wire exists to create. Bounded ratio CONCEPTUALLY but not capped above — a 5000‰ wire
    /// is legal, per AGENTS.md's no-hard-ceilings rule for a magnitude a balance pass raises.
    /// </summary>
    public int EntryStaminaMultiplierMilli { get; init; } = 1000;

    /// <summary>
    /// base-defense `siege-fog` (spec-siege-fog.md §2, module 30, 2026-09-06): how far this structure
    /// lets its own side see, in tiles. Defaults to `fog.defaultVisionRangeTiles` so no structure ships
    /// with an unauthored, unreadable range by omission. A `See`-role structure (`structure-schema`'s
    /// own reserved-but-unmapped role) authors a larger value than a non-`See` structure — this is the
    /// mechanical payoff the idea doc named for that role's first real purpose.
    /// </summary>
    public int VisionRangeTiles { get; init; } = SiegeTuningPolicy.Fog.DefaultVisionRangeTiles;

    /// <summary>
    /// base-defense `structure-instantiate` (spec-structure-instantiate.md, module 26): which
    /// `ContainerRow` (if any) this structure rolls traits/actions from at placement time, via
    /// <see cref="FusionRpg.Core.Battle.Siege.StructureInstantiate.TryInstantiateStructure"/>. Null
    /// for every structure today — a real, honest state (spec-structure-instantiate.md §1: "the roll
    /// is narrow" — cost/HP/footprint never roll, only traits/actions do), not a placeholder: giving
    /// a structure a container is `structure-planner`/`structure-pipeline`'s (27/28) own content
    /// decision, not this catalog's. A null container means "nothing to roll," a real, correct
    /// outcome the instantiate call handles directly rather than treating as an error.
    /// </summary>
    public string? ContainerId { get; init; }
}

/// <summary>
/// What can be built onto a slot (spec-structure-substrate.md). Deliberately content-light this
/// wave: one placeholder row proves the mechanism before `loam-structures` adds Well and Waystation.
/// </summary>
public static class StructureCatalog
{
    static IReadOnlyList<StructureDef>? _all;
    static Dictionary<string, StructureDef>? _byId;
    static FusionRpg.Core.World.StructureSeed.StructureCorpus? _corpus;

    /// <summary>
    /// base-defense `structure-catalog-import` (module 25, spec-structure-catalog-import.md §1).
    /// Called by the composition root, never by game code — resets the cached rows so a
    /// reconfigure is honoured, the same contract `BattleModeProfileCatalog.Configure` already
    /// states for profiles. Pass `null` to revert to the pure C# <see cref="Seed"/> literal (the
    /// same behaviour as never calling this at all — every existing test that never calls
    /// `Configure` keeps seeing exactly what it always has).
    /// </summary>
    public static void Configure(FusionRpg.Core.World.StructureSeed.StructureCorpus? corpus)
    {
        _corpus = corpus;
        _all = null;
        _byId = null;
    }

    public static IReadOnlyList<StructureDef> All => _all ??= Validate(BuildRows());

    /// <summary>
    /// The four C# <see cref="Seed"/> rows are the FALLBACK, not the source (spec §1): a row the
    /// configured corpus does not cover (or no corpus configured at all) still comes from `Seed`.
    /// Once every `Seed` id has a real, catalog-loadable corpus row — proven by the byte-identity
    /// tests — the `Seed` literal itself is deleted (task 25.4), per the spec's own "order matters"
    /// instruction (prove identity, then delete, never the reverse).
    /// </summary>
    static IReadOnlyList<StructureDef> BuildRows()
    {
        if (_corpus is null) return Seed;

        var fromCorpus = new List<StructureDef>();
        var corpusIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in _corpus.Rows)
        {
            if (!row.IsCatalogLoadable) continue; // identity-registered only — structure-planner's job
            corpusIds.Add(row.StructureId);
            fromCorpus.Add(ToStructureDef(row));
        }

        foreach (var seedRow in Seed)
            if (!corpusIds.Contains(seedRow.StructureId))
                fromCorpus.Add(seedRow);

        return fromCorpus;
    }

    /// <summary>The one place a corpus row's ordinals/magnitudes become a real `StructureDef` —
    /// see spec-structure-catalog-import.md's own Correction 1 for why `Magnitudes` (never
    /// `StrengthBand` alone) is authoritative for `MaterialTier`, and Correction 2 for why
    /// `Magnitudes.StructureKind` (never a `role`-keyed derivation) is authoritative for `Kind` —
    /// `anchor/schema.py`'s own `ROLE_TO_STRUCTURE_KIND` dict is provably wrong against real
    /// shipped rows (`hatchery`/`soul-conduit`/`extractor` are all `Yield`, not what it predicts).
    /// </summary>
    static StructureDef ToStructureDef(FusionRpg.Core.World.StructureSeed.StructureCorpusRow row)
    {
        var m = row.Magnitudes!;
        return new StructureDef
        {
            StructureId = row.StructureId,
            Name = row.Name,
            Kind = Enum.Parse<StructureKind>(m.StructureKind),
            RequiredSlotKind = Enum.Parse<SlotKind>(row.RequiredSlotKind),
            Cost = m.Cost,
            YieldMultiplierMilli = m.YieldMultiplierMilli,
            BuildTurns = m.BuildTurns,
            CapacityBonus = m.CapacityBonus,
            FlatYieldPerTurn = m.FlatYieldPerTurn,
            ConstructRubbleCost = m.ConstructRubbleCost,
            ConstructIronworkCost = m.ConstructIronworkCost,
            MaterialTier = m.MaterialTier,
            BlocksMovement = m.BlocksMovement,
            BlocksLineOfFire = m.BlocksLineOfFire,
            Obstacle = Enum.Parse<ObstacleKind>(m.ObstacleKind),
            AcquisitionPaths = row.AcquisitionPaths.Select(p => Enum.Parse<AcquisitionPath>(p, ignoreCase: true)).ToList(),
            CoverPowerMilli = m.CoverPowerMilli,
            CoverRadius = m.CoverRadius,
            EntryStaminaMultiplierMilli = m.EntryStaminaMultiplierMilli,
            VisionRangeTiles = m.VisionRangeTiles ?? SiegeTuningPolicy.Fog.DefaultVisionRangeTiles,
            ContainerId = m.ContainerId,
        };
    }

    public static bool IsKnown(string? structureId) =>
        structureId != null && ByIdMap().ContainsKey(structureId);

    public static StructureDef Get(string structureId) =>
        ByIdMap().TryGetValue(structureId, out var def)
            ? def
            : throw new ArgumentException($"Unknown structure id '{structureId}'.");

    // base-defense `siege-obstacles` §7: every shipped row below predates the acquisition-path
    // concept, so each is retrofitted with `Built` — the least controversial, most literal reading of
    // "a legion action constructs this on the spot", which is exactly how every one of these seven
    // structures has always come to exist in this repo's own shipped mechanics (no summon/laboured/
    // assembled path exists anywhere for loam content today). This is what keeps `AcquisitionPaths`
    // validated non-empty for EVERY structure, obstacle or not, without breaking startup.
    static readonly IReadOnlyList<AcquisitionPath> BuiltOnly = new[] { AcquisitionPath.Built };

    static readonly IReadOnlyList<StructureDef> Seed = new StructureDef[]
    {
        new()
        {
            StructureId = "loam-source-placeholder",
            Name = "Loam Source (placeholder)",
            Kind = StructureKind.LoamSource,
            RequiredSlotKind = SlotKind.Rootbed,
            Cost = 0,
            YieldMultiplierMilli = 1000,
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            StructureId = "well",
            Name = "Well",
            Kind = StructureKind.LoamSource,
            RequiredSlotKind = SlotKind.Rootbed,
            Cost = Loam.LoamPolicy.WellCost,
            YieldMultiplierMilli = Loam.LoamPolicy.WellYieldMultiplierMilli,
            BuildTurns = Loam.LoamPolicy.WellBuildTurns,
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            StructureId = "waystation",
            Name = "Waystation",
            Kind = StructureKind.LoamSource,
            RequiredSlotKind = SlotKind.Seat,
            Cost = Loam.LoamPolicy.WaystationCost,
            // A Seat's own base yield is already zero, so the multiplier is irrelevant here —
            // 1000 (unchanged) rather than a special case in LoamProduction's formula.
            YieldMultiplierMilli = 1000,
            BuildTurns = Loam.LoamPolicy.WaystationBuildTurns,
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            StructureId = "granary",
            Name = "Granary",
            Kind = StructureKind.Storage,
            RequiredSlotKind = SlotKind.Wildland,
            Cost = Loam.LoamPolicy.GranaryCost,
            // Unused for Storage-kind structures — a granary does not produce, it raises capacity.
            YieldMultiplierMilli = 1000,
            BuildTurns = Loam.LoamPolicy.GranaryBuildTurns,
            CapacityBonus = Loam.LoamPolicy.GranaryCapacityBonus,
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            StructureId = "soul-conduit",
            Name = "Soul Conduit",
            Kind = StructureKind.Yield,
            RequiredSlotKind = SlotKind.EssenceDeposit,
            Cost = Loam.LoamPolicy.SoulConduitCost,
            // Unused for Yield-kind structures — the flat field below is what this one produces.
            YieldMultiplierMilli = 1000,
            BuildTurns = Loam.LoamPolicy.SoulConduitBuildTurns,
            FlatYieldPerTurn = Loam.LoamPolicy.SoulConduitFlatYieldPerTurn,
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            StructureId = "extractor",
            Name = "Extractor",
            Kind = StructureKind.Yield,
            RequiredSlotKind = SlotKind.ShardVein,
            Cost = Loam.LoamPolicy.ExtractorCost,
            YieldMultiplierMilli = 1000,
            BuildTurns = Loam.LoamPolicy.ExtractorBuildTurns,
            FlatYieldPerTurn = Loam.LoamPolicy.ExtractorFlatYieldPerTurn,
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            StructureId = "hatchery",
            Name = "Hatchery",
            Kind = StructureKind.Yield,
            RequiredSlotKind = SlotKind.Lair,
            Cost = Loam.LoamPolicy.HatcheryCost,
            // world-map W56: read by GrowthPhases as the sector's own lair multiplier, through
            // RecruitPolicy.PulseFor's existing `lairMultiplierMilli` parameter — never a second
            // formula. Unlike every other row here, this one's own YieldMultiplierMilli is real.
            YieldMultiplierMilli = Loam.LoamPolicy.HatcheryYieldMultiplierMilli,
            BuildTurns = Loam.LoamPolicy.HatcheryBuildTurns,
            // FlatYieldPerTurn unset (0) — a hatchery produces more recruits, not more loam.
            AcquisitionPaths = BuiltOnly
        },
        new()
        {
            // base-defense `siege-construction` §5 / `siege-obstacles` §5.18 (corrected 2026-09-06):
            // dug, piled, felled — stamina and hunger, no stockpile (Laboured's own cost, authored on
            // the ACTION that builds it, never here — see spec-siege-construction.md §5's own "the
            // cost source differs, never the atom's/structure's concern"). Tier 1, the ladder's lowest
            // rung: a dug ditch is the most basic obstacle a legion can raise, not a fortified wall.
            // The FIRST obstacle row this catalog has ever shipped — `siege-obstacles`' own "closed"
            // status was the mechanism (ObstacleKind, LineOfFire, cover math), not content.
            //
            // Deliberately buildable via ALL FOUR acquisition paths (15.3b, 2026-09-06): a fuller
            // siege-content roster (Trench/Wire/Mine/Emplacement, and separate Assembled/Summoned/Built
            // structures) is `structure-corpus`'s (module 24) own job, not this task's — inventing three
            // more obstacle rows here would be unrequested content, not mechanism-proving. One real,
            // shipped structure exercising every path proves the `structure.place` mechanism end to end
            // without guessing at a roster nobody has designed yet.
            StructureId = "moat",
            Name = "Moat",
            Kind = StructureKind.Obstacle,
            RequiredSlotKind = SlotKind.Wildland,
            Cost = 0, // never built peacetime; every siege-time path spends its own action cost, not loam
            BuildTurns = SiegeTuningPolicy.Construction.LabourMoatTurns,
            MaterialTier = 1,
            BlocksMovement = true,
            BlocksLineOfFire = true,
            Obstacle = ObstacleKind.Rampart,
            ConstructRubbleCost = SiegeTuningPolicy.Construction.RefineRubblePerIronwork, // a real, non-zero Built cost — reuses an already-tunable number rather than inventing a fresh one
            ConstructIronworkCost = 1,
            AcquisitionPaths = new[] { AcquisitionPath.Built, AcquisitionPath.Assembled, AcquisitionPath.Summoned, AcquisitionPath.Laboured }
        }
    };

    static Dictionary<string, StructureDef> ByIdMap()
    {
        if (_byId == null)
        {
            _ = All;
            _byId = All.ToDictionary(s => s.StructureId, StringComparer.Ordinal);
        }

        return _byId;
    }

    /// <summary>Catalog discipline — a bad structure row is a startup error, never a runtime surprise.</summary>
    public static IReadOnlyList<StructureDef> Validate(IReadOnlyList<StructureDef> structures)
    {
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in structures)
        {
            WorldIds.RequireKebab(s.StructureId, "Structure id");
            if (!seenIds.Add(s.StructureId))
                throw new InvalidOperationException($"Duplicate structure id '{s.StructureId}'.");
            if (string.IsNullOrWhiteSpace(s.Name))
                throw new InvalidOperationException($"Structure '{s.StructureId}' has no display name.");
            if (s.Cost < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has negative cost.");
            if (s.ConstructRubbleCost < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has negative construct rubble cost.");
            if (s.ConstructIronworkCost < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has negative construct ironwork cost.");
            if (s.YieldMultiplierMilli < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative yield multiplier.");
            if (s.FlatYieldPerTurn < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative flat yield.");
            if (s.MaterialTier < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative material tier.");
            // Tier 0 (indestructible) never reaches TierMultiplierMilli — MaxHpOf short-circuits it.
            // Every tier above 0 must have an authored multiplier, or decision 32's whole ladder is
            // useless the moment content actually uses a tier the tuning file forgot.
            if (s.MaterialTier > 0)
                _ = StructurePolicy.TierMultiplierMilli(s.MaterialTier);
            if (s.AcquisitionPaths.Count == 0)
                throw new InvalidOperationException(
                    $"Structure '{s.StructureId}' names no acquisition path — a structure no path can produce can never appear on a board.");
            if (s.CoverPowerMilli < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has negative cover power.");
            if (s.CoverRadius < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has negative cover radius.");
            if (s.EntryStaminaMultiplierMilli < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative entry stamina multiplier.");
            if (s.VisionRangeTiles < 0)
                throw new InvalidOperationException($"Structure '{s.StructureId}' has a negative vision range.");
        }

        return structures;
    }
}
