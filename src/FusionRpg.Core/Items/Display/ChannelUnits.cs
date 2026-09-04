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

    /// <summary>Matches by prefix, the way derived readers already match generated element channels
    /// (`DerivedStatChannels.cs`'s own `…Prefix` constants) — a new element needs no new unit row.</summary>
    public static UnitClass? For(string channelId, DerivedStatRegistry? derivedRegistry = null)
    {
        if (Primary.TryGetValue(channelId, out var primary)) return primary;

        var registry = derivedRegistry ?? DerivedStatRegistry.CreateDefault();
        return registry.TryGet(channelId, out var def) ? def.Unit : null;
    }
}
