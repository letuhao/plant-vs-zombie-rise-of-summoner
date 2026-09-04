using FusionRpg.Core.World;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// L25 acceptance (spec-structure-substrate.md): the catalog validates itself at bootstrap, and
/// <c>Rule14</c> pairs a structure to the slot kind it was built for.
/// </summary>
public class StructureCatalogTests
{
    [Fact]
    public void The_catalog_validates_on_first_touch()
    {
        Assert.NotEmpty(StructureCatalog.All);
    }

    [Fact]
    public void Every_seed_row_is_known_by_id()
    {
        foreach (var structure in StructureCatalog.All)
            Assert.True(StructureCatalog.IsKnown(structure.StructureId));
    }

    [Fact]
    public void An_unknown_id_is_not_known_and_Get_throws()
    {
        Assert.False(StructureCatalog.IsKnown("not-a-real-structure"));
        Assert.Throws<ArgumentException>(() => StructureCatalog.Get("not-a-real-structure"));
    }

    [Fact]
    public void Duplicate_and_malformed_ids_reject()
    {
        var dupe = new[]
        {
            new StructureDef { StructureId = "stable", Name = "A", RequiredSlotKind = SlotKind.Rootbed },
            new StructureDef { StructureId = "stable", Name = "B", RequiredSlotKind = SlotKind.Rootbed }
        };
        Assert.Contains("Duplicate", Assert.Throws<InvalidOperationException>(
            () => StructureCatalog.Validate(dupe)).Message);

        var shouty = new[]
        {
            new StructureDef { StructureId = "Stable", Name = "A", RequiredSlotKind = SlotKind.Rootbed }
        };
        Assert.Throws<InvalidOperationException>(() => StructureCatalog.Validate(shouty));
    }

    [Fact]
    public void A_negative_cost_or_multiplier_rejects()
    {
        var badCost = new[]
        {
            new StructureDef { StructureId = "bad-cost", Name = "A", RequiredSlotKind = SlotKind.Rootbed, Cost = -1 }
        };
        Assert.Throws<InvalidOperationException>(() => StructureCatalog.Validate(badCost));

        var badMultiplier = new[]
        {
            new StructureDef { StructureId = "bad-multiplier", Name = "A", RequiredSlotKind = SlotKind.Rootbed, YieldMultiplierMilli = -1 }
        };
        Assert.Throws<InvalidOperationException>(() => StructureCatalog.Validate(badMultiplier));
    }

    [Fact]
    public void A_default_multiplier_leaves_yield_unchanged()
    {
        var structure = StructureCatalog.Get("loam-source-placeholder");
        Assert.Equal(1000, structure.YieldMultiplierMilli);
        Assert.Equal(SlotKind.Rootbed, structure.RequiredSlotKind);
    }
}
