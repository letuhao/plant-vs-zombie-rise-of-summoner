# Piece: `shield-layer-inspect`

**Program:** `gui-lego` · **Kind:** panel-slice  
**Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Draft:** `docs/design/gui-lego/pieces/shield-layer-inspect.html` — **must author before factory done**  
**Depends on:** `shield-stack-bar` selection, stack projection  
**Lock:** **D8** — cascade / apply-outcome **explicit P3 defer**

---

## Role

Inspect selected shield layer: hp/max, element, priority fiction, source fiction label, optional regen.

## Fields

| Field | Required | Notes |
|---|---|---|
| `piece` | yes | `shield-layer-inspect` |
| `instanceId` | yes | |
| `phase` | yes | |
| `title` | yes | fiction |
| `currentText` / `maxText` | yes | fold-formatted |
| `elementLabel` | no | |
| `priorityLabel` | yes | |
| `sourceLabel` | yes | fiction — not raw SourceId alone |
| `regenText` | no | if DTO has rate |
| `cascadeLines` | no | **P3 defer (D8)** — schema TBD when absorb inspect ships |
| `themeRef` | when typed | |

## Deferred (**D8**)

| Item | Until |
|---|---|
| Cascade remainder table | Server projects absorb cascade lines |
| Apply outcomes (DroppedWeaker / Rejected) | Event projection + toast/inspect honesty |

Do **not** recompute ShieldMath in FE.

## Success criteria

- [ ] Draft exists.
- [ ] No FE absorb math.
- [ ] Apply-outcome / cascade marked deferred, not silently “done.”

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/shield-layer-inspect.html
```

## Sample payload

```json
{
  "piece": "shield-layer-inspect",
  "instanceId": "shield:inspect:aura-1",
  "phase": "ready",
  "title": "Ice aura shield",
  "currentText": "40",
  "maxText": "40",
  "priorityLabel": "Aura",
  "sourceLabel": "From your pact",
  "themeRef": { "kind": "element", "id": "ice" }
}
```
