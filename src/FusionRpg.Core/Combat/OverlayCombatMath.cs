using FusionRpg.Contracts;
using FusionRpg.Core.Combat.Element;

namespace FusionRpg.Core.Combat;

/// <summary>ICombatMath adapter — runs overlay pipeline when element payload is present.</summary>
public sealed class OverlayCombatMath : ICombatMath
{
    readonly CombatActorResolve _resolve;
    readonly OverlayCombatCalculator _calculator;
    readonly ICombatRng _rng;
    readonly Action<OverlayCombatBreakdown, DamagePacket, string>? _emitBreakdown;

    OverlayCombatMath(
        CombatActorResolve resolve,
        OverlayCombatCalculator calculator,
        ICombatRng rng,
        Action<OverlayCombatBreakdown, DamagePacket, string>? emitBreakdown)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _calculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _emitBreakdown = emitBreakdown;
    }

    public static OverlayCombatMath Create(
        CombatActorResolve resolve,
        IElementHub? elementHub = null,
        ICombatRng? rng = null,
        Action<OverlayCombatBreakdown, DamagePacket, string>? emitBreakdown = null) =>
        new(
            resolve,
            new OverlayCombatCalculator(elementHub),
            rng ?? new SeededCombatRng(0),
            emitBreakdown);

    public long Finalize(long signedAmount, string ptr, DamagePacket packet, BoardEntitySnap? entity)
    {
        if (signedAmount > 0)
            return signedAmount;

        if (packet.ElementPayload == null || packet.ElementPayload.Count == 0)
            return signedAmount;

        var components = OverlayCombatCalculator.ParseComponents(packet.ElementPayload);
        if (components.Count == 0)
            return signedAmount;

        var baseDamage = Math.Abs(signedAmount);
        var attackerLess = string.IsNullOrWhiteSpace(packet.ActorPtr);
        var attacker = _resolve(packet.ActorPtr, attackerLess);
        var defender = _resolve(ptr, attackerLess: false);

        var request = new OverlayCombatRequest
        {
            BaseOverlayDamage = baseDamage,
            Components = components,
            Attacker = attacker,
            Defender = defender
        };

        var (delta, breakdown) = _calculator.Compute(request, _rng);
        _emitBreakdown?.Invoke(breakdown, packet, ptr);
        return delta;
    }
}
