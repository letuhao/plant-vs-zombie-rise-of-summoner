using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Status;

public enum StatusKind
{
    OverTime,
    Counter,
    Buff,
    Debuff,
    CrowdControl,
    Contagion,
    Meter,
    UnityCc
}

public enum StatusStacking
{
    Refresh,
    Replace,
    Coexist
}

public enum StatusPayloadKind
{
    PulseHp,
    UnityCc,
    ModifyStat,
    Spread
}

public sealed record StatusDef(
    string StatusId,
    StatusKind Kind,
    string Family,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Tags,
    StatusStacking Stacking,
    IReadOnlyList<StatusPayloadKind> PayloadKinds);

public sealed class UnknownStatusIdException : Exception
{
    public string StatusId { get; }

    public UnknownStatusIdException(string statusId)
        : base($"Unknown statusId: {statusId}")
    {
        StatusId = statusId;
    }
}

public sealed class StatusCatalog
{
    readonly Dictionary<string, StatusDef> _defs = new(StringComparer.OrdinalIgnoreCase);

    public StatusCatalog(IEnumerable<StatusDef>? defs = null)
    {
        if (defs != null)
            foreach (var d in defs)
                Register(d);
    }

    public void Register(StatusDef def)
    {
        if (string.IsNullOrWhiteSpace(def.StatusId))
            throw new ArgumentException("StatusId required");
        _defs[def.StatusId] = def;
    }

    public bool TryGet(string statusId, out StatusDef def) =>
        _defs.TryGetValue(statusId, out def!);

    public StatusDef GetRequired(string statusId)
    {
        if (!TryGet(statusId, out var def))
            throw new UnknownStatusIdException(statusId);
        return def;
    }

    public IReadOnlyList<StatusDef> All() =>
        _defs.Values.OrderBy(d => d.StatusId, StringComparer.OrdinalIgnoreCase).ToList();
}

public interface IStatusRng
{
    double NextUnit();
}

public sealed class FixedStatusRng : IStatusRng
{
    readonly double _value;

    public FixedStatusRng(double value) => _value = value;

    public double NextUnit() => _value;
}

public sealed class SeededStatusRng : IStatusRng
{
    readonly ICombatRng _rng;

    public SeededStatusRng(ICombatRng rng) => _rng = rng;

    public double NextUnit() => _rng.Next(1_000_000) / 1_000_000.0;
}

/// <summary>Two-phase L2b apply math — actor-hub-ssot.md §4.</summary>
public sealed class ResistanceEvaluator
{
    public static double Sigmoid(double x, double steepness = 1.0) =>
        1.0 / (1.0 + Math.Exp(-x * steepness));

    public StatusApplyResult Evaluate(
        StatusApplyRequest request,
        ActorDerivedSnapshot? attacker,
        ActorDerivedSnapshot defender,
        IStatusRng rng)
    {
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        var category = StatusCategoryRegistry.GetRequiredCategory(request.StatusId);
        var tags = request.ImmunityTags ?? Array.Empty<string>();

        foreach (var tag in tags)
        {
            var immuneKey = DerivedStatChannels.StatusImmune(tag);
            if (defender.Get(immuneKey) >= 1.0)
            {
                return Resisted(request, 0, 0, 0, 0, 0, StatusResistReason.Immunity);
            }
        }

        var attackerLess = request.AttackerLess || attacker == null;
        var attackerSnap = ResolveAttackerSnapshot(request, attacker);
        var delta = ComputeDelta(request.StatusId, category, attackerSnap, defender, attackerLess);
        var netFactor = ComputeNetFactor(delta);

        foreach (var tag in tags)
        {
            var reductionKey = DerivedStatChannels.StatusImmuneReduction(tag);
            var reduction = Math.Clamp(defender.Get(reductionKey), 0, 1);
            netFactor *= 1.0 - reduction;
        }

        if (netFactor <= StatusPolicy.MinNetFactor)
        {
            return Resisted(request, delta, netFactor, 0, 0, 0, StatusResistReason.PotencyFloor);
        }

        // T3.2 (audit F3): no longer scaled by matchPower. Under linear Theta, a power-scaled divisor
        // makes a FIXED gap DECAY as both sides climb (measured: gap-5 p_apply 0.5010 at Theta=10,
        // 0.5000 at Theta=10,000) -- every apply converges to a coin flip regardless of how far ahead
        // the attacker is. A constant divisor is the one regime where both halves of the evaluator
        // (delta's contest, and the roll built from it) read power the same way.
        var effectiveApplyScale = Math.Max(
            StatusPolicy.ApplyScaleFloor,
            StatusPolicy.ApplyScaleKForCategory(category));
        var steepness = StatusPolicy.ApplySteepnessForCategory(category);
        var pApply = Sigmoid(delta / effectiveApplyScale, steepness);
        var pFinal = request.GrantChance * pApply;

        if (rng.NextUnit() >= pFinal)
        {
            return Resisted(request, delta, netFactor, pApply, pFinal, effectiveApplyScale, StatusResistReason.ApplyRoll);
        }

        var effectiveMagnitude = request.BaseMagnitude * netFactor;
        var effectiveDuration = request.BaseDuration * netFactor;

        if (IsUseless(effectiveMagnitude, effectiveDuration))
        {
            return Resisted(request, delta, netFactor, pApply, pFinal, effectiveApplyScale, StatusResistReason.UselessMagnitude);
        }

        return new StatusApplyResult(
            Applied: true,
            ResistReason: null,
            Delta: delta,
            NetFactor: netFactor,
            PApply: pApply,
            PFinal: pFinal,
            EffectiveApplyScale: effectiveApplyScale,
            EffectiveMagnitude: effectiveMagnitude,
            EffectiveDuration: effectiveDuration);
    }

    static ActorDerivedSnapshot ResolveAttackerSnapshot(StatusApplyRequest request, ActorDerivedSnapshot? attacker)
    {
        if (request.AttackerLess || attacker == null)
            return ActorDerivedSnapshot.AttackerLess();
        return attacker;
    }

    public static double ComputeDelta(
        string statusId,
        string category,
        ActorDerivedSnapshot attacker,
        ActorDerivedSnapshot defender,
        bool attackerLess = false)
    {
        var totalPower = (StatusPolicy.IncludeTierPowerInDelta ? attacker.TierPower : 0)
                         + attacker.Get(DerivedStatChannels.StatusPowerOmni)
                         + attacker.Get($"status.power.{category}")
                         + attacker.Get(DerivedStatChannels.StatusPower(statusId));

        var categoryResist = Math.Min(defender.Get($"status.resist.{category}"), StatusPolicy.CategoryResistCap);
        var perIdResist = Math.Min(defender.Get(DerivedStatChannels.StatusResist(statusId)), StatusPolicy.CategoryResistCap);

        // T3.1 fix (power-plan.md, found via BattleStatusTests going Victory -> Stalemate, not
        // assumed safe): the tier-power term is a CONTEST between two sides, so it is symmetric —
        // either both sides' tier power enter it, or neither does. An attacker-less application
        // (scripted setup statuses, trait/attack riders — BattleEngine.cs's "land attacker-less at
        // t0") has no real attacker side to contest with; without this guard, ResistFromPowerRatio=1.0
        // makes attacker.TierPower=0 net negative against ANY normal-power defender, netFactor clamps
        // to MinNetFactor (0.0), and the scripted effect becomes permanently, completely inert.
        // Category/per-id/omni resist (actual immunities and resistances) still apply normally.
        var totalResist = (attackerLess ? 0 : defender.TierPower * StatusPolicy.ResistFromPowerRatio)
                          + defender.Get(DerivedStatChannels.StatusResistOmni)
                          + categoryResist
                          + perIdResist;

        return totalPower - totalResist;
    }

    // T3.2 (audit F4): a raw delta used directly as a multiplier made parity and +1 both give 1.0x
    // but +2 give 2.0x -- a cliff, and one retired world (Wa=25) gave 25x. Normalizing by
    // NetFactorScale removes the cliff and retires the delta==0 special case: the linear formula
    // already gives exactly 1.0 there with no branch (1 + 0/scale == 1), which is also why the
    // clamp's floor (MinNetFactor) is what makes a heavily negative delta fully-resisted, not a
    // hardcoded zero.
    public static double ComputeNetFactor(double delta) =>
        Math.Clamp(1.0 + delta / StatusPolicy.NetFactorScale, StatusPolicy.MinNetFactor, StatusPolicy.MaxNetFactor);

    static bool IsUseless(double magnitude, double duration) =>
        Math.Abs(magnitude) < 1e-9 && duration <= 0;

    static StatusApplyResult Resisted(
        StatusApplyRequest request,
        double delta,
        double netFactor,
        double pApply,
        double pFinal,
        double effectiveApplyScale,
        StatusResistReason reason) =>
        new(
            Applied: false,
            ResistReason: reason,
            Delta: delta,
            NetFactor: netFactor,
            PApply: pApply,
            PFinal: pFinal,
            EffectiveApplyScale: effectiveApplyScale,
            EffectiveMagnitude: 0,
            EffectiveDuration: 0);
}
