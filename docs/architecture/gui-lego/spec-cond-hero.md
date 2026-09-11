# Piece: `cond-hero`

**Program:** `gui-lego` · **Kind:** layout / host  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** [../../design/gui-lego/pieces/cond-hero.html](../../design/gui-lego/pieces/cond-hero.html)  
**Depends on:** [spec-pool-radial.md](spec-pool-radial.md), [spec-pool-meter.md](spec-pool-meter.md),
[spec-shield-status.md](spec-shield-status.md)

---

## Role

Vitality host: `pool-radial`, conditional `shield-status`, `pool-meter*`.

## Slots

| Slot | Child | Mount rule |
|---|---|---|
| `radial` | `pool-radial` | when pools ready |
| `shield` | `shield-status` | **only if** shield mountable (Q3) |
| `meters` | `pool-meter[]` | six pools when ready |

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"cond-hero"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `revision` | `number` | yes | passed through children |

## Dual shield chrome

When `shield-status` mounts: card under radial **and** optional secondary ring on `pool-radial`.
When omitted: neither card nor ring.

## Success criteria

- [ ] Landmark `data-grid-area="hero"`.
- [ ] No shield slot DOM when cold null.
- [ ] Draft shows shield slot placeholder for design review.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run cond-hero
```

## Sample payload

```json
{
  "piece": "cond-hero",
  "instanceId": "condition:hero",
  "phase": "ready",
  "revision": 3
}
```

## Boundaries

- Shield home is **here**, not stand-row.
