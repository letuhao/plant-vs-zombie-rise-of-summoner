using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// E1 acceptance. The count guards exist so growing the vocabulary requires editing a test and
/// noticing — a closed vocabulary that drifts is just an open one with extra steps.
/// </summary>
public class AtomKindRegistryTests
{
    static Dictionary<string, object?> P(params (string Key, object? Value)[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in pairs) d[k] = v;
        return d;
    }

    [Fact]
    public void Vocabulary_is_closed_at_twelve_kinds_and_five_attach_points()
    {
        Assert.Equal(AtomKindRegistry.KindCount, AtomKindRegistry.All.Count);
        Assert.Equal(AtomKindRegistry.AttachPointCount, Enum.GetValues<AttachPoint>().Length);
    }

    [Fact]
    public void Rejection_reasons_are_the_closed_list_of_thirty_three()
    {
        // definitions.md §10 fixes the list at 33 (plus None). It is the operator-facing error
        // surface: a code added without review is a code no runbook explains.
        var reasons = Enum.GetValues<AtomRejectionReason>();

        Assert.Equal(34, reasons.Length);
        Assert.Contains(AtomRejectionReason.None, reasons);
    }

    [Fact]
    public void Every_kind_declares_a_runtime_a_trigger_and_a_power_category()
    {
        // Kinds with no executor in ANY runtime. Listing them here is the point: a kind may sit in
        // the vocabulary ahead of its consumer, but it must be quarantined (all-None, so binds are
        // rejected) and named in this set, never advertising support it does not have.
        //
        // EMPTY since 2026-08-23: `stat.derived` was the only occupant, and E12 shipped its first
        // consumer. An empty set is the healthy state — a kind waiting for a consumer is a promise
        // the vocabulary has not kept yet.
        var awaitingConsumer = Array.Empty<string>();

        // Permanent modifiers are not event-driven, so they declare no trigger (definitions.md §14.2).
        // stat.modify moved out of this set 2026-08-28 (A18e) -- it is no longer PURELY permanent, it
        // may ALSO be triggered (contributing from first fire onward); TriggerOptional (AtomKind.cs)
        // is what keeps its OWN no-trigger case still legal despite Triggers now being non-empty.
        var permanentModifiers = new[] { "stat.derived" };

        foreach (var kind in AtomKindRegistry.All)
        {
            var anyRuntime = kind.SupportIn(RuntimeId.Lawn) != RuntimeState.None
                             || kind.SupportIn(RuntimeId.Battle) != RuntimeState.None
                             || kind.SupportIn(RuntimeId.Sim) != RuntimeState.None;

            if (awaitingConsumer.Contains(kind.KindId))
                Assert.False(anyRuntime, $"{kind.KindId} is listed as awaiting a consumer but claims a runtime");
            else
                Assert.True(anyRuntime, $"{kind.KindId} supports no runtime and is not listed as awaiting one");

            if (permanentModifiers.Contains(kind.KindId))
                Assert.Empty(kind.Triggers);
            else
                Assert.True(kind.Triggers.Count > 0, $"{kind.KindId} allows no trigger");

            Assert.True(kind.Categories != PowerCategory.None, $"{kind.KindId} prices to no category");
        }
    }

    [Fact]
    public void Trigger_vocabulary_is_closed_at_eight()
    {
        // A18b (spec-on-activate-trigger.md): 7 -> 8 with OnActivate. Still a self-consistency check,
        // not a hardcoded literal -- TriggerCount and AtomTriggers.All.Length moved together.
        Assert.Equal(AtomKindRegistry.TriggerCount, AtomTriggers.All.Length);
        Assert.Equal(AtomTriggers.All.Length, AtomTriggers.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_kinds_triggers_are_drawn_from_the_eight()
    {
        foreach (var kind in AtomKindRegistry.All)
            foreach (var t in kind.Triggers)
                Assert.True(AtomTriggers.IsKnown(t), $"{kind.KindId} names unknown trigger {t}");
    }

    [Fact]
    public void Unknown_trigger_and_disallowed_trigger_reject_differently()
    {
        Assert.Equal(AtomRejectionReason.UnknownTrigger,
            AtomKindRegistry.ValidateTrigger("stat.modify", "OnWave").Reason);

        // stat.derived is STILL a pure permanent modifier (A18e only widened stat.modify): it carries
        // no trigger at all. Not OnDamageDealt...
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("stat.derived", AtomTriggers.OnDamageDealt).Reason);

        // ...and not OnGranted either, for EITHER kind (stat.modify included, despite its own
        // AllTriggers widen -- OnGranted/OnRemoved are the separate Lifecycle array, never part of
        // AllTriggers). OnGranted/OnRemoved are runtime lifecycle states, not authorable triggers
        // (definitions.md §14.2) — the bag injects the revert itself. Allowing content to name only
        // the OnGranted half was how a permanent buff could leak.
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("stat.modify", AtomTriggers.OnGranted).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("stat.derived", AtomTriggers.OnGranted).Reason);

        // stat.modify's own new case: OnDamageDealt (part of AllTriggers since A18e) is now allowed.
        Assert.True(AtomKindRegistry.ValidateTrigger("stat.modify", AtomTriggers.OnDamageDealt).IsOk);

        Assert.True(AtomKindRegistry.ValidateTrigger("resource.delta", AtomTriggers.OnTimer).IsOk);
    }

    /// <summary>
    /// A18b (spec-on-activate-trigger.md §1): the one-line `AllTriggers` widen reached exactly three
    /// kinds at first (A18c/d's own) — this test's own original doc comment predicted `stat.modify`
    /// would move separately, "A18e's own separate call." A18e (2026-08-28) made that call: updated
    /// here to the post-A18e state (four kinds now), not a stale prediction left unfulfilled. Board
    /// kinds stay refused (H3: Battle is `RuntimeState.None` for all of them regardless of trigger).
    /// </summary>
    [Fact]
    public void OnActivate_reaches_exactly_resource_delta_status_apply_shield_grant_and_stat_modify()
    {
        Assert.True(AtomKindRegistry.ValidateTrigger("resource.delta", AtomTriggers.OnActivate).IsOk);
        Assert.True(AtomKindRegistry.ValidateTrigger("status.apply", AtomTriggers.OnActivate).IsOk);
        Assert.True(AtomKindRegistry.ValidateTrigger("shield.grant", AtomTriggers.OnActivate).IsOk);
        Assert.True(AtomKindRegistry.ValidateTrigger("stat.modify", AtomTriggers.OnActivate).IsOk);

        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("stat.derived", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("spawn.entity", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("board.action", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("grid.spawn", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("grid.clear", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("box.set", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("resource.economy", AtomTriggers.OnActivate).Reason);
        Assert.Equal(AtomRejectionReason.TriggerNotAllowed,
            AtomKindRegistry.ValidateTrigger("status.clear", AtomTriggers.OnActivate).Reason);
    }

    // Four states, not three. PlanOnly must never read as Full, or sim silently accepts
    // bindings it cannot execute - the exact no-op this layer exists to prevent.
    [Fact]
    public void Runtime_support_carries_four_distinct_states()
    {
        var statModify = AtomKindRegistry.Get("stat.modify")!;
        Assert.Equal(RuntimeState.Full, statModify.SupportIn(RuntimeId.Lawn));
        // Battle moved None -> Full with A18e (2026-08-28) -- BattleStatModifierLedger. spawn.entity
        // (below, Battle_support_is_narrow_and_honest) is this matrix's own live None example now.
        Assert.Equal(RuntimeState.Full, statModify.SupportIn(RuntimeId.Battle));
        Assert.Equal(RuntimeState.PlanOnly, statModify.SupportIn(RuntimeId.Sim));

        // status.apply in battle is Full since A18d (2026-08-28): BattleEffectSink's own
        // ExecApplyStatus branch means a real FA2 path exists now, not just scripted InitialStatuses.
        Assert.Equal(RuntimeState.Full,
            AtomKindRegistry.Get("status.apply")!.SupportIn(RuntimeId.Battle));
    }

    [Fact]
    public void Unknown_kind_is_a_rejection_not_a_skip()
    {
        var r = AtomKindRegistry.Validate("stat.nope", P());
        Assert.Equal(AtomRejectionReason.UnknownKind, r.Reason);
    }

    [Fact]
    public void Unknown_param_rejects()
    {
        var r = AtomKindRegistry.Validate("stat.modify",
            P(("channel", "atk"), ("op", "flat"), ("amount", 10), ("wat", 1)));
        Assert.Equal(AtomRejectionReason.UnknownParam, r.Reason);
    }

    // G7: ExecModifyStat defaults a missing channel to "atk". A malformed atom would silently
    // buff attack, which is the worst possible failure mode for a content bug.
    [Fact]
    public void Missing_channel_rejects_rather_than_defaulting_to_atk()
    {
        var r = AtomKindRegistry.Validate("stat.modify", P(("op", "flat"), ("amount", 10)));
        Assert.Equal(AtomRejectionReason.MissingParam, r.Reason);
        Assert.Contains("channel", r.Detail);
    }

    // G1: the overlay allowlist accepts atk on spawn; the injector sink drops it for every kind.
    [Fact]
    public void Spawn_atk_rejects_because_the_sink_drops_it()
    {
        var r = AtomKindRegistry.Validate("spawn.entity",
            P(("kind", "zombie"), ("typeId", 0), ("atk", 500)));
        Assert.Equal(AtomRejectionReason.ParamNotImplemented, r.Reason);
    }

    // G1 again, the conditional half: hp is forwarded for zombies and dropped for plants.
    [Theory]
    [InlineData("zombie", true)]
    [InlineData("plant", false)]
    [InlineData("bullet", false)]
    public void Spawn_hp_is_only_honoured_for_zombies(string kind, bool shouldPass)
    {
        var r = AtomKindRegistry.Validate("spawn.entity",
            P(("kind", kind), ("typeId", 0), ("hp", 500)));

        if (shouldPass) Assert.True(r.IsOk, r.ToString());
        else Assert.Equal(AtomRejectionReason.ParamNotHonoured, r.Reason);
    }

    [Fact]
    public void Spawn_col_is_only_honoured_for_plants()
    {
        Assert.True(AtomKindRegistry.Validate("spawn.entity",
            P(("kind", "plant"), ("col", 3))).IsOk);

        Assert.Equal(AtomRejectionReason.ParamNotHonoured, AtomKindRegistry.Validate("spawn.entity",
            P(("kind", "zombie"), ("col", 3))).Reason);
    }

    // G2: cells[] is allowlisted; the executor sets a single cell.
    [Fact]
    public void BoxSet_cells_rejects_as_unimplemented()
    {
        var r = AtomKindRegistry.Validate("box.set",
            P(("boxType", "Dirt"), ("cells", new[] { 1, 2 })));
        Assert.Equal(AtomRejectionReason.ParamNotImplemented, r.Reason);
    }

    // G4 CLOSED 2026-08-22. capPerMatch sat in the FA9 allowlist implemented nowhere, so E1 refused
    // it at load. E15 shipped the counter (AtomRunner + RunnerState + CapPerMatchTests), and leaving
    // the refusal in place would have made the feature unauthorable by the content it was built for
    // — a guard outliving its reason is just a silently dead feature.
    [Fact]
    public void Economy_capPerMatch_validates_now_that_the_runner_owns_it()
    {
        var r = AtomKindRegistry.Validate("resource.economy",
            P(("currency", "sun"), ("op", "add"), ("amount", 25), ("capPerMatch", 3)));

        Assert.True(r.IsOk, r.ToString());
        Assert.Null(AtomKindRegistry.Get("resource.economy")!.Params.Defs
            .First(d => d.Name == "capPerMatch").NotImplementedNote);
    }

    // G5: an empty target means "every zombie on the board" — and D7 established that this CANNOT be
    // closed here. FA2 has no `target` param at all; the target comes from ResolveStatusTargetPtr(ctx),
    // i.e. from the event, at runtime. A required-key check would have declared a key the executor
    // never reads, so content would validate and the hole would stay open. The fix belongs to whoever
    // guards the FindObjectsOfType<Zombie>() loop, not to load-time validation.
    [Fact]
    public void StatusApply_does_not_pretend_to_close_G5_with_a_param()
    {
        Assert.False(
            AtomKindRegistry.Get("status.apply")!.Params.Defs.Any(d => d.Name == "target"),
            "FA2 carries no target param; declaring one would validate a key nothing reads");

        // The FA2 shape that really is authorable validates.
        Assert.True(AtomKindRegistry.Validate("status.apply", P(("status", "butter"))).IsOk);
    }

    // The counterpart: a missing `status` is a real load-time refusal, because FA2 does read it.
    [Fact]
    public void StatusApply_requires_the_status_name_FA2_reads()
    {
        var r = AtomKindRegistry.Validate("status.apply", P(("level", 1)));
        Assert.Equal(AtomRejectionReason.MissingParam, r.Reason);
        Assert.Contains("status", r.Detail);
    }

    [Fact]
    public void Canonical_samples_validate()
    {
        Assert.True(AtomKindRegistry.Validate("stat.modify",
            P(("channel", "maxHp"), ("op", "flat"), ("amount", 10))).IsOk);

        Assert.True(AtomKindRegistry.Validate("stat.derived",
            P(("channel", "combat.power.fire"), ("op", "flat"), ("amount", 10))).IsOk);

        Assert.True(AtomKindRegistry.Validate("resource.delta",
            P(("amount", -50), ("element", "fire"))).IsOk);

        Assert.True(AtomKindRegistry.Validate("shield.grant",
            P(("amount", 80), ("element", "ice"))).IsOk);

        Assert.True(AtomKindRegistry.Validate("board.action", P(("op", "cherry"))).IsOk);
    }

    // Battle consumes FA10 only and never calls OnEvent - the matrix says so rather than pretending.
    [Fact]
    public void Battle_support_is_narrow_and_honest()
    {
        // D6, 2026-08-22: this test used to assert None for resource.delta/shield.grant.
        // Re-verification against BattleEngine at the time showed both unreachable FROM AN ATOM:
        // battle never granted and never called OnEvent, so no trigger could fire; BattleEffectHost
        // never set Bag.ShieldGate, so a shield grant skipped with "shield-runtime-missing". Battle
        // having a working FA10 sink was not the same as an atom being able to reach it.
        //
        // A18a-c (2026-08-28, action-map.md §12) grew that grant path for real: BattleRunState binds
        // real EffectGrantDtos (A18a), OnActivate/OnDamageDealt actually fire (A18b/A18c), and
        // Bag.Status/Bag.StatusRng/Bag.ShieldGate are all wired. Both cells move to Full for the same
        // reason stat.derived's did below -- the cell moved because the code did, proven by real
        // shipped defs in BattleResourceShieldGrantsTests (T46), not asserted from this test alone.
        Assert.Equal(RuntimeState.Full, AtomKindRegistry.Get("resource.delta")!.SupportIn(RuntimeId.Battle));
        Assert.Equal(RuntimeState.Full, AtomKindRegistry.Get("shield.grant")!.SupportIn(RuntimeId.Battle));

        // stat.derived RE-OPENED for battle 2026-08-23: E12 shipped the consumer. `BattleStatComposer`
        // reads bound stat.derived atoms at squad build, through `TraitAtomSource` — the same place it
        // read `ChannelMods` before. The cell moved because the code did, which is the only reason a
        // cell in this matrix is ever allowed to move.
        Assert.Equal(RuntimeState.Full, AtomKindRegistry.Get("stat.derived")!.SupportIn(RuntimeId.Battle));

        // stat.modify moved to Full with A18e (2026-08-28) -- BattleStatModifierLedger. spawn.entity/
        // board.action stay None: no A18 sub-module touches the Board attach point (H3's own boundary).
        Assert.Equal(RuntimeState.Full, AtomKindRegistry.Get("stat.modify")!.SupportIn(RuntimeId.Battle));
        Assert.Equal(RuntimeState.None, AtomKindRegistry.Get("spawn.entity")!.SupportIn(RuntimeId.Battle));
        Assert.Equal(RuntimeState.None, AtomKindRegistry.Get("board.action")!.SupportIn(RuntimeId.Battle));

        // status.apply moved to Full with A18d (2026-08-28) -- see the dedicated test above.
        Assert.Equal(RuntimeState.Full, AtomKindRegistry.Get("status.apply")!.SupportIn(RuntimeId.Battle));

        // Sim: shield.grant is one line of wiring away (SimEffectHost sets Bag.Status and Bag.UtcNow,
        // never Bag.ShieldGate), but until that line exists a bind would be a silent skip.
        Assert.Equal(RuntimeState.None, AtomKindRegistry.Get("shield.grant")!.SupportIn(RuntimeId.Sim));

        // stat.derived: LAWN opened 2026-08-30 (decisions.md "Derived-write lawn executor") because it
        // finally has a consumer -- `AtomDerivedSubsystem`, registered on the injector's ActorHub. The
        // rule this line has always enforced is unchanged: a runtime opens when, and only when, a
        // consumer exists for it.
        Assert.Equal(RuntimeState.Full, AtomKindRegistry.Get("stat.derived")!.SupportIn(RuntimeId.Lawn));
        // SIM still has none -- `SimEffectHost` has no derived consumer, so opening it on the strength
        // of the other two would re-create exactly what the quarantine was for.
        Assert.Equal(RuntimeState.None, AtomKindRegistry.Get("stat.derived")!.SupportIn(RuntimeId.Sim));
    }

    // The documented channel enum listed four keys effects cannot reach. Pin the real eight.
    [Fact]
    public void Primary_channels_are_the_real_eleven_since_E16()
    {
        // It was eight, and this test asserted the three intervals were ABSENT — correctly, because
        // they were cheat-document keys written straight to the Unity field, bypassing the modifier
        // bag. The documented enum listed them anyway, which is how the gap survived. E16 promoted
        // them into real composed channels, so the absent-assertions became wrong.
        Assert.Equal(11, AtomKindRegistry.PrimaryChannels.Length);
        Assert.Contains("defense", AtomKindRegistry.PrimaryChannels);
        Assert.Contains("arm1Max", AtomKindRegistry.PrimaryChannels);
        Assert.Contains("attackInterval", AtomKindRegistry.PrimaryChannels);
        Assert.Contains("produceInterval", AtomKindRegistry.PrimaryChannels);
        Assert.Contains("zombieSpeed", AtomKindRegistry.PrimaryChannels);
    }

    [Fact]
    public void The_registry_and_the_stat_layer_cannot_disagree_about_the_channel_list()
    {
        // Two hand-maintained copies of one list is how the documented nine came to differ from the
        // real eight in the first place.
        Assert.Equal(FusionRpg.Core.Stats.StatChannels.All, AtomKindRegistry.PrimaryChannels);
    }

    // G6: the registry declares PrimaryChannels and, until now, never read it — so `channel: "atkk"`
    // validated and then wrote nothing. Declared-but-unread is the same silent no-op as an unknown
    // leaf, which is the failure this module exists to refuse.
    [Fact]
    public void An_unknown_primary_channel_is_rejected()
    {
        var r = AtomKindRegistry.Validate("stat.modify",
            P(("channel", "atkk"), ("op", "flat"), ("amount", 10)));

        Assert.Equal(AtomRejectionReason.BadParamValue, r.Reason);
    }

    [Fact]
    public void Every_real_primary_channel_is_accepted()
    {
        foreach (var channel in AtomKindRegistry.PrimaryChannels)
            Assert.True(
                AtomKindRegistry.Validate("stat.modify",
                    P(("channel", channel), ("op", "flat"), ("amount", 10))).IsOk,
                channel);
    }
}
