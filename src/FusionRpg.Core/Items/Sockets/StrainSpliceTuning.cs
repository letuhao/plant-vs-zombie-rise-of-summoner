using System.Text.Json;

namespace FusionRpg.Core.Items.Sockets;

/// <summary>
/// Pure parser over <c>data/tuning/strain-splice.v1.json</c> (item module 21) — no file I/O
/// (tunables-ssot.md §7.2: "Core never reads a file. Hosts load and inject"), matching
/// <see cref="SocketTuning"/>, <see cref="Mutation.EnhancementTuning"/> and
/// <see cref="Materials.MaterialTuning"/>.
///
/// <para>⚠ <b>Two files, one domain, and the split is deliberate.</b> D20's ingredient count, the
/// per-actor backstop, the attuned tier bonus, the structural ceiling and the fifteen per-role
/// ceilings all live in <c>sockets.v1.json</c> and belong to module 16. This file holds only what
/// module 16 does not own. <see cref="Parse"/> takes the <see cref="SocketTuning"/> alongside the
/// JSON and CROSS-VALIDATES against it — a min-tier plan whose length disagrees with the ingredient
/// count is refused at load, because a generator and a matcher disagreeing about how many
/// ingredients a Strain takes is a defect nothing downstream can see.</para>
///
/// <para><b>No key has a default.</b> A missing section throws at load rather than resolving to a
/// silently-invented tier.</para>
/// </summary>
public sealed class StrainSpliceTuning
{
    StrainSpliceTuning(
        IReadOnlyList<int> minTierPlan,
        IReadOnlyDictionary<string, int> baseTier,
        int catalogueSizeBar,
        int exactDuplicateNamesMax,
        int nearDuplicateRateMaxPermille)
    {
        MinTierPlan = minTierPlan;
        BaseTier = baseTier;
        CatalogueSizeBar = catalogueSizeBar;
        ExactDuplicateNamesMax = exactDuplicateNamesMax;
        NearDuplicateRateMaxPermille = nearDuplicateRateMaxPermille;
    }

    /// <summary>The insert tier each of D20's ingredients must meet, ascending.</summary>
    public IReadOnlyList<int> MinTierPlan { get; }

    /// <summary>Combination-shape id (<c>strain</c>/<c>splice</c>) → the tier granted BEFORE
    /// <see cref="SocketTuning.AttunedTierBonus"/>. Unbounded above: a granted tier is never
    /// clamped, and the structural socket ceiling caps a recipe's SHAPE, never its magnitude.</summary>
    public IReadOnlyDictionary<string, int> BaseTier { get; }

    /// <summary>ssot-sockets §4.4's ~45 learnable-catalogue bar. <b>Reported, never enforced</b> —
    /// a threshold that refused the 102nd combination would be a hard content ceiling.</summary>
    public int CatalogueSizeBar { get; }

    public int ExactDuplicateNamesMax { get; }
    public int NearDuplicateRateMaxPermille { get; }

    public int BaseTierFor(ComboShape shape)
    {
        var id = ComboShapes.Id(shape);
        return BaseTier.TryGetValue(id, out var tier)
            ? tier
            : throw new InvalidOperationException(
                $"strain-splice tuning has no recipe.baseTier row for '{id}'; the rows are " +
                $"[{string.Join(", ", BaseTier.Keys.OrderBy(k => k, StringComparer.Ordinal))}]");
    }

    /// <summary>
    /// The tier a combination grants. Base plus D22-as-amended's attuned bonus.
    /// <para>⚠ <b>Never a gate.</b> A mismatched fill still produces the combination — it just
    /// produces it at the base tier. §2f.2 reverted the hard requirement by name ("a fee wearing a
    /// gate's name"), so there is deliberately no arm here that returns "no combination".</para>
    /// </summary>
    public int GrantedTier(ComboShape shape, SocketTuning socketTuning, bool allAttuned)
    {
        if (socketTuning is null) throw new ArgumentNullException(nameof(socketTuning));
        return BaseTierFor(shape) + (allAttuned ? socketTuning.AttunedTierBonus : 0);
    }

    public static StrainSpliceTuning Parse(string json, SocketTuning socketTuning)
    {
        if (socketTuning is null) throw new ArgumentNullException(nameof(socketTuning));

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var recipe = Section(root, "recipe");
        var plan = Section(recipe, "minTierPlan").EnumerateArray().Select(e => e.GetInt32()).ToList();

        var baseTier = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var row in Section(recipe, "baseTier").EnumerateObject())
            baseTier[row.Name] = row.Value.GetInt32();

        var tuning = new StrainSpliceTuning(
            plan,
            baseTier,
            Section(Section(root, "learnability"), "catalogueSizeBar").GetInt32(),
            Section(Section(root, "distinctness"), "exactDuplicateNamesMax").GetInt32(),
            Section(Section(root, "distinctness"), "nearDuplicateRateMaxPermille").GetInt32());

        Validate(tuning, socketTuning);
        return tuning;
    }

    static JsonElement Section(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value)
            ? value
            : throw new InvalidOperationException(
                $"strain-splice tuning is missing '{name}' — refusing to substitute a default; an " +
                $"unreviewed number here reaches every generated combination");

    static void Validate(StrainSpliceTuning t, SocketTuning sockets)
    {
        var wanted = sockets.StrainSpliceIngredientCount;
        if (t.MinTierPlan.Count != wanted)
            throw new InvalidOperationException(
                $"recipe.minTierPlan has {t.MinTierPlan.Count} entries but sockets.v1.json fixes " +
                $"the ingredient count at {wanted} (D20) — the plan is zipped onto the ingredients, " +
                $"so a length mismatch silently drops or invents a min tier");

        for (var i = 1; i < t.MinTierPlan.Count; i++)
            if (t.MinTierPlan[i] < t.MinTierPlan[i - 1])
                throw new InvalidOperationException(
                    $"recipe.minTierPlan [{string.Join(", ", t.MinTierPlan)}] is not ascending — it " +
                    $"is zipped onto the ingredient multiset sorted by family id, so the order " +
                    $"decides which duplicate gets the cheaper tier and is load-bearing");

        foreach (var tier in t.MinTierPlan)
            if (tier < 1 || tier > sockets.InsertTierCount)
                throw new InvalidOperationException(
                    $"recipe.minTierPlan names tier {tier}, outside the shipped insert ladder " +
                    $"[1..{sockets.InsertTierCount}] — an ingredient no insert can satisfy makes " +
                    $"the combination unbuildable");

        if (t.BaseTier.Count == 0)
            throw new InvalidOperationException(
                "recipe.baseTier is empty — every combination kind needs a tier");

        foreach (var (kind, tier) in t.BaseTier)
        {
            if (tier < 1)
                throw new InvalidOperationException(
                    $"recipe.baseTier.{kind} is {tier}; a granted tier below 1 grants nothing");
            if (!ComboShapes.TryParse(kind, out var shape) || !ComboShapes.IsStrainOrSplice(shape))
                throw new InvalidOperationException(
                    $"recipe.baseTier names '{kind}', which is not a Strain or a Splice — a " +
                    $"generated resonance's tier is ResonanceGenerator's and is not tunable here");
        }

        if (t.CatalogueSizeBar < 1)
            throw new InvalidOperationException(
                "learnability.catalogueSizeBar below 1 is unreachable — the bar is a report " +
                "threshold, not a cap, and a bar nothing can clear reports on every run");

        if (t.ExactDuplicateNamesMax < 0 || t.NearDuplicateRateMaxPermille < 0)
            throw new InvalidOperationException("a distinctness threshold is never negative");
    }
}
