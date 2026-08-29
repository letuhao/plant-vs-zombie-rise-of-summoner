# Spec — `skill-modifiers`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `catalog-extension` · **Parallel with:** the rest of the band
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Give actions two knobs per category — how often, and how hard — and stop a second program from
inventing the first one.**

Ten channels over five categories (`attack` · `defense` · `support` · `movement` · `status`):

| Family | Class | Meaning |
|---|---|---|
| `skill.cooldown.{category}` | **`Race`** | how often |
| `skill.effectiveness.{category}` | **`Feeder`** | how hard |

Shipping only the first gives builds that get faster but never stronger — the omission
[../research/chaos-derived-stats-audit.md](../research/chaos-derived-stats-audit.md) §8.2 names.

### 1.1 This module exists partly to prevent a duplicate

[action-map.md:177](../action-map.md) records:

> *"Our envelope has `SpeedChannel` but **no bounds and no cooldown-reduction channel** — a real gap
> this program should close."*

and D3 (line 200) schedules adding one to `ActionEnvelope`. **`skill.cooldown.{category}` is that
channel.** Two programs answering one question with two mechanisms is the exact failure the power
ladder was written to end — three incompatible level curves shipped simultaneously that way. D3 gets
pointed at the catalog; the envelope gains a `CooldownChannel` *reference*, mirroring the
`SpeedChannel` it already has, not a second channel of its own.

---

## 2. Why neither needs a counterpart

Both answers come from [spec-stat-taxonomy.md](spec-stat-taxonomy.md), and neither is an exemption.

**`skill.cooldown.*` is `Race`.** Both actors want it lower; advantage is being ahead. The opponent's
own cooldown *is* the counter. A "lengthen the enemy's cooldown" channel would be a stat whose only
job is to debuff — which is a **status**, and shipping it as a channel creates a second way to say
slow.

**`skill.effectiveness.*` is `Feeder`, and its placement is its pair.** It scales
`baseOverlayDamage` **before** the power/defense delta ([combat-damage-ssot.md](../combat-damage-ssot.md)
§6.7), so `combat.defense` already answers it:

```text
baseDamage          = request.baseOverlayDamage × skill.effectiveness.{category}   ← HERE
powerAdjustedDamage = baseDamage + weightedDelta                                    ← defense answers it
```

> **Pinning it pre-mitigation is a contract, not an implementation detail.** Move it after mitigation
> and it becomes `Contest` and owes a `.reduction` half — exactly the obligation `crit.damage` carries
> for landing after the delta. Relocating it later is a **breaking change**, not a refactor, and the
> spec says so where an implementer will read it.

**All five categories are `Feeder`** — one class for the family, which is what the registry and the
seed catalog can express.

An earlier draft claimed `support` and `movement` "feed nothing" and should therefore be `Pool`. **That
was wrong** (owner, 2026-08-24). Buff, debuff, heal and movement magnitudes all meet real opposition —
in the world-map layer, the web battle area, and partly on the PvZ lawn. What is true is narrower and
does not change the class:

> **Those downstream contests are not designed yet.** The action system is still being specified, and
> it — not this module — decides what answers a support or movement magnitude. `Feeder` is the correct
> class the moment those contests exist, and nothing here has to change when they do.

Worth knowing while reading the taxonomy: **`guard-stat-pairs.ps1` cannot distinguish `Feeder` from
`Pool`** — both mean "no counterpart required". The distinction is documentation, so a family that is
`Feeder` in three categories and arguably `Pool` in two has no mechanical consequence either way.

---

## 3. The zero-duration floor — already identified, now it has a rule

[action-map.md:177](../action-map.md) asks for *"`min`/`base`/`max` bounds so a stat cannot drive a
duration to zero."* That is [spec-stat-taxonomy.md](spec-stat-taxonomy.md) §2.4's divisor rule,
arrived at independently by a different program:

> A `Race` stat used as a divisor requires a floor above zero. That floor is a **structural limit** —
> a zero-tick cooldown is an infinite action loop, not a balance outcome — so it is **PS-8 exempt**
> and must say so in a comment.

**This floor is not a progression ceiling and must not be written as one.** Cooldown *reduction* stays
uncapped; what is bounded is the resulting **duration**, at one tick. The difference matters: capping
the reduction walls the grind, while flooring the duration only refuses division by zero.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Skill|FullyQualifiedName~ActionEnvelope"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-stat-pairs.ps1
python scripts\audit-magic-numbers.py --summary
```

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Battle/Timeline/ActionEnvelope.cs` | `CooldownChannel` — a **reference**, mirroring `SpeedChannel`; no new channel |
| `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs` | apply `effectiveness` to `baseOverlayDamage` **before** the delta |
| `docs/architecture/action-map.md` | **D3 repointed** at the catalog (§1.1); §177's gap marked closed |
| `data/tuning/battle.v1.json` | the structural one-tick floor, with its PS-8 exemption comment |

---

## 6. Testing strategy

| Test | Asserts |
|---|---|
| **`NoGoldensMoveAtDefaults`** | All ten at `0` → identical. `effectiveness` defaults to a **1.0 multiplier**, `cooldown` to zero reduction |
| `EffectivenessIsPreMitigation` | Raising `effectiveness` and raising `defense` cancel — the proof it inherits its pair |
| `EffectivenessCannotBypassDefense` | High effectiveness vs high defense still floors at `0` damage. **This is the test that would fail if someone moved it post-mitigation** |
| `CooldownFloorsAtOneTick` | Arbitrarily large reduction never yields `0` |
| `CooldownReductionUncapped` | The *reduction* has no ceiling — only the duration does (§3) |
| `NoCooldownCounterpartRegistered` | `Race` forbids a pair; the taxonomy guard enforces it |
| `EnvelopeReferencesCatalog` | `CooldownChannel` resolves to a registered id — no envelope-local channel |

`EffectivenessCannotBypassDefense` is the load-bearing one: it is the executable form of §2's contract.

---

## 7. Boundaries

**Always** — apply `effectiveness` pre-mitigation. Reference catalog channels from the envelope, never
declare one there. Comment the one-tick floor as structural.

**Ask first** — a sixth action category. Five came from the external inventory and cover the shipped
atom triggers; a sixth probably means a miscategorised action.

**Never** — ship `skill.cooldown.increase` or `skill.effectiveness.reduction` (§2 — those are statuses).
Cap cooldown *reduction* (§3). Move `effectiveness` after mitigation without adding its `.reduction`
half in the same change.

---

## 8. Success criteria

- [ ] Ten channels live; `cooldown` classified `Race`, `effectiveness` `Feeder`.
- [ ] `ActionEnvelope.CooldownChannel` **references** the catalog; **D3 repointed and `action-map.md:177` marked closed**.
- [ ] `effectiveness` provably pre-mitigation — `EffectivenessCannotBypassDefense` green.
- [ ] Cooldown floors at one tick, structural, commented; reduction uncapped.
- [ ] `git status tests/` clean at defaults.
- [ ] `guard-stat-pairs.ps1` green — no `Race` counterpart exists.

---

## 9. Open questions

**None.** The `Feeder`/`Pool` question this spec previously carried was **closed by the owner
2026-08-24** (§2): all five categories are `Feeder`, because support and movement magnitudes do meet
real opposition — those contests are merely undesigned, and designing them is the action system's
work, not a reason to reclassify the channel.
