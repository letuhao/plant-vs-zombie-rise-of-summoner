using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Status;

namespace FusionRpg.Core.Battle;

// This file lives under Core/Actions/ (spec-basic-attack-adoption.md's own Structure section), but
// declares part of BattleEngine itself (namespace FusionRpg.Core.Battle, `partial`) rather than a
// free-standing type: the four adopted steps need ActorState, SelectTarget and IsCcLocked, all
// private to BattleEngine, and duplicating them would be the second private-state copy this whole
// program refuses to create. The authored row lives here; the loop that calls it lives in
// BattleEngine.cs — one seam, two files.

public static partial class BattleEngine
{
    /// <summary>
    /// The authored row for the basic attack (spec-basic-attack-adoption.md §2). Degenerate on
    /// purpose — every timing field at zero proves plumbing and nothing else. `A12`'s real actions
    /// under a real profile are where non-zero timing gets exercised.
    /// </summary>
    public static readonly ActionEnvelope BasicAttackEnvelope = ActionEnvelope.NoOp with
    {
        ActionId = "act.attack",
        Commitment = Commitment.LateBound,
    };

    /// <summary>
    /// The authored targeting rule (spec-targeting.md §2a). `Ordering = SourceOrder` is the field
    /// §3's hazard exists to name: <c>TargetResolver</c> sorts by ordinal ptr, but this action's
    /// actual target comes from the engine's own <see cref="SelectTarget"/>, which is list order —
    /// so the basic attack states which order it means rather than silently disagreeing with the
    /// resolver. This spec is carried as data for that reason; the live loop below still calls
    /// <see cref="SelectTarget"/> directly, because that function also encodes bloodthirsty
    /// targeting and loyal-bodyguard interception — trait behaviour a generic target spec does not
    /// (and should not) express.
    /// </summary>
    public static readonly ActionTargetSpec BasicAttackTargeting = new()
    {
        Mode = ActionTargetMode.Single,
        Relation = ActionRelation.Enemy,
        Ordering = ActionTargetOrdering.SourceOrder,
    };

    enum AttackStepOutcome
    {
        /// <summary>No valid target, or the attacker cannot act — the round's attack phase ends for this actor.</summary>
        Continue,
        /// <summary>No valid target for this attacker at all — hazard 3: the ROUND breaks, it does not continue.</summary>
        Break,
        /// <summary>A hit landed; the caller applies it and runs the trait tail.</summary>
        Proceed,
    }

    readonly record struct AttackStep(AttackStepOutcome Outcome, ActorState? Target, long SignedDelta);

    /// <summary>
    /// The four adopted steps (spec-basic-attack-adoption.md §1), moved here verbatim from the loop
    /// in <see cref="Resolve"/>: active check → CC-lock → target → <c>calculator.Compute</c>.
    /// Everything from the berserker ramp onward is `EngineBehavior` trait logic and stays in the
    /// loop — this module's whole point is to prove the first four steps can be expressed as a
    /// declared action without moving a single byte, not to relocate the trait tail.
    /// </summary>
    static AttackStep RunBasicAttackStep(
        ActorState attacker, List<ActorState> actors, StatusRuntime status, DateTimeOffset now,
        OverlayCombatCalculator calculator, ICombatRng critRng, BattleTrace? trace, int round)
    {
        if (!attacker.Active) return new AttackStep(AttackStepOutcome.Continue, null, 0);
        if (IsCcLocked(status, attacker.Setup.Key, now)) return new AttackStep(AttackStepOutcome.Continue, null, 0);

        var target = SelectTarget(actors, attacker, status, now);
        if (target is null) return new AttackStep(AttackStepOutcome.Break, null, 0);
        trace?.Target(round, attacker.Setup.Key, target.Setup.Key);

        var (signedDelta, breakdown) = calculator.Compute(new OverlayCombatRequest
        {
            BaseOverlayDamage = attacker.Setup.Atk,
            Components = attacker.AttackComponents,
            Attacker = new CombatActorSnapshot(attacker.Derived, attacker.ElementTypes),
            Defender = new CombatActorSnapshot(target.Derived, target.ElementTypes),
            Profile = CombatProfile.BattleSim
        }, critRng);

        if (!breakdown.Hit) return new AttackStep(AttackStepOutcome.Continue, null, 0);

        return new AttackStep(AttackStepOutcome.Proceed, target, signedDelta);
    }
}
