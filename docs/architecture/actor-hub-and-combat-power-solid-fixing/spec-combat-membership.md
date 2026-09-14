# Spec: `combat-membership`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** Q5 full membership · D2 filter · findings 4–6  
**Wave:** 2 (after `battle-hub-fuse`)  
**Code anchors:** `DerivedStatChannels.AllCombatChannelIds` / `IsCombatChannel` · `SkillCooldownPrefix` / effectiveness · `StatusPower*` / `status.resist.*` registry · `ActorPowerCache.Compose`

---

## Objective

Ship one **closed membership predicate** used by Standing (and any future combat-power price): which derived channel totals raise the combat power matrix.

**Include (locked):**
- Every id in `AllCombatChannelIds` / `IsCombatChannel` (~196 element-typed combat)
- Every registered `skill.cooldown.*` and `skill.effectiveness.*`
- Every registered `status.power.*` and `status.resist.*` that combat Apply / potency already reads (category + open prefix forms that are in `DerivedStatRegistry`)

**Exclude (locked):**
- All `progression.*` (especially `progression.power` / Θ)
- Future loot / magic-find / XP-bonus class channels
- `resource.max.*` / `resource.regen.*` (pools ≠ combat-power Standing)

Success: a single Core helper (e.g. `CombatPowerMembership.Includes(channelId)`) with tests; Standing and copy surfaces call it — no ad-hoc `IsCombatChannel`-only filters.

---

## Tech stack

- Core: `FusionRpg.Core` Stats/Derived (new membership type next to `DerivedStatChannels`)
- No new ladder; undifferentiated coeffs are Wave 2 OK — **D4 axis weights** owned by `standing-coeff-tuning` (Wave 4)

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CombatPowerMembership|IsCombatChannel|Standing"
```

---

## Project structure

| Path | Duty |
|---|---|
| New `CombatPowerMembership` (name flexible) | `Includes(string channelId)` + `Filter(IEnumerable)` |
| Tests | Include/exclude tables; roster swap with ElementTable still works |
| Callers | Only `standing-compose` in Wave 2; chip/copy must not invent a second filter |

### Status potency detail

- Include registry-backed category channels (`status.power.omni|dot|cc|contagion` and resist counterparts).
- Include open-prefix `status.power.{id}` / `status.resist.{id}` **when registered or accepted by registry open-prefix rules** used at Apply.
- Do **not** invent unaffiliated status ids. Prefer “whatever Apply can read today” over a hand-curated subset.

### Future loot / magic-find / XP-bonus class

When those derived families are registered, they **must stay excluded** from this predicate (product: non-combat). Do **not** invent the families in this module — only keep the exclude rule. Adding a family without updating this exclude list is a defect.

---

## Code style

- Predicate must not allocate per hot call beyond HashSet lookup if cached.
- Document exclude list in XML next to the type (SSOT comment).

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Every `AllCombatChannelIds` → true |
| Unit | `skill.cooldown.*` / `skill.effectiveness.*` samples → true |
| Unit | `status.power.omni` / `status.resist.omni` → true |
| Unit | `progression.power`, `resource.max.hp` → false |
| Unit | After ElementTable swap, new element combat ids → true |

---

## Boundaries

- **Always:** Full Q5 lock; exclude progression/resource/loot class; one helper.
- **Ask first:** Adding loot/MF families later (must stay excluded from this predicate).
- **Never:** Standing via naive Hub snapshot; `IsCombatChannel` alone as Standing filter.

---

## Success criteria

- [ ] `CombatPowerMembership` (or named peer) shipped with include/exclude tests.
- [ ] No Standing code path uses `IsCombatChannel` alone.
- [ ] Documented list matches map assumption §7.
