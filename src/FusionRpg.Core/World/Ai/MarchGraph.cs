using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Topology;

namespace FusionRpg.Core.World.Ai;

/// <summary>
/// Where an army can actually walk, as one faction believes it
/// (spec-ai-commander.md §Two graphs, and not confusing them).
///
/// This is <see cref="LaneGraph"/> under <see cref="LaneLens.March"/>, built from belief rather than
/// the truth — the whole of the module's contribution here is *which* lens and *whose* map.
///
/// Belief makes it **optimistic**: a lane with neither end in sight reads open whether it is or not,
/// so a faction routes confidently over a bridge that is down and finds out by arriving. That is the
/// same optimism believed supply has, and it is deliberate — fog you can plan around is not fog.
///
/// **A test written against `first-light` cannot tell this from the supply lens.** All six of its
/// lanes carry supply, so the two coincide there and a policy built on the wrong one would pass
/// everything we have. The distinction needs a shape chosen to expose it.
/// </summary>
public static class MarchGraph
{
    /// <summary>
    /// The believed march graph.
    ///
    /// The climate lookup is passed but **not consulted here**: <see cref="LaneGraph"/> prices every
    /// lane with a null banner on purpose, because topology is about the ground rather than who
    /// happens to be walking it, and the ley discount belongs to a particular legion's march. It is
    /// handed over so the seam is complete for `ReachMap`, which *is* per-entity and does have a
    /// banner. Said plainly because a reader would otherwise reasonably assume march costs here are
    /// fog-affected. They are not.
    /// </summary>
    public static LaneGraph Of(IWorldView view, IReadOnlySet<string>? include = null) =>
        LaneGraph.Build(view.SectorIds, view.Lanes, ClimateOf(view), include, LaneLens.March);

    /// <summary>
    /// What a faction knows about a sector's climate — which is what decides, for a legion carrying a
    /// matching banner, whether it expects a ley lane's discount. A sector it has never seen has no
    /// climate as far as it knows, so it **over-prices** that march. An army plans with what it has.
    ///
    /// Consulted by per-entity routing (`ReachMap`), not by <see cref="Of"/> — see above.
    /// </summary>
    public static Func<string, ElementTypeId?> ClimateOf(IWorldView view) =>
        sectorId => view.Believed(sectorId)?.Climate;
}
