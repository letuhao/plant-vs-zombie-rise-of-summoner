namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// The six container kinds. Closed: adding one is a reviewed change, because each implies a spec
/// that owns its authoring and its lifecycle.
/// </summary>
public enum ContainerKind
{
    Item = 0,
    Trait,
    Skill,
    SpeciesPassive,
    Patron,
    WorldBuff,
}

/// <summary>One atom in a container's <b>fixed core</b> — always present, in <c>seq</c> order.</summary>
/// <param name="Seq">
/// Authoring order, and stable. <b>Not an execution guarantee</b> (definitions §0): execution order
/// belongs to the actor's effect list, which sorts by priority across every container it holds.
/// </param>
/// <param name="OverridesJson">Value-spec overrides on the referenced atom; see E2.</param>
public sealed record ContainerAtomRow(int Seq, string AtomId, string? OverridesJson = null);

/// <summary>One candidate in a container's <b>weighted pool</b> — offered, not guaranteed.</summary>
/// <param name="AffixId">
/// References an <see cref="AffixRow"/>, never a bare atom directly (`affix-schema`, T3.1 —
/// `definitions.md` §4a: "effect_container_pool rows reference affixes, not bare atoms"). The
/// overwhelming majority are single-atom affixes generated 1:1 from the atom catalog
/// (`affix-library`, module 3); a hand-authored multi-atom bundle is the exception, not the rule.
/// </param>
/// <param name="Weight">Spawn weight. <c>0</c> excludes the row without deleting it; negative rejects.</param>
/// <param name="Group">
/// At most one atom per group per instance — PoE's mod-family rule, which is what stops a rolled item
/// reading `+10 atk / +12 atk / +14 atk`. Defaults to <c>(family_id, variant)</c> so a container may
/// roll *fire* power and *ice* power, but never two tiers of the same variant.
/// </param>
public sealed record ContainerPoolRow(string AffixId, int Weight, string? Group = null);

/// <summary>
/// Which roll budget an affix consumes (`item/seed-contract.md` §2.1 — DERIVED from the kind(s) its
/// refs resolve to, never authored). A bundle spanning both a permanent-modifier ref and a triggered
/// ref is <see cref="Mixed"/> and consumes one prefix roll <b>and</b> one suffix roll simultaneously
/// (A1, `effect-pipeline-ideal.md` §9) — never doubling either count, never picked by whichever ref
/// happens to be first.
/// </summary>
public enum AffixClass { Prefix, Suffix, Mixed }

/// <summary>
/// One reference inside an affix bundle — either a concrete atom, or a <b>slot</b>: a parameterised
/// reference naming a domain (e.g. `element`) and a pick count, resolved to a concrete variant at roll
/// time (module 2, `resolution-order`). Exactly one of <see cref="AtomId"/> or
/// (<see cref="SlotName"/>, <see cref="SlotDomain"/>) is set — never both, never neither.
/// </summary>
/// <param name="Seq">Authoring order within the bundle — stable, mirrors <see cref="ContainerAtomRow.Seq"/>.</param>
/// <param name="AtomId">Set for a concrete ref. The atom catalog and its unique key are unchanged —
/// only the container's own reference becomes parameterised, never the atom itself.</param>
/// <param name="SlotName">Set for a slot ref — e.g. `E1` in `atom.elemental-power.$E1`.</param>
/// <param name="SlotDomain">The vocabulary the slot draws from (e.g. `element`). Validated at load: a
/// patterned ref must resolve for <b>every</b> member of its domain, or the affix is rejected whole —
/// a missing element row is a load-time defect, never a roll-time surprise.</param>
/// <param name="SlotAtomPattern">
/// For a slot ref: the atom-id family/variant pattern with the slot name as a `$`-prefixed
/// placeholder (e.g. <c>atom.elemental-power.$E1</c>). Resolved against a concrete domain member by
/// substituting the placeholder — the tier suffix is appended later, at roll time (module 2), since
/// tier resolves after slots per the normative resolution order.
/// </param>
public sealed record AffixRefRow(
    int Seq, string? AtomId, string? SlotName = null, string? SlotDomain = null,
    int SlotPick = 0, string? SlotAtomPattern = null)
{
    public bool IsSlot => SlotName is not null;
}

/// <summary>
/// A named bundle of atom refs (which may include slots), drawn together as one roll — the pool's
/// actual unit (`definitions.md` §4a). What makes *"master of fire and ice"* (two families, one
/// correlated element choice) expressible: today's one-ref-per-row pool could not correlate two
/// independent draws.
///
/// <para><b><see cref="Class"/> is nullable</b> (E32, spec-affix-import-path.md §3.2, decided
/// 2026-09-03): <c>null</c> means "not authored, derive it" — the shape a real generator/authoring
/// pipeline emits. Every consumer OUTSIDE the validate/import path (<c>Resolver</c>,
/// <c>Instantiator</c>, <c>EligibilityRule</c>, <c>ContainerValidator</c>) only ever sees an
/// <see cref="AffixRow"/> already resolved — read back from storage or built by
/// <c>AffixLibraryGenerator</c> — where <see cref="Class"/> is always concrete;
/// <see cref="AffixValidator.ResolveClass"/> is the one place that fills a <c>null</c> in, and it runs
/// before anything is written.</para>
/// </summary>
public sealed record AffixRow(string AffixId, AffixClass? Class, IReadOnlyList<AffixRefRow> Refs);

/// <summary>
/// A named, ordered bundle of atom references, optionally with a weighted pool it rolls from.
///
/// <para><b>Containers are mechanism, not content.</b> This holds <i>what a skill contains</i> —
/// never <i>when it fires</i>. Activation, cooldown, and targeting belong to the turn kernel and the
/// action layer.</para>
/// </summary>
public sealed record ContainerRow
{
    public string ContainerId { get; init; } = "";

    public ContainerKind Kind { get; init; } = ContainerKind.Item;

    /// <summary>Item slots; null for kinds that do not occupy one.</summary>
    public string? Slot { get; init; }

    /// <summary>Rarity id, resolved against the `rarity` table's append-only ordinals.</summary>
    public string? Rarity { get; init; }

    /// <summary>
    /// The tier window the pool may offer. <b>Rarity and tier are different axes</b>: tier is how
    /// strong one affix is; rarity selects how many are drawn and which tiers are allowed.
    /// </summary>
    public int? MinTier { get; init; }
    public int? MaxTier { get; init; }

    /// <summary>Enforced at <b>bind</b> (E6, `LevelTooLow`) — a declared field nothing reads is a bug.</summary>
    public int? LevelReq { get; init; }

    /// <summary>How many affixes to draw from the PREFIX pool. <c>0</c>/<c>0</c> with
    /// <see cref="SuffixRolls"/> means the fixed list alone. Replaces the single <c>PoolRolls</c>
    /// (T3.2, `spec-container-schema.md:27` — "replaces the single `pool_rolls` column"): D2 and PoE
    /// both cap the two classes separately, so a single count would let a container roll every draw
    /// permanent and none triggered, or the reverse.</summary>
    public int PrefixRolls { get; init; }

    /// <summary>How many affixes to draw from the SUFFIX pool. See <see cref="PrefixRolls"/>.</summary>
    public int SuffixRolls { get; init; }

    public string TagsJson { get; init; } = "{}";

    public bool Enabled { get; init; } = true;

    public long Revision { get; init; }

    public IReadOnlyList<ContainerAtomRow> Atoms { get; init; } = Array.Empty<ContainerAtomRow>();

    public IReadOnlyList<ContainerPoolRow> Pool { get; init; } = Array.Empty<ContainerPoolRow>();

    /// <summary>The `container_id` prefix a kind requires — the grammar in definitions §1.</summary>
    public static string PrefixOf(ContainerKind kind) => kind switch
    {
        ContainerKind.Item => "item",
        ContainerKind.Trait => "trait",
        ContainerKind.Skill => "skill",
        ContainerKind.SpeciesPassive => "species-passive",
        ContainerKind.Patron => "patron",
        ContainerKind.WorldBuff => "world-buff",
        _ => "",
    };
}

/// <summary>
/// A rarity band. Ordinals are <b>explicit and append-only</b>: they are load-bearing for sorting and
/// for the budget lookup, so a reorder silently re-prices every container that names one.
///
/// <para><see cref="PrefixRolls"/>/<see cref="SuffixRolls"/> (T3.2, `ssot-rarity.md` §3.3's own
/// prefix/suffix band table, added 2026-09-01) replace the single <c>PoolRolls</c> — each rung's
/// combined count band splits into a prefix band and a suffix band, summing to the same total the
/// ladder already published. Starting values; a balance pass tunes them per rung.</para>
/// </summary>
public sealed record RarityRow(string RarityId, int Ordinal, int PrefixRolls, int SuffixRolls, int MinTier, int MaxTier);
