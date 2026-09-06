using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items.Drops;
using FusionRpg.Core.Items.Uniques;

namespace FusionRpg.Core.Delve.Loot;

/// <summary>
/// D4.28 (spec-unique-pipeline.md §5, "The `boss-unique` drop group and first clear by id") — the
/// group's own binding-and-eligibility logic, plus the domain anchor's `firstClearRef` validation.
///
/// <para><b>Deliberately narrow, and honestly so.</b> A dedicated research pass confirmed three real,
/// substantial gaps this task does NOT close, each named at its own call site below rather than
/// papered over: (1) no code anywhere resolves a unique's own theme to a domain's climate
/// (`UniqueDomainListing`, the spec's own named method for this, does not exist — `UniqueSeed` carries
/// no theme/climate field at all) — the climate match arrives here as a caller-supplied fact, the same
/// "read model owned elsewhere" shape this whole program already uses everywhere a real upstream
/// piece is missing. (2) No planner-JSON → `DropTableRow` band/weight resolver exists ANYWHERE in this
/// codebase — not even for the 40 already-shipped item-corpus tables, which still author raw
/// `dropBand` strings today — so the real weight for `dropBand: exceptional`/`staple`
/// (`data/seed/items/_registry/bands.v1.json:463-484`: 7 and 1000) is a caller-supplied `int`, never
/// re-derived or hardcoded here. (3) No real per-domain planner JSON exists on disk at all
/// (`data/seed/dungeon/domains/` is empty) — this file builds the GROUP from an in-memory candidate
/// list, provable today against a small hand-built input; running it over real domains is D4.30's own
/// separately-blocked job.</para>
/// </summary>
public static class DungeonLootTableGen
{
    static DungeonLootTableGen() => ContentRuleNamespaces.Register("dungeon");

    /// <summary>One unique's own eligibility-relevant facts, resolved by the caller. `ClimateMatches`
    /// stands in for the unbuilt `UniqueDomainListing` (see class doc) — true when the theme's own
    /// `elementAffinity[]` contains the domain's climate or is empty (spec §5, verbatim).</summary>
    public readonly record struct BossUniqueCandidate(
        string SeedId, string ContainerId, int RungOrdinal, bool Enabled,
        UniqueAcquisition Acquisition, bool ClimateMatches);

    /// <summary>Spec §5, verbatim: "Eligible = rung ≥ 80, `enabled`, `acquisition ∈ {drop,
    /// source-locked}` (a `deterministic` unique never sits in a table) ... the theme's
    /// `elementAffinity[]` contains the domain's climate or is empty — a filter, never a weight." The
    /// "≥ 80" is `uniques.v1.json`'s own `rungFloorOrdinal` tunable (D4.23), read through
    /// <see cref="UniqueTuning.IsRungEligible"/> rather than a second, independently-hardcoded 80 —
    /// the exact "no second owner of a domain fact" defect this program repeatedly names elsewhere.</summary>
    public static bool IsEligible(BossUniqueCandidate c, UniqueTuning tuning) =>
        tuning.IsRungEligible(c.RungOrdinal)
        && c.Enabled
        && c.Acquisition is UniqueAcquisition.Drop or UniqueAcquisition.SourceLocked
        && c.ClimateMatches;

    /// <summary>
    /// Spec §5, verbatim: "one `{entryKind: unique, ref: item.&lt;slug&gt;, dropBand: exceptional}`
    /// per eligible unique beside `{nothing, staple}`." Refuses `unique.group-starved` (the spec's own
    /// named reason) rather than shipping a group that can only ever draw `nothing` — silently
    /// authoring a dead group is the exact failure this whole program's "refuse, never a default"
    /// posture exists to prevent.
    /// </summary>
    public static AtomRejection BuildBossUniqueGroup(
        IEnumerable<BossUniqueCandidate> candidates, UniqueTuning tuning, int exceptionalWeight, int stapleWeight,
        out IReadOnlyList<DropTableEntryRow>? entries)
    {
        entries = null;
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));

        UniqueRules.EnsureRegistered(); // "unique" namespace may not be registered yet if nothing else has touched UniqueRules first

        var eligible = candidates.Where(c => IsEligible(c, tuning)).ToList();
        if (eligible.Count == 0)
            return AtomRejection.ContentRule(UniqueRules.Namespace + ".group-starved",
                "no eligible unique for this domain's boss-unique group");

        var seq = 0;
        var rows = new List<DropTableEntryRow>(eligible.Count + 1);
        foreach (var c in eligible)
            rows.Add(new DropTableEntryRow(seq++, DropEntryKind.Unique, c.ContainerId, exceptionalWeight, AffixChannel: AffixChannels.Boss));
        rows.Add(new DropTableEntryRow(seq, DropEntryKind.Nothing, "", stapleWeight));

        entries = rows;
        return AtomRejection.Ok;
    }

    /// <summary>One `entryKind: unique` reference, naming the table it was placed in — the input
    /// <see cref="ValidateSourceLockedOnce"/> checks. A generator emits one of these per unique entry
    /// it writes into any table (boss-unique or otherwise), not just this file's own group.</summary>
    public readonly record struct SourceLockReference(string UniqueId, string TableId);

    /// <summary>One `source-locked` id referenced by more than one table, naming every table it
    /// appears in (so the fix is obvious, not just "somewhere is wrong").</summary>
    public readonly record struct SourceLockViolation(string UniqueId, IReadOnlyList<string> TableIds);

    /// <summary>
    /// "The lock lives in which table references the id" (ideal, verbatim) — a `source-locked` unique
    /// must be listed by EXACTLY one domain table. Confirmed by a dedicated research pass that this
    /// cardinality is enforced NOWHERE today: the Python `acquisition.py` module checks reachability
    /// (is this id obtainable at all), never a per-id reference count, and the C# corpus validator's
    /// own `ValidateDropReferences` explicitly skips every non-general-channel (i.e. source-locked)
    /// reference. This is the first real check of the actual rule, in either language.
    /// </summary>
    public static IReadOnlyList<SourceLockViolation> ValidateSourceLockedOnce(IEnumerable<SourceLockReference> references)
    {
        if (references is null) throw new ArgumentNullException(nameof(references));

        return references
            .GroupBy(r => r.UniqueId, StringComparer.Ordinal)
            .Select(g => new
            {
                g.Key,
                Tables = g.Select(r => r.TableId).Distinct(StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList(),
            })
            .Where(x => x.Tables.Count > 1)
            .Select(x => new SourceLockViolation(x.Key, x.Tables))
            .OrderBy(v => v.UniqueId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>One known unique's facts, keyed by container id — everything
    /// <see cref="ValidateFirstClearRef"/> needs to check a domain anchor's own claim against.</summary>
    public readonly record struct KnownUnique(int RungOrdinal, UniqueAcquisition Acquisition);

    /// <summary>
    /// Spec §5, verbatim: "the domain anchor gains `firstClearRef` (VALIDATED against rung-80+
    /// `deterministic` unique ids · `none` legal)." `null`/absent IS "none" — `DomainAnchor
    /// .FirstClearRef` is itself the nullable field, so there is no separate literal `"none"` string
    /// to compare against here.
    /// </summary>
    public static AtomRejection ValidateFirstClearRef(string? firstClearRef, IReadOnlyDictionary<string, KnownUnique> knownUniques, UniqueTuning tuning)
    {
        if (knownUniques is null) throw new ArgumentNullException(nameof(knownUniques));
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (firstClearRef is null) return AtomRejection.Ok;

        UniqueRules.EnsureRegistered();

        if (!knownUniques.TryGetValue(firstClearRef, out var u))
            return AtomRejection.ContentRule("dungeon.first-clear-ref-unknown",
                $"firstClearRef '{firstClearRef}' does not name a known unique container");
        if (u.Acquisition != UniqueAcquisition.Deterministic)
            return AtomRejection.ContentRule("dungeon.first-clear-ref-not-deterministic",
                $"firstClearRef '{firstClearRef}' names a {u.Acquisition} unique, not deterministic");
        if (!tuning.IsRungEligible(u.RungOrdinal))
            return AtomRejection.ContentRule("dungeon.first-clear-ref-below-rung-floor",
                $"firstClearRef '{firstClearRef}' is rung {u.RungOrdinal}, below the floor of {tuning.RungFloorOrdinal}");

        return AtomRejection.Ok;
    }
}
