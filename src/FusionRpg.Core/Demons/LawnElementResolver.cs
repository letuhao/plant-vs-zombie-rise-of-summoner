using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons;

/// <summary>
/// Turns a lawn actor's `(side, PvZ typeId)` into its species' <see cref="ActorElementTypes"/>, cached
/// per actor per match (spec-lawn-element-bind.md §2.4, E27).
///
/// <para><b>The board lookup is the expensive part, not the element map.</b>
/// <c>InjectorCombatBridge.ResolveElementTypesFromHub</c> and <c>InjectorStatusBridge.ResolveDerived</c>
/// each loop the whole board to find one actor's `(side, typeId)` — <b>on every hit</b>, which the
/// 2026-08 perf audit already blamed. `boardLookup` is a <see cref="Func{TResult}"/> for exactly this
/// reason: it only ever runs on a cache miss, so this resolver is what removes that scan from the hot
/// path rather than adding a second one beside it.</para>
///
/// <para><b>A pointer is match-scoped.</b> A later match can hand the same Unity pointer to a different
/// entity, so the cache clears whenever <c>matchKey</c> changes rather than living for the process.</para>
/// </summary>
public sealed class LawnElementResolver
{
    readonly LawnElementIndex _index;
    readonly Action<string>? _onDiagnostic;
    readonly Dictionary<string, (string Side, ActorElementTypes Elements)> _cache =
        new(StringComparer.Ordinal);
    readonly HashSet<int> _reportedMisses = new();
    string? _matchKey;

    public LawnElementResolver(LawnElementIndex index, Action<string>? onDiagnostic = null)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _onDiagnostic = onDiagnostic;
        foreach (var line in index.Collisions)
            _onDiagnostic?.Invoke("lawn-element-bind: duplicate (side, gameTypeId) " + line);
    }

    /// <summary>Resolves since the resolver was built — read-only, for a cache-hit-rate test.</summary>
    public int ResolveCallCount { get; private set; }

    /// <summary>Board lookups actually performed — strictly less than <see cref="ResolveCallCount"/>
    /// once a ptr repeats within a match, which is the whole point of the cache.</summary>
    public int BoardLookupCount { get; private set; }

    public (string Side, ActorElementTypes Elements) Resolve(
        string? matchKey, string ptrKey, Func<(string Side, int TypeId)> boardLookup)
    {
        if (string.IsNullOrEmpty(ptrKey)) throw new ArgumentException("ptrKey is required.", nameof(ptrKey));
        if (boardLookup is null) throw new ArgumentNullException(nameof(boardLookup));

        ResolveCallCount++;

        if (!string.Equals(matchKey, _matchKey, StringComparison.Ordinal))
        {
            _cache.Clear();
            _reportedMisses.Clear();
            _matchKey = matchKey;
        }

        if (_cache.TryGetValue(ptrKey, out var hit)) return hit;

        BoardLookupCount++;
        var (side, typeId) = boardLookup();
        var elements = ElementsFor(side, typeId);

        var result = (side, elements);
        _cache[ptrKey] = result;
        return result;
    }

    ActorElementTypes ElementsFor(string side, int typeId)
    {
        if (!_index.TryGet(side, typeId, out var species))
            return Miss(typeId, $"no species for (side={side}, typeId={typeId})");

        if (!Enum.IsDefined(typeof(ElementTypeId), species.ElementPrimary))
            return Miss(typeId,
                $"species '{species.SpeciesId}' has an undefined ElementPrimary ({(int)species.ElementPrimary})");

        var secondary = species.ElementSecondary;
        if (secondary is { } sec && !Enum.IsDefined(typeof(ElementTypeId), sec))
            return Miss(typeId,
                $"species '{species.SpeciesId}' has an undefined ElementSecondary ({(int)sec})");

        // Mirrors BattleEngine.cs:36-38 verbatim, including the secondary-collapse rule — two
        // constructions of the same concept that differ by a corner case is how the two runtimes
        // drift apart. DemonSpeciesCatalog.Validate already refuses secondary == primary at import,
        // so this is belt-and-braces, not a case that fires against a validated roster.
        return ActorElementTypes.Create(
            species.ElementPrimary,
            secondary == species.ElementPrimary ? null : secondary);
    }

    ActorElementTypes Miss(int typeId, string reason)
    {
        if (_reportedMisses.Add(typeId))
            _onDiagnostic?.Invoke("lawn-element-bind: " + reason + " — resolving Neutral");
        return ActorElementTypes.Neutral;
    }
}
