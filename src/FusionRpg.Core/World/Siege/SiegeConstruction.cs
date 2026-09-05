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
}
