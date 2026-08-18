# Chaos Actor Core → FusionRpg Actor Hub mapping

**Status:** Research / design reference only — not product ADR. Normative spec: [../architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md).

External design source (do not vendor code or YAML trees):

`chaos-backend-service/docs/actor-core/` and `docs/element-core/` on the author's machine.

---

## What we borrow

| Chaos concept | FusionRpg mapping |
|---|---|
| Derived stats layer separate from primary | **ActorDerivedSnapshot** via **DerivedComposer** — catalog channels in [actor-hub-ssot.md](../architecture/actor-hub-ssot.md) |
| Primary → derived mapping (vitality → resist, intelligence → prob) | Future power ADR; v1 uses **`progression.power`** stub + **`status.power.*` / `status.resist.*`** |
| **Omni additive-only** rule | `totalPower = omni + category + per-id` — never multiply omni × category |
| Element **mastery** / **power_scale** from experience | **`progression.power`** from `(player_id, kind, type_id)` level — future `PowerCurve(kind, level) × realm` |
| **Realm / breakthrough multipliers** | **`progression.realm`** stub — future `get_realm_multiplier()` analog |
| Logarithmic power growth | Reference curve for `UpdatePower` — not copied verbatim until power ADR |
| Buff/debuff affect derived only, never primary | Same ban: progression never mutates Y0 |
| Actor stat subsystem registry | **`IActorStatSubsystem`** — baseline, rpg.progression, pvz.stats, effect, cheat |

Key Chaos docs used:

- [element-core/08_Elemental_Mastery_System_Design.md](D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/08_Elemental_Mastery_System_Design.md) — `power_scale = base × level_bonus × tier_bonus × realm_bonus`
- [actor-core/21_Element_Core_Configuration_Examples.md](D:/Works/source/chaos-repositories/chaos-backend-service/docs/actor-core/21_Element_Core_Configuration_Examples.md) — `log10(experience/1e6)*1000`, realm multipliers 1→4096
- [element-core/06_Implementation_Notes.md](D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/06_Implementation_Notes.md) — Omni additive-only; primary → StatusProbability / StatusResistance
- [element-core/01_Probability_Mechanics_Design.md](D:/Works/source/chaos-repositories/chaos-backend-service/docs/element-core/01_Probability_Mechanics_Design.md) — sigmoid apply chance

---

## What we do not port

| Chaos feature | FusionRpg decision |
|---|---|
| Element Core SQL / YAML loader | **No** — code-first catalog + grant overlay |
| Per-element sigmoid config arrays | Map to **category** tags on Fusion status families (`dot`, `cc`, `contagion`) |
| Fixed **`trigger_scale`** / **`status_scaling_factor`** as product ApplyScale | **Fusion divergence:** dynamic **`effectiveApplyScale = K × avg(progression.power)`** — see [actor-hub-ssot.md §5](../architecture/actor-hub-ssot.md) |
| Element mastery per fire/water/wood SQL tables | Single **`progression.power`** per type actor until element system exists |
| Actor-core performance sharding | **Deferred** — lawn scale does not need it v1 |
| Persist derived snapshot as SSOT | **Ban** — recompute from subsystems + progression row |

---

## Level / realm → progression.power (future curve reference)

Chaos computes element mastery (power scale) roughly as:

```text
base_power_scale = max(1, log10(experience / 1_000_000) × 1000)
final_power_scale = base × level_bonus × tier_bonus × realm_bonus × element_modifier
final_power_scale = min(final, 1_000_000)
```

Realm breakthrough table (excerpt):

| Breakthroughs | Multiplier |
|---|---|
| 0 | 1 |
| 1 | 2 |
| 2 | 4 |
| 3 | 8 |
| … | 2^n |
| 12 | 4096 |

**Fusion mapping (when power ADR lands):**

```text
progression.power = PowerCurve(kind, level) × progression.realm
// PowerCurve: POC table or simplified linear — separate balance doc
// IProgressionPowerProvider.UpdatePower(ActorKey, level, realm?)
```

**v1 (Actor Hub code plan):** hardcode `progression.power = 1.0` for all actors. No level read until `UpdatePower` ships.

---

## Primary stat → derived (Chaos reference, not v1)

Chaos maps primary stats at level-up / equipment change:

```text
vitality      → DefensePoint (×2), StatusResistance (×0.5)
intelligence  → PowerPoint (×1.5), StatusProbability (×0.3)
```

Fusion does **not** ship primary attributes v1. When attrs arrive, they contribute via **catalog channels** only — not ad-hoc Unity fields. Likely mapping:

| Future primary | Catalog channel |
|---|---|
| Vitality analog | `status.resist.omni` or `progression.bonus.defense` |
| Intelligence analog | `status.power.omni` |

Document in power ADR — not Actor Hub v1.

---

## ApplyScale divergence (document explicitly)

| Question | Chaos | Fusion |
|---|---|---|
| What scales with level/realm? | **Power and resist magnitudes** in delta | **Same** — via `progression.power` in delta |
| What is the sigmoid divisor? | **Fixed** `trigger_scale` (~50–100) per element | **`max(Floor, K × avg(progression.power))`** |
| Why diverge? | High power stats + fixed scale still saturate sigmoid when delta is huge | Fusion self-normalizes apply sensitivity as tier rises — high-level fights need larger delta edge for guaranteed apply |

Chaos fixed scale works because delta and stats grow together without normalizing the divisor. Fusion chooses explicit match-tier normalization instead of copying fixed `trigger_scale`.

---

## Layer alignment

```text
Chaos ActorCore derived snapshot     →  FusionRpg ActorDerivedSnapshot
Chaos element_mastery / power_scale  →  progression.power (+ realm stub)
Chaos omni + element status stats    →  status.power.omni + status.power.{category}
Chaos status_resist arrays           →  status.resist.omni + status.resist.{category}
Chaos primary stat mapping           →  Deferred — power ADR
Chaos fixed trigger_scale            →  NOT ported — Fusion dynamic ApplyScale
```

---

## Related

- [../architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md)
- [status-core-chaos-mapping.md](status-core-chaos-mapping.md)
- [../architecture/status-ssot.md](../architecture/status-ssot.md)
- [../architecture/rpg-progression.md](../architecture/rpg-progression.md)
