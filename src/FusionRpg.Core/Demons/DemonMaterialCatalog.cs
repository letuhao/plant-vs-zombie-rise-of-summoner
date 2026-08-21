using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Demons;

/// <summary>
/// Fusion material stubs (spec-expeditions.md §Resolver): element essences + rarity shards.
/// demon-fusion consumes these later; until then they are inventory rows with validated ids.
/// </summary>
public static class DemonMaterialCatalog
{
    public static readonly IReadOnlyList<string> All = Build();

    static IReadOnlyList<string> Build()
    {
        var ids = new List<string>();
        foreach (var element in ElementRoster.Concrete)
            ids.Add($"essence.{element.ToElementId()}");
        foreach (var rarity in new[] { DemonRarity.Common, DemonRarity.Rare, DemonRarity.Epic, DemonRarity.Legendary })
            ids.Add($"shard.{rarity.ToId()}");
        return ids;
    }

    static readonly HashSet<string> Known = new(All, StringComparer.Ordinal);

    public static bool IsKnown(string? materialId) => materialId != null && Known.Contains(materialId);
}
