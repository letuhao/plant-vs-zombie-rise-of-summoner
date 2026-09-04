namespace FusionRpg.Core.World.Loam;

/// <summary>
/// The additive operands behind one sector's upkeep, before the two multiplicative modifiers apply
/// — world-stage W10 (re-homed from `world-numbers`): the ledger shows a derivation, not just a
/// result. <see cref="Sum"/> then <see cref="Total"/> recombine to the exact same number
/// <see cref="LoamUpkeep.For(WorldState, WorldSector)"/> already returns — asserted by a test, not
/// trusted, since two independently-drifting formulas is exactly the defect a breakdown must not
/// introduce.
/// </summary>
public readonly record struct LoamUpkeepBreakdown(
    long Base, long Garrison, long Development, long Danger, int IntensityMilli, int HandicapMilli, int SeasonMilli)
{
    public long Sum => Base + Garrison + Development + Danger;

    /// <summary>
    /// Same arithmetic as the formula's own comment: one multiply-through, one division, at the
    /// end — now a four-factor product (world-map W48 adds <see cref="SeasonMilli"/>), so the
    /// divisor is <c>1_000_000_000</c>, not <c>1_000_000</c>. `Sum` is already `long` and every
    /// multiplicand promotes from it, so the product cannot silently wrap the way an `int` chain
    /// would (`WorldState.cs:137-144`'s own recorded defect) — `checked` makes that a thrown
    /// exception rather than a hoped-for property (AGENTS.md's overflow rule).
    /// </summary>
    public long Total => checked(Sum * IntensityMilli * HandicapMilli * SeasonMilli / 1_000_000_000);
}

/// <summary>
/// upkeep(sector) = ( base + Σ garrison + f(development, danger) ) × intensity/1000 × handicap/1000
/// × season/1000 (spec-loam-calc.md #3, season term added world-map W48). No distance term (map
/// finding A3, an assumption on an open decision — intensity already carries remoteness, and a
/// second multiplier would be invisible on screen and unfalsifiable in tuning). No structure term
/// yet — structures arrive in `loam-structures`, wave 4.
/// </summary>
public static class LoamUpkeep
{
    /// <summary>The truth side: reads the sector's own state, its garrison, its faction's handicap, and the turn's season.</summary>
    public static long For(WorldState world, WorldSector sector) => BreakdownFor(world, sector).Total;

    /// <summary>
    /// Same gating and inputs as <see cref="For(WorldState, WorldSector)"/>, but returns the operands
    /// instead of collapsing straight to the total — world-stage W10's projection reads this, not a
    /// second copy of the formula.
    /// </summary>
    public static LoamUpkeepBreakdown BreakdownFor(WorldState world, WorldSector sector)
    {
        if (sector.OwnerFactionId is not { } owner) return default; // G-B: every field zero, Total 0

        // G-C: a faction with no loam source anywhere is exempt entirely, mirroring
        // `SupplyGraph.cs`'s "the wild do not starve for want of a capital they never had". The
        // wild are a hazard, not an empire, and charging upkeep nobody can ever pay is a countdown
        // to a loss nobody chose, not a difficulty curve.
        var hasSourceAnywhere = world.Sectors.Any(s =>
            string.Equals(s.OwnerFactionId, owner, StringComparison.Ordinal)
            && s.Slots.Any(sl => SlotTypeCatalog.Get(sl.SlotTypeId).Kind == SlotKind.Rootbed));
        if (!hasSourceAnywhere) return default;

        var handicapMilli = world.Factions.First(f => f.FactionId == owner).UpkeepHandicapMilli;
        var garrisonMembers = world.Entities
            .Where(e => string.Equals(e.AtSectorId, sector.SectorId, StringComparison.Ordinal))
            .Sum(e => e.Members.Count);

        // Terrain-or-self-knowledge, the same argument `LoamUpkeep.cs:33-39`'s own doc comment
        // already makes for garrison/development/danger/intensity: a season is never fogged, so
        // truth reads it straight off the turn, exactly like `TurnCalendar.SeasonOf` itself does.
        var seasonMilli = World.WorldTuningHub.Tuning.Seasons.UpkeepMilli[Turn.TurnCalendar.SeasonOf(world.CurrentTurn)];

        return Breakdown(garrisonMembers, sector.DevelopmentLevel, sector.DangerBand, sector.FractureIntensityMilli, handicapMilli, seasonMilli);
    }

    /// <summary>
    /// The belief side: a garrison count, development, danger and intensity are all terrain or
    /// self-knowledge (never fogged for your own decision-making), and the handicap is a faction's
    /// own declared lever; the season is calendar, never fogged either. Quantities promote to
    /// <c>long</c> from the first multiplication, so the expression cannot silently overflow into
    /// negative upkeep the way an <c>int</c> version would at legal inputs — multiply once across
    /// everything, divide exactly once.
    /// </summary>
    public static long For(int garrisonMembers, int developmentLevel, int dangerBand, int intensityMilli, int handicapMilli, int seasonMilli) =>
        Breakdown(garrisonMembers, developmentLevel, dangerBand, intensityMilli, handicapMilli, seasonMilli).Total;

    /// <summary>Pure operand builder behind both overloads above — the one place the four additive terms are named.</summary>
    public static LoamUpkeepBreakdown Breakdown(int garrisonMembers, int developmentLevel, int dangerBand, int intensityMilli, int handicapMilli, int seasonMilli) =>
        new(
            Base: LoamPolicy.BaseUpkeepPerSector,
            Garrison: (long)garrisonMembers * LoamPolicy.GarrisonUpkeepPerMember,
            Development: (long)developmentLevel * LoamPolicy.DevelopmentUpkeepPerLevel,
            Danger: (long)dangerBand * LoamPolicy.DangerUpkeepPerBand,
            IntensityMilli: intensityMilli,
            HandicapMilli: handicapMilli,
            SeasonMilli: seasonMilli);
}
