namespace FusionRpg.Core.World.Loam;

/// <summary>
/// habitable(sector) ⇔ it holds at least one loam source (spec-loam-calc.md #6) — the owner's
/// settlement rule (ideal §8.10). The wording never changes across the program; only the set of
/// sources grows when `loam-structures` lands (wave 1: rootbed slots; wave 4: wells and waystations too).
/// </summary>
public static class Habitability
{
    /// <summary>The truth side.</summary>
    public static bool For(WorldSector sector) => For(sector.Slots.Select(sl => sl.SlotTypeId));

    /// <summary>The belief side: slot types are terrain, so this never needs ownership or fog.</summary>
    public static bool For(IEnumerable<string> slotTypeIds) =>
        slotTypeIds.Any(id => SlotTypeCatalog.Get(id).Kind == SlotKind.Rootbed);
}
