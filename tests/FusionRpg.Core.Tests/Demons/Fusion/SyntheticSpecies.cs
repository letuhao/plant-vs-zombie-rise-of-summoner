using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Tests.Demons.Fusion;

/// <summary>Minimal, hand-built <see cref="DemonSpeciesDef"/> fixtures for tests that need a small,
/// human-checkable roster instead of the real ~829-species corpus (<see cref="RealCorpusFixture"/>)
/// — shared by `FusionRecipeDistributionIndexTests` (T8.1) and `FusionRecipeReconcileTests`
/// (T8.3) so the same handful of required-but-irrelevant fields (`Side`, `GameTypeId`, ...) are
/// filled in exactly once.</summary>
internal static class SyntheticSpecies
{
    public static DemonSpeciesDef Make(
        string id, DemonRarity rarity, DemonAcquisition acquisition, int demonTypeId,
        ElementTypeId elementPrimary = ElementTypeId.Fire) => new()
    {
        SpeciesId = id,
        Name = id,
        Side = "plant",
        GameTypeId = demonTypeId,
        DemonTypeId = demonTypeId,
        ElementPrimary = elementPrimary,
        BaseRarity = rarity,
        Acquisition = acquisition,
    };
}
