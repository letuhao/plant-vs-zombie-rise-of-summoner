namespace FusionRpg.Core.Events;

/// <summary>
/// Fixed-capacity FIFO for hot-tier event records — event-pipeline-v2 plan Task 4.
/// Single-threaded by contract (every producer and the drain run on the game's main thread);
/// appending while a drain is popping is safe — new records land behind the drain cursor and
/// are seen by the same or next drain pass. Overflow drops the incoming record with a counter:
/// the hot tier carries only effect triggers (never lifecycle, membership, or XP events, which
/// bypass the ring entirely), so a drop degrades procs under extreme backlog, never state.
/// </summary>
public sealed class GameEventRing
{
    public const int DefaultCapacity = 4096;

    readonly GameEventRec[] _buf;
    int _head;
    int _count;
    long _dropped;
    long _seq;

    public GameEventRing(int capacity = DefaultCapacity)
    {
        if (capacity < 16) capacity = 16;
        _buf = new GameEventRec[capacity];
    }

    public int Count => _count;
    public int Capacity => _buf.Length;
    public long Dropped => _dropped;

    /// <summary>Monotonic record ordinal — assign to <see cref="GameEventRec.Seq"/> at record time.</summary>
    public long NextSeq() => ++_seq;

    public bool TryAppend(in GameEventRec rec)
    {
        if (_count >= _buf.Length)
        {
            _dropped++;
            return false;
        }

        var tail = _head + _count;
        if (tail >= _buf.Length) tail -= _buf.Length;
        _buf[tail] = rec;
        _count++;
        return true;
    }

    public bool TryPop(out GameEventRec rec)
    {
        if (_count == 0)
        {
            rec = default;
            return false;
        }

        rec = _buf[_head];
        _head++;
        if (_head >= _buf.Length) _head = 0;
        _count--;
        return true;
    }

    public bool TryPeek(out GameEventRec rec)
    {
        if (_count == 0)
        {
            rec = default;
            return false;
        }

        rec = _buf[_head];
        return true;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
    }
}
