# Piece: `shield-layer-inspect`

**Program:** `gui-lego` · **Kind:** panel-slice  
**Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Draft:** `docs/design/gui-lego/pieces/shield-layer-inspect.html` — **must author before factory done**  
**Depends on:** `shield-stack-bar` selection, stack projection  
**Design:** cascade readout — **server sends numbers; UI does not recompute remainder**

---

## Role

Inspect selected shield layer: hp/max, element, priority fiction, source fiction label, optional
regen, optional last-absorb cascade lines when Hot provides them.

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
| `cascadeLines` | no | only if server projected |
| `themeRef` | when typed | |

## Success criteria

- [ ] Draft exists.
- [ ] No FE absorb math.
- [ ] Apply-outcome honesty (DroppedWeaker / Rejected) when those events are later projected — Pending until then.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/shield-layer-inspect.html
```
