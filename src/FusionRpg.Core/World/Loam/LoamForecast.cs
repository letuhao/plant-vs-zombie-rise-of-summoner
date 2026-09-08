namespace FusionRpg.Core.World.Loam;

/// <summary>
/// One place both the engine and the player-facing projection call for "which sector takes the
/// fade" and "did that actually zero it out" (spec-loam-fe.md's abandonment surface) — so
/// <see cref="LoamPhases.Pressure"/>, which runs the rule, and the server's forecast, which warns
/// about it a turn early, share the same selection instead of risking two copies drifting apart.
/// </summary>
public static class LoamForecast
{
    /// <summary>
    /// The weakest contributor in a component whose available stock cannot cover its upkeep — worst
    /// per-sector balance, ordinal tiebreak, excluding any warded sector from candidacy entirely
    /// (spec-loam-texture.md's Wardens: warding stops the fade, not the cost, so a warded sector's
    /// upkeep still counts above but it can never be the one selected to absorb the shortfall). Null
    /// when the pool covers its own upkeep in full, or when every member is warded — the caller must
    /// treat both the same way: no fade applied this turn.
    ///
    /// <paramref name="ceded"/> is the faction's own deliberate choice (world-stage W24/W25's `cede`
    /// order) — an *input* to this one selection, never a second code path: it wins only when it is
    /// already a legal candidate (a member of this component, not warded). Ceding warded ground,
    /// someone else's ground, or a sector in another component is simply not a candidate here, so the
    /// default worst-balance ordering answers exactly as if no preference had been filed at all.
    /// </summary>
    public static string? Weakest(
        WorldState world, IReadOnlyList<string> component, long available, long upkeep, string? ceded = null)
    {
        if (available >= upkeep) return null;

        var candidates = component
            .Where(id => world.Sectors.First(s => s.SectorId == id).WardenBindingId is null)
            .ToList();
        if (candidates.Count == 0) return null;

        if (ceded is not null && candidates.Contains(ceded, StringComparer.Ordinal))
            return ceded;

        return candidates
            .OrderBy(id => LoamBalance.PerSector(world, world.Sectors.First(s => s.SectorId == id)))
            .ThenBy(id => id, StringComparer.Ordinal)
            .First();
    }

    /// <summary>
    /// One turn ahead of <see cref="LoamPhases.Pressure"/>: the stock this component will hold once
    /// this turn's capped accrual lands — the same throttle <see cref="LoamPhases.Production"/>
    /// applies, so this never over-counts a sector already sitting at capacity.
    /// </summary>
    public static long ProjectedStock(IReadOnlyList<string> component, WorldState world)
    {
        long total = 0;
        foreach (var id in component)
        {
            var sector = world.Sectors.First(s => string.Equals(s.SectorId, id, StringComparison.Ordinal));
            var yield = LoamProduction.For(sector);
            var room = Math.Max(0, LoamPhases.EffectiveCapacity(sector) - sector.LoamStock);
            total += sector.LoamStock + Math.Min(room, yield);
        }

        return total;
    }

    /// <summary>
    /// The sector the engine will actually release (stability to zero, ground lost) next turn if
    /// nothing changes between now and then — not merely fade further, release outright. This is the
    /// marker spec-loam-fe.md's abandonment surface asks for: visible before it happens, not after.
    ///
    /// <paramref name="ceded"/> forwards straight to <see cref="Weakest"/> so a caller who already
    /// knows the faction's pending `cede` order (world-stage W26's `/state` route) predicts the same
    /// sector the engine will actually release, instead of the two silently disagreeing.
    /// </summary>
    public static string? WillRelease(WorldState world, IReadOnlyList<string> component, string? ceded = null)
    {
        var upkeep = component.Sum(id => LoamUpkeep.For(world, world.Sectors.First(s => s.SectorId == id)));
        var projected = ProjectedStock(component, world);
        var weakest = Weakest(world, component, projected, upkeep, ceded);
        if (weakest is null) return null;

        var shortfall = upkeep - projected;
        var weakestSector = world.Sectors.First(s => s.SectorId == weakest);
        return FadePolicy.Apply(weakestSector.StabilityMilli, -shortfall) == 0 ? weakest : null;
    }
}
