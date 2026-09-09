# gui-lego-condition — plan

**Program:** `gui-lego`  
**Queue row:** [docs/architecture/gui-lego/menu-refactor-queue.md](../docs/architecture/gui-lego/menu-refactor-queue.md) P1  
**Specs:** [docs/architecture/actor-sheet/spec-condition-tab.md](../docs/architecture/actor-sheet/spec-condition-tab.md) ·
[docs/architecture/actor-sheet/spec-actor-sheet-shell.md](../docs/architecture/actor-sheet/spec-actor-sheet-shell.md) ·
[docs/architecture/gui-lego/spec-condition-surface-vm.md](../docs/architecture/gui-lego/spec-condition-surface-vm.md)

## Goal

Refactor ActorSheet Condition into a GUI Lego surface with:

- shell-owned essential identity once on the left rail
- Condition-owned progression gauge + current-state glance on the right
- backend support via the existing `/api/actors/{id}/sheet` fan-in DTO

## Delivery slices

1. Docs/spec alignment for Condition grammar, shell identity ownership, and `/sheet` backend seam.
2. Backend `/sheet` contract extension for identity/progression/current-state payloads.
3. Condition design pack: piece HTML, recipe JSON, assembled surface.
4. FE fold/bus + piece registration + thin `ConditionTab`.
5. Enriched `ActorSummarize` consuming `/sheet` identity.
6. Tests + deploy proof.
