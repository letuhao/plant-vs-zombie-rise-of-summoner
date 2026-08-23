namespace FusionRpg.Core.World.Loam;

/// <summary>
/// upkeep(sector) = ( base + Σ garrison + f(development, danger) ) × intensity/1000 × handicap/1000
/// (spec-loam-calc.md #3). No distance term (map finding A3, an assumption on an open decision —
/// intensity already carries remoteness, and a second multiplier would be invisible on screen and
/// unfalsifiable in tuning). No structure term yet — structures arrive in `loam-structures`, wave 4.
/// </summary>
public static class LoamUpkeep
{
    /// <summary>The truth side: reads the sector's own state, its garrison, and its faction's handicap.</summary>
    public static long For(WorldState world, WorldSector sector)
    {
        if (sector.OwnerFactionId is not { } owner) return 0; // G-B

        // G-C: a faction with no loam source anywhere is exempt entirely, mirroring
        // `SupplyGraph.cs`'s "the wild do not starve for want of a capital they never had". The
        // wild are a hazard, not an empire, and charging upkeep nobody can ever pay is a countdown
        // to a loss nobody chose, not a difficulty curve.
        var hasSourceAnywhere = world.Sectors.Any(s =>
            string.Equals(s.OwnerFactionId, owner, StringComparison.Ordinal)
            && s.Slots.Any(sl => SlotTypeCatalog.Get(sl.SlotTypeId).Kind == SlotKind.Rootbed));
        if (!hasSourceAnywhere) return 0;

        var handicapMilli = world.Factions.First(f => f.FactionId == owner).UpkeepHandicapMilli;
        var garrisonMembers = world.Entities
            .Where(e => string.Equals(e.AtSectorId, sector.SectorId, StringComparison.Ordinal))
            .Sum(e => e.Members.Count);

        return For(garrisonMembers, sector.DevelopmentLevel, sector.DangerBand, sector.FractureIntensityMilli, handicapMilli);
    }

    /// <summary>
    /// The belief side: a garrison count, development, danger and intensity are all terrain or
    /// self-knowledge (never fogged for your own decision-making), and the handicap is a faction's
    /// own declared lever. Quantities promote to <c>long</c> from the first multiplication, so the
    /// expression cannot silently overflow into negative upkeep the way an <c>int</c> version would
    /// at legal inputs — multiply once across everything, divide exactly once.
    /// </summary>
    public static long For(int garrisonMembers, int developmentLevel, int dangerBand, int intensityMilli, int handicapMilli)
    {
        long sum = LoamPolicy.BaseUpkeepPerSector
                   + (long)garrisonMembers * LoamPolicy.GarrisonUpkeepPerMember
                   + LoamPolicy.DevelopmentAndDangerUpkeep(developmentLevel, dangerBand);

        return sum * intensityMilli * handicapMilli / 1_000_000;
    }
}
