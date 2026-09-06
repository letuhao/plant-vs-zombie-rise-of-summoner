using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Match;
using FusionRpg.Data;

namespace FusionRpg.Server;

/// <summary>
/// Builds the compiled-output push for one owner (spec-compiled-push.md, E19).
///
/// <para><b>Cold, by construction.</b> This runs at Hello and at bind time — never between an event
/// and its apply. The injector rolls its own dice locally so the hot loop never waits; what travels
/// from here is the compiled content and the <b>seed</b>, which is what makes those local rolls
/// replayable (definitions §13 D5).</para>
///
/// <para>What leaves this class is already resolved: compiled grants, defs, and runner entries whose
/// predicates are flat int ops. No atom row, container row or curve row is ever put on the wire — if
/// one had to be, the compile/run split would have leaked.</para>
/// </summary>
public sealed class AtomPushService
{
    readonly RpgStore _store;

    /// <summary>
    /// `mods-absorption` (T6.1, item-ideal.md equip-runtime module 5) — the owner list one player's
    /// live atom push actually needs: the player's own scope plus every currently-<see
    /// cref="UniqueActorPhases.ActiveBound"/> specimen (deployed AND bound, not merely rostered).
    /// Extracted 2026-09-06 from <c>RpgHub.BuildApplyCommand</c>'s own inline loop (Hello) so a
    /// SECOND real call site — a mid-session re-push triggered by a unique actor's own phase
    /// transition, not a fresh connection — builds the identical union rather than a hand-rolled
    /// copy that could silently drift from the Hello path's own rule.
    /// </summary>
    public static List<OwnerScope> OwnersForPlayer(RpgStore store, long playerId)
    {
        var owners = new List<OwnerScope>
        {
            new(OwnerKind.Player, playerId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        };
        foreach (var specimen in store.ListUniqueActors(playerId).Items)
            if (string.Equals(specimen.Phase, UniqueActorPhases.ActiveBound, StringComparison.Ordinal))
                owners.Add(new OwnerScope(OwnerKind.UniqueActor, specimen.InstanceId));
        return owners;
    }

    public AtomPushService(RpgStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

    /// <summary>
    /// `patron-absorption` (spec-patron-absorption.md, 2026-09-06) — the live callback
    /// <see cref="ValueSpec.ExternalRef"/> atoms resolve through. Backed by
    /// <see cref="PatronEndpoints.Compute"/>, the SAME real logic that endpoint itself reports (patron
    /// row → profile/actor → the player's own Θ → <c>PatronPolicy.Aura</c>) — never a second
    /// derivation. Computed fresh on every call, never persisted, so a promotion or a switch is
    /// reflected on the very next push with nothing to refresh or go stale.
    ///
    /// <para>`patron.aura`'s own 12 atoms (one per real element × power/defense,
    /// <c>data/seed/atoms/patron-aura.json</c>) are all authored statically — every player gets the
    /// same 12 atoms pushed, and only the ONE or TWO matching the pushed player's own patron element(s)
    /// resolve non-zero here; every other element's ref resolves to a harmless `0` (a flat `+0`
    /// contributes nothing, the additive identity — not a value this method invents, the same shape
    /// `AuraMilli`'s own zero-for-absent-secondary already has).</para>
    /// </summary>
    Func<string, long> BuildExternalRefs(IReadOnlyList<OwnerScope> owners)
    {
        return refId =>
        {
            var playerOwner = owners.FirstOrDefault(o => o.Kind == OwnerKind.Player);
            if (playerOwner == default || !long.TryParse(playerOwner.Key, out var playerId))
                return 0;

            var computed = PatronEndpoints.Compute(_store, playerId);
            if (computed is not { } c)
                return 0; // no patron set — every patron-aura ref resolves to 0, never an error

            var (_, aura) = c;
            return refId switch
            {
                _ when TryElementSuffix(refId, "patron.auraPowerMilli.", out var el) =>
                    ForElement(el, aura.ElementPrimary, aura.PowerMilli, aura.ElementSecondary, aura.SecondaryPowerMilli),
                _ when TryElementSuffix(refId, "patron.auraDefenseMilli.", out var el) =>
                    ForElement(el, aura.ElementPrimary, aura.DefenseMilli, aura.ElementSecondary, aura.SecondaryDefenseMilli),
                _ => throw new InvalidOperationException($"unknown externalRef id: {refId}"),
            };
        };

        static bool TryElementSuffix(string refId, string prefix, out string element)
        {
            element = refId.StartsWith(prefix, StringComparison.Ordinal) ? refId[prefix.Length..] : "";
            return element.Length > 0;
        }

        static long ForElement(string element, string primary, long primaryMilli, string? secondary, long secondaryMilli) =>
            string.Equals(element, primary, StringComparison.Ordinal) ? primaryMilli
            : secondary is not null && string.Equals(element, secondary, StringComparison.Ordinal) ? secondaryMilli
            : 0;
    }

    /// <summary>
    /// The full set for one owner, or an empty up-to-date reply when the receiver already holds this
    /// catalog revision.
    /// </summary>
    /// <param name="receiverRevision">What the injector says it holds; null on cold start.</param>
    /// <param name="receiverEmitterVersion">
    /// E26: what <see cref="AtomPushCodec.EmitterVersion"/> the injector last learned (from its Hello).
    /// Null on cold start or against a pre-E26 injector that has never reported the field — distinct
    /// from a real version, so neither is mistaken for the other in the short-circuit below.
    /// </param>
    public AtomPushDto Build(
        OwnerScope owner,
        BindContext ctx,
        ulong matchSeed,
        string? matchKey = null,
        long? receiverRevision = null,
        int? ownerLevel = null,
        int? receiverEmitterVersion = null) =>
        Build(new[] { owner }, ctx, matchSeed, matchKey, receiverRevision, ownerLevel, receiverEmitterVersion);

    /// <summary>
    /// The same build, over several owner scopes at once — the shape module 5 (`equip-runtime`)
    /// named as the missing half of the live lawn push: a player's own grants plus every
    /// <see cref="OwnerKind.UniqueActor"/> specimen currently deployed with them. One compile over
    /// the UNION of every scope's atoms, not one push per owner — two owners sharing an atom (a
    /// player-side buff and an equipped item both touching the same channel) must compile it once,
    /// identically, or the runner would hold two "identical" entries that only accidentally agree.
    /// <see cref="RunnerBinding"/> already carries its own <c>OwnerKey</c> per binding, which is what
    /// makes merging safe: the wire shape was never owner-singular, only this call site was.
    /// </summary>
    public AtomPushDto Build(
        IReadOnlyList<OwnerScope> owners,
        BindContext ctx,
        ulong matchSeed,
        string? matchKey = null,
        long? receiverRevision = null,
        int? ownerLevel = null,
        int? receiverEmitterVersion = null)
    {
        if (owners is null || owners.Count == 0) throw new ArgumentException("at least one owner scope is required", nameof(owners));

        var revision = _store.GetCatalogRevision();

        // The hash is carried even when nothing else is, so a mismatch stays visible in telemetry on
        // a reconnect that delivers no content.
        var contentHash = _store.ComputeContentHash().ToCompact();

        // Two-term short-circuit (E26), mirroring AtomPushCodec.BuildPayload's own: CatalogRevision is
        // a stamp over seed DATA, so a receiver at the right revision but the wrong (or unknown)
        // emitter version still needs the full rebuild — the compiler-code path below is what makes
        // that decision, this early return must not shortcut around it on revision alone.
        if (receiverRevision == revision && receiverEmitterVersion == AtomPushCodec.EmitterVersion)
            return new AtomPushDto
            {
                CatalogRevision = revision,
                ContentHash = contentHash,
                MatchSeed = matchSeed,
                MatchKey = matchKey,
                UpToDate = true,
                EmitterVersion = AtomPushCodec.EmitterVersion,
            };

        // One compile over the distinct atoms behind every accepted binding, across every owner. A
        // per-owner compile would redo the whole classify/bake pass per owner, and two owners sharing
        // an atom would disagree about nothing at real cost — so the union is built first, compiled
        // once, and each owner's bindings are wired against that one shared catalog below.
        var distinct = new Dictionary<string, AtomRow>(StringComparer.Ordinal);
        var acceptedBindings = new List<(BindingRow Binding, IReadOnlyList<AtomRow> Rows)>();

        // item-ideal.md, equip-runtime (module 5): which owners each atom's COMPILED (passive) grant
        // belongs to. The RUNNER path already carried per-owner identity on RunnerBinding.OwnerKey;
        // the compiled path carried none at all, so a live specimen's passive stat.derived/stat.modify
        // gear reached MATCH scope — every plant and zombie on the lawn, not the one wearing it.
        // Sorted so a given revision bakes identical bytes regardless of binding enumeration order.
        var ownersByAtom = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        foreach (var owner in owners)
        {
            var resolution = _store.ResolveBindings(owner, ctx, ownerLevel);
            foreach (var binding in resolution.Bindings)
            {
                if (resolution.AtomsByBinding is null ||
                    !resolution.AtomsByBinding.TryGetValue(binding.BindingId, out var rows))
                    continue;

                acceptedBindings.Add((binding, rows));

                // Off the BINDING's own scope, never the requested owner's: a resolution can surface a
                // binding at a scope the caller did not name (match-wide rows), and stamping the
                // requested owner onto one of those would scope a shared effect to whoever asked first.
                var ownerKey = UniqueOwnerBinder.OwnerKeyForDurableGrant(binding.Scope);

                foreach (var row in rows)
                {
                    distinct[row.AtomId] = row;
                    if (!ownersByAtom.TryGetValue(row.AtomId, out var keys))
                        ownersByAtom[row.AtomId] = keys = new SortedSet<string>(StringComparer.Ordinal);
                    keys.Add(ownerKey);
                }
            }
        }

        var catalog = AtomCompiler.Compile(
            distinct.Values.OrderBy(a => a.AtomId, StringComparer.Ordinal).ToList(),
            ctx.Runtime,
            revision,
            curves: id => _store.GetCurve(id),
            ownerLevel: ownerLevel ?? 1,
            grantOwnerKeys: id => ownersByAtom.TryGetValue(id, out var keys) ? keys : null,
            externalRefs: BuildExternalRefs(owners));

        var byAtomId = catalog.Runtime.ToDictionary(e => e.AtomId, StringComparer.Ordinal);
        var bindings = new List<RunnerBinding>();

        foreach (var (binding, rows) in acceptedBindings)
        {
            foreach (var row in rows)
            {
                if (!byAtomId.TryGetValue(row.AtomId, out var entry)) continue;

                // The id is (binding, atom), not the binding alone. A container carrying three
                // runner atoms needs three independent ICD clocks and three independent caps — and
                // a shared id would also tie the evaluation sort, making order depend on how the
                // rows happened to arrive. Binding ids are unique per effect_binding row regardless
                // of owner, so merging owners here cannot collide two different owners' entries.
                bindings.Add(new RunnerBinding(
                    binding.BindingId + "#" + row.AtomId,
                    binding.Priority,
                    binding.OwnerKey,
                    entry));
            }
        }

        return AtomPushCodec.BuildPayload(
            catalog, bindings, matchSeed, matchKey, contentHash, receiverRevision, receiverEmitterVersion);
    }
}
