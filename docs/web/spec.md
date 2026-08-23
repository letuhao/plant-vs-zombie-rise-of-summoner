# Web spec (v2)

Vite + React + TypeScript in `web/fusion-rpg-web`.

> **v2, 2026-08-22 — rewritten for the stage/layer model.** v1 specified an *audit UI*: twelve
> numbered screens behind `HashRouter`, an `AuditNav` sidebar, and a `HudBar` player picker. The
> `decisions.md` **Game GUI** row replaced that with stages and layers. This file is the **module
> spec** — what the code is and where it lives. The *rules* are
> [architecture/game-gui-principles.md](../architecture/game-gui-principles.md) (GG-1…GG-61), the
> *map* is [design/information-architecture.md](../design/information-architecture.md), the *stack*
> is [design/tech-stack.md](../design/tech-stack.md), and the *visual reference* is the eight plates
> in [design/README.md](../design/README.md).
>
> **Approved 2026-08-23.** All six spec-driven-development areas complete (§12–§14 close the gap a
> prior pass left open); World stage build excluded this phase, owner decision, §10.

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

**The World stage — owner decision, 2026-08-23, phase-scoped not permanent:** *"Map GUI is exclude
this phase, just keep it as is — we will have other plan for it because that is huge design, should
make new GUI solid foundation before we move to the map."* `#/world` stays on its pre-refactor route,
untouched, until its own dedicated plan exists. Every other stage, all nine player layers, and the
developer tree are unaffected — detail in
[`tasks/game-gui-plan.md`](../../tasks/game-gui-plan.md)'s World-exclusion section.

---

## 11. Tests

See [testing/web.md](../testing/web.md). Vitest + coverage, Playwright e2e against `vite preview`
with REST mocked from the shared fixtures.

v1's coverage scope was 9.3% of the FE with the *game* modules at 0%; v2 rewrites the include list
around `shell/`, `stages/`, `layers/`, `ui/`, `lib/bus/` and `i18n/`. The twenty rule-checks — band
lint, stage-persistence, reachability matrix, Esc/focus, mutation feedback, four-states, axe, banned
vocabulary, hex, contrast, viewport sweep, **shell-height fixtures**, bundle budget, unit families,
volume fixtures, CJK, catalog completeness — are listed in
[game-gui-principles.md §19](../architecture/game-gui-principles.md) (verified by direct count against
the current table, not carried over from the prior draft).

---

## 12. Commands

```powershell
cd web/fusion-rpg-web
npm run dev              # :5173, API :5088 in DEV
npm run build             # tsc --noEmit + vite build — player output
npm run preview           # :5173, serves the build
npm test                  # vitest run
npm run test:coverage     # vitest run --coverage
npm run test:e2e          # playwright, against vite preview :4173
npm run test:all          # coverage + build + e2e
```

Design plates (visual reference, not the app): `start docs/design/00-foundation.html`.

---

## 13. Code style

**No linter is configured yet** — `web/fusion-rpg-web` has no `.eslintrc`/`eslint.config.*` and no
Prettier config. Stated honestly rather than assumed: the conventions below are read off the shipped
code, not enforced by tooling. Adding one is in scope for the refactor (a natural home is the phase
that rewrites the coverage include list, §11), not a precondition for starting it.

`tsconfig.json` already carries real teeth: `strict`, `noUnusedLocals`, `noUnusedParameters`,
`noFallthroughCasesInSwitch`. Nothing here should propose loosening any of them.

**Representative sample**, [`AlmanacDumpPage.tsx`](../../web/fusion-rpg-web/src/features/almanac-dump/AlmanacDumpPage.tsx):

```tsx
import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getJson } from "@/lib/bus/rest";
import { Page } from "@/layouts/Page";
import { EmptyState, HelpText, Panel, Select, TextInput } from "@/ui";

type AlmanacDump = {
  side: string;
  typeId: number;
  displayName?: string | null;
};

export function AlmanacDumpPage() {
  const [side, setSide] = useState("");
  const dumps = useQuery({
    queryKey: ["almanacDumps", side],
    queryFn: () => getJson<{ items: AlmanacDump[] }>(/* … */)
  });
  // …
}
```

| Convention | Rule |
|---|---|
| Imports | `@/*` path alias for everything under `src/`; relative imports only for same-folder siblings |
| Exports | Named exports for components and hooks — `export function AlmanacDumpPage()`, never `export default` |
| Types | `type`, not `interface`, for props and data shapes; optional fields as `?: T \| null` when the API can genuinely omit or null them, not `?: T` alone |
| Strings | Double quotes |
| Data fetching | `useQuery`/`useMutation` only — **feature code never calls `fetch` or the SignalR client directly** (§1's own rule, restated because it is the one most likely to be broken by habit) |
| Formatting | 2-space indent, semicolons, trailing commas in multiline literals |

**Two rules this refactor adds, that the sample above predates and does not follow:**

- **No bare numeric magnitude.** Every rendered number is a tagged `Magnitude` through
  `formatMagnitude` (§6) — a raw `{value}` interpolated into JSX is a review rejection, not a style
  nit, because it is the literal mechanism GG-46 depends on.
- **No component builds its own modal, band, or z-index.** `PanelShell` / `DialogShell` / `Toast` /
  `SystemSheet` / `HudCluster` (§4) are the only band shells; a component setting `z-index` or
  rendering a fixed-position overlay directly is out of contract regardless of how it looks.
- **Translated text in a component goes through `useLingui()`'s `_`, never the bare `t` macro.**
  `const { _ } = useLingui(); _(msg\`...\`)` — not `t\`...\`` called directly. `t` compiles to a call
  against the global `i18n` singleton with no React subscription attached; a component that only uses
  it has nothing wiring it to `I18nProvider`'s context, so a locale switch elsewhere in the tree never
  triggers that component to re-render (ordinary children-prop-reference semantics, not a Lingui bug).
  `useLingui()` is a real context consumer and re-renders correctly. Found live in T6 (game-gui-todo.md)
  by switching to the dev pseudolocale and watching a `t`-macro string sit untranslated — fixed in
  `LawnStage.tsx`, and recorded here so it isn't rediscovered per-component.

---

## 14. Boundaries

Repo-wide boundaries (git hands-off, no watermarks, hard architectural boundaries) are
[AGENTS.md](../../AGENTS.md)'s and apply unchanged here. These are the ones specific to this program.

**Always:**
- Bind every magnitude through `formatMagnitude` (§6) — never interpolate a raw number.
- Route every mutation through the data bus (§5) and render its band-4 result, success or failure
  (§9) — a silent mutation is a defect, not an oversight.
- Give every shell a bound and let its body scroll (GG-61) — copy the `PanelShell` contract, never a
  bespoke fixed-height div.
- Cite the governing GG rule or design document in review when a layout decision is non-obvious —
  the nine detail-design documents and the responsive/scroll audit exist so a reviewer can check the
  claim, not just trust it.

**Ask first:**
- Adding a dependency to the entry bundle (§7's budget is a CI gate, not a guideline).
- Introducing a twelfth band or a stacking mechanism outside the six declared bands (§2).
- Any change to the sealed contract types in `lib/bus/` — item 2 of `game-gui-map.md`'s open
  questions, still unresolved as of the responsive/scroll audit.
- Configuring a linter/formatter for the first time (§13) — a real decision (which rules, `strict`
  presets or hand-picked) that should be made once, deliberately, not as a side effect of one PR.

**Never:**
- A second modal/overlay implementation outside the five band shells (§4).
- A raw hex colour outside `src/theme/` (§3, CI-guarded).
- Engine vocabulary (`typeId`, `Intent`, `UniqueActor`, `mods_json`) on a player-facing surface (§9)
  — the developer tree (§8) is the only place it is correct.
- A route added for something that is a layer, or a layer's content duplicated as a route (§2's whole
  point, and the failure the entire v1→v2 rewrite exists to reverse).

---

## 15. Success criteria

**Scoped to this phase — World excluded (§10).** Checked against
[`tasks/game-gui-todo.md`](../../tasks/game-gui-todo.md)'s seven checkpoints, which are the
executable form of this list; this is what "done" means in prose.

1. **The reachability matrix passes** for every (stage, layer) pair **except World** — Sanctum, Lawn
   and Battle each reach all nine player layers, or the pair is one of the three permanent
   behavioural exceptions (IA §6, D8). Automated, not eyeballed (Checkpoint F).
2. **GG-11 holds**: opening a band-2 panel over the live Lawn stage does not remount, reset, or drop
   the Phaser `Game` instance — asserted by reference identity, not by the screen looking right
   (Task 2, the keystone, checked before anything else is built on top of it).
3. **Zero horizontal scroll and zero shell-height violations** at the declared viewport contract
   (1280×720 / 1440×900 / 1920×1080, GG-36) across every stage and every layer — not just the ones
   this session's plates happened to demonstrate.
4. **The entry bundle holds its budget** (≤180 KB gz, §7) with Phaser absent from it, verified by CI,
   not by a one-time measurement that goes stale.
5. **No bare numeric magnitude reaches the DOM.** Every rendered number is a tagged `Magnitude`
   through `formatMagnitude` (§6, §13) — enforced by the unit-family guard, not by review vigilance.
6. **Every mutation reports its result.** Success or failure, band-4, including "nothing changed" —
   zero silent mutations, asserted by forcing failure on every mutation path in tests (§9).
7. **All nine detail-design documents' components exist as real code**, not just as design plates —
   the item card's eleven blocks, the derived-stat sheet's six states, the shield stack's per-layer
   readout, the action bar's five typed refusals, traceable back to the `spec-*.md` document that
   designed each one.
8. **Old routes redirect**, except `/world`, which is deliberately untouched (§10). A 404 on any
   pre-refactor route is a regression, not an acceptable gap.
9. **The nine diagnostic routes are behind the gated developer tree**, default off, reachable on
   `` ` `` — and absent from player navigation (§8, Checkpoint at Task 12).

**What this list does not claim.** It is not "the game is finished" — Battle has no live backend yet
(§ open question 3 in the plan), the World stage is explicitly deferred, and several components (the
gap board, the charm pouch UI, band-3 dialog load-testing) were named in the detail-design documents
as designed-but-not-built-this-phase. Success here means the *foundation* is solid enough that the
World stage, Battle's real backend, and the deferred components each become an addition to a working
shell, not a redesign of it.
