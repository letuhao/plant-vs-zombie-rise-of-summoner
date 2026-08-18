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

    public static bool IsAllowedSlot(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot)) return false;
        return AllowedSlots.Contains(slot.Trim());
    }

    public static bool IsKnownItem(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        return Items.ContainsKey(itemId.Trim());
    }

    public static bool TryGetGrant(string? itemId, out EffectGrantDto grant)
    {
        grant = null!;
        if (string.IsNullOrWhiteSpace(itemId)) return false;
        if (!Items.TryGetValue(itemId.Trim(), out var g) || g is null) return false;
        grant = Clone(g);
        return true;
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
