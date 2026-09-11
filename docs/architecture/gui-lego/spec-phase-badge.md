# Piece: `phase-badge`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Chip  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/phase-badge.html` — **must author before factory done**  
**Depends on:** [spec-theme-packs.md](spec-theme-packs.md) (side / neutral)

---

## Role

Fiction **phase** badge on Condition identity. Role is a separate piece (`role-badge`) on the
ActorPanel rail — do not put role on this piece.

## Structure

| | |
|---|---|
| Landmark / root | `span.phase-badge` |
| Slots | _none_ |

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"phase-badge"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | lifecycle |
| `label` | `string` | yes | Fiction phase label |
| `themeRef` | `ThemeRef` | no | Prefer `side` or `neutral` |
| `themeResolved` | `ThemeResolved` | bind | |

## Success criteria

- [ ] Draft HTML exists.
- [ ] Mounted badge is not mute-only grey; theme/side paint applied.
- [ ] Omit when `phaseLabel` null.
- [ ] Does not render role (rail `role-badge` owns role).

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/phase-badge.html
cd web\fusion-rpg-web; npm test -- --run phase-badge
```

## Sample payload

```json
{
  "piece": "phase-badge",
  "instanceId": "condition:phase",
  "phase": "ready",
  "label": "Ascendant",
  "themeRef": { "kind": "side", "id": "plant" }
}
```
