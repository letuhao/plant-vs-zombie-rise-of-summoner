using FusionRpg.Core.Combat;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// lawn-combat-wire T10 (spec-basic-attack-grant.md): binds the per-actor basic-attack grant at spawn
/// so `EffectRuntime.HasOnDamageDealtGrant()` is true for every lawn actor, plant and zombie, specimen
/// and general creature alike.
///
/// <para><b>Record-then-drain, the SAME shape <c>EventDrainHost</c>/<c>MoveDrainHost</c> already use
/// (`InjectorLoop.Tick`), for the identical reason.</b> <c>LawnElementResolverHost.Resolve</c> caches
/// per ptr per match, but the board scan behind a cache MISS goes through
/// <c>InjectorBoardSnapshot.Capture()</c>, which is cached only <i>per rendered frame</i> and is
/// invalidated by every spawn (`PlantStart`/zombie-spawn postfixes call `Invalidate()` before this
/// class ever runs). Resolving inline from the spawn hook itself would mean a wave of N zombies
/// spawning in the same frame forces N fresh board captures — each spawn's own invalidate stomping the
/// previous one's freshly-warmed snapshot. Queuing the ptr here and draining once per frame (AFTER every
/// spawn hook for that frame has already fired) means the batch's first resolve pays for one capture and
/// every other ptr in the same frame hits the frame cache, not the board scan.</para>
///
/// <para><b>2026-09-14 fix (lawn-combat-wire T10/T12 live-inert investigation, second defect —
/// grant-bind/registry-registration race): a spawn hook's own `Emit("plant.spawn"/"zombie.spawn", ...)`
/// is NOT guaranteed to run after `InjectorEntityRegistry.Add` for that same ptr.</b> For an ORGANIC
/// spawn (`PlantStart`/`ZombieStart`/`ZombieInitHealth` Harmony postfixes) `Add` always runs first, in
/// the same call stack, before `ApplyPlant`/`ApplyZombie` ever emits — safe by construction. But
/// `EntityApply.RunPlant`/`RunZombie` also has callers OUTSIDE that call stack — most visibly
/// `DebugActions.SpawnPlant`/`SpawnZombie`, which call `RunPlant`/`RunZombie` directly right after
/// creating the entity — and there is no engine guarantee that the entity's OWN `Start`/`InitHealth`
/// (which is what actually calls `InjectorEntityRegistry.Add`) has fired yet at that point: Unity may
/// defer a freshly-instantiated object's own lifecycle callbacks to a later point in the same frame or
/// the next one. `Bind` below used to resolve once and give up for good on a `typeId == 0` miss — so a
/// ptr resolved one frame too early lost its grant forever, with no error anywhere (confirmed live: 2 of
/// 4 otherwise-identical `debug.spawn-plant`/`debug.spawn-zombie` calls lost this exact race, verified
/// via `POST /api/debug/effect/list` showing `grants:0` for the missed ptr, persisting for 60+ seconds
/// with no retry). A ptr that misses now requeues for a further <see cref="MaxRetryFrames"/> frames
/// before being dropped, so a same-frame-or-next-frame registration (the overwhelmingly common shape of
/// this race) still gets its grant. A ptr that is STILL unresolvable after that many frames really is
/// gone (died before ever being registered) and is dropped exactly as before.</para>
/// </summary>
public static class LawnBasicAttackGrantBinder
{
    /// <summary>How many EXTRA frames a ptr that missed its board-fact resolve gets requeued for before
    /// being dropped for good. Structural retry bound, not a balance tunable (tunables-ssot.md) — the
    /// race this exists for resolves within one or two frames or not at all; a bigger number only delays
    /// discovering a genuinely-dead ptr, it never binds one that was never going to resolve.</summary>
    const int MaxRetryFrames = 8;

    static readonly object Gate = new();
    static readonly List<string> Pending = new();
    static readonly List<(string Ptr, int RetriesLeft)> PendingRetry = new();

    /// <summary>Record a spawn — called from `MatchHost.Apply` on `plant.spawn`/`zombie.spawn`, inside
    /// the SAME board-fold apply the spec asks this to key on. No board read, no grant call: O(1).</summary>
    public static void QueueSpawn(string? ptr)
    {
        if (string.IsNullOrWhiteSpace(ptr)) return;
        lock (Gate) Pending.Add(ptr!);
    }

    /// <summary>Drain the batch — called once per frame from `InjectorLoop.Tick`, the same slot
    /// `EventDrainHost.Tick`/`MoveDrainHost.Tick` already occupy. A no-op when nothing spawned this
    /// frame (the common case) or the feature is off (kill switch — queue is drained-and-discarded so
    /// it never grows unbounded while disabled).</summary>
    public static void Tick()
    {
        List<string> batch;
        List<(string Ptr, int RetriesLeft)> retryBatch;
        lock (Gate)
        {
            if (Pending.Count == 0 && PendingRetry.Count == 0) return;
            batch = new List<string>(Pending);
            Pending.Clear();
            retryBatch = new List<(string, int)>(PendingRetry);
            PendingRetry.Clear();
        }

        if (!LawnBasicAttackFeature.Enabled) return;

        foreach (var ptr in batch)
            TryBindOrRequeue(ptr, MaxRetryFrames);

        foreach (var (ptr, retriesLeft) in retryBatch)
            TryBindOrRequeue(ptr, retriesLeft);
    }

    static void TryBindOrRequeue(string ptr, int retriesLeft)
    {
        bool bound;
        try { bound = Bind(ptr); }
        catch (Exception ex)
        {
            CheatState.Error("lawn-basic-attack bind: " + ex.Message);
            return; // an exception is a real failure, not a "not registered yet" race -- never requeue it
        }

        if (bound || retriesLeft <= 0) return;
        lock (Gate) PendingRetry.Add((ptr, retriesLeft - 1));
    }

    /// <summary>Match-edge reset — a ptr queued in a match that ended before its drain must not bind
    /// against the NEXT match's board. Called from `GameHooks.ClearMatch`.</summary>
    public static void ClearPending()
    {
        lock (Gate)
        {
            Pending.Clear();
            PendingRetry.Clear();
        }
    }

    /// <returns><c>true</c> when a grant was actually bound this call; <c>false</c> when the board does
    /// not know this ptr YET (or anymore) — the caller decides whether that is worth a retry.</returns>
    static bool Bind(string ptr)
    {
        // The same board-fact resolve InjectorCombatBridge/InjectorStatusBridge already share (E27) —
        // never a second board scan. typeId 0 is this codebase's own "no entity" sentinel
        // (CreateZombieHooks.cs: `theZombieType == ZombieType.Nothing`) — a ptr the board does not (yet,
        // or any longer) know about has nothing to bind THIS frame; the caller requeues a fresh spawn's
        // ptr for a few frames (see this class's own 2026-09-14 doc) rather than assuming a later real
        // spawn will re-queue it, since a genuinely dead ptr never spawns again to do so. This never lets
        // a stale element survive: the resolve here reads whatever the resolver's cache holds for THIS
        // ptr right now, not what it held at queue time, so a same-frame death+reuse at the same address
        // still resolves the CURRENT occupant's own species, never the dead one's.
        var (_, typeId, elements) = LawnElementResolverHost.Resolve(ptr);
        if (typeId == 0) return false;

        // Inert-at-default dual-typing (HybridPayload's own doc: 0 weight collapses to the single
        // full-weight primary component) — raising it for lawn actors is a balance decision this
        // module does not make; spec-basic-attack-grant.md names only the primary/secondary source.
        var dto = BasicAttackGrantBuilder.Build(ptr, elements.Primary, elements.Secondary, secondaryWeightMilli: 0);
        EffectRuntime.GrantQuiet(dto);
        return true;
    }
}
