namespace FusionRpg.Tools.ItemSeedValidator.Registries;

/// <summary>
/// The bridge between three things the wave-0 artifacts name differently: the seed file's
/// <c>kind</c> (seed-contract.md §9), the directory it lives in, and naming.v1.json's
/// <c>idNamespaces</c> key. Nothing in the registries states this mapping, so it lives here —
/// and <see cref="MissingNamespaces"/> fails loudly if the registry grows a namespace this
/// catalog does not know, rather than letting it validate against nothing.
/// </summary>
public sealed record SeedKind(
    string Kind,
    string Directory,
    string NamespaceKey,
    string Stage,
    bool ShapeDefined,
    IReadOnlyList<string> RequiredFields,
    IReadOnlyList<string> AllowedFields);

public static class KindCatalog
{
    /// <summary>Envelope fields every entry may carry, whatever its kind (seed-contract.md §9).</summary>
    public static readonly string[] CommonFields =
    {
        "id", "nameKey", "name", "tags", "notes", "enabled", "overrides",
        "flavor", "flavorKey", "iconKey", "unlockGate",
    };

    /// <summary>Fields every entry must carry.</summary>
    public static readonly string[] CommonRequired = { "id", "nameKey", "name" };

    /// <summary>_meta provenance the contract requires (§9). sourceRef is warned, not required.</summary>
    public static readonly string[] RequiredMeta =
    {
        "batch", "partition", "contractVersion", "registryVersions",
        "exemplarVersion", "promptVersion", "model", "authoredUtc",
    };

    static SeedKind Defined(string kind, string dir, string ns, string stage,
        string[] required, string[] extra) =>
        new(kind, dir, ns, stage, ShapeDefined: true,
            CommonRequired.Concat(required).ToArray(),
            CommonFields.Concat(extra).ToArray());

    static SeedKind Undefined(string kind, string dir, string ns, string stage) =>
        new(kind, dir, ns, stage, ShapeDefined: false, CommonRequired, CommonFields);

    /// <summary>Every kind, keyed by its <c>kind</c> value.</summary>
    public static readonly IReadOnlyDictionary<string, SeedKind> All = new[]
    {
        // Shapes seed-contract.md §10 actually specifies.
        Defined("base-type", "base-types", "baseTypes", "1b",
            required: new[] { "frame", "role", "class", "band", "iconKey", "tags" },
            extra: new[] { "frame", "role", "class", "band", "implicit", "socketMax" }),
        // "roles" is not in this required list even though it is effectively mandatory: the
        // requirement is "roles" OR the pre-rename "roleGroups", and the generic single-key
        // RequiredFields check has no OR — ReferenceCheck.CheckRoles enforces the real rule
        // (missing both is RequiredFieldMissing; roleGroups alone warns RoleGroupsRenamed instead
        // of rejecting) so an author who has not yet migrated off the old name is not blamed for
        // a contract rename.
        Defined("affix-family", "affix-families", "affixFamilies", "1a",
            required: new[] { "kindId", "powerBand", "tags" },
            extra: new[]
            {
                "kindId", "params", "variants", "frames", "side", "roles", "roleGroups",
                "powerBand", "nameWords", "displayTemplate", "channel",
            }),
        Defined("unique", "uniques", "uniques", "1c",
            required: new[]
            {
                "frame", "baseType", "rarity", "fixedAtoms", "counterPressure", "tags",
                // ssot-uniques.md §3.7 keys its anti-convergence rule on (role, rung band, power
                // axis) and the shape carried no axis, so the rule was unverifiable and eighteen
                // partitions were trusted rather than checked. Required, not optional: an optional
                // field that half the corpus omits cannot gate anything.
                "powerAxis",
            },
            extra: new[]
            {
                "frame", "baseType", "rarity", "fixedAtoms", "varianceSlot",
                "counterPressure", "theme", "themeKey", "acquisition", "powerAxis",
            }),
        Defined("set", "sets", "sets", "1c",
            required: new[] { "themeKey", "members", "thresholds" },
            extra: new[] { "themeKey", "theme", "members", "thresholds" }),

        // Shapes docs/architecture/item/entry-shapes.md gives, closing the gap the ten
        // UnknownKeyShapeUndefined warnings used to report. Field lists are transcribed from that
        // document's per-kind tables; DERIVED/GENERATED fields (tiers, resonances, weight, grade
        // on a consumable, souls_cost, qty_curve_id, affixClass, min_count/max_count, …) are
        // deliberately absent so they stay rejectable as UnknownKey.
        Defined("gem", "gems", "gems", "1b",
            required: new[] { "family", "powerBand" },
            extra: new[] { "family", "element", "powerBand", "affinityElement" }),
        Defined("material", "materials", "materials", "1a",
            required: new[] { "runtimeId", "materialClass" },
            extra: new[] { "runtimeId", "materialClass", "element", "frame", "grade" }),
        Defined("curve", "curves", "curves", "1a",
            required: new[] { "input", "points" },
            extra: new[] { "input", "points" }),
        Undefined("attribute", "attributes", "attributes", "1a"),
        Defined("charm", "charms", "charms", "1c",
            required: new[] { "charmClass", "apCost", "axis", "frameHint", "fixedAtoms" },
            extra: new[]
            {
                "charmClass", "apCost", "axis", "frameHint", "uniqueCarry", "fixedAtoms",
                "roleGroups", "poolRolls",
            }),
        Defined("socket-word", "socket-words", "socketWords", "1c",
            required: new[] { "runtimeId", "minSockets", "ingredients", "fixedAtoms" },
            extra: new[] { "runtimeId", "hostRole", "hostFrame", "minSockets", "ingredients", "fixedAtoms" }),
        Defined("recipe", "recipes", "recipes", "1c",
            required: new[] { "operation", "outputKind", "frame", "costLines" },
            extra: new[]
            {
                "operation", "outputKind", "outputRef", "outputQty", "frame", "costLines", "soulsCostBand",
            }),
        Defined("enhancement-milestone", "enhancement-milestones", "enhancementMilestones", "1c",
            required: new[] { "runtimeFamily", "kindId", "params", "powerBand" },
            extra: new[] { "runtimeFamily", "kindId", "params", "powerBand" }),
        Defined("consumable", "consumables", "consumables", "1c",
            required: new[] { "classId", "useContext", "family", "powerBand" },
            extra: new[]
            {
                "classId", "useContext", "family", "element", "powerBand", "manifestCost",
                "grantsActionId", "cooldownKey",
            }),
        Defined("drop-table", "drop-tables", "dropTables", "1c",
            required: new[] { "sourceAllow", "groups" },
            extra: new[] { "sourceAllow", "groups" }),
        Defined("display-template", "display-templates", "displayTemplates", "1c",
            required: new[] { "runtimeFamily", "groupId", "status" },
            extra: new[] { "runtimeFamily", "plantOverrideKey", "plantOverrideName", "groupId", "status" }),
    }.ToDictionary(k => k.Kind, StringComparer.Ordinal);

    public static SeedKind? ByDirectory(string directory) =>
        All.Values.FirstOrDefault(k => string.Equals(k.Directory, directory, StringComparison.Ordinal));

    public static SeedKind? ByNamespace(string namespaceKey) =>
        All.Values.FirstOrDefault(k => string.Equals(k.NamespaceKey, namespaceKey, StringComparison.Ordinal));

    /// <summary>
    /// idNamespaces keys this catalog does not cover. A non-empty result means naming.v1.json
    /// allocated a namespace nothing here validates — a stop-the-fleet gap, not a warning.
    /// </summary>
    public static IReadOnlyList<string> MissingNamespaces(RegistrySet registries)
    {
        var declared = (registries.Naming["idNamespaces"] as System.Text.Json.Nodes.JsonObject)
            ?.Select(kv => kv.Key)
            .Where(k => !k.StartsWith('_') && k != "partitionCountCheck")
            .ToList() ?? new List<string>();
        return declared.Where(k => ByNamespace(k) is null).ToList();
    }
}
