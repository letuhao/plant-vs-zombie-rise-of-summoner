# Plan: phaser-scene-poc

Source: approved Cursor plan “Phaser scene-switch POC” (2026-09-06).  
Task list: [phaser-scene-poc-todo.md](phaser-scene-poc-todo.md).  
Research protocol + results: [../docs/research/phaser-scene-switch-poc-2026-09-06.md](../docs/research/phaser-scene-switch-poc-2026-09-06.md).

Related (read-only context, not this program’s `/spec`):  
[phaser-kernel-ideal.md](../docs/architecture/phaser-kernel-ideal.md) ·  
[fe-phaser-architecture-audit.md](../docs/architecture/fe-phaser-architecture-audit.md).

---

## Shape

One measurement program: prove Phaser 4.2 multi-Scene switching vs Game destroy/create under React chrome **before** clearing kernel open questions.

```text
Docs protocol → POC scenes + Track A/B → React chrome + metrics → Run matrix → Owner locks open Qs
```

Out of scope: `phaser-kernel` `/spec`, siege/battle unpause, BoardLayers production flip, “everything in Phaser” unless Fail bar trips.

---

## Decisions for this POC only

- Track A = one `Phaser.Game`, `scene.switch` between World/Siege/Lawn fake scenes.
- Track B = destroy → wait DESTROY → `createGame` per mode.
- Surface = developer tree tab `phaser-scene-poc` (`?dev=phaser-scene-poc`), not a player stage.
- Dual-plane stress = React panel over Track A must not recreate Game.

---

## Delivery

See todo. After Pass/Fail is filled in the research doc, owner clears open questions in the ideal (separate edit).
