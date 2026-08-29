# Chaos derived-stats audit — full external inventory

**Status:** Research reference. **Frozen inventory, read this instead of re-reading the source repo.**
Normative specs are ours: [../architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md) §3
(channel registration), [../architecture/element-hub-ssot.md](../architecture/element-hub-ssot.md) §6
(family list + omni rule), [../architecture/combat-damage-ssot.md](../architecture/combat-damage-ssot.md) §5
(what overlay combat consumes).

**Source:** `D:/Works/source/chaos-repositories/chaos-backend-service/docs` — 323 markdown files across
34 subsystem folders. Audited 2026-08-24. Do not vendor code, YAML, or formulas from it.

**Why this file exists.** Three sessions have now walked that doc tree to answer the same question
("what derived stats are we missing?"). It is 323 files, the counts inside it disagree with each
other, and the answer is stable. This is the answer. Companion mappings, written earlier and narrower:
[actor-core-chaos-mapping.md](actor-core-chaos-mapping.md) (power/realm curve),
[status-core-chaos-mapping.md](status-core-chaos-mapping.md) (apply order).

---

## 1. Where the stats actually live in that repo

Only six of the 34 folders define stats. The rest are services, migrations, and optimisation notes.

| Folder | Files | What it defines |
|---|---|---|
| `actor-core/designs/21_Dimension_Catalog.md` | 1 | The primary + derived + meta dimension list (31) |
| `element-core/` | 61 | The big one — per-element derived stats (48 named + 3 maps) |
| `element-core/elements/configs/*.yaml` | 8 | Per-element instantiation of that list |
| `race-core/00_Tinh_Primary_Stats_Design.md` | 1 | A **second, competing** primary set (5 + 5) |
| `action-core/02, 11, 16` | 3 | Defense-side and movement-side derived stats |
| `resource-manager/`, `combat-core/` | 34 | Resource types, damage stats |

**Counts in that repo are not reliable.** `element-core/elements/configs/DERIVED_STATS_CONSISTENCY_CHECK.md`
summarises itself as "38+ individual stats"; its own category list sums to **48**. Counted by hand
2026-08-24 — use 48.

---

## 2. Actor Core — 31 dimensions

`actor-core/designs/21_Dimension_Catalog.md`

| Tier | n | Stats |
|---|---|---|
| Primary | 5 | `strength` · `vitality` · `dexterity` · `intelligence` · `spirit` |
| Health/resource | 3 | `hp_max` · `mp_max` · `stamina_max` |
| Combat | 4 | `attack_power` · `defense` · `magic_power` · `magic_resistance` |
| Crit/accuracy | 3 | `crit_rate` · `crit_damage` · `accuracy` |
| Speed | 3 | `move_speed` · `attack_speed` · `cast_speed` |
| Efficiency | 3 | `cooldown_reduction` · `mana_efficiency` · `energy_efficiency` |
| Progression | 3 | `learning_rate` · `cultivation_speed` · `breakthrough_success` |
| Meta/world | 6 | `lifespan_years` · `poise_rank` · `charisma` · `stealth` · `perception` · `luck` |

Every derived dimension there carries a **hard min/max clamp** (`crit_damage: 1.0–10.0`,
`cooldown_reduction: 0.0–0.5`, `attack_power: 0–999,999`). **We do not port the clamps** — PS-8 makes a
cap on a magnitude a progression ceiling ([power/ssot-power-scale.md](../architecture/power/ssot-power-scale.md) §11).
Bounded ratios (`0..1` rates) are exempt and stay bounded; magnitudes must not.

---

## 3. Element Core — 48 named + 3 maps, **per element**

`element-core/elements/configs/DERIVED_STATS_CONSISTENCY_CHECK.md`, instantiated in each of
`fire_element.yaml` · `water` · `earth` · `wood` · `metal` · `wind` · `lightning` · `ice`.

The organising idea worth borrowing is **counterbalance pairs** — every offensive stat ships with its
named defensive counterpart, so no stat can be added without deciding what answers it.

### 3.1 Counterbalance pairs (22)

| Attacker side | Defender side | Ours today |
|---|---|---|
| `power_point` | `defense_point` | ✅ `combat.power.*` / `combat.defense.*` |
| `crit_rate` | `resist_crit_rate` | ✅ `combat.crit.rate.*` / `combat.crit.resist.*` |
| `crit_damage` | `resist_crit_damage` | ✅ `combat.crit.damage.*` / `combat.crit.resist.damage.*` |
| `accurate_rate` | `dodge_rate` | ✅ `combat.accuracy.*` / `combat.dodge.*` |
| `status_probability` | `status_resistance` | ⚠️ ours are **category**-typed (`dot`/`cc`/`contagion`), not element-typed |
| `status_duration` | `status_duration_reduction` | ❌ |
| `status_intensity` | `status_intensity_reduction` | ❌ |
| `element_penetration` | `element_absorption` | ❌ |
| `element_amplification` | `element_reduction` | ❌ |
| `reflection_rate` | `resist_reflection_rate` | ❌ |
| `reflection_damage` | `resist_reflection_damage` | ❌ |

### 3.2 Parry (4) and Block (4)

`parry_rate` · `parry_break` · `parry_strength` · `parry_shred`
`block_rate` · `block_break` · `block_strength` · `block_shred`

The pairing is attacker-vs-defender inside each set: `rate`/`strength` are the defender's, `break`/`shred`
are the attacker's answer. Resolution order stated in `element-core/11_Advanced_Derived_Stats_Design.md`:
**Hit → Parry → Block → penetration → defense**; parry short-circuits, block subtracts before shields.

> **Overlap warning.** `block_strength ↔ block_shred` is arithmetically identical to our shipped
> `combat.shield.toughness ↔ combat.shield.pen`, which already carries a **golden-locked saturation
> curve** — chip floor `0.10×`, pen cap `3×`, deliberately asymmetric so defense saturates harder
> ([shield-system-spec.md](../architecture/shield-system-spec.md) §2.4). Reuse that curve; do not
> author a second one.

### 3.3 Skill execution & effectiveness (10)

`skill_execution_speed` · `skill_cooldown_reduction` · `skill_effectiveness` and seven category splits:
`attack_` · `defense_` · `status_` · `movement_technique_` · `healing_` · `support_` · `utility_skill_effectiveness`.

Two knobs per category: **cooldown = how often**, **effectiveness = how hard**. Taking only one half
gives builds that can get faster but never stronger.

### 3.4 Remaining (12)

| Group | Stats |
|---|---|
| Resource (2) | `resource_regeneration` · `resource_efficiency` |
| Social/economy (4) | `element_leadership_bonus` · `element_teaching_efficiency` · `element_crafting_efficiency` · `element_resource_discovery` |
| Perception (1) | `element_sensitivity` |
| Advanced (1) | `mastery_synergy_bonus` |
| Maps (3) | `element_mastery` · `element_interaction_bonuses` · `feature_flags` |

### 3.5 Designed but absent from the configs (10)

In `11_Advanced_Derived_Stats_Design.md` only — never made it into the per-element yaml, so they are
design sketches, not their shipped surface: `skill_resource_efficiency` · `skill_cast_time_reduction` ·
`mastery_experience_gain` · `mastery_decay_resistance` · `mastery_training_speed` ·
`element_movement_speed` · `element_teleportation` · `element_self_healing` · `element_group_healing` ·
`element_conversion`.

### 3.6 `element_mastery` is the input all of these read

Every formula in §3 is `base + element_mastery × k`. **That is the structural difference from us.**
Chaos gives each element its own progression counter per actor; we have one global scalar `Θ`. Porting
the channels without a per-element source means every value comes from gear only, and no element ever
varies by how much an actor has practised it.

---

## 4. Race Core "Tinh" — a second, unreconciled primary set

`race-core/00_Tinh_Primary_Stats_Design.md`. Note this **does not agree** with Actor Core §2's
STR/VIT/DEX/INT/SPI. Two primary-stat systems coexist in that repo and were never merged.

| Primary | Derived from it |
|---|---|
| `lifespan` | `max_lifespan` |
| `vitality` | `regen_rate` |
| `physical_foundation` | `base_hp` |
| `talent_foundation` | `learning_speed` |
| `bloodline` | `racial_bonus` |

Carries `RaceTinhBase` per race (human / dragon / **demon** bases already authored) and
`BreakthroughBonus` per realm. **If we ever adopt primary stats, pick one of these two sets — not both.**

---

## 5. Movement (`action-core/16`) — ~18 named + 9 maps

`movement_speed` · `movement_acceleration` · `movement_deceleration` · `movement_turn_rate` ·
`teleportation_ability` · `flight_ability` · `swimming_ability` · `climbing_ability` · `phase_ability` ·
`movement_restriction_resistance` · `movement_technique_mastery` · `_efficiency` · `_cooldown_reduction` ·
`technique_speed/distance/duration_multiplier` · `technique_resource_efficiency`, plus
`terrain_adaptation` / `weather_adaptation` / `elemental_movement_bonus` / `_resistance` maps.

Our equivalents are `turn.speed` · `turn.haste` · `turn.moveSpeed`
([DerivedTurnChannels.cs](../../src/FusionRpg.Core/Battle/Timeline/DerivedTurnChannels.cs)) and
`move.range` ([action-map.md:382](../architecture/action-map.md)) — **all four declared, none
registered, none with a reader.** Owned by the battle/action streams, not a gap in the stat catalog.

---

## 6. Resources — 14 types

`resource-manager/configs/enums.yaml` + `ResourceType::` usages:
`Health` · `HP` · `Mana` · `MP` · `Stamina` · `Qi` · `SpiritualEnergy` · `LifeForce` · `Lifespan` ·
`Vitality` · `Energy` · `Essence` · `Soul` · `Custom`, each with `_max` / `_current` / `_regen`.

We ship **five, deliberately** — `hp` · `stamina` · `hunger` · `spirit` · `qi`
([resource-hub-ssot.md](../architecture/resource-hub-ssot.md) §1). Their 14 is not a target; several
are synonyms (`Health`/`HP`, `Mana`/`MP`) that their own docs never reconciled.

Their exhaustion vocabulary is worth keeping: `disable_tags` · `damage_multiplier` · `set_flag` ·
`resource_multiplier` · `cooldown_multiplier` · `movement_speed_multiplier` · `cast_time_multiplier`.

---

## 7. What we deliberately do **not** port

| Chaos feature | Our decision | Where locked |
|---|---|---|
| Hard min/max clamps on every dimension | **Rejected** — PS-8, caps on magnitudes are progression ceilings | [ssot-power-scale.md §11](../architecture/power/ssot-power-scale.md) |
| Fixed `trigger_scale` sigmoid divisor | **Rejected** — we use dynamic `effectiveApplyScale` | [actor-hub-ssot.md §5](../architecture/actor-hub-ssot.md) |
| Realm multiplier `2^n` → 4096 | **Rejected** — geometric curve on a difference-based contest; `progression.realm` pinned 1.0 | ADR P1, [decisions.md](../architecture/decisions.md) |
| `log10(experience/1e6)×1000` power curve | **Rejected** — one ladder, `P(Θ)` triangular | [ssot-power-scale.md](../architecture/power/ssot-power-scale.md) |
| Per-element YAML/SQL loader | **Rejected** — code-first catalog, `data/seed/` registries | [status-ssot.md](../architecture/status-ssot.md) |
| 8 five-element roster (wood/metal/lightning/water) | **Different by design** — ours is a 4-ring + `light ⇄ dark` | [element-hub-ssot.md](../architecture/element-hub-ssot.md) |
| Social / economy / crafting / teaching stats | **Out of combat scope** — belongs to commander/map work | — |
| `f64` everywhere | **Rejected** for magnitudes — invariant 13, magnitudes are `long` | [DESIGN-GATE.md §2](../DESIGN-GATE.md) |

---

## 8. The gaps we accept as real (2026-08-24 owner review)

Ordered by effect on combat balance. Items 1–3 are the ones a session is most likely to miss.

1. **`status_duration` / `status_intensity` splits.** Our Phase-2 potency uses **one** `netFactor` for
   both magnitude and duration ([actor-hub-ssot.md §4](../architecture/actor-hub-ssot.md)) — "long but
   weak" and "short but brutal" are currently inexpressible.
2. **Skill effectiveness split** — the magnitude half of the cooldown/effectiveness pair.
3. **Healing has no stat at all.** Zero `heal*` channels in `src/`; `lifesteal` exists only as an atom
   and `leech`'s heal half was never built
   ([atom-catalog-ssot.md:113](../architecture/effect-atom/atom-catalog-ssot.md)). No anti-heal exists
   in Chaos either — `healing_power ↔ heal_reduction` would be ours.
4. `element_penetration ↔ absorption`, `element_amplification ↔ reduction`, `reflection_*` (8).
5. Parry (4) and Block (4) — subject to §3.2's shield-overlap warning.
6. `cooldown_reduction` per action category.
7. `resource.max` / `regen` / `efficiency` per resource.
8. `xp_rate`, `breakthrough_success`.
9. **`element_mastery`** — §3.6. Deferred with the primary-stat discussion.

---

## Related

- [../architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md) — channel registration, §3
- [../architecture/element-hub-ssot.md](../architecture/element-hub-ssot.md) — family list, §6
- [../architecture/combat-damage-ssot.md](../architecture/combat-damage-ssot.md) — §5 v1 inputs and the deferred list
- [../architecture/shield-system-spec.md](../architecture/shield-system-spec.md) — toughness/pen curve
- [actor-core-chaos-mapping.md](actor-core-chaos-mapping.md) · [status-core-chaos-mapping.md](status-core-chaos-mapping.md)
