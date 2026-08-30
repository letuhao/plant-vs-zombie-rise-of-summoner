using System.Globalization;
using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Stats.Derived.Subsystems;

/// <summary>
/// The Unity-free half of the lawn `stat.derived` executor: turns the grants an
/// <see cref="IEffectGrantStore"/> already holds for an actor into the <see cref="BoundDerivedAtom"/>
/// list <see cref="AtomDerivedSubsystem"/> composes.
///
/// <para><b>Why this lives in Core (aura-skill-todo.md Phase 5 / TC3).</b> It used to live entirely in
/// <c>FusionRpg.Injector.Stats.GrantedDerivedAtoms</c>, which cannot be reached by any test that CI
/// runs: the injector targets net6.0 and references the game's BepInEx/Il2Cpp interop assemblies, so
/// building it needs a real PVZ Fusion install — and `ci.yml`'s test step names ten projects, none of
/// them the injector. A guard nobody can run is not a guard. TC3's own rule was *"if the decision
/// cannot be reached without a Unity host, extract the decision into a Unity-free type, leaving only
/// the field pokes in the untestable shell"* — this is that extraction.</para>
///
/// <para>Everything here is Unity-free by construction: <see cref="IEffectGrantStore"/>,
/// <see cref="EffectGrant"/> and <see cref="EffectOwnerKeys"/> are all <c>FusionRpg.Contracts</c>
/// types, and <see cref="StatContext"/> is Core's. What stays in the injector is exactly the part that
/// is genuinely host-specific: reaching the live <c>EffectRuntime.Bag</c> static.</para>
///
/// <para><b>Scope grammar is the shipped one, not a new one:</b> `match`, `plant:{typeId}` /
/// `zombie:{typeId}`, and `entity:{ptr}`. `instance:{guid}` deliberately never appears —
/// <c>UniqueOwnerBinder</c> rewrites it to `entity:{ptr}` at Bound, and unique-entity-effects.md
/// forbids it reaching a hot resolve.</para>
/// </summary>
public static class GrantedDerivedAtomReader
{
    /// <summary>
    /// Overlay keys, deliberately NAMESPACED.
    ///
    /// <para>An earlier draft of this reader matched bare <c>channel</c>/<c>op</c>/<c>amount</c>, which
    /// are exactly the keys <c>InjectorEffectActionSink</c> already reads for <b>FA1 ModifyStat</b>
    /// (line 80) and <b>FA10 ApplyResourceDelta</b> (line 132). Every FA1 grant on the board would
    /// therefore have been consumed a second time as a derived mod — applied once as a primary stat
    /// modifier and again as a derived channel. Caught before shipping, by asking what else writes
    /// these keys rather than assuming nothing did.</para>
    ///
    /// <para>The namespace makes the collision impossible by construction rather than by convention:
    /// only a <c>stat.derived</c> compilation emits these, so an FA1/FA10 overlay can never be mistaken
    /// for one no matter how its own keys evolve. <c>GrantedDerivedAtomReaderTests</c> is the
    /// regression test that claim previously lacked — before TC3 it was asserted by a comment alone.</para>
    /// </summary>
    public const string ChannelKey = "derived.channel";

    /// <inheritdoc cref="ChannelKey"/>
    public const string OpKey = "derived.op";

    /// <inheritdoc cref="ChannelKey"/>
    public const string AmountKey = "derived.amount";

    /// <summary>
    /// Every bound derived atom that applies to this actor, from every owner scope it belongs to.
    /// Returns an empty array — never null — when nothing is granted, so
    /// <see cref="AtomDerivedSubsystem"/>'s own "contribute nothing rather than a zero-valued modifier"
    /// rule holds by construction.
    /// </summary>
    public static IReadOnlyList<BoundDerivedAtom> Read(IEffectGrantStore? grants, StatContext? ctx)
    {
        if (grants is null || ctx is null) return Array.Empty<BoundDerivedAtom>();

        List<BoundDerivedAtom>? found = null;

        Collect(grants, "match", EffectOwnerKeys.Match, ref found);

        var sideKind = ctx.Side == StatSide.Plant ? "plant" : "zombie";
        Collect(grants, sideKind, ctx.TypeId.ToString(CultureInfo.InvariantCulture), ref found);

        if (!string.IsNullOrWhiteSpace(ctx.EntityKey))
            Collect(grants, "entity", ctx.EntityKey!, ref found);

        return (IReadOnlyList<BoundDerivedAtom>?)found ?? Array.Empty<BoundDerivedAtom>();
    }

    static void Collect(IEffectGrantStore grants, string ownerKind, string ownerKey, ref List<BoundDerivedAtom>? into)
    {
        IReadOnlyList<EffectGrant> list;
        try { list = grants.ForOwner(ownerKind, ownerKey); }
        catch { return; }

        if (list is null) return;

        for (var i = 0; i < list.Count; i++)
        {
            var g = list[i];
            if (g?.Overlay is null || g.Overlay.Count == 0) continue;

            if (!TryString(g.Overlay, ChannelKey, out var channel)) continue;
            if (!TryString(g.Overlay, OpKey, out var op)) continue;
            // An op the derived side does not have (there is no `More` here) is content that would
            // otherwise be silently coerced into a wrong-but-plausible number. Skip it; the bind gate
            // is where such a row is meant to be refused, and coercing here would hide that it wasn't.
            if (!AtomDerivedSubsystem.TryParseOp(op, out var parsed)) continue;
            if (!TryDouble(g.Overlay, AmountKey, out var amount)) continue;

            (into ??= new List<BoundDerivedAtom>()).Add(
                new BoundDerivedAtom(channel, parsed, amount,
                    SourceId: string.IsNullOrWhiteSpace(g.EffectId) ? g.GrantId : g.EffectId));
        }
    }

    static bool TryString(IReadOnlyDictionary<string, object?> overlay, string key, out string value)
    {
        value = "";
        if (!overlay.TryGetValue(key, out var raw) || raw is null) return false;
        if (raw is string s) { value = s; return !string.IsNullOrWhiteSpace(s); }
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.String)
        {
            value = je.GetString() ?? "";
            return !string.IsNullOrWhiteSpace(value);
        }
        return false;
    }

    static bool TryDouble(IReadOnlyDictionary<string, object?> overlay, string key, out double value)
    {
        value = 0;
        if (!overlay.TryGetValue(key, out var raw) || raw is null) return false;
        switch (raw)
        {
            case double d: value = d; return true;
            case float f: value = f; return true;
            case long l: value = l; return true;
            case int i: value = i; return true;
            case JsonElement je when je.ValueKind == JsonValueKind.Number:
                return je.TryGetDouble(out value);
            default:
                return double.TryParse(
                    Convert.ToString(raw, CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
