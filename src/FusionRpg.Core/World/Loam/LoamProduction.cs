namespace FusionRpg.Core.World.Loam;

/// <summary>
/// production(sector) = Σ over slots that are loam sources of seepPerTurn(slotType)
/// (spec-loam-calc.md #1). Wave 1's only source is the rootbed slot itself — untended ground seeps,
/// unconditionally: there is no chain gate here (the owner's S3 resolution). Connectivity does not
/// decide whether loam appears, only who can spend it (<see cref="TerritoryComponents"/>).
/// </summary>
public static class LoamProduction
{
    /// <summary>The truth side.</summary>
    public static long For(WorldSector sector) =>
        For(sector.OwnerFactionId, sector.Slots.Select(sl => sl.SlotTypeId));

    /// <summary>
    /// The belief side: all a caller needs is whether the sector is owned (never fogged — you
    /// always know what you hold) and which slot types it carries (terrain, remembered once
    /// scouted). Ordering-invariant: production is a sum, and a sum does not care what order it is
    /// added in.
    /// </summary>
    public static long For(string? ownerFactionId, IEnumerable<string> slotTypeIds)
    {
        // G-B: unowned sectors have no economy. A rootbed sitting in neutral ground does not
        // quietly fill up while nobody holds it — otherwise the optimal play is to wait and take
        // the windfall, which rewards doing nothing.
        if (ownerFactionId is null) return 0;

        long total = 0;
        foreach (var slotTypeId in slotTypeIds)
            if (SlotTypeCatalog.Get(slotTypeId).Kind == SlotKind.Rootbed)
                total += LoamPolicy.SeepPerTurn;

        return total;
    }
}
