using FusionRpg.Contracts;

namespace FusionRpg.Core.Effects.Atoms;

/// <summary>
/// One atom the Secondary runner owns. <b>E7 owns this contract; E15 runs it and E19 ships it.</b>
///
/// <para>Everything here is already resolved: the predicate is compiled to E13's chosen form, value
/// bounds are pre-multiplied by their curve, and status and element names are interned to ints. The
/// injector receives this and never needs a content row to decide anything — which is exactly why it
/// needs no content tables.</para>
/// </summary>
/// <param name="Predicate">
/// Compiled, never a tree. <see cref="PredicateCompiler.Always"/> when the atom has no condition.
/// </param>
/// <param name="ChanceMilli">Per-mille gate, 1000 when unconditional.</param>
/// <param name="IcdMs">Internal cooldown. Keyed on <paramref name="IcdKey"/>, not on this entry.</param>
/// <param name="IcdKey">
/// The compile-time grouping key. Atoms sharing one were merged into a single grant, so their clock
/// is shared by construction rather than by a runtime lookup (definitions §14.1).
/// </param>
/// <param name="Values">
/// Resolved value bounds per param. An <c>OnApply</c> range stays a range — that is the roll the
/// runner exists to make — but its curve has already been applied.
/// </param>
public sealed record RunnerEntry(
    string AtomId,
    string KindId,
    string? Trigger,
    ICompiledPredicate Predicate,
    int ChanceMilli,
    int IcdMs,
    string IcdKey,
    IReadOnlyDictionary<string, ValueBounds> Values)
{
    public bool IsUnconditional => ChanceMilli >= 1000 && ReferenceEquals(Predicate, PredicateCompiler.Always);
}

/// <summary>
/// A value's resolved bounds. <b>Curve-scaled already</b>: E19 forbids curve rows from travelling, so
/// the injector could not scale a value even if it wanted to (definitions §13 D9).
/// </summary>
public readonly record struct ValueBounds(int Min, int Max, RollPolicy Roll)
{
    public bool IsFixed => Min == Max;

    public static ValueBounds Of(ValueSpec spec, int multiplierMilli = 1000) =>
        new(CurveTable.ApplyMilli(spec.Min, multiplierMilli),
            CurveTable.ApplyMilli(spec.Max, multiplierMilli),
            spec.Roll);
}

/// <summary>
/// The baked output of one catalog revision: what Foundation runs, and what the runner runs.
///
/// <para>Baking happens <b>once per catalog revision</b>, not per bind — and the same revision must
/// produce identical bytes, or a push cannot be compared against what the injector already holds.</para>
/// </summary>
public sealed record CompiledCatalog(
    long CatalogRevision,
    IReadOnlyList<EffectDefDto> Defs,
    IReadOnlyList<EffectGrantDto> Compiled,
    IReadOnlyList<string> CompiledAtomIds,
    IReadOnlyList<RunnerEntry> Runtime,
    IReadOnlyList<CompileRejection> Rejected)
{
    /// <summary>
    /// Every atom id that reached a path — the completeness check that nothing was dropped.
    ///
    /// <para><see cref="CompiledAtomIds"/> is carried rather than derived: a def names its ICD group,
    /// and an action carries no id, so several atoms can hide behind one def.</para>
    /// </summary>
    public IEnumerable<string> AllAtomIds =>
        CompiledAtomIds.Concat(Runtime.Select(r => r.AtomId)).Concat(Rejected.Select(r => r.AtomId));
}

/// <summary>An atom neither path can execute here, with the reason an author needs.</summary>
public readonly record struct CompileRejection(string AtomId, AtomRejectionReason Reason, string Detail)
{
    public override string ToString() => $"{AtomId}: {Reason} — {Detail}";
}
