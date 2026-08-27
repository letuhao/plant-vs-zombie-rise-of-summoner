namespace FusionRpg.Core.Balance.Analytic;

/// <summary>
/// class-system-todo.md P4.5 — the deterministic per-round action-selection walk.
///
/// <para><b>Unlike <see cref="StrikeMixture"/>/<see cref="PhaseModel"/>/<see cref="StatusUptime"/>,
/// there is no shipped resolver to call here.</b> The real action-cost system
/// (action-map.md's <c>A3</c>, <c>spec-action-costs.md</c>) is specced but not built —
/// <c>DerivedStatChannels.ResourceIds</c>'s six ids are registered and, per that spec's own §1,
/// currently "have no reader". The only existing reference for this specific piece is the POC itself,
/// <c>tools/CombatSim/ActionEconomy.cs</c> (<c>ActorPools</c>/<c>ActionPolicy</c>) together with its
/// shipped action-set data <c>tools/CombatSim/actions/basic.json</c> — which
/// <c>spec-deterministic-core.md</c> §3's own <c>--actions basic</c> command targets directly as the
/// thing Phase 4 must reproduce. This module ports that mechanism — lazy regen, priority-ordered
/// affordability, pay-on-commit — deliberately, since there is nothing else to defer to for this piece.
/// </para>
///
/// <para><b>No RNG here either</b> (spec-deterministic-core.md §5): which action fires each round is
/// fully determined by starting pool state, costs and priorities — only the swing's OWN outcome (miss,
/// parry, block, clean, crit — <see cref="StrikeMixture"/>) is probabilistic. This is a bounded,
/// deterministic walk, not a simulation; <paramref name="rounds"/> is the same kind of integration
/// bound <c>RoundLimit</c> is (spec-deterministic-core.md §5) — a ceiling on the computation, never a
/// balance parameter.</para>
///
/// <para><b>Order per round: regen, then choose, then pay.</b> <c>spec-action-costs.md</c> §2 frames a
/// pool as a continuous lazy function of elapsed time (<c>value(now) = clamp(stored + rate×Δt, 0,
/// max)</c>), not a discrete "tick then act" step order — advancing exactly one round's worth of that
/// function immediately before choosing is the reading closest to "what can I afford right now,
/// having last acted one round ago". Round 1 starts from whatever <paramref name="initialPools"/>
/// says (<c>ActorPools</c>'s own "a run starts full" — the caller's responsibility, not this
/// function's), so a full pool is unaffected by that first regen step (clamped at max already).</para>
/// </summary>
public static class ActionSchedule
{
    public readonly record struct ActionOption(
        string Id, int Priority, double DamageMultiplier,
        string? CostResourceId, long CostShareOfOutputMilli);

    public readonly record struct PoolState(double Value, double Max, double Regen);

    public readonly record struct RoundOutcome(string ActionId, double DamageMultiplier);

    /// <summary>Nominal output a cost is priced against — NOT damage dealt: committing is what costs,
    /// so a miss pays in full (spec-action-costs.md §3; <c>ActionEconomy.cs</c>'s own
    /// <c>NominalOutput</c>, mirrored exactly).</summary>
    public static double NominalOutput(ActionOption a, double baseDamage) => baseDamage * a.DamageMultiplier;

    public static double CostOf(ActionOption a, double baseDamage) =>
        a.CostResourceId is null ? 0.0 : NominalOutput(a, baseDamage) * (a.CostShareOfOutputMilli / 1000.0);

    /// <param name="options">The action set. Must contain at least one action with
    /// <c>CostResourceId == null</c> — a free fallback (<c>ActionSet.Load</c>'s own validation,
    /// mirrored: "a dry actor has nothing to do" otherwise).</param>
    /// <param name="initialPools">Starting pool state per resource id, keyed the same way
    /// <see cref="ActionOption.CostResourceId"/> values are. A resource with no entry here reads as
    /// always-zero (never affordable) — callers must supply every resource any costed action names.</param>
    /// <param name="baseDamage">Nominal per-swing base damage — the same value that would otherwise go
    /// into <see cref="StrikeMixture.Compute"/> before this round's chosen action's multiplier is
    /// applied to it.</param>
    /// <param name="rounds">How many rounds to walk.</param>
    public static IReadOnlyList<RoundOutcome> Walk(
        IReadOnlyList<ActionOption> options, IReadOnlyDictionary<string, PoolState> initialPools,
        double baseDamage, int rounds)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (initialPools is null) throw new ArgumentNullException(nameof(initialPools));
        if (options.Count == 0)
            throw new ArgumentException("must contain at least one action", nameof(options));
        if (!options.Any(a => a.CostResourceId is null))
            throw new ArgumentException(
                "must contain at least one free action (CostResourceId == null) -- a dry actor has nothing to do",
                nameof(options));
        if (double.IsNaN(baseDamage) || baseDamage < 0.0)
            throw new ArgumentOutOfRangeException(nameof(baseDamage), baseDamage, "must be non-negative");
        if (rounds < 0)
            throw new ArgumentOutOfRangeException(nameof(rounds), rounds, "must be non-negative");

        var ordered = options.OrderBy(a => a.Priority).ToArray();
        var pools = new Dictionary<string, PoolState>(initialPools, StringComparer.Ordinal);
        var result = new List<RoundOutcome>(rounds);

        for (var round = 0; round < rounds; round++)
        {
            foreach (var id in pools.Keys.ToArray())
            {
                var p = pools[id];
                pools[id] = p with { Value = Math.Clamp(p.Value + p.Regen, 0.0, p.Max) };
            }

            var chosen = Choose(ordered, pools, baseDamage);
            Pay(chosen, pools, baseDamage);
            result.Add(new RoundOutcome(chosen.Id, chosen.DamageMultiplier));
        }

        return result;
    }

    static ActionOption Choose(IReadOnlyList<ActionOption> ordered, IReadOnlyDictionary<string, PoolState> pools, double baseDamage)
    {
        foreach (var a in ordered)
        {
            if (a.CostResourceId is null) return a;
            var have = pools.TryGetValue(a.CostResourceId, out var p) ? p.Value : 0.0;
            if (have >= CostOf(a, baseDamage)) return a;
        }

        // Unreachable given the constructor-level validation above (a free action always short-
        // circuits the loop before it exhausts) -- kept because ActionPolicy.Choose, the thing this
        // ports, keeps the identical defensive line.
        return ordered[^1];
    }

    static void Pay(ActionOption chosen, Dictionary<string, PoolState> pools, double baseDamage)
    {
        if (chosen.CostResourceId is null) return;
        var p = pools[chosen.CostResourceId];
        pools[chosen.CostResourceId] = p with { Value = p.Value - CostOf(chosen, baseDamage) };
    }
}
