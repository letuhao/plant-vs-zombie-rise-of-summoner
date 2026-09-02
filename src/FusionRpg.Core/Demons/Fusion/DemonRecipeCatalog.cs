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
    /// <summary>
    /// Species eligible to be fusion OUTPUTS. Was `&gt;= DemonRarity.Rare` (three of the old four
    /// rungs). Rare's own migration target is `Cultivated` (the rare band's lowest rung —
    /// ssot-rarity.md §4.3's forward map), so this is the same translation: "recipes exist from the
    /// old Rare band's target rung upward" (spec-rarity-migration.md §3's ordinal-arithmetic fix).
    ///
    /// ⛔ MUST be a `const`, not `static readonly`, and MUST be declared before `All` below. A
    /// `static readonly` field here would run its initializer in DECLARATION ORDER — after `All`'s
    /// own initializer already called `Build()` — so `Build()` would read the enum's default value
    /// (`Chaff`, ordinal 0) instead of `Cultivated`, silently admitting every species as an output.
    /// Found exactly this way: `DemonRecipeCatalogTests` failed with two Chaff-rarity species
    /// (`allpeater`, `ashthreepeater`) appearing as recipe outputs.
    /// </summary>
    internal const DemonRarity OutputEligibilityFloor = DemonRarity.Cultivated;

    // Lazy, not `static readonly ... = Build()` (T4.7, catalog-runtime §3a) — same reasoning as
    // WaveCatalog's own conversion. `ById`/`ByPair` read the `All` PROPERTY (not the old field), so
    // their own lazy initializers trigger `Build()` on first touch regardless of which of the three
    // is accessed first — the exact field-declaration-order hazard `OutputEligibilityFloor`'s own doc
    // comment already warned about for a different pair of fields, now impossible by construction
    // since none of these are fields evaluated at class-load time any more.
    static IReadOnlyList<DemonRecipeDef>? _all;
    public static IReadOnlyList<DemonRecipeDef> All => _all ??= Build();

    static Dictionary<string, DemonRecipeDef>? _byId;
    static Dictionary<string, DemonRecipeDef> ById => _byId ??= All.ToDictionary(r => r.RecipeId, StringComparer.Ordinal);

    static Dictionary<string, DemonRecipeDef>? _byPair;
    static Dictionary<string, DemonRecipeDef> ByPair =>
        _byPair ??= All.ToDictionary(r => PairKey(r.InputSpeciesIdA, r.InputSpeciesIdB), StringComparer.Ordinal);

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
            .Where(s => DemonRarityLadder.AtLeast(s.BaseRarity, OutputEligibilityFloor)
                        && s.Acquisition != DemonAcquisition.CaptureOnly)
            .OrderBy(s => s.BaseRarity)
            .ThenBy(s => s.SpeciesId, StringComparer.Ordinal)
            .ToList();

        var recipes = new List<DemonRecipeDef>(outputs.Count);
        var usedPairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var output in outputs)
        {
            var pool = InputPoolBelow(output.BaseRarity);
            if (pool.Count < 2)
                throw new InvalidOperationException(
                    $"Recipe pool below {output.BaseRarity} has {pool.Count} species (searched down to "
                    + $"{DemonRarity.Chaff}) — catalog cannot support fusion.");

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

    /// <summary>
    /// Species one rung below <paramref name="outputRarity"/> — widening the search downward one
    /// rung at a time until at least two candidates exist, or the bottom (Chaff) has been included.
    /// A fixed "exactly one rung below" (the pre-migration shape) assumed every adjacent rung was
    /// populated; the ten-rung ladder does not guarantee that (spec-rarity-migration.md's own risk:
    /// widening an enum does not widen the roster that fills it). Walking down is the ladder-safe
    /// replacement — never a bare `(DemonRarity)((int)r - 1)` cast.
    /// </summary>
    static List<DemonSpeciesDef> InputPoolBelow(DemonRarity outputRarity)
    {
        var pool = new List<DemonSpeciesDef>();
        var cursor = outputRarity;
        while (true)
        {
            var atBottom = DemonRarityLadder.IsBottomRung(cursor);
            cursor = atBottom ? cursor : DemonRarityLadder.OneRungBelow(cursor);

            pool = DemonSpeciesCatalog.All
                .Where(s => s.BaseRarity == cursor && s.Acquisition != DemonAcquisition.CaptureOnly)
                .OrderBy(s => s.SpeciesId, StringComparer.Ordinal)
                .ToList();

            if (pool.Count >= 2 || atBottom)
                return pool;
        }
    }

    static int BRank(DemonSpeciesDef candidate, DemonSpeciesDef output)
    {
        if (output.ElementSecondary is { } secondary && candidate.ElementPrimary == secondary)
            return 0;
        return ElementRingMatrix.GetRelation(candidate.ElementPrimary, output.ElementPrimary)
               != ElementMatchupRelation.Neutral ? 1 : 2;
    }
}
