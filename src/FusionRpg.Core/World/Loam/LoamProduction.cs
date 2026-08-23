namespace FusionRpg.Core.World.Loam;

/// <summary>
/// production(sector) = Σ over slots that are loam sources of seepPerTurn(slotType)
/// (spec-loam-calc.md #1). Wave 1's only source is the rootbed slot itself — untended ground seeps,
/// unconditionally: there is no chain gate here (the owner's S3 resolution). Connectivity does not
/// decide whether loam appears, only who can spend it (<see cref="TerritoryComponents"/>).
/// </summary>
public static class LoamProduction
{
    /// <summary>
    /// The truth side. Extends additively over the belief-side sum: each Rootbed slot's own base
    /// yield, multiplied by its own active structure's <c>YieldMultiplierMilli</c> if one is present
    /// (spec-loam-structures.md — a Well multiplies, it does not replace) — 1000 (unchanged) for a
    /// slot with no structure or one still under construction, so a rootbed with no well behaves
    /// exactly as it does today.
    /// </summary>
    public static long For(WorldSector sector)
    {
        if (sector.OwnerFactionId is null) return 0; // G-B

        long total = 0;
        foreach (var slot in sector.Slots)
        {
            if (SlotTypeCatalog.Get(slot.SlotTypeId).Kind != SlotKind.Rootbed) continue;

            var multiplierMilli = 1000;
            if (slot.StructureId is { } structureId
                && slot.ConstructionTurnsRemaining is null or <= 0
                && StructureCatalog.IsKnown(structureId))
            {
                multiplierMilli = StructureCatalog.Get(structureId).YieldMultiplierMilli;
            }

            total += LoamPolicy.SeepPerTurn * multiplierMilli / 1000;
        }

        return total;
    }

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
