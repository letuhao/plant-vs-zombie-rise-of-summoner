namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// The closed vocabulary: 5 attach points, 12 kinds. Eleven map to a shipped FA opcode;
/// <c>stat.derived</c> is the one addition, and it earns its place because patron auras, star
/// merges, expedition injuries, and contract ranks already write derived channels with no opcode.
///
/// Runtime cells are audited fact (2026-08-22 sweep), not aspiration: <c>InjectorEffectActionSink</c>
/// implements all ten opcodes, while <c>BattleEffectSink</c> states outright that "battle mode
/// consumes FA10 only; other actions are inert here".
/// </summary>
public static class AtomKindRegistry
{
    // Structural (tunables-ssot.md T2), all three: closed-vocabulary cardinalities, not balance —
    // each must match what this registry actually builds below, not a dial a balance pass turns.
    public const int AttachPointCount = 5;
    // Structural (tunables-ssot.md T2) — see AttachPointCount above.
    public const int KindCount = 12;
    // Structural (tunables-ssot.md T2) — see AttachPointCount above.
    public const int TriggerCount = 8;

    /// <summary>Event triggers plus OnTimer plus OnActivate (A18b) — for
    /// resource.delta/status.apply/shield.grant, the exactly-three kinds this reaches. Board kinds
    /// stay on the narrower <see cref="AtomTriggers.Events"/> deliberately (H3: Battle is
    /// <see cref="RuntimeState.None"/> for all of them regardless of trigger, so widening their
    /// trigger list would authorize content nothing in battle can execute).</summary>
    static readonly string[] AllTriggers =
        { AtomTriggers.OnSpawn, AtomTriggers.OnDamageDealt, AtomTriggers.OnDamageTaken,
          AtomTriggers.OnDeath, AtomTriggers.OnTimer, AtomTriggers.OnActivate };

    /// <summary>
    /// The primary stat channels — <b>eleven</b> since E16.
    ///
    /// <para>It was eight, and the documented nine were partly fiction: attackInterval,
    /// produceInterval and zombieSpeed were cheat-document keys written straight to the Unity field,
    /// bypassing the modifier bag, so no effect could reach them. E16 promoted all three, which is
    /// what makes "shoots faster" authorable at all. Read from <see cref="StatChannels.All"/> rather
    /// than copied, so the two lists cannot drift.</para>
    /// </summary>
    public static readonly string[] PrimaryChannels = Stats.StatChannels.All;

    static readonly Dictionary<string, AtomKind> Kinds = Build();

    public static IReadOnlyCollection<AtomKind> All => Kinds.Values;

    public static AtomKind? Get(string kindId) =>
        kindId is not null && Kinds.TryGetValue(kindId, out var k) ? k : null;

    /// <summary>Validate a kind id and its params. Unknown kind is a refusal, never a skip.</summary>
    public static AtomRejection Validate(string kindId, IReadOnlyDictionary<string, object?> parameters)
    {
        var kind = Get(kindId);
        if (kind is null)
            return AtomRejection.Fail(AtomRejectionReason.UnknownKind, kindId ?? "(null)");

        var pars = parameters ?? new Dictionary<string, object?>();

        var shape = kind.Params.Validate(pars);
        if (!shape.IsOk) return shape;

        // G6: an unknown PRIMARY channel used to pass validation and then write nothing, because
        // ModifierBag.Upsert only checks for a non-empty name. The registry declared PrimaryChannels
        // and never read it, which made the list documentation rather than a rule.
        if (string.Equals(kindId, "stat.modify", StringComparison.Ordinal)
            && pars.TryGetValue("channel", out var channel))
        {
            var name = channel?.ToString();
            if (!Array.Exists(PrimaryChannels, c => string.Equals(c, name, StringComparison.Ordinal)))
                return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                    $"channel '{name}' is not one of the {PrimaryChannels.Length} primary channels. " +
                    "attackInterval / produceInterval / zombieSpeed are cheat-document keys that bypass " +
                    "the modifier bag; E16 promotes them.");
        }

        // A18e (spec-battle-live-stat-modifiers.md §4): "effects cannot emit Override" was a doc
        // comment on this kind's own description with nothing enforcing it -- found while building
        // this module's own bind-time-refusal test. Same shape as G6's channel check, immediately
        // above: a validated-but-never-checked claim is the exact defect this method exists to catch.
        if (string.Equals(kindId, "stat.modify", StringComparison.Ordinal)
            && pars.TryGetValue("op", out var op)
            && string.Equals(op?.ToString(), "override", StringComparison.OrdinalIgnoreCase))
        {
            return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                "stat.modify ops are Flat|Increased|More — Override is not a legal op for this kind " +
                "(it has no revert path; a permanent Override would leak, the same reason OnGranted/" +
                "OnRemoved are lifecycle states, not authorable triggers, on this same kind).");
        }

        return AtomRejection.Ok;
    }

    /// <summary>Validate that a kind may carry a trigger. Unknown or disallowed both reject.</summary>
    public static AtomRejection ValidateTrigger(string kindId, string? trigger)
    {
        var kind = Get(kindId);
        if (kind is null)
            return AtomRejection.Fail(AtomRejectionReason.UnknownKind, kindId ?? "(null)");
        if (!AtomTriggers.IsKnown(trigger))
            return AtomRejection.Fail(AtomRejectionReason.UnknownTrigger, trigger ?? "(null)");
        if (!kind.AllowsTrigger(trigger))
            return AtomRejection.Fail(AtomRejectionReason.TriggerNotAllowed, $"{kindId} cannot carry {trigger}");
        return AtomRejection.Ok;
    }

    static Dictionary<string, AtomKind> Build()
    {
        var kinds = new AtomKind[]
        {
            // ---- Stat ----------------------------------------------------------------------
            new("stat.modify", AttachPoint.Stat, new ParamSchema(
                    // G7: a missing channel used to silently default to "atk". Required now.
                    new ParamDef("channel", ParamKind.String, Required: true),
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("amount", ParamKind.Value, Required: true)),
                // A18e (spec-battle-live-stat-modifiers.md §4): Battle was None -- battle's sink
                // ignored FA1 outright. A live, sourced/revertible-on-removal modifier ledger
                // (BattleStatModifierLedger) now composes triggered stat.modify grants through the
                // same PhasedComposeStrategy the overlay's own primary stat system uses. This
                // module's own explicit call, not A18b's: widening AllTriggers here is a kind gaining
                // trigger eligibility it never had, distinct from OnActivate existing at all.
                // Permanent, no-trigger modifiers (definitions.md §14.2) keep working exactly as
                // before -- but a naive widen broke them: AtomRowValidator.ValidateWhen infers "trigger
                // REQUIRED" from Triggers.Count > 0 (found by running the existing ChannelExtensionTests
                // fixture, not read from the code first), which had never needed to distinguish
                // "triggers ALLOWED" from "trigger REQUIRED" before this kind. TriggerOptional: true
                // (AtomKind.cs) is the fix -- stat.modify is the one kind that needs both shapes at once.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.PlanOnly),
                AllTriggers,
                PowerCategory.Offense | PowerCategory.Survivability,
                "FA1. Ops are Flat|Increased|More — effects cannot emit Override (enforced at bind, " +
                "Validate's own stat.modify check). Battle's sink now handles FA1 through a live " +
                "modifier ledger (A18e). Permanent modifier: declares no trigger (definitions.md §14.2) " +
                "— OR is now triggered, contributing from first fire onward (A18e's own scope).",
                TriggerOptional: true),

            new("stat.derived", AttachPoint.Stat, new ParamSchema(
                    new ParamDef("channel", ParamKind.String, Required: true),
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("amount", ParamKind.Value, Required: true)),
                // D6, 2026-08-22: quarantined to None/None/None because the kind had NO executor in
                // any runtime — no opcode, no EffectBag branch, no sink arm, and battle read derived
                // mods only from TraitBattleCatalog. A bind would have been accepted and then done
                // nothing forever, which is the exact failure this module exists to prevent.
                //
                // BATTLE re-opened 2026-08-23 by E12, which ships the first consumer:
                // `BattleStatComposer` reads bound stat.derived atoms at squad build, through
                // `TraitAtomSource`. Lawn and sim stay None — they still have no consumer, and
                // flipping them on the strength of battle's would re-create the quarantine's cause.
                new RuntimeSupportMatrix(RuntimeState.None, RuntimeState.Full, RuntimeState.None),
                AtomTriggers.None,
                PowerCategory.Offense | PowerCategory.Survivability | PowerCategory.Control,
                "No opcode — direct derived-channel mods. Derived ops are Flat|Increased|Replace|Flag; " +
                "there is no More on the derived side."),

            // ---- Resource ------------------------------------------------------------------
            new("resource.delta", AttachPoint.Resource, new ParamSchema(
                    // D7 (E11, 2026-08-22): `channel` was undeclared, and `ExecApplyResourceDelta`
                    // reads it (InjectorEffectActionSink.cs:132, defaulting to hp). `fx.overlay_damage`
                    // is exactly that effect — a channel and no magnitude, because the magnitude is
                    // per-grant overlay. So `amount` is optional here for the same reason it is on
                    // shield.grant (D10): a required magnitude makes overlay-driven content
                    // unauthorable. Absence is checked at BIND, against the overlay that will carry it.
                    new ParamDef("channel", ParamKind.String),
                    new ParamDef("amount", ParamKind.Value, OverlayOrParam: true),
                    new ParamDef("element", ParamKind.String),
                    new ParamDef("target", ParamKind.Object),
                    // D7: the DoT and contagion payload lives HERE, not on status.apply. EffectBag
                    // calls StatusEffectBridge.TryApplyFromGrant only inside the ApplyResourceDelta
                    // branch, and these keys are in FA10's allowlist — not FA2's.
                    new ParamDef("statusId", ParamKind.String),
                    new ParamDef("periodMs", ParamKind.Value),
                    new ParamDef("durationMs", ParamKind.Value),
                    new ParamDef("tickBudget", ParamKind.Int),
                    new ParamDef("spread", ParamKind.Object)),
                // D6: Battle was Full, then downgraded to None because no ATOM could reach it —
                // BattleEngine never granted and never called OnEvent. A18c (spec-battle-resource-shield-grants.md)
                // grew that grant path: OnActivate/OnDamageDealt now fire, Bag.Status/Bag.StatusRng
                // are wired, and a real shipped def (fx.overlay_damage) proves plain amounts, the
                // DoT/contagion payload, and the owner-matching dual-fire all work end to end
                // (BattleResourceShieldGrantsTests, T46). Full again, for real this time.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.PlanOnly),
                AllTriggers,
                PowerCategory.Offense | PowerCategory.Survivability,
                "FA10, hp add-only. The only opcode battle consumes. " +
                "Dealing damage is this plus a trigger — there is no separate damage attach point."),

            new("resource.economy", AttachPoint.Resource, new ParamSchema(
                    new ParamDef("currency", ParamKind.String, Required: true),
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("amount", ParamKind.Value, Required: true),
                    // G4: was in the legacy allowlist and implemented nowhere. E15 shipped the
                    // counter 2026-08-22 (AtomRunner + RunnerState), so the "not available yet"
                    // guard is lifted — leaving it would have made the feature unauthorable by the
                    // very content it was built for.
                    new ParamDef("capPerMatch", ParamKind.Int)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Economy,
                "FA9. Currencies are sun|money|points; the injector also exposes maxSun/maxMoney, " +
                "which FA9 does not."),

            // ---- Status --------------------------------------------------------------------
            // D7: re-derived from FA2's allowlist and ExecApplyStatus. The previous schema declared
            // statusId/durationMs/target and the DoT payload — none of which FA2 carries. FA2 allows
            // exactly { status, duration, level, chance, icd_ms, max_stacks, filters }, the executor
            // reads "status" as a string and "duration" as float SECONDS, and the target is resolved
            // from the event (ResolveStatusTargetPtr), never from a param.
            new("status.apply", AttachPoint.Status, new ParamSchema(
                    new ParamDef("status", ParamKind.String, Required: true),
                    // Seconds, not milliseconds. FA2 predates the integer-ms rule and was not changed
                    // for it; declaring durationMs here would validate a key nothing reads.
                    new ParamDef("duration", ParamKind.Value),
                    new ParamDef("level", ParamKind.Int)),
                // A18d (spec-battle-status-apply.md): Battle was Partial -- StatusRuntime was mounted
                // but reachable only through scripted InitialStatuses at battle setup, never through an
                // atom-triggered event. BattleEffectSink now has its own ExecApplyStatus branch, proven
                // against a real shipped def (fx.poison_on_hit, BattleStatusApplyTests, T49). Full.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.PlanOnly),
                AllTriggers,
                PowerCategory.Control | PowerCategory.Offense,
                "FA2 only. The DoT/contagion payload (statusId, periodMs, tickBudget, spread) lives on " +
                "FA10 `resource.delta` — StatusEffectBridge.TryApplyFromGrant is called from the " +
                "ApplyResourceDelta branch, so that content compiles to a different opcode. " +
                "G5 is a RUNTIME hole: an event resolving to an empty ptr hits the unguarded " +
                "FindObjectsOfType<Zombie>() loop, and no load-time param check can close it."),

            // D7 (E11, 2026-08-22): re-derived from the executor, not the doc. `ExecClearStatus`
            // reads `status` (InjectorEffectActionSink.cs:260) and `target` as a STRING it may omit,
            // falling back to the resolved event target. The schema declared `statusId` — a key
            // nothing reads — and made `target` a required object. Between them, the one shipped
            // FA3 effect (`fx.clear_butter`, params `{status: butter}`) was unauthorable as an atom.
            new("status.clear", AttachPoint.Status, new ParamSchema(
                    new ParamDef("status", ParamKind.String, Required: true),
                    new ParamDef("target", ParamKind.String)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA3."),

            // ---- Shield --------------------------------------------------------------------
            new("shield.grant", AttachPoint.Shield, new ParamSchema(
                    // D10: optional, not required. `fx.shield_grant` ships with EMPTY params —
                    // every magnitude is overlay — so a required `amount` would force migration to
                    // author a number the original never had, which is a behaviour change wearing a
                    // schema's clothes. Presence is a BIND-time check against the overlay.
                    new ParamDef("amount", ParamKind.Value, OverlayOrParam: true),
                    new ParamDef("element", ParamKind.String),
                    // D7: honoured by ExecGrantShield - it selects PriorityAura/PriorityInnate and flips the
                    // refillOnMerge default. Undeclared, every atom-granted shield was
                    // PrioritySkill with refill=true, so the warded family lost a shipped capability.
                    new ParamDef("sourceClass", ParamKind.String),
                    new ParamDef("priority", ParamKind.Int),
                    new ParamDef("durationTicks", ParamKind.Int),
                    new ParamDef("refillOnMerge", ParamKind.Bool),
                    new ParamDef("target", ParamKind.Object)),
                // D6: shipped Full/Full/Full, then downgraded — ExecGrantShield requires
                // Bag.ShieldGate, which neither BattleEffectHost nor SimEffectHost set at the time.
                // T14 wired Battle's own Bag.ShieldGate (this reopening, 2026-08-28); A18c
                // (spec-battle-resource-shield-grants.md) grew the grant path on top of it (OnActivate/
                // OnDamageDealt now fire real grants) — Battle is Full again, proven via a real shipped
                // def (fx.shield_grant, BattleResourceShieldGrantsTests, T46). Sim's own ShieldGate is
                // still unwired — a separate, un-scoped gap this module does not touch.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.None),
                AllTriggers,
                PowerCategory.Survivability,
                "The eleventh opcode: shipped, unnumbered, absent from the FA1-FA10 doc table, and " +
                "NOT in InjectorEffectActionSink — it executes bag-side in Core."),

            // ---- Board ---------------------------------------------------------------------
            new("spawn.entity", AttachPoint.Board, new ParamSchema(
                    new ParamDef("kind", ParamKind.String, Required: true),
                    new ParamDef("typeId", ParamKind.Int),
                    // D7/D3: E9's spawn price is chance x count x power(body), and count was declared
                    // nowhere. Floor it at 1 in E4's validator - an omitted count defaulting to 0
                    // prices the whole spawn at zero, which is the defect the body pricing fixed.
                    new ParamDef("count", ParamKind.Int,
                        NotImplementedNote: "the sink spawns one entity per plan item; count is a " +
                                            "pricing input until the executor loops"),
                    new ParamDef("row", ParamKind.Int),
                    // G1: the sink forwards a different subset per kind and silently drops the rest.
                    new ParamDef("col", ParamKind.Int, HonouredOnlyWhen: "kind=plant"),
                    new ParamDef("x", ParamKind.Value, HonouredOnlyWhen: "kind=zombie|bullet"),
                    new ParamDef("hp", ParamKind.Value, HonouredOnlyWhen: "kind=zombie"),
                    new ParamDef("maxHp", ParamKind.Value, HonouredOnlyWhen: "kind=zombie"),
                    new ParamDef("mindControlled", ParamKind.Bool, HonouredOnlyWhen: "kind=zombie"),
                    new ParamDef("atk", ParamKind.Value,
                        NotImplementedNote: "the sink drops atk for every spawn kind")),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Offense | PowerCategory.Utility,
                "FA4."),

            new("board.action", AttachPoint.Board, new ParamSchema(
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int),
                    new ParamDef("damage", ParamKind.Value)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Offense | PowerCategory.Control,
                "FA5. Ops are freeze|doom|fireline|cherry."),

            new("grid.spawn", AttachPoint.Board, new ParamSchema(
                    new ParamDef("gridItemType", ParamKind.Int, Required: true),
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int),
                    new ParamDef("graveType", ParamKind.Int,
                        NotImplementedNote: "the sink does not forward graveType")),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA6."),

            new("grid.clear", AttachPoint.Board, new ParamSchema(
                    new ParamDef("gridItemType", ParamKind.Int),
                    new ParamDef("selector", ParamKind.String)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA7."),

            new("box.set", AttachPoint.Board, new ParamSchema(
                    // D7: ExecSetBox reads this with JsonOverlay.GetInt. Declared String, an atom authoring
                    // boxType: "dirt" validated and then silently set box type 1.
                    new ParamDef("boxType", ParamKind.Int, Required: true),
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int),
                    // G2: allowlisted, but the executor handles a single cell only.
                    new ParamDef("cells", ParamKind.Array,
                        NotImplementedNote: "the executor sets a single cell; cells[] is unimplemented")),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA8."),
        };

        var map = new Dictionary<string, AtomKind>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in kinds) map[k.KindId] = k;
        return map;
    }
}
