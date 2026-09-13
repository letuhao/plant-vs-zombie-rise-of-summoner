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
/// </summary>
public static class LawnBasicAttackGrantBinder
{
    static readonly object Gate = new();
    static readonly List<string> Pending = new();

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
        lock (Gate)
        {
            if (Pending.Count == 0) return;
            batch = new List<string>(Pending);
            Pending.Clear();
        }

        if (!LawnBasicAttackFeature.Enabled) return;

        foreach (var ptr in batch)
        {
            try { Bind(ptr); }
            catch (Exception ex) { CheatState.Error("lawn-basic-attack bind: " + ex.Message); }
        }
    }

    /// <summary>Match-edge reset — a ptr queued in a match that ended before its drain must not bind
    /// against the NEXT match's board. Called from `GameHooks.ClearMatch`.</summary>
    public static void ClearPending()
    {
        lock (Gate) Pending.Clear();
    }

    static void Bind(string ptr)
    {
        // The same board-fact resolve InjectorCombatBridge/InjectorStatusBridge already share (E27) —
        // never a second board scan. typeId 0 is this codebase's own "no entity" sentinel
        // (CreateZombieHooks.cs: `theZombieType == ZombieType.Nothing`) — a ptr the board no longer
        // knows about (died and was never re-occupied between queue and drain) has nothing to bind;
        // a later real spawn re-queues it. This never lets a stale element survive: the resolve here
        // reads whatever the resolver's cache holds for THIS ptr right now, not what it held at
        // queue time, so a same-frame death+reuse at the same address still resolves the CURRENT
        // occupant's own species, never the dead one's.
        var (_, typeId, elements) = LawnElementResolverHost.Resolve(ptr);
        if (typeId == 0) return;

        // Inert-at-default dual-typing (HybridPayload's own doc: 0 weight collapses to the single
        // full-weight primary component) — raising it for lawn actors is a balance decision this
        // module does not make; spec-basic-attack-grant.md names only the primary/secondary source.
        var dto = BasicAttackGrantBuilder.Build(ptr, elements.Primary, elements.Secondary, secondaryWeightMilli: 0);
        EffectRuntime.GrantQuiet(dto);
    }
}
