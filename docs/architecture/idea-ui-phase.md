# Idea-UI phase — player menu / FE module enrichment

**Status: binding procedure** for idea work on **anything a player sees** that is a menu,
ActorSheet tab, band-2 body, filter/inspect panel, HUD chip, gauge, badge, or glance strip.
**Not a spec. Not a plan. Not code.**

**Slash:** `/idea-ui` (local assistant command → skill `idea-ui`).  
**Sibling:** `/idea` / `idea-phase` for RPG **systems** (combat, Hub, atoms). Use **this**
procedure when the work is presentation composition — even if RPG data is involved.

**Why this file is committed.** `.claude/` and `.cursor/` are local-only. Agents that wipe or
clone without those folders still must follow this doc. The Condition glance rebuild (2026-09-10)
burned a session treating one tab as a page CSS pass; theme packs and catalogs already existed.

---

## 0. Out loud before any subsystem doc

State these in your own words **before** reading drafts or opening TSX:

1. **Every RPG feature lives in the RPG layer.** Element badges, shields, statuses, Standing, and
   resource meters present Hub/catalog/RPG state. They are never blocked by Plant/Zombie Unity
   fields. “The lawn can’t show X” is almost always the wrong frame.
2. **A game is a stage with layers, not a document with pages** (GG-1). Menus open over the stage.
   Do not invent a sibling route to “fix” a glance.
3. **Player menus are recipe + pure fold + closed bus — never a god TSX** (`decisions.md` GUI
   Lego). A “page component” that owns layout, paint, joins, and copy is the defect this phase
   exists to catch.
4. **Theme packs own paint.** Pieces declare slots; packs supply `css` + `paint` (hex for SVG) +
   optional `vfx`. Hard-coded mute chips for `dark` / `fire` are a Lego violation, not taste.
5. **Buy before build for presentation.** Prefer locked libs (`lucide-react`, `recharts`,
   `motion`) or kit pieces already in the queue. A fat chunk is a **code-splitting** failure —
   split it; do not ban the library or invent four SVGs.
6. **Each player bug is one or more modules**, not one CSS fix. Scope is intentionally large:
   shared piece first, surface recipe second, polish third.
7. **No engine vocabulary on the player surface.** Titles must not cite `definitions.md`, channel
   ids, Intent, or author notes.

Then satisfy **DESIGN-GATE** §1 for at least:

| Row | Must read in this session |
|---|---|
| **Anything a player sees (UI)** | `game-gui-principles.md`, IA, design README §2 (ERM), `fe-game-foundation.md` |
| **Player menus** | `gui-lego-ideal.md`, `gui-lego-authoring.md`, `gui-lego-map.md`, `design/gui-lego/README.md`, `menu-refactor-queue.md`, decisions **GUI Lego** |
| Host / domain as needed | `actor-sheet-ideal.md` / map, element / status / resource SSOTs, relevant `docs/design/spec-*.md` |

Also open `docs/guide/the-game.md` + `the-loops.md` and name which loop/place the surface serves.

---

## 1. Module unit of work (non-negotiable)

| Wrong | Right |
|---|---|
| “Fix the Condition panel” | Break into `element-badge`, `pool-meter`, `standing-radar`, `shield-status`, … |
| One React host + ad-hoc CSS | Piece id + optional theme pack + fold slice + draft HTML + later factory |
| Private color map in the tab | Catalog / theme pack `paint` + `themeRef` on payload |
| Empty = omit the UI | Empty = lifecycle / empty **piece** (honest cold sheet) |
| Stub draft copy shipped as product | Amend design SSOT first; fiction labels only |

**Three registries** (always):

| Registry | Key | Owns |
|---|---|---|
| Piece | `pieceId` | Contract + draft (+ React factory later) |
| Theme | `kind.id` | css / paint / vfx |
| Recipe | `surfaceId` | Slot tree + binds |

**Authoring order** (do not start at React): queue row → reuse ERM/piece index → recipe → fold +
bus → themeRefs → HTML drafts → owner accept → React mount + landmark tests.

---

## 2. Inventory — three buckets + FE-specific fourth

Sort every finding. Use these words:

| Bucket | Means |
|---|---|
| **Built** | Works end to end — cite `file:line` + what proves it (curl, SPA, test) |
| **Wiring gap** | Machinery exists but is inert/bypassed (plain chip instead of themed chip; `themeRef` folded but unused; homemade glyph vs `StatusGlyph`) — **not a wall** |
| **Real gap** | No shareable piece/pack/recipe path exists yet |
| **Built, defective** | Present but wrong (overflow, stub title, radar tips at wrong radius) — still a **module** fix, not a vibe pass |

**Never** report a wiring gap as “we need a new design system” or “elements can’t have VFX.”

Prefer parallel surveys: (a) `docs/design/gui-lego/` pieces + themes + recipes, (b) FE
`features/gui-lego` / `ui/gui-lego` factories + fold, (c) catalogs under `data/tuning/`, (d) live
`/sheet` or surface API.

---

## 3. Bug list → module map (required table)

If the owner listed bugs, **every row becomes modules**:

```markdown
| # | Player bug | Module(s) | Bucket | Notes / file:line |
```

Add a **shared module map** (who reuses what). Prefer one `element-badge` for Condition + Elements
tab + HUD over a Condition-only twin.

Name surfaces correctly: Condition ≠ Derived ≠ Shield tab. Do not audit the wrong recipe.

---

## 4. CSS / layout / graph audit checklist

Run this against the live surface or draft+SPA side-by-side:

| Check | Fail means |
|---|---|
| Recipe grid/flex areas match draft landmarks | Layout module defective — fix recipe/CSS contract, don’t “nudge” with margin |
| Column min size ≥ largest child (SVG, radial box) | Overflow / “abundant scrollbar” |
| `overflow: auto` only where content must scroll | Default overflow on glance columns is often the bug |
| Gauge/radar owns a fixed box (e.g. 128×128) | Flex stole the gauge |
| SVG/canvas uses **paint hex**, not `fill="var(--token)"` alone | Black / invisible graph |
| Chart tips/markers sit on scaled values | “Graph not drawn” / wrong shape |
| Bars remain precision path for absolute ints; radar is glance | Radar-only absolute Standing |
| Theme `vfx` class applied when pack defines it | “No effect” with packs present = wiring |
| Player strings have no `.md` / channel ids | Stub leak |

Genre prior art (gauges, element color systems, radar failure modes, status HUD chips) still belongs
in the ideal — concrete numbers and failure modes, with sources.

---

## 5. Ideal deliverable

**Path:** `docs/architecture/<program>-ideal.md` (e.g. `condition-glance-ideal.md`). Always.
Never `SPEC.md`, never bare `tasks/plan.md` / `todo.md`.

Required sections:

1. **Which loop this extends**
2. **Load-bearing principles restated inline** (not links-only)
3. **What this is** (player language)
4. **What already exists** — built / wiring gap / real gap / built-defective, with `file:line`
5. **Owner bugs → module breakdown** (+ shared reuse map)
6. **Prior art** (outside repo)
7. **The shape** — chosen vs rejected (must reject “one CSS PR” / “god TSX”)
8. **Tunables** — catalog / theme pack / structural CSS called out
9. **What this deliberately does not decide**
10. **Open questions** — owner decisions only
11. **The real question** — usually **shape** (module set), not feasibility

---

## 6. Hand-off rules

- Stop at the ideal. **No** specs, plans, or code from this phase.
- Next step after owner answers open questions: `/spec` → capability map + **per-module** specs
  under `docs/architecture/<program>/spec-<module-id>.md`, plans in `tasks/<program>-plan.md` /
  `tasks/<program>-todo.md`.
- Queue: add or update `menu-refactor-queue.md` when a new surface enters the stream.
- Reference ideals that already exist (`gui-lego-ideal.md`, `actor-sheet-ideal.md`) — do not
  fork a second Lego grammar.

---

## 7. Red flags (abort and re-read)

- Starting at React / “fix the tab CSS”
- Treating Condition and Derived as one panel
- Inventing element colors when `element-catalog` + `themes/packs/element-*.json` exist
- Mute text chips while Derived already mounts themed `chip`
- Shipping `definitions.md` or “tap for Status tab” as product chrome
- Omitting empty shield/status because cold `/sheet` is null — missing piece, not missing data
- Declaring VFX “impossible” without checking theme pack `vfx` + binder
- Ban libraries for entry-chunk anxiety (GG-38 amended: split the chunk)
- Writing to `SPEC.md` or the bare plan/todo pair
- Ideal that only links principles instead of restating them

---

## 8. Incident this procedure exists for

**2026-09-10 — Condition glance.** Live numbers and Hub seed worked; the surface still looked
broken: mute element text (packs unused), stub Standing title, missing shield/status modules,
radar/layout overflow. Root cause: page-shaped FE work instead of recipe + pieces + themes.
Corrective ideal: `condition-glance-ideal.md`.
