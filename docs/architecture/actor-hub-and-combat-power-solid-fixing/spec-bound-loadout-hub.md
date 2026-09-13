# Spec: `bound-loadout-hub`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-bound-loadout · Wrong use Writer abs beside Hub  
**Wave:** 3 (depends on `lawn-aptitude-parity`)  
**Code anchors:** `UniqueBoundLoadout.TryApply` — `EntityStatWriter` ForceSet abs + Funnel grants · `EntityApply` Hub resolve · Funnel delta rules · DESIGN-GATE combat deltas

---

## Objective

Bound UniqueActor loadout combat must land through **ActorHub contributions + legal Funnel deltas**, not **Writer absolute** HP/ATK beside Hub (`UniqueBoundLoadout.ApplyAbsolutes`). Absolute Writer on Bound is **wrong use** of EntityStatWriter relative to sole Hot compose.

Success: Bound loadout grants are Hub atoms / EffectBag modifiers with `entity:{ptr}`; no `ForceSet*` combat abs from loadout JSON for atk/hp/maxHp that bypass Hub; lawn Hub Derived reflects loadout; Funnel remains delta-only for HP.

---

## Tech stack

- Injector: `UniqueBoundLoadout.cs`, EntityApply, Funnel
- Core/Data: loadout spec parse → atom/grant projection (align `cold-equip-one` / effect bindings where overlap)
- DESIGN-GATE: deltas not absolutes for overlay HP

---

## Commands

```powershell
dotnet test tests/FusionRpg.Injector.Tests --filter "FullyQualifiedName~UniqueBound|Loadout" 
# if project exists; else Core + Guard
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-actor-hub.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| `UniqueBoundLoadout.cs` | Remove abs Writer path for combat; keep ptr-scoped grants via Funnel/Hub |
| Loadout → atoms | Project durable loadout into Hub-readable contributions |
| Spawn/bind order | Apply loadout **before/with** Hub resolve so Writer sees composed AppliedCombat |
| Tests | Bound loadout changes Hub-derived atk/hp inputs without ForceSet |

### Serious gaps

1. Map each loadout absolute key to Hub channel or Funnel grant — no silent drop.
2. HP: use Funnel Add / preserve-ratio apply — **not** `mode=set` current HP.
3. Do not reintroduce type-wide `plant:N` keys (already banned).

---

## Code style

- Prefer delete `ApplyAbsolutes` over wrapping it.
- Comment any transitional shim as DEBT with delete milestone.

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Loadout with atk grant → Hub/AppliedCombat; no ForceSetAtk |
| Unit | HP grant via Funnel delta path |
| Guard | single-writer + funnel-delta green |
| Live | Bound unique with loadout shows Hub-consistent stats |

---

## Boundaries

- **Always:** Hub + Funnel; deltas for HP; ptr-scoped only.
- **Ask first:** Expanding loadout schema.
- **Never:** Absolute Writer as loadout SSOT; type-wide loadout keys; BattleStatComposer.

---

## Success criteria

- [ ] `ApplyAbsolutes` combat path removed or reduced to non-combat-safe leftovers with owner sign-off.
- [ ] Bound loadout visible on Hub Derived / AppliedCombat.
- [ ] Funnel + single-writer guards green.
- [ ] HF-bound-loadout ticked on ideal register.
