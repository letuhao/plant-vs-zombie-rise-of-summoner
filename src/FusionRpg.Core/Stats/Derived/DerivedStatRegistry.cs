namespace FusionRpg.Core.Stats.Derived;

public enum DerivedComposeKind
{
    FlatSum,
    FlatReplace,
    SumIncreased,
    MaxPriorityFlag
}

/// <summary>
/// <c>Class</c> and <c>Unit</c> are two orthogonal axes (spec-stat-taxonomy.md §2.5): <c>Class</c> answers
/// "does it need a counterpart" (null only for the two non-combat progression channels the counterbalance
/// rule does not apply to — actor-hub-ssot.md §H.0's "Non-combat" row); <c>Unit</c> answers "what
/// arithmetic is it and how does it render" and is null only when no reader exists yet to name (§2.7) —
/// never a guessed placeholder. <c>double</c> composition fields are unchanged by design — §10.7 of
/// ssot-power-scale.md decided the <c>long</c> rule binds what composition produces, not the arithmetic
/// that composes it.
/// </summary>
public sealed record DerivedStatDef(
    string ChannelId,
    DerivedComposeKind Compose,
    double DefaultValue,
    double? Cap = null,
    StatClass? Class = null,
    UnitClass? Unit = null,
    string? CounterpartOf = null);

/// <summary>Catalog SSOT for derived channels — unknown id → reject.</summary>
public sealed class DerivedStatRegistry
{
    readonly Dictionary<string, DerivedStatDef> _defs = new(StringComparer.Ordinal);

    /// <summary>Captured once at construction, not re-read live by <see cref="TryResolveChannel"/> —
    /// so a registry instance stays internally consistent between its statically-registered channels
    /// (whose <see cref="DerivedStatDef.Cap"/> is frozen into the def at registration) and the sparse
    /// per-status-id ones resolved on demand. Both must agree on which cap value they saw.</summary>
    readonly double _categoryResistCap;

    DerivedStatRegistry() => _categoryResistCap = DerivedStatPolicy.CategoryResistCap;

    public static DerivedStatRegistry CreateDefault()
    {
        var r = new DerivedStatRegistry();
        r.RegisterDefaults();
        return r;
    }

    void RegisterDefaults()
    {
        // Pool: absorbed at the AppliedCombat merge into hp/maxHp/atk/defense/arm* — the actor's own
        // progression bonus, nothing to contest (actor-hub-ssot.md §H.0).
        Register(new(DerivedStatChannels.ProgressionBonusMaxHp, DerivedComposeKind.FlatSum, 0,
                     Class: StatClass.Pool, Unit: UnitClass.GameUnits));
        Register(new(DerivedStatChannels.ProgressionBonusAtk, DerivedComposeKind.FlatSum, 0,
                     Class: StatClass.Pool, Unit: UnitClass.GameUnits));
        Register(new(DerivedStatChannels.ProgressionBonusDefense, DerivedComposeKind.FlatSum, 0,
                     Class: StatClass.Pool, Unit: UnitClass.GameUnits));
        Register(new(DerivedStatChannels.ProgressionBonusArm1, DerivedComposeKind.FlatSum, 0,
                     Class: StatClass.Pool, Unit: UnitClass.GameUnits));
        Register(new(DerivedStatChannels.ProgressionBonusArm2, DerivedComposeKind.FlatSum, 0,
                     Class: StatClass.Pool, Unit: UnitClass.GameUnits));

        // Class: null — the counterbalance rule does not apply to Θ itself ("Non-combat" row, H.0).
        // Unit: LadderIndex — an index on the power ladder, not a magnitude (spec-magnitude-and-units §3.2).
        Register(new(DerivedStatChannels.ProgressionPower, DerivedComposeKind.FlatReplace, 1.0,
                     Class: null, Unit: UnitClass.LadderIndex));
        Register(new(DerivedStatChannels.ProgressionRealm, DerivedComposeKind.FlatReplace, 1.0,
                     Class: null, Unit: UnitClass.LadderIndex));

        // Contest: attacker half, paired with status.resist.{category}. Unit: StatusPotencyPoints —
        // raw magnitude, context part suppressed (spec-magnitude-and-units.md §4.3).
        Register(new(DerivedStatChannels.StatusPowerOmni, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusResistOmni));
        Register(new(DerivedStatChannels.StatusPowerDot, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusResistDot));
        Register(new(DerivedStatChannels.StatusPowerCc, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusResistCc));
        Register(new(DerivedStatChannels.StatusPowerContagion, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusResistContagion));

        // Contest: defender half. omni is the balance knob and stays uncapped; dot/cc/contagion cap at
        // the tuned DerivedStatPolicy.CategoryResistCap — ONE home (cap-consolidation, T1; was clamped
        // a second time in ResistanceEvaluator, which made raising the tunable past 0.95 a silent
        // no-op — see data/tuning/derived-stats.v1.json's _meta). The cap is StatusPotencyPoints-shaped,
        // not GameUnits — ContestHalvesAreUncapped exempts it by design (spec-stat-taxonomy.md §6.1:
        // the "uncapped" rule binds GameUnits/GameUnitsPerSecond magnitudes).
        Register(new(DerivedStatChannels.StatusResistOmni, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusPowerOmni));
        Register(new(DerivedStatChannels.StatusResistDot, DerivedComposeKind.SumIncreased, 0, _categoryResistCap,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusPowerDot));
        Register(new(DerivedStatChannels.StatusResistCc, DerivedComposeKind.SumIncreased, 0, _categoryResistCap,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusPowerCc));
        Register(new(DerivedStatChannels.StatusResistContagion, DerivedComposeKind.SumIncreased, 0, _categoryResistCap,
                     Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: DerivedStatChannels.StatusPowerContagion));

        RegisterNonElementExtensions();
        RegisterCombatDefaults();
    }

    /// <summary>H.2-H.7 (actor-hub-ssot.md, derived-stats program, 2026-08-24) — registration only, no
    /// reader wired. Every new channel defaults to 0 and carries no Cap: assigning one now would be
    /// shipping a balance value ahead of the module that owns its formula (spec-catalog-extension.md
    /// §7 "Never"). Unit stays null throughout — none of the 157 has a nameable consumer yet (§2.3).</summary>
    void RegisterNonElementExtensions()
    {
        // H.2 -- status potency, attacker-side (duration/intensity) mirrors status.power exactly.
        Register(new(DerivedStatChannels.StatusDurationOmni, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationReductionOmni));
        Register(new(DerivedStatChannels.StatusDurationDot, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationReductionDot));
        Register(new(DerivedStatChannels.StatusDurationCc, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationReductionCc));
        Register(new(DerivedStatChannels.StatusDurationContagion, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationReductionContagion));

        Register(new(DerivedStatChannels.StatusDurationReductionOmni, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationOmni));
        Register(new(DerivedStatChannels.StatusDurationReductionDot, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationDot));
        Register(new(DerivedStatChannels.StatusDurationReductionCc, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationCc));
        Register(new(DerivedStatChannels.StatusDurationReductionContagion, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusDurationContagion));

        Register(new(DerivedStatChannels.StatusIntensityOmni, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityReductionOmni));
        Register(new(DerivedStatChannels.StatusIntensityDot, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityReductionDot));
        Register(new(DerivedStatChannels.StatusIntensityCc, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityReductionCc));
        Register(new(DerivedStatChannels.StatusIntensityContagion, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityReductionContagion));

        Register(new(DerivedStatChannels.StatusIntensityReductionOmni, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityOmni));
        Register(new(DerivedStatChannels.StatusIntensityReductionDot, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityDot));
        Register(new(DerivedStatChannels.StatusIntensityReductionCc, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityCc));
        Register(new(DerivedStatChannels.StatusIntensityReductionContagion, DerivedComposeKind.SumIncreased, 0,
                     Class: StatClass.Contest, CounterpartOf: DerivedStatChannels.StatusIntensityContagion));

        // H.3 -- action-category. cooldown is Race (Q3, unpaired by nature); effectiveness is Feeder
        // (Q3, inherits its pair from combat.defense downstream — no CounterpartOf at this layer).
        foreach (var category in DerivedStatChannels.ActionCategories)
        {
            Register(new(DerivedStatChannels.SkillCooldown(category), DerivedComposeKind.FlatSum, 0,
                         Class: StatClass.Race));
            Register(new(DerivedStatChannels.SkillEffectiveness(category), DerivedComposeKind.FlatSum, 0,
                         Class: StatClass.Feeder));
        }

        // H.4 -- healing. Pool, unpaired (owner decision 2026-08-24 — dissolves §4.3 rather than
        // reopening it: no defender-side term means no delta on the heal path at all).
        Register(new(DerivedStatChannels.CombatHealPower, DerivedComposeKind.FlatSum, 0, Class: StatClass.Pool));

        // H.5 -- resource. Pool throughout (Q4): the counters are statuses, not stats. max/regen are
        // magnitudes and stay FlatSum/uncapped (spec-actor-channels.md §2.2). efficiency is a bounded
        // 0..1 ratio (T4.4) -- SumIncreased, not FlatSum: ComposeChannel's FlatSum case never calls
        // Cap(...) at all (only SumIncreased does), so a Cap on a FlatSum channel would be a silent
        // no-op. SumIncreased also reads Increased-op modifiers, the correct shape for a percentage-
        // like stacking ratio (matching status.power/status.resist's own SumIncreased+Cap precedent,
        // the only other capped channel family in this registry).
        foreach (var resourceId in DerivedStatChannels.ResourceIds)
        {
            Register(new(DerivedStatChannels.ResourceMax(resourceId), DerivedComposeKind.FlatSum, 0, Class: StatClass.Pool));
            Register(new(DerivedStatChannels.ResourceRegen(resourceId), DerivedComposeKind.FlatSum, 0, Class: StatClass.Pool));
            Register(new(DerivedStatChannels.ResourceEfficiency(resourceId), DerivedComposeKind.SumIncreased, 0,
                         DerivedStatPolicy.ResourceEfficiencyCap, Class: StatClass.Pool));
        }

        // H.6 -- movement. Pool (Q4, same reasoning as resource).
        Register(new(DerivedStatChannels.MoveRange, DerivedComposeKind.FlatSum, 0, Class: StatClass.Pool));

        // H.7 -- progression. xpRate: Class null, matching progression.power/progression.realm — a
        // rate/magnitude the counterbalance rule does not apply to ("Non-combat" row, H.0); FlatSum,
        // uncapped. breakthroughSuccess (T4.4): unlike power/realm's LadderIndex shape, this is the
        // actor's OWN roll probability, no pair -- Pool fits it the same way it fits resource.efficiency,
        // and EveryCapIsClassified (spec-stat-taxonomy.md §6.1, StatTaxonomyTests.cs) requires any
        // capped channel to carry a StatClass, not stay ambiguous. SumIncreased + Cap, same reasoning
        // as resource.efficiency above.
        Register(new(DerivedStatChannels.ProgressionXpRate, DerivedComposeKind.FlatSum, 0, Class: null));
        Register(new(DerivedStatChannels.ProgressionBreakthroughSuccess, DerivedComposeKind.SumIncreased, 0,
                     DerivedStatPolicy.BreakthroughSuccessCap, Class: StatClass.Pool));
    }

    void RegisterCombatDefaults()
    {
        foreach (var entry in DerivedStatChannels.AllCombatChannelEntries)
        {
            // StatClass is required for every family -- it is a known, decided property (H.0/H.1).
            // UnitClass is OPTIONAL: only families with a nameable reader today are present in
            // CombatFamilyUnitClass (the original 12) -- the 16 new H.1 families have none yet, so
            // this must stay null rather than throw (§2.3, §2.7: no placeholder for a channel with no
            // consumer at registration time).
            var (cls, counterpartFamily) = DerivedStatChannels.CombatFamilyClassification[entry.Family];
            UnitClass? unit = DerivedStatChannels.CombatFamilyUnitClass.TryGetValue(entry.Family, out var u) ? u : null;
            string? counterpart = counterpartFamily is null ? null : $"{counterpartFamily}.{entry.Slot}";
            Register(new(entry.ChannelId, DerivedComposeKind.FlatSum, 0,
                         Class: cls, Unit: unit, CounterpartOf: counterpart));
        }
    }

    public void Register(DerivedStatDef def)
    {
        if (string.IsNullOrWhiteSpace(def.ChannelId))
            throw new ArgumentException("ChannelId required");
        _defs[def.ChannelId] = def;
    }

    public bool IsKnown(string channelId) => TryGet(channelId, out _);

    public bool TryGet(string channelId, out DerivedStatDef def) =>
        _defs.TryGetValue(channelId, out def!);

    public DerivedStatDef GetRequired(string channelId)
    {
        if (!TryGet(channelId, out var def))
            throw new UnknownDerivedChannelException(channelId);
        return def;
    }

    public bool TryResolveChannel(string channelId, out DerivedStatDef def)
    {
        if (TryGet(channelId, out def!))
            return true;

        if (channelId.StartsWith("status.power.", StringComparison.Ordinal))
        {
            var counterpart = "status.resist." + channelId["status.power.".Length..];
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0,
                Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: counterpart);
            return true;
        }

        if (channelId.StartsWith("status.resist.", StringComparison.Ordinal))
        {
            var counterpart = "status.power." + channelId["status.resist.".Length..];
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0, _categoryResistCap,
                Class: StatClass.Contest, Unit: UnitClass.StatusPotencyPoints, CounterpartOf: counterpart);
            return true;
        }

        if (channelId.StartsWith("status.durationReduction.", StringComparison.Ordinal))
        {
            var counterpart = "status.duration." + channelId["status.durationReduction.".Length..];
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0,
                Class: StatClass.Contest, CounterpartOf: counterpart);
            return true;
        }

        if (channelId.StartsWith("status.duration.", StringComparison.Ordinal))
        {
            var counterpart = "status.durationReduction." + channelId["status.duration.".Length..];
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0,
                Class: StatClass.Contest, CounterpartOf: counterpart);
            return true;
        }

        if (channelId.StartsWith("status.intensityReduction.", StringComparison.Ordinal))
        {
            var counterpart = "status.intensity." + channelId["status.intensityReduction.".Length..];
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0,
                Class: StatClass.Contest, CounterpartOf: counterpart);
            return true;
        }

        if (channelId.StartsWith("status.intensity.", StringComparison.Ordinal))
        {
            var counterpart = "status.intensityReduction." + channelId["status.intensity.".Length..];
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0,
                Class: StatClass.Contest, CounterpartOf: counterpart);
            return true;
        }

        if (channelId.StartsWith("status.immune.", StringComparison.Ordinal)
            && !channelId.StartsWith("status.immuneReduction.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.MaxPriorityFlag, 0, 1,
                Class: StatClass.Pool, Unit: UnitClass.Flag);
            return true;
        }

        if (channelId.StartsWith("status.immuneReduction.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.MaxPriorityFlag, 0, 1,
                Class: StatClass.Pool, Unit: UnitClass.Flag);
            return true;
        }

        if (channelId.StartsWith("status.expose.", StringComparison.Ordinal))
        {
            // No StatClass/Unit: registered vocabulary with zero readers today (ssot-affixes.md flags
            // authoring it as RuntimeUnsupported) — §2.7 forbids inventing a placeholder classification
            // for a channel with no nameable consumer.
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0);
            return true;
        }

        def = null!;
        return false;
    }

    public void ValidateChannel(string channelId)
    {
        if (!TryResolveChannel(channelId, out _))
            throw new UnknownDerivedChannelException(channelId);
    }

    public IReadOnlyCollection<DerivedStatDef> AllRegistered => _defs.Values.ToList();
}

public sealed class UnknownDerivedChannelException : Exception
{
    public string ChannelId { get; }

    public UnknownDerivedChannelException(string channelId)
        : base($"Unknown derived channel: {channelId}")
    {
        ChannelId = channelId;
    }
}
