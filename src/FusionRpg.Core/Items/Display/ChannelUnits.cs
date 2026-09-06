using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Items.Display;

/// <summary>
/// N3 (spec-item-card.md): "a channel's unit is inseparable from its reader." <see cref="UnitClass"/>
/// itself already shipped (`Stats/Derived/StatClass.cs`, class-system's own eleven-member closed
/// ledger) — this is not a new enum, it is the item layer's lookup FACADE over it, extended with the
/// small set of PRIMARY channels `DerivedStatRegistry` does not cover (it is scoped to derived
/// channels only). A channel with a reader and no resolvable unit is <c>null</c> here, and the
/// caller's own validation turns that into `MissingUnitClass` — this class never guesses.
/// </summary>
public static class ChannelUnits
{
    /// <summary>The 8 primary `stat.modify` channels plus the two not-yet-promoted ones already
    /// declared on an atom family (`atom-family-library.md` §3.1) — `DerivedStatRegistry` is scoped to
    /// derived channels and does not carry these.</summary>
    static readonly IReadOnlyDictionary<string, UnitClass> Primary = new Dictionary<string, UnitClass>(StringComparer.Ordinal)
    {
        ["maxHp"] = UnitClass.GameUnits,
        ["hp"] = UnitClass.GameUnits,
        ["atk"] = UnitClass.GameUnits,
        ["defense"] = UnitClass.GameUnits,
        ["arm1"] = UnitClass.GameUnits,
        ["arm1Max"] = UnitClass.GameUnits,
        ["arm2"] = UnitClass.GameUnits,
        ["arm2Max"] = UnitClass.GameUnits,
        ["attackInterval"] = UnitClass.Milliseconds,
        ["produceInterval"] = UnitClass.Milliseconds,
        ["zombieSpeed"] = UnitClass.GameUnitsPerSecond,
    };

    /// <summary>
    /// Matches by prefix, the way derived readers already match generated element channels
    /// (`DerivedStatChannels.cs`'s own `…Prefix` constants) — a new element needs no new unit row.
    ///
    /// <para>⛔ <b>Reads <see cref="DerivedStatRegistry.TryResolveChannel"/>, not <c>TryGet</c>
    /// (fixed 2026-09-06, item module 10's Card pass).</b> The two are not the same question:
    /// <c>TryGet</c> asks "is this channel in the explicit table", <c>TryResolveChannel</c> asks
    /// "does this channel have a reader", which is exactly this class's own stated contract
    /// ("a channel with a reader and no resolvable unit is null here"). Against <c>TryGet</c> every
    /// GENERATED status family — <c>status.duration.*</c>, <c>status.intensity.*</c>,
    /// <c>status.immune.*</c>, and their reduction halves — reported <c>null</c> and would have
    /// become a <c>MissingUnitClass</c> rejection for a channel the engine reads perfectly well.
    /// An unknown channel still resolves to <c>null</c>: <c>TryResolveChannel</c>'s arms are all
    /// prefix-gated and its last statement is <c>return false</c>, so nothing is ever guessed.</para>
    /// </summary>
    public static UnitClass? For(string channelId, DerivedStatRegistry? derivedRegistry = null)
    {
        if (string.IsNullOrEmpty(channelId)) return null;
        if (Primary.TryGetValue(channelId, out var primary)) return primary;

        var registry = derivedRegistry ?? DerivedStatRegistry.CreateDefault();
        return registry.TryResolveChannel(channelId, out var def) ? def.Unit : null;
    }

    /// <summary>The concrete element a <c>{variant}</c>-templated channel is probed with. Structural,
    /// not tunable: it only has to be a real member of the element roster so the generated half of the
    /// channel family exists — which element it is cannot change the answer, because every element
    /// half of one family shares the family's unit by construction.</summary>
    const string ProbeElement = "fire";

    /// <summary>The concrete status a bare <c>status.*</c> family STEM is probed with. Same rule.</summary>
    const string ProbeStatus = "omni";

    /// <summary>
    /// The unit of a channel <b>as an affix family authors it</b>, which is not always a runtime
    /// channel id. The corpus writes two shapes <see cref="For"/> cannot resolve directly and should
    /// not be taught to, because neither is a channel anything ever reads:
    ///
    /// <list type="bullet">
    /// <item>an element TEMPLATE — <c>combat.power.{variant}</c> — which materialises one channel per
    /// element at expansion time (<c>FamilyExpansion</c>, W7.9: the variant deliberately does not
    /// materialise in the atom id);</item>
    /// <item>a bare family STEM — <c>status.resist</c>, <c>status.immune</c> — whose concrete status
    /// segment is supplied per variant.</item>
    /// </list>
    ///
    /// <para>Both are resolved by probing one concrete member, because a channel family's unit is a
    /// property of the family and not of the member. Returns <c>null</c> — never a guess — for
    /// anything that resolves to no reader even after probing; that null is what the validator turns
    /// into <see cref="DisplayRules.MissingUnitClass"/>.</para>
    /// </summary>
    public static UnitClass? ForAuthoredChannel(string channelId, DerivedStatRegistry? derivedRegistry = null)
    {
        if (string.IsNullOrEmpty(channelId)) return null;

        var registry = derivedRegistry ?? DerivedStatRegistry.CreateDefault();

        var direct = For(channelId, registry);
        if (direct is not null) return direct;

        if (channelId.Contains("{variant}", StringComparison.Ordinal))
            return For(channelId.Replace("{variant}", ProbeElement, StringComparison.Ordinal), registry);

        // A bare `status.*` stem: append one concrete member so the registry's own prefix arm fires.
        if (channelId.StartsWith("status.", StringComparison.Ordinal) && !channelId.EndsWith('.'))
            return For(channelId + "." + ProbeStatus, registry);

        return null;
    }
}
