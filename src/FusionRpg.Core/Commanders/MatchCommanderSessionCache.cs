using FusionRpg.Core.Stats.Aptitudes;

namespace FusionRpg.Core.Commanders;

/// <summary>
/// Injector session cache for commander snapshot data — populated by background REST refresh,
/// read synchronously at <c>board.start</c> (never await HTTP inside <c>MatchHost.Apply</c>).
/// </summary>
public static class MatchCommanderSessionCache
{
    static readonly object Gate = new();
    static bool _hasCache;
    static string _defaultId = CommanderIds.ToStableId(CommanderId.Dave);
    static string _displayName = PlayerEmpireCommanders.DisplayName(CommanderId.Dave);
    static string? _activeAuraId;
    static string? _activeAuraName;
    static AptitudeAllocation _allocation = AptitudeAllocation.Empty;
    static long _cacheRevision;

    /// <summary>True when the last <see cref="BuildFromSessionCache"/> used Dave fallback.</summary>
    public static bool LastBuildUsedFallback { get; private set; }

    public static long CacheRevision
    {
        get { lock (Gate) return _cacheRevision; }
    }

    public static void Apply(
        string defaultLawnCommanderId,
        string leadingDisplayName,
        string? activeAuraId,
        string? activeAuraName,
        AptitudeAllocation allocation)
    {
        if (string.IsNullOrWhiteSpace(defaultLawnCommanderId)
            || !CommanderIds.TryParseStableId(defaultLawnCommanderId, out _))
            return;

        lock (Gate)
        {
            _defaultId = defaultLawnCommanderId.Trim();
            _displayName = string.IsNullOrWhiteSpace(leadingDisplayName)
                ? PlayerEmpireCommanders.DisplayName(CommanderId.Dave)
                : leadingDisplayName;
            _activeAuraId = activeAuraId;
            _activeAuraName = activeAuraName;
            _allocation = allocation;
            _hasCache = true;
            checked { _cacheRevision++; }
        }
    }

    public static MatchCommanderSnapshot BuildFromSessionCache()
    {
        lock (Gate)
        {
            if (!_hasCache || !CommanderIds.TryParseStableId(_defaultId, out _))
            {
                LastBuildUsedFallback = true;
                return DaveFallback(revision: 0);
            }

            LastBuildUsedFallback = false;
            var rev = _cacheRevision;
            return new MatchCommanderSnapshot(
                _defaultId,
                _displayName,
                _activeAuraId,
                _activeAuraName,
                _allocation,
                rev,
                rev);
        }
    }

    /// <summary>Tests only — reset poll state.</summary>
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _hasCache = false;
            _defaultId = CommanderIds.ToStableId(CommanderId.Dave);
            _displayName = PlayerEmpireCommanders.DisplayName(CommanderId.Dave);
            _activeAuraId = null;
            _activeAuraName = null;
            _allocation = AptitudeAllocation.Empty;
            _cacheRevision = 0;
            LastBuildUsedFallback = false;
        }
    }

    static MatchCommanderSnapshot DaveFallback(long revision) =>
        new(
            CommanderIds.ToStableId(CommanderId.Dave),
            PlayerEmpireCommanders.DisplayName(CommanderId.Dave),
            null,
            null,
            AptitudeAllocation.Empty,
            revision,
            revision);
}
