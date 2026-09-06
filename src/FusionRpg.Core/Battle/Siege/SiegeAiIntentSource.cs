using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md), task 17.4: the first LIVE `IIntentSource` this program
/// has ever built — `AiScoring`'s own pure functions (R1/R2/R5/R6) get their first real caller.
/// Copies `StubIntentSource`'s own exact steps 1/3/4/5 (cannot-act check, first-usable-action loop,
/// move-if-out-of-reach, pass) VERBATIM — this module's only new work is step 2, "who": nearest-enemy
/// replaced by `AiScoring.ChooseTarget` over real, scored candidates.
///
/// <para><b>Stateless by default, optionally stateful (17.8).</b> Omitting the constructor's
/// `retarget` parameter reproduces the ORIGINAL 17.4 shape exactly — every `TryDeclare` call recomputes
/// the best target fresh from `IBattleView`, zero memory between ticks, the same shape `StubIntentSource`
/// itself has. Supplying a `RetargetLedger` enables §5.20 rule 3's own retarget-latency enforcement: a
/// still-valid held target inside `ai.retargetLatencyTicks` of its last retarget is returned WITHOUT
/// rescoring, exactly the genuinely NEW, stateful shape this class's own doc comment once named as
/// separate, deferred work — see `RetargetLedger`'s own doc comment below.</para>
///
/// <para><b>17.9 (§5.20 rule 5):</b> an actor currently garrisoning a `CombatantKind.Structure`
/// emplacement never falls through to step 4's movement fallback — see `TryDeclare`'s own inline
/// comment for the full reasoning and `EmplacementFireMode`'s own doc comment for the replacement
/// vocabulary this represents.</para>
///
/// <para><b>A real v1, with three sub-scores honestly left at their zero/default value — named here,
/// not hidden.</b> Of `AiCandidate`'s 7 scoring inputs, THREE are real today: `HitChanceMilli`
/// (`SiegeHitChance.EstimateMilli`, reusing `OverlayCombatCalculator`'s own Omni formula),
/// `TargetMissingHpMilli` (direct from `EntityFacts.HpMilli`), and `TargetCanCounter` (does the
/// target hold any action at all). `ObjectiveClassMilli` (needs `BoardPathfinder.TerrainOnlyOccupancy`
/// path-scoring against a real objective — R3), `IsKillingBlow` (needs an expected-damage estimate,
/// not just a hit-chance one), and `IncomingThreatMilli` (needs multi-enemy damage aggregation
/// discounted by `siege-cover`) are each their own real, separate body of work — left at 0/false
/// rather than guessed at, matching this program's own "an honest gap costs a sentence; a hidden one
/// costs an hour" discipline. `BaseTier`/`Aggression` stay flat (every candidate in one tier, zero
/// aggression) since no stealth/taunt content authors a non-default value yet (§5.20 rule 4's own
/// vocabulary existing does not obligate every actor to use it). None of this makes the scorer inert:
/// three independently-meaningful signals (accuracy, existing damage, counter-safety) still produce a
/// real, non-arbitrary preference order — see `AiScoring.Score`'s own additive formula.</para>
/// </summary>
public sealed class SiegeAiIntentSource : IIntentSource
{
    readonly IBattleView _view;
    readonly CooldownLedger _cooldowns;
    readonly IStanceCheck _stance;
    readonly IAffordabilityCheck _affordability;
    readonly AiTuning _tuning;
    readonly Func<long, int> _roundOf;
    readonly RetargetLedger? _retarget;

    /// <summary>
    /// `roundOf` converts the kernel's own `nowTick` into `AiScoring`'s own `currentRound` — a
    /// caller-supplied callback, never assumed, matching the SAME "domain knowledge the compiler-shaped
    /// class does not itself need to know" precedent `AtomCompiler`'s `grantOwnerKeys`/`externalRefs`
    /// and `siege-fog`'s own `visionRangeOf` callbacks already established, rather than guessing at a
    /// tick-to-round relationship this pass has not verified.
    ///
    /// <para><c>retarget</c> (task 17.8, §5.20 rule 3) is OPTIONAL and defaults to <c>null</c> —
    /// omitting it reproduces 17.4's own shipped stateless behavior byte-for-byte (every call rescores
    /// fresh). Supplying one is the genuinely NEW, stateful shape 17.4's own doc comment named as
    /// separate, deferred work: a caller that wants `ai.retargetLatencyTicks` (17.3) actually enforced
    /// constructs ONE `RetargetLedger` and reuses it across every `TryDeclare` call for the SAME
    /// battle, so the "last target, last retarget tick" memory persists tick to tick.</para>
    /// </summary>
    public SiegeAiIntentSource(
        IBattleView view, CooldownLedger cooldowns, IStanceCheck stance, IAffordabilityCheck affordability,
        AiTuning tuning, Func<long, int> roundOf, RetargetLedger? retarget = null)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _cooldowns = cooldowns ?? throw new ArgumentNullException(nameof(cooldowns));
        _stance = stance ?? throw new ArgumentNullException(nameof(stance));
        _affordability = affordability ?? throw new ArgumentNullException(nameof(affordability));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _roundOf = roundOf ?? throw new ArgumentNullException(nameof(roundOf));
        _retarget = retarget;
    }

    public ActionIntent TryDeclare(string actorKey, long nowTick)
    {
        var heldActions = _view.HeldActionsOf(actorKey);
        if (heldActions.Count == 0) return ActionIntent.None; // step 1: cannot act at all

        var targetKey = ChooseTarget(actorKey, nowTick); // step 2: who -- real scoring, not nearest
        if (targetKey is null) return ActionIntent.None; // no scoreable live enemy exists at all

        var casterPos = _view.PositionOf(actorKey);
        var targetPos = _view.PositionOf(targetKey);
        var selfFacts = _view.FactsOf(actorKey);
        var targetFacts = _view.FactsOf(targetKey);

        // step 3: with what -- first usable action, in the actor's own preference order, against the
        // ONE chosen target. Identical to StubIntentSource's own step 3, verbatim.
        for (var i = 0; i < heldActions.Count; i++)
        {
            var action = heldActions[i];
            var facts = new FactReader(selfFacts, targetFacts);
            var result = UsabilityEvaluator.Evaluate(
                actorKey, action.ActionId, action.Envelope, action.MinRange, action.MaxRange,
                actorHoldsAction: true, nowTick, _cooldowns, _stance, _affordability,
                casterPos, targetPos, action.Condition, ref facts);

            if (result.IsUsable)
                return new ActionIntent(action.ActionId, targetKey, action.Envelope);
        }

        // 17.9 (§5.20 rule 5): a garrisoned emplacement's occupant NEVER falls through to R3's own
        // movement fallback below -- "advance toward the objective" is meaningless for something that
        // cannot move, and abandoning the post to chase a target would defeat the whole point of
        // garrisoning it. Its replacement, two-entry vocabulary (Hold fire / Fire at will,
        // EmplacementFireMode) resolves to Fire-at-will for every actor today -- no content sets Hold
        // fire yet, so this v1 is honestly just "the movement fallback never fires for a garrisoned
        // occupant," matching 17.7's own precedent of a named vocabulary shipping before any content
        // gives its other value a real trigger.
        if (_view.GarrisonedStructureKeyOf(actorKey) is not null) return ActionIntent.None;

        // step 4: can't reach with anything -- move toward them if any held action is tagged
        // Movement. Identical to StubIntentSource's own step 4, verbatim.
        for (var i = 0; i < heldActions.Count; i++)
        {
            var action = heldActions[i];
            if (!Contains(action.Tags, ActionTag.Movement)) continue;

            var facts = new FactReader(selfFacts, targetFacts);
            var result = UsabilityEvaluator.Evaluate(
                actorKey, action.ActionId, action.Envelope, minRange: 0, maxRange: int.MaxValue,
                actorHoldsAction: true, nowTick, _cooldowns, _stance, _affordability,
                casterPos: null, targetPos: null, action.Condition, ref facts);

            if (result.IsUsable)
                return new ActionIntent(action.ActionId, targetKey, action.Envelope);
        }

        return ActionIntent.None; // step 5: pass -- a requirement, not a fallback
    }

    /// <summary>Step 2, real: builds one `AiCandidate` per live enemy actor this view's own
    /// `DerivedOf` can evaluate, scores them with `AiScoring.ChooseTarget` (R1/R2/R5), and returns the
    /// winner's key — or `null` when no enemy exists or none is scoreable (e.g., every enemy is
    /// currently hidden under fog, so `DerivedOf` returns null for all of them).
    ///
    /// <para>17.8: when a `RetargetLedger` was supplied, a still-valid held target inside
    /// `ai.retargetLatencyTicks` of its own last retarget is returned WITHOUT rescoring — "a unit that
    /// keeps swinging at a target which just moved," §5.20 rule 3's own words, honored literally: a
    /// strictly better candidate appearing mid-window does not trigger an early switch. A held target
    /// that becomes invalid (dead, or hidden under fog) is abandoned immediately rather than honored
    /// through the rest of its window, since there is nothing left to keep attacking.</para>
    /// </summary>
    string? ChooseTarget(string actorKey, long nowTick)
    {
        var mySide = _view.SideOf(actorKey);
        var selfDerived = _view.DerivedOf(actorKey);
        if (selfDerived is null) return null; // cannot estimate a hit chance for a self we cannot read

        var liveActorKeys = _view.LiveActorKeys;

        if (_retarget is not null && _retarget.TryGetHeld(actorKey, nowTick, _tuning.RetargetLatencyTicks,
                candidateKey => IsStillScoreable(candidateKey, mySide, liveActorKeys), out var held))
            return held;

        var candidates = new List<(string Key, AiCandidate Candidate)>(liveActorKeys.Count);

        for (var i = 0; i < liveActorKeys.Count; i++)
        {
            var candidateKey = liveActorKeys[i];
            if (string.Equals(candidateKey, actorKey, StringComparison.Ordinal)) continue;
            if (_view.SideOf(candidateKey) == mySide) continue;

            var targetDerived = _view.DerivedOf(candidateKey);
            if (targetDerived is null) continue; // e.g. hidden under fog -- cannot score, skip

            var facts = _view.FactsOf(candidateKey);
            var hitChanceMilli = SiegeHitChance.EstimateMilli(selfDerived, targetDerived);
            var missingHpMilli = Math.Clamp(1000 - facts.HpMilli, 0, 1000);
            var canCounter = _view.HeldActionsOf(candidateKey).Count > 0;

            candidates.Add((candidateKey, new AiCandidate(
                ActorKey: candidateKey, BaseTier: 0, Aggression: 0,
                HitChanceMilli: hitChanceMilli, ObjectiveClassMilli: 0, IsKillingBlow: false,
                TargetMissingHpMilli: missingHpMilli, TargetCanCounter: canCounter,
                IncomingThreatMilli: 0)));
        }

        if (candidates.Count == 0)
        {
            _retarget?.Forget(actorKey);
            return null;
        }

        var chosen = AiScoring.ChooseTarget(
            candidates.Select(c => c.Candidate).ToList(), _roundOf(nowTick), _tuning);
        if (chosen is not null) _retarget?.RecordRetarget(actorKey, chosen.Value.ActorKey, nowTick);
        return chosen?.ActorKey;
    }

    bool IsStillScoreable(string candidateKey, int mySide, IReadOnlyList<string> liveActorKeys)
    {
        var stillLive = false;
        for (var i = 0; i < liveActorKeys.Count; i++)
        {
            if (!string.Equals(liveActorKeys[i], candidateKey, StringComparison.Ordinal)) continue;
            stillLive = true;
            break;
        }
        if (!stillLive) return false; // dead/removed from the board
        if (_view.SideOf(candidateKey) == mySide) return false; // defensive: a side swap invalidates it too
        return _view.DerivedOf(candidateKey) is not null; // e.g. newly hidden under fog
    }

    static bool Contains(IReadOnlyList<ActionTag> tags, ActionTag tag)
    {
        for (var i = 0; i < tags.Count; i++)
            if (tags[i] == tag) return true;
        return false;
    }
}

/// <summary>
/// base-defense `siege-ai` task 17.8 (spec-siege-ai.md §5.20 rule 3): the per-actor "last target, last
/// retarget tick" memory `SiegeAiIntentSource`'s own 17.4 doc comment named as a genuinely NEW,
/// stateful shape — deliberately its own small class rather than fields folded into
/// `SiegeAiIntentSource` itself, so that class stays trivially stateless/reusable when a caller has no
/// use for retarget latency (the constructor's `retarget` parameter defaults to `null`).
///
/// <para>One instance is meant to be constructed ONCE per battle and reused across every
/// `TryDeclare` call on the SAME `SiegeAiIntentSource` for that battle — like `CooldownLedger`, this
/// is battle-scoped state, never a `static`/shared singleton.</para>
/// </summary>
public sealed class RetargetLedger
{
    readonly Dictionary<string, (string TargetKey, long RetargetedAtTick)> _lastByActor = new(StringComparer.Ordinal);

    /// <summary>
    /// True when `actorKey` already has a target that should be HELD rather than rescored this tick —
    /// the latency has not yet elapsed since the last retarget AND `stillValid` confirms the held
    /// target is still a real, live, scoreable enemy. `retargetLatencyTicks == 0` (the shipped
    /// default) always returns false: `nowTick - RetargetedAtTick` is never negative, so "elapsed &lt;
    /// 0 ticks" can never hold — immediate re-evaluation every call, byte-identical to 17.4's own
    /// stateless shape.
    /// </summary>
    public bool TryGetHeld(
        string actorKey, long nowTick, long retargetLatencyTicks, Func<string, bool> stillValid,
        out string targetKey)
    {
        if (_lastByActor.TryGetValue(actorKey, out var last) &&
            checked(nowTick - last.RetargetedAtTick) < retargetLatencyTicks &&
            stillValid(last.TargetKey))
        {
            targetKey = last.TargetKey;
            return true;
        }

        targetKey = "";
        return false;
    }

    /// <summary>Records a fresh retarget decision — called only when `ChooseTarget` actually rescored
    /// (never when `TryGetHeld` served a held target), so the latency window restarts from THIS tick.</summary>
    public void RecordRetarget(string actorKey, string targetKey, long nowTick) =>
        _lastByActor[actorKey] = (targetKey, nowTick);

    /// <summary>Clears an actor's own memory — e.g. it just rescored and found no live target at all.</summary>
    public void Forget(string actorKey) => _lastByActor.Remove(actorKey);
}
