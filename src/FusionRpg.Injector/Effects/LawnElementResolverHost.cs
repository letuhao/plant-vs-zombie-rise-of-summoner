using FusionRpg.Core.Combat;
using FusionRpg.Core.Demons;
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
    /// </summary>
    public static (string Side, int TypeId, ActorElementTypes Elements) Resolve(string key)
    {
        var (side, typeId) = BoardFactsFor(key);
        var (resolvedSide, elements) = Resolver.Resolve(GameHooks.MatchKey, key, () => (side, typeId));
        return (resolvedSide, typeId, elements);
    }

    static LawnElementResolver Resolver
    {
        get
        {
            if (_resolver != null) return _resolver;
            lock (Gate)
            {
                if (_resolver != null) return _resolver;

                IReadOnlyList<DemonSpeciesDef> species;
                try { species = DemonSpeciesCatalog.All; }
                catch (InvalidOperationException)
                {
                    // Configure() has not run yet — a narrow bootstrap window, not a defect. Hand back
                    // a throwaway empty-index resolver (every resolve is a Neutral miss) rather than
                    // caching it, so the NEXT call re-checks once the roster is actually configured.
                    return new LawnElementResolver(new LawnElementIndex(Array.Empty<DemonSpeciesDef>()));
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
