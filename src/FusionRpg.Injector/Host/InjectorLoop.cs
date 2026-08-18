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
    static bool _started;

    /// <summary>Reset timers (e.g. after host reload). Normally not needed.</summary>
    public static void Reset()
    {
        _hb = 0;
        _cmdPull = 0;
        _poll = 0;
        _cheatPush = 0;
        _startDelay = 0;
        _started = false;
    }

    public static void Tick(float unscaledDeltaTime)
    {
        var client = RpgHost.Client;
        if (!_started)
        {
            _startDelay += unscaledDeltaTime;
            if (_startDelay >= 2f)
            {
                _started = true;
                _ = client?.StartAsync();
            }
        }
        client?.TryFlush();
        try { GameHooks.PumpMainThread(); } catch { }
        try { GameHooks.PollBoard(); } catch { }
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
        try { CheatActions.TickContinuous(); } catch { }
        try { CheatActions.AutoCollectTick(); } catch { }
        try { DamageFxOverlay.Tick(unscaledDeltaTime); } catch { }
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

    /// <summary>Convenience when Unity Time is available (BepInEx RpgLoop).</summary>
    public static void TickFromUnity() => Tick(Time.unscaledDeltaTime);
}
