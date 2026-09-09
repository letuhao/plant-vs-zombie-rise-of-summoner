# Piece: `role-badge`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Chip  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/role-badge.html` — **must author before factory done**  
**Depends on:** [spec-theme-packs.md](spec-theme-packs.md) (`side` / `neutral`), [payload-types.md](payload-types.md)  
**First consumer:** ActorPanel slim rail (`ActorSummarize`) — replaces mute `Lv · role` plain text  
**Data:** `/sheet.roleLabel` + `side` (already projected — **consume Hub/sheet only; no new BE**)

---

## Objective

Fiction **role** is a serious themed badge piece — not muted meta string. Plain-text
`Lv ${level} · ${role}` on the rail is a **defect** (same failure mode as mute element chips).

## Role (player)

Shows what the specimen is on the sheet rail (e.g. Plant / Zombie / Commander fiction label) with
side theme paint + optional vfx — readable at a glance next to level.

## Structure

| | |
|---|---|
| Landmark / root | `span.role-badge` (or `button` if later selectable) |
| Slots | _none_ |
| Rail composition | `Lv {n}` text + **`role-badge`** (not `Lv · role` single muted string) |

**Ban:** embedding role only inside `text-muted` meta paragraph; Condition mute chip twins of role.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"role-badge"` | yes | |
| `instanceId` | `string` | yes | e.g. `rail:role:{instanceId}` |
| `phase` | `Phase` | yes | |
| `label` | `string` | yes | Fiction from `sheet.roleLabel` / summarize prop |
| `roleId` | `string` | no | Stable id if catalog grows; else label slug |
| `themeRef` | `ThemeRef` | yes | Prefer `{ kind: "side", id: "plant" \| "zombie" }` |
| `themeResolved` | `ThemeResolved` | bind | From theme-bind |
| `glyphRef` | `GlyphRef` | no | Optional |

## Theme / vfx

Side packs (`side-plant`, `side-zombie`) supply css + paint; `vfx.select` when pack defines (may be null today — paint still required). Never mute-only grey for the role word.

## Data flow / BE

| Layer | Duty |
|---|---|
| Server | Already projects `roleLabel` + `side` on `ActorSheetDto` — **no new projection required** for v1 |
| Fold / host | Rail host builds payload from sheet; ThemeRef from `side` |
| Piece | Renders badge only — no fetch |

If a future role catalog (more than side-derived Plant/Zombie/Commander) appears, that is a **new**
catalog + optional BE join — ask first; not required to ship this piece.

## Omit rules

- Empty/whitespace `roleLabel` → omit badge (keep `Lv n` if level known).
- Collapsed rail: portrait-only today; badge may appear in `title` tip only until collapsed chrome is redesigned — **expanded rail must show the piece**.

## Success criteria

- [ ] Draft HTML exists at `docs/design/gui-lego/pieces/role-badge.html`.
- [ ] Expanded `ActorSummarize` mounts `role-badge` — **no** single muted `Lv · Role` string as the only role presentation.
- [ ] Side theme paint visible (plant/zombie packs); not `text-muted` alone.
- [ ] Piece registered in gui-lego registry; factory consumes `themeResolved`.
- [ ] Unit: sheet `roleLabel: "Plant"` + side plant → badge label + themeRef side.plant.
- [ ] Condition identity does **not** duplicate role (phase + elements only) unless a later map amend says otherwise.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/role-badge.html
cd web\fusion-rpg-web
npm test -- --run role-badge
npm test -- --run ActorSummarize
rg -n "Lv .* ·|role-badge|summarize-meta" web/fusion-rpg-web/src/ui/actor/ActorSummarize.tsx
```

## Testing

- ActorSummarize tests update: assert role-badge testid / landmark, not only meta text join.
- Theme-bind: side pack paint on badge root.
- Snapshot / a11y: label exposed to AT.

## Boundaries

- **Always:** treat role as Chip ERM piece; theme packs own paint.
- **Ask first:** role catalog beyond side-derived labels; showing role-badge on Condition too.
- **Never:** leave rail role as mute plain text; invent FE-only role strings that disagree with `/sheet.roleLabel`.

## Sample payload

```json
{
  "piece": "role-badge",
  "instanceId": "rail:role:derived-audit",
  "phase": "ready",
  "label": "Plant",
  "themeRef": { "kind": "side", "id": "plant" }
}
```

## ActorHub

**Consume** sheet `roleLabel` / `side` only. No new Hub contribution for v1.
