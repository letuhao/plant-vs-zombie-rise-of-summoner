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
        // relic.cracked_seal (fx.entity_atk) is the one relic with no real atom behind it
        // (mods-absorption, spec-mods-absorption.md) — still the legacy grant path.
        // relic.ashen_reliquary (fx.passive_atk_flat) moved to effect_binding; see
        // BuildModsJson_excludes_grant_for_atom_backed_items below.
        var json = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[] { ("trinket", "relic.cracked_seal") });
        using var doc = JsonDocument.Parse(json);
        var grants = doc.RootElement.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal("equip-relic-cracked_seal:trinket", grants[0].GetProperty("grantId").GetString());
        Assert.Equal("fx.entity_atk", grants[0].GetProperty("effectId").GetString());
    }

    [Fact]
    public void BuildModsJson_excludes_grant_for_atom_backed_items()
    {
        // mods-absorption (spec-mods-absorption.md): an atom-backed item's grant no longer reaches
        // mods_json at all — RpgStore.ReconcileUniqueEquipmentAtomBindingsUnlocked is its only path
        // now, or the actor would carry the same slot's effect through both at once.
        var json = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[] { ("weapon", "stub.atk_ring") });
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(0, doc.RootElement.GetProperty("grants").GetArrayLength());
    }

    [Fact]
    public void BuildModsJson_preserves_nested_and_flat_absolutes()
    {
        var json = UniqueEquipmentCatalog.BuildModsJson(
            """{"absolutes":{"hp":42},"atk":9}""",
            new[] { ("weapon", "stub.hp_charm") }); // legacy path — stub.atk_ring's grant moved off mods_json
        using var doc = JsonDocument.Parse(json);
        var abs = doc.RootElement.GetProperty("absolutes");
        Assert.Equal(42, abs.GetProperty("hp").GetInt32());
        Assert.Equal(9, abs.GetProperty("atk").GetInt32());
        var grants = doc.RootElement.GetProperty("grants");
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal("equip-stub-hp:weapon", grants[0].GetProperty("grantId").GetString());
        Assert.Equal("fx.entity_atk", grants[0].GetProperty("effectId").GetString());
    }

    [Fact]
    public void BuildModsJson_same_stub_two_slots_unique_grantIds()
    {
        // stub.hp_charm — legacy path; stub.atk_ring's own grantId-stamping moved to effect_binding's
        // per-slot binding, proven in ModsAbsorptionTests.
        var json = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[]
            {
                ("weapon", "stub.hp_charm"),
                ("armor", "stub.hp_charm")
            });
        using var doc = JsonDocument.Parse(json);
        var grants = doc.RootElement.GetProperty("grants");
        Assert.Equal(2, grants.GetArrayLength());
        var ids = grants.EnumerateArray().Select(g => g.GetProperty("grantId").GetString()).ToList();
        Assert.Contains("equip-stub-hp:weapon", ids);
        Assert.Contains("equip-stub-hp:armor", ids);
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
        // stub.butter_bead's own grant moved to effect_binding (mods-absorption); stub.hp_charm stays
        // on the legacy path, so this merge behavior still has a non-empty grants list to prove against.
        var mods = UniqueEquipmentCatalog.BuildModsJson(
            "{}",
            new[] { ("trinket", "stub.hp_charm") });
        var merged = UniqueLoadoutMerge.Merge(null, mods);
        Assert.Equal(mods, merged);
        Assert.Contains("fx.entity_atk", merged, StringComparison.Ordinal);
        var deployWins = UniqueLoadoutMerge.Merge("""{"absolutes":{"hp":1}}""", mods);
        Assert.Contains("\"hp\":1", deployWins.Replace(" ", ""), StringComparison.Ordinal);
        Assert.DoesNotContain("entity_atk", deployWins, StringComparison.OrdinalIgnoreCase);
    }
}
