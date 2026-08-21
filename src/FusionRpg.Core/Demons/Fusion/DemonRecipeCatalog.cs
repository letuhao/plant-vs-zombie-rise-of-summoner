using FusionRpg.Core.Combat.Element;

namespace FusionRpg.Core.Demons.Fusion;

/// <summary>One cross-species fusion recipe: two band-below inputs mint the output species.</summary>
public sealed record DemonRecipeDef(
    string RecipeId, string OutputSpeciesId, string InputSpeciesIdA, string InputSpeciesIdB);

/// <summary>
/// Code-built deterministically from the species catalog (WaveCatalog pattern — same catalog ⇒
/// same recipes; no capture data): every summonable rare+ species gets exactly ONE recipe.
/// Input A shares the output's primary element where the band allows; input B prefers the
/// secondary-element donor, then a ring-related element, then catalog order — and pairs are
/// forced unique so an input pair identifies its recipe. Capture-only species appear nowhere
/// (spec-demon-fusion.md, owner lock 6). Validated eagerly at startup like the species catalog.
/// </summary>
public static class DemonRecipeCatalog
{
    public static readonly IReadOnlyList<DemonRecipeDef> All = Build();

    static readonly Dictionary<string, DemonRecipeDef> ById =
        All.ToDictionary(r => r.RecipeId, StringComparer.Ordinal);

    static readonly Dictionary<string, DemonRecipeDef> ByPair =
        All.ToDictionary(r => PairKey(r.InputSpeciesIdA, r.InputSpeciesIdB), StringComparer.Ordinal);

    public static bool IsKnown(string? recipeId) => recipeId != null && ById.ContainsKey(recipeId);

    public static DemonRecipeDef Get(string recipeId) =>
        ById.TryGetValue(recipeId, out var def)
            ? def
            : throw new ArgumentException($"Unknown fusion recipe id '{recipeId}'.");

    /// <summary>Orderless lookup by the two input species — null when no recipe matches.</summary>
    public static DemonRecipeDef? TryMatch(string speciesA, string speciesB)
    {
        if (string.Equals(speciesA, speciesB, StringComparison.Ordinal)) return null;
        return ByPair.TryGetValue(PairKey(speciesA, speciesB), out var def) ? def : null;
    }

    /// <summary>Test seam: rebuild from scratch to prove determinism.</summary>
    public static IReadOnlyList<DemonRecipeDef> BuildForTest() => Build();

    static string PairKey(string a, string b) =>
        string.CompareOrdinal(a, b) <= 0 ? a + "+" + b : b + "+" + a;

    static IReadOnlyList<DemonRecipeDef> Build()
    {
        var outputs = DemonSpeciesCatalog.All
            .Where(s => s.BaseRarity >= DemonRarity.Rare && s.Acquisition != DemonAcquisition.CaptureOnly)
            .OrderBy(s => s.BaseRarity)
            .ThenBy(s => s.SpeciesId, StringComparer.Ordinal)
            .ToList();

        var recipes = new List<DemonRecipeDef>(outputs.Count);
        var usedPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            var pool = DemonSpeciesCatalog.All
                .Where(s => s.BaseRarity == (DemonRarity)((int)output.BaseRarity - 1)
                            && s.Acquisition != DemonAcquisition.CaptureOnly)
                .OrderBy(s => s.SpeciesId, StringComparer.Ordinal)
                .ToList();
            if (pool.Count < 2)
                throw new InvalidOperationException(
                    $"Recipe pool below {output.BaseRarity} has {pool.Count} species — catalog cannot support fusion.");

            var a = pool.FirstOrDefault(p => p.ElementPrimary == output.ElementPrimary) ?? pool[0];

            // B preference: secondary-element donor → ring-related primary → catalog order;
            // then advance until the orderless pair is unused (pairs identify recipes).
            var candidates = pool
                .Where(p => !ReferenceEquals(p, a))
                .OrderBy(p => BRank(p, output))
                .ThenBy(p => p.SpeciesId, StringComparer.Ordinal)
                .ToList();
            var b = candidates.FirstOrDefault(p => usedPairs.Add(PairKey(a.SpeciesId, p.SpeciesId)))
                ?? throw new InvalidOperationException(
                    $"No unused input pair available for '{output.SpeciesId}'.");

            recipes.Add(new DemonRecipeDef("recipe." + output.SpeciesId, output.SpeciesId, a.SpeciesId, b.SpeciesId));
        }

        return recipes;
    }

    static int BRank(DemonSpeciesDef candidate, DemonSpeciesDef output)
    {
        if (output.ElementSecondary is { } secondary && candidate.ElementPrimary == secondary)
            return 0;
        return ElementRingMatrix.GetRelation(candidate.ElementPrimary, output.ElementPrimary)
               != ElementMatchupRelation.Neutral ? 1 : 2;
    }
}
