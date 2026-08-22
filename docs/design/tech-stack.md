# Tech stack and gap register — the FE refactor

**Status:** decided. Stack choices T1–T4 and the migration order are settled under design authority (2026-08-22); reversing one is a normal decision, not a re-litigation. Governed by
[architecture/game-gui-principles.md](../architecture/game-gui-principles.md) and
[information-architecture.md](information-architecture.md).

Every number here was measured on this repo, not estimated. The method is stated so it can be
re-run.

---

## 1. What the design demands that the current stack cannot do

Not a wishlist — each row is a rule that is already binding and a stack that has no answer for it.

| Rule | Needs | Today | Verdict |
|---|---|---|---|
| GG-1 · GG-5 · GG-6 | A layer stack: push, pop, bands, one owner of visibility | 20 top-level routes | **cannot** |
| GG-19 | Focus trap and restore per layer | 1 keyboard handler in the whole app | **cannot** |
| GG-16 | A notification surface | **zero** toast surfaces exist | **cannot** |
| GG-8 | URL encodes stage + open layers | HashRouter, untyped search params | partial |
| GG-38 | Layers load what they need | one 2.77 MB chunk, zero splitting | **cannot** |
| GG-50 | 1 000-item collections | `array.map` | **cannot** |
| GG-31 | Nine declared transitions, incl. exit animation | CSS transitions, no presence | partial |
| GG-46 | Magnitudes formatted by unit family | string interpolation; **0** uses of `Intl` | **cannot** |
| — | i18n | nothing | **cannot** |
| GG-56 · invariant 9 | CJK + works offline | fonts fetched from `fonts.googleapis.com` | **breaks offline** |
| GG-21 · GG-30 | Verified a11y and contrast | no checks | **cannot** |
| GG-58 | Art fallback | 404 → broken image | **cannot** |

The offline one is worth pausing on. `src/theme/fonts.css` is a single line:

```css
@import url("https://fonts.googleapis.com/css2?family=Lilita+One&family=Nunito…");
```

Standalone-first says every feature must work with the game closed. It does not say "with the
internet up". A player on a disconnected machine currently loses the entire typeface system.

---

## 2. The measurement that drives most decisions

Built this repo with vendor chunking to attribute the payload. Reproduce by adding a
`manualChunks` function to a throwaway Vite config and running `vite build`.

| Chunk | gzip | share |
|---|---:|---:|
| **Phaser** | 381.4 KB | **53.5%** |
| **Charts** (recharts + d3) | 100.8 KB | **14.1%** |
| App code | 60.8 KB | 8.5% |
| React + DOM | 45.6 KB | 6.4% |
| **Map** (`@xyflow/react`) | 41.4 KB | 5.8% |
| Misc vendor | 33.2 KB | 4.7% |
| SignalR | 14.3 KB | 2.0% |
| Router | 14.1 KB | 2.0% |
| TanStack Query | 12.3 KB | 1.7% |
| CSS | 9.1 KB | 1.3% |
| **Total** | **712.9 KB** | |

**Three libraries are 74% of the payload, and none of them is needed on the home screen.** Phaser is
the lawn stage; charts are the Chronicle; the map is the world stage. In an overlay the player
toggles mid-match, that is boot cost paid on every launch for surfaces most sessions never open.

**Projected entry chunk after splitting: ~160 KB gz — a 4.4× reduction.**

---

## 3. Stack decisions

### 3.1 Keep

| Package | Why it stays |
|---|---|
| **React 18** + Vite + TypeScript | No reason to churn. React 19 later is a separate, boring upgrade |
| **TanStack Query** | Correct for server state, and it stays the only owner of it |
| **@microsoft/signalr** | The live channel is a locked decision |
| **Tailwind 4** + CVA + clsx + tailwind-merge | The token system is already expressed here, and `_kit/tokens.css` generates into it |
| **Phaser 4** | Locked (`fe-game-foundation.md`). Change: it loads with the lawn stage, not with the app |
| **Vitest + Testing Library + Playwright** | Fine. What changes is scope, not tooling |
| **react-router-dom** | Demoted, not removed — see §3.4 |

### 3.2 Add

| Package | For | ~gz | Why this one |
|---|---|---:|---|
| **zustand** | The layer stack, selection, settings | ~1 KB | UI state only. Entity data stays in Query, which keeps the DPLP lock intact — the rejected pattern there is *"Redux/Zustand as living entity SSOT for sprites"*, and a layer stack is not that |
| **@radix-ui/react-{dialog,popover,tooltip,tabs,select,toast}** | Focus trap, portal, dismiss, ARIA | ~15 KB used | Solves GG-19 and most of GG-21 correctly rather than by hand. Headless, so the token system stays in charge |
| **@lingui/{core,react,macro,cli}** | i18n — §4 | ~2 KB runtime | Compile-time catalogs, ICU, and an extraction artifact a guard can check |
| **@tanstack/react-virtual** | GG-50 | ~3 KB | Headless, coherent with Query already present |
| **@fontsource/**\* (or vendored woff2) | GG-56 + offline | assets | Kills the CDN dependency |
| **@axe-core/playwright** | GG-21 in CI | dev only | |
| **size-limit** *(or a 20-line script)* | GG-38 budget | dev only | |
| **rollup-plugin-visualizer** | Chunk attribution | dev only | The tool that produced §2 |

Total runtime addition: **≈ 21 KB gz** — against 500 KB removed from the entry path.

### 3.3 Remove

**`recharts` — 100.8 KB gz, 14% of the payload.** The design uses exactly four chart shapes:
horizontal bar (Chronicle "where growth came from"), sparkline, meter, and a zero-anchored diverging
bar. All four are already drawn on the plates in plain CSS and SVG, in about forty lines total. A
charting library that costs a hundred kilobytes to draw four rectangles is not paying for itself.

**`@xyflow/react` — 41.4 KB gz — dropped.** It is a node-editor library and our map is authored,
read-only content. Full reasoning in **T3** (§8); the short version is that plate 03 renders the whole
map in plain SVG and we would be paying for roughly 15% of a library. It may return later as a
*developer* authoring tool, where the bundle is unbudgeted.

### 3.4 Demote

**`react-router-dom`.** Routes stop deciding what is on screen. A `LayerStack` store owns visibility;
the router becomes a URL adapter that serialises `stage + open layers` into search params (GG-8) and
restores them on load. This is deliberately the *low-risk* option — TanStack Router would give typed
search params natively, but migrating routing and the layer model at once means two unfamiliar
failure modes in one change.

### 3.5 Deliberately not adding

| | Why not |
|---|---|
| **Storybook** | The plates are the design reference and the acceptance target. A second reference that can disagree with the first is a liability. Verify components against plate screenshots with Playwright instead |
| **Framer Motion / `motion`** | The nine transitions in [IA §10](information-architecture.md) are all reachable with CSS. Revisit only if springs are wanted |
| **Redux / MobX / Jotai** | Query owns server state, Zustand owns UI state. There is no third category |
| **MUI / Chakra / shadcn wholesale** | They bring opinions that would fight the token system. Radix primitives give the behaviour without the styling |
| **An icon library** | GG-58 needs an art *registry* with a fallback, not a set of generic glyphs |

### 3.6 One project-specific advantage worth using

The player runs this inside a **WebView2 (Chromium) overlay**, so the shipped path has a known,
modern engine. That legitimately unlocks:

- **View Transitions API** for M6/M7 stage travel — exactly the API for "one full-screen thing
  becomes another", and it removes the need for a presence library
- `@starting-style`, container queries, `:has()`

Guard them with `@supports` so a developer's browser degrades rather than breaks. Do **not** rely on
this for anything load-bearing — the dev path is a normal browser.

---

## 4. i18n

### 4.1 The distinction that has to be made first

**There are two text systems, and only one of them is i18n.**

| | Chrome text | Content text |
|---|---|---|
| Examples | "Bind a creature", "Tribute overdue", "1 of 2" | Creature names, almanac pedia strings |
| Source | Our catalog | **The game's own data — already Chinese** |
| Localised by | Lingui | The game/server, tagged with its locale |
| Count | ~250–350 today, roughly double after the redesign | Thousands, and it changes when the game patches |

Conflating them is the classic failure: a translation key per plant name, and a catalog that must be
regenerated every time the game updates. The FE's `stripTmpRichText` already exists precisely because
content text arrives with the game's own markup — that is the boundary, and it is already there.

### 4.2 The choice: Lingui

| Requirement | Lingui |
|---|---|
| Bundle (GG-38) | ~2 KB runtime; catalogs compile to plain objects and split per locale |
| Plurals and numbers | Full ICU MessageFormat — *"1 relic"* / *"2 relics"* without a hand-rolled rule |
| Readable source | Macros: `` t`Bind a creature` `` — not `t('sanctum.bind.cta')` |
| **Guardable** | `lingui extract` emits a catalog file; CI can fail on an unextracted or untranslated string. This is the property that matters most here, because it matches how this repo already enforces everything else |

Rejected: **react-i18next** (heavier, key-based source, completeness is hard to prove),
**react-intl** (ICU-correct but large), **Paraglide** (smallest, but weaker ICU and a younger
ecosystem than this should bet on).

### 4.3 English-only, shaped so the second language is cheap

```ts
// lingui.config.ts
sourceLocale: 'en',
locales: ['en', 'pseudo'],       // 'pseudo' is dev-only, never shipped
pseudoLocale: 'pseudo',
catalogs: [{ path: 'src/locales/{locale}/messages', include: ['src'] }]
```

Three things make "add Chinese later" a week instead of a project:

1. **Extraction from day one.** Every string goes through the macro as it is written. The catalog is
   a by-product, not a migration.
2. **A pseudolocale in dev.** It renders `[!!Bìnd á creátûre!!]` — which surfaces *hardcoded strings*
   and *layouts that cannot take +40% length* before a translator is ever hired. It is the cheapest
   possible test of GG-56, and it runs today with one locale.
3. **The CJK font stack already exists** (D7, `_kit/tokens.css`), and entity names already render
   through `--font-content`.

**What adding `zh-Hans` then actually costs:** `lingui extract` → translate ~600 strings → add the
locale to the Display tab → done. No string hunt, no layout surprises.

### 4.4 The unit formatter — not i18n, but built alongside it

GG-46 says a magnitude may not be rendered without its unit family. That needs a type, not a
convention:

> **Corrected 2026-08-22 — four families was wrong, and `resolverPoints` was the error.**
> [spec-magnitude-and-units.md §2-§3](spec-magnitude-and-units.md) verifies against code that
> `CombatDerivedReader.Power` has **exactly one call site** and never passes through `Sigmoid`
> ([OverlayCombatCalculator.cs:84-87,105](../../src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs)):
> **six of the twelve combat families are flat game units, six are sigmoid.** One bucket renders six
> families 10× wrong. The `effect?: {from, to}` shape above is also refused — it prints a percentage
> with no named reference, and the same affix is worth +0.73 pp at the lawn baseline and +2.50 pp at
> neutral, a 3.4× spread. **Nine classes, and the reference is a field:**

```ts
type UnitClass =
  | 'gameUnits' | 'gameUnitsPerSecond'
  | 'sigmoidPoints' | 'sigmoidMultiplierPoints' | 'statusPotencyPoints'
  | 'perMilleRatio' | 'milliseconds' | 'count' | 'flag'

type Magnitude = {
  unit:     UnitClass
  value:    number          // the frozen integer the engine holds. Never pre-formatted
  channel?: ChannelId       // required for gameUnits / sigmoid* — carries the arena
  op?:      'flat' | 'increased' | 'more'   // required for perMilleRatio: Increased sums, More multiplies
}

// An estimate never ships without naming what it is measured against.
type ContextRead = { reference: 'neutral' | { specimenId: string }; text: string }

function formatMagnitude(m: Magnitude, locale: string): string
```

There is no overload taking a bare `number`. That single omission *is* the GG-46 guard — a component
cannot print an unlabelled magnitude because there is no function that accepts one. Locale handling
inside it is `Intl.NumberFormat`, which the codebase currently uses **zero** times.

---

## 5. Gap register — what the design does not yet answer

The design covers every surface. These are the things a build will hit that no plate settles.

| # | Gap | Why it bites | Proposed answer |
|---|---|---|---|
| **G1** | **Where UI preferences persist** | Keybinds, volumes, filters. `localStorage` is per-browser; a save is per-player; the overlay is one machine | Resolved — **T4**. Device-scoped preferences in `localStorage`; view state in memory + the URL; nothing server-side |
| **G2** | **Error-boundary granularity** | GG-11 says the stage survives. A crashing panel must not take it down | One boundary per layer, one for the stage. A crashed layer renders a band-2 error and pops cleanly |
| **G3** | **Skeleton timing** | A skeleton that flashes for 80 ms is worse than no skeleton | Hold the previous frame; show loading only after ~200 ms |
| **G4** | **Art registry and fallback** | GG-58. Today: 404 and a broken image | Typed `artFor(entity)` returning a real asset or a generated placeholder carrying side, element and rarity |
| **G5** | **Contract type drift** | `web/spec.md` says hand-written TS from protocol docs. The surface just tripled | Resolved — **T1**. Types stay hand-written (the `decisions.md:67` lock holds); shared JSON fixtures emitted by the server test suite and consumed by the FE mocks make drift a failing test |
| **G6** | **Coverage configuration** | Scope is 9.3% of the FE and the *game* modules (`world`, `demons`, `expeditions`, `fusion`, `patron`) sit at **0%** | Rewrite the include list around the new structure; per-area thresholds |
| **G7** | **Offline degradation** | Standalone-first ≠ server-less. Everything comes from the server | State it plainly: without the server there is only the shell. The band-5 screen already designs that moment |
| **G8** | **Focus after stage travel** | GG-19 covers layers; travel is unspecified | Each stage declares its initial focus target, same as a layer |
| **G9** | **Does the dev tree ship?** | D4 says the tree ships and a toggle reveals it — but should a player build contain it at all | Resolved — **T2**. It ships. One artifact, lazy chunk, persisted toggle, default off |
| **G10** | **Audio assets** | GG-35 and the Sound tab exist; there is no audio pipeline | Out of scope for this refactor. The tab ships **disabled with its reason** rather than lying |
| **G11** | **Test queries** | The suite is `data-testid`-heavy. The new rules make roles and accessible names real | Migrate to role/name queries; keep `testid` only where semantics are genuinely absent. Free a11y regression coverage |
| **G12** | **Bundle budget numbers** | GG-38 demands "a ceiling" and never sets one | §6 |

---

## 6. Bundle plan and budget

| Chunk | Loads when | Budget (gz) |
|---|---|---|
| **entry** — shell, sanctum, kit, rail, bands, Query, SignalR, router | Always | **≤ 180 KB** |
| `layer-collection` — Creatures, Relics, Fusion | First open of any | ≤ 40 KB |
| `layer-world` — sector inspector, Expeditions, Pacts | First open | ≤ 30 KB |
| `layer-reference` — Almanac, Chronicle, chart primitives | First open | ≤ 30 KB |
| `stage-lawn` — Phaser + the projector | Entering the lawn | ≤ 400 KB |
| `stage-map` — SVG map renderer + pan/zoom | Entering the world | ≤ 25 KB |
| `dev` — the whole developer tree | Developer mode on | unbudgeted |

Measured baseline: **712.9 KB gz, one chunk.** Target entry: **≤ 180 KB gz** — a 4× reduction on the
path every launch pays.

**The check:** CI asserts the entry chunk ceiling *and* asserts that `phaser` does not appear in it
(`recharts` and `@xyflow` are removed outright, so their check is that they stay out of
`package.json`). A budget without the second half passes the day someone imports Phaser
at the top of a shared module.

---

## 7. Rules → tooling

| Rule | Check | Built from |
|---|---|---|
| GG-5 bands | ESLint `no-restricted-syntax` on `z-index` outside tokens | eslint |
| GG-1 · GG-11 stage survives | Mount-count assertion across open/close | Vitest |
| GG-7 reachability | Matrix test over (stage, layer) vs the three declared exceptions | Vitest |
| GG-6 · GG-19 Esc and focus | Push 3 / pop 3; focus trap and restore | Testing Library |
| GG-16 mutation feedback | Force 500 on every mutation, assert a band-4 surface | Vitest + MSW |
| GG-17 four states | Per data surface: loading / empty / error / locked | Vitest |
| GG-21 a11y | Per-layer scan | `@axe-core/playwright` |
| GG-23 vocabulary | Banned engine terms in player strings; dev tree allow-listed | script + Lingui catalog |
| GG-29 tokens only | No hex outside `src/theme` | script |
| GG-30 contrast | Token-pair matrix vs WCAG | script (already written for §A.2 of plate 00) |
| GG-36 viewports | Every layer at the declared widths, no h-scroll | Playwright |
| GG-38 budget | Entry ceiling + heavy-dep exclusion | size-limit |
| GG-46 units | No formatter overload takes a bare number | the type system |
| GG-50 volume | 10 / 100 / 1000 fixtures, assert rendered node count | Vitest |
| GG-56 CJK | Pseudolocale + a Chinese fixture through every text component | Playwright |
| i18n completeness | `lingui extract` produces no new untranslated entries | Lingui CLI |

Twelve of the sixteen are plain scripts or existing tools. That matches how this repo already
enforces its boundaries.

---

## 8. Decisions

Taken under design authority, 2026-08-22. Each records its reasoning so it can be overturned singly.

### T1 — Contract types stay hand-written; drift is caught by shared fixtures

`decisions.md:67` locks *"Web hand-writes TS from protocol docs."* Generating from OpenAPI would
break that lock and make the FE build depend on the server project, which it currently does not.

The real failure mode is not "the shape is wrong" — it is **drift**: the server changes and the
hand-written type, and the FE's test mocks, quietly stop matching. Generation does not fix that on its
own, because the mocks are a separate artifact.

**Decision.** Keep hand-written types. Add **one shared fixture set**: `FusionRpg.E2E.Tests` already
runs a `WebApplicationFactory`, so it serialises each response DTO to a committed JSON fixture; the
FE's Playwright and Vitest mocks read those same files. One source, two consumers, drift becomes a
failing test in whichever project changed first. No server API surface added, no lock broken.

### T2 — The developer tree ships in the player build

**Decision.** One artifact. The tree is lazy-loaded, gated by a persisted setting, default off.

There is nothing to protect: the server is on `127.0.0.1`, the data is the player's own save, and
gameplay cheats are already a documented feature of this project rather than something being hidden.
Two build paths would double the CI matrix and introduce the "works in dev, broken in prod" class of
bug for the exact surfaces used to diagnose bugs. And it has real support value — a player reporting a
problem can be asked to turn developer mode on and screenshot the status page.

It costs nothing on the entry path because it is its own chunk (§6).

### T3 — Drop `@xyflow/react`

**Decision.** Render the world map as SVG with a small pan/zoom hook.

`@xyflow/react` is a **node-editor** library. Our map is authored, read-only content: the player never
drags a sector, never draws an edge, never needs routing — the lanes are straight lines between fixed
positions. We would be paying 41.4 KB gz for roughly 15% of a library, and there is already evidence
of the cost of adopting its idioms: `LaneEdge.tsx:12-17` and `LegionMarker.tsx:73` carry a foreign
framework palette into the newest surface, against the token law.

Plate 03 draws the entire map — nodes, lanes, ownership, fog, legions, legend — in plain SVG.

**Where it may legitimately return:** a sector-graph *authoring* tool is a genuine node editor, and
that is a developer surface, where the bundle is unbudgeted. If we ever build one, xyflow belongs in
the `dev` chunk.

### T4 — Preferences are device-scoped; view state is session-scoped. Nothing is server-side

**Decision.** Two tiers, not three.

| Tier | Holds | Where | Survives |
|---|---|---|---|
| **Preference** | UI scale, text size, colour-blind assist, language, volumes, keybinds, pause-while-away, damage numbers, skip-reward-moments, developer mode | `localStorage` | Restart, and **the server being down** |
| **View state** | Search text, filters, sort, active tab, selection, the open layer stack | Memory + the URL (GG-8) | The session, per GG-51 |

The split is by *what the setting is about*. Text size and volume are about the person and their
screen — they are the same on every save. Filters are about what the player is doing right now, and
GG-51 already scopes them to the session, so persisting them buys nothing.

Nothing goes to the server, which has a useful consequence for **G7**: settings still work when the
server is unreachable, so the band-5 failure screen is a genuinely usable place to change the retry
behaviour or turn on developer mode.

*Known limitation, accepted:* the WebView2 overlay and a normal browser are separate origins' storage,
so a player using both would have two preference sets. That is a rare path and not worth a server
round trip to fix.

---

## 9. Migration order

Six phases. Ordered so the riskiest thing is proven first and the "this is a game" impression lands
second.

| # | Phase | Contains | Why here |
|---|---|---|---|
| **0** | **Foundations** | Tokens regenerated from `_kit`, fonts self-hosted, Lingui + pseudolocale, the `Magnitude` formatter, code splitting + budget, Radix + the layer stack + bands | Nothing ships visibly and everything depends on it. **Prove the layer stack over the *existing* pages first** — wrap a current page in a panel shell and assert the stage never unmounts. That de-risks GG-1/GG-11 before a single screen is redesigned |
| **1** | **Shell + the sweep** | Title, save select, Sanctum, rail, HUD — and move the nine diagnostic routes behind the developer gate | The sweep is nearly free and it is what makes the navigation stop reading as `AUDIT`. Old routes stay reachable inside the dev tree, so nothing is lost during migration |
| **2** | **Collection** | Creatures, Relics, Fusion, comparison, virtualization | Highest player value, and it exercises the entity ladder hardest — if the ladder is wrong, it is wrong here and cheap to fix before four more layers depend on it |
| **3** | **Stages** | Lawn re-hosted under the stage model with Phaser lazy-loaded; world map rewritten as SVG | Depends on phase 0's stage/layer split being real |
| **4** | **Reference** | Almanac, Chronicle, the four chart shapes, recharts removed | Lower risk, and the chart primitives are small once the tokens exist |
| **5** | **System + flows** | Settings, keymap, rebinding, loadout, deploy targeting, the offer, the first-run script | The first-run script is last on purpose — it should be authored against the game as it actually plays, not against a design |

Two rules for the whole migration: **the old route keeps working until its replacement lands**, and
**each phase adds its own enforcement checks from §7** rather than leaving them all to the end.
