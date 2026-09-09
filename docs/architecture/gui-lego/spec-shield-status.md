# Piece: `shield-status`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Card (glance)  
**Map:** [../condition-glance-map.md](../condition-glance-map.md) · sibling [../shield-sheet-map.md](../shield-sheet-map.md)  
**Draft:** `docs/design/gui-lego/pieces/shield-status.html` — **must author before factory done**  
**Depends on:** [spec-theme-bind.md](spec-theme-bind.md),
[../condition-glance/spec-sheet-hot-projection.md](../condition-glance/spec-sheet-hot-projection.md)  
**DTO align:** `ActorShieldSummaryDto` (`elementId`, `current`, `max`; optional `stacks` if widened)  
**Home:** under `cond-hero`  
**Not a substitute for:** Shield tab `shield-stack-bar` (instance layers) — same runtime, different projection.

---

## Role

Glance card for current shield HP (+ stacks when DTO has them). Mounted **only when** shield is
present and meaningful (Q3).

## Omit rules (binding)

Do **not** mount when `shieldSummary` is `null` OR `(current ?? 0) <= 0`. No empty chrome.

## Structure

| | |
|---|---|
| Landmark / root | `.shield-status` / kit `.shield-card` |
| Slots | optional stack pips |
| Parent | `cond-hero` slot `shield` |

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"shield-status"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `current` / `max` | `long` wire | yes | From sheet DTO |
| `stacks` | `number` | no | Only if DTO widened |
| `elementId` | `string` | no | |
| `themeRef` / `themeResolved` | | when element | |
| `currentText` / `maxText` | `string` | yes | Fold-formatted |

## Dual display with radial

When this piece mounts, `pool-radial` may show a **secondary shield ring** (same summary). When
omitted, radial has **no** shield ring. Card is the primary glance; ring is secondary chrome.

## Success criteria

- [ ] Draft HTML exists.
- [ ] Cold sheet: no DOM node.
- [ ] Hot with shield: HP + element paint from paint SSOT / theme-bind.
- [ ] DTO fields align with sheet-hot-projection.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/shield-status.html
cd web\fusion-rpg-web; npm test -- --run shield-status
```

## Sample payload

```json
{
  "piece": "shield-status",
  "instanceId": "condition:shield",
  "phase": "ready",
  "current": 1200,
  "max": 2000,
  "elementId": "ice",
  "themeRef": { "kind": "element", "id": "ice" },
  "currentText": "1,200",
  "maxText": "2,000"
}
```
