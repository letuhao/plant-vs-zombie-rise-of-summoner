using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Actions.Cost;

public enum CostPayOutcome
{
    Paid,
    InsufficientFunds
}

/// <summary><see cref="ShortfallResourceId"/> is set only on <see cref="CostPayOutcome.InsufficientFunds"/>
/// — the FIRST resource (in row order) validation found unaffordable, matching
/// <see cref="UsabilityReason.CannotAfford"/>'s own single-detail shape.</summary>
public readonly record struct CostPayResult(CostPayOutcome Outcome, string? ShortfallResourceId)
{
    public static readonly CostPayResult Success = new(CostPayOutcome.Paid, null);
    public static CostPayResult Shortfall(string resourceId) => new(CostPayOutcome.InsufficientFunds, resourceId);
}

/// <summary>
/// T17 (action-todo.md, spec-action-costs.md §3): validate all, then consume all — never partially.
/// "Rollback" reduces to "nothing is ever spent until every row has already been peeked affordable",
/// so a failure never needs undoing (<see cref="ActorResourcePools.TrySpend"/> is itself all-or-
/// nothing per pool, and this ledger never calls it before every row has cleared validation).
///
/// <para><b>Committing is what costs, not landing</b> (§3) — this ledger has no notion of whether an
/// action hits; a caller pays at <c>onCommit</c>, and again per resolve tick for a <c>perTick</c> row,
/// regardless of outcome.</para>
///
/// <para><b>Cost rides the rung</b> (§5): <c>cost(rung, Θ) = anchorCost(Θ) × costMulti(rung)</c>.
/// <c>costMulti(rung)</c> is <see cref="RungTable"/>'s already-shipped, already-authorized per-mille
/// multiplier (`A12`, no new formula). <c>anchorCost(Θ)</c> has **no row yet** in
/// ssot-power-scale.md §10's closed inventory — inventing one here would be exactly the private
/// <c>f(level)</c> AGENTS.md bans. <paramref name="thetaScaleMilliOf"/> is therefore a seam, the same
/// shape as <see cref="IAffordabilityCheck"/> itself: <c>null</c> (the default) is inert (1000‰, no
/// scaling), and the real anchor formula is a follow-up decision this module does not make for
/// itself. Cooldown never reads this seam at all — `A12`'s <c>CdMulti</c> is rung-only, by design
/// (§5: "cooldown rides the rung alone... never `Θ`").</para>
/// </summary>
public sealed class CostLedger : IAffordabilityCheck
{
    readonly IReadOnlyDictionary<string, IReadOnlyList<ActionCostRow>> _costsByActionId;
    readonly Func<string, ActorResourcePools> _poolsFor;
    readonly Func<string, ActorDerivedSnapshot> _derivedFor;
    readonly Func<string, int> _rungOf;
    readonly Func<long> _nowTick;
    readonly Func<double, int> _thetaScaleMilliOf;

    public CostLedger(
        IReadOnlyDictionary<string, IReadOnlyList<ActionCostRow>> costsByActionId,
        Func<string, ActorResourcePools> poolsFor,
        Func<string, ActorDerivedSnapshot> derivedFor,
        Func<string, int> rungOf,
        Func<long> nowTick,
        Func<double, int>? thetaScaleMilliOf = null)
    {
        _costsByActionId = costsByActionId ?? throw new ArgumentNullException(nameof(costsByActionId));
        _poolsFor = poolsFor ?? throw new ArgumentNullException(nameof(poolsFor));
        _derivedFor = derivedFor ?? throw new ArgumentNullException(nameof(derivedFor));
        _rungOf = rungOf ?? throw new ArgumentNullException(nameof(rungOf));
        _nowTick = nowTick ?? throw new ArgumentNullException(nameof(nowTick));
        _thetaScaleMilliOf = thetaScaleMilliOf ?? (_ => 1000);
    }

    IReadOnlyList<ActionCostRow> RowsFor(string actionId, ActionCostTiming when)
    {
        if (!_costsByActionId.TryGetValue(actionId, out var rows))
            return Array.Empty<ActionCostRow>();

        var matching = new List<ActionCostRow>(rows.Count);
        foreach (var row in rows)
            if (row.When == when)
                matching.Add(row);
        return matching;
    }

    /// <summary>
    /// Deterministic, no roll — a caller may poll this every frame (a greyed-out button) without
    /// burning the actor's cost-roll rng stream. Uses <see cref="ValueSpec.Max"/> as the affordability
    /// bound: a spread cost never resolves ABOVE its own Max, so "affordable at Max" is never a false
    /// positive against the real, later-rolled amount.
    /// </summary>
    public UsabilityResult Check(string actorKey, string actionId)
    {
        var pools = _poolsFor(actorKey);
        var derived = _derivedFor(actorKey);
        var nowTick = _nowTick();
        var rung = _rungOf(actionId);

        foreach (var row in RowsFor(actionId, ActionCostTiming.OnCommit))
        {
            var bound = ScaledAmount(row.AmountSpec.Max, rung, derived);
            if (pools.Resolve(row.ResourceId, nowTick, derived) < HpFloorAdjustedBound(row, bound))
                return UsabilityResult.Refuse(UsabilityReason.CannotAfford, row.ResourceId);
        }

        return UsabilityResult.Usable;
    }

    /// <summary>
    /// Validate every row for <paramref name="when"/>, then consume every row — never partially. The
    /// FIRST unaffordable resource (row order) is reported and NOTHING is spent, for either row.
    /// </summary>
    public CostPayResult TryPay(string actorKey, string actionId, ActionCostTiming when, AtomRng? rng)
    {
        var pools = _poolsFor(actorKey);
        var derived = _derivedFor(actorKey);
        var nowTick = _nowTick();
        var rung = _rungOf(actionId);
        var rows = RowsFor(actionId, when);
        if (rows.Count == 0) return CostPayResult.Success;

        // Pass 1 — validate ALL, spend NONE. Resolving here (not re-resolving in pass 2) is what
        // guarantees pass 2 pays the EXACT amount pass 1 validated against, even for an OnApply spread
        // cost that would otherwise roll a second, different number on a second Resolve call.
        var amounts = new long[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            var amount = ScaledAmount(rows[i].AmountSpec.Resolve(rng), rung, derived);
            amounts[i] = amount;
            if (pools.Resolve(rows[i].ResourceId, nowTick, derived) < HpFloorAdjustedBound(rows[i], amount))
                return CostPayResult.Shortfall(rows[i].ResourceId);
        }

        // Pass 2 — consume ALL. Every row already cleared validation against the SAME nowTick/derived
        // snapshot, so this cannot fail — TrySpend's own bool is asserted, not branched on, because a
        // false here would mean pass 1 and pass 2 disagreed about the actor's own state mid-call,
        // which never happens in this single-threaded ledger.
        for (var i = 0; i < rows.Count; i++)
        {
            var spent = pools.TrySpend(rows[i].ResourceId, amounts[i], nowTick, derived);
            System.Diagnostics.Debug.Assert(spent, "pass 2 spend failed after pass 1 validated it");
        }

        return CostPayResult.Success;
    }

    /// <summary>aura-skill T14 (`resource-hub-ssot.md`): an `hp` cost floors at 1 by default — the
    /// affordability bound is raised by exactly 1 so a payment that would leave the actor at 0 or
    /// below reads as unaffordable (`CannotAfford("hp")`), the same typed refusal every other
    /// shortfall already uses. A row that opted into lethality (<see cref="ActionCostRow.AllowLethal"/>)
    /// is untouched — its bound is the raw amount, exactly like every non-hp resource.</summary>
    static long HpFloorAdjustedBound(ActionCostRow row, long bound) =>
        row.ResourceId == "hp" && !row.AllowLethal ? bound + 1 : bound;

    long ScaledAmount(int baseAmount, int rung, ActorDerivedSnapshot derived)
    {
        if (!RungPolicy.Table.TryResolve(rung, out var multipliers))
            throw new ArgumentOutOfRangeException(nameof(rung), rung, "no rung row for this action's rung");

        var theta = derived.Get(DerivedStatChannels.ProgressionPower);
        var afterRung = CurveTable.ApplyMilli(baseAmount, multipliers.CostMulti);
        return CurveTable.ApplyMilli(afterRung, _thetaScaleMilliOf(theta));
    }
}
