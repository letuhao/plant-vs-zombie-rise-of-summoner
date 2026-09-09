# Piece: `stand-row`

**Program:** `gui-lego` · **Kind:** layout / host  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** [../../design/gui-lego/pieces/stand-row.html](../../design/gui-lego/pieces/stand-row.html)  
**Depends on:** [spec-standing-radar.md](spec-standing-radar.md), [spec-standing-bars.md](spec-standing-bars.md),
[spec-status-glyph-strip.md](spec-status-glyph-strip.md),
[../condition-glance/spec-fiction-copy.md](../condition-glance/spec-fiction-copy.md),
[../condition-glance/spec-condition-layout.md](../condition-glance/spec-condition-layout.md)

---

## Role

Standing host: radar + bars + optional status strip under `live`.

## Slots

| Slot | Child | Mount rule |
|---|---|---|
| `radar` | `standing-radar` | when standing ready |
| `live` / bars | `standing-bars` | when standing ready |
| `live` / status | `status-glyph-strip` | **only if** liveStatuses.length > 0 |

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"stand-row"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `title` | `string` | yes | Fiction from fiction-copy — **not** fold-local `definitions.md` |
| `revision` | `number` | yes | |

## Success criteria

- [ ] Fiction title only (owned by fiction-copy checklist).
- [ ] Column width ≥ radar chart (condition-layout).
- [ ] No status strip when empty.
- [ ] No shield-status here.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run stand-row
rg -n "definitions\.md" docs/design/gui-lego/pieces/stand-row.html web/fusion-rpg-web/src/features/gui-lego/foldConditionSurfaceVm.ts
```

## Sample payload

```json
{
  "piece": "stand-row",
  "instanceId": "condition:standing",
  "phase": "ready",
  "title": "Standing",
  "revision": 3
}
```
