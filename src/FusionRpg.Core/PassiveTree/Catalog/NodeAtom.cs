using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.PassiveTree.Catalog;

/// <summary>The four ops a node atom may carry (spec-tree-catalog.md §2.3) — matches
/// `AtomRowValidator.DerivedOps` exactly; there is no dedicated shared enum in the atom layer
/// (ops are validated there as raw strings against `StatOps`/`DerivedOps`), so this is the
/// catalog's own typed view over the same four values.</summary>
public enum NodeAtomOp
{
    Flat,
    Increased,
    Replace,
    Flag,
}

/// <summary>§2.4 — three-valued function of <see cref="UnitClass"/>, computed by the binder at bake
/// time and stored so the read path is a lookup, never a rederivation. Not a fourth channel
/// classification (DESIGN-GATE.md §34's warning) — a use of the third.</summary>
public enum ScaleAxis
{
    /// <summary>`GameUnits`, `GameUnitsPerSecond`, `ReciprocalPoints` — `kMicro · P(Θ_node) / 1e6`.</summary>
    PTheta,

    /// <summary>`SigmoidPoints`, `SigmoidMultiplierPoints`, `StatusPotencyPoints` — `kMicro · Θ_node / 1e6`
    /// (PS-3: contests read Θ, linear — never P(Θ), which would rise on the sheet with no
    /// multiplier effect).</summary>
    Theta,

    /// <summary>`PerMilleRatio` — flat per-mille points planned against the clamp. A bounded ratio,
    /// exempt from PS-8 by nature.</summary>
    FlatPermille,
}

/// <summary>
/// The magnitude fields of one node's granted atom — and there is no magnitude among them
/// (spec-tree-catalog.md §2.3). <c>kMicro</c> is the only number the catalog carries about
/// strength, and it is a coefficient (a per-million SHARE of <c>P(Θ)</c>), not a resolved value.
/// </summary>
public sealed record NodeAtom(
    string KindId,
    Effects.Atoms.AttachPoint AttachPoint,
    string ChannelId,
    NodeAtomOp Op,
    string? Trigger,
    string? WhenJson,
    long KMicro,
    ScaleAxis ScaleAxis,
    UnitClass UnitClass,
    string? SoulCurveId);
