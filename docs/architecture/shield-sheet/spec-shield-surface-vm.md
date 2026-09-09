# Module: `shield-surface-vm`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Composition:** [../gui-lego/spec-composition.md](../gui-lego/spec-composition.md)

---

## Objective

Pure fold `foldShieldSurfaceVm(input) → ShieldSurfaceVm`. No React. Formats magnitudes, attaches
themeRef, maps Pending/Hot-empty, never invents layers.

## Inputs

| Field | Source |
|---|---|
| `layers` | Hot shields API / sheet.shieldLayers |
| `summary` | sheet.shieldSummary (parity check) |
| `omniChannels` | sheet derived join |
| `availability` | ready / loading / error / pending |
| `ui` | selectedShieldId |
| `locale` | formatter |

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
| Query pending (API unwired) | `phase-pending` / PendingNote — **not** three Empty layer labels |
| Hot ready, 0 layers | empty stack chrome + fiction empty copy; dashed wells OK |
| Hot ready, N layers | mount bar with N segments + (3−N) dashed |

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
