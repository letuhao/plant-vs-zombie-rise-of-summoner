using System.Text.Json.Nodes;
using FusionRpg.Tools.ItemSeedValidator.Model;
using FusionRpg.Tools.ItemSeedValidator.Registries;

namespace FusionRpg.Tools.ItemSeedValidator.Checks;

/// <summary>
/// D11 clause 1 at seed time (spec-base-types.md, item module 6): every role's humanoid and plant
/// `implicit.family` sets must be disjoint. Retired entries (`enabled: false`, the `standard` role's
/// legacy rows — D14, out of scope) are excluded: dead content cannot violate a live-content rule.
/// </summary>
public static class FrameDirectionCheck
{
    public static void Run(ValidationContext ctx)
    {
        var legalByRole = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (ctx.Registries.Classes["implicitSlates"] is JsonObject slates)
            foreach (var slate in slates)
                if (slate.Value is JsonObject node)
                    legalByRole[slate.Key] = RegistrySet.Strings(node["legalFamilies"]).ToHashSet(StringComparer.Ordinal);

        var byRoleFrame = new Dictionary<(string Role, string Frame), List<SeedEntry>>();
        foreach (var entry in ctx.Entries)
        {
            if (entry.File.Kind != "base-type") continue;
            if (entry.File.IsExemplar) continue; // a pattern, not corpus content
            if (entry.Node["enabled"] is JsonValue en && en.TryGetValue<bool>(out var enabled) && !enabled) continue;

            var role = entry.AsString("role");
            var frame = entry.AsString("frame");
            if (role is null || frame is null) continue;

            var family = FamilyOf(entry);
            if (family is not null && legalByRole.TryGetValue(role, out var legal) && !legal.Contains(family))
                ctx.Error(entry, "ImplicitFamilyNotLegalForRole", "classes.v2.json implicitSlates",
                    $"'{entry.Label}': implicit family '{family}' is not in role '{role}''s legalFamilies");

            var key = (role, frame);
            if (!byRoleFrame.TryGetValue(key, out var list)) byRoleFrame[key] = list = new List<SeedEntry>();
            list.Add(entry);
        }

        foreach (var role in byRoleFrame.Keys.Select(k => k.Role).Distinct(StringComparer.Ordinal).OrderBy(r => r, StringComparer.Ordinal))
        {
            var humanoid = byRoleFrame.GetValueOrDefault((role, "humanoid"), new List<SeedEntry>());
            var plant = byRoleFrame.GetValueOrDefault((role, "plant"), new List<SeedEntry>());
            if (humanoid.Count == 0 || plant.Count == 0) continue;

            var hFamilies = humanoid.Select(FamilyOf).Where(f => f is not null).ToHashSet(StringComparer.Ordinal)!;
            var pFamilies = plant.Select(FamilyOf).Where(f => f is not null).ToHashSet(StringComparer.Ordinal)!;

            var overlap = hFamilies.Intersect(pFamilies, StringComparer.Ordinal).ToList();
            if (overlap.Count > 0)
                ctx.CorpusError("FrameImplicitNotDisjoint", "spec-base-types.md D11 clause 1",
                    $"role '{role}': humanoid and plant implicit families are not disjoint — shared: {string.Join(", ", overlap)}");
        }
    }

    static string? FamilyOf(SeedEntry e) =>
        e.Node["implicit"] is JsonObject implicitObj && implicitObj["family"] is JsonValue f && f.TryGetValue<string>(out var s)
            ? s : null;
}
