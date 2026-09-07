using System.Text.Json;

namespace FusionRpg.Core.Delve.Events;

/// <summary>
/// `event-deck` D3.9's own real bridging gap (party-dungeon-todo.md, 2026-09-07): spec-event-deck.md
/// §9's `OverrideTagUnsupplied` rule ("every `supplyOverride` tag is carried by ≥ 1 supply") needs the
/// UNION of every real `supplies-and-objects` extension anchor's own `overrideTags` array — no reader
/// exposed this anywhere (`supplies-and-objects`' own shipped code, `SupplyClassMap`/`SupplyInstantiation`/
/// `SupplyUse`, is runtime mechanics over an already-resolved supply, never a raw seed-content reader).
/// This is the smallest reader that closes the gap, living beside the ONE real consumer (`event-deck`,
/// not `supplies-and-objects`), matching <see cref="RoomEventPoolSeedFile"/>'s own identical
/// "reader lives with its consumer, not its source" placement.
/// </summary>
public static class SupplyOverrideTagSeedFile
{
    /// <summary>The flat union of every real supply-extension anchor's own `overrideTags` — existence
    /// only ("is this tag carried by ANYTHING"), never which specific supply, matching the rule's own
    /// "carried by &gt;= 1 supply" wording exactly.</summary>
    public static IReadOnlySet<string> LoadAllOverrideTags(string suppliesDir)
    {
        if (suppliesDir is null) throw new ArgumentNullException(nameof(suppliesDir));
        if (!Directory.Exists(suppliesDir)) return new HashSet<string>(StringComparer.Ordinal);

        var tags = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(suppliesDir, "*.json").OrderBy(p => p, StringComparer.Ordinal))
        {
            if (string.Equals(Path.GetFileName(path), "_index.json", StringComparison.OrdinalIgnoreCase)) continue;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            foreach (var tag in root.GetProperty("overrideTags").EnumerateArray())
                tags.Add(tag.GetString()!);
        }
        return tags;
    }
}
