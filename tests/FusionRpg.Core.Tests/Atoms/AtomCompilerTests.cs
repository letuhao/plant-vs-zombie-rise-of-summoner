using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Atoms;

/// <summary>
/// E7 acceptance. The compile/run split is the one architectural idea this program adds: compile what
/// Foundation can already express, hand the rest to the runner, and <b>never drop anything</b>.
///
/// <para>The load-bearing case is the subject trap. On <c>OnDamageDealt</c> the shipped overlay's
/// filters mean the <i>damaged</i> entity, so an atom with <c>subject: self</c> looks identical and
/// means the opposite. Compiling it would silently invert the filter — which is why it is a test.</para>
/// </summary>
public class AtomCompilerTests
{
    static AtomRow Atom(
        string family,
        string kindId = "stat.modify",
        string paramsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":45}",
        string whenJson = "{}",
        string? icdKey = null) => new()
    {
        AtomId = AtomRow.DeriveId(family, "", 1),
        KindId = kindId,
        FamilyId = family,
        Variant = "",
        Tier = 1,
        Name = family,
        ParamsJson = paramsJson,
        WhenJson = whenJson,
        IcdKey = icdKey,
    };

    static AtomRow Strike(string whenJson, string paramsJson = "{\"amount\":-120}", string? icdKey = null) =>
        Atom("atom.strike", "resource.delta", paramsJson, whenJson, icdKey);

    static string Leaf(string leaf, string subject, string value) =>
        "{\"leaf\":\"" + leaf + "\",\"subject\":\"" + subject + "\",\"value\":" + value + "}";

    static string When(string trigger, string? predicate = null, int? chance = null, int? icdMs = null)
    {
        var parts = new List<string> { "\"trigger\":\"" + trigger + "\"" };
        if (chance is { } c) parts.Add("\"chance\":" + c);
        if (icdMs is { } i) parts.Add("\"icd_ms\":" + i);
        if (predicate is not null) parts.Add("\"predicate\":" + predicate);
        return "{" + string.Join(",", parts) + "}";
    }

    static CompiledCatalog Compile(params AtomRow[] atoms) =>
        AtomCompiler.Compile(atoms, RuntimeId.Lawn, catalogRevision: 1);

    static AtomPath PathOf(AtomRow atom) =>
        Compilability.Classify(atom, RuntimeId.Lawn).Path;

    // ---- the classification rules ----------------------------------------------------------------

    [Fact]
    public void An_atom_with_no_predicate_and_an_opcode_kind_compiles()
    {
        Assert.Equal(AtomPath.Compiled, PathOf(Strike(When(EffectTriggers.OnDamageDealt))));
    }

    [Fact]
    public void Side_and_typeId_with_subject_target_on_OnDamageDealt_compiles()
    {
        var predicate = "{\"op\":\"and\",\"children\":["
                        + Leaf("sideIs", "target", "\"zombie\"") + ","
                        + Leaf("typeIdIs", "target", "12") + "]}";

        Assert.Equal(AtomPath.Compiled, PathOf(Strike(When(EffectTriggers.OnDamageDealt, predicate))));
    }

    [Fact]
    public void The_same_predicate_with_subject_self_goes_to_the_runner()
    {
        // Identical shape, opposite meaning. `filters.side` on OnDamageDealt refers to the DAMAGED
        // entity, so compiling this would invert the filter and nothing would look wrong.
        var predicate = "{\"op\":\"and\",\"children\":[" + Leaf("sideIs", "self", "\"zombie\"") + "]}";

        Assert.Equal(AtomPath.Runner, PathOf(Strike(When(EffectTriggers.OnDamageDealt, predicate))));
    }

    [Fact]
    public void On_a_trigger_that_does_not_invert_subject_self_is_the_compilable_one()
    {
        var self = "{\"op\":\"and\",\"children\":[" + Leaf("sideIs", "self", "\"plant\"") + "]}";
        var target = "{\"op\":\"and\",\"children\":[" + Leaf("sideIs", "target", "\"plant\"") + "]}";

        Assert.Equal(AtomPath.Compiled, PathOf(Strike(When(EffectTriggers.OnDeath, self))));
        Assert.Equal(AtomPath.Runner, PathOf(Strike(When(EffectTriggers.OnDeath, target))));
    }

    [Theory]
    [InlineData("or")]
    [InlineData("not")]
    public void Alternation_and_negation_go_to_the_runner(string op)
    {
        // A grant overlay's filters are a conjunction and nothing else.
        var predicate = "{\"op\":\"" + op + "\",\"children\":[" + Leaf("sideIs", "target", "\"zombie\"") + "]}";

        Assert.Equal(AtomPath.Runner, PathOf(Strike(When(EffectTriggers.OnDamageDealt, predicate))));
    }

    [Fact]
    public void A_leaf_with_no_legacy_filter_goes_to_the_runner()
    {
        var predicate = "{\"op\":\"and\",\"children\":[" + Leaf("hpBelowMilli", "target", "500") + "]}";

        Assert.Equal(AtomPath.Runner, PathOf(Strike(When(EffectTriggers.OnDamageDealt, predicate))));
    }

    [Fact]
    public void An_OnApply_range_goes_to_the_runner()
    {
        var atom = Strike(When(EffectTriggers.OnDamageDealt),
            "{\"amount\":{\"min\":100,\"max\":200,\"roll\":\"onApply\"}}");

        Assert.Equal(AtomPath.Runner, PathOf(atom));
    }

    [Fact]
    public void An_OnApply_value_whose_bounds_are_equal_is_just_Fixed_and_compiles()
    {
        var atom = Strike(When(EffectTriggers.OnDamageDealt),
            "{\"amount\":{\"min\":150,\"max\":150,\"roll\":\"onApply\"}}");

        Assert.Equal(AtomPath.Compiled, PathOf(atom));
    }

    [Fact]
    public void Icd_alone_does_not_force_the_runner()
    {
        // EffectBag already enforces grant ICD on the compiled path. The runner owns ICD only for
        // atoms it already owns for some other reason.
        Assert.Equal(AtomPath.Compiled, PathOf(Strike(When(EffectTriggers.OnDamageDealt, icdMs: 500))));
    }

    [Fact]
    public void Per_binding_state_forces_the_runner()
    {
        var atom = Atom("atom.sun", "resource.economy",
            "{\"currency\":\"sun\",\"op\":\"add\",\"amount\":25,\"capPerMatch\":3}",
            When(EffectTriggers.OnDeath));

        Assert.Equal(AtomPath.Runner, PathOf(atom));
    }

    [Fact]
    public void A_kind_with_no_consumer_in_the_runtime_is_rejected_not_dropped()
    {
        var verdict = Compilability.Classify(
            Atom("atom.mow", "board.action", "{\"op\":\"cherry\"}", When(EffectTriggers.OnDeath)),
            RuntimeId.Battle);

        Assert.Equal(AtomPath.Rejected, verdict.Path);
        Assert.Equal(AtomRejectionReason.RuntimeUnsupported, verdict.Rejection);
    }

    [Fact]
    public void A_quarantined_kind_is_rejected_everywhere()
    {
        var atom = Atom("atom.power", "stat.derived",
            "{\"channel\":\"combat.power.fire\",\"op\":\"flat\",\"amount\":5}");

        Assert.Equal(AtomPath.Rejected, PathOf(atom));
    }

    // ---- emission ---------------------------------------------------------------------------------

    [Fact]
    public void A_compiled_atom_emits_a_def_and_a_grant_that_points_at_it()
    {
        var result = Compile(Strike(When(EffectTriggers.OnDamageDealt, chance: 250, icdMs: 500)));

        var def = Assert.Single(result.Defs);
        var grant = Assert.Single(result.Compiled);

        Assert.Equal(def.EffectId, grant.EffectId);
        Assert.Equal(EffectTypes.Triggered, def.EffectType);
        Assert.Equal(new[] { EffectTriggers.OnDamageDealt }, def.Triggers);
        Assert.Equal(EffectActions.ApplyResourceDelta, Assert.Single(def.Actions).Action);

        // Chance rides the overlay as a fraction, exactly as a hand-authored grant carries it.
        Assert.Equal(0.25, Assert.IsType<double>(grant.Overlay!["chance"]));
        Assert.Equal(500, grant.Overlay["icd_ms"]);
    }

    [Fact]
    public void A_triggerless_permanent_modifier_is_emitted_as_Passive()
    {
        // EffectType defaults to Triggered, and the bag fires the lifecycle pair only when the def is
        // Passive or its triggers contain OnGranted. Emitting the default here means the modifier
        // never applies at all.
        var def = Assert.Single(Compile(Atom("atom.vitality")).Defs);

        Assert.Equal(EffectTypes.Passive, def.EffectType);
        Assert.Empty(def.Triggers);
    }

    [Fact]
    public void An_icd_group_becomes_one_def_carrying_the_union_of_its_triggers()
    {
        // This is what keeps fx.shield_grant's single ICD clock after it splits into three atoms.
        var a = Strike(When(EffectTriggers.OnDamageDealt), icdKey: "shield-grant") with
        { AtomId = "atom.shield-a.t1", FamilyId = "atom.shield-a" };
        var b = Strike(When(EffectTriggers.OnTimer), icdKey: "shield-grant") with
        { AtomId = "atom.shield-b.t1", FamilyId = "atom.shield-b" };
        var c = Strike(When(EffectTriggers.OnSpawn), icdKey: "shield-grant") with
        { AtomId = "atom.shield-c.t1", FamilyId = "atom.shield-c" };

        var result = Compile(a, b, c);

        var def = Assert.Single(result.Defs);
        Assert.Single(result.Compiled);
        Assert.Equal(
            new[] { EffectTriggers.OnDamageDealt, EffectTriggers.OnSpawn, EffectTriggers.OnTimer },
            def.Triggers);
        Assert.Equal(3, def.Actions.Count);
    }

    [Fact]
    public void One_runner_member_sends_the_whole_icd_group_to_the_runner()
    {
        // Splitting the group would split the clock they were grouped to share — a behaviour change,
        // not an optimisation.
        var simple = Strike(When(EffectTriggers.OnDamageDealt), icdKey: "shared") with
        { AtomId = "atom.simple.t1", FamilyId = "atom.simple" };
        var rolling = Strike(When(EffectTriggers.OnTimer),
            "{\"amount\":{\"min\":1,\"max\":9,\"roll\":\"onApply\"}}", icdKey: "shared") with
        { AtomId = "atom.rolling.t1", FamilyId = "atom.rolling" };

        var result = Compile(simple, rolling);

        Assert.Empty(result.Compiled);
        Assert.Equal(2, result.Runtime.Count);
        Assert.All(result.Runtime, r => Assert.Equal("shared", r.IcdKey));
    }

    [Fact]
    public void A_runner_entry_carries_a_compiled_predicate_and_its_bounds()
    {
        var predicate = "{\"op\":\"and\",\"children\":[" + Leaf("hpBelowMilli", "target", "500") + "]}";
        var atom = Strike(When(EffectTriggers.OnDamageDealt, predicate, chance: 250),
            "{\"amount\":{\"min\":100,\"max\":200,\"roll\":\"onApply\"}}");

        var entry = Assert.Single(Compile(atom).Runtime);

        Assert.Equal(250, entry.ChanceMilli);
        Assert.False(entry.IsUnconditional);
        Assert.Equal(new ValueBounds(100, 200, RollPolicy.OnApply), entry.Values["amount"]);
    }

    [Fact]
    public void Curve_scaled_bounds_are_pre_multiplied_so_no_curve_row_travels()
    {
        // D9: the injector cannot scale a value, because E19 forbids curve rows from travelling.
        CurveTable.TryCreate("curve.atk.level", CurveInput.Level,
            new[] { new CurvePoint(1, 1000), new CurvePoint(10, 2000) }, out var curve);

        var atom = Strike(When(EffectTriggers.OnDamageDealt),
            "{\"amount\":{\"min\":100,\"max\":200,\"roll\":\"onApply\",\"curve\":\"curve.atk.level\"}}");

        var result = AtomCompiler.Compile(new[] { atom }, RuntimeId.Lawn, 1,
            curves: _ => curve, ownerLevel: 10);

        var bounds = Assert.Single(result.Runtime).Values["amount"];
        Assert.Equal(new ValueBounds(200, 400, RollPolicy.OnApply), bounds);
    }

    // ---- completeness and determinism ---------------------------------------------------------------

    [Fact]
    public void Every_atom_lands_in_exactly_one_bucket()
    {
        var atoms = new[]
        {
            Atom("atom.vitality"),
            Strike(When(EffectTriggers.OnDamageDealt)),
            Strike(When(EffectTriggers.OnTimer), "{\"amount\":{\"min\":1,\"max\":9,\"roll\":\"onApply\"}}")
                with { AtomId = "atom.roll.t1", FamilyId = "atom.roll" },
            Atom("atom.derived", "stat.derived", "{\"channel\":\"combat.power.fire\",\"op\":\"flat\",\"amount\":5}"),
        };

        var result = Compile(atoms);

        Assert.Equal(
            atoms.Select(a => a.AtomId).OrderBy(i => i, StringComparer.Ordinal),
            result.AllAtomIds.OrderBy(i => i, StringComparer.Ordinal));
    }

    [Fact]
    public void The_same_revision_bakes_to_the_same_shape()
    {
        var atoms = new[] { Atom("atom.vitality"), Strike(When(EffectTriggers.OnDamageDealt)) };

        var a = Compile(atoms);
        var b = Compile(atoms);

        Assert.Equal(a.Defs.Select(d => d.EffectId), b.Defs.Select(d => d.EffectId));
        Assert.Equal(a.Compiled.Select(g => g.GrantId), b.Compiled.Select(g => g.GrantId));
        Assert.Equal(a.CompiledAtomIds, b.CompiledAtomIds);
    }

    [Fact]
    public void Enumeration_order_does_not_change_the_bake()
    {
        var atoms = new[]
        {
            Atom("atom.vitality"), Atom("atom.might"), Atom("atom.alacrity"),
        };

        var forward = Compile(atoms);
        var backward = Compile(atoms.Reverse().ToArray());

        Assert.Equal(forward.Defs.Select(d => d.EffectId), backward.Defs.Select(d => d.EffectId));
    }
}
