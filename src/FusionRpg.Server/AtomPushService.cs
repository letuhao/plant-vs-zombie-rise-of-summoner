using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
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

    public AtomPushService(RpgStore store) => _store = store ?? throw new ArgumentNullException(nameof(store));

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

        foreach (var owner in owners)
        {
            var resolution = _store.ResolveBindings(owner, ctx, ownerLevel);
            foreach (var binding in resolution.Bindings)
            {
                if (resolution.AtomsByBinding is null ||
                    !resolution.AtomsByBinding.TryGetValue(binding.BindingId, out var rows))
                    continue;

                acceptedBindings.Add((binding, rows));
                foreach (var row in rows)
                    distinct[row.AtomId] = row;
            }
        }

        var catalog = AtomCompiler.Compile(
            distinct.Values.OrderBy(a => a.AtomId, StringComparer.Ordinal).ToList(),
            ctx.Runtime,
            revision,
            curves: id => _store.GetCurve(id),
            ownerLevel: ownerLevel ?? 1);

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
