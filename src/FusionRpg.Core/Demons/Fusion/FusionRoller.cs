using FusionRpg.Core.Battle;

namespace FusionRpg.Core.Demons.Fusion;

public sealed record FusionRollResult(string Variant, IReadOnlyList<string> TraitIds);

/// <summary>
/// Pure fusion rolls (spec-demon-fusion.md, locks 4+5): the output species is the recipe's —
/// only traits and variant roll, from dedicated `fusion:*` streams so replays reproduce the
/// outcome bit-for-bit. Pick-one is player agency; the rest comes from the combined INPUT pool
/// (an exhausted pool yields fewer traits, never padding from elsewhere).
/// </summary>
public static class FusionRoller
{
    // The summon altar's constant, referenced — the two shiny odds can never drift apart.
    static int ShinyDie => SummonRoller.ShinyOneIn;

    public static FusionRollResult Roll(
        DemonSpeciesDef resultSpecies, DemonRarity resultRarity,
        string pickedTraitId, IReadOnlyList<string> combinedInputTraits, ulong seed)
    {
        if (!DemonTraitCatalog.IsKnown(pickedTraitId))
            throw new ArgumentException($"Unknown trait id '{pickedTraitId}'.");
        if (!combinedInputTraits.Contains(pickedTraitId, StringComparer.Ordinal))
            throw new ArgumentException($"Picked trait '{pickedTraitId}' is not on any input demon.");

        var traitRng = SeededRng.DeriveStream(seed, "fusion:traits");
        var want = SlotsFor(resultRarity);
        var traits = new List<string>(want) { pickedTraitId };
        var pool = combinedInputTraits
            .Distinct(StringComparer.Ordinal)
            .Where(t => t != pickedTraitId)
            .OrderBy(t => t, StringComparer.Ordinal) // stable pool order — rolls are seed-only
            .ToList();
        while (traits.Count < want && pool.Count > 0)
        {
            var i = traitRng.NextInt(pool.Count);
            traits.Add(pool[i]);
            pool.RemoveAt(i);
        }

        var variantRng = SeededRng.DeriveStream(seed, "fusion:variant");
        // Gate on the species' own variant list (SummonRoller does the same) — the generator
        // happens to give everyone `shiny`, but that is a generator habit, not a catalog contract.
        var variant = variantRng.NextInt(ShinyDie) == 0
                      && resultSpecies.Variants.Contains("shiny", StringComparer.Ordinal)
            ? "shiny"
            : "normal";
        return new FusionRollResult(variant, traits);
    }

    /// <summary>Promotion trait growth (lock 7): existing traits KEPT in order; only the new
    /// slots roll, from the SPECIES pool (the demon grows into its kind, not its fuel).</summary>
    public static IReadOnlyList<string> RollPromotionTraits(
        DemonSpeciesDef species, IReadOnlyList<string> existing, int newSlotCount, ulong seed)
    {
        if (existing.Count >= newSlotCount)
            return existing;

        var rng = SeededRng.DeriveStream(seed, "fusion:promotion");
        var traits = existing.ToList();
        var pool = species.TraitPool
            .Where(t => !traits.Contains(t, StringComparer.Ordinal))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();
        while (traits.Count < newSlotCount && pool.Count > 0)
        {
            var i = rng.NextInt(pool.Count);
            traits.Add(pool[i]);
            pool.RemoveAt(i);
        }

        return traits;
    }

    /// <summary>Trait slot count by rarity — the same 1/2/2/3 the summon altar uses.</summary>
    public static int SlotsFor(DemonRarity rarity) => rarity switch
    {
        DemonRarity.Common => 1,
        DemonRarity.Rare => 2,
        DemonRarity.Epic => 2,
        _ => 3
    };
}
