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
                ApplyLawnMoveMode();
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
            // Dirty means dirty. The second, source-enumerating veto that used to sit here
            // (`ShouldPushScalesOnDirty`: cheat doc revision / PvzStats revision / Tab A scales)
            // silently discarded every OTHER contributor's change -- a commander reallocation set the
            // dirty flag and was then vetoed, so living entities never re-resolved (owner-observed
            // live 2026-08-30). Deciding whether a re-resolve is WORTH writing is EntityApply's job
            // now, and it answers by comparing values (EntityFinal.DiffersFrom), so a reapply that
            // changes nothing writes nothing. `Invalidate` is edge-triggered from 6 discrete state
            // changes, never per-frame, so this is one board pass per real change.
            if (CheatState.Stats.ConsumeDirty(out _))
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
        // A-M2 lawn-reposition — same record-then-drain slot as EventDrainHost above. Default off
        // (spec-lawn-reposition.md §6 hazard 4, ships knowingly inert); a no-op while
        // MoveDrainHost.Enabled is false or nothing has called TryRecordMove.
        try { MoveDrainHost.Tick(unscaledDeltaTime); } catch { }
        // battle-timeline T13 — the kernel drives DoT and shield upkeep as scheduled 100 ms events,
        // in the same slot and the same order the two accumulator grids used to occupy
        // (drain -> DoT -> shields; shield-system-spec.md §2.6). Same period, same work: only the
        // scheduling moved, which is what makes this a substitution rather than a redesign.
        //
        // The kernel clock is FULLY SCALED (decisions.md, "Battle engine open questions
        // (2026-09-04)", item 4): it stops on pause and accelerates on fast-forward, up to the 10x
        // CheatActions.cs allows. That acceleration is chosen, not overlooked -- do not "fix" a 10x
        // DoT as a bug. `unscaledDeltaTime * Time.timeScale` rather than `Time.deltaTime` because
        // Unity clamps the latter at Time.maximumDeltaTime, which would silently lose simulated time
        // after a level-load hitch -- the exact loss the carry-corrected clock exists to prevent.
        // The second argument stays REAL frame time: the drain budget bounds wall-clock work on the
        // main thread, so it must not scale with the game's clock.
        try { KernelDriveHost.Tick(unscaledDeltaTime * Time.timeScale, unscaledDeltaTime); } catch { }
        if (!KernelDriveHost.DrivingGrids)
        {
            // Off-board, or FUSIONRPG_KERNEL_GRIDS=0. The legacy accumulators still own the grids
            // then — the same revert shape FUSIONRPG_EVENT_V2=0 already gives the event pipeline.
            // These stay UNSCALED on purpose: the kill switch's job is to restore pre-T13 behaviour
            // exactly, and pre-T13 behaviour was unscaled. The scaled clock is the kernel's, not a
            // repo-wide change of what a DoT tick means.
            try { EffectRuntime.TickDots(unscaledDeltaTime); } catch { }
            try { EffectRuntime.TickShields(unscaledDeltaTime); } catch { }
        }
        try { Hud.ActorHudCache.ReconcileDirty(); } catch { }
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

    /// <summary>
    /// A-M2 lawn-reposition ships default-off (spec-lawn-reposition.md §6 hazard 4, "SHIPS
    /// KNOWINGLY INERT" — the production caller does not exist yet). Unlike
    /// <see cref="ApplyEventPipelineMode"/>'s default-ON-unless-killed shape, this only ever forces
    /// the switch OFF: FUSIONRPG_LAWN_MOVE=0 is a true kill switch that wins over any future default
    /// flip or debug toggle, but its absence never turns the feature on by itself — the static
    /// default (false) is what "ships inert" means, and nothing in this method may override that.
    /// </summary>
    static void ApplyLawnMoveMode()
    {
        try
        {
            var off = string.Equals(Environment.GetEnvironmentVariable("FUSIONRPG_LAWN_MOVE"), "0", StringComparison.Ordinal);
            if (off) MoveDrainHost.Enabled = false;
            RpgHost.Log.Info("[perf] lawn move drain " + (MoveDrainHost.Enabled ? "ON" : "OFF (default; FUSIONRPG_LAWN_MOVE=0 forces off)"));
        }
        catch { }
    }

    /// <summary>Convenience when Unity Time is available (BepInEx RpgLoop).</summary>
    public static void TickFromUnity() => Tick(Time.unscaledDeltaTime);
}
