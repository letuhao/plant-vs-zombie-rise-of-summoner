# phaser-kernel — module specs index

**Program map:** [../phaser-kernel-map.md](../phaser-kernel-map.md)  
**Ideal:** [../phaser-kernel-ideal.md](../phaser-kernel-ideal.md)  
**Plan / todo:** [../../tasks/phaser-kernel-plan.md](../../tasks/phaser-kernel-plan.md) ·
[../../tasks/phaser-kernel-todo.md](../../tasks/phaser-kernel-todo.md)

| Module | Wave | Spec |
|---|---|---|
| `lock-doc-sync` | 0 | [spec-lock-doc-sync.md](spec-lock-doc-sync.md) |
| `destroy-game` | 1 | [spec-destroy-game.md](spec-destroy-game.md) |
| `stage-bus` | 1 | [spec-stage-bus.md](spec-stage-bus.md) |
| `island-host` | 1 | [spec-island-host.md](spec-island-host.md) |
| `host-data-lifecycle` | 1 | [spec-host-data-lifecycle.md](spec-host-data-lifecycle.md) |
| `board-contract` | 1b | [spec-board-contract.md](spec-board-contract.md) |
| `lawn-plane` | 1b | [spec-lawn-plane.md](spec-lawn-plane.md) |
| `lawn-paint` | 2+ deferred | [spec-lawn-paint.md](spec-lawn-paint.md) |
| `focus-input` | 2+ deferred | [spec-focus-input.md](spec-focus-input.md) |
| `cell-boot` | 2+ deferred | [spec-cell-boot.md](spec-cell-boot.md) |
| `fx-facade` | 2+ deferred | [spec-fx-facade.md](spec-fx-facade.md) |
| `retire-clones` | R deferred | [spec-retire-clones.md](spec-retire-clones.md) |

**Freeze (lock 5a):** after Wave 1 + `board-contract` + `host-data-lifecycle` PASS on production
hosts, base-defense may unpause siege/battle against the frozen import list.
