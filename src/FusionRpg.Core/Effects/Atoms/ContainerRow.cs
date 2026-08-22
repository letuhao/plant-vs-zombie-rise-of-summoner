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
/// <param name="Weight">Spawn weight. <c>0</c> excludes the row without deleting it; negative rejects.</param>
/// <param name="Group">
/// At most one atom per group per instance — PoE's mod-family rule, which is what stops a rolled item
/// reading `+10 atk / +12 atk / +14 atk`. Defaults to <c>(family_id, variant)</c> so a container may
/// roll *fire* power and *ice* power, but never two tiers of the same variant.
/// </param>
public sealed record ContainerPoolRow(string AtomId, int Weight, string? Group = null);

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

    /// <summary>How many atoms to draw from the pool. <c>0</c> means the fixed list alone.</summary>
    public int PoolRolls { get; init; }

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
/// </summary>
public sealed record RarityRow(string RarityId, int Ordinal, int PoolRolls, int MinTier, int MaxTier);
