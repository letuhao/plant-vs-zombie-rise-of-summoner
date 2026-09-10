# Module: `shield-surface-vm`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Composition:** [../gui-lego/spec-composition.md](../gui-lego/spec-composition.md)  
**Locks:** **S1** layers on sheet · Q3 does **not** omit whole tab

---

## Objective

Pure fold `foldShieldSurfaceVm(input) → ShieldSurfaceVm`. No React. Formats magnitudes, attaches
themeRef, maps Pending/Hot-empty, never invents layers.

## Inputs

| Field | Source |
|---|---|
| `layers` | `sheet.shieldLayers` |
| `summary` | `sheet.shieldSummary` (parity check) |
| `omniChannels` | sheet derived join |
| `availability` | ready / loading / error / pending |
| `ui` | selectedShieldId |
| `locale` | formatter |
| `themeRegistry` | inject |

## Outputs

| Slice | Pieces |
|---|---|
| `stack` | `shield-stack-bar` |
| `inspect` | `shield-layer-inspect` or phase-* |
| `omni` | omni row payloads |
| `phase` | surface lifecycle |
| `revision` | monotonic |

## Omit / pending

| Condition | Behavior |
|---|---|
| Query pending (API unwired) | `phase-pending` — **not** three Empty layer labels |
| Hot ready, 0 layers | empty stack + fiction empty; dashed wells OK |
| Hot ready, N layers | bar with N segments + (3−N) dashed |

## Sample VM fragment

```json
{
  "phase": "ready",
  "revision": 3,
  "stack": {
    "piece": "shield-stack-bar",
    "segments": [],
    "emptySlots": 3
  },
  "inspect": { "piece": "phase-empty", "message": "No shield layer selected" },
  "omni": []
}
```

## Success criteria

- [ ] Unit tests: pending vs hot-empty vs 1–3 layers.
- [ ] Summary totals agree with layer sums when both present.
- [ ] Pieces do not re-fetch.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run foldShield
```

## Boundaries

- **Never:** recompute ShieldMath remainder; FE fixtures as Hot.
