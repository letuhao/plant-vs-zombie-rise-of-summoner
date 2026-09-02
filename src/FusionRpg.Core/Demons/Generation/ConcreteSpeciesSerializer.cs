using System.Text.Json;

namespace FusionRpg.Core.Demons.Generation;

/// <summary>
/// The generated tree's canonical form (T4.5, spec-species-generator.md §7: "committed, canonically
/// serialised, and regenerating over unchanged seeds produces byte-identical files"). Sorted keys
/// throughout — `SortedDictionary` for both the row's own fields and its magnitudes map — so the
/// same species always serialises to the same bytes regardless of dictionary insertion order.
/// </summary>
public static class ConcreteSpeciesSerializer
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string Canonical(ConcreteSpecies species)
    {
        var obj = new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["speciesId"] = species.SpeciesId,
            ["rarity"] = species.Rarity.ToString(),
            ["theta"] = species.Theta,
            ["pTheta"] = species.PTheta,
            ["attackIntervalMs"] = species.AttackIntervalMs,
            ["attackIntervalSource"] = species.AttackIntervalSource,
            ["rangeCells"] = species.RangeCells,
            ["variantCount"] = species.VariantCount,
            // catalog-runtime pass-through (T4.8's own real precondition, resolved 2026-09-02) —
            // copied from the anchor, not derived; committed here so the generated tree stays the
            // full, diffable, reviewable picture of what species-import actually writes.
            ["side"] = species.Side,
            ["gameTypeId"] = species.GameTypeId,
            ["elementPrimary"] = species.ElementPrimary.ToString(),
            ["elementSecondary"] = species.ElementSecondary?.ToString(),
            ["deployMode"] = species.DeployMode.ToString(),
            ["acquisition"] = species.Acquisition.ToString(),
            ["variants"] = species.Variants.OrderBy(v => v, StringComparer.Ordinal).ToArray(),
            ["traitPool"] = species.TraitPool.OrderBy(t => t, StringComparer.Ordinal).ToArray(),
            ["magnitudes"] = new SortedDictionary<string, long>(
                species.Magnitudes.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal),
        };
        return JsonSerializer.Serialize(obj, Options) + "\n";
    }
}
