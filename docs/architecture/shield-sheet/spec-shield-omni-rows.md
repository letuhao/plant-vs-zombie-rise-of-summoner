# Module: `shield-omni-rows`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Depends on:** `/sheet` derived channels · cook families `combat.shield.*`

---

## Objective

Show **omni** shield StatRows on the Shield tab by joining sheet Derived — not a second private fold
and not Ward copy. Full element×family matrix stays on **Derived**.

## Channel allow-list (omni)

Join sheet channels whose family is under cook shield group and expand resolves to **omni** (or
`expand: none` shield families), including at minimum:

| Family pattern | Notes |
|---|---|
| `combat.shield.capacity.omni` | |
| `combat.shield.toughness.omni` | |
| `combat.shield.pen.omni` | |
| `combat.shield.regen.omni` | |
| Related cook-listed shield omni rows | From derived-surface cook — do not hardcode forever |

Exact set = cook families tagged shield / sheetGroup shield with omni variant — iterate cook, don’t
hand-author a frozen FE list that drifts.

## Fields (row payload)

| Field | Type | Notes |
|---|---|---|
| `channelId` | string | internal join only |
| `displayName` | string | fiction |
| `valueText` | string | formatMagnitude |
| `renderState` | six states | from wire when present |
| `themeRef` | optional | neutral/side for omni |

## Success criteria

- [ ] Rows from sheet channels; Pending when missing.
- [ ] No duplicate god console; noun Shield only.
- [ ] Element matrix not duplicated here.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run foldShield ShieldTab
```

## Sample

```json
{
  "displayName": "Shield capacity",
  "valueText": "120",
  "renderState": "active"
}
```

## Boundaries

- **Never:** invent shield magnitudes; show full 28×7 matrix here.
