# Web spec (v2)

Vite + React + TypeScript in `web/fusion-rpg-web`.

> **v2, 2026-08-22 — rewritten for the stage/layer model.** v1 specified an *audit UI*: twelve
> numbered screens behind `HashRouter`, an `AuditNav` sidebar, and a `HudBar` player picker. The
> `decisions.md` **Game GUI** row replaced that with stages and layers. This file is the **module
> spec** — what the code is and where it lives. The *rules* are
> [architecture/game-gui-principles.md](../architecture/game-gui-principles.md) (GG-1…GG-60), the
> *map* is [design/information-architecture.md](../design/information-architecture.md), the *stack*
> is [design/tech-stack.md](../design/tech-stack.md), and the *visual reference* is the eight plates
> in [design/README.md](../design/README.md).

**Players:** `npm run build` output is copied to `FusionRpg.Server/wwwroot`. The server hosts it at
`http://127.0.0.1:5088`. The UI uses the **same origin** (relative `/api` and `/hub/rpg`). No Node on
the player PC. It also runs inside the launcher/injector **WebView2 overlay**, sized to the game
window — so the viewport is the player's game resolution, not a browser they can resize.

**Developers:** `npm run dev` on port 5173 (API `http://127.0.0.1:5088` in DEV).

Hand-written TS types matching [protocol/rest.md](../protocol/rest.md) — no OpenAPI generator
(`decisions.md` **Contracts** row). Drift is caught by shared JSON fixtures emitted by
`FusionRpg.E2E.Tests` and consumed by the FE mocks (tech-stack **T1**).

---

## 1. Architecture

Six layers. Feature code never calls `fetch` or SignalR, never sets a stacking order, and never
renders a bare number.

| Layer | Location | Role |
|---|---|---|
| **Shell** | `src/app/` | Providers (Query, Hub, i18n, ErrorBoundary), the router as a **URL adapter only** |
| **Stage + layer runtime** | `src/shell/` | The `LayerStack` store, the six band shells, the stage host, the verb table |
| **Theme** | `src/theme/` | Tokens **generated from** `docs/design/_kit/tokens.css` — not hand-edited alongside it |
| **Kit** | `src/ui/` | Primitives and the **entity ladders** (§4) |
| **Data bus** | `src/lib/bus/` | REST snapshots + SignalR live + query keys + mutations + event ring |
| **Game island** | `src/game/` | Phaser-only Dual-Plane Lawn Projector — SSOT [architecture/fe-game-foundation.md](../architecture/fe-game-foundation.md) |

```text
src/
  app/          providers, router adapter, root error boundary
  shell/        LayerStack, band shells, stage host, keymap, toasts
  theme/        tokens.css (generated), fonts (self-hosted)
  i18n/         lingui config, catalogs, the Magnitude formatter
  ui/           primitives + entity ladders (atom, actor, container, …)
  stages/       sanctum, world, lawn, battle
  layers/       creatures, relics, fusion, pacts, expeditions, almanac, chronicle
  dev/          the developer tree — lazy, gated, off by default
  lib/bus/      types, rest, keys, queries, mutations, hub, log-store
  game/         Phaser island — EventBus, scenes, entities, systems, fx
```

**`features/` is gone.** A feature was a route; there are no longer routes to be a feature of. Code
lives under `stages/`, `layers/` or `dev/` according to what band it is on.

---

## 2. Stages and layers

Four stages, one at a time. Eight player layers, openable over any of them. One gated developer tree.
Full catalog, keymap and reachability rules:
[design/information-architecture.md](../design/information-architecture.md).

| Stage | Route | Owns |
|---|---|---|
| **Sanctum** | `#/sanctum` | Home. Default. Every run returns here |
| **World** | `#/world` | Sector graph, lanes, legions, fog |
| **Lawn** | `#/lawn/{matchKey}` | The Phaser canvas. Created on **entering the stage**, destroyed on **leaving it** — never on a layer opening |
| **Battle** | `#/battle/{id}` | Grid, initiative, action bar |

| Layer | Key | Layer | Key |
|---|---|---|---|
| Creatures | `C` | Almanac | `A` |
| Relics | `R` | Chronicle | `H` |
| Fusion | `F` | Sector inspector | — |
| Pacts | `P` | System | `Esc` on an empty stack |
| Expeditions | `E` | Developer | `` ` `` (when enabled) |

**Bands.** Six stacking tiers as CSS custom properties; **nothing outside that list may set a
stacking order.** Shell −1 · Stage 0 · HUD 100 · Panel 200 · Dialog 300 · Toast 400 · System 500.

**The URL encodes the stack, not a screen** — `#/sanctum?panel=creatures&sel=spec-42`. Following one
cold restores the stage first, then opens the layer over it. Back and Esc do the same thing.

---

## 3. Theme

Tokens live only in `theme/tokens.css`, **generated from** `docs/design/_kit/tokens.css`. Components
use Tailwind keys or `var(--*)`; **no raw hex outside `src/theme/`** (guarded).

Changed in v2, measured not eyeballed:

| Token | v1 → v2 | Why |
|---|---|---|
| `--warn` | `#c4923a` → `#d8a94f` | 7.17 on panel |
| `--bad` | `#c45c5c` → `#d97b7b` | 5.22 on panel (was 3.71) |
| `--bad-solid` | *new* `#a33c3c` | Ink on a filled danger button reads 5.36 (was 3.48) |
| `--border-control` | *new* `#7a6d59` | 3.07 — controls previously inherited the 1.53 hairline, failing WCAG 1.4.11 |
| `--font-content` | *new* | Entity names, CJK-capable; the display face is chrome-only |
| `--cjk` | *new* | Fallback stack on every family |
| bands | *new* | Six `--band-*` values |

**Fonts are self-hosted.** v1 imported from `fonts.googleapis.com`, which broke the offline promise
(invariant 9). CJK relies on system faces via `--cjk`.

**Motion** is the nine declared transitions M1–M9 in
[design/information-architecture.md §10](../design/information-architecture.md); `prefers-reduced-motion`
collapses M1–M8 and keeps M9.

---

## 4. The kit

Primitives: `Button`, `TextInput`, `NumberInput`, `Select`, `Checkbox`, `Field`, `Stepper`, `Toggle`,
`Tabs`, `Segmented`, `Pager`, `Meter`, `Delta`, `Tag`, `Frame`, `Tooltip`, `Banner`, `EmptyState`,
plus the four required states (loading / empty / error / locked) for every data surface.

Band shells: `PanelShell`, `DialogShell`, `Toast`, `SystemSheet`, `HudCluster`. **Feature code never
builds its own modal.**

**Entity ladders** — the part v1 had no concept of. Each domain entity has the same rungs
(token → chip → row → card → panel → editor), so two surfaces showing the same entity cannot
diverge:

`Atom` · `Container` · `Actor` · `Status` · `Element` · `Channel` · `Resource` · `Power` · `Sector` ·
`Contract` · `Run`

Coverage matrix: [design/00-foundation.html](../design/00-foundation.html) §G.

> **⚠ Eleven is not the whole list.** The design's entity extraction missed
> [`architecture/item/`](../architecture/item/) and [`architecture/action/`](../architecture/action/),
> so **29 entities have no ladder** — the item card and its eleven blocks, sockets, sets, affixes,
> rarity rungs, requirements, enhancement, inventory, crafting, actions, targeting, usability, the
> shield stack, and the derived-stat sheet. Critically,
> [`item/ssot-presentation.md`](../architecture/item/ssot-presentation.md) is the **presentation
> contract for every number a player reads** — the nine-value unit ledger, the line grammar, the
> roll-quality split — and it cedes *"UI layout, component code, CSS, routing"* to **this file**. That
> seam is unclaimed from this side. Register and the nine detail-design documents owed:
> [design/gap-audit-2026-08-22.md](../design/gap-audit-2026-08-22.md).

Control clusters: stage transport, map controls, filter bar, selection tray, action bar, menu rail.

---

## 5. Data bus

Unchanged from v1 in shape.

- **REST** = snapshot truth. **SignalR** = live bus (`HubProvider`, `Join("web")`); `Health` updates
  the query cache, `Event`/`EventBatch` append to an 800-event ring and invalidate by `kind`.
- **Hub connected:** no 3s poll-everything. **Hub down:** poll health 5s, lists 10s.
- Query owns **server** state. The `LayerStack` store owns **UI** state. There is no third category,
  and neither owns the other's.

---

## 6. i18n

English only at launch, shaped so a second locale is cheap. **Lingui** — compile-time catalogs, ICU
plurals, ~2 KB runtime, and an extraction artifact CI can fail on.

**Two text systems, and only one is i18n:**

| | Chrome text | Content text |
|---|---|---|
| e.g. | "Bind a creature", "1 of 2" | Creature names, almanac pedia strings |
| Source | our catalog | the **game's own data — already Chinese** |
| Handled by | Lingui | server data, locale-tagged; `stripTmpRichText` is the existing boundary |

A **dev-only pseudolocale** renders `[!!Bìnd á creátûre!!]`, surfacing hardcoded strings and layouts
that cannot absorb +40% length before any translator exists.

**Magnitudes are not i18n but ship with it.** `formatMagnitude(m, locale)` takes a tagged
`Magnitude` with a `UnitClass` — **nine of them**: `gameUnits` · `gameUnitsPerSecond` ·
`sigmoidPoints` · `sigmoidMultiplierPoints` · `statusPotencyPoints` · `perMilleRatio` ·
`milliseconds` · `count` · `flag`. **There is no overload accepting a bare number**, which is what
enforces GG-46.

*Corrected 2026-08-22: this read `gameUnits · resolverPoints · permille · ms`. `resolverPoints`
merged six flat game-unit families with six sigmoid ones and would have rendered half of them 10×
wrong. Full ledger, each class verified at its consumer in `src/`:
[design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md).*

---

## 7. Bundle

Measured v1 baseline: **712.9 KB gz in one chunk**, zero splitting. Phaser was 53.5%, charts 14.1%.

| Chunk | Loads when | Budget (gz) |
|---|---|---|
| entry | always | **≤ 180 KB** |
| `layer-collection` / `layer-world` / `layer-reference` | first open | ≤ 40 / 30 / 30 KB |
| `stage-lawn` (Phaser) | entering the lawn | ≤ 400 KB |
| `stage-map` | entering the world | ≤ 25 KB |
| `dev` | developer mode on | unbudgeted |

CI asserts the entry ceiling **and** that Phaser is absent from it.

**Removed:** `recharts` (100.8 KB gz for four chart shapes drawn in ~40 lines of SVG) and
`@xyflow/react` (a node-editor library for a read-only map). **Added:** zustand, Radix primitives,
Lingui, `@tanstack/react-virtual` — ≈21 KB gz total.

---

## 8. Developer tree

Ships in the player build, lazy-loaded, behind a persisted setting, **default off**, reachable on
`` ` ``. Absent from player navigation. Governed by GG-40–GG-42: density beats polish, engine
vocabulary is correct there, but it still obeys the stack, Esc, focus and volume rules.

Holds what v1 called screens: status, log, events, metrics, activity, raw runs, types, icons, almanac
dump, cheats, sim, tuning, perf.

---

## 9. UX

A game, not an audit tool. The player lands on a **place** (the Sanctum), opens **layers** over
whatever they are doing, and never navigates to look at something. Player surfaces use the fiction's
words — never `typeId`, `Intent`, `UniqueActor`, `mods_json`, `ingest queue`.

Failures are reported: every mutation gets a band-4 result, success or failure, and a failure says
what changed — including "nothing". If the API is down, the shell still runs; settings are
`localStorage` and keep working.

---

## 10. Out of scope

Auth, per-type stat editors, additional locales at launch, audio assets, illustration beyond framed
chrome, and the sector-graph authoring tool.

---

## 11. Tests

See [testing/web.md](../testing/web.md). Vitest + coverage, Playwright e2e against `vite preview`
with REST mocked from the shared fixtures.

v1's coverage scope was 9.3% of the FE with the *game* modules at 0%; v2 rewrites the include list
around `shell/`, `stages/`, `layers/`, `ui/`, `lib/bus/` and `i18n/`. The twenty rule-checks — band
lint, stage-persistence, reachability matrix, Esc/focus, mutation feedback, four-states, axe, banned
vocabulary, hex, contrast, viewport sweep, bundle budget, unit families, volume fixtures, CJK,
catalog completeness — are listed in
[game-gui-principles.md §19](../architecture/game-gui-principles.md).
