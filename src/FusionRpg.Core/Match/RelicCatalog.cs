using FusionRpg.Contracts;

namespace FusionRpg.Core.Match;

/// <summary>
/// A small, real, seeded relic catalog (T14 — no acquisition system exists yet, so every
/// player holds the full catalog; see game-gui-todo.md's honest scoping note). Equipping
/// reuses the existing per-actor `rpg_unique_equipment` pipeline via
/// <see cref="UniqueEquipmentCatalog.IsKnownItem"/> / <see cref="UniqueEquipmentCatalog.TryGetGrant"/>,
/// which check this catalog after their own stub items. Effect ids are drawn from the
/// existing, already-shipped effect vocabulary — nothing new is added to Foundation here.
/// </summary>
public static class RelicCatalog
{
    public static readonly IReadOnlyList<RelicDto> Items = new List<RelicDto>
    {
        new()
        {
            Id = "relic.ashen_reliquary",
            Name = "Ashen Reliquary",
            Rarity = 4,
            Slot = "weapon",
            Description = "A reliquary warm to the touch. Channels raw offense.",
            EffectId = "fx.passive_atk_flat"
        },
        new()
        {
            Id = "relic.sunworn_charm",
            Name = "Sunworn Charm",
            Rarity = 2,
            Slot = "weapon",
            Description = "A sun-bleached charm, favoring survival over aggression.",
            EffectId = "fx.shield_grant"
        },
        new()
        {
            Id = "relic.tidewrack_band",
            Name = "Tidewrack Band",
            Rarity = 3,
            Slot = "armor",
            Description = "Salt-crusted band pulled from a flooded lawn.",
            EffectId = "fx.cold_on_hit"
        },
        new()
        {
            Id = "relic.cracked_seal",
            Name = "Cracked Seal",
            Rarity = 1,
            Slot = "trinket",
            Description = "A minor ward, barely holding together.",
            EffectId = "fx.entity_atk"
        }
    };

    static readonly IReadOnlyDictionary<string, RelicDto> ById =
        Items.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

    public static bool IsKnownRelic(string? relicId) =>
        !string.IsNullOrWhiteSpace(relicId) && ById.ContainsKey(relicId.Trim());

    public static bool TryGetRelic(string? relicId, out RelicDto relic)
    {
        relic = null!;
        if (string.IsNullOrWhiteSpace(relicId)) return false;
        return ById.TryGetValue(relicId.Trim(), out relic!);
    }

    public static bool TryGetGrant(string? relicId, out EffectGrantDto grant)
    {
        grant = null!;
        if (!TryGetRelic(relicId, out var relic)) return false;
        grant = new EffectGrantDto
        {
            GrantId = $"equip-relic-{relic.Id[6..]}",
            EffectId = relic.EffectId,
            OwnerKind = "instance",
            OwnerKey = "instance:pending",
            PluginId = "unique.equip",
            Priority = 0
        };
        return true;
    }
}
