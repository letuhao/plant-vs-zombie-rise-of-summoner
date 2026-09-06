namespace FusionRpg.Core.Match;

/// <summary>
/// Injector session cache for the lawn-deploy roster snapshot — populated by background REST refresh,
/// read synchronously at <c>board.start</c> (never await HTTP inside <c>MatchHost.Apply</c>). Mirrors
/// <see cref="Commanders.MatchCommanderSessionCache"/>'s own shape exactly.
/// </summary>
public static class LawnDeployRosterSessionCache
{
    static readonly object Gate = new();
    static bool _hasCache;
    static IReadOnlyList<LawnDeployRosterEntry> _eligible = Array.Empty<LawnDeployRosterEntry>();
    static long _cacheRevision;

    public static long CacheRevision
    {
        get { lock (Gate) return _cacheRevision; }
    }

    /// <param name="eligible">Already resolved by the caller (Patron excluded) — this cache only
    /// stores, it never re-derives eligibility, matching <c>MatchCommanderSessionCache.Apply</c>'s own
    /// "caller resolves, cache stores" split.</param>
    public static void Apply(IReadOnlyList<LawnDeployRosterEntry> eligible)
    {
        if (eligible == null) throw new ArgumentNullException(nameof(eligible));
        lock (Gate)
        {
            _eligible = eligible;
            _hasCache = true;
            checked { _cacheRevision++; }
        }
    }

    public static LawnDeployRosterSnapshot BuildFromSessionCache()
    {
        lock (Gate)
        {
            if (!_hasCache) return LawnDeployRosterSnapshot.Empty;
            return new LawnDeployRosterSnapshot(_eligible, _cacheRevision);
        }
    }

    /// <summary>Tests only — reset poll state.</summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _hasCache = false;
            _eligible = Array.Empty<LawnDeployRosterEntry>();
            _cacheRevision = 0;
        }
    }
}
