using System.Diagnostics;

namespace FusionRpg.Core.Diagnostics;

/// <summary>Hot-path sections timed by <see cref="PerfProbe"/> — perf-probe-plan.md §1.1.</summary>
public enum PerfSection
{
    LoopTick = 0,
    BoardCapture = 1,
    StatsResolve = 2,
    HubResolveDerived = 3,
    EffectOnCapture = 4,
    EffectTickDots = 5,
    TakeDamagePrefix = 6,
    FxShow = 7,
    GrantsScan = 8,
    EntityApply = 9,
    MatchApply = 10,
    EffectOnEvent = 11
}

/// <summary>
/// Allocation-free perf counters for the injector/core hot path.
/// Record path is Interlocked-only; <see cref="SnapshotAndReset"/> (every ~5s) is the only allocating call.
/// Kill switch: env <c>FUSIONRPG_PERF=0</c>.
/// </summary>
public static class PerfProbe
{
    const int SectionCount = 12;

    static readonly string[] SectionNames =
    {
        "loop.tick",
        "board.capture",
        "stats.resolve",
        "hub.resolveDerived",
        "effect.onCapture",
        "effect.tickDots",
        "takeDamage.prefix",
        "fx.show",
        "grants.scan",
        "entity.apply",
        "match.apply",
        "effect.onEvent"
    };

    static readonly long[] Counts = new long[SectionCount];
    static readonly long[] TotalTicks = new long[SectionCount];
    static readonly long[] MaxTicks = new long[SectionCount];

    // Emit classes: 0=combat.* 1=debug.* 2=*.damage 3=other
    static readonly string[] EmitNames = { "combat", "debug", "damage", "other" };
    static readonly long[] Emits = new long[4];

    // Frame buckets: <8.4ms (120fps ok), <16.7ms (60fps ok), <33.4ms, >=33.4ms
    static readonly long[] FrameBuckets = new long[4];
    static long _frameMaxUs;

    static long _windowStart = Stopwatch.GetTimestamp();
    static int _lastGen0 = GC.CollectionCount(0);
    static int _lastGen1 = GC.CollectionCount(1);
    static int _lastGen2 = GC.CollectionCount(2);
    static long _lastAlloc = GC.GetTotalAllocatedBytes();

    public static bool Enabled { get; set; } =
        !string.Equals(Environment.GetEnvironmentVariable("FUSIONRPG_PERF"), "0", StringComparison.Ordinal);

    public static PerfScope Measure(PerfSection section) =>
        Enabled ? new PerfScope((int)section, Stopwatch.GetTimestamp()) : default;

    internal static void Record(int section, long elapsedTicks)
    {
        if ((uint)section >= SectionCount) return;
        if (elapsedTicks < 0) elapsedTicks = 0;
        Interlocked.Increment(ref Counts[section]);
        Interlocked.Add(ref TotalTicks[section], elapsedTicks);
        InterlockedMax(ref MaxTicks[section], elapsedTicks);
    }

    /// <summary>Classify an emitted event kind into a coarse bucket and count it.</summary>
    public static void CountEmit(string? kind)
    {
        if (!Enabled) return;
        var idx = 3;
        if (!string.IsNullOrEmpty(kind))
        {
            if (kind.StartsWith("debug.", StringComparison.OrdinalIgnoreCase)) idx = 1;
            else if (kind.StartsWith("combat.", StringComparison.OrdinalIgnoreCase)) idx = 0;
            else if (kind.EndsWith(".damage", StringComparison.OrdinalIgnoreCase)) idx = 2;
        }
        Interlocked.Increment(ref Emits[idx]);
    }

    /// <summary>Record one rendered frame (unscaled delta time in seconds).</summary>
    public static void RecordFrame(float dtSeconds)
    {
        if (!Enabled || dtSeconds <= 0f) return;
        var idx = dtSeconds < 0.0084f ? 0 : dtSeconds < 0.0167f ? 1 : dtSeconds < 0.0334f ? 2 : 3;
        Interlocked.Increment(ref FrameBuckets[idx]);
        InterlockedMax(ref _frameMaxUs, (long)(dtSeconds * 1_000_000f));
    }

    static void InterlockedMax(ref long target, long value)
    {
        var seen = Volatile.Read(ref target);
        while (value > seen)
        {
            var prev = Interlocked.CompareExchange(ref target, value, seen);
            if (prev == seen) return;
            seen = prev;
        }
    }

    static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// Snapshot the current window and reset all counters. Allocates — call on the 5s grid only.
    /// Caller may add extra keys (board census, queue depth) before shipping.
    /// </summary>
    public static Dictionary<string, object> SnapshotAndReset()
    {
        var now = Stopwatch.GetTimestamp();
        var windowMs = TicksToMs(now - Interlocked.Exchange(ref _windowStart, now));

        var sections = new Dictionary<string, object>(SectionCount);
        for (var i = 0; i < SectionCount; i++)
        {
            var count = Interlocked.Exchange(ref Counts[i], 0);
            var total = Interlocked.Exchange(ref TotalTicks[i], 0);
            var max = Interlocked.Exchange(ref MaxTicks[i], 0);
            if (count == 0) continue;
            var totalMs = TicksToMs(total);
            sections[SectionNames[i]] = new Dictionary<string, object>
            {
                ["count"] = count,
                ["totalMs"] = Math.Round(totalMs, 3),
                ["maxMs"] = Math.Round(TicksToMs(max), 3),
                ["avgUs"] = Math.Round(totalMs * 1000.0 / count, 1),
                ["perSec"] = windowMs > 0 ? Math.Round(count * 1000.0 / windowMs, 1) : 0
            };
        }

        var emits = new Dictionary<string, object>(4);
        for (var i = 0; i < Emits.Length; i++)
        {
            var n = Interlocked.Exchange(ref Emits[i], 0);
            if (n > 0) emits[EmitNames[i]] = n;
        }

        long frameTotal = 0;
        var buckets = new long[FrameBuckets.Length];
        for (var i = 0; i < FrameBuckets.Length; i++)
        {
            buckets[i] = Interlocked.Exchange(ref FrameBuckets[i], 0);
            frameTotal += buckets[i];
        }
        var frameMaxUs = Interlocked.Exchange(ref _frameMaxUs, 0);
        var frames = new Dictionary<string, object>
        {
            ["total"] = frameTotal,
            ["lt8ms"] = buckets[0],
            ["lt17ms"] = buckets[1],
            ["lt33ms"] = buckets[2],
            ["gte33ms"] = buckets[3],
            ["maxMs"] = Math.Round(frameMaxUs / 1000.0, 2),
            ["fpsAvg"] = windowMs > 0 ? Math.Round(frameTotal * 1000.0 / windowMs, 1) : 0
        };

        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var alloc = GC.GetTotalAllocatedBytes();
        var gc = new Dictionary<string, object>
        {
            ["gen0"] = gen0 - _lastGen0,
            ["gen1"] = gen1 - _lastGen1,
            ["gen2"] = gen2 - _lastGen2,
            ["allocKb"] = Math.Round((alloc - _lastAlloc) / 1024.0, 1)
        };
        _lastGen0 = gen0;
        _lastGen1 = gen1;
        _lastGen2 = gen2;
        _lastAlloc = alloc;

        return new Dictionary<string, object>
        {
            ["t"] = DateTime.UtcNow.ToString("o"),
            ["windowMs"] = Math.Round(windowMs, 0),
            ["frames"] = frames,
            ["sections"] = sections,
            ["emits"] = emits,
            ["gc"] = gc
        };
    }

    /// <summary>Test/reload helper — drop all pending counters without producing a window.</summary>
    public static void ResetAll() => SnapshotAndReset();
}

/// <summary>Disposable timing scope — struct, no allocation. Default instance records nothing.</summary>
public readonly struct PerfScope : IDisposable
{
    readonly int _section;
    readonly long _start;

    internal PerfScope(int section, long start)
    {
        _section = section + 1; // 0 = inert default
        _start = start;
    }

    public void Dispose()
    {
        if (_section == 0) return;
        PerfProbe.Record(_section - 1, Stopwatch.GetTimestamp() - _start);
    }
}
