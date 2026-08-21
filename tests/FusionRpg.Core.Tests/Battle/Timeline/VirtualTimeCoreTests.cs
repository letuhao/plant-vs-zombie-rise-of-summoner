using System.Runtime.CompilerServices;
using FusionRpg.Core.Battle.Timeline;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Timeline;

/// <summary>T1 — the clock and the Future Event List. No actors, no battle, no game.</summary>
public class EventQueueTests
{
    static readonly List<ScheduledEvent> Buf = new();

    static List<ScheduledEvent> Drain(EventQueue q, long now)
    {
        var into = new List<ScheduledEvent>();
        q.PopDue(now, into);
        return into;
    }

    [Fact]
    public void Pop_order_is_due_tick_then_insertion()
    {
        var q = new EventQueue();
        q.Schedule(200, "b", 1, 0);
        q.Schedule(100, "a", 1, 0);
        q.Schedule(100, "c", 1, 0);   // ties with "a" — insertion order decides

        var popped = Drain(q, 500).Select(e => e.OwnerKey).ToList();
        Assert.Equal(new[] { "a", "c", "b" }, popped);
    }

    [Fact]
    public void Pop_takes_only_events_due_now()
    {
        var q = new EventQueue();
        q.Schedule(100, "a", 1, 0);
        q.Schedule(300, "b", 1, 0);

        Assert.Equal(new[] { "a" }, Drain(q, 100).Select(e => e.OwnerKey));
        Assert.Empty(Drain(q, 299));
        Assert.Equal(new[] { "b" }, Drain(q, 300).Select(e => e.OwnerKey));
    }

    [Fact]
    public void Cancel_removes_only_its_own_event()
    {
        var q = new EventQueue();
        q.Schedule(100, "a", 1, 0);
        var h = q.Schedule(100, "b", 1, 0);
        q.Schedule(100, "c", 1, 0);

        Assert.True(q.Cancel(h));
        Assert.Equal(new[] { "a", "c" }, Drain(q, 100).Select(e => e.OwnerKey));
    }

    [Fact]
    public void Cancelling_a_fired_or_unknown_handle_is_a_no_op()
    {
        var q = new EventQueue();
        var h = q.Schedule(100, "a", 1, 0);
        Drain(q, 100);

        Assert.False(q.Cancel(h));      // already fired — not an exception
        Assert.False(q.Cancel(new EventHandle(9999)));
    }

    [Fact]
    public void Reschedule_moves_the_event_and_preserves_seq()
    {
        // The audit's I10: implemented as cancel+insert with a fresh Seq, a delay effect would
        // silently reorder UNRELATED actors and make every golden history-dependent.
        var q = new EventQueue();
        var moved = q.Schedule(100, "moved", 1, 0);
        q.Schedule(500, "x", 1, 0);
        q.Schedule(500, "y", 1, 0);     // x before y, by insertion

        Assert.True(q.Reschedule(moved, 500));

        var popped = Drain(q, 500).Select(e => e.OwnerKey).ToList();
        // "moved" keeps its original (earliest) Seq, so it sorts ahead of x and y — and
        // crucially x still precedes y: rescheduling one event did not disturb the others.
        Assert.Equal(new[] { "moved", "x", "y" }, popped);
    }

    [Fact]
    public void Peek_reports_the_next_due_tick()
    {
        var q = new EventQueue();
        Assert.Null(q.PeekDueTick());
        q.Schedule(400, "a", 1, 0);
        q.Schedule(150, "b", 1, 0);
        Assert.Equal(150, q.PeekDueTick());
    }

    [Fact]
    public void Replaying_the_same_script_produces_the_same_sequence()
    {
        string Run()
        {
            var q = new EventQueue();
            var handles = new List<EventHandle>();
            for (var i = 0; i < 50; i++)
                handles.Add(q.Schedule((i * 37) % 200, "a" + i, i % 3, i));
            for (var i = 0; i < 50; i += 7) q.Cancel(handles[i]);
            for (var i = 1; i < 50; i += 11) q.Reschedule(handles[i], 500 + i);
            return string.Join(",", Drain(q, 10_000).Select(e => e.OwnerKey));
        }

        Assert.Equal(Run(), Run());
    }
}

public class SimulationClockTests
{
    [Fact]
    public void Next_event_advance_jumps_to_the_next_due_tick()
    {
        var q = new EventQueue();
        q.Schedule(1234, "a", 1, 0);
        var clock = new SimulationClock();

        var r = clock.TryAdvance(new NextEventAdvance(), q);
        Assert.Equal(AdvanceOutcome.Advanced, r.Outcome);
        Assert.Equal(1234, clock.Now);
    }

    [Fact]
    public void Next_event_advance_blocks_on_an_empty_queue()
    {
        // The audit's C3: a jumping clock has no wall-time to dwell in, so it must be able to
        // report that it cannot advance — otherwise an interactive battle leaps past its input
        // window. This is why TryAdvance returns a result instead of void.
        var clock = new SimulationClock();
        var r = clock.TryAdvance(new NextEventAdvance(), new EventQueue());

        Assert.Equal(AdvanceOutcome.Blocked, r.Outcome);
        Assert.Equal(0, clock.Now);
        Assert.False(string.IsNullOrWhiteSpace(r.Reason));
    }

    [Fact]
    public void Fixed_increment_at_sixty_fps_does_not_drift()
    {
        // 1000 ms / 60 frames = 16.667 ticks per frame. Naive Δ=16 loses 0.667 ms per frame —
        // 40 ms per second, 2.4 seconds per minute. The carry must eliminate that entirely.
        var advance = new FixedIncrementAdvance(ticksPerFrameNum: 1000, ticksPerFrameDen: 60);
        var clock = new SimulationClock();
        var q = new EventQueue();

        for (var i = 0; i < 10_000; i++)
            clock.TryAdvance(advance, q, frames: 1);

        Assert.Equal(10_000L * 1000 / 60, clock.Now);
    }

    [Fact]
    public void Fixed_increment_one_frame_at_a_time_equals_one_big_step()
    {
        var a = new SimulationClock();
        var advA = new FixedIncrementAdvance(1000, 60);
        for (var i = 0; i < 600; i++) a.TryAdvance(advA, new EventQueue(), frames: 1);

        var b = new SimulationClock();
        b.TryAdvance(new FixedIncrementAdvance(1000, 60), new EventQueue(), frames: 600);

        Assert.Equal(b.Now, a.Now);
    }

    [Fact]
    public void Clock_never_moves_backwards()
    {
        var q = new EventQueue();
        q.Schedule(500, "a", 1, 0);
        var clock = new SimulationClock();
        clock.TryAdvance(new NextEventAdvance(), q);

        q.Schedule(100, "late", 1, 0);   // scheduled in the past
        var r = clock.TryAdvance(new NextEventAdvance(), q);

        Assert.Equal(AdvanceOutcome.Advanced, r.Outcome);
        Assert.Equal(500, clock.Now);    // clamped, not rewound
    }
}

/// <summary>
/// The purity guard, as a source scan rather than reflection — reflection cannot see a
/// <c>DateTime.UtcNow</c> inside a method body, and the audit's I12 point was that the
/// invariant was being enforced exactly where it was not at risk.
/// </summary>
public class TimelinePurityGuardTests
{
    static string TimelineDir([CallerFilePath] string here = "")
    {
        var testsDir = Path.GetDirectoryName(here)!;                                   // tests/.../Battle/Timeline
        var repo = Path.GetFullPath(Path.Combine(testsDir, "..", "..", "..", ".."));   // repo root
        return Path.Combine(repo, "src", "FusionRpg.Core", "Battle", "Timeline");
    }

    [Fact]
    public void Kernel_sources_contain_no_wall_clock_rng_or_floating_point()
    {
        var dir = TimelineDir();
        Assert.True(Directory.Exists(dir), $"kernel source dir not found: {dir}");
        Assert.True(Directory.GetFiles(dir, "*.cs").Length > 0, "purity scan found no kernel files to check");

        var offences = KernelPurityScan.Scan(dir);
        Assert.True(offences.Count == 0,
            "kernel purity violated (integer ticks only, no wall clock, no RNG):\n" + string.Join("\n", offences));
    }

    [Theory]
    [InlineData("var t = DateTime.UtcNow;", "DateTime")]
    [InlineData("readonly Random _rng = new();", "Random")]
    [InlineData("double ratio = 0.5;", "double ")]
    [InlineData("float dt = 0.016f;", "float ")]
    public void The_purity_scan_actually_detects_a_violation(string badLine, string expectedToken)
    {
        // Tests the guard, not the kernel. A scan that stays green while the invariant is broken
        // is worse than none — it manufactures confidence. This proves it can fail.
        var tmp = Path.Combine(Path.GetTempPath(), "fusionrpg-purity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllLines(Path.Combine(tmp, "Offender.cs"),
                new[] { "namespace X;", "public sealed class Offender", "{", "    " + badLine, "}" });

            var offences = KernelPurityScan.Scan(tmp);
            Assert.NotEmpty(offences);
            Assert.Contains(offences, o => o.Contains(expectedToken, StringComparison.Ordinal));
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void The_purity_scan_ignores_prose_that_merely_names_a_banned_token()
    {
        // Doc comments explain WHY wall-clock reads are forbidden, so they must be allowed to say
        // the words — otherwise the guard punishes documentation.
        var tmp = Path.Combine(Path.GetTempPath(), "fusionrpg-purity-ok-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            File.WriteAllLines(Path.Combine(tmp, "Clean.cs"),
                new[] { "// never read DateTime here", "public sealed class Clean { public long Now; }" });

            Assert.Empty(KernelPurityScan.Scan(tmp));
        }
        finally
        {
            try { Directory.Delete(tmp, true); } catch { /* temp */ }
        }
    }
}
