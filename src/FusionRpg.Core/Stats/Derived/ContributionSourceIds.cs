namespace FusionRpg.Core.Stats.Derived;

/// <summary>
/// GG-49 SourceId grammar for Hot derived contributions. Producers mint via these helpers;
/// sheet projections parse them into fiction labels. Empty / bare "generic" ids are defects.
/// </summary>
public static class ContributionSourceIds
{
    public const string Progression = "rpg.progression";

    /// <summary>UniqueActor Hub base resource.max/regen seed (Condition /sheet pools).</summary>
    public const string ResourceBaseline = "rpg.resource.base";

    public static string Equip(string role, string itemRefId)
    {
        var r = string.IsNullOrWhiteSpace(role) ? "unknown" : role.Trim();
        var item = string.IsNullOrWhiteSpace(itemRefId) ? "unknown" : itemRefId.Trim();
        return $"equip:{r}:{item}";
    }

    public static string Tree(string treeId, string nodeId) =>
        $"tree.{NullToUnknown(treeId)}.{NullToUnknown(nodeId)}";

    public static string Aptitude(string share) => $"aptitude.{NullToUnknown(share)}";

    public static string Status(string instanceId) => $"status:{NullToUnknown(instanceId)}";

    public static string Grant(string effectOrGrantId) => $"grant:{NullToUnknown(effectOrGrantId)}";

    public static string Primary(string sourceKind, string sourceId) =>
        $"primary:{NullToUnknown(sourceKind)}|{NullToUnknown(sourceId)}";

    /// <summary>Fiction label for InspectSplit — never raw-only when grammar matches.</summary>
    public static string FictionLabel(string sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return "(unattributed)";
        if (string.Equals(sourceId, Progression, StringComparison.Ordinal)) return "Progression";
        if (string.Equals(sourceId, ResourceBaseline, StringComparison.Ordinal)) return "Resource base";

        if (sourceId.StartsWith("equip:", StringComparison.Ordinal))
        {
            var parts = sourceId.Split(':', 3);
            var role = parts.Length > 1 ? parts[1] : "unknown";
            var item = parts.Length > 2 ? parts[2] : "";
            return string.IsNullOrEmpty(item) || item == "unknown"
                ? $"Equip · {role}"
                : $"Equip · {role} ({item})";
        }

        if (sourceId.StartsWith("aptitude.", StringComparison.Ordinal))
            return $"Aptitude · {sourceId["aptitude.".Length..]}";

        if (sourceId.StartsWith("tree.", StringComparison.Ordinal))
            return $"Tree · {sourceId["tree.".Length..].Replace('.', '/')}";

        if (sourceId.StartsWith("status:", StringComparison.Ordinal))
            return $"Status · {sourceId["status:".Length..]}";

        if (sourceId.StartsWith("grant:", StringComparison.Ordinal))
            return $"Grant · {sourceId["grant:".Length..]}";

        if (sourceId.StartsWith("primary:", StringComparison.Ordinal))
            return $"Primary · {sourceId["primary:".Length..]}";

        return sourceId;
    }

    static string NullToUnknown(string? s) =>
        string.IsNullOrWhiteSpace(s) ? "unknown" : s.Trim();
}
