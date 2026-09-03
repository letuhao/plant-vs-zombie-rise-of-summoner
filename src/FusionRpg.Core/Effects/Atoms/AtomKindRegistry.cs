using System.Text.Json;

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

    // ---- E29 vocabularies (spec-kind-value-guard.md §2) ---------------------------------------------
    //
    // Each is a `Func`, called fresh on every Validate — never cached into a field here — so a
    // vocabulary that grows (a new status registered, a new derived channel) widens what validates
    // with no guard edit (rule 2: "the guard resolves the SSOT; it never holds a copy"). Validation
    // runs at import/bind time, not the hit path, so re-deriving a registry per call costs nothing
    // that matters; `PvzStatsSheetComposer.CachedDerivedRegistry` shows the heavier AsyncLocal-cached
    // shape if that ever changes.

    /// <summary>267 registered derived channels (§1.1's headline case — `stat.derived` had no value
    /// check at all; `crit.rat` for `crit.rate` validated, bound, compiled, and wrote nothing).</summary>
    static IReadOnlyCollection<string> DerivedChannels() =>
        Stats.Derived.DerivedStatRegistry.CreateDefault().AllRegistered.Select(d => d.ChannelId).ToList();

    /// <summary>21 catalog statuses `status.apply`/`status.clear` both name — the union, per rule 4:
    /// a status legal here but inert on one runtime (e.g. `wither` on the lawn — it is not, see the
    /// spec's own §3 rule-4 correction) refuses at EXECUTE time with a reason, never at load.</summary>
    static IReadOnlyCollection<string> StatusIds() =>
        Status.StatusCatalogBootstrap.CreateDefault().All().Select(d => d.StatusId).ToList();

    /// <summary>5 — §2.1 corrects `AtomKindRegistry`'s own stale "which FA9 does not" claim about
    /// maxSun/maxMoney: `ExecEconomy` passes `currency` through unfiltered and `CheatActions.SetEconomy`
    /// (`CheatActions.cs:599-621`) handles both.</summary>
    static readonly string[] EconomyCurrencies = { "sun", "money", "points", "maxSun", "maxMoney" };

    /// <summary>3 authorable spellings for 2 behaviours. `ExecEconomy` (`InjectorEffectActionSink.cs`)
    /// treats literally any non-"add"/"+" string as "set" — the most damaging silent no-op in the set,
    /// because a typo like `op: "addd"` succeeds loudly at the wrong thing instead of failing at the
    /// right one. "set" is the vocabulary's canonical spelling for "not add".</summary>
    static readonly string[] EconomyOps = { "add", "+", "set" };

    /// <summary>6 — <see cref="Stats.Derived.DerivedStatChannels.ResourceIds"/>, the stat layer's own
    /// SSOT. Declared as the full 6 regardless of E28's own rollout state (only `hp` executes today) —
    /// a runtime gap is E28's reporting concern, not a reason to narrow what the schema accepts
    /// (rule 4; and this vocabulary must widen the moment E28 fix #1 ships, with no guard edit).</summary>
    static IReadOnlyCollection<string> ResourceChannels() => Stats.Derived.DerivedStatChannels.ResourceIds;

    /// <summary>3 — `EffectBag.cs:597-599`: any string that isn't "aura"/"innate" (case-insensitive)
    /// silently becomes "skill" today, so a typo'd `sourceClass: "arua"` succeeds as the wrong
    /// priority/refill-on-merge combination instead of failing loudly. "skill" is the vocabulary's
    /// canonical spelling for the fallback. (`shield.grant.element` needs no vocabulary here — it is
    /// already strict-parsed via `ElementRoster.TryParse` and refused at `EffectBag.cs:585-594`.)</summary>
    static readonly string[] ShieldSourceClasses = { "aura", "innate", "skill" };

    /// <summary>4 canonical spellings `DebugActions.BoardAction`'s switch matches after
    /// `ExecBoardAction`'s own substring normalization (`Contains("cherry") → cherry`, etc. —
    /// `InjectorEffectActionSink.cs`). The one shipped `board.action` atom already authors the
    /// canonical form; this is additive for existing content, and makes the normalization's non-
    /// canonical aliases (`"CreateCherryBomb"`, …) unauthorable going forward — a deliberate narrowing,
    /// not an oversight: a named refusal at load beats a substring match nobody can see failing.</summary>
    static readonly string[] BoardActionOps = { "freeze", "doom", "fireline", "cherry" };

    /// <summary>12 — the shipped `GridItemType` IL2CPP enum, reflected off the game's own
    /// `Assembly-CSharp.dll` 2026-09-03 (Core has no Unity reference, so this is Core's own mirror of
    /// that enum, not a second copy of anything already in Core — matching how `ElementTypeId` etc.
    /// already work). Ordinals: CraterDay=0, CraterNight=1, (2 unused), Ladder=3, ScaryPot=4,
    /// ScaryPot_plant=5, ScaryPot_zombie=6, Grave=7, IceBlock=8, ScaryPot_hypnoZombie=9,
    /// ScaryPot_obsidian=10, ScaryPot_gold=11, ScaryPot_red=12.</summary>
    static readonly string[] GridItemTypeValues =
        { "0", "1", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12" };

    /// <summary>8 — the shipped `BoxType` IL2CPP enum, reflected the same way 2026-09-03:
    /// Grass=0, Water=1, Dirt=2, Roof=3, Stone=4, River=5, Dirt_water=6, Lava=7. This is also the
    /// evidence behind E28's content fix — `fx.set_dirt_box` authored `boxType: 1` (Water) and meant
    /// Dirt (2).</summary>
    static readonly string[] BoxTypeValues = { "0", "1", "2", "3", "4", "5", "6", "7" };

    /// <summary>3 — the only kinds `ExecSpawnEntity`'s switch has arms for.</summary>
    static readonly string[] SpawnEntityKinds = { "plant", "zombie", "bullet" };

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

        // E29 (spec-kind-value-guard.md): a value from an enumerable vocabulary — status id,
        // currency, board verb, grid item type — that is not a member is a load-time refusal, not a
        // silent no-op. One generic loop over every param carrying a declared Vocabulary, reading
        // each SSOT fresh (never a copy) so a vocabulary that grows widens what validates with no
        // guard edit. This is G6 generalised: `stat.modify.channel`'s own check (the original G6) now
        // runs through this exact loop via its Vocabulary declaration below, rather than as its own
        // special case — proof the generic mechanism produces identical behaviour to the hand-rolled
        // one it replaces, not just a template for the other twelve.
        foreach (var def in kind.Params.Defs)
        {
            if (def.Vocabulary is null) continue;
            if (!pars.TryGetValue(def.Name, out var raw) || raw is null) continue; // absence is ParamSchema's job

            // E30 (spec-channel-pool.md §3.2): a JSON OBJECT value here is a pool reference, never a
            // scalar member of this string vocabulary — `Convert.ToString` on the whole object would
            // stringify it to `{"pool":"...","count":...}` and always fail this check, refusing every
            // valid pool reference before AtomRowValidator's own pool-specific check (§3.3) ever runs.
            // Skipped, not evaluated-and-passed: the pool form's OWN members are checked against this
            // exact same vocabulary by ValidateChannelPoolRef, so nothing here goes unchecked.
            if (raw is JsonElement { ValueKind: JsonValueKind.Object } or Dictionary<string, object?>)
                continue;

            var members = def.Vocabulary();
            var value = Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (!members.Contains(value, StringComparer.Ordinal))
                return AtomRejection.Fail(AtomRejectionReason.BadParamValue,
                    $"{kindId}.{def.Name} '{value}' is not one of the {members.Count} legal values for this param");
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
                    // E29 (was G6): an unknown PRIMARY channel used to pass validation and then write
                    // nothing, because ModifierBag.Upsert only checks for a non-empty name.
                    new ParamDef("channel", ParamKind.String, Required: true,
                        Vocabulary: () => PrimaryChannels),
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
                    // E29 (spec-kind-value-guard.md §1.1): AtomRowValidator.cs's own registered-
                    // channel hand-off never ran for this kind ("unregistered channel is G6's job, not
                    // this check's" — but G6 was scoped to stat.modify only). `crit.rat` for
                    // `crit.rate`, one letter off out of 267, used to validate, bind, compile, and
                    // write nothing forever.
                    new ParamDef("channel", ParamKind.String, Required: true, Vocabulary: DerivedChannels),
                    new ParamDef("op", ParamKind.String, Required: true),
                    new ParamDef("amount", ParamKind.Value, Required: true)),
                // D6, 2026-08-22: quarantined to None/None/None because the kind had NO executor in
                // any runtime — no opcode, no EffectBag branch, no sink arm, and battle read derived
                // mods only from TraitBattleCatalog. A bind would have been accepted and then done
                // nothing forever, which is the exact failure this module exists to prevent.
                //
                // BATTLE re-opened 2026-08-23 by E12, which ships the first consumer:
                // `BattleStatComposer` reads bound stat.derived atoms at squad build, through
                // `TraitAtomSource`.
                //
                // LAWN re-opened 2026-08-30 (decisions.md, "Derived-write lawn executor" — owner
                // approved) now that it, too, has a real consumer: `AtomDerivedSubsystem`, an
                // `IActorStatSubsystem` registered on the injector's `ActorHub` at the reserved
                // order-350 `foundation.effect` slot, contributing bound stat.derived atoms into the
                // same `DerivedComposer` fold every other derived producer already uses. The flip is
                // deliberately the LAST step of that change, not the first: flipping before the
                // executor existed would have re-created D6's exact state (binds accepted, nothing
                // applied) inside the change meant to end it.
                //
                // SIM stays None — `SimEffectHost` still has no consumer, and flipping it on the
                // strength of the other two would re-create the quarantine's cause.
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.Full, RuntimeState.None),
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
                    // E29: 6 after E28 (see spec §2's own note — only "hp" executes until E28 fix #1
                    // ships; the schema declares the full SSOT regardless, a runtime gap being E28's
                    // reporting concern, not a reason to narrow what validates here).
                    new ParamDef("channel", ParamKind.String, Vocabulary: ResourceChannels),
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
                    // E29 §2.1 correction 1: the description below used to claim maxSun/maxMoney were
                    // injector-only and outside FA9's own reach — false. `ExecEconomy` passes
                    // `currency` through unfiltered and `CheatActions.SetEconomy` handles both, so the
                    // vocabulary is 5, not 3. §7.1 (decided 2026-09-03): this is also where "no empire
                    // currency is atom-authorable" is recorded — loam/soul/essence.*/shard.* share no
                    // member with this vocabulary, so `currency: "loam"` is now a hard load-time
                    // refusal. Correct: an atom writes the match-scoped economy, never the empire
                    // ledger, and that boundary belongs on the atom layer's own definition, not a
                    // fourth document.
                    new ParamDef("currency", ParamKind.String, Required: true, Vocabulary: () => EconomyCurrencies),
                    // E29: any non-"add"/"+" string silently meant "set" — `op: "addd"` succeeded
                    // loudly at the wrong behaviour instead of failing at the right one.
                    new ParamDef("op", ParamKind.String, Required: true, Vocabulary: () => EconomyOps),
                    new ParamDef("amount", ParamKind.Value, Required: true),
                    // G4: was in the legacy allowlist and implemented nowhere. E15 shipped the
                    // counter 2026-08-22 (AtomRunner + RunnerState), so the "not available yet"
                    // guard is lifted — leaving it would have made the feature unauthorable by the
                    // very content it was built for.
                    new ParamDef("capPerMatch", ParamKind.Int)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Economy,
                "FA9. Currencies are sun|money|points|maxSun|maxMoney — 5, not 3; the injector does " +
                "not narrow this (E29 §2.1 correction 1). Empire currencies (loam, soul, essence.*, " +
                "shard.*) share no member with this vocabulary and are not atom-authorable: an atom " +
                "writes the match-scoped economy, never the empire ledger."),

            // ---- Status --------------------------------------------------------------------
            // D7: re-derived from FA2's allowlist and ExecApplyStatus. The previous schema declared
            // statusId/durationMs/target and the DoT payload — none of which FA2 carries. FA2 allows
            // exactly { status, duration, level, chance, icd_ms, max_stacks, filters }, the executor
            // reads "status" as a string and "duration" as float SECONDS, and the target is resolved
            // from the event (ResolveStatusTargetPtr), never from a param.
            new("status.apply", AttachPoint.Status, new ParamSchema(
                    // E29: 21 catalog statuses, the union across runtimes (rule 4) — a status legal
                    // here but inert on a given runtime (e.g. the eight the lawn's Unity CC switch
                    // implements, `DebugActions.cs:861-909`) refuses at EXECUTE time with a name,
                    // never at load.
                    new ParamDef("status", ParamKind.String, Required: true, Vocabulary: StatusIds),
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
                    // E29: same 21-status vocabulary as status.apply (rule 4's union) — the lawn's
                    // own 4-of-21 executable-today gap (E28) is an execute-time reporting concern,
                    // not a reason to narrow what a status.clear atom may name.
                    new ParamDef("status", ParamKind.String, Required: true, Vocabulary: StatusIds),
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
                    // E29: needs no Vocabulary here — already strict-parsed via ElementRoster.TryParse
                    // and refused at EffectBag.cs:585-594 (spec §2's own note).
                    new ParamDef("element", ParamKind.String),
                    // D7: honoured by ExecGrantShield - it selects PriorityAura/PriorityInnate and flips the
                    // refillOnMerge default. Undeclared, every atom-granted shield was
                    // PrioritySkill with refill=true, so the warded family lost a shipped capability.
                    // E29: any non-"aura"/"innate" string silently became "skill" — a typo like
                    // "arua" succeeded at the wrong priority/refill combination instead of failing.
                    new ParamDef("sourceClass", ParamKind.String, Vocabulary: () => ShieldSourceClasses),
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
                    // E29: the only three kinds ExecSpawnEntity's switch has arms for.
                    new ParamDef("kind", ParamKind.String, Required: true, Vocabulary: () => SpawnEntityKinds),
                    new ParamDef("typeId", ParamKind.Int),
                    // D7/D3: E9's spawn price is chance x count x power(body), and count was declared
                    // nowhere. Floor it at 1 in E4's validator - an omitted count defaulting to 0
                    // prices the whole spawn at zero, which is the defect the body pricing fixed.
                    // E28 fix #5: the sink now loops count spawns (floored at 1 — structural, not a
                    // progression cap: zero spawns is not a legal "less of the effect").
                    new ParamDef("count", ParamKind.Int),
                    new ParamDef("row", ParamKind.Int),
                    // G1: the sink forwards a different subset per kind and silently drops the rest.
                    new ParamDef("col", ParamKind.Int, HonouredOnlyWhen: "kind=plant"),
                    new ParamDef("x", ParamKind.Value, HonouredOnlyWhen: "kind=zombie|bullet"),
                    new ParamDef("hp", ParamKind.Value, HonouredOnlyWhen: "kind=zombie"),
                    new ParamDef("maxHp", ParamKind.Value, HonouredOnlyWhen: "kind=zombie"),
                    new ParamDef("mindControlled", ParamKind.Bool, HonouredOnlyWhen: "kind=zombie"),
                    // E28 fix #5: DebugActions.ApplyAbsoluteProps already has an absolute-atk hook for
                    // plants (P-ATK) and the Z-ATK cheat id already exists for zombies — the sink just
                    // never forwarded `atk` into the payload for either kind, and never gave the zombie
                    // branch an atk read at all. Bullets have no such hook (they carry `damage` on the
                    // projectile itself, a different mechanism) — scoped out, not silently dropped.
                    new ParamDef("atk", ParamKind.Value, HonouredOnlyWhen: "kind=plant|zombie")),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Offense | PowerCategory.Utility,
                "FA4."),

            new("board.action", AttachPoint.Board, new ParamSchema(
                    // E29: the 4 canonical spellings ExecBoardAction's own normalization maps onto —
                    // a deliberate narrowing of the substring-matched aliases it also accepts
                    // ("CreateCherryBomb" etc, none shipped): a named refusal at load beats a
                    // substring match nobody can see failing.
                    new ParamDef("op", ParamKind.String, Required: true, Vocabulary: () => BoardActionOps),
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int),
                    new ParamDef("damage", ParamKind.Value)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Offense | PowerCategory.Control,
                "FA5. Ops are freeze|doom|fireline|cherry."),

            new("grid.spawn", AttachPoint.Board, new ParamSchema(
                    // E29: the 12-member GridItemType vocabulary (Core's own mirror — see
                    // GridItemTypeValues' own doc comment for the reflected ordinals).
                    new ParamDef("gridItemType", ParamKind.Int, Required: true,
                        Vocabulary: () => GridItemTypeValues),
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int),
                    // E28 (spec-param-parity.md §3 row 6): the sink now forwards graveType to
                    // DebugActions.SpawnGrid, which already read and honoured it.
                    new ParamDef("graveType", ParamKind.Int)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA6."),

            new("grid.clear", AttachPoint.Board, new ParamSchema(
                    // E29: same 12-member GridItemType vocabulary as grid.spawn.
                    new ParamDef("gridItemType", ParamKind.Int, Vocabulary: () => GridItemTypeValues),
                    new ParamDef("selector", ParamKind.String),
                    // E28 (spec-param-parity.md §3 row 4): DebugActions.ClearGridItem already accepts
                    // col/row (DebugActions.cs:639-668) — targeted clearing was reachable Unity-side
                    // and simply never declared, so an atom could not narrow which cell to clear and a
                    // multi-match runtime call refused outright ("multiple matches; pass col/row or
                    // random:true", DebugActions.cs:666).
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int)),
                new RuntimeSupportMatrix(RuntimeState.Full, RuntimeState.None, RuntimeState.PlanOnly),
                AtomTriggers.Events,
                PowerCategory.Utility,
                "FA7."),

            new("box.set", AttachPoint.Board, new ParamSchema(
                    // D7: ExecSetBox reads this with JsonOverlay.GetInt. Declared String, an atom authoring
                    // boxType: "dirt" validated and then silently set box type 1.
                    // E29: the 8-member BoxType vocabulary (Core's own mirror — see BoxTypeValues'
                    // own doc comment for the reflected ordinals; this is also E28's own content-fix
                    // evidence, `fx.set_dirt_box` authored `1` (Water) and meant `2` (Dirt)).
                    new ParamDef("boxType", ParamKind.Int, Required: true, Vocabulary: () => BoxTypeValues),
                    new ParamDef("row", ParamKind.Int),
                    new ParamDef("col", ParamKind.Int),
                    // E28 fix #7 (spec-param-parity.md §3 row 7): the executor now paints every listed
                    // cell. Each entry is `{row, col}` — the same shape `row`/`col` already have on
                    // this kind, just plural. Required AtomCompiler.Plain() to preserve array/object
                    // structure instead of stringifying it (fixed alongside), and AtomPushCodec.ToDef
                    // to unwrap the wire's JsonElement boxing recursively (fixed the same session —
                    // the cross-cutting defect this array param exposed while being specced).
                    new ParamDef("cells", ParamKind.Array)),
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
