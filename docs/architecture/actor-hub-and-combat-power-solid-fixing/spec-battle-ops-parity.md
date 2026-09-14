# Spec: `battle-ops-parity`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-battle-ops · HF-battle-tree · AtomKind Lawn/Battle Full vs Partial lie  
**Wave:** 1 (after `battle-hub-fuse`)  
**Code anchors:** `EquipAtomSource.ModsFor` (ignores op) vs `DerivedAtomsFor` (honours `TryParseOp`) · `Battle.TreeAtomSource` (unused in Compose) · `AtomKindRegistry` `stat.derived` Battle **Full** · post-fuse Hub equip/tree readers

---

## Objective

After fuse, battle must not silently **downgrade** equip ops to flat-add, and must not leave a **dead** `TreeAtomSource` “third Compose slot.” Equip and tree combat grants use the **same op semantics and Hub atom path** as sheet — AtomKind matrix claim **Full** for Battle must be true in code.

Success: Increased/More/Replace (and shipped ops) on equipped `stat.derived` affect battle Hub Derived like sheet; tree bound atoms for battle actors contribute via Hub; `Battle.TreeAtomSource` deleted or reduced to a thin Hub adapter with a production caller — no orphan API.

---

## Tech stack

- Core: `AtomDerivedSubsystem.TryParseOp` · `EquippedBoundAtoms` · PassiveTree bound atoms / `BoundAtomsFor`
- Delete or repurpose `FusionRpg.Core/Battle/TreeAtomSource.cs` and `EquipAtomSource.ModsFor` battle-only flat path once Hub-only
- Tests: `EquipRuntimeTests`, `TreeAtomSourceParityTests` → Hub parity

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~Equip|TreeAtom|AtomDerived|Battle"
.\scripts\guard-actor-hub.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| Equip path | Battle consumes Hub atoms — remove `ModsFor` ignore-op fold |
| Tree path | Wire lawn/battle tree bindings into Hub; delete unused `TreeAtomSource` Compose slot |
| AtomKind docs | Battle `stat.derived` Full matches runtime |
| Tests | Op parity: same atom → same Derived on sheet helper and battle Hub snapshot |

---

## Code style

- One parse (`EquippedDerived` / shared atom walk) — no battle-special coerce-to-flat.
- Prefer delete dead battle helpers over “document Partial forever.”

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Flat / Increased / Replace (shipped ops) parity Hub sheet vs battle actor |
| Unit | Tree node atom raises same channel in battle Hub when bindings present |
| Negative | Unknown op skipped visibly — not coerced |
| Guard | No revival of `BattleChannelMod` equip fold |

---

## Boundaries

- **Always:** Ops honored; tree via Hub; Full means Full.
- **Ask first:** Widening AtomKind op vocabulary.
- **Never:** Reintroduce BattleStatComposer for “ops are hard”; leave TreeAtomSource unused as fake completeness.

---

## Success criteria

- [ ] Battle equip path uses Hub op-aware atoms only.
- [ ] Tree contributions reach battle actors via Hub when bindings exist.
- [ ] `Battle.TreeAtomSource` unused Compose slot gone (deleted or single Hub adapter with caller).
- [ ] AtomKind Battle Full for `stat.derived` matches tests.
- [ ] No Partial lie in battle equip comments without an owned follow-up.
