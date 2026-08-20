using FusionRpg.Contracts;
using FusionRpg.Core.Events;
using Xunit;

namespace FusionRpg.Core.Tests.Events;

/// <summary>Budgeted drain — spec test groups 1 (ordering), 4 (budget), 5 (re-entrancy), 6 (session).</summary>
public class EventDrainTests
{
    /// <summary>Fake clock: each Process call advances time by a configurable cost.</summary>
    sealed class Harness
    {
        public readonly List<EffectEventDto> Seen = new();
        public long Now;
        public long CostPerProcess = 10;
        public Action<EffectEventDto>? OnProcess;
        public EventDrain Drain;

        public Harness()
        {
            Drain = new EventDrain(dto =>
            {
                Seen.Add(dto);
                Now += CostPerProcess;
                OnProcess?.Invoke(dto);
            }, () => Now);
        }

        public GameEventRec Rec(
            GameEventKind kind = GameEventKind.CombatHit,
            long target = 0xB,
            byte chainDepth = 0,
            long amount = -10,
            int pairId = 0)
            => new(kind, frame: 1, seq: Drain.NextSeq(), actorPtr: new IntPtr(0xA),
                targetPtr: new IntPtr(target), typeId: 1, targetTypeId: 2,
                side: GameEventSide.Zombie, amount: amount, hitCount: 1,
                chainDepth: chainDepth, sourceGrantIdx: -1,
                matchKeyIdx: Drain.InternMatchKey("m1"), pairId: pairId);
    }

    [Fact]
    public void Processes_all_within_budget()
    {
        var h = new Harness();
        h.Drain.Record(h.Rec(target: 1));
        h.Drain.Record(h.Rec(target: 2));
        var stats = h.Drain.Drain(budgetTicks: 1000);
        Assert.Equal(2, stats.Processed);
        Assert.Equal(0, stats.Carried);
        Assert.Equal(0, h.Drain.PendingCount);
    }

    [Fact]
    public void Budget_exhaustion_carries_remainder_in_order()
    {
        var h = new Harness { CostPerProcess = 50 };
        for (var i = 1; i <= 5; i++)
            h.Drain.Record(h.Rec(target: i));

        // Budget of 100 → ~2 records (first always processes; check runs after each).
        var stats = h.Drain.Drain(budgetTicks: 100);
        Assert.True(stats.Processed is >= 1 and < 5);
        Assert.Equal(5 - stats.Processed, h.Drain.PendingCount);

        var already = stats.Processed;
        var stats2 = h.Drain.Drain(budgetTicks: 100_000);
        Assert.Equal(5 - already, stats2.Processed);

        // Order across the two drains is the original FIFO by target ptr.
        for (var i = 0; i < 5; i++)
            Assert.Equal(new IntPtr(i + 1).ToString("X"), h.Seen[i].TargetPtr);
    }

    [Fact]
    public void Zero_budget_still_processes_one_record()
    {
        var h = new Harness { CostPerProcess = 50 };
        h.Drain.Record(h.Rec());
        h.Drain.Record(h.Rec(target: 0xC));
        var stats = h.Drain.Drain(budgetTicks: 0);
        Assert.Equal(1, stats.Processed); // forward progress is guaranteed
        Assert.Equal(1, stats.Carried);
    }

    [Fact]
    public void Dto_mapping_dealt_has_attacker_side_and_ptr_hex()
    {
        var h = new Harness();
        h.Drain.Record(h.Rec()); // target side = zombie → attacker side = plant
        h.Drain.Drain(1000);
        var dto = Assert.Single(h.Seen);
        Assert.Equal(EffectTriggers.OnDamageDealt, dto.Trigger);
        Assert.Equal("plant", dto.Side);
        Assert.Equal("A", dto.ActorPtr);
        Assert.Equal("B", dto.TargetPtr);
        Assert.Equal("m1", dto.MatchKey);
        Assert.Equal(1, dto.HitCount);
    }

    [Fact]
    public void Records_at_depth_limit_are_refused()
    {
        var h = new Harness();
        h.Drain.ChainDepthLimit = 3;
        Assert.True(h.Drain.Record(h.Rec(chainDepth: 2)));
        Assert.False(h.Drain.Record(h.Rec(chainDepth: 3)));
        Assert.Equal(1, h.Drain.DroppedByDepth);
    }

    [Fact]
    public void Depth_limit_clamps_to_hard_range()
    {
        var h = new Harness();
        h.Drain.ChainDepthLimit = 0;   // below floor
        Assert.Equal(1, h.Drain.ChainDepthLimit);
        h.Drain.ChainDepthLimit = 99;  // above ceiling — infinite chains must be impossible
        Assert.Equal(8, h.Drain.ChainDepthLimit);
    }

    [Fact]
    public void Reentrant_records_inherit_depth_and_process_same_pass()
    {
        var h = new Harness();
        var spawned = false;
        h.OnProcess = _ =>
        {
            if (spawned) return;
            spawned = true;
            // Simulates a Unity hook firing during action execution: stamps RecordDepth.
            h.Drain.Record(new GameEventRec(
                GameEventKind.ChainSynthetic, 1, h.Drain.NextSeq(),
                new IntPtr(0xA), new IntPtr(0xC), 1, 2, GameEventSide.Zombie,
                -5, 1, h.Drain.RecordDepth, -1, -1, 0));
        };

        h.Drain.Record(h.Rec());
        var stats = h.Drain.Drain(10_000);
        Assert.Equal(2, stats.Processed);          // same-pass generation
        Assert.Equal(2, stats.Generations);
        Assert.Equal(1, h.Seen[1].ChainDepth);     // inherited parent+1
    }

    [Fact]
    public void Generation_cap_carries_runaway_chains_to_next_frame()
    {
        var h = new Harness();
        h.Drain.GenerationCap = 2;
        h.OnProcess = _ =>
        {
            var depth = h.Drain.RecordDepth;
            h.Drain.Record(new GameEventRec(
                GameEventKind.ChainSynthetic, 1, h.Drain.NextSeq(),
                new IntPtr(0xA), new IntPtr(0xC), 1, 2, GameEventSide.Zombie,
                -5, 1, depth, -1, -1, 0));
        };

        h.Drain.Record(h.Rec());
        var stats = h.Drain.Drain(100_000);
        Assert.True(stats.Generations <= 3);
        Assert.True(h.Drain.PendingCount > 0);     // chain continues next frame, not unbounded now
    }

    [Fact]
    public void FlushForPtr_processes_only_that_ptr_and_keeps_order()
    {
        var h = new Harness();
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));
        h.Drain.Record(h.Rec(target: 0xB, amount: -3));

        h.Drain.FlushForPtr(new IntPtr(0xB));
        Assert.Equal(2, h.Seen.Count);
        Assert.All(h.Seen, dto => Assert.Equal("B", dto.TargetPtr));
        Assert.Equal(1, h.Drain.PendingCount);

        h.Drain.Drain(10_000);
        Assert.Equal("C", h.Seen[2].TargetPtr);
    }

    [Fact]
    public void FlushAllAndReset_drains_everything_and_clears_interning()
    {
        var h = new Harness();
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));
        h.Drain.FlushAllAndReset();
        Assert.Equal(2, h.Seen.Count);
        Assert.Equal(0, h.Drain.PendingCount);
        // Interner reset: new intern starts from index 0 again.
        Assert.Equal(0, h.Drain.InternMatchKey("m2"));
    }

    [Fact]
    public void Session_mode_ignores_budget_and_does_not_coalesce()
    {
        var h = new Harness { CostPerProcess = 1000 };
        h.Drain.SessionMode = true;
        h.Drain.Record(h.Rec(amount: -10));
        h.Drain.Record(h.Rec(amount: -15)); // same key — would merge outside sessions

        var stats = h.Drain.Drain(budgetTicks: 1);
        Assert.Equal(2, stats.Processed);   // event-for-event fidelity
        Assert.Equal(0, stats.Carried);
    }

    [Fact]
    public void Normal_mode_coalesces_same_key_before_processing()
    {
        var h = new Harness();
        h.Drain.Record(h.Rec(amount: -10));
        h.Drain.Record(h.Rec(amount: -15));
        var stats = h.Drain.Drain(10_000);
        Assert.Equal(1, stats.Processed);
        var dto = Assert.Single(h.Seen);
        Assert.Equal(2, dto.HitCount);
        Assert.Equal(-25, dto.Damage);
    }

    [Fact]
    public void Expensive_kind_defers_after_first_but_cheap_ones_flow()
    {
        var h = new Harness();
        // Teach the EMA that StatusHook is expensive: one pricey pass first.
        h.CostPerProcess = 900;
        h.Drain.Record(h.Rec(GameEventKind.StatusHook, target: 1));
        h.Drain.Drain(budgetTicks: 1000);
        h.Seen.Clear();
        h.CostPerProcess = 10;

        h.Drain.Record(h.Rec(GameEventKind.StatusHook, target: 2));
        h.Drain.Record(h.Rec(GameEventKind.StatusHook, target: 3));
        h.Drain.Record(h.Rec(GameEventKind.CombatHit, target: 4));
        var stats = h.Drain.Drain(budgetTicks: 1000);

        Assert.Equal(1, stats.ExpensiveDeferred);         // second StatusHook deferred
        Assert.Contains(h.Seen, d => d.TargetPtr == "4"); // cheap record behind it still ran
        Assert.True(h.Drain.PendingCount >= 1);
    }
}
