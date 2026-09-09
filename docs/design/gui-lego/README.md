# GUI Lego — design index

**Program:** `gui-lego`  
**Status:** **Binding design standard** for player menus — P0 React **shipped** (fold → bind → RecipeMount).  
**Ideal / map / authoring:** [../../architecture/gui-lego-ideal.md](../../architecture/gui-lego-ideal.md) ·
[../../architecture/gui-lego-map.md](../../architecture/gui-lego-map.md) ·
[../../architecture/gui-lego-authoring.md](../../architecture/gui-lego-authoring.md)  
**Idea-UI (menu bug lists / FE audits before `/spec`):** [../../architecture/idea-ui-phase.md](../../architecture/idea-ui-phase.md) · example [../../architecture/condition-glance-ideal.md](../../architecture/condition-glance-ideal.md)  
**Queue:** [../../architecture/gui-lego/menu-refactor-queue.md](../../architecture/gui-lego/menu-refactor-queue.md)  
**Tasks:** [../../tasks/gui-lego-plan.md](../../tasks/gui-lego-plan.md)

**Start here (whole menu):** [surfaces/derived-console.html](surfaces/derived-console.html)  
Open any `pieces/*.html` or `themes/*.html` in a browser (links `_kit` relatively).

**FE sync:** Theme packs and `recipes/derived-console.json` are **copied** into
`web/fusion-rpg-web/src/features/gui-lego/themes/` and `web/fusion-rpg-web/src/ui/gui-lego/recipes/`.
Design remains SSOT — re-copy when packs change (manual until a sync script exists).

Pre-Lego visual peer (history): [../derived-combat-console.html](../derived-combat-console.html).

---

## 1. What lives here

| Path | Role |
|---|---|
| [surfaces/derived-console.html](surfaces/derived-console.html) | **Assembled P0 surface** (recipe composed) |
| [surfaces/condition-console.html](surfaces/condition-console.html) | **Assembled P1 Condition glance** (partial — see ideal) |
| [recipes/derived-console.json](recipes/derived-console.json) | Derived surface assembly (slots + binds) |
| [recipes/condition-console.json](recipes/condition-console.json) | Condition surface assembly (2×2 + nested stand/hero) |
| [pieces/](pieces/) | Per-piece HTML drafts (structure + sample payload) |
| [themes/](themes/) | Theme demos + [swap-lab](themes/swap-lab.html) |
| [themes/packs/](themes/packs/) | JSON packs: elements, status-categories, resources, sides, neutral |
| [pieces/_piece-kit.css](pieces/_piece-kit.css) | Shared draft chrome for piece pages |

Architecture contracts: [../../architecture/gui-lego/](../../architecture/gui-lego/).

---

## 2. Piece inventory (Derived surface v1)

### Layout / chrome

| piece-id | Kind | Role | Draft | Spec |
|---|---|---|---|---|
| `surface-shell` | layout | Frame + slots | [pieces/surface-shell.html](pieces/surface-shell.html) | [spec](../../architecture/gui-lego/spec-surface-shell.md) |
| `surface-foot` | chrome | Hidden / deferred note | [pieces/surface-foot.html](pieces/surface-foot.html) | [spec](../../architecture/gui-lego/spec-surface-foot.md) |
| `identity-hd` | chrome | Who / meta | [pieces/identity-hd.html](pieces/identity-hd.html) | [spec](../../architecture/gui-lego/spec-identity-hd.md) |
| `tool-search` | chrome | Search | [pieces/tool-search.html](pieces/tool-search.html) | [spec](../../architecture/gui-lego/spec-tool-search.md) |
| `tool-toggle` | chrome | Show unchanged | [pieces/tool-toggle.html](pieces/tool-toggle.html) | [spec](../../architecture/gui-lego/spec-tool-toggle.md) |
| `rail-primary` | chrome | Cook tabs | [pieces/rail-primary.html](pieces/rail-primary.html) | [spec](../../architecture/gui-lego/spec-rail-primary.md) |
| `rail-variant` | chrome | Variants | [pieces/rail-variant.html](pieces/rail-variant.html) | [spec](../../architecture/gui-lego/spec-rail-variant.md) |
| `chip` | Chip | Rail option | [pieces/chip.html](pieces/chip.html) | [spec](../../architecture/gui-lego/spec-chip.md) |
| `split-inspect` | layout | Dock \| inspect | [pieces/split-inspect.html](pieces/split-inspect.html) | [spec](../../architecture/gui-lego/spec-split-inspect.md) |
| `scroll-region` | layout | Overflow | [pieces/scroll-region.html](pieces/scroll-region.html) | [spec](../../architecture/gui-lego/spec-scroll-region.md) |
| `family-list` | layout | Family blocks | [pieces/family-list.html](pieces/family-list.html) | [spec](../../architecture/gui-lego/spec-family-list.md) |
| `family-block` | layout | Section + rows | [pieces/family-block.html](pieces/family-block.html) | [spec](../../architecture/gui-lego/spec-family-block.md) |

### Domain / gauges

| piece-id | Kind | Role | Draft | Spec |
|---|---|---|---|---|
| `channel-row` | Row | Derived channel | [pieces/channel-row.html](pieces/channel-row.html) | [spec](../../architecture/gui-lego/spec-channel-row.md) |
| `inspect-pane` | Panel-slice | Inspect host | [pieces/inspect-pane.html](pieces/inspect-pane.html) | [spec](../../architecture/gui-lego/spec-inspect-pane.md) |
| `value-hero` | Token→display | Big value | [pieces/value-hero.html](pieces/value-hero.html) | [spec](../../architecture/gui-lego/spec-value-hero.md) |
| `meta-sentences` | chrome | Sentences | [pieces/meta-sentences.html](pieces/meta-sentences.html) | [spec](../../architecture/gui-lego/spec-meta-sentences.md) |
| `cap-note` | chrome | Cap line | [pieces/cap-note.html](pieces/cap-note.html) | [spec](../../architecture/gui-lego/spec-cap-note.md) |
| `gauge-donut` | gauge | Share donut (**paint**) | [pieces/gauge-donut.html](pieces/gauge-donut.html) | [spec](../../architecture/gui-lego/spec-gauge-donut.md) |
| `gauge-stack` | gauge | Stack bars | [pieces/gauge-stack.html](pieces/gauge-stack.html) | [spec](../../architecture/gui-lego/spec-gauge-stack.md) |
| `source-list` | Row list | GG-49 sources | [pieces/source-list.html](pieces/source-list.html) | [spec](../../architecture/gui-lego/spec-source-list.md) |

### Lifecycle

| piece-id | Role | Draft | Spec |
|---|---|---|---|
| `phase-loading` | Loading | [pieces/phase-loading.html](pieces/phase-loading.html) | [spec](../../architecture/gui-lego/spec-phase-loading.md) |
| `phase-empty` | Empty filter | [pieces/phase-empty.html](pieces/phase-empty.html) | [spec](../../architecture/gui-lego/spec-phase-empty.md) |
| `phase-error` | Error + retry | [pieces/phase-error.html](pieces/phase-error.html) | [spec](../../architecture/gui-lego/spec-phase-error.md) |
| `phase-pending` | Pending field | [pieces/phase-pending.html](pieces/phase-pending.html) | [spec](../../architecture/gui-lego/spec-phase-pending.md) |

### Themes (not pieces)

| Demo | Path |
|---|---|
| **Swap lab (all elements + status)** | [themes/swap-lab.html](themes/swap-lab.html) |
| Element fire / ice | [themes/element-fire.html](themes/element-fire.html) · [themes/element-ice.html](themes/element-ice.html) |
| Status-category dot | [themes/status-dot.html](themes/status-dot.html) |
| Neutral | [themes/neutral.html](themes/neutral.html) |
| Pack JSON | [themes/packs/](themes/packs/) — omni+6 elements, 4 status-categories, resource packs, plant/zombie sides, neutral |

### Condition surface (P1 — partial)

Ideal + map + specs: [../../architecture/condition-glance-ideal.md](../../architecture/condition-glance-ideal.md) ·
[../../architecture/condition-glance-map.md](../../architecture/condition-glance-map.md).  
Queue: **Partial — specs written**; implement Waves 1–4 (BE Hot, paint SSOT, recharts, omit-empty).

| piece-id | Kind | Role | Draft | Spec |
|---|---|---|---|---|
| `progression-gauge` | gauge | XP / level + recharts/theme | [pieces/progression-gauge.html](pieces/progression-gauge.html) | [spec](../../architecture/gui-lego/spec-progression-gauge.md) |
| `actor-identity` | chrome | Species + badges | [pieces/actor-identity.html](pieces/actor-identity.html) | [spec](../../architecture/gui-lego/spec-actor-identity.md) |
| `cond-hero` | layout | Radial + meters + shield slot | [pieces/cond-hero.html](pieces/cond-hero.html) | [spec](../../architecture/gui-lego/spec-cond-hero.md) |
| `pool-meter` | gauge | Resource track + CatalogIcon | [pieces/pool-meter.html](pieces/pool-meter.html) | [spec](../../architecture/gui-lego/spec-pool-meter.md) |
| `stand-row` | layout | Standing host | [pieces/stand-row.html](pieces/stand-row.html) | [spec](../../architecture/gui-lego/spec-stand-row.md) |
| `pool-radial` | gauge | HP radial (+ shield ring) | **draft debt** | [spec](../../architecture/gui-lego/spec-pool-radial.md) |
| `standing-radar` | gauge | Five-axis **recharts** | **draft debt** | [spec](../../architecture/gui-lego/spec-standing-radar.md) |
| `standing-bars` | gauge | Absolute Standing ints | **draft debt** | [spec](../../architecture/gui-lego/spec-standing-bars.md) |
| `status-glyph-strip` | Chip strip | Live glyphs; **omit when 0** | **draft debt** | [spec](../../architecture/gui-lego/spec-status-glyph-strip.md) |
| `element-badge` | Chip | Shared elemental identity | **draft debt** | [spec](../../architecture/gui-lego/spec-element-badge.md) |
| `phase-badge` | Chip | Fiction phase | **draft debt** | [spec](../../architecture/gui-lego/spec-phase-badge.md) |
| `role-badge` | Chip | Fiction role on ActorPanel rail | **draft debt** | [spec](../../architecture/gui-lego/spec-role-badge.md) |
| `shield-status` | Card | Shield under cond-hero; **omit when null/0** | **draft debt** | [spec](../../architecture/gui-lego/spec-shield-status.md) |

Shared seams: [element-paint-ssot](../../architecture/gui-lego/spec-element-paint-ssot.md) ·
[theme-bind](../../architecture/gui-lego/spec-theme-bind.md) ·
[sheet-hot-projection](../../architecture/condition-glance/spec-sheet-hot-projection.md) ·
[fiction-copy](../../architecture/condition-glance/spec-fiction-copy.md) ·
[condition-layout](../../architecture/condition-glance/spec-condition-layout.md) ·
[recipe-wire](../../architecture/condition-glance/spec-recipe-wire.md).

### Derived cook harden (P0 Harden)

Map: [../../architecture/derived-cook-map.md](../../architecture/derived-cook-map.md) ·
Ideal: [../../architecture/derived-cook-ideal.md](../../architecture/derived-cook-ideal.md).  
Full cook truth (six states, CAP wire, paint SSOT, player copy) — not a CSS pass.

### Shield sheet (P1b)

Map: [../../architecture/shield-sheet-map.md](../../architecture/shield-sheet-map.md) ·
Ideal: [../../architecture/shield-sheet-ideal.md](../../architecture/shield-sheet-ideal.md).

| piece-id | Kind | Role | Draft | Spec |
|---|---|---|---|---|
| `shield-stack-bar` | gauge | Drain-order segmented stack | **draft debt** | [spec](../../architecture/gui-lego/spec-shield-stack-bar.md) |
| `shield-layer-inspect` | panel-slice | Layer detail | **draft debt** | [spec](../../architecture/gui-lego/spec-shield-layer-inspect.md) |
| `shield-status` | Card | Glance only (Condition) | **draft debt** | [spec](../../architecture/gui-lego/spec-shield-status.md) |

Shared overlays used by Condition recipe: `surface-shell`, `phase-loading`, `phase-error`, `phase-pending`.
`phase-empty` is **not** used for zero shield/status on Condition (Q3 omit mounts).

---

## 3. Dependency graph

```text
surface-shell
├── identity-hd
├── tool-search
├── tool-toggle
├── rail-primary ── chip*
├── rail-variant ── chip*
├── split-inspect
│   ├── scroll-region ── family-list ── family-block ── channel-row*
│   └── scroll-region ── inspect-pane
│         ├── value-hero
│         ├── meta-sentences
│         ├── cap-note
│         ├── gauge-donut
│         ├── gauge-stack
│         └── source-list
└── surface-foot

lifecycle overlays: phase-loading | phase-empty | phase-error | phase-pending
theme packs → chip, channel-row, inspect gauges (css + paint)
```

Condition glance (target composition — see ideal):

```text
surface-shell
└── main
    ├── progression-gauge
    ├── actor-identity ── phase-badge · element-badge*
    ├── cond-hero ── pool-radial · shield-status · pool-meter*
    └── stand-row ── standing-radar · standing-bars · status-glyph-strip
```

---

## 4. Cross-surface reuse matrix

| Piece | Derived | Condition | Creatures list |
|---|---|---|---|
| `chip` | yes | via `element-badge` + paint SSOT (mute twin forbidden) | filters |
| `element-badge` | — | first consumer (specced; draft debt) | — |
| `phase-badge` | — | Condition (specced; draft debt) | — |
| `role-badge` | — | ActorPanel rail (replaces mute Lv · role text) | — |
| `shield-status` | — | under `cond-hero`; omit when 0 | — |
| `tool-search` | yes | — | yes |
| `tool-toggle` | yes | — | maybe |
| `split-inspect` | yes | no | — |
| `scroll-region` | yes | maybe | yes |
| `channel-row` | yes | — | — |
| `gauge-*` (donut/stack) | yes | — | — |
| Condition gauges (`pool-*`, `standing-*`, `progression-gauge`) | — | yes (recharts radar) | — |
| `phase-*` | yes | loading/error/pending yes; empty **not** for 0 status/shield | yes |
| `identity-hd` | yes | shell identity sibling | — |
| `StatusGlyph` | — | Status tab yes; Condition strip (wire + omit when 0) | — |

---

## 5. Review checklist

- [ ] Assembled surfaces/derived-console.html reviewed
- [ ] Assembled surfaces/condition-console.html reviewed (fiction titles; no `definitions.md`)
- [ ] Recipe slots match intended landmark tree (`>` parents intact)
- [ ] Every Condition recipe piece has standalone draft **or** listed as draft debt in the ideal
- [ ] Every piece has structure + payload + flow + theme slots
- [ ] Theme swap lab proves paint hex on donut
- [ ] Lifecycle states drawn, not implied (`phase-empty` for cold Condition empties)
- [ ] Owner accepts or rejects piece boundaries
- [ ] Condition module catalog accepted: [condition-glance-ideal.md](../../architecture/condition-glance-ideal.md)
