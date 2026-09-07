using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Delve.Battle;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Server;

/// <summary>
/// D2.16 (spec-delve-battle-profile.md §3-4, party-dungeon-todo.md's 2026-09-08 correction) — one
/// live, steered delve-room battle over a dedicated background <see cref="Task"/>.
///
/// <para><b>Freeze is cancel-and-discard, never pause-and-resume.</b> `BattleEngine.cs` carries zero
/// `catch` blocks anywhere in its own call stack down to <see cref="InteractiveIntentSource"/>'s live
/// `_ask` delegate (<see cref="NoCatchInLiveBattleCallStackTests"/> — a real Core.Tests architecture
/// guard, referenced here in a doc comment only, this file lives in Server) — so an
/// <see cref="OperationCanceledException"/> thrown from inside <see cref="Ask"/>, uncaught, propagates
/// straight out of the in-flight <c>BattleEngine.Resolve</c> call: the simulation is abandoned
/// mid-loop and NO <see cref="BattleReport"/> is ever produced for that attempt — this IS
/// "no finish-on-autopilot," a structural guarantee, not an approximation of one. Resume is always a
/// **fresh** <see cref="DelveBattleSession"/> over a rehydrated <see cref="DecisionTrace"/>, matching
/// spec-delve-battle-profile.md §4b's own words verbatim ("Resume re-runs `Resolve(setup, seed, …)`
/// from the row's `setup_json`") — never a reattachment to a still-running background <see cref="Task"/>.
///
/// <para><b>Always resumes, never has two live-source shapes.</b> Every session — a brand-new one and
/// a genuine reconnect alike — is built with
/// <see cref="InteractiveIntentSource.ResumeReplayThenLive"/> against a <see cref="DecisionTrace"/>
/// that is either fresh and empty (a new session — <c>ReplayExhausted</c> is true for an empty trace,
/// so it goes live on the very first ask, proven by <c>InteractiveTurnsTests
/// .ResumeWithAnAlreadyExhaustedTraceGoesLiveImmediately</c>) or rehydrated from a persisted row (a
/// real resume, replaying the recorded prefix first). One code path, not two, for the identical reason
/// this program already prefers "one seam, not a branch" elsewhere.</para>
///
/// <para><b>The pure freeze-trigger decision lives in Core</b> (<see cref="LiveFreezeTrigger"/>) —
/// this class owns only the threading/store-facing mechanics: blocking the background thread on a
/// <see cref="TaskCompletionSource{TResult}"/> for up to the dwell window, waking it early via a
/// <see cref="CancellationTokenSource"/> on a genuine freeze signal, and calling
/// <see cref="BattleSessionRegistry.Disconnect"/> plus the delve-level log append when one fires.</para>
/// </summary>
public sealed class DelveBattleSession
{
    /// <summary>T6's own dwell window (spec-delve-battle-profile.md §Tunables: "dwell `inputWindowMs`
    /// ... 1500 ... no tuning file carries them and no code reads them today — an ask on T6/T11, not
    /// owned here"). Kept as the same structural placeholder that spec already names rather than
    /// inventing a second, differently-sourced default — wiring it into a real tuning file is T6/T11's
    /// own open ask, not this task's.</summary>
    public const int DefaultDwellWindowMs = 1500;

    public string MatchKey { get; }
    public long DelveId { get; }
    public int PartyIndex { get; }
    public long PlayerId { get; }

    /// <summary>The decision trace this session reads from (on resume) and writes to (live). Exposed
    /// so a caller's <c>onDecisionPersisted</c> closure can serialise it after every new decision —
    /// matching spec §4b's own "called after every `DecisionTrace.Record`, with
    /// `DecisionTrace.ToJson()`" wording exactly.</summary>
    public DecisionTrace Trace { get; }

    public bool Frozen { get; private set; }

    /// <summary>The actor currently occupying the dwell window, or <c>null</c> when nobody is being
    /// asked right now (between turns, before <see cref="Start"/>, or after the run finished/froze).
    /// Real, useful diagnostic surface (the natural answer to "who is this fight waiting on" —
    /// <see cref="DelveBattleEndpoints"/>'s own status read exposes it) and what lets a test drive
    /// <see cref="Declare"/> reactively instead of guessing the engine's own turn order.</summary>
    public string? PendingActorKey { get { lock (_gate) return _pendingActorKey; } }

    /// <summary>The background resolve. <c>RanToCompletion</c> means a real <see cref="BattleReport"/>
    /// exists (the Task's own <c>Result</c>); <c>Canceled</c> means this session froze and no report
    /// was ever produced — the cancel-and-discard contract this whole class exists for.</summary>
    public Task<BattleReport>? RunTask { get; private set; }

    readonly BattleSetup _setup;
    readonly ulong _seed;
    readonly ActionCatalog? _actionCatalog;
    readonly IContainerEffectResolver? _containerResolver;
    readonly IIntentSource _automated;
    readonly IReadOnlySet<string> _steeredKeys;
    readonly BattleSessionRegistry _registry;
    readonly LiveFreezeTrigger _freezeTrigger = new();
    readonly Action<DecisionTrace>? _onDecisionPersisted;
    readonly Action<SteerLogPayload>? _onFrozen;
    readonly int _dwellMs;
    readonly CancellationTokenSource _cts = new();

    readonly object _gate = new();
    TaskCompletionSource<PlayerChoice>? _pending;
    string? _pendingActorKey;

    /// <param name="onDecisionPersisted">Fires once, synchronously, right after every NEW decision
    /// (player or timeout) is recorded — never during the replayed prefix. The natural caller is
    /// `RpgStore.WriteWebMatchDecisions(entryId, trace.ToJson())` (D2.15's own already-shipped writer,
    /// its first real caller). Optional so a unit/integration test can omit persistence entirely.</param>
    /// <param name="onFrozen">Fires once, synchronously, the instant this session freezes — for
    /// whatever reason (three timeouts, an explicit steer-away, a dropped connection). The natural
    /// caller appends the payload to the delve-level log via `RpgStore.AppendDecision` (§4a). Optional
    /// for the same reason as <paramref name="onDecisionPersisted"/>.</param>
    public DelveBattleSession(
        string matchKey, long delveId, int partyIndex, long playerId,
        BattleSetup setup, ulong seed, DecisionTrace trace,
        IIntentSource automated, IReadOnlySet<string> steeredKeys,
        BattleSessionRegistry registry,
        ActionCatalog? actionCatalog = null, IContainerEffectResolver? containerResolver = null,
        Action<DecisionTrace>? onDecisionPersisted = null, Action<SteerLogPayload>? onFrozen = null,
        int dwellMs = DefaultDwellWindowMs)
    {
        MatchKey = string.IsNullOrWhiteSpace(matchKey) ? throw new ArgumentException("A session needs a match key.", nameof(matchKey)) : matchKey;
        DelveId = delveId;
        PartyIndex = partyIndex;
        PlayerId = playerId;
        _setup = setup ?? throw new ArgumentNullException(nameof(setup));
        _seed = seed;
        Trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _automated = automated ?? throw new ArgumentNullException(nameof(automated));
        _steeredKeys = steeredKeys ?? throw new ArgumentNullException(nameof(steeredKeys));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _actionCatalog = actionCatalog;
        _containerResolver = containerResolver;
        _onDecisionPersisted = onDecisionPersisted;
        _onFrozen = onFrozen;
        _dwellMs = dwellMs > 0 ? dwellMs : throw new ArgumentOutOfRangeException(nameof(dwellMs));
    }

    /// <summary>Starts the background resolve. Idempotent-unsafe by design — a caller starts a session
    /// exactly once; a genuine reconnect builds a NEW <see cref="DelveBattleSession"/> (see the class
    /// doc comment), never calls this twice on the same instance.</summary>
    public void Start()
    {
        _registry.Open(MatchKey, PlayerId, Trace);

        // A player's declared choice carries only an actionId + targetKey (never a full envelope --
        // see IIntentSource.TryDeclare's own contract), so InteractiveIntentSource must resolve it
        // through this delegate before it can accept the choice at all. The basic attack is the one
        // action every battle recognises with no authored content whatsoever (BattleEngine.
        // BasicAttackEnvelope is the engine's own "always legal" row, used identically when no
        // ActionCatalog is supplied at all) -- falling back to it here, when the real catalog has no
        // entry, matches that same behaviour rather than silently refusing a player's basic attack
        // whenever a delve room resolves with no compiled action content.
        ActionEnvelope? EnvelopeOf(string actionId) =>
            _actionCatalog?.Get(actionId)?.Envelope
            ?? (actionId == BattleEngine.BasicAttackEnvelope.ActionId ? BattleEngine.BasicAttackEnvelope : null);

        var interactive = InteractiveIntentSource.ResumeReplayThenLive(
            _automated, Ask, EnvelopeOf, Trace, OnRecorded);
        var raid = new RaidIntentSource(interactive, _automated, _steeredKeys);

        // The SAME CancellationToken is handed to Task.Run: when the delegate throws
        // OperationCanceledException FOR THIS TOKEN while it is cancelled, the TPL marks the
        // resulting Task Canceled rather than Faulted -- the exact status the freeze/cancel-and-
        // discard contract is proven against (RunTask.Status == TaskStatus.Canceled, never
        // RanToCompletion, and never silently swallowed).
        RunTask = Task.Run(
            () => Core.Delve.Battle.DelveBattle.Run(
                _setup, _seed, actionCatalog: _actionCatalog, containerResolver: _containerResolver,
                intentSource: raid),
            _cts.Token);

        _ = RunTask.ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion)
                _registry.Complete(MatchKey);
            // Canceled/Faulted: nothing further -- BattleSessionRegistry stays Disconnected (Freeze
            // already called it) or Live-but-never-completed if the process itself died, either way
            // MayWrite(MatchKey) correctly refuses to bless a battle nobody finished.
        }, TaskScheduler.Default);
    }

    /// <summary>
    /// Blocks the background resolve thread for up to the dwell window, or until
    /// <see cref="Declare"/> resolves this exact turn, or until <see cref="Freeze"/> cancels the
    /// session. Called synchronously, in-line, from deep inside <c>BattleEngine.Resolve</c>'s own
    /// single-threaded simulation loop — this is the ENTIRE mechanism; there is no polling anywhere.
    /// </summary>
    PlayerChoice Ask(string actorKey, long nowTick)
    {
        var tcs = new TaskCompletionSource<PlayerChoice>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate) { _pending = tcs; _pendingActorKey = actorKey; }
        try
        {
            // Task.WaitAny(tasks, timeout, token) throws OperationCanceledException the moment the
            // token is (or becomes) cancelled -- checked synchronously first, so a freeze that landed
            // BETWEEN turns (nobody currently blocked in this method) still aborts on the very next
            // call, immediately, never letting one more turn slip through on autopilot.
            var won = Task.WaitAny(new Task[] { tcs.Task }, _dwellMs, _cts.Token) == 0;
            return won ? tcs.Task.GetAwaiter().GetResult() : PlayerChoice.None;
        }
        finally
        {
            // A `finally`, never a `catch` -- cleanup only. An OperationCanceledException thrown above
            // still propagates through this method, through InteractiveIntentSource.TryDeclare,
            // through BattleEngine.Resolve's own call stack, uncaught, exactly as the freeze contract
            // requires (NoCatchInLiveBattleCallStackTests is the compile-time guard for the Core half
            // of that chain).
            lock (_gate) { if (ReferenceEquals(_pending, tcs)) { _pending = null; _pendingActorKey = null; } }
        }
    }

    /// <summary>
    /// A player's choice arrives over the wire (a SignalR hub call). Resolves the pending
    /// <see cref="Ask"/> wait for the CURRENTLY-asked actor only — a stale or misdirected declare for a
    /// different actor (or one that arrives after the dwell already elapsed, or after a freeze) is
    /// refused, never silently misapplied to the wrong turn.
    /// </summary>
    public bool Declare(string actorKey, string actionId, string? targetKey)
    {
        TaskCompletionSource<PlayerChoice>? pending;
        lock (_gate)
        {
            if (Frozen || !string.Equals(_pendingActorKey, actorKey, StringComparison.Ordinal)) return false;
            pending = _pending;
        }
        return pending is not null && pending.TrySetResult(new PlayerChoice(actionId, targetKey));
    }

    /// <summary>Fires once, synchronously, right after every new (non-replayed) decision — the
    /// incremental-persistence seam (§4b) plus this session's own consecutive-timeout counting
    /// (<see cref="LiveFreezeTrigger"/>, never <see cref="BattleSessionRegistry.NoteTurn"/> — see that
    /// class's own doc comment for why).</summary>
    void OnRecorded(TracedDecision decision)
    {
        _onDecisionPersisted?.Invoke(Trace);
        if (_freezeTrigger.OnDecisionRecorded(decision.Source))
            Freeze(LiveFreezeTrigger.FreezeAwayPayload(PartyIndex));
    }

    /// <summary>
    /// Explicit freeze — the player steered away, or the connection dropped. Idempotent: freezing an
    /// already-frozen session is a no-op (the caller composes whichever <see cref="SteerLogPayload"/>
    /// fits its own cause; only the FIRST one that actually lands is recorded, matching
    /// <see cref="BattleSessionRegistry.Abandon"/>'s own "keeps the first reason" precedent).
    /// </summary>
    public bool Freeze(SteerLogPayload payload)
    {
        lock (_gate)
        {
            if (Frozen) return false;
            Frozen = true;
        }

        _registry.Disconnect(MatchKey);
        _onFrozen?.Invoke(payload);

        // Wakes a currently-blocked Ask immediately (WaitAny observes the token) and makes every
        // FUTURE Ask on this session throw immediately too -- cancel-and-discard, not "finish this one
        // turn first."
        _cts.Cancel();
        return true;
    }
}
