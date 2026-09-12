# Condition glance modules — the ideal

**Status:** idea closed into **approved map + module specs** (2026-09-10). Build via `/plan` →
implement Waves — not authorized as a cheap FE-only pass.  
**Capability map:** [condition-glance-map.md](condition-glance-map.md) (**approved**)  
**Specs:** [condition-glance/](condition-glance/) · shared [gui-lego/spec-*.md](gui-lego/) (see map table)  
**Program id:** `condition-glance` (ActorSheet Condition tab surface under `gui-lego` + `actor-sheet`).  
**Procedure:** produced under [idea-ui-phase.md](idea-ui-phase.md) (`/idea-ui`) — module-first menu
enrichment, not generic system `/idea`.  
**Screenshot under audit:** live Condition glance (progression / identity / vitality / Standing) —
not the Derived cook console. Calling it “derived panel” in chat is a naming slip; Derived is a
separate recipe (`derived-console`).

---

## Which loop this extends

| Loop / place | Role |
|---|---|
| **Spine A — Level up and power** | Progression gauge + Standing (`PowerVector`) are the specimen power glance |
| **Spine B — Creature summon and fusion** | Species / phase / element identity of a bound UniqueActor |
| **Place — Lawn / Sanctum (ActorSheet)** | Band-2 layer over the stage: inspect a specimen without leaving the stage |

This is **not** a new product loop and **not** a new top-level route. It is how the Condition
**menu surface** is authored as composable modules so the sheet stops shipping as one fragile
React host with ad-hoc CSS.

---

## Load-bearing principles (restated inline)

1. **Every RPG feature lives in the RPG layer.** Element badges, shield stacks, live statuses, and
   Standing are RPG presentation of RPG state (`elementTyping`, `shieldSummary`, `liveStatuses`,
   `PowerVector`, Hub `resourcePools`). They are never blocked by Plant/Zombie Unity fields.
2. **Player menus are recipe + pure fold + closed bus — never a god TSX.** A “page component”
   that owns layout, paint, data joins, and copy is the exact defect that produces the ten bugs
   below as one tangled CSS pass. Each bug is one or more **modules** (piece + optional theme pack
   + fold slice + draft HTML).
3. **Theme packs own paint.** Pieces declare slots; packs supply token CSS and resolved paint hex.
   Hard-coded muted `.chip` text for `dark` / `fire` is a violation of the GUI Lego decision, not a
   style preference.
4. **Buy before build for presentation.** Gauges/charts use locked libs (`recharts`, `lucide-react`,
   `motion`) or kit CSS radials already accepted for Condition — inventing a third gauge grammar
   inside ConditionTab is forbidden. A fat chunk is a splitting failure, not a reason to ban libs.
5. **Cold sheet honesty.** Hot-only runtimes (`liveStatuses`, live shield) stay empty/`null` on cold
   UniqueActor `/sheet`. FE **omits** those modules when empty (Q3) — not empty chrome, not
   `phase-empty` wallpaper.
6. **No engine vocabulary on the player surface.** Titles must not cite `definitions.md`, channel
   ids, or author notes. Fiction labels only.
7. **HTML draft fidelity.** Implementation moves toward approved drafts; drafts that still carry
   stub copy (`definitions.md`) must be amended in design first, then ported — not left as
   product UI.

---

## What this is (player language)

When you open a creature’s sheet on **Condition**, you should see at a glance: how close they are to
the next level, what species and elements they are, how full their six pools are (with a real HP
radial and optional shield ring), how their five Standing axes sit, what live effects are on them,
and whether a shield is up — without scrolling sideways, without reading developer footnotes, and
without mistaking a blank square for a gauge.

---

## Locked decisions (former open questions)

| Decision | Lock |
|---|---|
| **Element badge** | Shared `element-badge` once; Condition is first consumer; Elements tab + HUD reuse later. Consumes element theme packs + catalog glyph — may wrap Derived `chip` or kit `.el`, **not** a mute twin. |
| **Shield glance home** | Under `cond-hero` (vitality); stack pips under the radial; HP radial shield ring stays secondary. |
| **Role** | **`role-badge` shared piece on ActorPanel rail** (replaces mute `Lv · role` text). Condition keeps phase + elements only — does not duplicate role. Data: existing `/sheet.roleLabel` (no new BE for v1). |
| **Gauge “effect”** | **Overturned 2026-09-10:** recharts + realtime revision animation in scope (Q4/Q5); theme packs still own paint. |
| **Empty shield/status** | **Overturned:** omit mount when 0/null (Q3) — no empty chrome. |
| **BE scope** | **Overturned:** Hot `/sheet` projection of shield + liveStatuses in program (Q6). |
| **Element paint** | **Overturned:** centralized `element-paint-ssot` for all element UI (Q2). |

---

## Closed module catalog

Every module this surface needs. Columns: **Draft** = `docs/design/gui-lego/pieces/<id>.html`;
**Spec** = `docs/architecture/gui-lego/spec-<id>.md`; **Recipe** = present in `condition-console.json`.

| Module id | Bucket | Draft | Spec | Recipe | First consumer / notes |
|---|---|---|---|---|---|
| `surface-shell` | Built | yes | yes | yes | Shared shell |
| `phase-loading` / `phase-error` / `phase-pending` | Built | yes | yes | overlays | Shared lifecycle |
| `phase-empty` | **Superseded (Q3)** | yes | yes | **no** | Do **not** use for 0 shield/status — **omit mounts**. Lifecycle piece remains for filter-empty surfaces (Derived), not Condition glance zeros |
| `progression-gauge` | Built + polish gap | yes | [spec](gui-lego/spec-progression-gauge.md) | yes | Track fill live; recharts/theme vfx in scope |
| `actor-identity` | Built, defective | yes | [spec](gui-lego/spec-actor-identity.md) | yes | Mute chips → badges; species missing = fiction copy |
| `cond-hero` | Built | yes | [spec](gui-lego/spec-cond-hero.md) | yes | Gains `shield-status` slot |
| `pool-meter` | Wiring + polish | yes | [spec](gui-lego/spec-pool-meter.md) | yes | themeRef/icon must render |
| `pool-radial` | Wiring + **draft debt** | **no** | [spec](gui-lego/spec-pool-radial.md) | yes | Standalone draft required |
| `stand-row` | Built, defective | yes | [spec](gui-lego/spec-stand-row.md) | yes | Host; overflow / width |
| `standing-radar` | Built, defective + **draft debt** | **no** | [spec](gui-lego/spec-standing-radar.md) | yes | **recharts** (Q4) |
| `standing-bars` | Built + **draft debt** | **no** | [spec](gui-lego/spec-standing-bars.md) | yes | Needs standalone draft |
| `status-glyph-strip` | Wiring + **draft debt** | **no** | [spec](gui-lego/spec-status-glyph-strip.md) | yes | StatusGlyph; **omit when 0** |
| `element-badge` | Real gap | **no** | [spec](gui-lego/spec-element-badge.md) | **no** | Shared; Condition first |
| `phase-badge` | Real gap | **no** | [spec](gui-lego/spec-phase-badge.md) | **no** | Condition identity |
| `role-badge` | **In scope** | **no** | [spec](gui-lego/spec-role-badge.md) | rail host | ActorSummarize — serious Chip; not mute text |
| `shield-status` | Real gap | **no** | [spec](gui-lego/spec-shield-status.md) | **no** | Under `cond-hero`; **omit when null/0** |
| `theme-bind` | Wiring gap | n/a | [spec](gui-lego/spec-theme-bind.md) | n/a | shieldThemeRef + consume |
| `fiction-copy` | Built, defective | surface + fold | [spec](condition-glance/spec-fiction-copy.md) | n/a | Standing + status titles |
| `species-empty` | **Not a piece** | — | identity copy rule | n/a | Fiction `speciesMessage` under `actor-identity` — keep block mounted |
| `condition-layout` | Built, defective | surface CSS | [spec](condition-glance/spec-condition-layout.md) | surface | Stand col ≥ chart |
| `element-paint-ssot` | Real gap (Q2) | packs | [spec](gui-lego/spec-element-paint-ssot.md) | n/a | Centralize element UI colors |
| `sheet-hot-projection` | Real gap (Q6) | — | [spec](condition-glance/spec-sheet-hot-projection.md) | n/a | Hot live statuses + shield on `/sheet` |
| Derived `chip` | Built (Derived) | yes | yes | Derived only | Condition uses `element-badge` + paint SSOT |
| `StatusGlyph` | Built (Status tab) | React | — | — | Wire into `status-glyph-strip` |
| `CatalogIcon` | Built | — | — | — | Wire on `pool-meter` |

---

## What already exists — buckets

### Built (works end to end today)

| Capability | Proof |
|---|---|
| `/sheet` Standing + six `resourcePools` from Hub | `UniqueActorHubCompose.ProjectSheet`; live curl `derived-audit` returned six pools + Standing |
| Hub base `resource.max.*` seed | `ResourceBaselineSubsystem` + `seedResourceBaseline: true` on UniqueActor compose |
| Condition recipe 2×2 tree | `condition-console.json`: prog \| identity / hero \| stand; status nested under stand `live` |
| Fold → bind → RecipeMount path | `foldConditionSurfaceVm` → `bindSurface` → factories in `condition.tsx` |
| Plain track fills for XP / meters when numbers present | Live SPA showed `13,971 / 13,971` style values (not pending) |
| Shared `StatusGlyph` for Status tab | `web/fusion-rpg-web/src/ui/actor/StatusGlyph.tsx` (comment claims Condition share — **not wired**) |
| Element catalog colors | `data/tuning/element-catalog.v1.json`; element theme packs include `vfx.select` (e.g. dark → `vfx.dark-pulse`) |
| Resource theme packs with `vfx.select` | `themes/packs/resource-*.json` — Condition never binds |
| Derived `chip` + theme/vfx | `chrome.tsx` chip factory |
| Kit `.el` / `.el-pip` / `.status-glyph` / `.shield-card` | `docs/design/_kit/kit.css` (plates only) |
| Nested piece **factories** (no drafts) | `pool-radial`, `standing-radar`, `standing-bars`, `status-glyph-strip` in `condition.tsx` |

### Wiring gap (machinery exists, inert or bypassed)

| Gap | Inert line / miss |
|---|---|
| Element chips ignore theme packs / catalog color | `condition.tsx` `actorIdentityFactory` — plain `<span className="chip">`; never themed `chip` / `.el` / catalog `color` |
| Meter `themeRef` set in fold, unused in factory | `foldConditionSurfaceVm` attaches resource `themeRef`; `poolMeterFactory` paints only via CSS `[data-pool]` — no `themeStyle` / `vfxClass` |
| Meter `CatalogIcon` unused | Fold sets `icon`; `poolMeterFactory` never renders it |
| Radial `themeResolved` unused | Fold sets resource `themeRef` on radial; factory hard-codes HP `var(--bad)` |
| **`theme-bind` / `shieldThemeRef`** | Fold sets `shieldThemeRef`; `bindSurface.ts` resolves only `themeRef`; radial reads unset `shieldColor` → fire default |
| Status strip reinvents glyphs | `statusGlyphStripFactory` local buttons; does not compose `StatusGlyph` |
| Pack `vfx.select` never applied | Element/resource packs define pulse classes; Condition factories never attach them |
| Shield tab wells pending forever | `CatalogTabs` ShieldTab “Empty layer” — separate surface (deferred) |
| Lawn HUD element color map stale | `actorHudDisplayTokens.ts` private table vs catalog → `element-paint-ssot` |

### Real gap (no shareable module yet — or draft/spec debt)

| Gap | What would have to exist |
|---|---|
| **`element-badge` piece** | Draft + spec + registry; catalog glyph + theme pack paint/vfx; may wrap `chip`/`.el` |
| **`phase-badge` piece** | Draft + spec; fiction phase with side/theme paint (Condition identity) |
| **`shield-status` piece** | Draft + spec + recipe slot under `cond-hero`; kit `.shield-card` is plate only |
| **Nested draft/spec debt** | Standalone HTML + `spec-*.md` for `pool-radial`, `standing-radar`, `standing-bars`, `status-glyph-strip` |
| **Condition piece specs** | `spec-progression-gauge`, `spec-actor-identity`, `spec-cond-hero`, `spec-pool-meter`, `spec-stand-row` (HTML exists; architecture specs do not) |
| **Gauge effect layer** | Theme `vfx` + kit depth on `progression-gauge` / `pool-*` (same piece ids — not a third grammar) |
| **Standing radar correctness** | Tip markers on scaled polygon; column width ≥ SVG; optional recharts only if kit SVG fails fidelity |
| **Product empty for live status / shield** | **Superseded (Q3):** omit mounts — no `phase-empty` wallpaper on Condition glance |

### Built, defective

| Gap | Proof |
|---|---|
| **`condition-layout`** | Stand column ~140px vs radar SVG 150×150; `.live-col { overflow: auto }` in `conditionConsole.css` |
| **`standing-radar` tips** | Polygon scales with `fillPct`; tip circles stay at full `RADAR_POINTS` (`condition.tsx`) |
| **`fiction-copy`** | Fold: `Standing · five axes (definitions.md)` (~L210–251); `Live status · tap for Status tab` (~L264); draft surface still carries stub |
| **`species-empty`** | Fold messages e.g. `Species isn't on file for this actor.` — product copy without empty-piece grammar |

---

## Owner bugs → module breakdown

Treat each row as a **module** (or module set). Scope is intentionally large: shared pieces first,
Condition recipe second. Do not “fix ConditionTab CSS.”

| # | Player bug | Module(s) | Bucket | Notes |
|---|---|---|---|---|
| 1 | Layout row broken — vitality vs Standing | `condition-layout` | **Built, defective** | Stand col ≥ radar; drop default live overflow; landmark tests |
| 2 | Element has no CSS/VFX; dark is pure text | **`element-badge`** + packs + `theme-bind` | **Real gap** + wiring | Packs **built** with `vfx.dark-pulse`; Condition never consumes |
| 3 | No role badge; roster/phase pure text | **`role-badge`** (rail) + **`phase-badge`** (Condition) | **Real gap** → **in scope** | Rail mute text is the defect; Condition does not duplicate role |
| 4 | No gauge effect for XP | `progression-gauge` + pack/kit vfx | **Real gap** (polish) | Track fill **built** |
| 5 | No graph/gauge effect for resources | `pool-meter` / `pool-radial` + `theme-bind` + `CatalogIcon` | **Wiring** + polish | Numbers **built** |
| 6 | Five-axis graph not drawn | `standing-radar` (+ draft debt) | **Built, defective** | Scale tips; width contract |
| 7 | `Standing · … (definitions.md)` | `fiction-copy` | **Built, defective** | Fiction titles only; amend design SSOT first |
| 8 | Live status needs a component | `status-glyph-strip` → `StatusGlyph`; **omit when 0** | **Wiring** | Strip exists; wrong glyph + stub title |
| 9 | Add shield status | **`shield-status`** under `cond-hero`; **omit when null/0** | **Real gap** | Cold `null` → omit, not empty chrome |
| 10 | Abundant horizontal scroll | `condition-layout` + radar width | **Built, defective** | Structural width, not hide scrollbar |

### Shared reuse map

```text
element-badge ──► Condition actor-identity (first), Elements tab, HUD later
                  (consumes element-paint-ssot — no mute twin)
phase-badge ──► Condition identity only (v1)
role-badge ──► ActorPanel rail (ActorSummarize) — serious Chip; not mute Lv · role text
StatusGlyph ──► Status tab (built), Condition status-glyph-strip (wire); omit strip when 0
shield-status ──► cond-hero (locked); omit when null/0; Shield tab later
theme-bind ──► bindSurface + all themed Condition pieces + role-badge
CatalogIcon ──► pool-meter (wire fold icon)
species missing ──► fiction speciesMessage under actor-identity (keep block)
progression-gauge / pool-* / standing-* ──► Condition glance grammar (recharts + revision)
chip + element packs ──► Derived rails (built); Condition via element-badge + paint SSOT
```

---

## Prior art (outside the repo)

### Element badges

- Element color is a **semantic system**, not decoration: fire ≈ orange-red, ice ≈ blue-cyan,
  necrotic/dark ≈ purple-black; the same color must link cause → icon → HUD
  ([ColorArchive — color in game UI](https://colorarchive.me/notes/apr-2028-color-game-ui/)).
- **Failure mode:** color alone on a shifting background. Mitigate with glyph shape + contrast +
  optional pulse — which is exactly what gui-lego theme packs (`paint` + `vfx`) already encode.
- RPG Maker–style elemental damage popups sell color-by-element as a clarity feature (e.g. itch.io
  elemental color popup plugins) — confirms players expect **named element → fixed color**, not grey
  text chips.

### Resource / XP gauges

- Radial gauges commonly **reflow wrongly** when placed beside a flex column (container size
  shrinks the gauge) — Telerik Blazor forum: gauge “gets smaller” when a label sits beside it;
  fix is explicit width/height ownership, not hope
  ([Telerik forum](https://www.telerik.com/forums/blazor-ui-radial-gauge-resizes-when-an-element-is-placed-beside-it)).
- Dynamic scale/range animations that reset to zero are a known failure mode when options (not
  value) change ([Kendo angular gauge](https://www.telerik.com/forums/kendo-radial-gauge-dynamic-scale-range)).
- **Implication for us:** `pool-radial` must own a fixed box (draft 128×128); pool column scrolls
  **internally** only when needed; never let flex steal the radial’s box.

### Standing radar (five axes)

- Radar/spider charts: **area distortion** (area grows ~r²), **axis-order bias**, and poor exact
  comparison — bars should stay the precision path; radar is glance shape
  ([Flourish radar guidance](https://flourish.studio/blog/create-online-radar-spider-charts/);
  [Algoscale spider limits](https://algoscale.com/blog/what-is-spider-chart/)).
- Recommended variable count **5–8** for readability — our five Standing axes fit.
- Markers on vertices improve comprehension when values vary
  ([smartphone radar study PDF](https://www.aasmr.org/jsms/Vol14/No.7/Vol.14.No.7.31.pdf)).
- **Implication:** keep bars as SSOT numbers; fix radar markers to sit **on** the scaled polygon;
  never use radar alone for absolute Standing ints.

### Status / shield HUD

- Status effects as **icon chips with duration/ring**, not paragraphs
  ([ORK Framework status HUD](https://orkframework.com/guide/non-knowledgebase/ui-system/huds-status-effects/)).
- HUD vs sheet split: HUD = now; sheet = inspect — Condition is sheet-glance, so empty live status
  still needs a chip strip empty-state, not a wall of prose
  ([DEV: HUD lying by omission](https://dev.to/meridian-ai/your-dungeon-crawler-hud-is-lying-by-omission-1f35)).

---

## The shape

### Rejected shapes

| Shape | Why rejected |
|---|---|
| “Fix ConditionTab CSS” as one PR | Reproduces the god-page failure; violates GUI Lego decision |
| Tailwind mood-board rewrite of the glance | Violates HTML design implementation; draft SSOT ignored |
| Invent Condition-only element colors | Catalog + theme packs already exist |
| Force recharts everywhere immediately | Overturned — **recharts in scope** (Q4); still no third hand-rolled grammar |
| Fabricate shield/live status on cold sheet | Violates Hot-only honesty; cold omits mounts |
| Empty chrome for 0 status/shield | Violates Q3 omit |
| Put mute `Lv · role` plain text on rail | Defect — use `role-badge` |
| Duplicate role on Condition v1 | Rail owns role-badge; Condition keeps phase + elements |

### Chosen shape

**Module-first Condition surface:**

1. **Author shared pieces** (`element-badge`, `phase-badge`, `shield-status`) with draft HTML +
   theme pack paint + piece registry.
2. **Pay nested draft/spec debt** for recipe ids that already mount without `pieces/*.html`.
3. **Amend Condition drafts** (surface + pieces) — fiction titles, empty states, layout widths;
   remove `definitions.md` from any player string.
4. **Wire `theme-bind`** — `bindSurface` resolves `shieldThemeRef`; factories consume
   `themeResolved` / `vfxClass`; meters render `CatalogIcon`; strip composes `StatusGlyph`.
5. **`condition-layout`** landmark CSS contracts + DOM tests.
6. **Gauge/chart effect** on the same piece ids via **recharts** + theme paint + `revision`
   animation (Q4/Q5) — not a third private gauge grammar.

### Composition tree (locked)

```text
ActorPanel
└── condition-console (recipe)
    ├── progression-gauge          (+ recharts/theme + revision)
    ├── actor-identity
    │     ├── phase-badge          (omit if no phase)
    │     ├── element-badge*
    │     └── species line / speciesMessage (fiction; keep block)
    ├── cond-hero
    │     ├── pool-radial (+ secondary shield ring when shield mounted)
    │     ├── shield-status        ← omit when null/0
    │     └── pool-meter*
    └── stand-row
          ├── standing-radar       (recharts)
          ├── standing-bars
          └── status-glyph-strip ── StatusGlyph*   ← omit when 0
```

---

## Tunables

| Number / look | Owner |
|---|---|
| Element display colors / names | `data/tuning/element-catalog.v{n}.json` |
| Element theme paint + vfx class | `docs/design/gui-lego/themes/packs/element-*.json` + `element-paint-ssot` |
| Status hudToken / color | `data/tuning/status-catalog.v{n}.json` |
| Resource meter paints | resource theme packs + catalog |
| Standing axis paints | fold constants today → theme packs if balance-touched |
| Radial box 128×128, stand column width | structural CSS (not balance) — document as structural |

No new power curve. Standing values remain `PowerVector` ints; fillPct stays relative normalize.

---

## What this deliberately does not decide / deferred

| Deferred from **this** program | Why |
|---|---|
| Full Shield **tab** stack UI | Sibling [`shield-sheet`](shield-sheet-map.md) — **in** product scope (**S1** layers on sheet) |
| Derived cook harden | Sibling [`derived-cook`](derived-cook-map.md) — **in** (**D1–D7**) |
| HUD adoption of `element-badge` | Second consumer after Condition (paint SSOT ready) |
| `role-badge` on Condition body | Rail is first consumer; Condition does not duplicate |

**In program (not deferred):** Hot `/sheet` live statuses + shield **summary** + live bag (**S3**);
`shieldLayers` filled same compose for sibling; **recharts**; Q3 omit-empty; **`role-badge` on rail**.  
Owner: **three programs, three plans** + strengthen locks **S1–S3**.

---

## The real question

**Not feasibility.** Pools, Standing, catalogs, theme packs (with `vfx.select`), StatusGlyph,
CatalogIcon, and the Lego registries already exist. The real question is **shape**: stop treating
Condition as a page and ship it as a **closed module set** — shared badges and `theme-bind` first,
nested draft/spec debt and recipe wiring second, polish effects third — so dark is never grey text
and Standing never cites a markdown path.

**Next:** specs strengthened — run `/plan` → `tasks/condition-glance-plan.md` /
`tasks/condition-glance-todo.md`, then implement Waves 1–4. No cheap FE-only shortcut.
