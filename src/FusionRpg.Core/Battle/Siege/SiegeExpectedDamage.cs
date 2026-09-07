using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md, `IsKillingBlow`, resolved 2026-09-07): a real, reusable
/// expected-damage estimate for `AiCandidate.IsKillingBlow` — the SAME "read `OverlayCombatCalculator`,
/// reuse its already-shipped pieces, never modify it" discipline `SiegeHitChance.cs` already
/// established for `HitChanceMilli`.
///
/// <para><b>Omni-only, deliberately, for the identical reason `SiegeHitChance` is</b>: a targeting
/// decision does not yet know which specific action (and therefore which `ElementPayloadComponent`s)
/// will be used, so the typed-element/Divisive-mitigation branches `OverlayCombatCalculator.Compute`
/// also has are not reachable from here — replicating them without that context would risk a second,
/// silently-diverging copy of a formula this program has already named a recurring bug class
/// (`stat.derived`/`bullet.modify`/`wave.control`, each a new opcode/branch added in one place but not
/// the others). Reuses `OverlayCombatCalculator.PierceFactor` and `CombatPolicy.Default.PierceScale`
/// directly rather than re-deriving either.</para>
///
/// <para><b>Deterministic, not probabilistic</b> — this asks "if a hit lands squarely, does it end the
/// fight," never "what is the expected value across the hit/crit/miss distribution." `AiScoring`'s own
/// formula already carries `HitChanceMilli` as its own separate, dominant term (§3: 70 vs `IsKillingBlow`'s
/// own 15); folding hit-chance into this boolean too would double-count it.</para>
/// </summary>
public static class SiegeExpectedDamage
{
    public static bool IsKillingBlow(ActorDerivedSnapshot attacker, ActorDerivedSnapshot defender, long targetCurrentHp)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(defender);

        // Identical to OverlayCombatCalculator.Compute's own Omni-fallback branch
        // (Combat/OverlayCombatCalculator.cs:108-112): penetration/absorption scale defense INSIDE the
        // delta, pierce-scaled, before the power/defense subtraction.
        var penDelta = attacker.Get(DerivedStatChannels.CombatPenetrationOmni)
                        - defender.Get(DerivedStatChannels.CombatAbsorptionOmni);
        var effectiveDefense = defender.Get(DerivedStatChannels.CombatDefenseOmni)
                                * OverlayCombatCalculator.PierceFactor(penDelta, CombatPolicy.Default.PierceScale);
        var expectedDamage = attacker.Get(DerivedStatChannels.CombatPowerOmni) - effectiveDefense;

        return expectedDamage >= targetCurrentHp;
    }
}
