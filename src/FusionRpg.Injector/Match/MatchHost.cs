using FusionRpg.Core.Match;
using FusionRpg.Core.Commanders;
using FusionRpg.Injector.Commanders;
using FusionRpg.Injector.Host;

namespace FusionRpg.Injector.Match;

/// <summary>
/// Process-singleton MatchRuntime for LIVE Injector (W2 wire + W3 pause + W5 bind). Main-thread only.
/// </summary>
public static class MatchHost
{
    static readonly object Gate = new();
    static MatchRuntime _runtime = new();

    public static MatchRuntime Runtime
    {
        get { lock (Gate) return _runtime; }
    }

    /// <summary>Replace runtime (tests / reconnect). Prefer Reset for match edges.</summary>
    public static void ResetRuntime()
    {
        lock (Gate) _runtime = new MatchRuntime();
    }

    public static bool TryAdmitSpawn(string? side, out GateResult result)
    {
        lock (Gate)
            return _runtime.TryAdmitSpawn(side, out result);
    }

    /// <summary>
    /// Unique deploy Intent accepted → PendingSpawn (W5-A). Optional loadout JSON for Bound apply.
    /// </summary>
    public static bool TryBeginUniquePending(
        string instanceId,
        string correlationId,
        string side,
        int typeId,
        string? loadoutJson = null)
    {
        try
        {
            lock (Gate)
                return _runtime.TryBeginPending(instanceId, correlationId, side, typeId, loadoutJson);
        }
        catch (Exception ex)
        {
            try { CheatState.Error("MatchHost.TryBeginUniquePending: " + ex.Message); } catch { }
            return false;
        }
    }

    /// <summary>FailDeploy / timeout / spawn-null — GC PendingSpawn (W5-D).</summary>
    public static bool TryClearUniquePending(string? instanceId = null, string? correlationId = null)
    {
        try
        {
            lock (Gate)
                return _runtime.TryClearPending(instanceId, correlationId);
        }
        catch (Exception ex)
        {
            try { CheatState.Error("MatchHost.TryClearUniquePending: " + ex.Message); } catch { }
            return false;
        }
    }

    /// <summary>
    /// Mirror pause UI into MatchPhase (W3-A). Observe-only Emit match.pause / match.resume on transition.
    /// Never throws to caller.
    /// </summary>
    public static void NotifyPaused(bool paused)
    {
        try
        {
            MatchPhase before;
            MatchPhase after;
            lock (Gate)
            {
                before = _runtime.Phase;
                _runtime.NotifyPaused(paused);
                after = _runtime.Phase;
            }

            if (before == after) return;

            try
            {
                GameHooks.Emit(
                    paused ? "match.pause" : "match.resume",
                    new Dictionary<string, object>());
            }
            catch { }
        }
        catch (Exception ex)
        {
            try { CheatState.Error("MatchHost.NotifyPaused: " + ex.Message); } catch { }
        }
    }

    /// <summary>
    /// Fold capture into MatchRuntime. On board.start/end/match.result, ClearAll effects.
    /// On Bound unique, apply ptr-only loadout. On end, clear GameHooks.MatchKey.
    /// Never throws to caller.
    /// </summary>
    public static void Apply(string kind, IReadOnlyDictionary<string, object>? payload)
    {
        try
        {
            lock (Gate)
            {
                var isStart = string.Equals(kind, "board.start", StringComparison.OrdinalIgnoreCase);
                var isEnd = IsMatchEnd(kind);
                string? endingKey = isEnd ? _runtime.MatchKey : null;

                // LIVE BoardAwake may fire without a prior board.end — align with sim overlay.
                if (isStart && _runtime.Phase is not MatchPhase.Idle)
                {
                    var priorKey = _runtime.MatchKey;
                    ClearCommanderSnapshot();
                    _runtime.Apply("board.end");
                    TryEffect("KernelEndBoard", Effects.KernelDriveHost.EndBoard);
                    TryEffect("NotifyMatchEnd", () => Effects.EffectRuntime.NotifyMatchEnd(priorKey));
                    TryEffect("ClearAll", () => Effects.EffectRuntime.ClearAll("match"));
                    GameHooks.MatchKey = null;
                }

                if (isStart)
                {
                    TryEffect("ClearAll", () => Effects.EffectRuntime.ClearAll("match"));
                }

                _runtime.Apply(kind, payload);

                var bound = _runtime.ConsumeLastBound();
                if (bound != null)
                {
                    try { UniqueBoundLoadout.TryApply(bound); } catch { }
                }

                if (isEnd)
                {
                    TryEffect("NotifyMatchEnd", () => Effects.EffectRuntime.NotifyMatchEnd(endingKey));
                    TryEffect("ClearAll", () => Effects.EffectRuntime.ClearAll("match"));
                    ClearCommanderSnapshot();
                    // T13: one kernel per board — drop it here so a stale queue can never survive
                    // into the next match.
                    TryEffect("KernelEndBoard", Effects.KernelDriveHost.EndBoard);
                }

                if (isStart)
                {
                    try { _runtime.ConfigureCaps(SpawnAdmit.Config); } catch { }
                    try
                    {
                        var key = _runtime.MatchKey;
                        if (!string.IsNullOrWhiteSpace(key))
                            GameHooks.MatchKey = key;
                    }
                    catch { }
                    var snapshot = MatchCommanderSnapshotSource.BuildFromSessionCache();
                    if (MatchCommanderSnapshotSource.LastBuildUsedFallback)
                    {
                        try { RpgHost.Log.Warning("commander snapshot: cache miss — implicit Dave"); } catch { }
                    }
                    MatchCommanderSnapshotHolder.BeginMatch(snapshot);
                    CheatState.RefreshCommanderAllocationCache();
                    // T13: a fresh timeline per board, alongside the commander snapshot that is
                    // already frozen at exactly this moment.
                    TryEffect("KernelBeginBoard", Effects.KernelDriveHost.BeginBoard);
                    TryEffect("NotifyMatchStart", () => Effects.EffectRuntime.NotifyMatchStart(_runtime.MatchKey ?? ""));
                }
                else if (isEnd)
                {
                    GameHooks.MatchKey = null;
                }
            }
        }
        catch (Exception ex)
        {
            try { CheatState.Error("MatchHost.Apply: " + ex.Message); } catch { }
        }
    }

    static bool IsMatchEnd(string kind) =>
        string.Equals(kind, "board.end", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "match.result", StringComparison.OrdinalIgnoreCase);

    static void ClearCommanderSnapshot()
    {
        MatchCommanderSnapshotHolder.EndMatch();
        CheatState.RefreshCommanderAllocationCache();
    }

    static void TryEffect(string label, Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            try { CheatState.Error("MatchHost plugin/clear: " + label + ": " + ex.Message); } catch { }
        }
    }
}
