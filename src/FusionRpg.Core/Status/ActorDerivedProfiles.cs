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

    public const string CombatNeutral = "combat-neutral";
    public const string CombatFireCaster = "combat-fire-caster";
    public const string CombatIceTank = "combat-ice-tank";
    public const string CombatGlass = "combat-glass";

    // Proof-board fixture values (class doc: "status prove boards"), not balance surface — a
    // balance pass never tunes what a *named test scenario* reports as its input, only what real
    // content specifies. tunables-ssot.md T1's test ("would a balance pass change this?") reads no
    // here, unlike genuine content/policy defaults.
    public const double IronOmniResist = 1_000_000;
    // Proof-board fixture, not balance surface (see above).
    public const double CasterPower = 100;
    // Proof-board fixture, not balance surface (see above).
    public const double CombatFirePower = 50;
    // Proof-board fixture, not balance surface (see above).
    public const double CombatIceDefense = 30;
    // Proof-board fixture, not balance surface (see above).
    public const double CombatHighAccuracy = 500;
    // Proof-board fixture, not balance surface (see above).
    public const double CombatLowAccuracy = -500;
    // Proof-board fixture, not balance surface (see above).
    public const double CombatHighDodge = 500;

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
        if (string.Equals(id, CombatNeutral, StringComparison.OrdinalIgnoreCase))
            return CombatNeutralSnapshot();
        if (string.Equals(id, CombatFireCaster, StringComparison.OrdinalIgnoreCase))
            return CombatFireCasterSnapshot();
        if (string.Equals(id, CombatIceTank, StringComparison.OrdinalIgnoreCase))
            return CombatIceTankSnapshot();
        if (string.Equals(id, CombatGlass, StringComparison.OrdinalIgnoreCase))
            return CombatGlassSnapshot();
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
    /// Category resist is capped at <see cref="Stats.Derived.DerivedStatPolicy.CategoryResistCap"/>
    /// (0.95) — applied once, at compose (cap-consolidation, T1). Omni is uncapped and carries the
    /// potency floor.
    /// </summary>
    static ActorDerivedSnapshot Iron(string categoryChannel) =>
        ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusResistOmni, IronOmniResist),
            new KeyValuePair<string, double>(categoryChannel, IronOmniResist)
        });

    static ActorDerivedSnapshot CombatNeutralSnapshot() =>
        ActorDerivedSnapshot.StubNeutral();

    static ActorDerivedSnapshot CombatFireCasterSnapshot() =>
        ActorDerivedSnapshot.StubNeutral().Overlay(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatPowerFire, CombatFirePower),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, CombatHighAccuracy),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatCritRateOmni, -CombatHighAccuracy)
        });

    static ActorDerivedSnapshot CombatIceTankSnapshot() =>
        ActorDerivedSnapshot.StubNeutral().Overlay(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseIce, CombatIceDefense),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDodgeOmni, CombatHighDodge)
        });

    static ActorDerivedSnapshot CombatGlassSnapshot() =>
        ActorDerivedSnapshot.StubNeutral().Overlay(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDefenseOmni, -50)
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
