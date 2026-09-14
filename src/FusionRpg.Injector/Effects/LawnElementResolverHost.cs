using FusionRpg.Core.Combat;
using FusionRpg.Core.Creatures;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Injector.Host;

namespace FusionRpg.Injector.Effects;

/// <summary>
/// The one shared <see cref="LawnElementResolver"/> both `InjectorCombatBridge` and
/// `InjectorStatusBridge` read (spec-lawn-element-bind.md §2.4, E27). Before this, each bridge ran its
/// own `foreach (var e in board.Entities)` scan to find one actor's `(side, typeId)` — <b>on every
/// hit</b>, in both files, independently. This host is what removes the duplication and the per-hit
/// cost the 2026-08 perf audit blamed: the board scan behind <see cref="BoardFactsFor"/> only ever runs
/// on a cache miss inside the shared <see cref="LawnElementResolver"/>, and a hit warmed by one bridge
/// is a hit for the other.
/// </summary>
public static class LawnElementResolverHost
{
    static readonly object Gate = new();
    static LawnElementResolver? _resolver;

    /// <summary>
    /// `(side, elementTypes)` for a lawn actor, cached per actor per match. Never throws — a species
    /// the index cannot find, or the catalog not yet configured, resolves <see cref="ActorElementTypes.Neutral"/>
    /// and is reported once (spec §2.4 step 5).
    ///
    /// <para><b>2026-09-14 fix (lawn-combat-wire T10/T12 live-inert investigation, second defect):</b>
    /// <paramref name="key"/>'s board facts are resolved via <see cref="BoardFactsFor"/> FIRST, and a
    /// <c>typeId == 0</c> ("board does not know this ptr" — see <see cref="LawnBasicAttackGrantBinder"/>'s
    /// own doc on the sentinel) now returns immediately WITHOUT ever calling into the inner
    /// <see cref="LawnElementResolver"/>. Before this fix, the inner call ran unconditionally and its own
    /// per-ptr cache has no notion of "try again later" — a ptr looked up even ONE FRAME before
    /// <c>InjectorEntityRegistry.Add</c> registers it (a real, reproducible race for any caller that
    /// applies stats/emits a spawn event outside the entity's own `Start`/`InitHealth` Harmony postfix,
    /// confirmed live via `debug.spawn-plant`/`debug.spawn-zombie`: roughly half of otherwise-identical
    /// spawns lost this race) got PERMANENTLY cached as <c>(side, ActorElementTypes.Neutral)</c> under
    /// that ptr's key, keyed by ptr only (not by whether the lookup actually resolved a real species) —
    /// so a caller that retried the SAME ptr on a later frame, once the board genuinely knew about it,
    /// still read back the stale Neutral answer for the rest of the match. This was the mechanism behind
    /// `LawnBasicAttackGrantBinder.Bind`'s `typeId == 0` early return being effectively permanent even
    /// when the caller re-resolves: <see cref="LawnBasicAttackGrantBinder"/> now retries a fresh ptr
    /// across a few frames, and that retry only works because a genuine "not on the board yet" miss no
    /// longer poisons this cache.</para>
    /// </summary>
    public static (string Side, int TypeId, ActorElementTypes Elements) Resolve(string key)
    {
        var (side, typeId) = BoardFactsFor(key);
        if (typeId == 0)
            return (side, 0, ActorElementTypes.Neutral);
        var (resolvedSide, elements) = Resolver.Resolve(GameHooks.MatchKey, key, () => (side, typeId));
        return (resolvedSide, typeId, elements);
    }

    /// <summary>
    /// Leave-board edge: forget ONE actor's cached `(side, elementTypes)` so an IL2CPP pointer reused
    /// by a new entity inside the SAME match cannot inherit the dead one's species
    /// (<see cref="LawnElementResolver"/>'s trigger set, item 3). Called from
    /// <c>GameHooks.ForgetEntity</c> — the single cleanup every death path already funnels through, and
    /// which runs after the die Emit, so `OnDeath` still sees the dying actor's element.
    ///
    /// <para>Never forces the resolver into existence: before the roster is configured there is nothing
    /// cached to forget, and building one here would cache the empty-index bootstrap resolver that
    /// <see cref="Resolver"/> deliberately refuses to keep.</para>
    /// </summary>
    public static void Invalidate(string? ptrKey) => _resolver?.Invalidate(ptrKey);

    static LawnElementResolver Resolver
    {
        get
        {
            if (_resolver != null) return _resolver;
            lock (Gate)
            {
                if (_resolver != null) return _resolver;

                IReadOnlyList<CreatureSpeciesDef> species;
                try { species = CreatureSpeciesCatalog.All; }
                catch (InvalidOperationException)
                {
                    // Configure() has not run yet — a narrow bootstrap window, not a defect. Hand back
                    // a throwaway empty-index resolver (every resolve is a Neutral miss) rather than
                    // caching it, so the NEXT call re-checks once the roster is actually configured.
                    return new LawnElementResolver(new LawnElementIndex(Array.Empty<CreatureSpeciesDef>()));
                }

                _resolver = new LawnElementResolver(new LawnElementIndex(species), Report);
                return _resolver;
            }
        }
    }

    static void Report(string message) => RpgHost.Log.Warning(message);

    /// <summary>
    /// The board scan both bridges used to run independently, moved here once. Mirrors the pre-E27
    /// logic verbatim, including the `CheatState.SelectedPtr` prove-pack fallback for an entity the
    /// board snapshot has not registered yet.
    /// </summary>
    static (string Side, int TypeId) BoardFactsFor(string key)
    {
        var board = InjectorBoardSnapshot.Capture();
        var side = "plant";
        var typeId = 0;
        foreach (var e in board.Entities)
        {
            if (!CombatPtr.EqualsPtr(e.Ptr, key)) continue;
            side = e.Side ?? "plant";
            typeId = e.TypeId;
            break;
        }

        if (typeId == 0
            && CheatState.SelectedPtr != IntPtr.Zero
            && CombatPtr.EqualsPtr(CheatState.SelectedPtr.ToString("X"), key)
            && !string.IsNullOrWhiteSpace(CheatState.SelectedSide))
            side = CheatState.SelectedSide;

        return (side, typeId);
    }
}
