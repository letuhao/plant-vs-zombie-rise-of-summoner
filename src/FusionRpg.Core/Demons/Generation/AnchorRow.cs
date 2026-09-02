using System.Text.Json;

namespace FusionRpg.Core.Demons.Generation;

/// <summary>
/// One classified anchor — the enum-only output of the LLM classification pipeline
/// (`tools/seedsmith/seedsmith/adapters/demons/anchor/`), read back into C# for `species-generator`
/// (module 12). Carries every field `catalog-runtime` (module 14) needs to assemble a real
/// `DemonSpeciesDef` alongside the fields `species-generator`'s own numeric derivation reads — the
/// anchor already has all of it (verified against the real anchor schema and the two real classified
/// anchors on disk, `pea.json`/`sunflower.json`, 2026-09-02); `_provenance` and `resourceProfile` are
/// the only real anchor fields still deliberately unparsed here, because nothing downstream reads
/// them yet.
/// </summary>
/// <param name="ThreatBand">Null when the real anchor has not been classified for this field yet —
/// verified against the two real classified anchors on disk today (`pea.json`, `sunflower.json`),
/// both of which genuinely omit it. `demon-threat.v1.json`'s own `inferredDefaultRung` is the
/// sanctioned fallback for exactly this case, not an invented default.</param>
/// <param name="AptitudeSecondary">Null when the anchor's own literal <c>"none"</c> sentinel is
/// present — the real anchor schema uses that string, not a JSON null, to mean "no secondary."</param>
/// <param name="ElementSecondary">Same <c>"none"</c>-sentinel convention as
/// <paramref name="AptitudeSecondary"/> — the real anchor schema uses one literal string convention
/// for every optional-secondary field, not a JSON null.</param>
/// <param name="Acquisition">The anchor's own raw flag-array strings (e.g. <c>["Summonable"]</c>) —
/// kept as strings here, parsed into the real <c>[Flags] DemonAcquisition</c> enum by the caller
/// (mirrors how <see cref="Rarity"/> stays a raw string here and <c>SpeciesExpander</c> parses it),
/// so this reader stays a pure JSON-shape mirror with no enum-parsing failure mode of its own.</param>
public sealed record AnchorRow(
    string SpeciesId, string Rarity, string? ThreatBand,
    string AptitudePrimary, string? AptitudeSecondary, bool Pure,
    string AttackTempo, string Reach, IReadOnlyList<string> Variants,
    string Side, int GameTypeId, string ElementPrimary, string? ElementSecondary,
    string DeployMode, IReadOnlyList<string> Acquisition, IReadOnlyList<string> Traits);

public sealed class AnchorRowRejection : Exception
{
    public AnchorRowRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O — reads the real classified-anchor JSON shape (a top-level
/// array of anchor objects, `data/seed/demons/species/**.json`).</summary>
public static class AnchorRowReader
{
    public static IReadOnlyList<AnchorRow> ReadAll(string json)
    {
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new AnchorRowRejection($"anchor file: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new AnchorRowRejection("anchor file: expected a top-level array");

            var rows = new List<AnchorRow>();
            foreach (var el in doc.RootElement.EnumerateArray()) rows.Add(ReadOne(el));
            return rows;
        }
    }

    static AnchorRow ReadOne(JsonElement el)
    {
        var speciesId = Str(el, "speciesId");
        var aptSecondary = Str(el, "aptitudeSecondary");
        var elSecondary = Str(el, "elementSecondary");
        return new AnchorRow(
            SpeciesId: speciesId,
            Rarity: Str(el, "rarity"),
            ThreatBand: el.TryGetProperty("threatBand", out var tb) && tb.ValueKind == JsonValueKind.String
                ? tb.GetString() : null,
            AptitudePrimary: Str(el, "aptitudePrimary"),
            AptitudeSecondary: string.Equals(aptSecondary, "none", StringComparison.OrdinalIgnoreCase) ? null : aptSecondary,
            Pure: el.TryGetProperty("pure", out var p) && p.ValueKind == JsonValueKind.True,
            AttackTempo: Str(el, "attackTempo"),
            Reach: Str(el, "reach"),
            Variants: StrArray(el, "variants"),
            Side: Str(el, "side"),
            GameTypeId: el.TryGetProperty("gameTypeId", out var g) && g.TryGetInt32(out var gi)
                ? gi : throw new AnchorRowRejection("anchor: missing or non-integer 'gameTypeId'"),
            ElementPrimary: Str(el, "elementPrimary"),
            ElementSecondary: string.Equals(elSecondary, "none", StringComparison.OrdinalIgnoreCase) ? null : elSecondary,
            DeployMode: Str(el, "deployMode"),
            Acquisition: StrArray(el, "acquisition"),
            Traits: StrArray(el, "traits"));
    }

    static string Str(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!
            : throw new AnchorRowRejection($"anchor: missing or non-string '{key}'");

    static IReadOnlyList<string> StrArray(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToArray()
            : Array.Empty<string>();
}
