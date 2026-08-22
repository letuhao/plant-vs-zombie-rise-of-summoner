namespace FusionRpg.Core.World.Topology;

/// <summary>
/// What it costs the empire to lose one sector (spec-world-topology.md) — the module's whole point.
///
/// For each sector, recompute all-pairs travel cost with it removed and sum how much worse every
/// surviving pair got. One number with three readings:
///
/// <list type="bullet">
/// <item>pairs become unreachable ⇒ it is an articulation point, and the number is enormous</item>
/// <item>large but finite ⇒ a chokepoint; everything still connects, but the long way round</item>
/// <item>near zero ⇒ redundant. Hold it lightly</item>
/// </list>
///
/// That is the number a garrison decision wants: not "is this valuable" — the value matrix owns
/// that — but "is this load-bearing". A junction is worth defending even if it produces nothing.
///
/// `O(V⁴)` for the sweep, which is fine at six sectors and fine at sixty. The cliff and the two ways
/// off it are in the spec; recomputed per turn like <c>SupplyGraph</c>, never cached.
/// </summary>
public static class ReconnectionCost
{
    /// <param name="include">Null means the whole map; otherwise the empire to ask about.</param>
    public static IReadOnlyDictionary<string, long> For(WorldState world, IReadOnlySet<string>? include = null)
    {
        var scope = include is null
            ? world.Sectors.Select(s => s.SectorId).ToHashSet(StringComparer.Ordinal)
            : include.ToHashSet(StringComparer.Ordinal);

        var result = new Dictionary<string, long>(StringComparer.Ordinal);

        var whole = AllPairsCost.Compute(LaneGraph.Build(world, scope));
        var order = scope.OrderBy(id => id, StringComparer.Ordinal).ToList();

        foreach (var lost in order)
        {
            // Two sectors cannot be disconnected from each other by removing one of them.
            if (scope.Count < 3)
            {
                result[lost] = 0;
                continue;
            }

            var survivors = scope.Where(id => !string.Equals(id, lost, StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);

            var after = AllPairsCost.Compute(LaneGraph.Build(world, survivors));

            // Compare like with like: only pairs that survive the loss, measured both before and
            // after. Counting pairs that involved the lost sector would charge the empire for
            // journeys nobody can make any more, which is a different question.
            long before = 0, now = 0;
            foreach (var a in survivors)
            foreach (var b in survivors)
            {
                if (string.Equals(a, b, StringComparison.Ordinal)) continue;
                before += whole.Between(a, b);
                now += after.Between(a, b);
            }

            result[lost] = Math.Max(0, now - before);
        }

        return result;
    }
}
