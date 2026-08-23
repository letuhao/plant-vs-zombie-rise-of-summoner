using FusionRpg.Tools.ItemSeedValidator.Model;

namespace FusionRpg.Tools.ItemSeedValidator.Checks;

/// <summary>
/// ssot-uniques.md §3.7 states three content rules for uniques that no registry encodes, so nothing
/// mechanical enforced them and eighteen partitions authored against eighteen readings. One agent
/// read the lane document, honoured the role ban, and said so; the other seventeen did not, which is
/// the difference between a rule that is written down and a rule that is checked.
///
/// A unique carries no `role` of its own — it occupies the role of the base type it is built on —
/// so both checks resolve through `baseType`.
/// </summary>
public static class UniqueRuleCheck
{
    /// <summary>ssot-uniques.md §3.7: at most 8 of the 15 roles per frame carry a unique in v1.</summary>
    const int RolesPerFrameQuota = 8;

    /// <summary>
    /// The quota is a number; authoring needs a list. An agent that sees only its own partition
    /// cannot know which eight roles the other seventeen chose, so — exactly like the id namespaces
    /// — the choice is made centrally once and handed down.
    ///
    /// These are the eight highest `budgetWeightMilli` roles in `core.v1.json`, ties broken by
    /// registry order. Both `jewel-minor` roles fall outside it anyway, at 15 milli each, which is
    /// a useful cross-check on §3.7's separate ban rather than a coincidence.
    ///
    /// This particular eight is a **default, not a derivation** — "uniques go where the budget is"
    /// is defensible and so is its opposite, given §3.5's "the rare wins the stat sheet, the unique
    /// wins the build". It is one list in one place; changing it is an edit and a re-run.
    /// </summary>
    public static readonly string[] AllowedRoles =
    {
        "armament-primary", "core-guard", "ward-array", "armament-secondary",
        "jewel-major", "manipulator", "mantle", "head-guard",
    };

    public static void Run(ValidationContext ctx)
    {
        var rolesByFrame = new Dictionary<string, Dictionary<string, SeedEntry>>(StringComparer.Ordinal);
        var axisSlots = new Dictionary<string, SeedEntry>(StringComparer.Ordinal);

        foreach (var entry in ctx.Entries)
        {
            if (!string.Equals(entry.File.Kind, "unique", StringComparison.Ordinal)) continue;
            if (entry.File.IsExemplar) continue;   // a pattern, not corpus content
            if (entry.AsString("baseType") is not { } baseTypeId) continue;
            if (!ctx.ById.TryGetValue(baseTypeId, out var baseType)) continue;   // ReferenceUnresolved owns that
            if (baseType.AsString("role") is not { } role) continue;

            // "No uniques on the two `jewel-minor` roles in v1" — a duplicated role with the
            // smallest budget is doubled by construction, which is the fastest path to every build
            // looking the same.
            if (role.StartsWith("jewel-minor", StringComparison.Ordinal))
                ctx.Error(entry, "UniqueRoleForbidden", "ssot-uniques.md §3.7",
                    $"base type '{baseTypeId}' occupies role '{role}'; uniques are barred from the "
                    + "two jewel-minor roles in v1");

            // (role, rung band, power axis) must be unique corpus-wide. The band comes from the
            // partition — `uniques/{theme}/{ordinal}` — because it is the allocation's own key.
            if (entry.AsString("powerAxis") is { } axis)
            {
                if (!ctx.Registries.PowerCategoryIds.Contains(axis, StringComparer.Ordinal))
                    ctx.Error(entry, "RegistryValueUnknown", "core.v1.json powerCategories",
                        $"powerAxis '{axis}' is not one of "
                        + string.Join(", ", ctx.Registries.PowerCategoryIds));
                else
                {
                    var band = entry.Partition.Split('/').LastOrDefault() ?? "?";
                    var key = $"{band}|{role}|{axis}";
                    if (axisSlots.TryGetValue(key, out var held))
                        ctx.Error(entry, "UniqueAxisCollision", "ssot-uniques.md §3.7",
                            $"role '{role}' at rung band {band} on axis '{axis}' is already taken "
                            + $"by {held.Label} ({held.Partition}); one unique per "
                            + "(role, rung band, power axis) is what stops every build converging "
                            + "on the same slot");
                    else axisSlots[key] = entry;
                }
            }

            var frame = baseType.AsString("frame") ?? entry.AsString("frame") ?? "(unknown)";
            if (!rolesByFrame.TryGetValue(frame, out var roles))
                rolesByFrame[frame] = roles = new Dictionary<string, SeedEntry>(StringComparer.Ordinal);
            if (!roles.ContainsKey(role)) roles[role] = entry;
        }

        // Reported per offending role rather than per frame, so each partition sees the entries it
        // owns instead of a corpus-wide total it cannot act on.
        foreach (var (frame, roles) in rolesByFrame)
        {
            foreach (var (role, entry) in roles)
            {
                if (AllowedRoles.Contains(role, StringComparer.Ordinal)) continue;
                if (role.StartsWith("jewel-minor", StringComparison.Ordinal)) continue; // already reported
                ctx.Error(entry, "UniqueRoleQuota", "ssot-uniques.md §3.7",
                    $"role '{role}' (frame '{frame}') is outside the eight roles allocated to carry "
                    + $"uniques in v1: {string.Join(", ", AllowedRoles)}. At most "
                    + $"{RolesPerFrameQuota} of 15 roles per frame may carry one, and the choice is "
                    + "made corpus-wide because no single partition can see the other seventeen");
            }
        }
    }
}
