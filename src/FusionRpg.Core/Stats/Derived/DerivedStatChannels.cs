using FusionRpg.Core.Combat.Element;
namespace FusionRpg.Core.Stats.Derived;

/// <summary>Known derived channel id patterns — see actor-hub-ssot.md §3.</summary>
public static class DerivedStatChannels
{
    public const string ProgressionBonusMaxHp = "progression.bonus.maxHp";
    public const string ProgressionBonusAtk = "progression.bonus.atk";
    public const string ProgressionBonusDefense = "progression.bonus.defense";
    public const string ProgressionBonusArm1 = "progression.bonus.arm1";
    public const string ProgressionBonusArm2 = "progression.bonus.arm2";

    public const string ProgressionPower = "progression.power";
    public const string ProgressionRealm = "progression.realm";

    public const string StatusPowerOmni = "status.power.omni";
    public const string StatusPowerDot = "status.power.dot";
    public const string StatusPowerCc = "status.power.cc";
    public const string StatusPowerContagion = "status.power.contagion";

    public const string StatusResistOmni = "status.resist.omni";
    public const string StatusResistDot = "status.resist.dot";
    public const string StatusResistCc = "status.resist.cc";
    public const string StatusResistContagion = "status.resist.contagion";

    public static string StatusPower(string statusId) => $"status.power.{statusId}";
    public static string StatusResist(string statusId) => $"status.resist.{statusId}";
    public static string StatusImmune(string tag) => $"status.immune.{tag}";
    public static string StatusImmuneReduction(string tag) => $"status.immuneReduction.{tag}";
    public static string StatusExpose(string category) => $"status.expose.{category}";

    public const string CombatPowerOmni = "combat.power.omni";
    public const string CombatPowerFire = "combat.power.fire";
    public const string CombatPowerIce = "combat.power.ice";
    public const string CombatPowerAir = "combat.power.air";
    public const string CombatPowerEarth = "combat.power.earth";
    public const string CombatPowerLight = "combat.power.light";
    public const string CombatPowerDark = "combat.power.dark";

    public const string CombatDefenseOmni = "combat.defense.omni";
    public const string CombatDefenseFire = "combat.defense.fire";
    public const string CombatDefenseIce = "combat.defense.ice";
    public const string CombatDefenseAir = "combat.defense.air";
    public const string CombatDefenseEarth = "combat.defense.earth";
    public const string CombatDefenseLight = "combat.defense.light";
    public const string CombatDefenseDark = "combat.defense.dark";

    public const string CombatCritRateOmni = "combat.crit.rate.omni";
    public const string CombatCritRateFire = "combat.crit.rate.fire";
    public const string CombatCritRateIce = "combat.crit.rate.ice";
    public const string CombatCritRateAir = "combat.crit.rate.air";
    public const string CombatCritRateEarth = "combat.crit.rate.earth";
    public const string CombatCritRateLight = "combat.crit.rate.light";
    public const string CombatCritRateDark = "combat.crit.rate.dark";

    public const string CombatCritResistOmni = "combat.crit.resist.omni";
    public const string CombatCritResistFire = "combat.crit.resist.fire";
    public const string CombatCritResistIce = "combat.crit.resist.ice";
    public const string CombatCritResistAir = "combat.crit.resist.air";
    public const string CombatCritResistEarth = "combat.crit.resist.earth";
    public const string CombatCritResistLight = "combat.crit.resist.light";
    public const string CombatCritResistDark = "combat.crit.resist.dark";

    public const string CombatCritDamageOmni = "combat.crit.damage.omni";
    public const string CombatCritDamageFire = "combat.crit.damage.fire";
    public const string CombatCritDamageIce = "combat.crit.damage.ice";
    public const string CombatCritDamageAir = "combat.crit.damage.air";
    public const string CombatCritDamageEarth = "combat.crit.damage.earth";
    public const string CombatCritDamageLight = "combat.crit.damage.light";
    public const string CombatCritDamageDark = "combat.crit.damage.dark";

    public const string CombatCritResistDamageOmni = "combat.crit.resist.damage.omni";
    public const string CombatCritResistDamageFire = "combat.crit.resist.damage.fire";
    public const string CombatCritResistDamageIce = "combat.crit.resist.damage.ice";
    public const string CombatCritResistDamageAir = "combat.crit.resist.damage.air";
    public const string CombatCritResistDamageEarth = "combat.crit.resist.damage.earth";
    public const string CombatCritResistDamageLight = "combat.crit.resist.damage.light";
    public const string CombatCritResistDamageDark = "combat.crit.resist.damage.dark";

    public const string CombatAccuracyOmni = "combat.accuracy.omni";
    public const string CombatAccuracyFire = "combat.accuracy.fire";
    public const string CombatAccuracyIce = "combat.accuracy.ice";
    public const string CombatAccuracyAir = "combat.accuracy.air";
    public const string CombatAccuracyEarth = "combat.accuracy.earth";
    public const string CombatAccuracyLight = "combat.accuracy.light";
    public const string CombatAccuracyDark = "combat.accuracy.dark";

    public const string CombatDodgeOmni = "combat.dodge.omni";
    public const string CombatDodgeFire = "combat.dodge.fire";
    public const string CombatDodgeIce = "combat.dodge.ice";
    public const string CombatDodgeAir = "combat.dodge.air";
    public const string CombatDodgeEarth = "combat.dodge.earth";
    public const string CombatDodgeLight = "combat.dodge.light";
    public const string CombatDodgeDark = "combat.dodge.dark";

    // Shield families (shield-system-spec.md §2.3) — element halves are generated, never hand-listed.
    public const string CombatShieldCapacityPrefix = "combat.shield.capacity";
    public const string CombatShieldToughnessPrefix = "combat.shield.toughness";
    public const string CombatShieldPenPrefix = "combat.shield.pen";
    public const string CombatShieldRegenPrefix = "combat.shield.regen";

    public const string CombatShieldCapacityOmni = "combat.shield.capacity.omni";
    public const string CombatShieldToughnessOmni = "combat.shield.toughness.omni";
    public const string CombatShieldPenOmni = "combat.shield.pen.omni";
    public const string CombatShieldRegenOmni = "combat.shield.regen.omni";

    public static string CombatShieldCapacity(ElementTypeId e) => $"{CombatShieldCapacityPrefix}.{e.ToElementId()}";
    public static string CombatShieldToughness(ElementTypeId e) => $"{CombatShieldToughnessPrefix}.{e.ToElementId()}";
    public static string CombatShieldPen(ElementTypeId e) => $"{CombatShieldPenPrefix}.{e.ToElementId()}";
    public static string CombatShieldRegen(ElementTypeId e) => $"{CombatShieldRegenPrefix}.{e.ToElementId()}";

    /// <summary>Combat channel family prefixes — 12 families × (omni + roster) slots.</summary>
    public static readonly IReadOnlyList<string> CombatChannelFamilies = new[]
    {
        "combat.power",
        "combat.defense",
        "combat.crit.rate",
        "combat.crit.resist",
        "combat.crit.damage",
        "combat.crit.resist.damage",
        "combat.accuracy",
        "combat.dodge",
        CombatShieldCapacityPrefix,
        CombatShieldToughnessPrefix,
        CombatShieldPenPrefix,
        CombatShieldRegenPrefix
    };

    /// <summary>
    /// All overlay combat derived channels — generated family × (omni + roster) so the list is
    /// exhaustive by construction (element-hub-ssot.md §6; 12 × 7 = 84 today).
    ///
    /// <para><b>Generated from the roster TABLE, not the enum</b> (E18). That is what makes a seventh
    /// element rows plus regeneration: its 12 channels appear here, and every consumer picks them up
    /// because <c>CombatDerivedReader</c> matches channels by pattern rather than by name.</para>
    ///
    /// <para><b>Cached by reference to the source table</b> (E25, completeness-audit.md B3). The
    /// stated reason for never caching — "the roster is loaded after startup" — stopped being a
    /// reason once E20 shipped: <see cref="ElementTable.Use"/>/<see cref="ElementTable.UseScoped"/>
    /// always assign a <b>new</b> immutable instance rather than mutating one in place, so comparing
    /// <see cref="ElementTable.Current"/> by reference against what this cache was last built from is
    /// exactly as fresh as rebuilding every call, at 84 fewer string allocations per read.
    /// <see cref="BattleStatComposer.Compose"/> called this once per actor composed.</para>
    /// </summary>
    public static IReadOnlyList<string> AllCombatChannelIds
    {
        get { lock (CacheGate) { EnsureCacheUnlocked(); return _cacheList!; } }
    }

    /// <summary>
    /// O(1) membership over the same cached generation <see cref="AllCombatChannelIds"/> uses — the
    /// other half of E25: <c>StatusStatPayload.IsKnownChannel</c> used to <c>.Contains</c> a freshly
    /// allocated 84-element list on every channel it parsed.
    /// </summary>
    public static bool IsCombatChannel(string channel)
    {
        lock (CacheGate) { EnsureCacheUnlocked(); return _cacheSet!.Contains(channel); }
    }

    static void EnsureCacheUnlocked()
    {
        var current = ElementTable.Current;
        if (ReferenceEquals(_cacheSource, current)) return;

        _cacheSource = current;
        var built = BuildAllCombatChannelIds(current.Elements.Where(e => e.Enabled).Select(e => e.ElementId));
        _cacheList = built;
        _cacheSet = new HashSet<string>(built, StringComparer.Ordinal);
    }

    static readonly object CacheGate = new();
    static ElementTable? _cacheSource;
    static IReadOnlyList<string>? _cacheList;
    static HashSet<string>? _cacheSet;

    /// <summary>The channel set for an explicit roster — how a test can add a seventh element.</summary>
    public static IReadOnlyList<string> BuildAllCombatChannelIds(IEnumerable<string> elementIds)
    {
        var roster = elementIds.ToList();
        var ids = new List<string>(CombatChannelFamilies.Count * (roster.Count + 1));
        foreach (var family in CombatChannelFamilies)
        {
            ids.Add($"{family}.{ElementRoster.OmniId}");
            foreach (var element in roster)
                ids.Add($"{family}.{element}");
        }

        return ids;
    }
}

/// <summary>Actor element type metadata field names — see element-hub-ssot.md §5.</summary>
public static class ElementMetadataKeys
{
    public const string Primary = "element.type.primary";
    public const string Secondary = "element.type.secondary";
}
