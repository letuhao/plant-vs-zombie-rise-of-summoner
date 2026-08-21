namespace FusionRpg.Core.Demons.Patron;

/// <summary>
/// Process-local patron cache — the injector fills it from the server's `patron.aura` command
/// (and the server/SIM fill it directly). Read by the PatronSecondaryPlugin at match start and
/// by the injector's combat compose. Match-scoped activity is flagged by the plugin lifecycle,
/// never by wall time, so a mid-match switch changes nothing until the next match (spec lock 2).
/// </summary>
public static class PatronRuntimeState
{
    static readonly object Gate = new();
    static PatronAura? _aura;
    static long _playerId;
    static bool _matchActive;

    public static void Set(long playerId, PatronAura? aura)
    {
        lock (Gate)
        {
            _playerId = playerId;
            _aura = aura;
        }
    }

    public static bool TryGet(long playerId, out PatronAura aura)
    {
        lock (Gate)
        {
            // playerId 0 = caller has no player context (injector before hello) — match any.
            if (_aura != null && (playerId == 0 || _playerId == 0 || _playerId == playerId))
            {
                aura = _aura;
                return true;
            }
        }

        aura = null!;
        return false;
    }

    /// <summary>Set by the plugin when its match grant lands; cleared on match teardown.
    /// The combat compose applies the aura ONLY while this is true — grant lifecycle is the
    /// single source of in-match truth.</summary>
    public static bool MatchActive
    {
        get { lock (Gate) return _matchActive; }
        set { lock (Gate) _matchActive = value; }
    }

    /// <summary>The aura the running match granted (frozen at match start — a switch mid-match
    /// updates the designation cache but never the live match).</summary>
    static PatronAura? _matchAura;

    public static PatronAura? MatchAura
    {
        get { lock (Gate) return _matchActive ? _matchAura : null; }
    }

    public static void BeginMatch(PatronAura aura)
    {
        lock (Gate)
        {
            _matchAura = aura;
            _matchActive = true;
        }
    }

    public static void EndMatch()
    {
        lock (Gate)
        {
            _matchActive = false;
            _matchAura = null;
        }
    }
}
