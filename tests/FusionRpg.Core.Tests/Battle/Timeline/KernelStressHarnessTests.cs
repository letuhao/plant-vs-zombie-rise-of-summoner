using System.Diagnostics;
using FusionRpg.Core.Battle.Timeline;
using Xunit;
using Xunit.Abstractions;

namespace FusionRpg.Core.Tests.Battle.Timeline;

/// <summary>
/// P2 — the measurement gate in front of the injector drive (T13).
///
/// The kernel is going to tick inside the Unity frame, where its failure mode is stutter that no
/// unit test sees. This harness shapes a 200-entity, 60 fps stress board offline and measures what
/// the kernel costs per frame. **If it cannot hold its slice here it will not hold it in the
/// frame** — and learning that costs a harness rather than an injector integration.
///
/// Budgets inherited from perf-probe-plan.md §0: the injector gets ≤2 ms/frame at 200+ entities
/// with no gen2 GC during a level, and the kernel's share of that is ≤0.15 ms/frame.
///
/// On timing assertions: the pass/fail gate is <b>allocation</b>, which is deterministic. Wall
/// clock is reported for the record but asserted only against a deliberately loose ceiling —
/// a tight timing assertion in CI measures the build agent, and the first flake gets it muted.
/// </summary>
public class KernelStressHarnessTests
{
    const int Entities = 200;
    const int Frames = 600;              // 10 seconds at 60 fps
    const double KernelSliceMs = 0.15;   // the kernel's share of the injector budget

    readonly ITestOutputHelper _out;
    public KernelStressHarnessTests(ITestOutputHelper output) => _out = output;

    /// <summary>
    /// One frame of a stress board: advance the clock, drain what is due, and re-arm each actor
    /// that acted. That is the real shape — a scheduler's per-frame cost is dominated by drain
    /// plus re-arm, not by scheduling in bulk.
    /// </summary>
    static (double TotalMs, long Bytes, int Drained) RunFrames(int frames, bool measureAllocation)
    {
        var q = new EventQueue(expectedEvents: Entities * 2);
        var clock = new SimulationClock();
        var advance = new FixedIncrementAdvance(1000, 60);
        var buffer = new List<ScheduledEvent>(Entities * 2);

        // The full per-frame path, not just the queue. An earlier version measured only clock +
        // queue and reported the result as "the kernel's cost", which understated it: a real
        // frame also drives each acting actor through its state machine and the slot pool.
        var machines = new ActorTurnMachine[Entities];
        var keys = new string[Entities];
        for (var i = 0; i < Entities; i++)
        {
            keys[i] = "actor:" + i;             // hoisted: building keys per frame would allocate
            machines[i] = new ActorTurnMachine(keys[i]);
        }

        var slots = new ActionSlots(Entities);

        void DriveActor(long tag)
        {
            var m = machines[tag];
            if (!m.CanAct) return;
            // Ready -> Committed -> Resolving -> Recovering -> Charging, taking and releasing a slot.
            if (m.State != TurnState.Charging) return;
            m.TransitionTo(TurnState.Ready);
            if (!slots.TryAcquire(keys[tag], "squad")) { m.TransitionTo(TurnState.Charging); return; }
            m.TransitionTo(TurnState.Committed);
            m.TransitionTo(TurnState.Resolving);
            m.TransitionTo(TurnState.Recovering);
            slots.Release(keys[tag]);
            m.TransitionTo(TurnState.Charging);
        }

        // Arm every actor with a staggered first event.
        for (var i = 0; i < Entities; i++)
            q.Schedule(10 + (i * 7 % 900), "actor", 0, i);

        // Warm up JIT and let the collections settle before measuring anything.
        for (var w = 0; w < 60; w++)
        {
            clock.TryAdvance(advance, q, frames: 1);
            buffer.Clear();
            q.PopDue(clock.Now, buffer);
            foreach (var e in buffer)
            {
                DriveActor(e.Tag);
                q.Schedule(clock.Now + 200 + (e.Tag * 13 % 700), "actor", 0, e.Tag);
            }
        }

        // The instrument is allocated BEFORE the snapshot. Creating it after would charge its own
        // 40 bytes to the kernel — which is precisely what the first run of this harness reported,
        // and a useful demonstration that the gate is sensitive down to a single small object.
        var sw = new Stopwatch();

        long before = 0;
        if (measureAllocation)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            before = GC.GetAllocatedBytesForCurrentThread();
        }

        var drained = 0;
        sw.Restart();
        for (var f = 0; f < frames; f++)
        {
            clock.TryAdvance(advance, q, frames: 1);
            buffer.Clear();
            drained += q.PopDue(clock.Now, buffer);

            // Drive the actor through its turn and re-arm it, as a live board would.
            foreach (var e in buffer)
            {
                DriveActor(e.Tag);
                q.Schedule(clock.Now + 200 + (e.Tag * 13 % 700), "actor", 0, e.Tag);
            }
        }

        sw.Stop();
        var bytes = measureAllocation ? GC.GetAllocatedBytesForCurrentThread() - before : 0;
        return (sw.Elapsed.TotalMilliseconds, bytes, drained);
    }

    [Fact]
    public void A_two_hundred_entity_board_costs_nothing_per_frame_in_allocation()
    {
        // THE GATE. Allocation is the deterministic half and the one that actually causes stutter:
        // "no gen2 GC during a level" is already a stated non-goal in the perf plan.
        var (_, bytes, drained) = RunFrames(Frames, measureAllocation: true);

        _out.WriteLine($"{Entities} entities, {Frames} frames: {drained} events drained, {bytes} bytes allocated");
        Assert.True(drained > 0, "harness drained nothing — it is not exercising the kernel");
        Assert.True(bytes == 0,
            $"steady-state stress allocated {bytes} bytes over {Frames} frames; budget is 0");
    }

    [Fact]
    public void A_two_hundred_entity_board_holds_the_kernel_frame_slice()
    {
        // Reported precisely, asserted loosely: the number below is the useful output, and the
        // assertion exists only to catch a catastrophic regression (an accidental O(n^2), say)
        // rather than to police a few microseconds on a shared build agent.
        var (totalMs, _, drained) = RunFrames(Frames, measureAllocation: false);
        var perFrame = totalMs / Frames;

        _out.WriteLine(
            $"{Entities} entities, {Frames} frames: {drained} events, " +
            $"{totalMs:F2} ms total, {perFrame:F4} ms/frame (slice budget {KernelSliceMs} ms)");

        Assert.True(perFrame < KernelSliceMs * 20,
            $"kernel cost {perFrame:F4} ms/frame is catastrophically over the {KernelSliceMs} ms slice");
    }

    [Fact]
    public void Per_event_frame_cost_does_not_grow_with_board_size()
    {
        // The property that actually matters for a stress board: cost per event must stay flat
        // (or grow only logarithmically) as the board grows. A quadratic scheduler shows up as
        // per-event cost rising with n.
        //
        // An earlier version counted DRAINED EVENTS, which scale linearly with board size BY
        // CONSTRUCTION — so the assertion was near-tautological and would have passed against a
        // quadratic queue. Comparisons per event is the honest measure.
        static double ComparisonsPerEvent(int entities)
        {
            var q = new EventQueue(expectedEvents: entities * 2);
            var buffer = new List<ScheduledEvent>(entities * 2);
            for (var i = 0; i < entities; i++) q.Schedule(i % 500, "a", 0, i);

            var before = q.ComparisonCount;
            long events = 0;
            for (var f = 0; f < 200; f++)
            {
                buffer.Clear();
                events += q.PopDue(f * 16, buffer);
                foreach (var e in buffer) q.Schedule(f * 16 + 300, "a", 0, e.Tag);
            }

            return events == 0 ? 0 : (double)(q.ComparisonCount - before) / events;
        }

        var small = ComparisonsPerEvent(100);
        var large = ComparisonsPerEvent(400);

        Assert.True(small > 0, "no comparisons counted — the instrument is not wired");
        Assert.True(large < small * 3,
            $"per-event cost grew with board size: {small:F1} comparisons/event at 100 entities " +
            $"vs {large:F1} at 400 — a heap should grow only with log n");
    }
}
