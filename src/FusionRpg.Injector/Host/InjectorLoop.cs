using FusionRpg.Core.Diagnostics;
using FusionRpg.Injector.Effects;
using FusionRpg.Injector.Fx;
using UnityEngine;

namespace FusionRpg.Injector.Host;

/// <summary>
/// Per-frame injector work. Called from BepInEx MonoBehaviour.Update or MelonMod.OnUpdate.
/// </summary>
public static class InjectorLoop
{
    static float _hb;
    static float _cmdPull;
    static float _poll;
    static float _cheatPush;
    static float _startDelay;
    static float _perf;
    static bool _started;

    /// <summary>Reset timers (e.g. after host reload). Normally not needed.</summary>
    public static void Reset()
    {
        _hb = 0;
        _cmdPull = 0;
        _poll = 0;
        _cheatPush = 0;
        _startDelay = 0;
        _perf = 0;
        _started = false;
    }

    public static void Tick(float unscaledDeltaTime)
    {
        PerfProbe.RecordFrame(unscaledDeltaTime);
        using var _perfScope = PerfProbe.Measure(PerfSection.LoopTick);
        var client = RpgHost.Client;
        if (!_started)
        {
            _startDelay += unscaledDeltaTime;
            if (_startDelay >= 2f)
            {
                _started = true;
                ApplyFpsCap();
                ApplyEventPipelineMode();
                _ = client?.StartAsync();
            }
        }
        client?.TryFlush();
        try { using (PerfProbe.Measure(PerfSection.PumpMain)) GameHooks.PumpMainThread(); } catch { }
        try { using (PerfProbe.Measure(PerfSection.PollBoard)) GameHooks.PollBoard(); } catch { }
        try { CheatCommandRunner.Drain(); } catch { }
        try { CheatUiActions.Drain(); }
        catch (Exception ex) { RpgHost.Log.Error("CheatUiActions: " + ex); }
        try
        {
            if (CheatState.Stats.ConsumeDirty(out _) && CheatState.ShouldPushScalesOnDirty())
            {
                CheatActions.ReapplyLivingFromStats();
                CheatState.MarkAppliedRevision();
            }
        }
        catch { }
        try { using (PerfProbe.Measure(PerfSection.CheatContinuous)) CheatActions.TickContinuous(); } catch { }
        try { using (PerfProbe.Measure(PerfSection.CheatAutoCollect)) CheatActions.AutoCollectTick(); } catch { }
        try { using (PerfProbe.Measure(PerfSection.VfxTick)) VfxDirector.Tick(unscaledDeltaTime); } catch { }
        try { Hud.OverlayInput.Tick(); } catch { }
        try { Hud.OverlaySwitch.Tick(); } catch { }
        // v2 drain before TickDots so DoT pulses share the drain's board freeze and
        // merge into the same funnel window (plan Task 10).
        try { EventDrainHost.Tick(unscaledDeltaTime); } catch { }
        try { EffectRuntime.TickDots(unscaledDeltaTime); } catch { }
        // Shield upkeep after dispatch+DoTs — spec frame order (shield-system-spec.md §2.6).
        try { EffectRuntime.TickShields(unscaledDeltaTime); } catch { }
        _hb += unscaledDeltaTime;
        if (_hb >= 2f)
        {
            _hb = 0;
            _ = client?.HeartbeatAsync();
        }
        _cmdPull += unscaledDeltaTime;
        if (_cmdPull >= 0.25f)
        {
            _cmdPull = 0;
            _ = client?.PullPendingCommandsAsync();
        }
        _perf += unscaledDeltaTime;
        if (_perf >= PerfReporter.IntervalSeconds)
        {
            _perf = 0;
            try { PerfReporter.Flush(client); } catch { }
        }
        _cheatPush += unscaledDeltaTime;
        if (_cheatPush >= 3f)
        {
            _cheatPush = 0;
            try { _ = client?.PushCheatSnapshotAsync(); } catch { }
        }
        if (client is { SignalROk: false })
        {
            _poll += unscaledDeltaTime;
            if (_poll >= 5f)
            {
                _poll = 0;
                _ = client.RefreshStatsAsync();
            }
        }
    }

    /// <summary>
    /// Frame cap — spec decision #3: default 60 for headroom; FUSIONRPG_FPS_CAP=0 uncaps,
    /// any other value overrides.
    /// </summary>
    static void ApplyFpsCap()
    {
        try
        {
            var s = Environment.GetEnvironmentVariable("FUSIONRPG_FPS_CAP");
            var cap = 60;
            if (int.TryParse(s, out var parsed))
                cap = parsed;
            if (cap <= 0)
            {
                RpgHost.Log.Info("[perf] fps uncapped (FUSIONRPG_FPS_CAP=0)");
                return;
            }
            QualitySettings.vSyncCount = 0; // targetFrameRate is ignored while vsync is on
            Application.targetFrameRate = cap;
            RpgHost.Log.Info($"[perf] fps capped at {cap} (default 60; FUSIONRPG_FPS_CAP overrides, 0 = uncapped)");
        }
        catch { }
    }

    /// <summary>v2 record-then-drain is on by default; FUSIONRPG_EVENT_V2=0 reverts to the legacy inline pipeline.</summary>
    static void ApplyEventPipelineMode()
    {
        try
        {
            var off = string.Equals(Environment.GetEnvironmentVariable("FUSIONRPG_EVENT_V2"), "0", StringComparison.Ordinal);
            EventDrainHost.Enabled = !off;
            RpgHost.Log.Info("[perf] event pipeline v2 " + (off ? "OFF (legacy inline)" : "ON (record-then-drain)"));
        }
        catch { }
    }

    /// <summary>Convenience when Unity Time is available (BepInEx RpgLoop).</summary>
    public static void TickFromUnity() => Tick(Time.unscaledDeltaTime);
}
