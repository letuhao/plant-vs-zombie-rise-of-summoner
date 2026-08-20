using FusionRpg.Contracts;
using FusionRpg.Core.Diagnostics;

namespace FusionRpg.Core.Events;

public struct DrainStats
{
    public int Processed;
    public int Carried;
    public int Generations;
    public int ExpensiveDeferred;
    public long DroppedByDepth;
}

/// <summary>
/// Frame-budgeted processor for hot-tier event records — event-pipeline-v2 plan Tasks 6–8.
///
/// Storage model: the ring is the only store. Each Drain pass pops the pending set, coalesces
/// it (whole set — a dealt/taken pair is never split across windows), and processes records
/// under the caller's budget. Unprocessed remainder re-appends to the ring at the end of the
/// pass, before any future hook can record, so FIFO order survives frames. Records appended
/// *during* processing (Unity re-entry through action execution) form the next generation,
/// processed in the same pass up to <see cref="GenerationCap"/>, then carried.
///
/// Chain containment: records carry ChainDepth; while a record is being processed,
/// <see cref="RecordDepth"/> exposes parent+1 for hooks to stamp. Records at or beyond
/// <see cref="ChainDepthLimit"/> are refused at Record time — the mechanism is hard-coded
/// (spec decision #1); only the limit value is configurable, clamped 1..8.
///
/// Session mode: no budget, no coalescing, no expensive-class deferral — LIVE prove packs
/// see v1 event-for-event behavior.
/// </summary>
public sealed class EventDrain
{
    public const int DefaultChainDepthLimit = 6;
    public const int MinChainDepthLimit = 1;
    public const int MaxChainDepthLimit = 8;

    readonly GameEventRing _ring;
    readonly EventStringInterner _matchKeys = new();
    readonly EventStringInterner _grantIds = new();
    readonly Dictionary<IntPtr, string> _ptrHex = new();
    readonly Dictionary<GameEventKind, double> _costEma = new();
    readonly List<GameEventRec> _scratch = new();
    readonly Action<EffectEventDto> _process;
    readonly Func<long> _timestamp;

    int _chainDepthLimit = DefaultChainDepthLimit;
    byte _recordDepth;
    long _droppedByDepth;

    public EventDrain(Action<EffectEventDto> process, Func<long>? timestamp = null, int capacity = GameEventRing.DefaultCapacity)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _timestamp = timestamp ?? System.Diagnostics.Stopwatch.GetTimestamp;
        _ring = new GameEventRing(capacity);
    }

    public bool SessionMode { get; set; }
    public int GenerationCap { get; set; } = 3;

    /// <summary>A kind whose EMA cost exceeds this fraction of the budget is "expensive": max 1 per pass.</summary>
    public double ExpensiveBudgetFraction { get; set; } = 0.5;

    public int ChainDepthLimit
    {
        get => _chainDepthLimit;
        set => _chainDepthLimit = Math.Clamp(value, MinChainDepthLimit, MaxChainDepthLimit);
    }

    public int PendingCount => _ring.Count;
    public long DroppedByOverflow => _ring.Dropped;
    public long DroppedByDepth => _droppedByDepth;

    /// <summary>Depth to stamp on records generated while the drain is executing (0 outside a drain).</summary>
    public byte RecordDepth => _recordDepth;

    public long NextSeq() => _ring.NextSeq();
    public int InternMatchKey(string? key) => _matchKeys.Intern(key);
    public int InternGrantId(string? id) => _grantIds.Intern(id);

    public bool Record(in GameEventRec rec)
    {
        if (rec.ChainDepth >= _chainDepthLimit)
        {
            _droppedByDepth++;
            return false;
        }
        return _ring.TryAppend(rec);
    }

    /// <summary>
    /// Per-frame drain. <paramref name="budgetTicks"/> in timestamp units (caller computes
    /// 10% of frame budget). Ignored in session mode.
    /// </summary>
    public DrainStats Drain(long budgetTicks)
    {
        var stats = default(DrainStats);
        var start = _timestamp();
        var expensiveUsed = false;

        while (_ring.Count > 0 && stats.Generations <= GenerationCap)
        {
            var window = SessionMode ? PassthroughWindow() : EventCoalescer.Window(_ring);
            stats.Generations++;

            var idx = 0;
            for (; idx < window.Count; idx++)
            {
                if (!SessionMode && stats.Processed > 0 && _timestamp() - start >= budgetTicks)
                    break;

                var rec = window[idx];
                if (!SessionMode && IsExpensive(rec.Kind, budgetTicks))
                {
                    if (expensiveUsed)
                    {
                        // Defer past this pass; cheap records behind it still process.
                        _scratch.Add(rec);
                        stats.ExpensiveDeferred++;
                        continue;
                    }
                    expensiveUsed = true;
                }

                Process(rec, ref stats);
            }

            // Budget exhausted mid-window: stash the remainder (order preserved).
            for (; idx < window.Count; idx++)
                _scratch.Add(window[idx]);

            if (_scratch.Count > 0) break; // carry set exists — stop consuming generations
            if (SessionMode) continue;     // session mode keeps draining re-entrant records
        }

        // Re-append carried records ahead of nothing (no producer runs between here and the
        // next frame's hooks), so FIFO order across frames is preserved.
        if (_scratch.Count > 0)
        {
            foreach (var rec in _scratch)
                _ring.TryAppend(rec);
            stats.Carried = _scratch.Count;
            _scratch.Clear();
        }

        stats.DroppedByDepth = _droppedByDepth;
        return stats;
    }

    /// <summary>
    /// Death/lifecycle barrier: process every pending record referencing <paramref name="ptr"/>
    /// now (unbudgeted, uncoalesced — bounded per death), keeping all others pending in order.
    /// Call before grant-withdraw so OnDeath-adjacent hits still see entity grants (SSOT §A2).
    /// </summary>
    public void FlushForPtr(IntPtr ptr)
    {
        if (_ring.Count == 0 || ptr == IntPtr.Zero) return;
        var stats = default(DrainStats);
        var n = _ring.Count;
        for (var i = 0; i < n && _ring.TryPop(out var rec); i++)
        {
            if (rec.ActorPtr == ptr || rec.TargetPtr == ptr)
                Process(rec, ref stats);
            else
                _scratch.Add(rec);
        }
        foreach (var rec in _scratch)
            _ring.TryAppend(rec);
        _scratch.Clear();
    }

    /// <summary>Lifecycle barrier (board end / match result): drain everything, then reset interned state.</summary>
    public void FlushAllAndReset()
    {
        var stats = default(DrainStats);
        while (_ring.TryPop(out var rec))
            Process(rec, ref stats);
        _matchKeys.Clear();
        _grantIds.Clear();
        _ptrHex.Clear();
    }

    List<GameEventRec> PassthroughWindow()
    {
        var list = new List<GameEventRec>(_ring.Count);
        while (_ring.TryPop(out var rec))
            list.Add(rec);
        return list;
    }

    bool IsExpensive(GameEventKind kind, long budgetTicks) =>
        budgetTicks > 0
        && _costEma.TryGetValue(kind, out var ema)
        && ema >= budgetTicks * ExpensiveBudgetFraction;

    void Process(in GameEventRec rec, ref DrainStats stats)
    {
        var t0 = _timestamp();
        _recordDepth = (byte)Math.Min(byte.MaxValue, rec.ChainDepth + 1);
        try
        {
            _process(ToDto(rec));
        }
        finally
        {
            _recordDepth = 0;
        }

        var cost = _timestamp() - t0;
        _costEma[rec.Kind] = _costEma.TryGetValue(rec.Kind, out var ema)
            ? ema * 0.8 + cost * 0.2
            : cost;
        stats.Processed++;
    }

    string? PtrHex(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return null;
        if (_ptrHex.TryGetValue(ptr, out var s)) return s;
        s = ptr.ToString("X");
        _ptrHex[ptr] = s;
        return s;
    }

    /// <summary>Mirror of EffectEventAdapterCore's hot-kind mappings, from values instead of dicts.</summary>
    EffectEventDto ToDto(in GameEventRec rec)
    {
        var matchKey = _matchKeys.Get(rec.MatchKeyIdx);
        var sourceGrant = _grantIds.Get(rec.SourceGrantIdx);
        switch (rec.Kind)
        {
            case GameEventKind.CombatHit:
            case GameEventKind.ChainSynthetic:
                // Dealt: DTO Side is the ATTACKER side — opposite of the record's target side.
                return new EffectEventDto
                {
                    Trigger = EffectTriggers.OnDamageDealt,
                    MatchKey = matchKey,
                    Side = rec.Side == GameEventSide.Zombie ? "plant" : "zombie",
                    ActorPtr = PtrHex(rec.ActorPtr),
                    TargetPtr = PtrHex(rec.TargetPtr),
                    TypeId = rec.TypeId,
                    TargetTypeId = rec.TargetTypeId,
                    Damage = (int)Math.Clamp(rec.Amount, int.MinValue, int.MaxValue),
                    HitCount = rec.HitCount,
                    ChainDepth = rec.ChainDepth,
                    SourceGrantId = sourceGrant,
                    Tick = rec.Seq
                };
            case GameEventKind.PlantDamage:
            case GameEventKind.ZombieDamage:
                return new EffectEventDto
                {
                    Trigger = EffectTriggers.OnDamageTaken,
                    MatchKey = matchKey,
                    Side = rec.Kind == GameEventKind.PlantDamage ? "plant" : "zombie",
                    ActorPtr = PtrHex(rec.ActorPtr),
                    TargetPtr = PtrHex(rec.TargetPtr),
                    TypeId = rec.TargetTypeId,
                    TargetTypeId = rec.TargetTypeId,
                    Damage = (int)Math.Clamp(rec.Amount, int.MinValue, int.MaxValue),
                    HitCount = rec.HitCount,
                    ChainDepth = rec.ChainDepth,
                    SourceGrantId = sourceGrant,
                    Tick = rec.Seq
                };
            case GameEventKind.BulletInit:
                return new EffectEventDto
                {
                    Trigger = EffectTriggers.OnSpawn,
                    MatchKey = matchKey,
                    Side = "bullet",
                    ActorPtr = PtrHex(rec.ActorPtr),
                    TypeId = rec.TypeId,
                    HitCount = rec.HitCount,
                    Tick = rec.Seq
                };
            case GameEventKind.StatusHook:
            default:
                return new EffectEventDto
                {
                    Trigger = EffectTriggers.OnDamageTaken,
                    MatchKey = matchKey,
                    Side = rec.Side == GameEventSide.Plant ? "plant" : "zombie",
                    ActorPtr = PtrHex(rec.ActorPtr),
                    TargetPtr = PtrHex(rec.TargetPtr),
                    TypeId = rec.TypeId,
                    TargetTypeId = rec.TargetTypeId,
                    HitCount = rec.HitCount,
                    Tick = rec.Seq
                };
        }
    }
}
