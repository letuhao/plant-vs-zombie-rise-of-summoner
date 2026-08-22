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
    public const int AttachPointCount = 5;
    public const int KindCount = 12;
    public const int TriggerCount = 7;

    /// <summary>Event triggers plus OnTimer — for kinds that can also fire on a tick.</summary>
    static readonly string[] AllTriggers =
        { AtomTriggers.OnSpawn, AtomTriggers.OnDamageDealt, AtomTriggers.OnDamageTaken,
          AtomTriggers.OnDeath, AtomTriggers.OnTimer };

    /// <summary>Primary stat channels — the real eight. The documented nine were partly fiction:
    /// attackInterval/produceInterval/zombieSpeed are cheat-document keys that bypass the modifier
    /// bag entirely and cannot be reached by an effect until E16 promotes them.</summary>
    public static readonly string[] PrimaryChannels =
        { "hp", "maxHp", "atk", "defense", "arm1", "arm1Max", "arm2", "arm2Max" };

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
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.None,
                PowerCategory.Offense | PowerCategory.Survivability,
                "FA1. Ops are Flat|Increased|More — effects cannot emit Override. " +
                "Battle's sink ignores FA1, so battle is not supported. " +
                "Permanent modifier: declares no trigger (definitions.md §14.2)."),

            new("stat.derived", AttachPoint.Stat, new ParamSchema(
                    new ParamDef("channel", ParamKind.String, Required: true),
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("amount", ParamKind.Value, Required: true)),
                // D6, 2026-08-22 re-verification: this shipped as Full/Full/Full and has NO executor
                // in any runtime. No opcode, no EffectBag branch, no sink arm (the injector sink
                // dispatches ten actions and throws on unknown), and battle reads derived mods only
                // from TraitBattleCatalog — never from a grant. A bind would be accepted and then do
                // nothing, forever, which is status.expose.* again: the exact failure this module
                // exists to prevent, and a violation of its own "never add a kind without an executor".
                // Goes back to Full per runtime as each consumer ships (E12's BattleStatComposer seam first).
                new RuntimeSupportMatrix(RuntimeState.None, RuntimeState.None, RuntimeState.None),
                AtomTriggers.None,
                PowerCategory.Offense | PowerCategory.Survivability | PowerCategory.Control,
                "No opcode — direct derived-channel mods. Derived ops are Flat|Increased|Replace|Flag; " +
                "there is no More on the derived side."),

            // ---- Resource ------------------------------------------------------------------
            new("resource.delta", AttachPoint.Resource, new ParamSchema(
                    new ParamDef("amount", ParamKind.Value, Required: true),
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
                // D6: Battle was Full. Battle's sink does handle FA10, but no ATOM can reach it —
                // BattleEngine never grants and never calls OnEvent, so a bound resource.delta is a
                // silent no-op. Full again when battle grows a grant path.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AllTriggers,
                PowerCategory.Offense | PowerCategory.Survivability,
                "FA10, hp add-only. The only opcode battle consumes. " +
                "Dealing damage is this plus a trigger — there is no separate damage attach point."),

            new("resource.economy", AttachPoint.Resource, new ParamSchema(
                    new ParamDef("currency", ParamKind.String, Required: true),
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("amount", ParamKind.Value, Required: true),
                    // G4: in the legacy allowlist, implemented nowhere. E15 owns the counter.
                    new ParamDef("capPerMatch", ParamKind.Int,
                        NotImplementedNote: "per-match caps are runner-owned (E15); not available yet")),
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
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Partial, RuntimeState.PlanOnly),
                AllTriggers,
                PowerCategory.Control | PowerCategory.Offense,
                "FA2 only. The DoT/contagion payload (statusId, periodMs, tickBudget, spread) lives on " +
                "FA10 `resource.delta` — StatusEffectBridge.TryApplyFromGrant is called from the " +
                "ApplyResourceDelta branch, so that content compiles to a different opcode. " +
                "G5 is a RUNTIME hole: an event resolving to an empty ptr hits the unguarded " +
                "FindObjectsOfType<Zombie>() loop, and no load-time param check can close it."),

            new("status.clear", AttachPoint.Status, new ParamSchema(
                    new ParamDef("statusId", ParamKind.String),
                    new ParamDef("target", ParamKind.Object, Required: true)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA3."),

            // ---- Shield --------------------------------------------------------------------
            new("shield.grant", AttachPoint.Shield, new ParamSchema(
                    new ParamDef("amount", ParamKind.Value, Required: true),
                    new ParamDef("element", ParamKind.String),
                    // D7: honoured by ExecGrantShield - it selects PriorityAura/PriorityInnate and flips the
                    // refillOnMerge default. Undeclared, every atom-granted shield was
                    // PrioritySkill with refill=true, so the warded family lost a shipped capability.
                    new ParamDef("sourceClass", ParamKind.String),
                    new ParamDef("priority", ParamKind.Int),
                    new ParamDef("durationTicks", ParamKind.Int),
                    new ParamDef("refillOnMerge", ParamKind.Bool),
                    new ParamDef("target", ParamKind.Object)),
                // D6: shipped Full/Full/Full. ExecGrantShield requires Bag.ShieldGate, which is set in
                // exactly two places — FoundationHarness and the injector's EffectRuntime. Neither
                // BattleEffectHost nor SimEffectHost sets it, so in both the grant skips with
                // "shield-runtime-missing". Sim is one line of wiring away; battle also needs a grant path.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.None),
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
