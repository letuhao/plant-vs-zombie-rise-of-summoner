using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Battle;

/// <summary>
/// combat-unification Wave E3 — how an actor's elements become an attack payload.
///
/// <para>Extracted from <c>BattleEngine</c>'s private actor state so it can be tested as the pure
/// function it is, rather than only through a whole battle. Same reason
/// <c>KernelPurityScan</c> lives outside the tests that use it: a rule you cannot point directly at
/// is a rule you cannot prove fails when it should.</para>
///
/// <para><b>Inert at the shipped default.</b> <c>hybrid.secondaryWeightMilli</c> is 0, and at 0 this
/// returns exactly the single full-weight primary component the engine built before E3 existed — same
/// shape, same 1.0 — so every golden is unmoved. Raising it is a balance decision
/// <c>combat-unification-todo.md</c> marks <b>ask-first</b>, and it <b>moves the expedition
/// goldens</b>: wave demons carry a real <c>ElementSecondary</c> (<c>WaveCatalog.cs:115</c>) even
/// though the hand-built battle goldens do not.</para>
/// </summary>
public static class HybridPayload
{
    /// <summary>
    /// The payload for an attacker with <paramref name="primary"/> and an optional distinct
    /// <paramref name="secondary"/>.
    ///
    /// <para><paramref name="secondaryWeightMilli"/> is the secondary's share, per-mille; the primary
    /// carries the remainder, so the two always sum to exactly 1.0. A share is bounded 0..1000 by
    /// nature — above 1000 the primary would take a negative weight, which is a nonsense payload
    /// rather than an aggressive balance choice — and `BattleTuningLoader` refuses it at load. This
    /// function asserts the same bound rather than trusting its caller, because it is public.</para>
    /// </summary>
    public static ElementPayloadComponent[] Build(
        ElementTypeId? primary, ElementTypeId? secondary, int secondaryWeightMilli)
    {
        if (secondaryWeightMilli < 0 || secondaryWeightMilli > 1000)
            throw new ArgumentOutOfRangeException(nameof(secondaryWeightMilli), secondaryWeightMilli,
                "A payload share is per-mille of one payload and must be within 0..1000.");

        if (primary is not { } p) return Array.Empty<ElementPayloadComponent>();

        // Two ways to have no hybrid: the content did not give this actor a second element, or the
        // dial is off. Both collapse to the pre-E3 shape rather than to a 0-weight second component,
        // so a zero-weight component never reaches the resolver and cannot perturb its component loop.
        if (secondaryWeightMilli == 0 || secondary is not { } s)
            return new[] { new ElementPayloadComponent(p, 1.0) };

        var secondaryWeight = secondaryWeightMilli / 1000.0;
        return new[]
        {
            new ElementPayloadComponent(p, 1.0 - secondaryWeight),
            new ElementPayloadComponent(s, secondaryWeight),
        };
    }
}
