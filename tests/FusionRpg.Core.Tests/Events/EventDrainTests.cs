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
    public void Carry_is_never_recoalesced_with_new_records()
    {
        // v5 saturation fix: backlog must not re-merge every frame — a carried record's
        // HitCount stays fixed; new same-key records coalesce only among themselves.
        var h = new Harness { CostPerProcess = 50 };
        h.Drain.Record(h.Rec(target: 1));          // X — processed frame 1
        h.Drain.Record(h.Rec(target: 0xB));        // Y
        h.Drain.Record(h.Rec(target: 0xB));        // Y — merges with previous (HitCount 2)
        var s1 = h.Drain.Drain(budgetTicks: 1);
        Assert.Equal(1, s1.Processed);
        Assert.Equal(1, s1.Carried);               // merged Y(2) carried

        h.Drain.Record(h.Rec(target: 0xB));        // new Y
        h.Drain.Record(h.Rec(target: 0xB));        // new Y — merges into new-window Y(2)
        h.Drain.Drain(100_000);

        // Two distinct Y records processed (2+2), NOT one mega-record of 4.
        var yRecords = h.Seen.Where(d => d.TargetPtr == "B").ToList();
        Assert.Equal(2, yRecords.Count);
        Assert.All(yRecords, d => Assert.Equal(2, d.HitCount));
    }

    [Fact]
    public void Carry_survives_multiple_starved_frames_without_growth_work()
    {
        // Saturated regime: repeated zero-budget drains advance the cursor one record per
        // frame; pending shrinks monotonically, order preserved.
        var h = new Harness { CostPerProcess = 50 };
        for (var i = 1; i <= 4; i++)
            h.Drain.Record(h.Rec(target: i));

        for (var frame = 0; frame < 4; frame++)
            h.Drain.Drain(budgetTicks: 0);

        Assert.Equal(4, h.Seen.Count);
        Assert.Equal(new[] { "1", "2", "3", "4" }, h.Seen.Select(d => d.TargetPtr).ToArray());
        Assert.Equal(0, h.Drain.PendingCount);
    }

    [Fact]
    public void FlushForPtr_reaches_records_in_the_carry()
    {
        // v5: pending records live in carry + ring; the death barrier must scan both.
        var h = new Harness { CostPerProcess = 50 };
        h.Drain.Record(h.Rec(target: 1));
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));
        h.Drain.Drain(budgetTicks: 1);             // processes 1; B,C now in carry
        Assert.True(h.Drain.PendingCountFor(new IntPtr(0xB)) > 0);

        h.Drain.FlushForPtr(new IntPtr(0xB));
        Assert.Contains(h.Seen, d => d.TargetPtr == "B");
        Assert.Equal(0, h.Drain.PendingCountFor(new IntPtr(0xB)));
        Assert.True(h.Drain.PendingCountFor(new IntPtr(0xC)) > 0); // untouched, still pending
    }

    [Fact]
    public void Latency_frames_measured_when_frame_passed()
    {
        // v4 item 4: a record from frame 10 processed at frame 14 → latency 4.
        var h = new Harness();
        h.Drain.Record(h.Rec()); // Rec stamps frame: 1
        var stats = h.Drain.Drain(100_000, currentFrame: 5);
        Assert.Equal(4, stats.MaxLatencyFrames);
        Assert.True(stats.MaxRecordTicks > 0); // v4 item 2: costliest record visible
    }

    [Fact]
    public void Death_flush_budget_sheds_over_budget_with_counter()
    {
        // v4 item 1: per-death flush work is bounded; over-budget records for the dying
        // entity are shed and counted, others stay pending untouched.
        var h = new Harness { CostPerProcess = 100 };
        for (var i = 0; i < 5; i++)
            h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));

        var spent = h.Drain.FlushForPtr(new IntPtr(0xB), budgetTicks: 150);
        Assert.True(spent >= 150);
        Assert.True(h.Seen.Count is >= 1 and < 5);            // some processed within budget
        Assert.True(h.Drain.DroppedByDeathBudget >= 1);        // rest shed, counted
        Assert.Equal(1, h.Drain.PendingCount);                 // 0xC untouched
        Assert.Equal(0, h.Drain.PendingCountFor(new IntPtr(0xB)));
    }

    [Fact]
    public void Death_flush_unlimited_budget_processes_all()
    {
        var h = new Harness { CostPerProcess = 100 };
        for (var i = 0; i < 5; i++)
            h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.FlushForPtr(new IntPtr(0xB)); // default -1 = unlimited (legacy semantics)
        Assert.Equal(5, h.Seen.Count);
        Assert.Equal(0, h.Drain.DroppedByDeathBudget);
    }

    [Fact]
    public void Ptr_index_tracks_pending_through_record_drain_and_flush()
    {
        // v3 A3: derived index must match the ring after arbitrary operation sequences.
        var h = new Harness { CostPerProcess = 50 };
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));
        Assert.Equal(2, h.Drain.PendingCountFor(new IntPtr(0xB)));
        Assert.Equal(3, h.Drain.PendingCountFor(new IntPtr(0xA))); // actor on all three

        // Partial drain (budget forces carry): index must reflect only what remains pending.
        var stats = h.Drain.Drain(budgetTicks: 1);
        Assert.True(stats.Carried > 0);
        var remainingForB = h.Drain.PendingCountFor(new IntPtr(0xB));
        var remainingForC = h.Drain.PendingCountFor(new IntPtr(0xC));
        Assert.Equal(h.Drain.PendingCount, remainingForB + remainingForC); // each rec: 1 target key
        Assert.Equal(h.Drain.PendingCount, h.Drain.PendingCountFor(new IntPtr(0xA)));

        h.Drain.FlushForPtr(new IntPtr(0xB));
        Assert.Equal(0, h.Drain.PendingCountFor(new IntPtr(0xB)));

        h.Drain.Drain(100_000);
        Assert.Equal(0, h.Drain.PendingCountFor(new IntPtr(0xA)));
        Assert.Equal(0, h.Drain.PendingCount);
    }

    [Fact]
    public void FlushForPtr_with_nothing_pending_is_a_noop()
    {
        var h = new Harness();
        h.Drain.Record(h.Rec(target: 0xB));
        // Unknown ptr: O(1) early-out — nothing processed, nothing reordered.
        h.Drain.FlushForPtr(new IntPtr(0xDEAD));
        Assert.Empty(h.Seen);
        Assert.Equal(1, h.Drain.PendingCount);
    }

    [Fact]
    public void Reentrant_flush_during_drain_is_safe_no_duplicates_no_loss()
    {
        // A drained record can kill an entity → death hook → FlushForPtr mid-drain.
        // The nested flush must no-op; every record processes exactly once.
        var h = new Harness();
        h.OnProcess = _ =>
        {
            h.Drain.FlushForPtr(new IntPtr(0xC));   // nested — must not corrupt the pass
            h.Drain.FlushAllAndReset();              // nested — must not clear mid-drain state
        };
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));
        h.Drain.Drain(100_000);

        // C1: the nested FlushAllAndReset means the match ended mid-drain — the window
        // remainder belongs to the ended match and is discarded, never resurrected.
        Assert.Single(h.Seen);
        Assert.Equal(0, h.Drain.PendingCount);
    }

    [Fact]
    public void Nested_reset_discards_carry_and_resets_interning()
    {
        // C1 (review, repro-proven): match end fired by a processed record must not leak the
        // ended match's carried records or interner entries into the next match.
        var h = new Harness { CostPerProcess = 50 };
        var fired = false;
        h.OnProcess = _ =>
        {
            if (fired) return;
            fired = true;
            h.Drain.FlushAllAndReset(); // board.end mid-drain
        };
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.Record(h.Rec(target: 0xC));
        h.Drain.Record(h.Rec(target: 0xD));

        h.Drain.Drain(budgetTicks: 1); // budget would carry — reset must override
        Assert.Single(h.Seen);
        Assert.Equal(0, h.Drain.PendingCount);
        Assert.Equal(0, h.Drain.PendingCountFor(new IntPtr(0xA)));
        // Interner was reset — next intern starts from index 0.
        Assert.Equal(0, h.Drain.InternMatchKey("next-match"));
    }

    [Fact]
    public void Budget_carry_preserves_fifo_against_reentrant_records()
    {
        // I1 (review): carried window remainder is OLDER than records recorded during
        // processing — the carry must drain first next frame.
        var h = new Harness { CostPerProcess = 50 };
        var recorded = false;
        h.OnProcess = _ =>
        {
            if (recorded) return;
            recorded = true;
            h.Drain.Record(h.Rec(target: 0xE)); // re-entrant, newest
        };
        h.Drain.Record(h.Rec(target: 1));
        h.Drain.Record(h.Rec(target: 2));
        h.Drain.Record(h.Rec(target: 3));

        var s1 = h.Drain.Drain(budgetTicks: 1); // processes target 1, carries 2,3; ring holds E
        Assert.True(s1.Carried >= 2);
        h.Drain.Drain(100_000);

        var order = h.Seen.Select(d => d.TargetPtr).ToList();
        Assert.Equal(new[] { "1", "2", "3", "E" }, order);
    }

    [Fact]
    public void Session_mode_still_suppresses_paired_taken()
    {
        // I4 (review): pair suppression is causal, not a coalescing optimization — v1
        // suppressed the taken of one physical hit too.
        var h = new Harness();
        h.Drain.SessionMode = true;
        h.Drain.Record(new GameEventRec(GameEventKind.CombatHit, 1, h.Drain.NextSeq(),
            new IntPtr(0xA), new IntPtr(0xB), 1, 2, Core.Events.GameEventSide.Plant,
            -10, 1, 0, -1, -1, pairId: 5));
        h.Drain.Record(new GameEventRec(GameEventKind.PlantDamage, 1, h.Drain.NextSeq(),
            new IntPtr(0xA), new IntPtr(0xB), 1, 2, Core.Events.GameEventSide.Plant,
            -10, 1, 0, -1, -1, pairId: 5));

        h.Drain.Drain(0);
        var dto = Assert.Single(h.Seen);
        Assert.Equal(EffectTriggers.OnDamageDealt, dto.Trigger);
    }

    [Fact]
    public void Reentrant_flush_during_flush_is_safe()
    {
        var h = new Harness();
        var nested = false;
        h.OnProcess = _ =>
        {
            if (nested) return;
            nested = true;
            h.Drain.Record(h.Rec(target: 0xD));      // new record mid-flush
            h.Drain.FlushForPtr(new IntPtr(0xD));    // nested flush must no-op, record stays
        };
        h.Drain.Record(h.Rec(target: 0xB));
        h.Drain.FlushForPtr(new IntPtr(0xB));

        Assert.Single(h.Seen);                       // only the flushed ptr processed
        Assert.Equal(1, h.Drain.PendingCount);       // mid-flush record survived for next drain
        h.Drain.Drain(100_000);
        Assert.Equal(2, h.Seen.Count);
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
