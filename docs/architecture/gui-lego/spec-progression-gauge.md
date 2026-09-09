# Piece: `progression-gauge`

**Program:** `gui-lego` · **Kind:** gauge  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** [../../design/gui-lego/pieces/progression-gauge.html](../../design/gui-lego/pieces/progression-gauge.html) — amend fiction + effect chrome  
**Depends on:** [spec-theme-bind.md](spec-theme-bind.md) · tech-stack **recharts** (Q4/Q5)

---

## Role

Level + XP glance with **animated** fill driven by sheet `revision`. Prefer **recharts** (lazy) for
animated track; theme pack paint/`vfx` for effect layer (ideal bug 4). Same piece id — no third grammar.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"progression-gauge"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `level` | `number` | yes | |
| `xp` / `xpToNext` | `long` wire | when ready | |
| `fillPct` | `number` | when ready | 0–100 structural |
| `xpText` | `string` | yes | Fiction |
| `themeRef` / `themeResolved` | | no | optional pack |
| `revision` | `number` | yes | |

## Success criteria

- [ ] Ready sheet: no pending track; fill matches xp/xpToNext.
- [ ] **recharts** (or locked adapter) animates on `revision`; reduced-motion instant.
- [ ] Theme/vfx effect layer applied when pack provides (not flat mute track only).
- [ ] Lazy chunk: recharts not in sanctum entry.
- [ ] Draft amended — no stub author copy.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run progression-gauge
npm run build
# Assert recharts not in sanctum entry chunk (manualChunks / lazy)
```

## Realtime

Host invalidates sheet → fold bumps `revision` → chart animates. No piece fetch.

## Boundaries

- **Always:** buy-before-build charts; split chunk.
- **Never:** invent FE XP; piece-level fetch.
