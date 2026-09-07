using System.Text.Json;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Delve.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// D2.16 — the Server-layer registry of live <see cref="DelveBattleSession"/>s, and the ONLY place
/// this program derives the match key / correlation id spec-delve-battle-profile.md §4b names for a
/// delve-room fight: <c>correlation delve:{delveId}:{r}:{c}:p{partyIndex}</c>,
/// <c>matchKey delve-{delveId}-{r}-{c}-p{partyIndex}</c> (colon-free, "same reason" as an expedition's
/// own <c>exp:{id}:{n}</c> / dash form).
///
/// <para><b>What this class does NOT do, named honestly.</b> It does not decide which room's
/// <see cref="BattleSetup"/> a party is currently fighting, and it does not build a competent
/// automated policy — both are genuinely separate, still-unbuilt content-wiring tasks (zero production
/// callers of <c>DelveBattle.Run</c>/<c>Encounter.Build</c> exist anywhere today, confirmed by a direct
/// search this session; the spec's own §3 names a real "siege-ai-class policy" as a CONSUMED
/// dependency, and `SiegeAi.PlayedSide` is never set in production either). <see cref="StartSession"/>
/// takes both as parameters — matching <c>DelveBattle.Run</c>'s own shape exactly — for whichever
/// future "a party arrived at a fight room" trigger calls it.</para>
///
/// <para><b>No new persisted "currently steered party" column.</b> <c>rpg_delves</c> has no such field
/// and adding one is a schema change this task does not need: <see cref="Steer"/> takes BOTH
/// <c>fromPartyIndex</c> and <c>toPartyIndex</c> explicitly from the caller (the web client already
/// knows which party tab is selected), so the server never needs to remember it between calls.</para>
/// </summary>
public sealed class DelveBattleSessionManager
{
    readonly RpgStore _store;
    readonly BattleSessionRegistry _registry = new();

    readonly object _gate = new();
    readonly Dictionary<string, DelveBattleSession> _sessions = new(StringComparer.Ordinal);
    // (delveId, partyIndex) -> the one matchKey currently live/frozen for that party, so Steer/
    // OnDisconnected can find "whatever this party (or this connection) was fighting" without the
    // caller re-deriving the room coordinates.
    readonly Dictionary<(long DelveId, int PartyIndex), string> _activeMatchKeyForParty = new();
    readonly Dictionary<string, string> _matchKeyForConnection = new(StringComparer.Ordinal);

    public DelveBattleSessionManager(RpgStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    public static string MatchKeyFor(long delveId, int row, int col, int partyIndex) =>
        $"delve-{delveId}-{row}-{col}-p{partyIndex}";

    public static string CorrelationFor(long delveId, int row, int col, int partyIndex) =>
        $"delve:{delveId}:{row}:{col}:p{partyIndex}";

    /// <summary>The two id shapes are position-for-position identical (spec §4b: "colon-free, same
    /// reason") — every field in both is a plain non-negative integer or the literal prefixes
    /// `delve`/`p`, none of which ever contain a dash, so a genuine resume (built from a persisted
    /// matchKey alone, e.g. after a server restart with no in-memory session left) recovers the
    /// correlation id with a plain character swap rather than needing a second stored column.</summary>
    internal static string CorrelationFromMatchKey(string matchKey) => matchKey.Replace('-', ':');

    static (long DelveId, int PartyIndex) ParseMatchKey(string matchKey)
    {
        // "delve-{delveId}-{r}-{c}-p{partyIndex}"
        var parts = matchKey.Split('-');
        if (parts.Length != 5 || parts[0] != "delve" || parts[4].Length < 2 || parts[4][0] != 'p')
            throw new ArgumentException($"not a delve battle match key: '{matchKey}'", nameof(matchKey));
        return (long.Parse(parts[1]), int.Parse(parts[4][1..]));
    }

    public DelveBattleSession? Find(string matchKey)
    {
        lock (_gate) return _sessions.TryGetValue(matchKey, out var s) ? s : null;
    }

    public DelveBattleSession? FindActiveForParty(long delveId, int partyIndex)
    {
        lock (_gate)
            return _activeMatchKeyForParty.TryGetValue((delveId, partyIndex), out var key) && _sessions.TryGetValue(key, out var s)
                ? s : null;
    }

    void Register(DelveBattleSession session, string? connectionId)
    {
        lock (_gate)
        {
            _sessions[session.MatchKey] = session;
            _activeMatchKeyForParty[(session.DelveId, session.PartyIndex)] = session.MatchKey;
            if (connectionId is not null) _matchKeyForConnection[connectionId] = session.MatchKey;
        }
    }

    /// <summary>A connection is now the one steering <paramref name="matchKey"/> — recorded so
    /// <see cref="FreezeByConnection"/> (the hub's <c>OnDisconnectedAsync</c>) can freeze the right
    /// fight without the disconnect handler re-deriving room coordinates itself.</summary>
    public void TrackConnection(string connectionId, string matchKey)
    {
        lock (_gate) _matchKeyForConnection[connectionId] = matchKey;
    }

    /// <summary>
    /// Starts a fresh session for a room's fight, OR — if a row for this exact correlation already
    /// exists (a retried "start" call, or a session that outlived an in-memory eviction) — rehydrates
    /// and resumes it, exactly like <see cref="Resume"/> does. This mirrors
    /// <c>WebMatchService.RunWebMatchAsync</c>'s own "the atomic append IS the replay gate" discipline:
    /// two concurrent starts for the same room+party can never fork two independent battles.
    /// </summary>
    public DelveBattleSession? StartSession(
        long delveId, int row, int col, int partyIndex, long playerId,
        BattleSetup setup, ulong seed, IIntentSource automated,
        ActionCatalog? actionCatalog = null, IContainerEffectResolver? containerResolver = null,
        string? connectionId = null)
    {
        var matchKey = MatchKeyFor(delveId, row, col, partyIndex);
        var correlationId = CorrelationFor(delveId, row, col, partyIndex);

        var (created, entry) = _store.AppendWebMatchLog(
            playerId, correlationId, matchKey, JsonSerializer.Serialize(setup), seed,
            BattleRuleset.EngineVersion, BattleRuleset.RulesetVersion, SeededRng.RngAlgoVersion,
            BattleEnvironment.Stamp, _store.ComputeContentHash().ToCompact(),
            profileId: FusionRpg.Core.Battle.Timeline.BattleModeProfileCatalog.DelveId);

        DecisionTrace trace;
        BattleSetup effectiveSetup;
        ulong effectiveSeed;
        if (created)
        {
            trace = new DecisionTrace();
            effectiveSetup = setup;
            effectiveSeed = seed;
        }
        else
        {
            // Already logged (a retry, or a still-open session) -- the STORED row is authoritative,
            // matching RunPlannedMatchAsync's own "on replay the STORED setup wins" precedent.
            if (entry.RunId is not null) return null;   // already finished and ingested -- nothing to (re)start
            var rehydrated = DecisionTrace.FromJson(entry.DecisionsJson);
            trace = rehydrated ?? new DecisionTrace();
            var storedSetup = JsonSerializer.Deserialize<BattleSetup>(entry.SetupJson);
            if (storedSetup is null) return null;
            effectiveSetup = storedSetup;
            effectiveSeed = entry.Seed;
        }

        _registry.Close(matchKey); // clear any stale bookkeeping so Start()'s own Open never throws
        var steeredKeys = RaidIntentSource.KeysForParty(effectiveSetup, partyIndex);
        var session = new DelveBattleSession(
            matchKey, delveId, partyIndex, playerId, effectiveSetup, effectiveSeed, trace,
            automated, steeredKeys, _registry, actionCatalog, containerResolver,
            onDecisionPersisted: t => _store.WriteWebMatchDecisions(entry.Id, t.ToJson()),
            onFrozen: payload => AppendSteerLogEntry(delveId, payload));
        session.Start();
        Register(session, connectionId);
        return session;
    }

    /// <summary>
    /// Rebuilds a session from the persisted <c>(setup_json, seed, decisions_json)</c> row after a
    /// freeze — a BRAND NEW session, never a reattachment to a still-running background <c>Task</c>
    /// (spec §4b, verbatim). Reuses the still-open in-memory trace object when one exists (the same
    /// process never restarted) so the registry's own bookkeeping and this session's `Trace` never
    /// diverge into two objects describing the same battle; rehydrates from JSON only when no
    /// in-memory session survived (a real restart, or an evicted entry).
    /// </summary>
    public DelveBattleSession? Resume(
        string matchKey, long playerId, IIntentSource automated,
        ActionCatalog? actionCatalog = null, IContainerEffectResolver? containerResolver = null,
        string? connectionId = null)
    {
        var correlationId = CorrelationFromMatchKey(matchKey);
        var entry = _store.TryGetWebMatchLog(playerId, correlationId);
        if (entry is null || entry.RunId is not null) return null;

        var existing = _registry.Find(matchKey);
        DecisionTrace trace;
        if (existing is { State: BattleSessionState.Disconnected } && existing.PlayerId == playerId)
        {
            trace = existing.Trace; // same process -- reuse, never fork a second trace object
        }
        else
        {
            var rehydrated = DecisionTrace.FromJson(entry.DecisionsJson);
            if (rehydrated is null) return null; // spec §9: an absent/incomplete trace refuses, never re-resolves blind
            trace = rehydrated;
        }

        var setup = JsonSerializer.Deserialize<BattleSetup>(entry.SetupJson);
        if (setup is null) return null;

        var (delveId, partyIndex) = ParseMatchKey(matchKey);
        var steeredKeys = RaidIntentSource.KeysForParty(setup, partyIndex);

        _registry.Close(matchKey);
        var session = new DelveBattleSession(
            matchKey, delveId, partyIndex, playerId, setup, entry.Seed, trace,
            automated, steeredKeys, _registry, actionCatalog, containerResolver,
            onDecisionPersisted: t => _store.WriteWebMatchDecisions(entry.Id, t.ToJson()),
            onFrozen: payload => AppendSteerLogEntry(delveId, payload));
        session.Start();
        Register(session, connectionId);
        return session;
    }

    public bool Declare(string matchKey, string actorKey, string actionId, string? targetKey) =>
        Find(matchKey)?.Declare(actorKey, actionId, targetKey) ?? false;

    /// <summary>
    /// Steering moves from one party to another (or to none). If the FROM party has a live session,
    /// freezing it appends the `steer{from,to}` log entry as a side effect of the freeze itself (this
    /// session's own `onFrozen` hook) -- exactly one entry either way. If there is no live session for
    /// the FROM party (nobody was fighting there), this method appends the entry directly, since there
    /// is no session to delegate the append to.
    /// </summary>
    public void Steer(long delveId, int? fromPartyIndex, int? toPartyIndex)
    {
        var payload = LiveFreezeTrigger.SteerPayload(fromPartyIndex, toPartyIndex);
        var session = fromPartyIndex is { } from ? FindActiveForParty(delveId, from) : null;
        if (session is not null)
            session.Freeze(payload); // appends via onFrozen -- see above
        else
            AppendSteerLogEntry(delveId, payload);
    }

    /// <summary>The hub's own <c>OnDisconnectedAsync</c> — freezes whatever this connection was
    /// steering, recording the SAME implicit "nobody is steering now" shape a triple-timeout would.
    /// A connection that was never tracked (never called Steer/Declare) is a no-op.</summary>
    public void FreezeByConnection(string connectionId)
    {
        string? matchKey;
        lock (_gate)
        {
            if (!_matchKeyForConnection.TryGetValue(connectionId, out matchKey)) return;
            _matchKeyForConnection.Remove(connectionId);
        }

        if (Find(matchKey) is { } session)
            session.Freeze(LiveFreezeTrigger.FreezeAwayPayload(session.PartyIndex));
    }

    void AppendSteerLogEntry(long delveId, SteerLogPayload payload)
    {
        var delve = _store.LoadDelve(delveId);
        var seq = CountDecisions(delve?.DecisionsJson);
        // PartyIndex on the entry names the party this decision is fundamentally ABOUT -- the one
        // being switched (or frozen) away from; both the implicit-freeze and explicit-steer shapes
        // always carry a From, so this is unambiguous either way.
        _store.AppendDecision(delveId, DelveDecision.Create(seq, DelveDecisionKinds.Steer, payload.From, payload: payload));
    }

    static int CountDecisions(string? decisionsJson)
    {
        if (string.IsNullOrWhiteSpace(decisionsJson)) return 0;
        var list = JsonSerializer.Deserialize<List<JsonElement>>(decisionsJson);
        return list?.Count ?? 0;
    }
}
