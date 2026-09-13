# Spec: `stale-compose-docs`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** finding 9 · SOLID · dual-compose overturn  
**Wave:** 4 (after `battle-hub-fuse` lands in code — docs can draft earlier, Done when fuse Done)  
**Code anchors:** `class-system-map` “composers stay separate” · `EquipAtomSource` / ProveAptitude / passive-tree battle specs · `actor-hub-ssot` §8.3 · `decisions.md` ActorHub sole Hot · BattleStatComposer header

---

## Objective

After dual compose is retired (or as fuse ships), **overturn leftover prose** that still treats Hub vs `BattleStatComposer` as intentional SSOT, “adapters OK forever,” or “locked separate.” Debt language → **retired** or **historical**.

Success: ripgrep for `composers stay separate`, `locked separate from ActorHub`, `BattleStatComposer stays` in `docs/` and production comments returns only historical/strikethrough or map “retired” notes; §8.3 marks debt closed when fuse Done.

---

## Tech stack

- Docs under `docs/architecture/` · code XML comments · research notes may stay historical with date

---

## Commands

```powershell
rg -n "composers stay separate|locked separate from ActorHub|BattleStatComposer stays|adapters OK" docs src --glob "!**/bin/**" --glob "!**/obj/**"
```

---

## Project structure

| Path | Duty |
|---|---|
| class-system / passive-tree / effect-atom specs | Amend dual-compose blessing |
| `actor-hub-ssot` §8.3 | Debt → retired when fuse Done |
| `decisions.md` | Cross-link fuse Done |
| Code comments | Align with debt/retired |
| This program map | Checklist tick |

---

## Testing strategy

| Level | Cases |
|---|---|
| Script assert | Guard or CI grep allowlist empty for blessing phrases (optional) |
| Manual | rg clean except `tasks/` history / research dated notes |

---

## Boundaries

- **Always:** Propagate corrections (DESIGN-GATE evidence rule 6).
- **Ask first:** Deleting whole historical research files.
- **Never:** Re-introduce “by ADR forever” dual compose.

---

## Success criteria

- [ ] Blessing phrases gone or clearly historical.
- [ ] §8.3 / decisions reflect fuse outcome.
- [ ] Map Done checkbox for stale docs.
