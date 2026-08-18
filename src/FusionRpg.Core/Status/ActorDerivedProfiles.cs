using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Status;

/// <summary>Named derived bags for status prove boards — apply/resist must not depend on ambient PvzStats.</summary>
public static class ActorDerivedProfiles
{
    public const string Neutral = "neutral";
    public const string Caster = "caster";
    public const string Glass = "glass";
    public const string IronCc = "iron-cc";
    public const string IronDot = "iron-dot";
    public const string IronContagion = "iron-contagion";
    public const string ImmunePoison = "immune-poison";
    public const string AttackerLess = "attacker-less";

    public const double IronOmniResist = 1_000_000;
    public const double CasterPower = 100;

    public static ActorDerivedSnapshot Get(string? profile)
    {
        var id = (profile ?? Neutral).Trim();
        if (string.Equals(id, AttackerLess, StringComparison.OrdinalIgnoreCase))
            return ActorDerivedSnapshot.AttackerLess();
        if (string.Equals(id, Caster, StringComparison.OrdinalIgnoreCase))
            return CasterSnapshot();
        if (string.Equals(id, Glass, StringComparison.OrdinalIgnoreCase))
            return ActorDerivedSnapshot.StubNeutral();
        if (string.Equals(id, IronCc, StringComparison.OrdinalIgnoreCase))
            return Iron(DerivedStatChannels.StatusResistCc);
        if (string.Equals(id, IronDot, StringComparison.OrdinalIgnoreCase))
            return Iron(DerivedStatChannels.StatusResistDot);
        if (string.Equals(id, IronContagion, StringComparison.OrdinalIgnoreCase))
            return Iron(DerivedStatChannels.StatusResistContagion);
        if (string.Equals(id, ImmunePoison, StringComparison.OrdinalIgnoreCase))
            return ActorDerivedSnapshot.StubNeutral().Overlay(new[]
            {
                new KeyValuePair<string, double>(DerivedStatChannels.StatusImmune("poison"), 1.0)
            });
        if (string.Equals(id, Neutral, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(id))
            return ActorDerivedSnapshot.StubNeutral();
        throw new ArgumentException("unknown derivedProfile: " + id);
    }

    public static ActorDerivedSnapshot Resolve(
        string? profile,
        IReadOnlyDictionary<string, double>? overlay = null)
    {
        var snap = Get(profile);
        if (overlay == null || overlay.Count == 0)
            return snap;
        return snap.Overlay(overlay);
    }

    static ActorDerivedSnapshot CasterSnapshot() =>
        ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerOmni, CasterPower),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerDot, CasterPower),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerCc, CasterPower),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerContagion, CasterPower)
        });

    /// <summary>
    /// Category resist is capped at <see cref="StatusPolicy.CategoryResistCap"/> (0.95).
    /// Omni is uncapped and carries the potency floor.
    /// </summary>
    static ActorDerivedSnapshot Iron(string categoryChannel) =>
        ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusResistOmni, IronOmniResist),
            new KeyValuePair<string, double>(categoryChannel, IronOmniResist)
        });
}

/// <summary>Ptr-keyed derived lookup for offline harness / scenario runner.</summary>
public sealed class ActorDerivedLookup
{
    readonly Dictionary<string, ActorDerivedSnapshot> _byPtr = new(StringComparer.Ordinal);

    public void Pin(string? ptr, ActorDerivedSnapshot snapshot)
    {
        var key = CombatPtr.Normalize(ptr);
        if (string.IsNullOrEmpty(key) || snapshot == null) return;
        _byPtr[key] = snapshot;
    }

    public void Clear() => _byPtr.Clear();

    public ActorDerivedSnapshot Resolve(string? ptr, bool attackerLess)
    {
        if (attackerLess)
            return ActorDerivedSnapshot.AttackerLess();
        var key = CombatPtr.Normalize(ptr);
        if (!string.IsNullOrEmpty(key) && _byPtr.TryGetValue(key, out var snap))
            return snap;
        return ActorDerivedSnapshot.StubNeutral();
    }
}
