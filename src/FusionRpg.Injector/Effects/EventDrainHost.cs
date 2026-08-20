using FusionRpg.Contracts;
using FusionRpg.Core.Events;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// Injector side of the v2 record-then-drain pipeline — event-pipeline-v2 plan Tasks 9–10.
/// Hot-kind hooks call the TryRecord* helpers instead of running the effect pipeline inline;
/// the drain executes from <c>InjectorLoop.Tick</c> under the frame budget.
///
/// Off (default until Task 10 wires the tick): every hook falls through to the legacy Emit
/// path, so behavior is byte-identical. During a debug session the legacy path is also used —
/// prove packs need full payload fidelity (spec test group 6).
/// </summary>
public static class EventDrainHost
{
    /// <summary>Master switch — flipped by InjectorLoop startup once the drain tick runs (T10). Env kill: FUSIONRPG_EVENT_V2=0.</summary>
    public static bool Enabled { get; set; }

    static EventDrain? _drain;
    static int _pairSeq;
    // I1 (2026-08-21 review): per-TARGET pending melee pairs — a single slot leaked N-1
    // spurious OnDamageTaken per multi-bite frame. Cleared when the frame advances.
    static readonly Dictionary<IntPtr, int> _meleePairsByTarget = new();
    static int _meleePairsFrame = -1;

    public static EventDrain Drain => _drain ??= new EventDrain(EffectRuntime.OnDrained);

    /// <summary>Record path active — off in debug sessions (legacy fidelity path instead).</summary>
    static bool Active => Enabled && !DebugRuntime.SessionActive;

    static int SafeFrame()
    {
        try { return Time.frameCount; }
        catch { return 0; }
    }

    /// <summary>Bullet dealt (pea→zombie, projectile→plant). No pair — the taken side is
    /// suppressed at source for bullets (CombatHitEmitPolicy), matching v1.
    /// <paramref name="wasBullet"/> distinguishes "not a bullet → caller records taken" from
    /// "bullet but ring overflow → drop counted, caller must NOT record taken" (I2).</summary>
    public static bool TryRecordDealtFromBullet(
        byte targetSide, Il2CppObjectBase? damageFrom, IntPtr targetPtr, int targetTypeId, int damage, out bool wasBullet)
    {
        wasBullet = false;
        if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;
        if (damageFrom == null) return false;

        Bullet? bullet = null;
        try { bullet = damageFrom.TryCast<Bullet>(); } catch { }
        if (bullet == null) return false; // melee is the AttackPlant site's job
        wasBullet = true;

        var fromType = 0;
        try { fromType = (int)bullet.fromType; } catch { }
        var d = Drain;
        return d.Record(new GameEventRec(
            GameEventKind.CombatHit, SafeFrame(), d.NextSeq(),
            actorPtr: bullet.Pointer, targetPtr: targetPtr,
            typeId: fromType, targetTypeId: targetTypeId, side: targetSide,
            amount: -Math.Abs(damage), hitCount: 1,
            chainDepth: d.RecordDepth, sourceGrantIdx: -1,
            matchKeyIdx: d.InternMatchKey(GameHooks.MatchKey), pairId: 0));
    }

    /// <summary>Melee dealt (zombie bite). Stamps a pair id that the plant's TakeDamage taken
    /// record consumes in the same frame — the causal dealt/taken link (SSOT §D3).</summary>
    public static bool TryRecordMeleeDealt(IntPtr attackerPtr, int attackerTypeId, IntPtr targetPtr, int targetTypeId, int damage)
    {
        if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;
        var d = Drain;
        var frame = SafeFrame();
        var pair = ++_pairSeq;
        var ok = d.Record(new GameEventRec(
            GameEventKind.CombatHit, frame, d.NextSeq(),
            actorPtr: attackerPtr, targetPtr: targetPtr,
            typeId: attackerTypeId, targetTypeId: targetTypeId, side: GameEventSide.Plant,
            amount: -Math.Abs(damage), hitCount: 1,
            chainDepth: d.RecordDepth, sourceGrantIdx: -1,
            matchKeyIdx: d.InternMatchKey(GameHooks.MatchKey), pairId: pair));
        if (ok)
        {
            if (frame != _meleePairsFrame)
            {
                _meleePairsByTarget.Clear();
                _meleePairsFrame = frame;
            }
            _meleePairsByTarget[targetPtr] = pair;
        }
        return ok;
    }

    /// <summary>Damage taken (OnDamageTaken trigger). Consumes a same-frame melee pair when the
    /// target matches, so the coalescer suppresses the double-fire.</summary>
    public static bool TryRecordTaken(byte side, IntPtr targetPtr, int targetTypeId, IntPtr actorPtr, int damage)
    {
        if (!Active || !EffectRuntime.HasOnDamageTakenGrant()) return false;
        var d = Drain;

        var pair = 0;
        if (_meleePairsFrame == SafeFrame() && _meleePairsByTarget.TryGetValue(targetPtr, out var mp))
        {
            pair = mp;
            _meleePairsByTarget.Remove(targetPtr);
        }

        return d.Record(new GameEventRec(
            side == GameEventSide.Plant ? GameEventKind.PlantDamage : GameEventKind.ZombieDamage,
            SafeFrame(), d.NextSeq(),
            actorPtr: actorPtr, targetPtr: targetPtr,
            typeId: targetTypeId, targetTypeId: targetTypeId, side: side,
            amount: -Math.Abs(damage), hitCount: 1,
            chainDepth: d.RecordDepth, sourceGrantIdx: -1,
            matchKeyIdx: d.InternMatchKey(GameHooks.MatchKey), pairId: pair));
    }

    /// <summary>bullet.init → OnSpawn, only while an OnSpawn grant is live.</summary>
    public static bool TryRecordBulletSpawn(IntPtr bulletPtr, int plantType)
    {
        if (!Active || !EffectRuntime.HasOnSpawnGrant()) return false;
        var d = Drain;
        return d.Record(new GameEventRec(
            GameEventKind.BulletInit, SafeFrame(), d.NextSeq(),
            actorPtr: bulletPtr, targetPtr: IntPtr.Zero,
            typeId: plantType, targetTypeId: 0, side: GameEventSide.Bullet,
            amount: 0, hitCount: 1,
            chainDepth: d.RecordDepth, sourceGrantIdx: -1,
            matchKeyIdx: d.InternMatchKey(GameHooks.MatchKey), pairId: 0));
    }

    // Rolling stats for the perf window (read + reset by PerfReporter).
    static long _processedWindow;
    static long _carriedWindow;
    static long _expensiveDeferredWindow;
    static int _maxLatencyFramesWindow;
    static long _maxRecordTicksWindow;
    // v4 item 1: per-frame death-flush allowance pool (one drain-budget's worth per frame).
    static long _lastBudgetTicks;
    static int _deathFlushFrame = -1;
    static long _deathFlushSpent;

    /// <summary>
    /// Per-frame drain (plan Task 10): budget = 10% of the measured frame time (spec decision
    /// #3), clamped to [0.2ms, 2ms]. Session mode drains unbudgeted and uncoalesced.
    /// Shares one board freeze with the TickDots call that follows.
    /// </summary>
    public static void Tick(float unscaledDeltaTime)
    {
        if (!Enabled || _drain == null || _drain.PendingCount == 0) return;
        using var _perf = FusionRpg.Core.Diagnostics.PerfProbe.Measure(FusionRpg.Core.Diagnostics.PerfSection.DrainTick);

        var frameSec = unscaledDeltaTime > 0f ? unscaledDeltaTime : 1f / 60f;
        var budgetSec = Math.Clamp(frameSec * 0.10, 0.0002, 0.002);
        var budgetTicks = (long)(budgetSec * System.Diagnostics.Stopwatch.Frequency);
        _lastBudgetTicks = budgetTicks;

        _drain.SessionMode = DebugRuntime.SessionActive;
        EffectRuntime.FreezeBoard();
        var stats = _drain.Drain(budgetTicks, SafeFrame());
        _processedWindow += stats.Processed;
        _carriedWindow += stats.Carried;
        _expensiveDeferredWindow += stats.ExpensiveDeferred;
        if (stats.MaxLatencyFrames > _maxLatencyFramesWindow) _maxLatencyFramesWindow = stats.MaxLatencyFrames;
        if (stats.MaxRecordTicks > _maxRecordTicksWindow) _maxRecordTicksWindow = stats.MaxRecordTicks;
    }

    /// <summary>Perf-window stats for PerfReporter — returns and resets the rolling counters.</summary>
    public static Dictionary<string, object> SnapshotStats()
    {
        var d = new Dictionary<string, object>
        {
            ["enabled"] = Enabled,
            ["pending"] = _drain?.PendingCount ?? 0,
            ["droppedOverflow"] = _drain?.DroppedByOverflow ?? 0,
            ["droppedDepth"] = _drain?.DroppedByDepth ?? 0,
            ["droppedDeathBudget"] = _drain?.DroppedByDeathBudget ?? 0,
            ["maxLatencyFrames"] = _maxLatencyFramesWindow,
            ["maxRecordUs"] = _maxRecordTicksWindow > 0
                ? Math.Round(_maxRecordTicksWindow * 1_000_000.0 / System.Diagnostics.Stopwatch.Frequency, 0)
                : 0,
            ["processed"] = _processedWindow,
            ["carried"] = _carriedWindow,
            ["expensiveDeferred"] = _expensiveDeferredWindow
        };
        _processedWindow = 0;
        _carriedWindow = 0;
        _expensiveDeferredWindow = 0;
        _maxLatencyFramesWindow = 0;
        _maxRecordTicksWindow = 0;
        return d;
    }

    /// <summary>
    /// Death barrier — pending hits for a dying entity drain before grant-withdraw (SSOT §A2).
    /// v4 item 1: bounded by a per-frame allowance (one drain-budget's worth); over-allowance
    /// records for dying entities are shed with a counter instead of blowing the frame.
    /// </summary>
    public static void FlushForPtr(IntPtr ptr)
    {
        if (_drain == null || _drain.PendingCount == 0) return;

        var frame = SafeFrame();
        if (frame != _deathFlushFrame)
        {
            _deathFlushFrame = frame;
            _deathFlushSpent = 0;
        }
        var allowance = _lastBudgetTicks > 0 ? _lastBudgetTicks : System.Diagnostics.Stopwatch.Frequency / 500; // 2ms fallback
        var remaining = allowance - _deathFlushSpent;
        if (remaining <= 0) remaining = 0; // still processes nothing beyond budget; drops counted

        EffectRuntime.FreezeBoard();
        _deathFlushSpent += _drain.FlushForPtr(ptr, remaining);
    }

    /// <summary>Match-edge barrier — drain everything, reset interning (board.end / match.result).</summary>
    public static void FlushAllAndReset()
    {
        if (_drain == null) return;
        if (_drain.PendingCount > 0)
            EffectRuntime.FreezeBoard();
        _drain.FlushAllAndReset();
        _meleePairsByTarget.Clear();
        _meleePairsFrame = -1;
    }
}
