using FusionRpg.Core.PassiveTree.Catalog;

namespace FusionRpg.Core.PassiveTree.Binding;

/// <summary>One node's affixes resolved to atom rows and priced — the output of
/// <see cref="CoefficientBinder"/> + <see cref="AffixComposer"/> (task B4). A `NodeAtom` per
/// resolved atom, each carrying its own `kMicro` (a node's affixes can write different channels
/// with different anchors, so the coefficient is per-atom, not per-node).</summary>
public sealed record BoundNode(string NodeId, IReadOnlyList<NodeAtom> Atoms);

public sealed class BindRefusal : Exception
{
    public BindRefusal(string message) : base(message) { }
}
