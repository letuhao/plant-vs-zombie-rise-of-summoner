using FusionRpg.Core.World.Turn;

namespace FusionRpg.Core.World.Loam;

/// <summary>
/// Wakes `Production` and `Pressure` up (spec-loam-turn.md). Pure in <c>(state, seed)</c> like every
/// other phase — no wall clock, no unowned RNG. Kept out of `TurnEngine.cs` for the same reason
/// `SiegePhase`/`MovementPhase` are their own files: that file is already the busiest in the module.
/// </summary>
public static class LoamPhases
{
    /// <summary>
    /// Yield per sector into <c>LoamStock</c>, capped at <see cref="LoamPolicy.LoamCapacity"/>.
    /// Overflow above the cap is lost and said to be lost, naming the sector — a per-faction summary
    /// would hide *which* sector is wasting, which is the only actionable half of the fact.
    /// </summary>
    public static WorldState Production(WorldState world, TurnReport report, string phase)
    {
        var sectors = new List<WorldSector>(world.Sectors.Count);

        foreach (var sector in world.Sectors)
        {
            var yield = LoamProduction.For(sector);
            if (yield == 0)
            {
                sectors.Add(sector);
                continue;
            }

            // The cap throttles new accrual only — it never claws back stock already held, the same
            // fix the economy harness (L9) needed for its own local ledger.
            var room = Math.Max(0, LoamPolicy.LoamCapacity - sector.LoamStock);
            var added = Math.Min(room, yield);
            var overflow = yield - added;

            if (overflow > 0)
                report.Add(phase, TurnReportKinds.Event, sector.SectorId, "loam.overflow:" + overflow, sector.SectorId);

            sectors.Add(sector with { LoamStock = sector.LoamStock + added });
        }

        return world with { Sectors = sectors };
    }

    /// <summary>
    /// Upkeep and fade, per component, run *after* <c>SupplyGraph.Run</c> so garrison upkeep reads
    /// the garrison that survived attrition this turn.
    ///
    /// Per faction, per component: sum upkeep, draw it from the pooled stock proportionally
    /// (remainder settled in ordinal id order), and — if the pool cannot cover it — apply the whole
    /// shortfall as fade to the single weakest contributor (worst net balance, ordinal tiebreak).
    /// One sector absorbs one turn's fade; if it is lost, the component recomputes next turn with
    /// one fewer member and a new weakest takes over. That is the countdown the design calls for,
    /// not a same-turn cascade across every member at once.
    /// </summary>
    public static WorldState Pressure(WorldState world, TurnReport report, string phase)
    {
        var stockById = world.Sectors.ToDictionary(s => s.SectorId, s => s.LoamStock, StringComparer.Ordinal);
        var stabilityById = world.Sectors.ToDictionary(s => s.SectorId, s => s.StabilityMilli, StringComparer.Ordinal);
        var lost = new HashSet<string>(StringComparer.Ordinal);

        foreach (var faction in world.Factions)
        {
            // A visible handicap is a balance lever; a silent one is a bug that explains itself
            // away. Named exactly once per faction per turn, regardless of how many components or
            // sectors that faction's upkeep touches this turn.
            if (faction.UpkeepHandicapMilli != 1000)
                report.Add(phase, TurnReportKinds.Event, faction.FactionId,
                    "loam.handicap:" + faction.UpkeepHandicapMilli);

            foreach (var component in TerritoryComponents.For(world, faction.FactionId))
            {
                var upkeep = component.Sum(id => LoamUpkeep.For(world, world.Sectors.Single(s => s.SectorId == id)));
                var available = component.Sum(id => stockById[id]);
                var drawn = Math.Min(available, upkeep);
                var shortfall = upkeep - drawn;

                DrawProportionally(component, stockById, drawn, available);

                if (shortfall > 0)
                {
                    // Same selection the forecast makes a turn early (LoamForecast.Weakest) — one
                    // rule, so the engine and the player-facing warning cannot silently disagree.
                    var weakest = LoamForecast.Weakest(world, component, available, upkeep)!;

                    stabilityById[weakest] = FadePolicy.Apply(stabilityById[weakest], -shortfall);
                    report.Add(phase, TurnReportKinds.Event, faction.FactionId, "loam.shortfall:" + shortfall, weakest);

                    if (stabilityById[weakest] == 0)
                        lost.Add(weakest);
                }
                else
                {
                    // Paid in full: every member recovers, not just the weakest — a component that
                    // can cover its own upkeep is not fading anywhere, which is ideal §12.4's "a
                    // rich core carries a poor frontier indefinitely" made literal.
                    foreach (var id in component)
                        stabilityById[id] = FadePolicy.Apply(stabilityById[id], balance: 1);
                }
            }
        }

        var sectors = world.Sectors.Select(s =>
        {
            if (lost.Contains(s.SectorId))
            {
                report.Add(phase, TurnReportKinds.Event, s.OwnerFactionId ?? "", "loam.lost:" + s.SectorId, s.SectorId);
                return s with { LoamStock = stockById[s.SectorId], StabilityMilli = 0, Phase = SectorPhase.Lost, OwnerFactionId = null };
            }

            return s with { LoamStock = stockById[s.SectorId], StabilityMilli = stabilityById[s.SectorId] };
        }).ToList();

        return world with { Sectors = sectors };
    }

    /// <summary>
    /// The SSOT's stated draw rule: proportional by stock share, remainder in ordinal id order.
    /// Public so <c>LegionSupply</c>'s own top-up pass draws from the same pool the same way,
    /// rather than inventing a second draw rule beside this one (spec-loam-legions.md).
    /// </summary>
    public static void DrawProportionally(
        IReadOnlyList<string> component, Dictionary<string, long> stockById, long drawn, long available)
    {
        if (available == 0) return;

        var remaining = drawn;
        foreach (var id in component.OrderBy(x => x, StringComparer.Ordinal))
        {
            var share = drawn * stockById[id] / available;
            stockById[id] -= share;
            remaining -= share;
        }

        if (remaining > 0)
            stockById[component.OrderBy(x => x, StringComparer.Ordinal).First()] -= remaining;
    }
}
