using System.Text.Json;
using FusionRpg.Contracts;

namespace FusionRpg.Core.Match;

/// <summary>
/// Stub item_id → grant template map for W8-A Cold equip (not a gear shop).
/// EffectIds align with offline effect fixtures where possible.
/// </summary>
public static class UniqueEquipmentCatalog
{
    public static readonly string[] DefaultSlots = { "weapon", "armor", "trinket" };

    static readonly HashSet<string> AllowedSlots =
        new(DefaultSlots, StringComparer.OrdinalIgnoreCase);

    static readonly string[] FlatAbsoluteKeys = { "hp", "maxHp", "atk", "HP", "MaxHp", "ATK" };

    /// <summary>Known stub items operators may equip.</summary>
    public static IReadOnlyDictionary<string, EffectGrantDto> Items { get; } =
        new Dictionary<string, EffectGrantDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["stub.atk_ring"] = Grant("equip-stub-atk", "fx.passive_atk_flat"),
            ["stub.butter_bead"] = Grant("equip-stub-butter", "fx.butter_on_hit"),
            ["stub.hp_charm"] = Grant("equip-stub-hp", "fx.entity_atk") // placeholder effect id for bag prove
        };

    /// <summary>
    /// `mods-absorption` (T6.1, `spec-mods-absorption.md`): which of the shipped `EffectId`s already
    /// have a real, seeded atom (`data/seed/containers/unique-equip.json`, wrapping the SAME atoms
    /// `EffectAtomCatalog.Generated.cs` already compiles from — found real, not invented, 2026-09-02).
    /// `fx.entity_atk` is deliberately absent — its own doc comment on <see cref="Items"/> already
    /// calls it a placeholder id with no real effect behind it, verified by grep across every seed
    /// file: nothing produces it. An item/relic granting through `fx.entity_atk` stays on the legacy
    /// `mods_json` grant path; every other one now produces through <c>InstanceProducer</c> instead.
    /// </summary>
    static readonly IReadOnlyDictionary<string, string> AtomBackedContainerByEffectId =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fx.passive_atk_flat"] = "item.fx-passive-atk-flat",
            ["fx.butter_on_hit"] = "item.fx-butter-on-hit",
            ["fx.shield_grant"] = "item.fx-shield-grant",
            ["fx.cold_on_hit"] = "item.fx-cold-on-hit",
        };

    /// <summary>The real container id for an equipped item/relic's own effect, or null when none
    /// exists yet (the item stays on the legacy `mods_json` grant path for that case).</summary>
    public static bool TryGetAtomBackedContainerId(string? itemId, out string containerId)
    {
        containerId = "";
        if (!TryGetGrant(itemId, out var grant)) return false;
        return AtomBackedContainerByEffectId.TryGetValue(grant.EffectId, out containerId!);
    }

    public static bool IsAllowedSlot(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return false;
        return AllowedSlots.Contains(slot.Trim());
    }

    public static bool IsKnownItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        var id = itemId.Trim();
        return Items.ContainsKey(id) || RelicCatalog.IsKnownRelic(id);
    }

    public static bool TryGetGrant(string? itemId, out EffectGrantDto grant)
    {
        grant = null!;
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        var id = itemId.Trim();
        if (Items.TryGetValue(id, out var g) && g is not null)
        {
            grant = Clone(g);
            return true;
        }
        return RelicCatalog.TryGetGrant(id, out grant);
    }

    /// <summary>True unless the item is a known relic declared for a different slot.
    /// Stub items (<see cref="Items"/>) carry no slot of their own, so any allowed slot fits them.</summary>
    public static bool SlotMatchesItem(string normalizedSlot, string itemId)
    {
        if (!RelicCatalog.TryGetRelic(itemId, out var relic)) return true;
        return string.Equals(relic.Slot, normalizedSlot, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Normalize to lowercase allowlisted slot; throws on empty/unknown.</summary>
    public static string NormalizeSlot(string? slot)
    {
        var s = (slot ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(s) || !AllowedSlots.Contains(s))
            throw new ArgumentException("slot required", nameof(slot));
        return s;
    }

    /// <summary>
    /// Build mods_json: keep existing absolutes (nested + flat root keys); replace grants from equipped slots.
    /// GrantIds are stamped <c>base:slot</c> so the same stub in two slots does not collapse.
    /// </summary>
    public static string BuildModsJson(
        string? existingModsJson,
        IEnumerable<(string Slot, string ItemId)> equipped)
    {
        var absolutes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(existingModsJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(existingModsJson);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("absolutes", out var abs) &&
                        abs.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in abs.EnumerateObject())
                        {
                            if (p.Value.TryGetInt32(out var n))
                                absolutes[p.Name] = n;
                        }
                    }

                    foreach (var key in FlatAbsoluteKeys)
                    {
                        if (root.TryGetProperty(key, out var flat) &&
                            flat.TryGetInt32(out var n) &&
                            !absolutes.ContainsKey(key))
                            absolutes[key] = n;
                    }
                }
            }
            catch
            {
                /* ignore bad prior json */
            }
        }

        var grants = new List<EffectGrantDto>();
        foreach (var (slotRaw, itemId) in equipped)
        {
            if (string.IsNullOrWhiteSpace(itemId)) continue;
            string slot;
            try { slot = NormalizeSlot(slotRaw); }
            catch { continue; }
            if (!TryGetGrant(itemId, out var g)) continue;
            g.GrantId = $"{g.GrantId}:{slot}";
            grants.Add(g);
        }

        var payload = new Dictionary<string, object?>
        {
            ["absolutes"] = absolutes,
            ["grants"] = grants
        };
        return JsonSerializer.Serialize(payload);
    }

    static EffectGrantDto Grant(string grantId, string effectId) => new()
    {
        GrantId = grantId,
        EffectId = effectId,
        OwnerKind = "instance",
        OwnerKey = "instance:pending",
        PluginId = "unique.equip",
        Priority = 0
    };

    static EffectGrantDto Clone(EffectGrantDto g) => new()
    {
        GrantId = g.GrantId,
        EffectId = g.EffectId,
        OwnerKind = g.OwnerKind,
        OwnerKey = g.OwnerKey,
        PluginId = g.PluginId,
        Priority = g.Priority,
        Overlay = g.Overlay is null ? null : new Dictionary<string, object?>(g.Overlay)
    };
}
