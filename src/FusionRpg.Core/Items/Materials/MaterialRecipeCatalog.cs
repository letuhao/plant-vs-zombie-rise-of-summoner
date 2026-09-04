using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;

namespace FusionRpg.Core.Items.Materials;

/// <summary>
/// ⛔ <b>D26 is the design constraint, not a footnote: every cost input is a property of the TARGET,
/// none is a property of the player.</b> A coefficient keyed on player power reads exactly like one
/// keyed on target power until someone opens the file — so the refusal is structural rather than
/// reviewed: <b>this type simply has nowhere to put a player stat</b>, and
/// <c>MaterialRecipeCatalog.Resolve</c> takes no second argument that could carry one. A t5 affix
/// costs more than a t1 at every Θ, and Θ is not an input at all.
/// </summary>
/// <param name="TargetRungIndex">
/// ⛔ 0..9, the rung INDEX on <see cref="RarityLadder.RungIds"/> — <b>not</b> `rarity.ordinal`, which
/// is 10…100 in `_registry/core.v1.json`. Reading the ordinal in here would make every cost row and
/// every salvage coefficient wrong by 10×; `spec-rarity-bands.md:307` makes writing an enum member
/// index into `rarity.ordinal` a Never for the mirror-image reason. The spec's own Code-style block
/// still spells this field `TargetRarityOrdinal, // 0..9, the rarity table's own ordinal`, which is
/// the exact confusion its own Platform-correction section warns against; recorded in
/// tasks/item-todo.md P4.1.
/// </param>
public readonly record struct RecipeContext(
    int TargetRungIndex,
    int TargetTier,
    int TargetItemLevel,
    string TargetFrame,
    int EnhanceLevel);

/// <summary>One resolved cost line: a concrete integer quantity of one material, or of souls.</summary>
public readonly record struct MaterialCostLine(MaterialClass Class, string MaterialId, long Qty)
{
    /// <summary>Souls have no material id — they are a ledger balance.</summary>
    public static MaterialCostLine Souls(long qty) => new(MaterialClass.Souls, "", qty);
}

/// <summary>One authored cost line, before the reference table prices it.</summary>
public readonly record struct AuthoredCostLine(string MaterialId, string CostBand);

/// <summary>One recipe as authored. `outputRef` is absent on the twenty `mutation` recipes, whose
/// output is the owning module's mutation rather than a new row.</summary>
public sealed record MaterialRecipe(
    string RecipeId,
    CraftOperation Operation,
    string OutputKind,
    string? OutputRef,
    int OutputQty,
    string Frame,
    string? SoulsCostBand,
    IReadOnlyList<AuthoredCostLine> CostLines);

/// <summary>A recipe the corpus authors that this build cannot resolve, named rather than dropped.</summary>
public sealed record RecipeRefusal(string RecipeId, string Rule, string Detail)
{
    public AtomRejection AsRejection() => AtomRejection.ContentRule(Rule, Detail);
}

public sealed class MaterialRecipeRejection : Exception
{
    public MaterialRecipeRejection(string message) : base(message) { }
}

/// <summary>
/// Load, validate and resolve the recipe corpus (`data/seed/items/recipes/*.json`).
///
/// <para><b>The SC7 line:</b> adding a base type's forge recipe is one recipe row plus two or three
/// cost rows and <b>no code</b>. Adding an <i>operation verb</i> is code, because a verb needs an
/// executor and a module that owns it.</para>
///
/// <para><b>Authors write bands, never magnitudes</b> (`seed-contract.md` §1/§3). This class is the
/// resolution step: the authored <c>costBand</c> scales the reference-table base quantity for the
/// recipe's operation, by `bands.v1.json`'s own formula, with the ceiling that makes it impossible
/// for a band to resolve a cost to zero.</para>
///
/// <para>⛔ Refusals follow module 11's shipped pattern exactly: a corpus entry this build cannot
/// resolve is refused <b>by name</b>, with the module that unblocks it, and never silently dropped.
/// No new <see cref="AtomRejectionReason"/> member is minted — every rule is a namespaced
/// <c>ContentRuleViolated{material.*}</c>.</para>
/// </summary>
public sealed class MaterialRecipeCatalog
{
    public const string Namespace = "material";

    /// <summary>An operation verb the corpus authors that the ten-verb vocabulary does not have.</summary>
    public const string OperationUnavailableRule = "material.operation-unavailable";

    /// <summary>A cost line naming one of the four retired band shard ids. They still RESOLVE
    /// (`spec-rarity-migration.md` §4 point 4) but are never minted, so a recipe demanding one is a
    /// recipe nothing can ever pay.</summary>
    public const string MaterialUnissuableRule = "material.material-unissuable";

    public const string UnknownMaterialRule = "material.unknown-id";
    public const string UnknownBandRule = "material.unknown-cost-band";
    public const string DuplicateRecipeRule = "material.duplicate-recipe-id";

    /// <summary>
    /// ⭐ R2, the strict-loss invariant, ENFORCED at import rather than only asserted in a test.
    ///
    /// <para>Found while building this module: the SC7 line — "adding a base type's forge recipe is
    /// one row plus two or three cost rows and <b>no code</b>" — means an author can create a
    /// substrate perpetual-motion machine with a single word. A `forge` whose substrate leg resolves
    /// to no more than the chaff salvage floor turns forge-then-salvage into a net gain, and R2 as a
    /// test over the SHIPPED table would never see it: it only fires the day someone authors the
    /// cheap band. So the check runs at load, on every mint, against the same salvage coefficients
    /// <see cref="SalvagePolicy"/> reads.</para>
    /// </summary>
    public const string StrictLossRule = "material.strict-loss-violated";

    static bool _namespaceRegistered;

    static void EnsureNamespace()
    {
        if (_namespaceRegistered) return;
        ContentRuleNamespaces.Register(Namespace);
        _namespaceRegistered = true;
    }

    MaterialRecipeCatalog(
        IReadOnlyDictionary<string, MaterialRecipe> recipes,
        IReadOnlyList<RecipeRefusal> refusals,
        MaterialTuning tuning)
    {
        Recipes = recipes;
        Refusals = refusals;
        Tuning = tuning;
    }

    public IReadOnlyDictionary<string, MaterialRecipe> Recipes { get; }

    /// <summary>Every entry this build refused, with the rule and the module that unblocks it.
    /// Never empty against today's shipped corpus — see tasks/item-todo.md P4.1.</summary>
    public IReadOnlyList<RecipeRefusal> Refusals { get; }

    public MaterialTuning Tuning { get; }

    /// <summary>
    /// Parse one corpus file against the tuning. Deterministic: the same input always produces the
    /// same recipe order and the same refusal order, which is what makes
    /// <see cref="ContentHash"/> a usable golden.
    /// </summary>
    public static MaterialRecipeCatalog Load(IEnumerable<string> corpusJson, MaterialTuning tuning)
    {
        EnsureNamespace();

        var recipes = new Dictionary<string, MaterialRecipe>(StringComparer.Ordinal);
        var refusals = new List<RecipeRefusal>();

        foreach (var json in corpusJson)
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
                throw new MaterialRecipeRejection("recipe corpus: missing or non-array 'entries'");

            foreach (var e in entries.EnumerateArray())
            {
                var id = Str(e, "id");

                if (recipes.ContainsKey(id) || refusals.Any(r => r.RecipeId == id))
                {
                    refusals.Add(new RecipeRefusal(id, DuplicateRecipeRule, $"recipe id '{id}' is authored twice"));
                    continue;
                }

                var operationId = Str(e, "operation");
                if (!CraftOperations.TryParse(operationId, out var op))
                {
                    refusals.Add(new RecipeRefusal(id, OperationUnavailableRule,
                        $"operation '{operationId}' is not one of the {CraftOperations.All.Count} priced verbs " +
                        $"({string.Join(", ", CraftOperations.AllIds)}). " +
                        (operationId == "reroll"
                            ? "`reroll` predates the reroll-one/reroll-all split; module 15 (enhance-reroll) owns that split and the `op_kind` namespace it lives in."
                            : "Adding a verb is code, not content — it needs an executor and an owning module.")));
                    continue;
                }

                var lines = new List<AuthoredCostLine>();
                string? refusalRule = null, refusalDetail = null;

                foreach (var l in e.GetProperty("costLines").EnumerateArray())
                {
                    var materialId = Str(l, "material");
                    var band = Str(l, "costBand");

                    if (MaterialCatalog.IsLegacyShardId(materialId))
                    {
                        refusalRule = MaterialUnissuableRule;
                        refusalDetail =
                            $"cost line names '{materialId}', one of the four retired band shard ids. They resolve " +
                            "(spec-rarity-migration.md §4 point 4) but are never minted, so this recipe can never be paid. " +
                            "The ten-rung id is shard.{chaff…almanac}; re-authoring the corpus is module 14's follow-up.";
                        break;
                    }

                    if (!MaterialCatalog.IsIssuable(materialId))
                    {
                        refusalRule = UnknownMaterialRule;
                        refusalDetail = $"cost line names '{materialId}', which is not in the 27-id closed vocabulary";
                        break;
                    }

                    if (!tuning.BandMultipliersPerMille.ContainsKey(band))
                    {
                        refusalRule = UnknownBandRule;
                        refusalDetail = $"cost line on '{materialId}' names cost band '{band}', which bands.v1.json does not define";
                        break;
                    }

                    var forbidden = CostClassMatrix.Check(op, materialId);
                    if (forbidden is { } f)
                    {
                        refusalRule = f.Rule;
                        refusalDetail = f.Detail;
                        break;
                    }

                    lines.Add(new AuthoredCostLine(materialId, band));
                }

                if (refusalRule != null)
                {
                    refusals.Add(new RecipeRefusal(id, refusalRule, refusalDetail!));
                    continue;
                }

                var soulsBand = e.TryGetProperty("soulsCostBand", out var sb) && sb.ValueKind == JsonValueKind.String
                    ? sb.GetString()
                    : null;

                if (soulsBand != null && !tuning.BandMultipliersPerMille.ContainsKey(soulsBand))
                {
                    refusals.Add(new RecipeRefusal(id, UnknownBandRule,
                        $"soulsCostBand '{soulsBand}' is not in bands.v1.json's costBand vocabulary"));
                    continue;
                }

                var recipe = new MaterialRecipe(
                    id, op,
                    Str(e, "outputKind"),
                    e.TryGetProperty("outputRef", out var orf) && orf.ValueKind == JsonValueKind.String ? orf.GetString() : null,
                    e.TryGetProperty("outputQty", out var oq) && oq.ValueKind == JsonValueKind.Number ? oq.GetInt32() : 1,
                    Str(e, "frame"),
                    soulsBand,
                    lines);

                var leak = StrictLossLeak(recipe, tuning);
                if (leak != null)
                {
                    refusals.Add(new RecipeRefusal(id, StrictLossRule, leak));
                    continue;
                }

                recipes[id] = recipe;
            }
        }

        return new MaterialRecipeCatalog(recipes, refusals, tuning);
    }

    /// <summary>
    /// R2's import-time half, for MINTS only. A mint's output is a brand-new base: I9 §7.5 example 1
    /// states its rarity as <b>Normal</b> — the bottom rung — and it carries no enhancement, so its
    /// salvage yield is exactly the chaff floor. If the recipe's own substrate leg does not exceed
    /// that floor, forge-then-salvage is a net gain and the loop stops being lossy.
    ///
    /// <para>Mutations are deliberately NOT checked here: their output is an item the recipe did not
    /// pay for in full, so the invariant that holds for them is the per-id one the property test
    /// asserts, not a comparison against a floor.</para>
    /// </summary>
    static string? StrictLossLeak(MaterialRecipe recipe, MaterialTuning tuning)
    {
        if (recipe.Operation is not (CraftOperation.Forge or CraftOperation.ForgeGem))
            return null;

        var cost = tuning.Operations[recipe.Operation];
        if (cost.Substrate is not { } leg) return null;

        var floor = tuning.Salvage[RarityLadder.RungIds[0]].SubstrateBase;

        foreach (var authored in recipe.CostLines)
        {
            var grade = MaterialCatalog.GradeOf(authored.MaterialId);
            if (grade == 0) continue;

            var qty = leg.BandImmune
                ? leg.BaseQty(grade, 0, 0)
                : MaterialTuning.ApplyBand(leg.BaseQty(grade, 0, 0), tuning.BandMultiplier(authored.CostBand));

            if (qty <= floor)
                return $"R2: minting at cost band '{authored.CostBand}' resolves '{authored.MaterialId}' to {qty}, " +
                       $"but salvaging the output returns {floor} substrate — forge-then-salvage would be a net gain. " +
                       "Author a band that costs more than the chaff salvage floor.";
        }

        return null;
    }

    /// <summary>
    /// The ordered <see cref="MaterialCostLine"/>s a spend must pay, in the FIXED class order
    /// souls → shard → substrate → essence → catalyst. Fixed order matters: a partial failure always
    /// fails at the same point, so two logs of one refusal are byte-comparable.
    ///
    /// <para>Every quantity is <c>long</c>, widened before multiplying, and divided by 1000 exactly
    /// once — inside <see cref="MaterialTuning.ApplyBand"/>, at the end.</para>
    /// </summary>
    public IReadOnlyList<MaterialCostLine> Resolve(string recipeId, RecipeContext ctx)
    {
        if (!Recipes.TryGetValue(recipeId, out var recipe))
            throw new MaterialRecipeRejection($"recipe '{recipeId}' is not in the catalog (it may have been refused at load — see Refusals)");

        if (ctx.TargetRungIndex < 0 || ctx.TargetRungIndex >= RarityLadder.RungIds.Count)
            throw new MaterialRecipeRejection(
                $"TargetRungIndex {ctx.TargetRungIndex} is outside 0..{RarityLadder.RungIds.Count - 1} — " +
                "this field is the rung INDEX, never rarity.ordinal (which is 10…100)");

        var cost = Tuning.Operations[recipe.Operation];
        var lines = new List<MaterialCostLine>();

        // The grade a recipe prices on is a property of the recipe's own substrate line — the thing
        // being made or worked on — never of the player. A recipe with no substrate line prices at
        // the grade the target's item level implies.
        var grade = recipe.CostLines
            .Select(l => MaterialCatalog.GradeOf(l.MaterialId))
            .Where(g => g > 0)
            .DefaultIfEmpty(Tuning.GradeForItemLevel(ctx.TargetItemLevel))
            .Max();

        if (recipe.Operation == CraftOperation.Upcycle && grade > Tuning.UpcycleMaxInputGrade)
            throw new MaterialRecipeRejection(
                $"recipe '{recipeId}' upcycles grade {grade}, above upcycle.maxInputGrade {Tuning.UpcycleMaxInputGrade} — " +
                "a BOUNDED RATIO between two material grades (I9 §5.3), not a progression ceiling");

        if (recipe.SoulsCostBand is { } soulsBand && cost.Souls is { } soulsLeg)
        {
            var baseQty = soulsLeg.BaseQty(grade, ctx.TargetRungIndex, ctx.EnhanceLevel);
            lines.Add(MaterialCostLine.Souls(
                soulsLeg.BandImmune ? baseQty : MaterialTuning.ApplyBand(baseQty, Tuning.BandMultiplier(soulsBand))));
        }

        foreach (var authored in recipe.CostLines.OrderBy(l => MaterialCatalog.ClassRank(MaterialCatalog.ClassOf(l.MaterialId)))
                                                 .ThenBy(l => l.MaterialId, StringComparer.Ordinal))
        {
            var cls = MaterialCatalog.ClassOf(authored.MaterialId);
            var leg = cls switch
            {
                MaterialClass.Shard => cost.Shard,
                MaterialClass.Substrate => cost.Substrate,
                MaterialClass.Essence => cost.Essence,
                MaterialClass.Catalyst => cost.Catalyst,
                _ => null,
            };

            if (leg is not { } l2)
                throw new MaterialRecipeRejection(
                    $"recipe '{recipeId}' authors a {cls} line but operation '{CraftOperations.Id(recipe.Operation)}' " +
                    "has no priced leg for it in materials.v1.json");

            var baseQty = l2.BaseQty(grade, ctx.TargetRungIndex, ctx.EnhanceLevel);
            var qty = l2.BandImmune ? baseQty : MaterialTuning.ApplyBand(baseQty, Tuning.BandMultiplier(authored.CostBand));
            lines.Add(new MaterialCostLine(cls, authored.MaterialId, qty));
        }

        // Souls are already first by construction; the material legs are sorted by class rank above.
        // Asserted rather than assumed, because "fixed class order" is what makes two refusal logs
        // byte-comparable and a silent reordering would be invisible.
        for (var i = 1; i < lines.Count; i++)
        {
            if (MaterialCatalog.ClassRank(lines[i].Class) < MaterialCatalog.ClassRank(lines[i - 1].Class))
                throw new MaterialRecipeRejection($"recipe '{recipeId}' resolved out of spend order — this is a bug in Resolve, not in the data");
        }

        return lines;
    }

    /// <summary>
    /// A stable content hash over every loaded recipe, for the "two builds are byte-identical" golden
    /// (the fusion-catalog precedent). Ordinal-sorted, so dictionary iteration order cannot leak in.
    /// </summary>
    public string ContentHash()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var r in Recipes.Values.OrderBy(r => r.RecipeId, StringComparer.Ordinal))
        {
            sb.Append(r.RecipeId).Append('|').Append(CraftOperations.Id(r.Operation)).Append('|')
              .Append(r.OutputKind).Append('|').Append(r.OutputRef ?? "-").Append('|').Append(r.OutputQty).Append('|')
              .Append(r.Frame).Append('|').Append(r.SoulsCostBand ?? "-").Append('|');
            foreach (var l in r.CostLines.OrderBy(l => l.MaterialId, StringComparer.Ordinal))
                sb.Append(l.MaterialId).Append('=').Append(l.CostBand).Append(',');
            sb.Append(';');
        }

        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static string Str(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.String)
            throw new MaterialRecipeRejection($"recipe corpus: entry missing or non-string '{key}'");
        return el.GetString()!;
    }
}
