using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>One `(Side, GameTypeId) → speciesId` lookup outcome, distinguishing THREE states rather
/// than collapsing to a bool (spec-allocation-transport.md's own ⛔ hazard): the underlying index not
/// having been configured yet (the bootstrap window `LawnElementResolverHost` documents — a throwaway
/// empty index would otherwise look identical to "this type genuinely has no species"), versus a real,
/// configured "no species at this (Side, TypeId)" answer, versus a real hit.</summary>
public readonly record struct SpeciesLookupResult(bool IndexConfigured, bool Found, string SpeciesId)
{
    public static readonly SpeciesLookupResult NotConfigured = new(false, false, "");
    public static readonly SpeciesLookupResult NoSpecies = new(true, false, "");
    public static SpeciesLookupResult Hit(string speciesId) => new(true, true, speciesId);
}

/// <summary>
/// `allocation-transport` (module 6) — the `ctx → allocation` resolution logic behind an injected
/// lookup, mirroring <see cref="Battle.SpecimenOwnershipOracle"/>'s own established shape: fully
/// provable in Core with fake resolvers, no running game, no I/O of its own (the Hot-path ban this
/// type must never violate — every parameter is a plain, already-resolved delegate call).
///
/// <para><b>Commander and species points merge into ONE <see cref="AptitudeAllocation"/></b>
/// (`operator+`), resolved once by the caller — never resolved per scope and concatenated
/// (`AptitudeAllocation`'s own "scopes sum before share, never the reverse").</para>
///
/// <para><b>An un-configured index reports, never returns a silent zero</b> — the identical shape to
/// the documented defect where a 222-point allocation resolved and wrote nothing. When
/// <paramref name="resolveSpeciesId"/> reports <see cref="SpeciesLookupResult.IndexConfigured"/> false,
/// <see cref="Resolve"/> calls <paramref name="reportUnconfigured"/> and falls back to the commander
/// allocation ALONE — a real, if incomplete, answer, not a made-up one.</para>
/// </summary>
public sealed class SpeciesAllocationSource
{
    readonly Func<StatSide, int, SpeciesLookupResult> _resolveSpeciesId;
    readonly Func<string, AptitudeAllocation> _resolveSpeciesAllocation;
    readonly Func<long?, AptitudeAllocation> _resolveCommanderAllocation;
    readonly Action<string> _reportUnconfigured;
    readonly Func<string, string?>? _resolveBoundInstanceId;
    readonly Func<string, AptitudeAllocation>? _resolveUniqueAllocation;

    /// <param name="resolveSpeciesId">`(Side, GameTypeId) → SpeciesLookupResult` — in production,
    /// `LawnElementIndex.TryGet` wrapped to also report whether the index itself has been configured
    /// (`LawnElementResolverHost`'s own state). Injected, never a hard dependency — a test supplies a
    /// fake covering all three outcomes with no `LawnElementIndex` involved.</param>
    /// <param name="resolveSpeciesAllocation">`speciesId → effective CreatureType allocation` — in
    /// production, the injector's own cached-by-speciesId dictionary (`allocation-transport`'s own
    /// cache, refreshed at the existing commander-cache cadence), never a server round trip.</param>
    /// <param name="resolveCommanderAllocation">`playerId? → Commander allocation` — in production,
    /// the SAME cache `CheatState.CommanderAllocation` already resolves from.</param>
    /// <param name="reportUnconfigured">Called with a diagnostic message when the species index has
    /// not been configured yet — required, never silently swallowed (this repo's own "no silent
    /// default" discipline, applied here to a runtime reporting hook rather than a config key).</param>
    /// <param name="resolveBoundInstanceId">`unique-lawn-wire` (aptitude-sheet AS-1.1) — `EntityKey`
    /// (the live ptr hex) → the UniqueCreature `instanceId` it is currently Bound to, or null when this
    /// entity is not a Bound specimen. Optional: omitted, every ctx resolves the species path exactly
    /// as before (existing callers/tests are unaffected). When supplied, a Bound hit takes ABSOLUTE
    /// priority over the species lookup below — never merged with it — which is what keeps a Bound
    /// unique that happens to share a species id with a general from inheriting empire shares
    /// (`spec-unique-lawn-wire.md`'s own G6 regression).</param>
    /// <param name="resolveUniqueAllocation">`instanceId → effective UniqueCreature allocation` — in
    /// production, the injector's own cached-by-instanceId dictionary, fetched via
    /// `GET /api/aptitudes/unique/{instanceId}` at the same cadence as the commander/species caches.
    /// Required whenever <paramref name="resolveBoundInstanceId"/> is supplied.</param>
    public SpeciesAllocationSource(
        Func<StatSide, int, SpeciesLookupResult> resolveSpeciesId,
        Func<string, AptitudeAllocation> resolveSpeciesAllocation,
        Func<long?, AptitudeAllocation> resolveCommanderAllocation,
        Action<string> reportUnconfigured,
        Func<string, string?>? resolveBoundInstanceId = null,
        Func<string, AptitudeAllocation>? resolveUniqueAllocation = null)
    {
        _resolveSpeciesId = resolveSpeciesId ?? throw new ArgumentNullException(nameof(resolveSpeciesId));
        _resolveSpeciesAllocation = resolveSpeciesAllocation ?? throw new ArgumentNullException(nameof(resolveSpeciesAllocation));
        _resolveCommanderAllocation = resolveCommanderAllocation ?? throw new ArgumentNullException(nameof(resolveCommanderAllocation));
        _reportUnconfigured = reportUnconfigured ?? throw new ArgumentNullException(nameof(reportUnconfigured));
        if (resolveBoundInstanceId is not null && resolveUniqueAllocation is null)
            throw new ArgumentNullException(nameof(resolveUniqueAllocation),
                "resolveUniqueAllocation is required whenever resolveBoundInstanceId is supplied");
        _resolveBoundInstanceId = resolveBoundInstanceId;
        _resolveUniqueAllocation = resolveUniqueAllocation;
    }

    /// <summary>The one resolve entry point. A Bound UniqueCreature (when the caller wired
    /// <c>resolveBoundInstanceId</c>) resolves commander merged with ITS OWN allocation and returns
    /// immediately — the species lookup never runs for that ctx, so a Bound specimen sharing a species
    /// id with a general can never inherit empire shares. Otherwise: commander alone when there is no
    /// species to merge (genuinely no species at this `(Side, TypeId)`, OR the index isn't configured
    /// yet — reported in the latter case), commander merged with the species' effective allocation
    /// otherwise. `Side` stays part of every lookup key, always — `polevaulterzombie`/`wallnut` share a
    /// `GameTypeId` but never a `Side`, so they never collide here.</summary>
    public AptitudeAllocation Resolve(StatContext ctx)
    {
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var commander = _resolveCommanderAllocation(ctx.PlayerId);

        if (_resolveBoundInstanceId is not null)
        {
            var instanceId = _resolveBoundInstanceId(ctx.EntityKey);
            if (!string.IsNullOrEmpty(instanceId))
                return commander + _resolveUniqueAllocation!(instanceId);
        }

        var lookup = _resolveSpeciesId(ctx.Side, ctx.TypeId);

        if (!lookup.IndexConfigured)
        {
            _reportUnconfigured(
                $"SpeciesAllocationSource.Resolve: species index not configured yet (side={ctx.Side}, " +
                $"typeId={ctx.TypeId}, entity='{ctx.EntityKey}') -- resolving commander-only for this call.");
            return commander;
        }

        if (!lookup.Found) return commander;

        return commander + _resolveSpeciesAllocation(lookup.SpeciesId);
    }
}
