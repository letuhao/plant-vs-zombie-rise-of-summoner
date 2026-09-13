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
/// ON by default since Task 10 wired the tick (<c>InjectorLoop.ApplyEventPipelineMode</c> sets
/// <see cref="Enabled"/> unless <c>FUSIONRPG_EVENT_V2=0</c>). When off, every hook falls through
/// to the legacy Emit path, so behavior is byte-identical. During a debug session the legacy path
/// is also used — prove packs need full payload fidelity (spec test group 6).
/// </summary>
public static class EventDrainHost
{
    /// <summary>Master switch — set true by InjectorLoop startup (T10 shipped), i.e. ON by default. Env kill: FUSIONRPG_EVENT_V2=0.</summary>
    public static bool Enabled { get; set; }

    static EventDrain? _drain;
    static int _pairSeq;
    // I1 (2026-08-21 review): per-TARGET pending melee pairs — a single slot leaked N-1
    // spurious OnDamageTaken per multi-bite frame. Cleared when the frame advances.
    static readonly Dictionary<IntPtr, int> _meleePairsByTarget = new();
    static int _meleePairsFrame = -1;

    // lawn-hit-attribution (T6): ambient melee attacker for a multi-target attack whose own Harmony
    // hook has no target parameter (QingZombie.AttackPlants()/EternalZombie_a.AttackPlants() — the
    // plural methods take no args, unlike the singular AttackPlant(Plant)). TakeDamage's own hook
    // knows the target but not the true attacker (damageFrom is unreliable for melee — see the class
    // comment on ZombieAttackPlant in GameHooks.cs), so the outer attack method's own Prefix/Postfix
    // brackets who is currently swinging. A single slot is safe: PVZ Fusion drives all combat from the
    // Unity main thread only, exactly like _meleePairsByTarget/_meleePairsFrame above. Frame-stamped so
    // a Postfix skipped by an exception in the original method (Harmony does not guarantee a postfix
    // runs after the patched method throws) cannot leak a stale attacker past its own frame.
    static IntPtr _ambientMeleeAttackerPtr = IntPtr.Zero;
    static int _ambientMeleeAttackerTypeId;
    static int _ambientMeleeAttackerFrame = -1;

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
    /// "bullet but ring overflow → drop counted, caller must NOT record taken" (I2).
    ///
    /// lawn-hit-attribution (T6): the recorded ATTACKER is the firing creature —
    /// <c>bullet.from</c> (Plant) or <c>bullet.from_zombie</c> (Zombie), chosen by
    /// <c>bullet.shootByZombie</c> — never the bullet's own pointer. A bullet ptr has no ActorHub
    /// baseline, so any Hub resolve on it fell to the stub <c>{Hp=100,MaxHp=100,Atk=10}</c>, and an
    /// <c>entity:{ptr}</c> grant bound to the firing plant could never match a projectile hit
    /// (spec-lawn-hit-attribution.md). <c>bullet.fromType</c> is deliberately NOT read here even
    /// though it is a real field — it is a <c>PlantType</c> id (the engine's own naming, not ours)
    /// and stays type-scoped for <c>GrantedBulletModifyAtoms</c>'s existing use; this module exists
    /// precisely because per-INSTANCE attribution was missing. The bullet's own ptr survives as the
    /// record's <see cref="GameEventRec.SwingPtr"/> — the swing identity, not the attacker.</summary>
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

        var shooterPtr = IntPtr.Zero;
        var shooterTypeId = 0;
        try
        {
            if (bullet.shootByZombie)
            {
                var z = bullet.from_zombie;
                if (z != null)
                {
                    shooterPtr = z.Pointer;
                    try { shooterTypeId = (int)z.theZombieType; } catch { }
                }
            }
            else
            {
                var p = bullet.from;
                if (p != null)
                {
                    shooterPtr = p.Pointer;
                    try { shooterTypeId = (int)p.thePlantType; } catch { }
                }
            }
        }
        catch { shooterPtr = IntPtr.Zero; }

        // A null shooter is ordinary — the firer already died, or an engine-spawned projectile with
        // no owner — never an error, and NEVER a fallback to the bullet's own ptr: that would
        // silently reintroduce the stub-resolve bug this module removes. No RPG contribution; the
        // caller still treats this as "was a bullet" (wasBullet stays true above) so the vanilla
        // taken-side record stays suppressed, matching existing bullet policy.
        if (shooterPtr == IntPtr.Zero) return false;

        var d = Drain;
        return d.Record(new GameEventRec(
            GameEventKind.CombatHit, SafeFrame(), d.NextSeq(),
            actorPtr: shooterPtr, targetPtr: targetPtr,
            typeId: shooterTypeId, targetTypeId: targetTypeId, side: targetSide,
            amount: -Math.Abs(damage), hitCount: 1,
            chainDepth: d.RecordDepth, sourceGrantIdx: -1,
            matchKeyIdx: d.InternMatchKey(GameHooks.MatchKey), pairId: 0,
            swingPtr: bullet.Pointer));
    }

    /// <summary>Melee dealt (zombie bite, or a plant-side area melee like Shulkflower's
    /// AttackEffect). Stamps a pair id that the target's TakeDamage taken record consumes in the
    /// same frame — the causal dealt/taken link (SSOT §D3). <paramref name="targetSide"/> is the
    /// SIDE OF THE TARGET (Plant for a zombie bite, Zombie for a plant-side area attack) — widened
    /// from a hardcoded Plant so this one function serves both directions rather than forking a
    /// second copy for the reverse attacker/target shape.</summary>
    public static bool TryRecordMeleeDealt(
        byte targetSide, IntPtr attackerPtr, int attackerTypeId, IntPtr targetPtr, int targetTypeId, int damage)
    {
        if (!Active || !EffectRuntime.HasOnDamageDealtGrant()) return false;
        var d = Drain;
        var frame = SafeFrame();
        var pair = ++_pairSeq;
        // swingPtr omitted (defaults to Zero): melee's swing identity is (ActorPtr, Frame),
        // already on the record — no bullet-shaped identity to carry.
        var ok = d.Record(new GameEventRec(
            GameEventKind.CombatHit, frame, d.NextSeq(),
            actorPtr: attackerPtr, targetPtr: targetPtr,
            typeId: attackerTypeId, targetTypeId: targetTypeId, side: targetSide,
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

    /// <summary>Begins the ambient-attacker bracket for a multi-target attack method with no target
    /// parameter of its own (see the class-level comment on the static fields above). Called from the
    /// attack method's own Harmony Prefix.</summary>
    public static void BeginAmbientMeleeAttacker(IntPtr attackerPtr, int attackerTypeId)
    {
        _ambientMeleeAttackerPtr = attackerPtr;
        _ambientMeleeAttackerTypeId = attackerTypeId;
        _ambientMeleeAttackerFrame = SafeFrame();
    }

    /// <summary>Ends the ambient-attacker bracket. Called from the attack method's own Harmony
    /// Postfix; safe to call even when no bracket is active.</summary>
    public static void EndAmbientMeleeAttacker()
    {
        _ambientMeleeAttackerPtr = IntPtr.Zero;
        _ambientMeleeAttackerTypeId = 0;
        _ambientMeleeAttackerFrame = -1;
    }

    /// <summary>
    /// Fallback dealt-record for a multi-target melee attack whose own Harmony hook has no target
    /// parameter to record from directly (QingZombie.AttackPlants / EternalZombie_a.AttackPlants) —
    /// TakeDamage's own hook knows the target but, per the class comment on ZombieAttackPlant in
    /// GameHooks.cs, cannot trust <c>damageFrom</c> to name the true attacker for a melee hit. No-ops
    /// (no exception, no fallback to the target's own ptr) when no ambient attacker is active, when
    /// the ambient is stale past its own frame (its Postfix never ran — e.g. the patched method
    /// threw), or when a single-target hook (AttackPlant's own Prefix) already claimed this exact
    /// target this frame — the ambient path exists only to cover targets that hook never sees, and
    /// must never double-record one it already did.
    /// </summary>
    public static bool TryRecordAmbientMeleeDealt(byte targetSide, IntPtr targetPtr, int targetTypeId, int damage)
    {
        if (_ambientMeleeAttackerPtr == IntPtr.Zero) return false;
        var frame = SafeFrame();
        if (_ambientMeleeAttackerFrame != frame) return false;
        if (_meleePairsFrame == frame && _meleePairsByTarget.ContainsKey(targetPtr)) return false;
        return TryRecordMeleeDealt(
            targetSide, _ambientMeleeAttackerPtr, _ambientMeleeAttackerTypeId, targetPtr, targetTypeId, damage);
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
    /// CAP RATIONALE (ssot-power-scale.md §11 / tunables-ssot.md): the clamp below is a STRUCTURAL
    /// per-frame cap, not a progression ceiling and not a balance tunable — it bounds how long the
    /// drain may hold the Unity main thread inside one frame, is derived from that frame's own
    /// measured time, and exhausting it carries records to the next frame (G5: delayed effects,
    /// never frame drops). Hardcoded on purpose; it has no `data/tuning` row.
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
        EndAmbientMeleeAttacker();
    }
}
