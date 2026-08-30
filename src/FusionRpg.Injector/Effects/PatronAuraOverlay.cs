using FusionRpg.Core.Demons.Patron;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// Applies the frozen match aura (spec-patron-demon.md) to PLANT-side derived reads as typed
/// combat-channel points — a pure compose-time overlay, never a Unity write. Unit mapping:
/// aura ‰ → absolute channel points at /10 (150‰ → +15 typed power), the resolver's working
/// scale. Active only while the PatronSecondaryPlugin's match grant lives, so a mid-match
/// switch changes nothing until the next match and board.end ends the aura with the grant.
///
/// <para><b>Uses <see cref="ActorDerivedSnapshot.OverlayAdd"/>, not <c>Overlay</c></b> (audit D1,
/// aura-skill T1): this is a CONTRIBUTION to a channel another producer may also write, not a
/// replacement. Each contributed value is the aura's own delta only — <c>OverlayAdd</c> adds it to
/// whatever the base snapshot already holds, so this must never also read and re-add the base value
/// itself, or the contribution doubles. That double-add was exactly the manual compensation this
/// class used before <c>OverlayAdd</c> existed.</para>
/// </summary>
public static class PatronAuraOverlay
{
    public static ActorDerivedSnapshot Apply(ActorDerivedSnapshot derived, string? side)
    {
        var aura = PatronRuntimeState.MatchAura;
        if (aura == null || !string.Equals(side, "plant", StringComparison.OrdinalIgnoreCase))
            return derived;

        var pairs = new List<KeyValuePair<string, double>>(4);
        AddChannel(pairs, "combat.power." + aura.ElementPrimary, aura.PowerMilli);
        AddChannel(pairs, "combat.defense." + aura.ElementPrimary, aura.DefenseMilli);
        if (aura.ElementSecondary is { } secondary)
        {
            AddChannel(pairs, "combat.power." + secondary, aura.SecondaryPowerMilli);
            AddChannel(pairs, "combat.defense." + secondary, aura.SecondaryDefenseMilli);
        }

        return pairs.Count == 0 ? derived : derived.OverlayAdd(pairs);
    }

    static void AddChannel(List<KeyValuePair<string, double>> pairs, string channel, long milli)
    {
        if (milli <= 0) return;
        pairs.Add(new KeyValuePair<string, double>(channel, milli / 10.0));
    }
}
