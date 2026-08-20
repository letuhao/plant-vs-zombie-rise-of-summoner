using FusionRpg.Core.Diagnostics;

namespace FusionRpg.Injector.Host;

/// <summary>Ships PerfProbe windows every ~5s — perf-probe-plan.md §1.4. Best-effort, never throws.</summary>
public static class PerfReporter
{
    public const float IntervalSeconds = 5f;

    public static void Flush(RpgClient? client)
    {
        if (!PerfProbe.Enabled) return;

        var window = PerfProbe.SnapshotAndReset();

        try
        {
            var match = Match.MatchHost.Runtime.ToSnapshot();
            window["board"] = new Dictionary<string, object>
            {
                ["plants"] = match.PlantCount,
                ["zombies"] = match.ZombieCount,
                ["bullets"] = match.BulletCount
            };
        }
        catch { }

        if (client != null)
        {
            window["queue"] = new Dictionary<string, object>
            {
                ["depth"] = client.QueueCount,
                ["dropped"] = client.Dropped
            };
        }

        try { LogLine(window); } catch { }
        _ = client?.PostPerfAsync(window);
    }

    static void LogLine(Dictionary<string, object> window)
    {
        var frames = window.TryGetValue("frames", out var f) ? f as Dictionary<string, object> : null;
        var gc = window.TryGetValue("gc", out var g) ? g as Dictionary<string, object> : null;
        var sections = window.TryGetValue("sections", out var s) ? s as Dictionary<string, object> : null;
        var loop = sections != null && sections.TryGetValue("loop.tick", out var l) ? l as Dictionary<string, object> : null;
        RpgHost.Log.Info(
            $"[perf] fps={frames?["fpsAvg"]} frameMax={frames?["maxMs"]}ms " +
            $"loopMs={loop?["totalMs"]} allocKb={gc?["allocKb"]} gen0={gc?["gen0"]} gen2={gc?["gen2"]}");
    }
}
