using System.Text.Json;

namespace FusionRpg.Core.Effects.Atoms.Generation;

/// <summary>
/// Pure parser for <c>data/seed/items/_tuning/tier-bands.v1.json</c> — the one genuinely tunable
/// surface a family's magnitude is computed from (that file's own <c>_meta.note</c>). Not
/// <c>bands.v1.json</c> (frozen, registry-side, never authored per-channel data) — the two are
/// different files with different owners; this parser reads only the tunable one.
/// </summary>
public static class TierBandsFile
{
    public static TierBandsInput Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("baseSharePermille", out var baseShareEl) || baseShareEl.ValueKind != JsonValueKind.Number)
            throw new FormatException("tier-bands: missing integer 'baseSharePermille'");

        if (!root.TryGetProperty("channelWeightPermille", out var channelWeightEl) || channelWeightEl.ValueKind != JsonValueKind.Object)
            throw new FormatException("tier-bands: missing object 'channelWeightPermille'");

        if (!root.TryGetProperty("opWeightPermille", out var opWeightEl) || opWeightEl.ValueKind != JsonValueKind.Object)
            throw new FormatException("tier-bands: missing object 'opWeightPermille'");

        return new TierBandsInput(baseShareEl.GetInt64(), ReadMap(channelWeightEl), ReadMap(opWeightEl));
    }

    static IReadOnlyDictionary<string, long> ReadMap(JsonElement obj)
    {
        var d = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var p in obj.EnumerateObject())
            d[p.Name] = p.Value.GetInt64();
        return d;
    }
}
