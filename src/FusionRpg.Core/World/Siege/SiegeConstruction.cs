using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.World.Turn;

namespace FusionRpg.Core.World.Siege;

/// <summary>
/// base-defense `siege-construction` (spec-siege-construction.md), decisions 27/28/34/38: the
/// `rubble`/`ironwork` two-stock economy and the refine chain between them. `AcquisitionPath`
/// already ships from `siege-obstacles` (<see cref="AcquisitionPath"/>, this same namespace) and is
/// reused verbatim -- no second vocabulary.
///
/// <para>Deliberately narrow scope. What this module builds: the pure stock arithmetic
/// (<see cref="Refine"/>/<see cref="RefineGated"/>) and the shared placement gate
/// (<see cref="ConstructionPlacement"/>, `Battle/Board/ConstructionPlacement.cs`). What it names as an
/// honest, stated gap rather than forcing under this session's remaining effort budget: the new
/// `WorldCommandKinds.Assault` order kind and its five plumbing sites (§7 cost 3 of the spec), the
/// `Assembled`/`Summoned`/`Laboured` action-system wiring (a `structure.assemble`/`structure.summon`
/// atom, the moat's terrain-override action), live per-turn `shard-vein`/`material-seam` yield wiring
/// into a turn phase, and `InterruptRefundMilli = 0` authored onto a real build envelope. Every one of
/// those touches either the five-site order-kind pipeline or an action/turn-phase surface this
/// session has not read in full -- rushing them risks the exact "site 4/5 missed" failure the spec's
/// own `bind-warden` precedent already recorded once.</para>
/// </summary>
public static class SiegeConstruction
{
    /// <summary>
    /// Refines rubble into ironwork. LOSSY and GATED -- economy principle P5's convertibility rule: a
    /// conversion that is free and unlimited makes the two stocks one stock with two names.
    ///
    /// <para><b>long throughout, checked, divide by 1000 last and exactly once.</b></para>
    /// </summary>
    public static long Refine(long rubbleSpent, int yieldMilli)
    {
        if (rubbleSpent < 0) throw new ArgumentOutOfRangeException(nameof(rubbleSpent));
        if (yieldMilli < 0) throw new ArgumentOutOfRangeException(nameof(yieldMilli));
        return checked(rubbleSpent * yieldMilli / 1000);
    }

    /// <summary>
    /// <see cref="Refine"/>, gated by a WORKING <see cref="StructureKind.Refinery"/> on a slot rather
    /// than a cooldown (decision 28) -- the rate is something a player builds toward, not waits out.
    /// No refinery, no output: the rubble is not spent either, since the caller decides what "spend"
    /// means for its own turn-phase accounting; this function only answers "how much ironwork would
    /// this rubble produce right now."
    /// </summary>
    public static long RefineGated(bool hasWorkingRefinery, long rubbleSpent, int yieldMilli) =>
        hasWorkingRefinery ? Refine(rubbleSpent, yieldMilli) : 0;

    /// <summary>
    /// §2's own faucets: `ironwork` from a CLEARED `shard-vein` slot, `rubble` from a CLEARED
    /// `material-seam` slot — a RAW ground yield, independent of any structure (unlike
    /// <see cref="LoamProduction"/>'s own extractor/soul-conduit model, which needs a built structure
    /// on the slot). "You clear a guarded vein IN ORDER TO build" — the guard is the whole gate; an
    /// intact (unguarded... unCLEARED) vein yields nothing, matching every other guarded slot's own
    /// resource-lock convention in this program.
    /// </summary>
    public static (long Ironwork, long Rubble) Yield(WorldSector sector)
    {
        long ironwork = 0, rubble = 0;
        foreach (var slot in sector.Slots)
        {
            if (slot.GuardState != GuardState.Cleared) continue;
            if (!SlotTypeCatalog.IsKnown(slot.SlotTypeId)) continue;

            var kind = SlotTypeCatalog.Get(slot.SlotTypeId).Kind;
            if (kind == SlotKind.ShardVein) ironwork = checked(ironwork + SiegeTuningPolicy.Construction.ShardVeinYieldPerTurn);
            else if (kind == SlotKind.MaterialSeam) rubble = checked(rubble + SiegeTuningPolicy.Construction.MaterialSeamYieldPerTurn);
        }

        return (ironwork, rubble);
    }

    /// <summary>
    /// base-defense `siege-construction` 15.4: the turn phase that actually credits <see cref="Yield"/>
    /// into <see cref="WorldSector.IronworkStock"/>/<see cref="WorldSector.RubbleStock"/> — mirrors
    /// <see cref="Loam.LoamPhases.Production"/>'s own per-sector shape exactly, deliberately, rather
    /// than inventing a second turn-phase pattern. Uncapped by design: unlike `LoamStock`
    /// (`LoamPolicy.LoamCapacity`), neither `ConstructionTuning` nor the spec names a stock ceiling for
    /// `rubble`/`ironwork` — AGENTS.md's own no-hard-ceilings rule, so no overflow event is possible or
    /// reported here.
    ///
    /// <para><b>Deliberately scoped to the two raw faucets only.</b> <see cref="RefineGated"/>'s own
    /// per-turn wiring (spending accumulated `rubble` into `ironwork` automatically, gated by a working
    /// `Refinery`) is a SEPARATE, not-yet-scoped turn-phase question this method does not attempt —
    /// `refine.perTurnCap` staying unset (decision 29) makes an automatic per-turn refine an economically
    /// live question (an unbounded refinery could drain a sector's whole rubble stock in one pass), not
    /// a mechanical wiring one, and 15.4's own task text names only the two faucets.</para>
    /// </summary>
    public static WorldState Production(WorldState world, TurnReport report, string phase)
    {
        var sectors = new List<WorldSector>(world.Sectors.Count);
        foreach (var sector in world.Sectors)
        {
            var (ironwork, rubble) = Yield(sector);
            sectors.Add(ironwork == 0 && rubble == 0
                ? sector
                : sector with
                {
                    IronworkStock = checked(sector.IronworkStock + ironwork),
                    RubbleStock = checked(sector.RubbleStock + rubble),
                });
        }

        return world with { Sectors = sectors };
    }
}
