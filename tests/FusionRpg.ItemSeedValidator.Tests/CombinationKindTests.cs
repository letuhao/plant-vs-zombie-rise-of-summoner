using Xunit;

namespace FusionRpg.ItemSeedValidator.Tests;

/// <summary>
/// item-seedgen `combination-write-unblock` (2026-09-07) — the real, previously-undiscovered gaps
/// found by running the real validator against a real generated `combination` entry for the first
/// time (its own Python-side `deps.validate_entries` only ever checked cross-corpus references, never
/// the full seed-contract). Three fixes, three regressions pinned here: the `combination` `SeedKind`
/// itself (`KindCatalog.cs`), the `SequenceShape.Derived` id-prefix allocation (`NamespaceAllocation.cs`),
/// and the kind-scoped ownership exception for `combogen`'s own code-derived
/// `minTier`/`quantity`/`grantedTier` fields (`OwnershipCheck.cs`).
/// </summary>
public class CombinationKindTests
{
    static IEnumerable<string> Codes(string json) =>
        SeedFixture.ErrorCodes(SeedFixture.Validate(json, kindDirectory: "combinations",
            path: "combinations/strains.json"));

    [Fact]
    public void A_conforming_combination_entry_produces_no_errors()
    {
        var result = SeedFixture.Validate(
            SeedFixture.CombinationFile(SeedFixture.CombinationEntry()),
            kindDirectory: "combinations", path: "combinations/strains.json");
        Assert.Equal(0, result.ErrorCount);
        Assert.False(result.ScannedNothing);
    }

    [Fact]
    public void A_combo_id_resolves_against_the_new_namespace_not_outside_it()
    {
        // The real bug: ExpandSlotOrFlat hard-assumed every idNamespaces group has a {seq:03}
        // template. `combinations` genuinely has none (combogen mints ids from a deterministic grid
        // cell), so it needs its own expansion case rather than falling through to a Problem.
        var codes = Codes(SeedFixture.CombinationFile(SeedFixture.CombinationEntry())).ToList();
        Assert.DoesNotContain("IdOutsideNamespace", codes);
    }

    [Fact]
    public void A_splice_id_resolves_too_not_only_strain()
    {
        var codes = Codes(SeedFixture.CombinationFile(
            SeedFixture.CombinationEntry(id: "combo.splice-verify-001",
                nameKey: "combination.splice-verify-001"))).ToList();
        Assert.DoesNotContain("IdOutsideNamespace", codes);
    }

    [Fact]
    public void An_id_with_neither_allocated_combo_prefix_still_refuses()
    {
        // The allowlist is real, not a blanket pass for the whole kind -- an id that names neither
        // combogen shape must still be caught.
        var codes = Codes(SeedFixture.CombinationFile(
            SeedFixture.CombinationEntry(id: "combo.mystery-verify-001"))).ToList();
        Assert.Contains("IdOutsideNamespace", codes);
    }

    [Fact]
    public void The_nameKey_prefix_is_registered_not_refused()
    {
        var codes = Codes(SeedFixture.CombinationFile(SeedFixture.CombinationEntry())).ToList();
        Assert.DoesNotContain("NameKeyPrefix", codes);
    }

    [Fact]
    public void MinTier_quantity_and_grantedTier_are_never_MagnitudeAuthored_or_OwnershipViolation_on_a_combination()
    {
        // These are combogen's own code-derived fields (emit.assemble_entry, tuning.load), never
        // model-authored -- the same P1 split ("model picks identity, code picks magnitude") the
        // whole seed-contract enforces, not a violation of it.
        var codes = Codes(SeedFixture.CombinationFile(SeedFixture.CombinationEntry())).ToList();
        Assert.DoesNotContain("MagnitudeAuthored", codes);
        Assert.DoesNotContain("OwnershipViolation", codes);
    }

    [Fact]
    public void The_quantity_exception_is_scoped_to_combination_ingredients_only()
    {
        // Regression for the narrowness of the fix: a `quantity` field OUTSIDE combinations/ingredients
        // (a recipe's cost line, say) must still be refused -- this is a recipe-shaped balance lever,
        // not combogen's structural ingredient count, and the exception must not leak past its own kind
        // and path.
        var codes = SeedFixture.ErrorCodes(SeedFixture.Validate(
            SeedFixture.BaseTypeFile(SeedFixture.BaseTypeEntry(
                extra: "\"costLines\": [{ \"material\": \"substrate.humanoid.crude\", \"quantity\": 3 }]"))))
            .ToList();
        Assert.Contains("OwnershipViolation", codes);
    }

    [Fact]
    public void The_minTier_exception_is_scoped_to_the_combination_kind_only()
    {
        // Same narrowness check for minTier/grantedTier: a base-type entry carrying `minTier` (not a
        // real base-type field, but the point is the SHAPE) must not silently pass just because the
        // exception exists somewhere in the codebase.
        var codes = SeedFixture.ErrorCodes(SeedFixture.Validate(
            SeedFixture.BaseTypeFile(SeedFixture.BaseTypeEntry(extra: "\"minTier\": 2"))))
            .ToList();
        Assert.Contains("MagnitudeAuthored", codes);
    }
}
