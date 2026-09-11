# Piece: `actor-identity`

**Program:** `gui-lego` · **Kind:** chrome / host  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** [../../design/gui-lego/pieces/actor-identity.html](../../design/gui-lego/pieces/actor-identity.html)  
**Depends on:** [spec-element-badge.md](spec-element-badge.md), [spec-phase-badge.md](spec-phase-badge.md),
[../condition-glance/spec-fiction-copy.md](../condition-glance/spec-fiction-copy.md)

---

## Role

Condition identity: portrait/type, species line, **phase-badge**, **element-badge***. Rail owns
name / Lv / role.

## Structure

| | |
|---|---|
| Landmark | `.actor-identity` / `data-grid-area="identity"` |
| Slots | `phase` → phase-badge; `elements` → element-badge* |

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"actor-identity"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | lifecycle |
| `speciesName` | `string` \| null | | |
| `speciesMessage` | `string` \| null | | fiction when species missing — **keep block mounted** |
| `typeIcon` | | | TypeIcon OK |

## Ban

Mute `<span className="chip">` for elements/phase. No role badge.

## Success criteria

- [ ] Elements render `element-badge` with paint SSOT.
- [ ] Phase uses `phase-badge` or omit.
- [ ] Species missing → `speciesMessage` fiction; identity stays.
- [ ] Zero mute chip twins in DOM tests.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run actor-identity
```

## Sample payload

```json
{
  "piece": "actor-identity",
  "instanceId": "condition:identity",
  "phase": "ready",
  "speciesName": null,
  "speciesMessage": "Species unknown"
}
```
