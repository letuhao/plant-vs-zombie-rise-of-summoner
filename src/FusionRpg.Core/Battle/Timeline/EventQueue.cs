namespace FusionRpg.Core.Battle.Timeline;

/// <summary>
/// One scheduled occurrence. <c>Kind</c> and <c>Tag</c> are opaque integers here — the FSM (T2)
/// assigns their meaning, which is what keeps this module testable with no game attached.
/// </summary>
public readonly record struct ScheduledEvent(long DueTick, long Seq, string OwnerKey, int Kind, long Tag);

/// <summary>
/// Identifies a scheduled event for cancel/reschedule. Stable for the event's lifetime.
///
/// Carries the id of the queue that issued it: handles are bare integers, and without an owner a
/// handle from one queue silently addresses a <i>different</i> event in another — cancelling
/// something unrelated with no error. One queue per battle and per replay makes that a real
/// confusion, so the queue rejects foreign handles loudly instead.
/// </summary>
public readonly record struct EventHandle(long QueueId, long Seq);

/// <summary>
/// The Future Event List: a binary heap ordered by <c>(DueTick, Seq)</c>.
///
/// Two ordering rules are load-bearing for byte-identical replay:
/// <list type="bullet">
/// <item>Cancellation uses tombstones and never renumbers <c>Seq</c>, so ordering cannot become
/// dependent on cancellation history.</item>
/// <item><see cref="Reschedule"/> <b>preserves</b> <c>Seq</c>. Implemented as cancel+insert with a
/// fresh sequence, a delay effect would silently reorder unrelated actors.</item>
/// </list>
/// </summary>
public sealed class EventQueue
{
    static long _nextQueueId;

    readonly long _queueId = System.Threading.Interlocked.Increment(ref _nextQueueId);
    readonly List<ScheduledEvent> _heap = new();
    /// <summary>Seqs that will never fire again — cancelled OR already fired.</summary>
    readonly HashSet<long> _retired = new();
    readonly Dictionary<long, long> _rescheduled = new();   // seq → newest DueTick
    long _nextSeq;

    /// <summary>Live (non-tombstoned) events still queued.</summary>
    public int Count { get; private set; }

    public EventHandle Schedule(long dueTick, string ownerKey, int kind, long tag)
    {
        var seq = _nextSeq++;
        Push(new ScheduledEvent(dueTick, seq, ownerKey, kind, tag));
        Count++;
        return new EventHandle(seq);
    }

    /// <summary>Tombstones the event. Returns false if it already fired or never existed.</summary>
    public bool Cancel(EventHandle handle)
    {
        if (!IsLive(handle.Seq)) return false;
        _cancelled.Add(handle.Seq);
        _rescheduled.Remove(handle.Seq);
        Count--;
        return true;
    }

    /// <summary>
    /// Moves a pending event to a new due tick, keeping its <c>Seq</c> so tie-break order with
    /// unrelated events is untouched.
    /// </summary>
    public bool Reschedule(EventHandle handle, long newDueTick)
    {
        if (!IsLive(handle.Seq)) return false;

        // Find the live entry, re-push it at the new tick with the SAME seq, and tombstone the
        // stale copy by recording the authoritative tick.
        for (var i = 0; i < _heap.Count; i++)
        {
            if (_heap[i].Seq != handle.Seq) continue;
            var e = _heap[i];
            _rescheduled[handle.Seq] = newDueTick;
            Push(e with { DueTick = newDueTick });
            return true;
        }

        return false;
    }

    /// <summary>The earliest live due tick, or null when nothing is queued.</summary>
    public long? PeekDueTick()
    {
        while (_heap.Count > 0)
        {
            var top = _heap[0];
            if (IsStale(top)) { PopRoot(); continue; }
            return top.DueTick;
        }

        return null;
    }

    /// <summary>
    /// Drains every live event with <c>DueTick &lt;= now</c> into a caller-owned buffer, in
    /// <c>(DueTick, Seq)</c> order. The buffer is appended to, never cleared — callers own their
    /// scratch (the same contract as <c>ShieldRuntime.DrainEvents</c>).
    /// </summary>
    public int PopDue(long now, List<ScheduledEvent> into)
    {
        if (into == null) throw new ArgumentNullException(nameof(into));
        var drained = 0;
        while (_heap.Count > 0)
        {
            var top = _heap[0];
            if (IsStale(top)) { PopRoot(); continue; }
            if (top.DueTick > now) break;
            PopRoot();
            _cancelled.Add(top.Seq);      // fired: further cancel/reschedule is a no-op
            _rescheduled.Remove(top.Seq);
            into.Add(top);
            Count--;
            drained++;
        }

        return drained;
    }

    bool IsLive(long seq) => seq < _nextSeq && !_cancelled.Contains(seq);

    /// <summary>A heap entry is stale if cancelled/fired, or superseded by a reschedule.</summary>
    bool IsStale(ScheduledEvent e) =>
        _cancelled.Contains(e.Seq) ||
        (_rescheduled.TryGetValue(e.Seq, out var due) && due != e.DueTick);

    // ---- binary heap on (DueTick, Seq) ----

    static bool Less(in ScheduledEvent a, in ScheduledEvent b) =>
        a.DueTick != b.DueTick ? a.DueTick < b.DueTick : a.Seq < b.Seq;

    void Push(ScheduledEvent e)
    {
        _heap.Add(e);
        var i = _heap.Count - 1;
        while (i > 0)
        {
            var parent = (i - 1) / 2;
            if (!Less(_heap[i], _heap[parent])) break;
            (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
            i = parent;
        }
    }

    void PopRoot()
    {
        var last = _heap.Count - 1;
        _heap[0] = _heap[last];
        _heap.RemoveAt(last);
        var i = 0;
        while (true)
        {
            int l = 2 * i + 1, r = l + 1, smallest = i;
            if (l < _heap.Count && Less(_heap[l], _heap[smallest])) smallest = l;
            if (r < _heap.Count && Less(_heap[r], _heap[smallest])) smallest = r;
            if (smallest == i) break;
            (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
            i = smallest;
        }
    }
}
