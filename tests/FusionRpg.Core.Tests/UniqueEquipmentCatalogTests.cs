using System.Text.Json;
using FusionRpg.Core.Match;
using Xunit;

namespace FusionRpg.Core.Tests;

public class UniqueEquipmentCatalogTests
{
    [Fact]
    public void NormalizeSlot_allowlist_only()
    {
        Assert.Equal("weapon", UniqueEquipmentCatalog.NormalizeSlot("Weapon"));
        Assert.Equal("armor", UniqueEquipmentCatalog.NormalizeSlot("ARMOR"));
        Assert.Throws<ArgumentException>(() => UniqueEquipmentCatalog.NormalizeSlot("hat"));
        Assert.Throws<ArgumentException>(() => UniqueEquipmentCatalog.NormalizeSlot(""));
        Assert.False(UniqueEquipmentCatalog.IsAllowedSlot("foo"));
        Assert.True(UniqueEquipmentCatalog.IsAllowedSlot("trinket"));
    }

    [Fact]
    public void IsKnownItem_stub_catalog()
    {
        Assert.True(UniqueEquipmentCatalog.IsKnownItem("stub.atk_ring"));
        Assert.False(UniqueEquipmentCatalog.IsKnownItem("stub.unknown"));
        Assert.False(UniqueEquipmentCatalog.IsKnownItem(""));
    }

    [Fact]
    public void IsKnownItem_and_TryGetGrant_recognize_real_relics()
    {
        Assert.True(UniqueEquipmentCatalog.IsKnownItem("relic.ashen_reliquary"));
        Assert.True(UniqueEquipmentCatalog.TryGetGrant("relic.ashen_reliquary", out var grant));
        Assert.Equal("fx.passive_atk_flat", grant.EffectId);
        Assert.Equal("equip-relic-ashen_reliquary", grant.GrantId);
    }

    [Fact]
    public void SlotMatchesItem_rejects_a_relic_in_the_wrong_slot_but_allows_stub_items_anywhere()
    {
        Assert.True(UniqueEquipmentCatalog.SlotMatchesItem("weapon", "relic.ashen_reliquary"));
        Assert.False(UniqueEquipmentCatalog.SlotMatchesItem("armor", "relic.ashen_reliquary"));
        Assert.True(UniqueEquipmentCatalog.SlotMatchesItem("armor", "stub.atk_ring"));
    }

    [Fact]
    public void BuildModsJson_includes_a_relic_grant()
    {
        var json = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[] { ("weapon", "relic.ashen_reliquary") });
        using var doc = JsonDocument.Parse(json);
        var grants = doc.RootElement.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal("equip-relic-ashen_reliquary:weapon", grants[0].GetProperty("grantId").GetString());
        Assert.Equal("fx.passive_atk_flat", grants[0].GetProperty("effectId").GetString());
    }

    [Fact]
    public void BuildModsJson_preserves_nested_and_flat_absolutes()
    {
        var json = UniqueEquipmentCatalog.BuildModsJson(
            """{"absolutes":{"hp":42},"atk":9}""",
            new[] { ("weapon", "stub.atk_ring") });
        using var doc = JsonDocument.Parse(json);
        var abs = doc.RootElement.GetProperty("absolutes");
        Assert.Equal(42, abs.GetProperty("hp").GetInt32());
        Assert.Equal(9, abs.GetProperty("atk").GetInt32());
        var grants = doc.RootElement.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal("equip-stub-atk:weapon", grants[0].GetProperty("grantId").GetString());
        Assert.Equal("fx.passive_atk_flat", grants[0].GetProperty("effectId").GetString());
    }

    [Fact]
    public void BuildModsJson_same_stub_two_slots_unique_grantIds()
    {
        var json = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[]
            {
                ("weapon", "stub.atk_ring"),
                ("armor", "stub.atk_ring")
            });
        using var doc = JsonDocument.Parse(json);
        var grants = doc.RootElement.GetProperty("grants");
        Assert.Equal(2, grants.GetArrayLength());
        var ids = grants.EnumerateArray().Select(g => g.GetProperty("grantId").GetString()).ToList();
        Assert.Contains("equip-stub-atk:weapon", ids);
        Assert.Contains("equip-stub-atk:armor", ids);
    }

    [Fact]
    public void BuildModsJson_skips_unknown_item()
    {
        var json = UniqueEquipmentCatalog.BuildModsJson(
            """{"absolutes":{"hp":1}}""",
            new[] { ("weapon", "stub.nope") });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("grants").GetArrayLength());
        Assert.Equal(1, doc.RootElement.GetProperty("absolutes").GetProperty("hp").GetInt32());
    }

    [Fact]
    public void UniqueLoadoutMerge_empty_deploy_keeps_equip_mods()
    {
        var mods = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[] { ("trinket", "stub.butter_bead") });
        var merged = UniqueLoadoutMerge.Merge(null, mods);
        Assert.Equal(mods, merged);
        Assert.Contains("fx.butter_on_hit", merged, StringComparison.Ordinal);
        var deployWins = UniqueLoadoutMerge.Merge("""{"absolutes":{"hp":1}}""", mods);
        Assert.Contains("\"hp\":1", deployWins.Replace(" ", ""), StringComparison.Ordinal);
        Assert.DoesNotContain("butter", deployWins, StringComparison.OrdinalIgnoreCase);
    }
}
