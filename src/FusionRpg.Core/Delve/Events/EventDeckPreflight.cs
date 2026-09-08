using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Delve.Events;

/// <summary>
/// `event-deck` D3.9 (spec-event-deck.md §9, "Refusals and preflight") — PARTIALLY BUILT: the rules
/// that are pure over an already-loaded <see cref="EventCatalog"/> alone, plus one that is pure over
/// the container store instead (see below). Spec's own full rule list names ten checks; five are
/// buildable here over the catalog, model-free, exactly matching the spec's own framing ("model-free,
/// run by the domain importer and the tests, refusing the domain with the row named") — four always run
/// inside <see cref="Run"/>, the fifth (<see cref="CheckSupplyOverrideCoverage"/>) opts in via an
/// optional parameter. Three more (archetype/pool coverage, recent-cells headroom, `>= 1
/// encounter-event per rest archetype`) are built as `domain-catalog`'s own `DomainEventPreflight`
/// bridge instead, since they need a domain's own room palette this catalog-wide class has no
/// parameter for.
///
/// <para>A tenth item — spec's own "no `nerve.*` id or non-event atom kind in any container" — is a
/// SINGLE spec bullet with two conjuncts that turned out to need two DIFFERENT scopes once actually
/// read against code (not assumed): see <see cref="CheckNoNerveTargetInAnyContainer"/>'s own doc
/// comment for the full reasoning. Only the `nerve.*` conjunct is built, as a standalone method rather
/// than wired into <see cref="Run"/> — it has nothing to do with <see cref="EventCatalog"/> at all (it
/// scans the container store, not events), so folding it into `Run`'s catalog-shaped signature would
/// conflate two unrelated corpora the same way the three domain-bridge rules above were kept out of
/// `Run` for needing domain/room data `Run` has no parameter for.</para>
/// </summary>
public static class EventDeckPreflight
{
    /// <summary>Spec §9, verbatim: "≥ 1 `good` and ≥ 1 `bad`-or-`mixed` per event." `nothing` neither
    /// satisfies nor disqualifies either side.</summary>
    public static IReadOnlyList<AtomRejection> CheckOutcomeMix(EventCatalog catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            var hasGood = row.Outcomes.Any(o => string.Equals(o.Ordinal, "good", StringComparison.Ordinal));
            var hasBadOrMixed = row.Outcomes.Any(o =>
                string.Equals(o.Ordinal, "bad", StringComparison.Ordinal) ||
                string.Equals(o.Ordinal, "mixed", StringComparison.Ordinal));

            if (!hasGood || !hasBadOrMixed)
                fails.Add(EventRules.Fail(EventRules.MissingRequiredOutcomeMix,
                    $"'{row.EventId}' needs >= 1 'good' and >= 1 'bad'-or-'mixed' outcome (has good={hasGood}, bad-or-mixed={hasBadOrMixed})"));
        }
        return fails;
    }

    /// <summary>
    /// Spec §9, verbatim: "`chainRef` acyclic, same kind." An unresolved `chainRef` (pointing at an id
    /// absent from the catalog) is a DIFFERENT rule — referential integrity, not named in this section
    /// of the spec — and is silently skipped here rather than invented.
    /// </summary>
    public static IReadOnlyList<AtomRejection> CheckChainRefs(EventCatalog catalog)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            if (row.ChainRef is null) continue;

            var target = catalog.Resolve(row.ChainRef);
            if (target is not null && !string.Equals(target.Kind, row.Kind, StringComparison.Ordinal))
                fails.Add(EventRules.Fail(EventRules.ChainRefKindMismatch,
                    $"'{row.EventId}' (kind '{row.Kind}') chains to '{row.ChainRef}' (kind '{target.Kind}') -- chainRef must stay the same kind"));

            if (HasCycleFrom(catalog, row.EventId))
                fails.Add(EventRules.Fail(EventRules.ChainRefCycle, $"'{row.EventId}' is part of a chainRef cycle"));
        }
        return fails;
    }

    static bool HasCycleFrom(EventCatalog catalog, string startId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = startId;
        while (current is not null)
        {
            if (!seen.Add(current)) return true;
            current = catalog.Resolve(current)?.ChainRef;
        }
        return false;
    }

    /// <summary>Spec §9, verbatim: "`RoomKindIs boss` refused (*"no event may gate the boss"*)" —
    /// scans every event's own compiled-once eligibility tree for a <see cref="LeafId.RoomKindIs"/>
    /// leaf whose <paramref name="bossRoomKindOrdinal"/> matches, wherever it sits under `And`/`Or`/
    /// `Not`. <paramref name="bossRoomKindOrdinal"/> is a plain, caller-resolved ordinal — this module
    /// never imports `RoomKindCatalog` (D3.7's own "no second owner of a domain vocabulary" posture).</summary>
    public static IReadOnlyList<AtomRejection> CheckNoRoomKindIsBoss(EventCatalog catalog, int bossRoomKindOrdinal)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
            if (row.Eligibility is not null && ContainsRoomKindIsBoss(row.Eligibility, bossRoomKindOrdinal))
                fails.Add(EventRules.Fail(EventRules.RoomKindIsBossForbidden,
                    $"'{row.EventId}' gates eligibility on RoomKindIs(boss) -- no event may gate the boss"));
        return fails;
    }

    static bool ContainsRoomKindIsBoss(PredicateNode node, int bossOrdinal) => node switch
    {
        PredicateNode.And a => a.Children.Any(c => ContainsRoomKindIsBoss(c, bossOrdinal)),
        PredicateNode.Or o => o.Children.Any(c => ContainsRoomKindIsBoss(c, bossOrdinal)),
        PredicateNode.Not n => ContainsRoomKindIsBoss(n.Child, bossOrdinal),
        PredicateNode.Leaf l => l.Id == LeafId.RoomKindIs && l.Value == bossOrdinal,
        _ => false,
    };

    /// <summary>
    /// Spec §9, verbatim: "known status ids." `PredicateCompiler.ValidateLeaf` does NOT itself refuse
    /// an unknown one — an unresolvable `HasStatus` interns to bit -1 and evaluates permanently false
    /// (confirmed by reading `PredicateCompiler.cs`'s own `ValidateLeaf`), so this is a genuinely
    /// separate, event-deck-owned check over the SAME `statusBit` function the catalog itself compiled
    /// against, catching a typo that would otherwise ship as a silently-dead condition.
    /// </summary>
    public static IReadOnlyList<AtomRejection> CheckKnownStatusIds(EventCatalog catalog, Func<string, int> statusBit)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (statusBit is null) throw new ArgumentNullException(nameof(statusBit));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            if (row.Eligibility is null) continue;
            foreach (var badId in UnknownStatusIds(row.Eligibility, statusBit).Distinct(StringComparer.Ordinal))
                fails.Add(EventRules.Fail(EventRules.UnknownStatusId,
                    $"'{row.EventId}' HasStatus references unknown status id '{badId}'"));
        }
        return fails;
    }

    static IEnumerable<string> UnknownStatusIds(PredicateNode node, Func<string, int> statusBit) => node switch
    {
        PredicateNode.And a => a.Children.SelectMany(c => UnknownStatusIds(c, statusBit)),
        PredicateNode.Or o => o.Children.SelectMany(c => UnknownStatusIds(c, statusBit)),
        PredicateNode.Not n => UnknownStatusIds(n.Child, statusBit),
        PredicateNode.Leaf { Id: LeafId.HasStatus, Text: { } text } when statusBit(text) < 0 => new[] { text },
        _ => Array.Empty<string>(),
    };

    /// <summary>D3.9 (spec §9, `OverrideTagUnsupplied`): "every `supplyOverride` tag is carried by
    /// &gt;= 1 supply." `tagsCarriedBySupplies` is the caller-supplied union
    /// (<see cref="SupplyOverrideTagSeedFile.LoadAllOverrideTags"/>) — this function reads no file
    /// itself, matching every other rule in this class. Catalog-wide, not domain-scoped: a supply is
    /// global inventory, not bound to one domain's own room palette.</summary>
    public static IReadOnlyList<AtomRejection> CheckSupplyOverrideCoverage(EventCatalog catalog, IReadOnlySet<string> tagsCarriedBySupplies)
    {
        if (catalog is null) throw new ArgumentNullException(nameof(catalog));
        if (tagsCarriedBySupplies is null) throw new ArgumentNullException(nameof(tagsCarriedBySupplies));

        var fails = new List<AtomRejection>();
        foreach (var row in catalog.All)
        {
            if (row.SupplyOverride is null) continue;
            if (!tagsCarriedBySupplies.Contains(row.SupplyOverride))
                fails.Add(EventRules.Fail(EventRules.OverrideTagUnsupplied,
                    $"'{row.EventId}' supplyOverride '{row.SupplyOverride}' is not carried by any real supply"));
        }
        return fails;
    }

    /// <summary>Every buildable per-event rule, run together — never throws, one
    /// <see cref="AtomRejection"/> per violation found, matching <see cref="EventCatalog.Load"/>'s own
    /// "N bad rows, N rejections" shape. <paramref name="tagsCarriedBySupplies"/> defaults to `null`
    /// (skip the check, every existing call site keeps compiling unchanged) — pass
    /// <see cref="SupplyOverrideTagSeedFile.LoadAllOverrideTags"/>'s own result to include it.
    ///
    /// <para>Two of spec §9's items are deliberately NOT called from here, for two different reasons.
    /// Three (archetype/pool coverage, recent-cells headroom, `>= 1 encounter-event per rest
    /// archetype`) are built as `domain-catalog`'s own `DomainEventPreflight` bridge instead, since they
    /// need a domain's own room palette this catalog-wide function has no parameter for. The tenth item
    /// ("no `nerve.*` id or non-event atom kind in any container") has its `nerve.*` conjunct built as
    /// <see cref="CheckNoNerveTargetInAnyContainer"/>, a standalone method — not wired in here, since it
    /// scans the container store and has no relationship to <paramref name="catalog"/> at all; its
    /// "non-event atom kind" conjunct is not built at all (see that method's own doc comment for why,
    /// confirmed rather than assumed). See D3.9's own todo entry for the full, dated accounting.</para>
    /// </summary>
    public static IReadOnlyList<AtomRejection> Run(
        EventCatalog catalog, int bossRoomKindOrdinal, Func<string, int> statusBit,
        IReadOnlySet<string>? tagsCarriedBySupplies = null)
    {
        var fails = new List<AtomRejection>();
        fails.AddRange(CheckOutcomeMix(catalog));
        fails.AddRange(CheckChainRefs(catalog));
        fails.AddRange(CheckNoRoomKindIsBoss(catalog, bossRoomKindOrdinal));
        fails.AddRange(CheckKnownStatusIds(catalog, statusBit));
        if (tagsCarriedBySupplies is not null)
            fails.AddRange(CheckSupplyOverrideCoverage(catalog, tagsCarriedBySupplies));
        return fails;
    }

    /// <summary>
    /// Spec §9, verbatim (one bullet, two conjuncts): "no `nerve.*` id or non-event atom kind in any
    /// container." <b>Only the first conjunct is built here</b> — the two are decided independently
    /// below, each by reading spec-container-schema.md / definitions.md / this module's own §5 dispatch
    /// table and real shipped content, not assumed.
    ///
    /// <para><b>The `nerve.*` conjunct — built, whole-corpus.</b> `nerve.*`
    /// (<c>NerveStatusIds.For</c>, `Delve/Attrition/NervePolicy.cs`) is an EXCLUSIVELY-DERIVED
    /// projection: <c>NervePolicy.Sync</c> is the only writer — "at most one `nerve.*` instance per
    /// demon, never a status field" applied by ordinary content (that class's own doc comment). A
    /// `status.apply` atom targeting it from ANY container anywhere — an item, a passive node, an event
    /// outcome, a demon unique — breaks the same sync-ownership invariant identically, so this conjunct
    /// is genuinely global, not event-deck-scoped, and there is no narrower mechanism to scope it to: an
    /// event outcome names only <c>(Family, PowerBand)</c> (<see cref="EventEffectRef"/>,
    /// `EventRow.cs:12`), never a container id — that record's own doc comment: "a def row loaded here
    /// has no concrete container behind it yet" — so there is no edge from an event to "the containers
    /// it uses" to walk instead of the whole store.</para>
    ///
    /// <para><b>The "non-event atom kind" conjunct — NOT built, confirmed out of reach today, not
    /// guessed at.</b> Read literally over "any container", it would refuse a container for holding a
    /// kind outside this module's own five (spec §5: `resource.delta`, `status.apply`, `shield.grant`,
    /// `stat.derived`, `ui.present`). That scope is empirically wrong, not just theoretically risky:
    /// `data/seed/atoms/generated/family-expand.g-attack.json` — real, shipped item-affix content —
    /// carries 15 `stat.modify` atoms (a real, registered kind, `AtomKindRegistry.cs:494`, not one of
    /// the five), which a real item container's pool draws through `effect_affix_ref` on every rung
    /// (confirmed live via the same shape `RpgStore.ListActionContainers` in `FusionRpg.Data` already
    /// uses — Core cannot reference it directly, so this is prose, not a `cref`). A whole-corpus scan
    /// for "non-event kind" would refuse most of the item/trait/
    /// species-passive corpus. The only sound scope is "containers an event's own outcome actually
    /// resolves to" — but that scope does not exist to check against yet: <see cref="ContainerKind"/>
    /// has no `Event`/`Delve` member (`ContainerRow.cs:25-38`) and, as the paragraph above found, no
    /// importer turns `outcomes[].effects[]` into a real, addressable <see cref="ContainerRow"/> at all
    /// yet — D3.3's own already-named container-binding gap, restated with the precise missing piece
    /// named rather than gestured at, not re-litigated. Left unbuilt rather than silently narrowed to a
    /// scope that would either miss everything (vacuously empty) or flag everything (whole-corpus) —
    /// both wrong.</para>
    /// </summary>
    public static IReadOnlyList<AtomRejection> CheckNoNerveTargetInAnyContainer(
        IReadOnlyList<ContainerRow> containers, Func<string, AtomRow?> lookupAtom, Func<string, AffixRow?> lookupAffix)
    {
        if (containers is null) throw new ArgumentNullException(nameof(containers));
        if (lookupAtom is null) throw new ArgumentNullException(nameof(lookupAtom));
        if (lookupAffix is null) throw new ArgumentNullException(nameof(lookupAffix));

        var fails = new List<AtomRejection>();
        foreach (var container in containers)
        {
            foreach (var atomId in ResolvedAtomIds(container, lookupAffix))
            {
                var atom = lookupAtom(atomId);
                // A dangling ref is ContainerValidator's own rejection (E5: "every atom_id resolves,
                // else reject") -- a container that failed THAT check never reaches the store, so this
                // is unreachable on real content; skipped rather than invented, the same posture
                // CheckChainRefs already takes on an unresolved chainRef.
                if (atom is null) continue;
                if (!string.Equals(atom.KindId, "status.apply", StringComparison.Ordinal)) continue;

                if (TargetsNerve(atom.ParamsJson))
                    fails.Add(EventRules.Fail(EventRules.NerveTargetInContainer,
                        $"container '{container.ContainerId}' atom '{atom.AtomId}' (status.apply) targets a " +
                        "nerve.* id -- nerve is an exclusively-derived projection (NervePolicy.Sync), never a direct grant"));
            }
        }
        return fails;
    }

    /// <summary>Every atom id a container can resolve to: the fixed core directly, plus every CONCRETE
    /// ref (<see cref="AffixRefRow.AtomId"/> set) inside every pool row's own affix. A slot ref
    /// (<see cref="AffixRefRow.IsSlot"/>) is skipped: every shipped slot domain today is `element`
    /// (`RpgStore.Containers.cs`'s own `DomainMembers`), a variant-selection axis on a family like
    /// `atom.elemental-power` — never a mechanism that picks WHICH atom-kind-family a ref resolves to,
    /// so a `status.apply` atom cannot be reached through a slot. Named here rather than silently
    /// assumed.</summary>
    static IEnumerable<string> ResolvedAtomIds(ContainerRow container, Func<string, AffixRow?> lookupAffix)
    {
        foreach (var a in container.Atoms)
            yield return a.AtomId;

        foreach (var p in container.Pool)
        {
            var affix = lookupAffix(p.AffixId);
            if (affix is null) continue; // dangling pool ref -- ContainerValidator's own rejection, not this rule's
            foreach (var r in affix.Refs)
                if (r.AtomId is not null)
                    yield return r.AtomId;
        }
    }

    /// <summary>`status.apply`'s own <c>ParamSchema</c> (`AtomKindRegistry.cs:660`): the target status
    /// lives at `params.status`, a required string. Absence or a non-string is not this rule's concern
    /// — E1's own load-time schema validation already refuses that shape before it reaches
    /// storage.</summary>
    static bool TargetsNerve(string paramsJson)
    {
        using var doc = JsonDocument.Parse(paramsJson);
        return doc.RootElement.TryGetProperty("status", out var statusEl)
            && statusEl.ValueKind == JsonValueKind.String
            && (statusEl.GetString() ?? "").StartsWith("nerve.", StringComparison.Ordinal);
    }
}
