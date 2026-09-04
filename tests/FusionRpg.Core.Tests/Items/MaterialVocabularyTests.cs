using System.Reflection;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Materials;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Items;

/// <summary>
/// `salvage-craft` (item module 14) §1 — the closed five-class, 27-id cost vocabulary every other
/// sink spends in, and the matrix that makes it enforceable rather than advisory.
/// </summary>
public class MaterialVocabularyTests
{
    [Fact]
    public void The_vocabulary_is_twenty_seven_closed_ids_in_five_classes()
    {
        var all = MaterialCatalog.All;
        Assert.Equal(27, all.Count);
        Assert.Equal(27, all.Distinct(StringComparer.Ordinal).Count());

        // The counts the spec's §1 table publishes, each measured off the shipped roster it derives
        // from rather than transcribed: 10 rungs, 2 frames x 4 grades, 6 concrete elements, 3 verbs.
        Assert.Equal(DemonRarityLadder.RungCount, all.Count(i => i.StartsWith("shard.", StringComparison.Ordinal)));
        Assert.Equal(8, all.Count(i => i.StartsWith("substrate.", StringComparison.Ordinal)));
        Assert.Equal(ElementRoster.Concrete.Count, all.Count(i => i.StartsWith("essence.", StringComparison.Ordinal)));
        Assert.Equal(3, all.Count(i => i.StartsWith("catalyst.", StringComparison.Ordinal)));

        // 27 and not 28: souls carry no material id, they are a ledger balance. The class exists;
        // the id does not.
        Assert.Contains(MaterialClass.Souls, Enum.GetValues<MaterialClass>());
        Assert.DoesNotContain(all, i => i.StartsWith("souls", StringComparison.Ordinal));
        Assert.Equal(5, Enum.GetValues<MaterialClass>().Length);
    }

    [Fact]
    public void Ten_shard_ids_exist_and_four_legacy_ids_resolve_but_are_never_minted()
    {
        // The platform state spec-salvage-craft.md verified I9's "four bands ship" claim FALSE
        // against, pinned here so a regression is loud.
        foreach (var rarity in DemonRarityLadder.All)
            Assert.True(MaterialCatalog.IsIssuable(MaterialCatalog.ShardId(rarity)), rarity.ToId());

        foreach (var legacy in new[] { "shard.common", "shard.rare", "shard.epic", "shard.legendary" })
        {
            Assert.True(MaterialCatalog.IsKnown(legacy), $"{legacy} must still RESOLVE");
            Assert.False(MaterialCatalog.IsIssuable(legacy), $"{legacy} must never be MINTED");
            Assert.True(MaterialCatalog.IsLegacyShardId(legacy));
            Assert.DoesNotContain(legacy, MaterialCatalog.All);
        }

        // And the sixteen shipped ids are REUSED from DemonMaterialCatalog, not re-minted here.
        foreach (var id in DemonMaterialCatalog.All)
            Assert.True(MaterialCatalog.IsIssuable(id), id);
    }

    [Theory]
    [InlineData("essence.fire.pvz")]
    [InlineData("shard.heirloom.web")]
    [InlineData("catalyst.forge.lawn")]
    public void A_source_tagged_material_id_is_refused(string id)
    {
        // Boundaries, "Never": the injector enriches, it never gates (SC8). A PvZ-exclusive material
        // id would make the lawn a required source for a web operation.
        Assert.False(MaterialCatalog.IsKnown(id));
        Assert.Throws<MaterialVocabularyRejection>(() => MaterialCatalog.ClassOf(id));
    }

    [Fact]
    public void Spend_order_is_souls_shard_substrate_essence_catalyst()
    {
        // Fixed class order matters: a partial failure always fails at the same point, so two logs
        // of one refusal are byte-comparable. The enum's member order IS the spend order.
        Assert.Equal(
            new[] { MaterialClass.Souls, MaterialClass.Shard, MaterialClass.Substrate, MaterialClass.Essence, MaterialClass.Catalyst },
            Enum.GetValues<MaterialClass>().OrderBy(MaterialCatalog.ClassRank).ToArray());
    }

    [Fact]
    public void Cost_class_forbidden_rejects_a_forge_spending_temper()
    {
        // The spec's own named example. `forge` DOES spend a catalyst, so this is not "no catalyst
        // allowed" — it is the RIGHT class carrying the WRONG catalyst, which is why the matrix
        // raises a second, distinct rule for it.
        var refusal = CostClassMatrix.Check(CraftOperation.Forge, "catalyst.temper");
        Assert.NotNull(refusal);
        Assert.Equal(CostClassMatrix.CatalystMismatchRule, refusal!.Value.Rule);
        Assert.Contains("catalyst.forge", refusal.Value.Detail);

        // And a class the operation may not spend at all is the OTHER rule.
        var forbidden = CostClassMatrix.Check(CraftOperation.Forge, "shard.heirloom");
        Assert.NotNull(forbidden);
        Assert.Equal(CostClassMatrix.CostClassForbiddenRule, forbidden!.Value.Rule);

        Assert.Null(CostClassMatrix.Check(CraftOperation.Forge, "catalyst.forge"));
        Assert.Null(CostClassMatrix.Check(CraftOperation.Forge, "substrate.plant.sound"));
    }

    [Fact]
    public void Imbue_rides_forge_and_the_two_catalyst_free_operations_ride_none()
    {
        // §"The three catalysts": make / improve / re-randomise.
        Assert.Equal("catalyst.forge", CostClassMatrix.CatalystFor(CraftOperation.Forge));
        Assert.Equal("catalyst.forge", CostClassMatrix.CatalystFor(CraftOperation.ForgeGem));
        Assert.Equal("catalyst.forge", CostClassMatrix.CatalystFor(CraftOperation.Bore));
        Assert.Equal("catalyst.forge", CostClassMatrix.CatalystFor(CraftOperation.Imbue));
        Assert.Equal("catalyst.temper", CostClassMatrix.CatalystFor(CraftOperation.Temper));
        Assert.Equal("catalyst.temper", CostClassMatrix.CatalystFor(CraftOperation.Elevate));
        Assert.Equal("catalyst.flux", CostClassMatrix.CatalystFor(CraftOperation.RerollOne));
        Assert.Equal("catalyst.flux", CostClassMatrix.CatalystFor(CraftOperation.RerollAll));

        // Neither brings anything into existence, so neither burns a catalyst — and socketing an
        // insert you already own must never be a material decision (I9 §8.4).
        Assert.Null(CostClassMatrix.CatalystFor(CraftOperation.Upcycle));
        Assert.Null(CostClassMatrix.CatalystFor(CraftOperation.Socket));
        Assert.False(CostClassMatrix.Allows(CraftOperation.Socket, MaterialClass.Catalyst));
        Assert.False(CostClassMatrix.Allows(CraftOperation.Socket, MaterialClass.Substrate));
    }

    [Fact]
    public void The_operation_vocabulary_is_ten_verbs_and_imbue_is_the_tenth()
    {
        // I9 §6.1's enum is SEVEN. The shipped vocabulary is ten: `forge-gem` and the reroll split
        // I9 §7.4 already implies, plus D24's `imbue`, which has no row anywhere in I9.
        Assert.Equal(10, CraftOperations.All.Count);
        Assert.Equal(
            new[] { "forge", "upcycle", "forge-gem", "bore", "imbue", "socket", "elevate", "temper", "reroll-one", "reroll-all" },
            CraftOperations.AllIds);

        Assert.True(CraftOperations.TryParse("imbue", out var imbue));
        Assert.Equal(CraftOperation.Imbue, imbue);

        // ⛔ `reroll` (the shipped corpus's verb) is deliberately NOT parseable — the reroll-one /
        // reroll-all split is module 15's, and inventing it here would mint a second op_kind
        // vocabulary the spec's Boundaries forbid outright.
        Assert.False(CraftOperations.TryParse("reroll", out _));
        Assert.False(CraftOperations.TryParse("socket-imbue", out _));
    }

    [Fact]
    public void Substrate_ids_round_trip_through_frame_and_grade_and_refuse_a_fifth_grade()
    {
        foreach (var frame in MaterialCatalog.SubstrateFrames)
        {
            for (var g = 1; g <= MaterialCatalog.SubstrateGrades.Count; g++)
            {
                var id = MaterialCatalog.SubstrateId(frame, g);
                Assert.True(MaterialCatalog.IsIssuable(id));
                Assert.Equal(g, MaterialCatalog.GradeOf(id));
                Assert.Equal(frame, MaterialCatalog.FrameOf(id));
                Assert.Equal(MaterialClass.Substrate, MaterialCatalog.ClassOf(id));
            }
        }

        // Throws rather than clamping: a silent clamp to prime would hand out the exact material
        // the grade lock exists to protect.
        Assert.Throws<MaterialVocabularyRejection>(() => MaterialCatalog.SubstrateId("humanoid", 5));
        Assert.Throws<MaterialVocabularyRejection>(() => MaterialCatalog.SubstrateId("humanoid", 0));
        Assert.Throws<MaterialVocabularyRejection>(() => MaterialCatalog.SubstrateId("mineral", 1));
    }

    [Fact]
    public void Every_id_in_the_vocabulary_classifies_and_nothing_outside_it_does()
    {
        foreach (var id in MaterialCatalog.All)
            Assert.True(Enum.IsDefined(MaterialCatalog.ClassOf(id)));

        foreach (var bad in new[] { "", "souls", "substrate.humanoid", "essence.omni", "catalyst.reforge", "shard.mythic" })
            Assert.Throws<MaterialVocabularyRejection>(() => MaterialCatalog.ClassOf(bad));
    }

    [Fact]
    public void No_cost_input_reads_a_player_property()
    {
        // D26, MECHANICALLY. The context types simply have nowhere to put a player stat, and the
        // resolver takes no other argument that could carry one. This is a shape assertion, not a
        // grep: a field added later is caught by name here.
        Assert.Equal(
            new[] { "TargetRungIndex", "TargetTier", "TargetItemLevel", "TargetFrame", "EnhanceLevel" },
            typeof(RecipeContext).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract").Select(p => p.Name).ToArray());

        Assert.Equal(
            new[] { "RungIndex", "ItemLevel", "Frame", "AffixCount", "ElementalAffixCounts", "EnhanceLevel" },
            typeof(SalvageInput).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != "EqualityContract").Select(p => p.Name).ToArray());

        // Resolve(recipeId, context) — no third argument, so nothing can be smuggled past the type.
        var resolve = typeof(MaterialRecipeCatalog).GetMethod(nameof(MaterialRecipeCatalog.Resolve))!;
        Assert.Equal(new[] { typeof(string), typeof(RecipeContext) },
            resolve.GetParameters().Select(p => p.ParameterType).ToArray());

        // And the closed variable vocabulary has no spelling for a player term at all.
        foreach (var v in Enum.GetNames<CostVariable>())
            Assert.DoesNotContain(v.ToLowerInvariant(), new[] { "theta", "playerlevel", "powerindex", "daily", "session" });
    }
}
