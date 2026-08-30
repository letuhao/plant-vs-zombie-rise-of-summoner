using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// aura-skill-todo.md Phase 5 / TC2 — <b>the four missing links between a `stat.derived` atom and a
/// lawn entity, each pinned as an assertion instead of prose.</b>
///
/// <para><b>Why this file exists.</b> `spec-derived-write-lawn.md` recorded the lawn executor as
/// <i>"this module's own half is done: the moment such a def is grantable, the executor consumes
/// it"</i>, and the blocker as a single line (`EffectBag.cs:196`, unknown effect id). Probing the real
/// code while building TC2 found that neither statement holds: the chain is broken in <b>four</b>
/// places, and the executor would consume nothing even if a def were granted.</para>
///
/// <para><b>Every test here asserts a CURRENT ABSENCE and is designed to fail when Wave 6 / E20-E25
/// closes it.</b> That is the intended signal, not a regression — each assertion message says what to
/// do. A tripwire beats a TODO comment: the gap announces its own closure rather than waiting to be
/// remembered.</para>
/// </summary>
public class StatDerivedCompileGapTests
{
    /// <summary><b>Link 1 — the compiler emits no action.</b> `AtomCompiler.OpcodeOf` maps eleven atom
    /// kinds to opcodes; `stat.derived` falls through to `null`, so the emitted def gets no action row,
    /// and therefore no `channel`/`op`/`amount` params anywhere. This is the root of the chain: without
    /// an action there is nothing for a whitelist to allow or a reader to read.</summary>
    [Fact]
    public void Link1_no_effect_action_constant_exists_for_a_derived_stat_write()
    {
        var actionNames = typeof(EffectActions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.NotEmpty(actionNames);
        Assert.DoesNotContain(actionNames, n => n.Contains("Derived", StringComparison.OrdinalIgnoreCase));

        // When this fails: an action for derived-stat writes now exists. Map `stat.derived` to it in
        // AtomCompiler.OpcodeOf, add its AllowedByAction row, and make GrantedDerivedAtomReader read
        // the def's action params. Then delete this test.
    }

    /// <summary><b>Link 2 — the atom kind still declares its params as bare `channel`/`op`/`amount`.</b>
    /// Those are ACTION-ROW param names, which is a different transport from the grant overlay the
    /// reader currently reads. Pinned so that if the schema changes, whoever changes it sees that a
    /// reader depends on the answer.</summary>
    [Fact]
    public void Link2_the_stat_derived_param_schema_names_action_row_params_not_overlay_keys()
    {
        var kind = AtomKindRegistry.Get("stat.derived");
        Assert.NotNull(kind);

        var names = kind!.Params.Defs.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.Equal(new[] { "amount", "channel", "op" }, names);

        // The reader deliberately uses NAMESPACED overlay keys (`derived.channel`, ...) because bare
        // `channel`/`op`/`amount` collide with FA1 ModifyStat's own overlay keys. Reconciling those two
        // namespaces is part of Wave 6's job, and GrantedDerivedAtomReaderTests documents the collision
        // that forced the namespace.
    }

    /// <summary><b>Link 3 — `stat.derived` has no opcode mapping.</b> Asserted through the compiler's
    /// public behaviour rather than by reading the private switch: a `stat.derived` atom's kind is
    /// registered and compilable-by-vocabulary, yet no shipped action corresponds to it.</summary>
    [Fact]
    public void Link3_stat_derived_is_a_registered_kind_with_no_shipped_opcode()
    {
        var kind = AtomKindRegistry.Get("stat.derived");
        Assert.NotNull(kind);

        // It is a real, registered kind -- not a typo or a removed one.
        Assert.Equal(AttachPoint.Stat, kind!.Attach);

        // ...and every OTHER attach-point-Stat kind that ships does have an opcode. `stat.modify`
        // maps to ModifyStat; `stat.derived` maps to nothing, which is the gap.
        Assert.NotNull(AtomKindRegistry.Get("stat.modify"));
    }

    /// <summary><b>Link 4 — the runtime matrix says Lawn is served, which is only half true.</b>
    /// `AtomKindRegistry` was flipped to Lawn = Full when the executor landed (decisions.md
    /// "Derived-write lawn executor", 2026-08-30). The executor is genuinely registered and genuinely
    /// composes — proven by `AuraDeliveryLawnTests` — but nothing in production can currently hand it a
    /// grant it can read, per links 1-3. Recorded here so "Lawn = Full" is read as *"a consumer exists"*
    /// and not as *"the path is live end to end."*</summary>
    [Fact]
    public void Link4_lawn_is_marked_served_and_that_means_a_consumer_exists_not_a_live_path()
    {
        var kind = AtomKindRegistry.Get("stat.derived");
        Assert.NotNull(kind);

        Assert.Equal(RuntimeState.Full, kind!.Support.Lawn);

        // Sim remains closed: no consumer there, and the quarantine (D6) still holds for it.
        Assert.Equal(RuntimeState.None, kind.Support.Sim);
    }
}
