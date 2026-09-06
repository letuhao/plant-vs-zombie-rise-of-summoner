using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.Battle.Siege;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md), task 17.4: the first live `IIntentSource`.
/// Mirrors `ActionSelectionTests`' own `StubIntentSource` coverage for steps 1/3/4/5 (this class
/// copies them verbatim), then adds real coverage for step 2 — scored target selection actually
/// discriminating on hit chance, existing damage, and counter-safety.
/// </summary>
public class SiegeAiIntentSourceTests
{
    sealed class FakeBattleView : IBattleView
    {
        public readonly List<string> Actors = new();
        public readonly Dictionary<string, int> Sides = new(StringComparer.Ordinal);
        public readonly Dictionary<string, GridPos?> Positions = new(StringComparer.Ordinal);
        public readonly Dictionary<string, EntityFacts> Facts = new(StringComparer.Ordinal);
        public readonly Dictionary<string, List<CompiledAction>> Held = new(StringComparer.Ordinal);
        public readonly Dictionary<string, ActorDerivedSnapshot?> Derived = new(StringComparer.Ordinal);
        public readonly Dictionary<string, string> Garrisoning = new(StringComparer.Ordinal);

        public FakeBattleView Add(string key, int side, GridPos? pos, int hpMilli = 1000,
            ActorDerivedSnapshot? derived = null, params CompiledAction[] held)
        {
            Actors.Add(key);
            Sides[key] = side;
            Positions[key] = pos;
            Facts[key] = new EntityFacts(side, 0, hpMilli, -1, pos?.Row ?? -1, pos?.Col ?? -1, false, false, 0);
            Held[key] = new List<CompiledAction>(held);
            Derived[key] = derived ?? ActorDerivedSnapshot.StubNeutral();
            return this;
        }

        public IReadOnlyList<string> LiveActorKeys => Actors;
        public int SideOf(string actorKey) => Sides[actorKey];
        public GridPos? PositionOf(string actorKey) => Positions[actorKey];
        public EntityFacts FactsOf(string actorKey) => Facts[actorKey];
        public IReadOnlyList<CompiledAction> HeldActionsOf(string actorKey) =>
            Held.TryGetValue(actorKey, out var list) ? list : Array.Empty<CompiledAction>();
        public ActorDerivedSnapshot? DerivedOf(string actorKey) =>
            Derived.TryGetValue(actorKey, out var d) ? d : null;
        public string? GarrisonedStructureKeyOf(string actorKey) =>
            Garrisoning.TryGetValue(actorKey, out var structureKey) ? structureKey : null;
    }

    static ActorDerivedSnapshot Snapshot(double accuracy = 0, double dodge = 0) =>
        ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, accuracy),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDodgeOmni, dodge),
        });

    static CompiledAction Action(
        string id, ActionTag[]? tags = null, int minRange = 0, int maxRange = int.MaxValue,
        ICompiledPredicate? condition = null) => new(
        id, ActionKind.Skill, 1, tags ?? Array.Empty<ActionTag>(), true, 1, false, false, "item.test",
        ActionEnvelope.NoOp with { ActionId = id }, new CompiledTargetSpec(true, Array.Empty<FusionRpg.Contracts.TargetSpec>()),
        minRange, maxRange, null, false, condition ?? PredicateCompiler.Always,
        Array.Empty<CompiledActionCost>(), Array.Empty<ActionScopeRow>());

    static SiegeAiIntentSource Ai(FakeBattleView view, Func<long, int>? roundOf = null) =>
        new(view, new CooldownLedger(), NoStanceHeld.Instance, AlwaysAffordable.Instance,
            SiegeTuningPolicy.Ai, roundOf ?? (tick => (int)tick));

    // ---- steps 1/3/4/5, copied from ActionSelectionTests' own StubIntentSource coverage ----------

    [Fact]
    public void NoHeldActionsPassesRatherThanHanging()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null);
        view.Add("wave:1", side: 1, pos: null, held: new[] { Action("act.attack") });

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);
        Assert.True(intent.IsNone);
    }

    [Fact]
    public void NoLiveEnemyPassesRatherThanHanging()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("wave:1", side: 0, pos: null, held: new[] { Action("act.attack") }); // same side

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);
        Assert.True(intent.IsNone);
    }

    [Fact]
    public void NothingUsableAndNoMovementActionPassesRatherThanHanging()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: new GridPos(0, 0), held: new[] { Action("act.attack", minRange: 5, maxRange: 5) });
        view.Add("wave:1", side: 1, pos: new GridPos(0, 1));

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);
        Assert.True(intent.IsNone);
    }

    [Fact]
    public void AUsableActionProducesAnIntentAgainstTheChosenTarget()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("wave:1", side: 1, pos: null);

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);

        Assert.False(intent.IsNone);
        Assert.Equal("act.attack", intent.ActionId);
        Assert.Equal("wave:1", intent.TargetKey);
    }

    [Fact]
    public void SelfWithNoDerivedSnapshotPassesRatherThanCrashing()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("wave:1", side: 1, pos: null);
        view.Derived["wave:0"] = null; // explicitly unreadable -- distinct from Add's own "unspecified" default

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);
        Assert.True(intent.IsNone); // cannot estimate a hit chance for an unreadable self -- no target
    }

    // ---- step 2: real scoring discriminates --------------------------------------------------------

    [Fact]
    public void Prefers_the_enemy_it_is_more_likely_to_hit()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, derived: Snapshot(accuracy: 500), held: new[] { Action("act.attack") });
        view.Add("hard-to-hit", side: 1, pos: null, derived: Snapshot(dodge: 500)); // big dodge -> low pHit
        view.Add("easy-to-hit", side: 1, pos: null, derived: Snapshot(dodge: -500)); // negative dodge -> high pHit

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);

        Assert.False(intent.IsNone);
        Assert.Equal("easy-to-hit", intent.TargetKey);
    }

    [Fact]
    public void Prefers_the_more_already_damaged_enemy_when_hit_chance_ties()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("full-hp", side: 1, pos: null, hpMilli: 1000);
        view.Add("half-hp", side: 1, pos: null, hpMilli: 500);

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);

        Assert.False(intent.IsNone);
        Assert.Equal("half-hp", intent.TargetKey); // WeightLowHp rewards the already-damaged target
    }

    [Fact]
    public void Prefers_a_target_that_cannot_counter_when_everything_else_ties()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("can-counter", side: 1, pos: null, held: new[] { Action("act.counter") });
        view.Add("cannot-counter", side: 1, pos: null); // no held actions at all

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);

        Assert.False(intent.IsNone);
        Assert.Equal("cannot-counter", intent.TargetKey);
    }

    [Fact]
    public void Same_board_same_decision_10000_times()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, derived: Snapshot(accuracy: 300), held: new[] { Action("act.attack") });
        for (var i = 0; i < 5; i++)
            view.Add($"zombie:{i}", side: 1, pos: null, hpMilli: 700 + i * 10, derived: Snapshot(dodge: i * 40));

        ActionIntent? first = null;
        for (var i = 0; i < 10_000; i++)
        {
            var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);
            first ??= intent;
            Assert.Equal(first.Value.TargetKey, intent.TargetKey);
            Assert.Equal(first.Value.ActionId, intent.ActionId);
        }
    }

    // ---- 17.8: retarget latency (§5.20 rule 3) -----------------------------------------------------

    static AiTuning WithLatency(long ticks) => SiegeTuningPolicy.Ai with { RetargetLatencyTicks = ticks };

    static SiegeAiIntentSource AiWithRetarget(FakeBattleView view, RetargetLedger ledger, AiTuning tuning) =>
        new(view, new CooldownLedger(), NoStanceHeld.Instance, AlwaysAffordable.Instance,
            tuning, tick => (int)tick, ledger);

    [Fact]
    public void Retarget_latency_is_authored_and_honoured()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("target-a", side: 1, pos: null, derived: Snapshot(dodge: -500)); // easy to hit
        view.Add("target-b", side: 1, pos: null, derived: Snapshot(dodge: 500));  // hard to hit
        var ledger = new RetargetLedger();
        var ai = AiWithRetarget(view, ledger, WithLatency(3));

        var first = ai.TryDeclare("wave:0", nowTick: 0);
        Assert.Equal("target-a", first.TargetKey); // objectively better at tick 0

        // target-b becomes objectively better -- simulates "the situation changed mid-window"
        view.Derived["target-a"] = Snapshot(dodge: 500);
        view.Derived["target-b"] = Snapshot(dodge: -500);

        var stillHeld = ai.TryDeclare("wave:0", nowTick: 1); // elapsed 1 < latency 3
        Assert.Equal("target-a", stillHeld.TargetKey); // held -- "keeps swinging at a target which just moved"

        var retargeted = ai.TryDeclare("wave:0", nowTick: 3); // elapsed 3, not < latency 3
        Assert.Equal("target-b", retargeted.TargetKey); // latency elapsed -- now rescores
    }

    [Fact]
    public void Retargeting_is_immediate_when_latency_is_the_shipped_default_of_zero()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("target-a", side: 1, pos: null, derived: Snapshot(dodge: -500));
        view.Add("target-b", side: 1, pos: null, derived: Snapshot(dodge: 500));
        var ledger = new RetargetLedger();
        var ai = AiWithRetarget(view, ledger, SiegeTuningPolicy.Ai); // RetargetLatencyTicks: 0

        Assert.Equal("target-a", ai.TryDeclare("wave:0", nowTick: 0).TargetKey);

        view.Derived["target-a"] = Snapshot(dodge: 500);
        view.Derived["target-b"] = Snapshot(dodge: -500);

        // latency 0 -- rescores on the very next tick, no holding at all
        Assert.Equal("target-b", ai.TryDeclare("wave:0", nowTick: 1).TargetKey);
    }

    [Fact]
    public void A_held_target_that_dies_is_abandoned_before_the_latency_elapses()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
        view.Add("target-a", side: 1, pos: null);
        var ledger = new RetargetLedger();
        var ai = AiWithRetarget(view, ledger, WithLatency(10));

        Assert.Equal("target-a", ai.TryDeclare("wave:0", nowTick: 0).TargetKey);

        view.Actors.Remove("target-a"); // simulates death -- no longer live
        view.Add("target-b", side: 1, pos: null);

        // well within the 10-tick latency window, but the held target no longer exists
        var intent = ai.TryDeclare("wave:0", nowTick: 1);
        Assert.False(intent.IsNone);
        Assert.Equal("target-b", intent.TargetKey);
    }

    [Fact]
    public void Same_stateful_decision_sequence_is_reproducible_10000_times()
    {
        ActionIntent? firstStep1 = null, firstStep2 = null;
        for (var i = 0; i < 10_000; i++)
        {
            var view = new FakeBattleView();
            view.Add("wave:0", side: 0, pos: null, held: new[] { Action("act.attack") });
            view.Add("target-a", side: 1, pos: null, derived: Snapshot(dodge: -500));
            view.Add("target-b", side: 1, pos: null, derived: Snapshot(dodge: 500));
            var ai = AiWithRetarget(view, new RetargetLedger(), WithLatency(3));

            var step1 = ai.TryDeclare("wave:0", nowTick: 0);
            firstStep1 ??= step1;
            Assert.Equal(firstStep1.Value.TargetKey, step1.TargetKey);

            view.Derived["target-a"] = Snapshot(dodge: 500);
            view.Derived["target-b"] = Snapshot(dodge: -500);
            var step2 = ai.TryDeclare("wave:0", nowTick: 1); // still held
            firstStep2 ??= step2;
            Assert.Equal(firstStep2.Value.TargetKey, step2.TargetKey);
        }
    }

    // ---- 17.9: the garrisoned emplacement's replacement vocabulary (§5.20 rule 5) ------------------

    [Fact]
    public void A_non_garrisoned_actor_still_moves_when_nothing_else_is_usable()
    {
        // act.move's OWN range must be restrictive too (matching act.attack's) so step 3's loop --
        // which tries every held action at ITS OWN declared range, movement included -- correctly
        // rejects it out of range; only step 4's hardcoded 0..int.MaxValue override can then let it
        // through, which is the actual behavior this test means to exercise.
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: new GridPos(0, 0),
            held: new[] { Action("act.attack", minRange: 5, maxRange: 5), Action("act.move", tags: new[] { ActionTag.Movement }, minRange: 5, maxRange: 5) });
        view.Add("wave:1", side: 1, pos: new GridPos(0, 1));

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);

        Assert.False(intent.IsNone);
        Assert.Equal("act.move", intent.ActionId); // confirms step 4 DOES fire absent a garrison
    }

    [Fact]
    public void A_garrisoned_occupant_never_abandons_post_to_chase_a_target()
    {
        var view = new FakeBattleView();
        view.Add("wave:0", side: 0, pos: new GridPos(0, 0),
            held: new[] { Action("act.attack", minRange: 5, maxRange: 5), Action("act.move", tags: new[] { ActionTag.Movement }, minRange: 5, maxRange: 5) });
        view.Add("wave:1", side: 1, pos: new GridPos(0, 1));
        view.Garrisoning["wave:0"] = "structure:turret-1";

        var intent = Ai(view).TryDeclare("wave:0", nowTick: 0);

        Assert.True(intent.IsNone, $"expected None, got ActionId={intent.ActionId} TargetKey={intent.TargetKey}");
    }

    [Fact]
    public void The_replacement_vocabulary_has_exactly_two_values_and_no_more() =>
        Assert.Equal(2, Enum.GetValues<EmplacementFireMode>().Length);
}

public class SiegeHitChanceTests
{
    static ActorDerivedSnapshot Snapshot(double accuracy = 0, double dodge = 0) =>
        ActorDerivedSnapshot.FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.CombatAccuracyOmni, accuracy),
            new KeyValuePair<string, double>(DerivedStatChannels.CombatDodgeOmni, dodge),
        });

    [Fact]
    public void Neutral_snapshots_give_exactly_half_hit_chance()
    {
        var chance = SiegeHitChance.EstimateMilli(Snapshot(), Snapshot());
        Assert.Equal(500, chance);
    }

    [Fact]
    public void Higher_attacker_accuracy_raises_hit_chance()
    {
        var baseline = SiegeHitChance.EstimateMilli(Snapshot(), Snapshot());
        var higher = SiegeHitChance.EstimateMilli(Snapshot(accuracy: 500), Snapshot());
        Assert.True(higher > baseline);
    }

    [Fact]
    public void Higher_defender_dodge_lowers_hit_chance()
    {
        var baseline = SiegeHitChance.EstimateMilli(Snapshot(), Snapshot());
        var lower = SiegeHitChance.EstimateMilli(Snapshot(), Snapshot(dodge: 500));
        Assert.True(lower < baseline);
    }

    [Fact]
    public void Result_is_always_clamped_to_a_real_per_mille_range()
    {
        var extreme = SiegeHitChance.EstimateMilli(Snapshot(accuracy: 1_000_000), Snapshot());
        Assert.InRange(extreme, 0, 1000);
        var extremeLow = SiegeHitChance.EstimateMilli(Snapshot(), Snapshot(dodge: 1_000_000));
        Assert.InRange(extremeLow, 0, 1000);
    }

    [Fact]
    public void Never_touches_a_float_the_result_is_a_plain_int()
    {
        int chance = SiegeHitChance.EstimateMilli(Snapshot(), Snapshot()); // compiles only if the return type is int
        Assert.Equal(500, chance);
    }
}
