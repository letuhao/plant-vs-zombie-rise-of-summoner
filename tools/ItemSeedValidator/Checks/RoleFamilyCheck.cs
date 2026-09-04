using System.Text.Json;
using FusionRpg.Tools.ItemSeedValidator.Model;

namespace FusionRpg.Tools.ItemSeedValidator.Checks;

/// <summary>
/// `item_role_family` legality at seed time (item-ideal.md, `affix-legality` module 8): the two new
/// override artefacts (`family-overrides.v1.json`, `role-relocation.v1.json`) must name only families
/// and roles that genuinely exist in the corpus — an override for a typo'd family id would silently
/// do nothing, which is worse than an error.
/// </summary>
public static class RoleFamilyCheck
{
    public static void Run(ValidationContext ctx)
    {
        var familyRoles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var entry in ctx.Entries)
        {
            if (entry.File.Kind != "affix-family") continue;
            if (entry.File.IsExemplar) continue; // a pattern, not corpus content
            if (entry.Id is not { } id) continue;
            var roles = (entry.Node["roles"] as System.Text.Json.Nodes.JsonArray)?
                .OfType<System.Text.Json.Nodes.JsonValue>()
                .Select(v => v.TryGetValue<string>(out var s) ? s : null)
                .OfType<string>().ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>();
            familyRoles[id] = roles;
        }

        if (familyRoles.Count == 0) return; // no affix-family corpus loaded (e.g. a scoped test run)

        CheckOverrides(ctx, familyRoles);
        CheckRelocation(ctx, familyRoles);
    }

    static void CheckOverrides(ValidationContext ctx, Dictionary<string, HashSet<string>> familyRoles)
    {
        var path = Path.Combine(ctx.Registries.RegistryDir, "family-overrides.v1.json");
        if (!File.Exists(path)) return;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        if (doc.RootElement.TryGetProperty("removedFamilies", out var removed))
            foreach (var r in removed.EnumerateArray())
            {
                var role = r.GetProperty("role").GetString()!;
                var familyId = r.GetProperty("familyId").GetString()!;
                if (!familyRoles.TryGetValue(familyId, out var roles))
                    ctx.CorpusError("RoleFamilyOverrideUnknownFamily", "family-overrides.v1.json",
                        $"removedFamilies names '{familyId}', which is not a family in the corpus");
                else if (!roles.Contains(role))
                    ctx.CorpusError("RoleFamilyOverrideNotLegal", "family-overrides.v1.json",
                        $"removedFamilies names '{familyId}' on role '{role}', but that family is not legal there to begin with");
            }
    }

    static void CheckRelocation(ValidationContext ctx, Dictionary<string, HashSet<string>> familyRoles)
    {
        var path = Path.Combine(ctx.Registries.RegistryDir, "role-relocation.v1.json");
        if (!File.Exists(path))
        {
            ctx.CorpusError("RoleRelocationArtefactMissing", "spec-affix-legality.md",
                "role-relocation.v1.json does not exist -- D3's relocation (ward-array/head-guard/sense) is unrecorded");
            return;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var row in doc.RootElement.GetProperty("relocations").EnumerateArray())
        {
            var familyId = row.GetProperty("familyId").GetString()!;
            var hostRole = row.GetProperty("hostRole").GetString()!;
            if (!familyRoles.TryGetValue(familyId, out var roles))
            {
                ctx.CorpusError("RoleRelocationUnknownFamily", "role-relocation.v1.json",
                    $"relocation names '{familyId}', which is not a family in the corpus");
                continue;
            }

            if (!roles.Contains(hostRole))
                ctx.CorpusError("RoleRelocationHostNotLegal", "role-relocation.v1.json",
                    $"relocation moves '{familyId}' onto host '{hostRole}', but that family is not legal on that role");
        }
    }
}
