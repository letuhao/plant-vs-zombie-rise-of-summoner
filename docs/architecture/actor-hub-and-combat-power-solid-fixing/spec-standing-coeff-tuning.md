# Spec: `standing-coeff-tuning`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** D4 coeff honesty · finding 5 · tunables-ssot  
**Wave:** 4 (after `standing-compose`)  
**Code anchors:** `data/seed/power/coefficients.v1.json` · `ActorPowerCache` / E9 `CostFunction` · kind categories O\|S\|C trisect · `RpgStore.Power`

---

## Objective

Standing / combat-power **pricing** must stop treating every `stat.derived` channel as undifferentiated $/point with dodge trisected across Offense/Survivability/Control. Ship **per-family (or category-mask) coefficients** in existing power tuning tables so high dodge reads **Survivability-weighted** while still raising total combat power via magnitude × coeff.

**Not** a second level→power ladder. Contests still Θ; magnitudes still `P(Θ)` where ladder applies.

Success: tuning file(s) express family/mask weights; dodge-heavy fixture Standing Survivability > Offense share vs undifferentiated baseline; balance pass edits JSON not Policy consts.

---

## Tech stack

- Seed/DB: `coefficients.v1` (or successor version) + load path already used by E9
- Core: CoefficientTable / CostFunction consumers of Standing Compose
- Tunables: [tunables-ssot.md](../tunables-ssot.md) — no bare literals on balance surface

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorPower|Coefficient|Standing"
python scripts/audit-magic-numbers.py --summary
```

---

## Project structure

| Path | Duty |
|---|---|
| `data/seed/power/coefficients*.json` | Per-family or category-mask rows for combat channels |
| Host load / RpgStore.Power | Version bump if schema widens |
| Compose / CostFunction | Read masks; default safe fallback documented |
| Tests | Dodge-weighted Survivability fixture |

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Same magnitudes, new coeffs → Survivability share rises for dodge family |
| Unit | Missing coeff row → load reject or documented structural default (T5) |
| Golden | Standing vector fixtures re-blessed once if needed |

---

## Boundaries

- **Always:** Config not code; one ladder; membership filter unchanged.
- **Ask first:** Full ~196 authored table vs family-level masks (default: **family/mask first**, not 196 one-off rows unless needed).
- **Never:** `f(level)` combat power; Policy bare coeff literals.

---

## Success criteria

- [ ] Tunable family/mask coeffs live and loaded.
- [ ] Product “high dodge → high combat power” still holds; axis identity improved for Survivability.
- [ ] Magic-number audit clean on new Policy surfaces.
