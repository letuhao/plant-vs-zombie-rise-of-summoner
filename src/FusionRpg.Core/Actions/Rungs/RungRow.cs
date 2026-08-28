namespace FusionRpg.Core.Actions.Rungs;

/// <summary>
/// One authored rung (spec-rung-table.md §2). Every multiplier is a per-mille integer — the
/// exponent forms documenting how these were derived (`1.75^((r-1)/2)` etc.) are never evaluated at
/// runtime; a human computed them once.
/// </summary>
public sealed record RungRow(
    int Rung,
    int MinTier,
    int MaxTier,
    int PoolRolls,
    int QPowerMilli,
    int CostMulti,
    int CdMulti,
    IReadOnlyList<string> StructureBudget);
