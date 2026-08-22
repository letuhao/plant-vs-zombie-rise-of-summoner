namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// The closed leaf list — eight ids. Adding one is a reviewed code change, because each needs a
/// reader on <see cref="FactReader"/>; a leaf with no reader is the `status.expose.*` failure again.
/// </summary>
public enum LeafId
{
    SideIs = 0,
    TypeIdIs,
    TypeIdIn,
    ActorIsKiller,
    HasStatus,
    HpBelowMilli,
    HpAboveMilli,
    ElementIs,
    RowIs,
    ColIs,
    IsMindControlled,
}

/// <summary>
/// Whose fact a leaf reads. <b>Required on every leaf</b>, with no default.
///
/// <para>On <c>OnDamageDealt</c> the shipped overlay inverts side and typeId — `filters.side` means
/// the <i>damaged</i> entity, not the attacker (<c>EffectProcAndOwner.ResolveFilterTarget</c>). That
/// inversion is a property of the <b>event</b>, so `hasStatus` and `hpBelowMilli` are exactly as
/// ambiguous as `sideIs`. Defaulting any of them would bake the trap in.</para>
/// </summary>
public enum Subject
{
    /// <summary>The entity the atom is bound to.</summary>
    Self = 0,

    /// <summary>The other entity in the event — the one damaged, killed, or spawned.</summary>
    Target,
}

/// <summary>A typed predicate tree. Four shapes, no syntax, nothing parseable.</summary>
public abstract record PredicateNode
{
    public sealed record And(IReadOnlyList<PredicateNode> Children) : PredicateNode;
    public sealed record Or(IReadOnlyList<PredicateNode> Children) : PredicateNode;
    public sealed record Not(PredicateNode Child) : PredicateNode;

    /// <summary>
    /// A closed-list leaf. <paramref name="Subject"/> is non-nullable on purpose — the compiler
    /// rejects a tree built from JSON that omitted it, and the type makes it unforgettable in code.
    /// </summary>
    /// <param name="Value">Scalar arg (`typeIdIs`, `hpBelowMilli`, `rowIs`, bool leaves as 0/1).</param>
    /// <param name="Text">String arg (`sideIs`, `hasStatus`, `elementIs`).</param>
    /// <param name="Values">Set arg (`typeIdIn`). Does not count toward the node limit.</param>
    public sealed record Leaf(
        LeafId Id,
        Subject Subject,
        int Value = 0,
        string? Text = null,
        IReadOnlyList<int>? Values = null) : PredicateNode;
}
