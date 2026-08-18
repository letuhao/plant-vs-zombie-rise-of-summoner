# Combat overlay grant examples

Documentation JSON samples for [combat-damage-ssot.md](../combat-damage-ssot.md). Overlay grants use seed effect `fx.overlay_damage` plus `target` / `delivery` overlay keys.

| File | Demonstrates |
|---|---|
| [instant-event-target.json](instant-event-target.json) | `Instant` + `EventTarget` |
| [heal-positive-amount.json](heal-positive-amount.json) | Heal as positive signed amount (same pipeline) |
| [area-row.json](area-row.json) | `Area` shape `Row` |
| [area-square-default.json](area-square-default.json) | `Square` with policy default size |
| [random-multi.json](random-multi.json) | `Random` + type/row filters |
| [counter-scope-target.json](counter-scope-target.json) | Counter meter per target |
| [counter-scope-actor.json](counter-scope-actor.json) | Counter meter per actor |
| [dot-overtime.json](dot-overtime.json) | `OverTime` scheduler ticks (armed on hit) |

Grant bodies match the shape passed to `POST /api/debug/effect/grant` overlay fields (merge with `effectId` / `ownerKey` as shown). Seed id is `fx.overlay_damage`. OverTime is armed on the triggering hit; ticks come from the injector ~100ms scheduler, not a def `OnTimer` trigger. Use `icd_ms: 0` for hit-streak Counter proves (default ICD is 250ms).

**Forward (Status SSOT):** After StatusRuntime ships, prefer `statusId` + status overlay instead of `delivery.mode = OverTime|Counter`. Migration shapes: [../status/](../status/).
