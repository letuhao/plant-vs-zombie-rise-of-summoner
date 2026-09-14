# Spec: `stale-doc-amend`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal lock:** **D5**

---

## Objective

Overturn in-repo docs that still teach “commander-scope v1 / UniqueCreature out of scope” so Done cannot
contradict the map. Wave 0 — docs only; no behavior change required in this module alone.

---

## Commands

```powershell
# Review-only — no tests required beyond doc links resolve
rg -n "commander-scope|commander scope only|UniqueCreature.*out of scope" docs/
```

---

## Project structure — amend in place

| Doc | Required amend |
|---|---|
| [../actor-sheet/spec-aptitudes-tab.md](../actor-sheet/spec-aptitudes-tab.md) | Point to aptitude-sheet map; UniqueActor → UniqueCreature; commander role → Mode C |
| [../class-system/spec-aptitude-allocation-surface.md](../class-system/spec-aptitude-allocation-surface.md) | Specimen picker = ActorSheet; UniqueCreature in scope via aptitude-sheet |
| [../../guide/mechanisms/aptitudes.md](../../guide/mechanisms/aptitudes.md) (+ html/json siblings if present) | Stop “other scopes still ahead” for UniqueCreature/species player surfaces |
| [../gui-lego/menu-refactor-queue.md](../gui-lego/menu-refactor-queue.md) | P4 Aptitudes claimed by `aptitude-sheet` stream |
| [../aptitude-sheet-ideal.md](../aptitude-sheet-ideal.md) | Hand-off points at map/specs (idea supersession note) |

---

## Boundaries

- **Always:** Link to map as index; do not silently delete historical rationale — mark superseded.
- **Never:** Re-litigate D1–D5 inside these amends.

---

## Success criteria

- [ ] `rg` for commander-only v1 claims on aptitude player surface returns zero active (non-struck) claims.
- [ ] Queue shows Aptitudes owned by `aptitude-sheet`.
- [ ] Ideal hand-off references map + spec folder.
