using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Adoption;

/// <summary>
/// B1 — the pre-adoption traces. These are captured from the CURRENT engine and become the
/// fixtures T5 replays against, so drift localizes to a stream/phase/round instead of showing
/// up as an opaque golden hash diff. They must be captured before any engine change: once the
/// kernel lands there is nothing left to capture.
/// </summary>
public class PreAdoptionTraceTests
{
    static BattleActorSetup Actor(string key, string side, int level = 5,
        ElementTypeId? elem = null, int? maxHp = null, params string[] traits) => new()
    {
        Key = key, Side = side, SpeciesId = "trace-species", TypeId = 10_001, Level = level,
        ElementPrimary = elem,
        TraitIds = traits,
        MaxHp = maxHp ?? BattleRuleset.BaseHp(level),
        Atk = BattleRuleset.BaseAtk(level),
        Defense = BattleRuleset.BaseDefense(level)
    };

    [Fact]
    public void Trace_replays_identically_for_the_same_battle()
    {
        string Once()
        {
            var trace = new BattleTrace();
            BattleEngine.Resolve(BattleGoldenTests.CloseSetup(), 2002, trace);
            return trace.Digest;
        }

        Assert.Equal(Once(), Once());
    }

    [Fact]
    public void Trace_records_initiative_draws_in_actor_list_order()
    {
        // The hazard T5 names first: OrderBy evaluates its key selector once per element in
        // SOURCE order, so the draw sequence is "actors list order, filtered to Active".
        var setup = new BattleSetup
        {
            WaveId = "trace-init",
            Squad = new[] { Actor("squad:0", "squad"), Actor("squad:1", "squad") },
            Wave = new[] { Actor("wave:0", "wave") }
        };
        var trace = new BattleTrace();
        BattleEngine.Resolve(setup, 77, trace);

        var initiative = SeededRng.DeriveStream(77, "initiative");
        var expected = new[] { initiative.NextInt(1000), initiative.NextInt(1000), initiative.NextInt(1000) };

        var firstThree = trace.Draws("initiative").Take(3).ToList();
        Assert.Equal(expected, firstThree);
    }

    [Fact]
    public void Status_and_proc_streams_draw_nothing_in_battle()
    {
        // Asserted rather than assumed: spread needs a non-null board (battle passes null) and
        // procs only roll on the Grant path, which battle never uses. A parity trace that let
        // these drift would be silently wrong.
        var trace = new BattleTrace();
        BattleEngine.Resolve(BattleGoldenTests.CloseSetup(), 2002, trace);

        Assert.Empty(trace.Draws("status"));
        Assert.Empty(trace.Draws("proc"));
    }

    [Fact]
    public void Sub_round_period_under_delivers_today()
    {
        // Hazard 5: TickBudget defaults to 1 and the clock jumps a whole round, so a 250 ms
        // period fires ONCE per round instead of four times. 4000 ms / 250 ms = 16 true pulses;
        // today's engine delivers 4. T5 must PRESERVE this; T9 fixes it with a version bump.
        // Nothing covered this before — every existing caller uses PeriodMs: 1000.
        var tank = Actor("wave:0", "wave", maxHp: 1_000_000) with
        {
            ChannelMods = new[] { new BattleChannelMod(DerivedStatChannels.CombatDodgeOmni, 100_000) },
            InitialStatuses = new[] { new BattleStatusSpec("wither", -25, DurationMs: 4000, PeriodMs: 250) }
        };
        var report = BattleEngine.Resolve(new BattleSetup
        {
            WaveId = "trace-subround",
            Squad = new[] { Actor("squad:0", "squad", level: 1, maxHp: 1_000_000) },
            Wave = new[] { tank }
        }, 13);

        var hp = report.Actors.Single(a => a.Key == "wave:0").HpRemaining;
        Assert.Equal(1_000_000 - 100, hp);   // 4 pulses × 25, NOT 16 × 25
    }

    // Captured 2026-08-21 from the pre-adoption engine (RulesetVersion 2). T5 replays against
    // these; a diff localizes drift to a stream/phase/round before any hash is consulted.
    [Theory]
    [InlineData("stomp", 1001)]
    [InlineData("close", 2002)]
    [InlineData("wipe", 3003)]
    public void Golden_traces_are_captured(string name, ulong seed)
    {
        var setup = name switch
        {
            "stomp" => BattleGoldenTests.StompSetup(),
            "close" => BattleGoldenTests.CloseSetup(),
            _ => BattleGoldenTests.WipeSetup()
        };
        var trace = new BattleTrace();
        BattleEngine.Resolve(setup, seed, trace);

        // The fixture is the trace itself; this asserts it is non-trivial and self-consistent,
        // so a silently empty trace can never pass for a match.
        Assert.NotEmpty(trace.Draws("initiative"));
        Assert.NotEmpty(trace.Phases);
        Assert.Equal(PreAdoptionFixtures.LoadOrCapture(name, trace.Digest), trace.Digest);
    }
}
