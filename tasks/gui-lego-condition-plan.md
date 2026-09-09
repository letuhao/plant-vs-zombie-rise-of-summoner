# gui-lego-condition — plan

**Program:** `gui-lego`  
**Queue row:** [docs/architecture/gui-lego/menu-refactor-queue.md](../docs/architecture/gui-lego/menu-refactor-queue.md) P1  
**Specs:** [docs/architecture/actor-sheet/spec-condition-tab.md](../docs/architecture/actor-sheet/spec-condition-tab.md) ·
[docs/architecture/actor-sheet/spec-actor-sheet-shell.md](../docs/architecture/actor-sheet/spec-actor-sheet-shell.md) ·
[docs/architecture/gui-lego/spec-condition-surface-vm.md](../docs/architecture/gui-lego/spec-condition-surface-vm.md)

## Goal

Refactor ActorSheet Condition into a GUI Lego surface with:

- shell rail = portrait + name + `Lv · role` only (no chips)
- Condition 2×2 CSS grid: progression \| species identity / vitality \| Standing
- `/sheet` projects `standing` (`PowerVector`) + six `resourcePools`; cold liveStatuses honest empty

## Delivery slices

1. Docs/spec alignment for Condition grammar, shell identity ownership, and `/sheet` backend seam.
2. Backend `/sheet` Standing + ResourcePools (+ identity/progression).
3. Condition design pack: piece HTML, recipe JSON, assembled **grid** surface.
4. FE fold/bus + piece registration + thin `ConditionTab` consuming standing/pools.
5. Slim `ActorSummarize` (no species/element chips).
6. Tests + `deploy-play.ps1 -NoServer -NoGame`.
