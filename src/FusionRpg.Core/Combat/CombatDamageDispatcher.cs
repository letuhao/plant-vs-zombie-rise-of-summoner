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
        EffectEventDto ev,
        EffectFunnel? funnel,
        CombatPolicy? policy = null,
        ICombatRng? rng = null,
        ICombatMath? math = null,
        List<string>? skipped = null,
        Shield.ShieldGate? shieldGate = null)
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
        }

        return n;
    }
}
