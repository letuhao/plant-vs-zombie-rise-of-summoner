# Piece: `pool-radial`

**Program:** `gui-lego` · **Kind:** gauge  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/pool-radial.html` — **must author before factory done**  
**Depends on:** [spec-theme-bind.md](spec-theme-bind.md), [../condition-glance/spec-condition-layout.md](../condition-glance/spec-condition-layout.md)

---

## Role

Primary HP radial with optional secondary shield ring. Owns fixed **128×128** box. Chart/animated
on revision. Shield ring **omitted** when no shield (Q3).

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"pool-radial"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `hpPct` | `number` | when ready | |
| `shieldPct` | `number` \| null | no | omit ring if null/≤0 |
| `themeRef` / `themeResolved` | | yes | HP resource — **not** hard-coded `--bad` |
| `shieldThemeRef` / `shieldThemeResolved` | | when shield | |
| `revision` | `number` | yes | |

## Success criteria

- [ ] Standalone draft HTML exists.
- [ ] Fixed-box landmark shared with condition-layout (128×128 not stolen by flex).
- [ ] themeResolved used for HP paint.
- [ ] Shield theme ice ≠ fire default when shieldThemeRef set.
- [ ] No shield ring when shield omitted.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/pool-radial.html
cd web\fusion-rpg-web; npm test -- --run pool-radial
```

## Boundaries

- **Never:** hard-code HP to `--bad` when themeResolved present; show shield ring at 0.
