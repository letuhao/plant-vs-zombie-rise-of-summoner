using FusionRpg.Core.Demons.Patron;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// Applies the frozen match aura (spec-patron-demon.md) to PLANT-side derived reads as typed
/// combat-channel points — a pure compose-time overlay, never a Unity write. Unit mapping:
/// aura ‰ → absolute channel points at /10 (150‰ → +15 typed power), the resolver's working
/// scale. Active only while the PatronSecondaryPlugin's match grant lives, so a mid-match
/// switch changes nothing until the next match and board.end ends the aura with the grant.
/// </summary>
public static class PatronAuraOverlay
{
    public static ActorDerivedSnapshot Apply(ActorDerivedSnapshot derived, string? side)
    {
        var aura = PatronRuntimeState.MatchAura;
        if (aura == null || !string.Equals(side, "plant", StringComparison.OrdinalIgnoreCase))
            return derived;

        var pairs = new List<KeyValuePair<string, double>>(4);
        AddChannel(pairs, derived, "combat.power." + aura.ElementPrimary, aura.PowerMilli);
        AddChannel(pairs, derived, "combat.defense." + aura.ElementPrimary, aura.DefenseMilli);
        if (aura.ElementSecondary is { } secondary)
        {
            AddChannel(pairs, derived, "combat.power." + secondary, aura.SecondaryPowerMilli);
            AddChannel(pairs, derived, "combat.defense." + secondary, aura.SecondaryDefenseMilli);
        }

        return pairs.Count == 0 ? derived : derived.Overlay(pairs);
    }

    static void AddChannel(
        List<KeyValuePair<string, double>> pairs, ActorDerivedSnapshot derived, string channel, int milli)
    {
        if (milli <= 0) return;
        pairs.Add(new KeyValuePair<string, double>(channel, derived.Get(channel) + milli / 10.0));
    }
}
