# Module: `fiction-copy`

**Program:** `condition-glance` · **Map:** [../condition-glance-map.md](../condition-glance-map.md)

---

## Objective

All Condition player-visible strings are **fiction product copy**. No engine paths, markdown
filenames, or “tap for Status tab” author notes in fold, drafts, or SPA.

---

## Owned strings

| Location | Forbidden today | Required |
|---|---|---|
| Standing host title | `Standing · five axes (definitions.md)` | Fiction e.g. `Standing` |
| Status strip title | `Live status · tap for Status tab` | Fiction e.g. `Effects` — **only when strip mounts** |
| Species missing | `UniqueActor` / typeId jargon | Short fiction e.g. `Species unknown` via `speciesMessage` |
| Empty status/shield | `No live effects applied` | **Omit mount** (Q3) — no replacement prose |

Exact wording may live in FE i18n later; English fiction placeholders allowed in fold until i18n wire.

### Species missing rule (not a piece)

- Keep `actor-identity` mounted.
- Set fiction `speciesMessage` when `speciesName` null/empty.
- Do **not** invent a `species-empty` piece; do **not** omit the whole identity block.

---

## Draft amend checklist (design SSOT first)

| File | Action |
|---|---|
| [surfaces/condition-console.html](../../design/gui-lego/surfaces/condition-console.html) | Remove `definitions.md` / stub titles |
| [pieces/progression-gauge.html](../../design/gui-lego/pieces/progression-gauge.html) | Fiction-only labels |
| [pieces/actor-identity.html](../../design/gui-lego/pieces/actor-identity.html) | Fiction species empty; badge placeholders |
| [pieces/cond-hero.html](../../design/gui-lego/pieces/cond-hero.html) | No stub author notes |
| [pieces/pool-meter.html](../../design/gui-lego/pieces/pool-meter.html) | Fiction labels |
| [pieces/stand-row.html](../../design/gui-lego/pieces/stand-row.html) | Fiction Standing title; no definitions.md |
| Future: `element-badge.html`, `phase-badge.html`, `shield-status.html`, `pool-radial.html`, `standing-radar.html`, `standing-bars.html`, `status-glyph-strip.html` | Fiction-only sample copy when authored |

---

## Success criteria

- [ ] Fold titles: Standing fiction; live status title fiction-only when strip mounts.
- [ ] Zero matches for `definitions.md`, `tap for Status`, `No live effects applied` under Condition fold + listed drafts + SPA Condition strings.
- [ ] Species missing uses short fiction `speciesMessage`; identity stays mounted.

---

## Commands

```powershell
# Forbidden substrings in Condition design + FE fold
rg -n "definitions\.md|tap for Status|No live effects applied" docs/design/gui-lego web/fusion-rpg-web/src/features/gui-lego/foldConditionSurfaceVm.ts web/fusion-rpg-web/src/ui/gui-lego
cd web\fusion-rpg-web
npm test -- --run foldConditionSurfaceVm
```

## Testing

- Unit/snapshot: fold golden titles + omit empty strip.
- Grep guard in CI or script for forbidden substrings.

## Boundaries

- **Always:** amend design SSOT drafts before treating copy as done.
- **Never:** leave stub titles in product SPA; empty-status prose instead of omit.

## ActorHub

N/A.
