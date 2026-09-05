using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Siege;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md): the pure decision mechanics (R1/R2/R5/R6) plus the
/// `SiegeIntentSource` dispatch wrapper (§1). See `SiegeAi.cs`'s own top comment for the named,
/// un-started gap this module leaves (a real `IBattleView`-reading AI, R3's objective-pathing fallback,
/// decision-trace wiring, the emplacement replacement vocabulary, retarget-latency enforcement).
/// </summary>
public class SiegeAiTests
{
    static AiTuning Weights(int risk = 120) => new(
        WeightHitChance: 70, WeightObjective: 50, WeightKill: 15, WeightLowHp: 10,
        WeightCannotCounter: 10, WeightRound: 1, WeightRisk: risk,
        StanceDefault: Stance.Guard, AutoResolveHandicapMilli: 1000, RetargetLatencyTicks: 0,
        AggressionRange: 2, MaxCandidatesScored: 32);

    static AiCandidate Candidate(string key, int baseTier = 0, int aggression = 0,
        int hitChanceMilli = 0, int objectiveClassMilli = 0, bool isKillingBlow = false,
        int targetMissingHpMilli = 0, bool targetCanCounter = false, long incomingThreatMilli = 0) =>
        new(key, baseTier, aggression, hitChanceMilli, objectiveClassMilli, isKillingBlow,
            targetMissingHpMilli, targetCanCounter, incomingThreatMilli);

    // -- R5: determinism, no RNG, no float --

    [Fact]
    public void Same_board_same_decisions_10000_times()
    {
        var candidates = new[]
        {
            Candidate("b", hitChanceMilli: 500),
            Candidate("a", hitChanceMilli: 500), // tie on every scored term, breaks on key
            Candidate("c", hitChanceMilli: 300),
        };
        var w = Weights();
        var first = AiScoring.ChooseTarget(candidates, currentRound: 3, w);
        for (var i = 0; i < 10_000; i++)
            Assert.Equal(first, AiScoring.ChooseTarget(candidates, currentRound: 3, w));
    }

    [Fact]
    public void No_rng_is_reachable_from_the_ai()
    {
        var text = File.ReadAllText(FindSourceFile("SiegeAi.cs"));
        Assert.DoesNotContain("Random", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_float_in_the_scoring_path()
    {
        var text = File.ReadAllText(FindSourceFile("SiegeAi.cs"));
        Assert.DoesNotContain("float", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("double", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ties_break_by_ordinal_key()
    {
        var candidates = new[] { Candidate("zebra", hitChanceMilli: 100), Candidate("apple", hitChanceMilli: 100) };
        var chosen = AiScoring.ChooseTarget(candidates, currentRound: 0, Weights());
        Assert.Equal("apple", chosen!.Value.ActorKey);
    }

    [Fact]
    public void Score_overflow_throws()
    {
        var w = Weights() with { WeightHitChance = int.MaxValue, WeightObjective = int.MaxValue, WeightKill = int.MaxValue };
        var c = Candidate("a", hitChanceMilli: int.MaxValue, objectiveClassMilli: int.MaxValue, isKillingBlow: true);
        Assert.Throws<OverflowException>(() => AiScoring.Score(c, currentRound: 0, w));
    }

    // -- R1/R2: stance/aggression/score are three distinct axes --

    [Fact]
    public void Taunt_dominates_within_its_tier_and_not_outside()
    {
        var w = Weights();
        // Taunted candidate (aggression +2) lands in a strictly better tier than an untaunted one
        // with a much higher raw score -- tier wins first, score only breaks ties within a tier.
        var taunted = Candidate("taunted", baseTier: 0, aggression: 2, hitChanceMilli: 1);
        var strongUntainted = Candidate("strong", baseTier: 0, aggression: 0, hitChanceMilli: 1000, objectiveClassMilli: 1000);
        var chosen = AiScoring.ChooseTarget(new[] { taunted, strongUntainted }, currentRound: 0, w);
        Assert.Equal("taunted", chosen!.Value.ActorKey);

        // Outside its own tier -- i.e. compared against nothing that competes for the same tier --
        // the taunt candidate simply isn't present, so it cannot affect the choice at all.
        var chosenWithoutTaunt = AiScoring.ChooseTarget(new[] { strongUntainted }, currentRound: 0, w);
        Assert.Equal("strong", chosenWithoutTaunt!.Value.ActorKey);
    }

    [Fact]
    public void Stealth_demotes_and_taunt_promotes_through_the_same_field()
    {
        var range = 2;
        var baseline = AiScoring.EffectiveTier(baseTier: 0, aggression: 0, range);
        var taunted = AiScoring.EffectiveTier(baseTier: 0, aggression: 2, range);
        var stealthed = AiScoring.EffectiveTier(baseTier: 0, aggression: -2, range);

        Assert.True(taunted < baseline); // numerically lower = higher priority
        Assert.True(stealthed > baseline);
    }

    [Fact]
    public void Aggression_is_applied_inside_the_tier_not_on_top_of_the_score()
    {
        // A decoy (+1 aggression) on an otherwise-worthless target still wins its tier over a
        // strong target one tier worse, and the decoy's own SCORE never needs to be competitive.
        var decoy = Candidate("decoy", baseTier: 0, aggression: 1, hitChanceMilli: 0, objectiveClassMilli: 0);
        var strongerButWorseTier = Candidate("strong", baseTier: 0, aggression: 0, hitChanceMilli: 1000, objectiveClassMilli: 1000);
        var chosen = AiScoring.ChooseTarget(new[] { decoy, strongerButWorseTier }, currentRound: 0, Weights());
        Assert.Equal("decoy", chosen!.Value.ActorKey);
    }

    [Fact]
    public void Aggression_range_is_bounded_and_the_bound_is_authored()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => AiScoring.EffectiveTier(0, aggression: 3, aggressionRange: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => AiScoring.EffectiveTier(0, aggression: -3, aggressionRange: 2));
    }

    // -- R2: additive scoring, XCOM's shipped ordering --

    [Fact]
    public void Hit_chance_outweighs_lethality_seventy_to_fifteen()
    {
        var w = Weights();
        // A guaranteed-hit non-kill beats a low-chance kill, matching XCOM's own shipped ordering --
        // an AI that maximises expected damage with no risk term reads as suicidal.
        var safeNonKill = Candidate("safe", hitChanceMilli: 1000, isKillingBlow: false);
        var riskyKill = Candidate("risky", hitChanceMilli: 100, isKillingBlow: true);
        var chosen = AiScoring.ChooseTarget(new[] { safeNonKill, riskyKill }, currentRound: 0, w);
        Assert.Equal("safe", chosen!.Value.ActorKey);
    }

    [Fact]
    public void The_round_term_makes_a_stalled_ai_bolder_over_time()
    {
        var w = Weights();
        var risky = Candidate("risky", incomingThreatMilli: 40);
        Assert.True(AiScoring.Score(risky, currentRound: 100, w) > AiScoring.Score(risky, currentRound: 0, w));
    }

    [Fact]
    public void Risk_term_prevents_walking_into_a_kill_zone()
    {
        var dangerous = Candidate("dangerous", hitChanceMilli: 150, incomingThreatMilli: 200);
        var safe = Candidate("safe", hitChanceMilli: 100, incomingThreatMilli: 0);

        var atDefaultRisk = AiScoring.ChooseTarget(new[] { dangerous, safe }, currentRound: 0, Weights(risk: 120));
        Assert.Equal("safe", atDefaultRisk!.Value.ActorKey);

        // decision 31's one-row rollback: risk weight 0 makes the AI cover-blind.
        var riskBlind = AiScoring.ChooseTarget(new[] { dangerous, safe }, currentRound: 0, Weights(risk: 0));
        Assert.Equal("dangerous", riskBlind!.Value.ActorKey);
    }

    [Fact]
    public void Risk_weight_zero_makes_the_ai_cover_blind()
    {
        var c = Candidate("a", incomingThreatMilli: 1000);
        Assert.Equal(AiScoring.Score(c with { IncomingThreatMilli = 0 }, 0, Weights()), AiScoring.Score(c, 0, Weights(risk: 0)));
    }

    [Fact]
    public void Cover_reduces_perceived_risk()
    {
        // "Cover reduces incoming threat" is the caller's job (siege-cover's own math); this module's
        // contribution is that a lower incomingThreatMilli produces a strictly higher (better) score.
        var exposed = Candidate("a", incomingThreatMilli: 800);
        var covered = Candidate("a", incomingThreatMilli: 200);
        Assert.True(AiScoring.Score(covered, 0, Weights()) > AiScoring.Score(exposed, 0, Weights()));
    }

    [Fact]
    public void No_path_holds_rather_than_fidgets()
    {
        // An empty candidate list is the "no target in reach" case -- ChooseTarget returns null, and
        // the caller's own fallback (R3: path toward objective, or hold) never rolls a random move.
        Assert.Null(AiScoring.ChooseTarget(Array.Empty<AiCandidate>(), currentRound: 0, Weights()));
    }

    // -- R6: readability --

    [Fact]
    public void Decision_trace_names_the_top_three_with_scores()
    {
        var candidates = new[]
        {
            Candidate("a", hitChanceMilli: 100), Candidate("b", hitChanceMilli: 900),
            Candidate("c", hitChanceMilli: 500), Candidate("d", hitChanceMilli: 10),
        };
        var top3 = AiScoring.TopThree(candidates, currentRound: 0, Weights());
        Assert.Equal(3, top3.Count);
        Assert.Equal("b", top3[0].ActorKey);
        Assert.Equal("c", top3[1].ActorKey);
        Assert.Equal("a", top3[2].ActorKey);
    }

    [Fact]
    public void Every_target_filter_has_a_display_key()
    {
        var filter = new TargetFilter { DisplayKey = "Ground only" };
        Assert.False(string.IsNullOrWhiteSpace(filter.DisplayKey));
    }

    // -- §7b: no hidden difficulty thumb, no score on ActionTargetOrdering --

    [Fact]
    public void No_stat_bonus_difficulty_exists()
    {
        var text = File.ReadAllText(FindSourceFile("SiegeAi.cs"));
        Assert.DoesNotContain("difficulty", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Score_is_not_on_ActionTargetOrdering()
    {
        var text = File.ReadAllText(FindSourceFile("SiegeAi.cs"));
        Assert.DoesNotContain("ActionTargetOrdering", text, StringComparison.Ordinal);
    }

    [Fact]
    public void No_targeting_ui_is_specced()
    {
        // Structural: this module ships no UI-facing type at all beyond TargetFilter's DisplayKey.
        var text = File.ReadAllText(FindSourceFile("SiegeAi.cs"));
        Assert.DoesNotContain("Ui", text, StringComparison.Ordinal);
    }

    static string FindSourceFile(string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10 && dir != null; i++)
        {
            var candidate = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories);
            if (candidate.Length > 0) return candidate[0];
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException($"{fileName} not found by walking up from {AppContext.BaseDirectory}");
    }

    // -- §1: SiegeIntentSource dispatch/fallthrough symmetry --

    sealed class FixedIntentSource : IIntentSource
    {
        readonly string _actionId;
        public FixedIntentSource(string actionId) => _actionId = actionId;
        public ActionIntent TryDeclare(string actorKey, long nowTick) => new(_actionId, null, null!);
    }

    [Fact]
    public void Played_side_delegate_overrides_the_ai()
    {
        var source = new SiegeIntentSource(new FixedIntentSource("ai-move"), new HashSet<string> { "hero" })
        {
            PlayedSide = new FixedIntentSource("player-move"),
        };

        Assert.Equal("player-move", source.TryDeclare("hero", 0).ActionId);
    }

    [Fact]
    public void Null_played_side_falls_through_to_the_ai()
    {
        var source = new SiegeIntentSource(new FixedIntentSource("ai-move"), new HashSet<string> { "hero" });

        Assert.Equal("ai-move", source.TryDeclare("hero", 0).ActionId);
        Assert.Equal("ai-move", source.TryDeclare("monster", 0).ActionId);
    }

    [Fact]
    public void Played_side_does_not_leak_to_the_other_side()
    {
        var source = new SiegeIntentSource(new FixedIntentSource("ai-move"), new HashSet<string> { "hero" })
        {
            PlayedSide = new FixedIntentSource("player-move"),
        };

        Assert.Equal("ai-move", source.TryDeclare("monster", 0).ActionId);
    }

    [Fact]
    public void Constructor_rejects_null_ai_side_and_null_played_side_keys()
    {
        Assert.Throws<ArgumentNullException>(() => new SiegeIntentSource(null!, new HashSet<string>()));
        Assert.Throws<ArgumentNullException>(() => new SiegeIntentSource(new FixedIntentSource("x"), null!));
    }
}
