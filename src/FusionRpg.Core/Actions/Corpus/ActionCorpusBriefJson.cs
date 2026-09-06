using System.Text.Json;

namespace FusionRpg.Core.Actions.Corpus;

public sealed class ActionCorpusBriefRejection : Exception
{
    public ActionCorpusBriefRejection(string message) : base(message) { }
}

/// <summary>
/// Pure parser over one `data/seed/actions/committed-round-*.json` file's text (tunables-ssot.md
/// §7.2: no file I/O here — `FusionRpg.Server`'s startup step reads the file, this parses the
/// string). Reads only the fields the corpus brief actually needs — the original six plus
/// `descriptionKey` (item-content `granted-action-text`, T14)
/// (`AuthoredEligibilityResolvesTests.cs`'s own established field-reading precedent); every OTHER
/// authored field (`areaShape`, `kindHint`, `motifsUsed`, `pairedPayoffFamily`, `pairingRole`) is
/// content-authoring metadata this module does not consume, so an unknown-key check would reject
/// real shipped content — deliberately not applied here (unlike `ActionTargetSpecJson`'s own closed
/// key set, which parses caller-authored predicate JSON, not a corpus brief).
/// </summary>
public static class ActionCorpusBriefJson
{
    public static IReadOnlyList<ActionCorpusBrief> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ActionCorpusBriefRejection("action corpus brief file: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ActionCorpusBriefRejection($"action corpus brief file: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                throw new ActionCorpusBriefRejection("action corpus brief file: missing or non-array 'entries'");

            var briefs = new List<ActionCorpusBrief>();
            foreach (var e in entries.EnumerateArray())
            {
                var id = RequireString(e, "id");
                var name = RequireString(e, "name");
                var category = RequireString(e, "category");
                var scope = RequireString(e, "scope");
                var scopeKey = e.TryGetProperty("scopeKey", out var sk) && sk.ValueKind == JsonValueKind.String ? sk.GetString() : null;
                var targetMode = RequireString(e, "targetMode");
                var relation = RequireString(e, "relation");
                // item-content `granted-action-text` (T14). Required, not optional: card block 9
                // (`ssot-presentation.md` §9.14) commits to showing a description, and a corpus row
                // that ships without one puts a blank line on a player's card. A key, never the
                // sentence — §3.6 L3.
                var descriptionKey = RequireString(e, "descriptionKey");

                if (!e.TryGetProperty("rungBand", out var band) || band.ValueKind != JsonValueKind.Array || band.GetArrayLength() != 2)
                    throw new ActionCorpusBriefRejection($"brief '{id}': rungBand must be a 2-element array");
                var rungFloor = band[0].GetInt32();
                var rungCeiling = band[1].GetInt32();

                if (!e.TryGetProperty("atomFamilies", out var famEl) || famEl.ValueKind != JsonValueKind.Array)
                    throw new ActionCorpusBriefRejection($"brief '{id}': missing or non-array atomFamilies");
                var families = famEl.EnumerateArray().Select(f => f.GetString() ?? "").ToArray();

                briefs.Add(new ActionCorpusBrief(
                    id, name, category, scope, scopeKey, rungFloor, rungCeiling, families,
                    targetMode, relation, descriptionKey));
            }
            return briefs;
        }
    }

    static string RequireString(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(el.GetString()))
            throw new ActionCorpusBriefRejection($"action corpus brief entry: missing or empty '{key}'");
        return el.GetString()!;
    }
}
