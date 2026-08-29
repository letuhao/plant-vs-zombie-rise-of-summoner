using FusionRpg.Contracts;
using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Combat;

/// <summary>
/// Plan DamagePacket → resolve ptrs → Funnel mutations. Never a multi-ptr FA10.
/// </summary>
public static class CombatDamageDispatcher
{
    public static int DispatchInstant(
        DamagePacket packet,
        BoardSnapshot snapshot,
        EffectEventDto? ev,
        EffectFunnel? funnel,
        CombatPolicy? policy = null,
        ICombatRng? rng = null,
        ICombatMath? math = null,
        List<string>? skipped = null,
        Shield.ShieldGate? shieldGate = null,
        CombatActorResolve? actorResolve = null)
    {
        using var _perf = FusionRpg.Core.Diagnostics.PerfProbe.Measure(FusionRpg.Core.Diagnostics.PerfSection.CombatDispatch);
        policy ??= CombatPolicy.Default;
        math ??= PassThroughCombatMath.Instance;
        snapshot ??= BoardSnapshot.Empty;
        var limit = policy.ResolveProcDepthLimit(packet.ProcDepthLimit);
        if (packet.ChainDepth >= limit)
        {
            skipped?.Add((packet.SourceGrantId ?? "") + ":proc-depth");
            return 0;
        }

        var ptrs = TargetResolver.Resolve(packet.Target, snapshot, ev, policy, rng);
        var n = 0;
        foreach (var raw in ptrs)
        {
            var ptr = CombatPtr.Normalize(raw);
            if (string.IsNullOrWhiteSpace(ptr)) continue;
            var amount = math.Finalize(packet.SignedAmount, ptr, packet, snapshot.FindPtr(ptr));
            if (funnel == null) continue;
            // Apply tail is the shared DamageApplyPipeline (combat-unification): shield gate
            // → funnel, byte-identical to the pre-extraction inline tail.
            var applied = DamageApplyPipeline.ApplyPacketToFunnel(
                packet, ptr, amount, ev?.HitCount ?? 1, shieldGate, funnel, ev);
            switch (applied.Outcome)
            {
                case DamageApplyOutcome.Applied:
                    n++;
                    break;
                case DamageApplyOutcome.FullyAbsorbed:
                    skipped?.Add((packet.SourceGrantId ?? ptr) + ":absorbed");
                    break;
                default:
                    skipped?.Add((packet.SourceGrantId ?? ptr) + ":enqueue");
                    break;
            }

            // T5.4 (spec-reflection.md SS3): reads finalDamage pre-shield -- `amount`, computed
            // above, never `applied` -- and reuses ProcDepthLimit as the ONLY bound (SS2, no
            // second counter). A terminal bounce is left to the SAME top-of-function check on
            // the recursive call inside TryReflect, so it is dropped, never applied at a
            // clamped zero (SS2.1 rule 2).
            //
            // CombatPolicy.ReflectReadsPostShield flips that one reading: `applied.AppliedAmount`
            // is what actually reached HP, so a fully absorbed hit reflects nothing. Default false
            // keeps the shipped behaviour exactly -- the value is already on hand either way, so
            // this costs no extra work, only a choice.
            var reflectSource = policy.ReflectReadsPostShield ? applied.AppliedAmount : amount;
            if (actorResolve != null && rng != null && reflectSource < 0 && !string.IsNullOrWhiteSpace(packet.ActorPtr))
                TryReflect(packet, ptr, reflectSource, ev, funnel, policy, rng, math, skipped, shieldGate, actorResolve, snapshot);
        }

        return n;
    }

    /// <summary>
    /// One reflection attempt for a single ptr's finalized hit. Builds a new, reversed
    /// DamagePacket (reflector to attacker) and re-enters <see cref="DispatchInstant"/> at the
    /// top -- spec-reflection.md SS2.2: reflection is a later Funnel event, not a callback
    /// inside the mitigation math. No ElementPayload on the bounce: OverlayCombatMath.Finalize
    /// passes an ElementPayload-less packet through unchanged, so `bounced` (already final)
    /// is not re-mitigated.
    /// </summary>
    static void TryReflect(
        DamagePacket packet, string reflectorPtr, long amount, EffectEventDto? ev, EffectFunnel funnel,
        CombatPolicy policy, ICombatRng rng, ICombatMath math, List<string>? skipped,
        Shield.ShieldGate? shieldGate, CombatActorResolve actorResolve, BoardSnapshot snapshot)
    {
        var attackerPtr = CombatPtr.Normalize(packet.ActorPtr);
        if (string.IsNullOrWhiteSpace(attackerPtr) || attackerPtr == reflectorPtr) return;

        var reflector = actorResolve(reflectorPtr, attackerLess: false).Derived;
        var reflectedUpon = actorResolve(attackerPtr, attackerLess: false).Derived;

        // Linear from zero, not the spec's own sigmoid sketch -- CombatPolicy.ReflectRateScale's
        // doc comment: sigmoid(0)=0.5 would hand every actor a default reflect chance,
        // contradicting NoGoldensMoveAtZero. Same reasoning already applied to parry/block (T5.3).
        var rateDelta = CombatDerivedReader.ReflectRate(reflector) - CombatDerivedReader.ReflectResistRate(reflectedUpon);
        var pReflect = Math.Clamp(Math.Max(0.0, rateDelta) / policy.ReflectRateScale, 0.0, 1.0);
        if (!CombatProbability.RollSuccess(rng, pReflect)) return;

        var dmgDelta = CombatDerivedReader.ReflectDamage(reflector) - CombatDerivedReader.ReflectResistDamage(reflectedUpon);
        var reflectShare = Math.Clamp(Math.Max(0.0, dmgDelta) / policy.ReflectShareScale, 0.0, 1.0);
        var bounced = (long)Math.Round(Math.Abs(amount) * reflectShare, MidpointRounding.AwayFromZero);
        if (bounced <= 0) return;

        var bounce = new DamagePacket
        {
            PacketId = packet.PacketId + ":reflect",
            SourceGrantId = packet.SourceGrantId,
            EffectId = packet.EffectId,
            PluginId = packet.PluginId,
            ActorPtr = reflectorPtr,
            Target = new TargetSpec { Mode = TargetModes.Single, Ptr = attackerPtr },
            SignedAmount = -bounced,
            ChainDepth = packet.ChainDepth + 1,
            Channel = packet.Channel,
            Tick = packet.Tick,
            ProcDepthLimit = packet.ProcDepthLimit
        };
        DispatchInstant(bounce, snapshot, ev, funnel, policy, rng, math, skipped, shieldGate, actorResolve);
    }
}
