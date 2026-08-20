namespace FusionRpg.Core.Events;

/// <summary>Hot-tier event kinds carried by the v2 ring — event-pipeline-v2-ssot.md §3.1.</summary>
public enum GameEventKind : byte
{
    None = 0,
    CombatHit = 1,      // combat.hit → OnDamageDealt
    PlantDamage = 2,    // plant.damage → OnDamageTaken
    ZombieDamage = 3,   // zombie.damage → OnDamageTaken
    BulletInit = 4,     // bullet.init → OnSpawn (recorded only when an OnSpawn grant is live)
    StatusHook = 5,     // zombie.status game hooks (freeze/cold/poison/butter)
    ChainSynthetic = 6  // drain-generated (overlay procs, counter bursts)
}

/// <summary>Actor side for grant filter resolution.</summary>
public static class GameEventSide
{
    public const byte None = 0;
    public const byte Plant = 1;
    public const byte Zombie = 2;
    public const byte Bullet = 3;
}

/// <summary>
/// One recorded game event — spec §4b.6. Values only: IL2CPP object references are
/// use-after-free at drain time; strings ride as intern indices so the hook-side record
/// path never allocates.
/// </summary>
public readonly struct GameEventRec
{
    public readonly GameEventKind Kind;
    public readonly int Frame;
    public readonly long Seq;
    public readonly IntPtr ActorPtr;
    public readonly IntPtr TargetPtr;
    public readonly int TypeId;
    public readonly int TargetTypeId;
    public readonly byte Side;
    public readonly long Amount;        // summed when coalesced
    public readonly short HitCount;     // proc-math multiplier (≥1)
    public readonly byte ChainDepth;    // >0 → never coalesce
    public readonly int SourceGrantIdx; // interned grant id; -1 = none; set → never coalesce
    public readonly int MatchKeyIdx;    // interned at record time, never drain time (audit §4c.1)
    public readonly int PairId;         // dealt/taken causal pair of one physical hit; 0 = none

    public GameEventRec(
        GameEventKind kind,
        int frame,
        long seq,
        IntPtr actorPtr,
        IntPtr targetPtr,
        int typeId,
        int targetTypeId,
        byte side,
        long amount,
        short hitCount,
        byte chainDepth,
        int sourceGrantIdx,
        int matchKeyIdx,
        int pairId)
    {
        Kind = kind;
        Frame = frame;
        Seq = seq;
        ActorPtr = actorPtr;
        TargetPtr = targetPtr;
        TypeId = typeId;
        TargetTypeId = targetTypeId;
        Side = side;
        Amount = amount;
        HitCount = hitCount < 1 ? (short)1 : hitCount;
        ChainDepth = chainDepth;
        SourceGrantIdx = sourceGrantIdx;
        MatchKeyIdx = matchKeyIdx;
        PairId = pairId;
    }

    public bool IsCoalescible => ChainDepth == 0 && SourceGrantIdx < 0;
}

/// <summary>
/// Append-only string interner for record fields (matchKey, grantId). Main-thread only,
/// like every producer. Cleared at match boundaries so indices never dangle across runs.
/// </summary>
public sealed class EventStringInterner
{
    readonly Dictionary<string, int> _toIdx = new(StringComparer.Ordinal);
    readonly List<string> _values = new();

    public int Intern(string? value)
    {
        if (string.IsNullOrEmpty(value)) return -1;
        if (_toIdx.TryGetValue(value, out var idx)) return idx;
        idx = _values.Count;
        _values.Add(value);
        _toIdx[value] = idx;
        return idx;
    }

    public string? Get(int idx) => idx >= 0 && idx < _values.Count ? _values[idx] : null;

    public int Count => _values.Count;

    public void Clear()
    {
        _toIdx.Clear();
        _values.Clear();
    }
}
