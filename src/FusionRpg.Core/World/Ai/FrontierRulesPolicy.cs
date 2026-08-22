using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Movement;
using FusionRpg.Core.World.Turn;

namespace FusionRpg.Core.World.Ai;

/// <summary>
/// The one policy that plays (spec-ai-commander.md §The decision layer).
///
/// Ordered rules, first match wins, **at most one order per entity**. Rules rather than scoring,
/// deliberately: scoring wants an economy to argue with and there is not one until
/// <c>sector-development</c>. The tables underneath — threat, value, reach, supply — are what both a
/// rule list and a scorer need, and they are what this module was really building.
///
/// Every decision is made from <see cref="IWorldView"/>, so Zomboss plays blind on exactly the terms
/// you do. When he walks into something, it is because his last report was six turns old.
/// </summary>
public sealed class FrontierRulesPolicy : IFactionPolicy
{
    public const string Id = "frontier-rules";

    public static readonly FrontierRulesPolicy Instance = new();

    /// <summary>Wounds above this, in per-mille of a member's health, are worth standing still for.</summary>
    public const int RecoverAtMilli = 400;

    /// <summary>How far a legion will travel to look at ground nobody has seen.</summary>
    public const int ExploreTurns = 3;

    public string PolicyId => Id;

    public IReadOnlyList<PolicyOrder> Decide(IWorldView view, ulong seed)
    {
        var orders = new List<PolicyOrder>();

        var defensive = ThreatMap.For(view, ThreatReading.Defensive);
        var supplied = BelievedSupply.ConnectedSectors(view);

        foreach (var entity in view.OwnForces.OrderBy(e => e.EntityId, StringComparer.Ordinal))
        {
            // A force beaten in the field spends its next turn recovering; ordering it about would
            // only fill the turn report with drops.
            if (entity.Routed) continue;

            var reach = ReachMap.For(view, entity);
            var value = ValueMap.For(view, defensive, reach);

            var order = Defend(view, entity, defensive, reach)
                        ?? Finish(view, entity)
                        ?? Take(view, entity)
                        ?? Recover(view, entity, supplied)
                        ?? Explore(view, entity, reach)
                        ?? Expand(view, entity, value, reach)
                        ?? Hold(view, entity);

            orders.Add(order);
        }

        // A faction with nothing left to command still has to say something, or the log cannot tell
        // "chose to do nothing" from "was never asked".
        if (orders.Count == 0) orders.AddRange(StandFastPolicy.Instance.Decide(view, seed));

        return orders;
    }

    // ---- 1. Defend ---------------------------------------------------------------------------

    /// <summary>
    /// A Seat of mine is threatened by more than is already standing on it.
    ///
    /// Compared against the garrison rather than against zero. Threat *spreads*, so on a small map
    /// almost everywhere carries some — a rule that fired on any threat at all would fire every turn
    /// for every legion, and the empire would never expand again.
    /// </summary>
    static PolicyOrder? Defend(
        IWorldView view, WorldEntity entity,
        IReadOnlyDictionary<string, long> threat, IReadOnlyDictionary<string, int> reach)
    {
        string? worst = null;
        long margin = 0;

        foreach (var sectorId in view.SectorIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (view.Believed(sectorId) is not { } believed) continue;
            if (!string.Equals(believed.OwnerFactionId, view.FactionId, StringComparison.Ordinal)) continue;
            if (believed.Slots.All(s => s.SlotTypeId != SlotTypeCatalog.SeatSlotTypeId)) continue;
            if (!reach.ContainsKey(sectorId)) continue;

            var incoming = threat.TryGetValue(sectorId, out var t) ? t : 0;
            var garrison = Garrison(view, sectorId);

            // One comparison, not two. `margin` starts at zero, so "threat exceeds the garrison" and
            // "this is the worst shortfall so far" are the same test on the first sector — the
            // separate guard that used to sit above this was redundant, and a mutant that deleted it
            // survived every test, which is how we found out.
            if (incoming - garrison <= margin) continue;
            margin = incoming - garrison;
            worst = sectorId;
        }

        if (worst is null) return null;
        if (string.Equals(entity.AtSectorId, worst, StringComparison.Ordinal)) return null;   // already home

        var path = Route(view, entity, worst);
        return path is null
            ? null
            : Order(view, entity, WorldCommandKinds.Move, $"defend {worst}, threat {margin} over the garrison",
                lanePath: path);
    }

    /// <summary>What this faction believes it already has standing in one of its sectors.</summary>
    static long Garrison(IWorldView view, string sectorId)
    {
        if (view.Believed(sectorId) is not { } believed) return 0;

        long total = 0;
        foreach (var force in believed.Forces)
            if (string.Equals(force.OwnerFactionId, view.FactionId, StringComparison.Ordinal))
                total += force.Defensive;

        return total;
    }

    // ---- 2. Finish ---------------------------------------------------------------------------

    /// <summary>Standing where a guard still holds a slot, with nothing hostile in sight.</summary>
    static PolicyOrder? Finish(IWorldView view, WorldEntity entity)
    {
        if (entity.AtSectorId is not { } here) return null;
        if (view.Believed(here) is not { } believed) return null;
        if (believed.Forces.Any(f => ZoneOfControl.IsHostile(f.OwnerFactionId, view.FactionId)
                                     && ZoneOfControl.Projects(f.Kind))) return null;

        // Lowest index first: an arbitrary but *stable* order, so a replay clears the same slot.
        var guarded = believed.Slots
            .Where(s => s.GuardState == GuardState.Intact && s.GuardWaveId != null)
            .OrderBy(s => s.SlotIndex)
            .FirstOrDefault();

        return guarded is null
            ? null
            : Order(view, entity, WorldCommandKinds.Clear, $"clear slot {guarded.SlotIndex} of {here}",
                sectorId: here, slotIndex: guarded.SlotIndex);
    }

    // ---- 3. Take -----------------------------------------------------------------------------

    /// <summary>
    /// Standing on ground that is believed clear and believed unowned.
    ///
    /// Guarded ground is refused here as well as by Finish above, so the two rules could be swapped
    /// without changing an outcome — a mutant proved it. They are ordered this way because *finish
    /// what is in front of you* reads correctly, not because anything depends on it.
    /// </summary>
    static PolicyOrder? Take(IWorldView view, WorldEntity entity)
    {
        if (entity.AtSectorId is not { } here) return null;
        if (view.Believed(here) is not { } believed) return null;
        if (believed.Detail != SectorSight.Full) return null;          // a glimpse reports no slots
        if (believed.OwnerFactionId != null) return null;
        if (believed.Slots.Any(s => s.GuardState == GuardState.Intact && s.GuardWaveId != null)) return null;

        return Order(view, entity, WorldCommandKinds.Claim, $"claim {here}", sectorId: here);
    }

    // ---- 4. Recover --------------------------------------------------------------------------

    /// <summary>Badly hurt, in supply, and not already dug in.</summary>
    static PolicyOrder? Recover(IWorldView view, WorldEntity entity, IReadOnlySet<string> supplied)
    {
        if (entity.AtSectorId is not { } here || !supplied.Contains(here)) return null;

        // Already dug in and still mending: say so. The stance persists on its own, so re-filing it
        // would cost a turn for nothing — but reporting "nothing worth doing" through the five turns
        // a legion spends healing is the audit trail lying about the most legible thing it does all
        // game. Note this ignores `RecoverAtMilli`: that threshold governs whether it is worth
        // *stopping* to heal, not whether healing is what is happening. Both halves were found by
        // playing twenty turns and reading them.
        var wounded = WoundedMilli(entity);
        if (string.Equals(entity.Stance, MovementPolicy.Hold, StringComparison.Ordinal))
            return wounded > 0
                ? Order(view, entity, WorldCommandKinds.StandFast, "holding position, still recovering")
                : null;

        if (wounded < RecoverAtMilli) return null;

        return Order(view, entity, WorldCommandKinds.Stance, "dig in and recover", stance: MovementPolicy.Hold);
    }

    static int WoundedMilli(WorldEntity entity)
    {
        if (entity.Members.Count == 0) return 0;

        long hp = 0, wounds = 0;
        foreach (var member in entity.Members)
        {
            hp += Math.Max(1, member.Hp);
            wounds += member.Wounds;
        }

        return (int)(wounds * 1000 / Math.Max(1, hp));
    }

    // ---- 5. Explore --------------------------------------------------------------------------

    /// <summary>
    /// Ground nobody has seen, close enough to be worth the trip.
    ///
    /// Files the *stance* or the *move*, never both: committing a stance costs the turn it is
    /// committed, so a policy that filed both would watch the move dropped and re-file it forever.
    /// </summary>
    static PolicyOrder? Explore(IWorldView view, WorldEntity entity, IReadOnlyDictionary<string, int> reach)
    {
        var target = view.SectorIds
            .Where(id => view.Believed(id) is null
                         && reach.TryGetValue(id, out var turns) && turns <= ExploreTurns)
            .OrderBy(id => reach[id])
            .ThenBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault();

        if (target is null) return null;

        if (!string.Equals(entity.Stance, MovementPolicy.Scout, StringComparison.Ordinal))
            return Order(view, entity, WorldCommandKinds.Stance, $"scout toward {target}",
                stance: MovementPolicy.Scout);

        var path = Route(view, entity, target);
        return path is null ? null : Order(view, entity, WorldCommandKinds.Move, $"explore {target}", lanePath: path);
    }

    // ---- 6. Expand ---------------------------------------------------------------------------

    /// <summary>The best-valued reachable ground I do not hold, if it is worth having at all.</summary>
    static PolicyOrder? Expand(
        IWorldView view, WorldEntity entity,
        IReadOnlyDictionary<string, SectorValue> value, IReadOnlyDictionary<string, int> reach)
    {
        string? best = null;
        long bestScore = 0;                                   // strictly positive: zero is not worth a march

        foreach (var sectorId in view.SectorIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!reach.ContainsKey(sectorId)) continue;
            if (string.Equals(entity.AtSectorId, sectorId, StringComparison.Ordinal)) continue;
            if (view.Believed(sectorId) is { } believed
                && string.Equals(believed.OwnerFactionId, view.FactionId, StringComparison.Ordinal)) continue;

            var score = value.TryGetValue(sectorId, out var v) ? v.Total : 0;
            if (score <= bestScore) continue;

            bestScore = score;
            best = sectorId;
        }

        if (best is null) return null;

        var path = Route(view, entity, best);
        return path is null
            ? null
            : Order(view, entity, WorldCommandKinds.Move,
                $"expand to {best}, {value[best].Explain()}", lanePath: path);
    }

    // ---- 7. Hold -----------------------------------------------------------------------------

    static PolicyOrder Hold(IWorldView view, WorldEntity entity) =>
        Order(view, entity, WorldCommandKinds.StandFast, "nothing worth doing");

    // ---- plumbing ------------------------------------------------------------------------------

    /// <summary>
    /// The cheapest believed route, as lane ids. Null when there is none — under fog that includes
    /// routes that only *look* open, which is the point.
    /// </summary>
    static IReadOnlyList<string>? Route(IWorldView view, WorldEntity entity, string target)
    {
        var origin = entity.AtSectorId ?? entity.OnLaneTowardSectorId;
        if (origin is null || string.Equals(origin, target, StringComparison.Ordinal)) return null;

        // Breadth-first over believed lanes: fewest lanes rather than cheapest, because a march is
        // spent per lane and the engine walks the path one lane at a time anyway.
        var cameBy = new Dictionary<string, (string From, string LaneId)>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal) { origin };
        var frontier = new Queue<string>();
        frontier.Enqueue(origin);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (string.Equals(current, target, StringComparison.Ordinal)) break;

            foreach (var lane in view.Lanes.OrderBy(l => l.LaneId, StringComparer.Ordinal))
            {
                if (lane.State != LaneState.Open) continue;

                var type = LaneTypeCatalog.Get(lane.TypeId);
                if (type.Gated && lane.GateKeyId != null) continue;

                string next;
                if (string.Equals(lane.FromSectorId, current, StringComparison.Ordinal)) next = lane.ToSectorId;
                else if (!type.OneWay && string.Equals(lane.ToSectorId, current, StringComparison.Ordinal)) next = lane.FromSectorId;
                else continue;

                if (!seen.Add(next)) continue;
                cameBy[next] = (current, lane.LaneId);
                frontier.Enqueue(next);
            }
        }

        if (!cameBy.ContainsKey(target)) return null;

        var path = new List<string>();
        for (var at = target; cameBy.TryGetValue(at, out var step); at = step.From)
            path.Add(step.LaneId);

        path.Reverse();
        return path;
    }

    static PolicyOrder Order(
        IWorldView view, WorldEntity entity, string kind, string reason,
        string? sectorId = null, int? slotIndex = null, string? stance = null,
        IReadOnlyList<string>? lanePath = null) =>
        new(
            new WorldCommand
            {
                CommanderId = view.FactionId,
                CommandId = $"ai-{view.CurrentTurn}-{entity.EntityId}",
                Kind = kind,
                EntityId = kind == WorldCommandKinds.StandFast ? null : entity.EntityId,
                SectorId = sectorId,
                SlotIndex = slotIndex,
                Stance = stance,
                LanePath = lanePath ?? Array.Empty<string>()
            },
            reason);
}
