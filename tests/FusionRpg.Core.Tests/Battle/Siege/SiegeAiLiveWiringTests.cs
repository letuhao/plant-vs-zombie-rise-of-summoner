using System.Linq;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.Battle.Siege;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (2026-09-07, session 5, owner-authorized): the FIRST live wiring of
/// `SiegeAiIntentSource` into a real `BattleEngine.Resolve` round, through the REAL atomic
/// action-phase loop `DeclareBasicAttack` uses (`Siege`/`Delve` both dispatch through it) — proving
/// `BattleRunState.DefaultAiIntentSource` genuinely reaches and changes a real targeting decision, not
/// merely that the two classes compile together. Before this task, `SiegeAiIntentSource` had zero
/// production callers anywhere in the game (confirmed repeat-edly this session); every real siege
/// used `StubIntentSource`'s purely geometric nearest-enemy fallback regardless of how "smart" the
/// scoring formulas underneath it were.
/// </summary>
public class SiegeAiLiveWiringTests
{
    static BattleActorSetup Attacker(string key) => new() { Key = key, Side = "squad", MaxHp = 1000 };

    /// <summary>A geometrically NEAR but statistically terrible target: real, shipped
    /// `CombatDodgeOmni` channel mod (the same mechanism `Dodge_mod_swings_fixed_battles` already
    /// proves swings real combat), pushed far enough that a hit is vanishingly unlikely, and full HP
    /// so neither "kill" nor "low HP" ever recommends it either.</summary>
    static BattleActorSetup NearButUnhittable(string key) => new()
    {
        Key = key, Side = "wave", MaxHp = 1000,
        ChannelMods = new[] { new BattleChannelMod(DerivedStatChannels.CombatDodgeOmni, 5000) },
    };

    /// <summary>A geometrically FAR but statistically perfect target: 1 HP (guaranteed kill,
    /// guaranteed lowest-missing-HP-fraction) and no defensive stat at all (ordinary hit chance).
    /// Every term `AiScoring.Score` sums — hit chance, kill, low HP, round -- agrees this is the
    /// better target; nothing conflicts, so the expected outcome is unambiguous.</summary>
    static BattleActorSetup FarButLethal(string key) => new() { Key = key, Side = "wave", MaxHp = 1, };

    static BoardState BoardWithNearAndFar()
    {
        var board = new BoardState(new GridSpec(20, 20));
        board.Place("squad:0", new GridPos(0, 0));
        board.Place("wave:near", new GridPos(1, 0));   // Chebyshev 1 -- the nearest possible cell
        board.Place("wave:far", new GridPos(19, 19));  // as far as this board allows
        return board;
    }

    static BattleSetup Setup() => new()
    {
        Squad = new[] { Attacker("squad:0") },
        Wave = new[] { NearButUnhittable("wave:near"), FarButLethal("wave:far") },
    };

    [Fact]
    public void Without_aiTuning_the_stub_fallback_attacks_the_geometrically_nearest_enemy()
    {
        // Baseline: proves the scenario itself is discriminating (StubIntentSource really does pick
        // by distance alone) before trusting what changes once aiTuning is supplied below.
        var report = BattleEngine.Resolve(Setup(), seed: 1, board: BoardWithNearAndFar());

        var far = report.Actors.Single(a => a.Key == "wave:far");
        Assert.Equal(1, far.HpRemaining);   // never targeted -- still at its starting 1 HP, not dead
    }

    [Fact]
    public void With_aiTuning_SiegeAiIntentSource_attacks_the_scored_better_enemy_instead()
    {
        // The real fix: identical setup, only `aiTuning` added -- BattleRunState.DefaultAiIntentSource
        // now exists and DeclareBasicAttack's own fallback tries it before StubIntentSource.
        var report = BattleEngine.Resolve(Setup(), seed: 1, board: BoardWithNearAndFar(),
            aiTuning: SiegeAiTuningForTest());

        var far = report.Actors.Single(a => a.Key == "wave:far");
        Assert.True(far.HpRemaining <= 0, $"expected the scored-better (guaranteed-kill) target to die; far.HpRemaining={far.HpRemaining}");
    }

    static AiTuning SiegeAiTuningForTest() => new(
        WeightHitChance: 70, WeightObjective: 50, WeightKill: 15, WeightLowHp: 10, WeightCannotCounter: 10,
        WeightRound: 1, WeightRisk: 120, StanceDefault: Stance.Guard, AutoResolveHandicapMilli: 1000,
        RetargetLatencyTicks: 0, AggressionRange: 2, MaxCandidatesScored: 32,
        ObjectiveReferenceDistanceCells: 10, ThreatRadiusCells: 4);
}
