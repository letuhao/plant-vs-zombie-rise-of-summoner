namespace FusionRpg.Core.Actions;

/// <summary>
/// A18a (spec-action-container-binding.md §1). Resolves a compiled action's <c>ContainerId</c> to the
/// <c>EffectDefDto</c> ids <c>AtomCompiler</c> produced from that container's atoms — the seam A20
/// (synthetic-loadout-harness) is the production supplier for; tests construct one directly, same as
/// <see cref="ActionCatalog"/> today.
/// </summary>
public interface IContainerEffectResolver
{
    /// <summary>Empty span for a non-existent or pooled container — loud rejection at bind time
    /// (battle's own loadout-compile loop), never a silent skip.</summary>
    IReadOnlyList<string> EffectIdsFor(string containerId);
}

/// <summary>The minimal, in-memory default — the same weight class as <see cref="ActionCatalog.Build"/>,
/// not a new content-authoring pipeline.</summary>
public sealed class DictionaryContainerEffectResolver : IContainerEffectResolver
{
    readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _map;

    public DictionaryContainerEffectResolver(IReadOnlyDictionary<string, IReadOnlyList<string>> map) =>
        _map = map ?? throw new ArgumentNullException(nameof(map));

    public IReadOnlyList<string> EffectIdsFor(string containerId) =>
        _map.TryGetValue(containerId, out var ids) ? ids : Array.Empty<string>();
}
