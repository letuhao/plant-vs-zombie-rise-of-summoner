# Tasks: Game GUI refactor

Plan: [game-gui-plan.md](game-gui-plan.md) · Map: [../docs/architecture/game-gui-map.md](../docs/architecture/game-gui-map.md)
Rules: [GG-1…GG-60](../docs/architecture/game-gui-principles.md) · Plates are the visual acceptance reference.

All commands run from `web/fusion-rpg-web` unless stated. Baseline: **292 tests / 36 files green**;
no task may reduce that.

---

## Phase 0 — Prove the architecture

### Task 1: Layer stack, bands and the two shells
**Description.** The mechanism only, unit-tested in isolation: a `LayerStack` store (push/pop/replace,
one owner of visibility), the six band tokens, and `PanelShell` + `DialogShell` built on Radix.
Also rewrites the coverage include list around the new structure (gap G6) so later tasks inherit it.

**Acceptance:**
- [x] `LayerStack` supports push, pop, popAll, and reports the top layer; no component reads it directly except the shells — guarded by `bandGuard.scanForLayerStackImports`, proven clean against the real tree
- [x] Six band values exist as CSS custom properties; a lint fails on any `z-index` or `z-*` outside them — `--band-shell/-hud/-panel/-dialog/-toast/-system` in `theme/tokens.css`; `bandGuard.scanForStrayZIndex` migrated the three pre-existing offenders (`LawnPage.tsx`, `LawnStatsModal.tsx`, `ConfirmDialog.tsx`) to the new `.band-*` classes rather than allowlisting them
- [x] `PanelShell` and `DialogShell` render, trap focus, and restore focus to their opener — note: Radix's own `onCloseAutoFocus` targets its internal `Dialog.Trigger`, which these fully-controlled shells never render, so restore is done explicitly (capture `document.activeElement` on the render that flips `open` true, refocus it in `onCloseAutoFocus`) — see the shells' own comments

**Verify:**
- [x] `npm test` — 25 new tests (`layerStack.test.ts`, `bandGuard.test.ts`, `shells.test.tsx`); full suite 348/348 green, no regressions (baseline 292)
- [x] `npm run build` — `tsc --noEmit` clean, `vite build` clean
- [x] Band lint fails on a deliberately added `z-index: 5` — proven as a permanent fixture-based regression test (`bandGuard.test.ts` "fixtures" suite), not a one-off manual check

**Dependencies:** None · **Files:** `src/shell/layerStack.ts`, `src/shell/PanelShell.tsx`, `src/shell/DialogShell.tsx`, `src/theme/tokens.css`, `vite.config.ts` (coverage) · **Scope:** M

---

### Task 2: Open a panel over the live lawn without disturbing Phaser ⚠ keystone
**Description.** Host the existing `LawnPage` as the **lawn stage** and open a `PanelShell` over it.
This is the GG-11 proof and the single highest-risk item in the initiative — done second, on purpose,
against the *existing* page rather than a redesigned one.

**Acceptance:**
- [x] Opening and closing a panel leaves the Phaser `Game` **instance identical by reference** — `createLawnGame` mocked and asserted called exactly once across an open→close cycle, same returned reference both times
- [x] The `LawnViewModel` is not reset and no hub re-subscription occurs — `LawnGameHost`'s own mount effect (`[]` deps) never re-runs because `LawnPage`'s component instance is never recreated; `LawnStage.test.tsx` asserts `getStageMountCount("lawn") === 1` through the whole cycle
- [x] The canvas keeps rendering behind the scrim — confirmed live: the projector grid, sidebar and nav are all still present in the DOM (marked `aria-hidden` by Radix's dismissable layer, not removed) while the panel is open, verified via accessibility snapshot in a real browser
- [x] Leaving the lawn stage still runs the full destroy checklist — `LawnStage.test.tsx`'s second test unmounts `LawnStage` and asserts `destroyLawnGame` was called

**Verify:**
- [x] `npm test` — `src/stages/lawn/LawnStage.test.tsx`, 2 new tests; full suite 350/350 green
- [x] Manual, live in a real browser (`npm run dev`, no server/injector needed for this proof — GG-14 holds, the shell renders through the "SignalR disconnected" state): navigated to `/lawn`, clicked **Board panel**, confirmed the projector/inspector/nav stayed intact behind the panel via a11y snapshot, pressed **Escape**, confirmed the panel closed and focus returned to the trigger button (`document.activeElement` verified as `lawn-stage-open-panel` via `evaluate_script`) — screenshots at 1440×900 and at the declared 1280×720 floor (no horizontal overflow, shell stays within `min(720px,82vh)`)
- [x] `npm run build` — clean

**Dependencies:** T1 · **Files:** `src/stages/lawn/LawnStage.tsx`, `src/features/lawn/LawnGameHost.tsx`, `src/shell/stageHost.tsx`, one test · **Scope:** M

---

### Task 3: Esc semantics, focus and the verb table
**Description.** One keymap module owning the global verbs. Esc pops exactly one layer and opens
System on an empty stack. Tab cycles within the top layer only.

**Acceptance:**
- [ ] Push three layers, press Esc three times: they pop one at a time, stage untouched
- [ ] Esc on an empty stack opens the System layer
- [ ] `F10` is never handled by the app; a guard fails if any handler binds it
- [ ] Every global verb is registered in one module; a lint fails on `onKeyDown` for a global verb elsewhere

**Verify:**
- [ ] `npm test` — push-3/pop-3, focus trap and restore per shell
- [ ] Keymap lint fails on a deliberately added stray handler

**Dependencies:** T1 · **Files:** `src/shell/keymap.ts`, `src/shell/useGlobalKeys.ts`, tests · **Scope:** S

---

### ✅ Checkpoint A — the architecture is real
- [ ] 292+ tests green, build clean
- [ ] A panel opens over a live board and Phaser survives it
- [ ] Esc/stack/focus behave per GG-6, GG-18, GG-19
- [ ] **Review with owner before proceeding** — if GG-11 failed, stop here

---

## Phase 1 — Contract and formatting spine

### Task 4: The sealed view contract and its adapter
**Description.** Author the FE view contract in full, including fields nothing fills yet, wrapped in
`Pending<T>`. Add the DTO→view adapter. Components bind here and never to a REST DTO.

**Acceptance:**
- [ ] `Pending<T>` has three states — `known` / `absent` / `pending` — and `pending` carries a non-empty reason
- [ ] Contract covers all eleven entities in the ladder matrix
- [ ] A check fails if any `pending` reason is empty or missing
- [ ] A check fails if any file under `stages/`, `layers/` or `ui/` imports a REST DTO type

**Verify:**
- [ ] `npm test` — adapter tests for each state; the two guards fail on deliberate violations
- [ ] `npm run build`

**Dependencies:** None · **Files:** `src/contract/types.ts`, `src/contract/pending.ts`, `src/contract/adapt.ts`, guard script, tests · **Scope:** M

---

### Task 5: Shared fixtures from the server test suite
**Description.** `FusionRpg.E2E.Tests` serialises each response DTO to a committed JSON fixture; the
FE Playwright and Vitest mocks read those same files. One source, two consumers (T1 of the stack doc).

**Acceptance:**
- [ ] Each REST response DTO emits a fixture on test run
- [ ] FE mocks import the fixtures rather than inline literals
- [ ] A server-side shape change fails a test in whichever project changed first

**Verify:**
- [ ] `dotnet test tests/FusionRpg.E2E.Tests`
- [ ] `npm run test:e2e`
- [ ] Manual: change a DTO field, confirm a test goes red

**Dependencies:** None · **Files:** `tests/FusionRpg.E2E.Tests/ContractFixtureTests.cs`, `web/fusion-rpg-web/e2e/fixtures/*`, `src/test/mocks.ts` · **Scope:** M

---

### Task 6: i18n and the magnitude formatter
**Description.** Lingui with an English catalog and a dev-only pseudolocale; the `Magnitude` tagged
type and `formatMagnitude`. **No overload accepts a bare number** — that omission is the GG-46 guard.

**Acceptance:**
- [ ] `t` macro in use; `lingui extract` produces a catalog with no untranslated entries
- [ ] Pseudolocale renders `[!!…!!]` in dev and is absent from the production build
- [ ] `formatMagnitude` handles `gameUnits` / `resolverPoints` / `permille` / `ms`, with `resolverPoints` able to render an effect range ("7.6% → 26.9%")
- [ ] No exported function formats a magnitude from a bare `number`

**Verify:**
- [ ] `npm test` — golden output per unit family, plus a CJK fixture through the formatter
- [ ] `npx lingui extract` clean
- [ ] `npm run build` — confirm pseudolocale is tree-shaken out

**Dependencies:** None · **Files:** `lingui.config.ts`, `src/i18n/*`, `src/i18n/magnitude.ts`, tests · **Scope:** M

---

### ✅ Checkpoint B — the spine
- [ ] Contract sealed; every `pending` field explains itself
- [ ] Fixtures shared; drift is a failing test
- [ ] Magnitudes cannot be printed without a unit family
- [ ] Tests green, build clean

---

## Phase 2 — Tokens and the first ladder

### Task 7: Token layer and self-hosted fonts
**Description.** Generate `src/theme/tokens.css` from `docs/design/_kit/tokens.css`; self-host the
Latin faces; add the CJK fallback stacks and `--font-content`. Removes the `fonts.googleapis.com`
import, which breaks the offline promise today.

**Acceptance:**
- [ ] Tokens are generated, not hand-maintained; a check fails if they drift from `_kit`
- [ ] No network request leaves the app on load
- [ ] Contrast test passes over the token pair matrix at WCAG AA / 3:1 for controls
- [ ] A guard fails on any hex literal outside `src/theme/`

**Verify:**
- [ ] `npm test` — contrast matrix, hex guard
- [ ] `npm run build`; load with the network blocked and confirm the typeface system holds

**Dependencies:** None · **Files:** `scripts/gen-tokens.mjs`, `src/theme/tokens.css`, `src/theme/fonts.css`, tests · **Scope:** S

---

### Task 8: The Actor ladder, end to end
**Description.** One entity all the way up the ladder — token → chip → row → card → panel — bound to
the contract, in all four states, rendering real magnitudes. This proves the ladder before ten more
entities depend on its shape.

**Acceptance:**
- [ ] Five rungs render from one contract type with no forked components
- [ ] Loading, empty, error and locked states exist for every rung that shows data
- [ ] Matches plate 00 §D.2 visually at 1440, 1024 and 800 px
- [ ] A CJK name renders without breaking any rung

**Verify:**
- [ ] `npm test` — four-states matrix, CJK fixture
- [ ] `npm run test:e2e` — viewport sweep, no horizontal scroll
- [ ] Manual: compare against plate 00 §D.2

**Dependencies:** T4, T6, T7 · **Files:** `src/ui/actor/*`, tests · **Scope:** M

---

### ✅ Checkpoint C — the ladder holds
- [ ] Actor renders at five densities from one type
- [ ] Four states everywhere; CJK safe; contrast passing
- [ ] **Review against plate 00 §D.2 with owner**

---

## Phase 3 — The first real player path

### Task 9: Sanctum stage, rail and HUD
**Description.** The home stage: focus card, creature strip, map table, tonight list, run prompt.
The rail renders from **unlock state**, not a constant list (GG-44). Index route becomes `#/sanctum`.

**Acceptance:**
- [ ] `#/` redirects to `#/sanctum`; `#/status` still resolves until T12
- [ ] Rail entries render active / available / badged / locked from state; locked entries say what unlocks them
- [ ] The focus card selects its content from state and is never an empty box
- [ ] HUD carries identity, souls, XP and the menu affordance — no API address, no injector dot

**Verify:**
- [ ] `npm test` — rail state matrix, focus-card selection
- [ ] `npm run test:e2e` — first paint contains a playable affordance (GG-2)
- [ ] Manual: compare against plate 01 §C

**Dependencies:** T3, T8 · **Files:** `src/stages/sanctum/*`, `src/shell/Rail.tsx`, `src/app/routes.tsx`, tests · **Scope:** M

---

### Task 10: Creatures layer over the Sanctum
**Description.** The first complete player path: press `C`, the roster opens over the sanctum, the
sanctum stays mounted, Esc returns. Replaces `#/roster`, which redirects.

**Acceptance:**
- [ ] `C` opens, Esc closes, the Sanctum is not unmounted at any point
- [ ] Cards show art, name, side, element and two numbers — **no `typeId` input anywhere**
- [ ] `#/sanctum?panel=creatures&sel=…` deep-links cold and restores stage-then-layer
- [ ] `#/roster` redirects

**Verify:**
- [ ] `npm test` — mount-count assertion, URL round-trip
- [ ] `npm run test:e2e` — deep-link cold start
- [ ] Vocabulary guard: no banned engine terms in player strings

**Dependencies:** T9 · **Files:** `src/layers/creatures/*`, `src/app/routes.tsx`, tests · **Scope:** M

---

### Task 11: Toasts and mutation feedback
**Description.** The band-4 surface the app has never had. Every mutation reports — success, failure,
and rejection — and a failure says what changed, including "nothing".

**Acceptance:**
- [ ] Every mutation in `lib/bus/mutations.ts` produces a band-4 result
- [ ] A forced 500 on each produces a failure toast naming the entity and stating nothing changed
- [ ] Toasts never block input and never occlude a dialog

**Verify:**
- [ ] `npm test` — force failure on every mutation, assert a band-4 surface each time
- [ ] Manual: stop the server, attempt a deploy, read the toast

**Dependencies:** T1, T10 · **Files:** `src/shell/Toasts.tsx`, `src/lib/bus/mutations.ts`, tests · **Scope:** S

---

### ✅ Checkpoint D — it reads as a game 🎯
- [ ] Boots to a place, not a diagnostic
- [ ] A layer opens over it from a key and closes back
- [ ] Failures are visible
- [ ] **Owner review — this is the milestone that answers the original complaint**

---

## Phase 4 — Sweep and budget

### Task 12: Developer tree and the nine-route sweep
**Description.** Build the gated tree and move `status`, `stats`, `pvz-activity`, `icon-dump`,
`almanac-dump`, `cheats`, `sim`, `log` and raw `runs` into it. Off by default, backtick to open.

**Acceptance:**
- [ ] A persisted setting gates it; default off; `` ` `` opens it when enabled
- [ ] All nine surfaces reachable inside it; their old routes redirect
- [ ] It is absent from player navigation
- [ ] It obeys the stack, Esc, focus and volume rules; presentation rules do not apply

**Verify:**
- [ ] `npm test` · `npm run test:e2e` — nine redirects, gate off by default
- [ ] Vocabulary guard allow-lists this tree only

**Dependencies:** T3 · **Files:** `src/dev/*`, `src/app/routes.tsx`, tests · **Scope:** M

---

### Task 13: Code splitting and budgets
**Description.** Split by stage and layer; assert the entry ceiling; remove `recharts` and
`@xyflow/react` from `package.json`. **Runs after T19** so the chart primitives exist first.

**Acceptance:**
- [ ] Entry chunk ≤ 180 KB gz (baseline: 712.9 KB, one chunk)
- [ ] Phaser is absent from the entry chunk
- [ ] `recharts` and `@xyflow/react` are gone from dependencies
- [ ] Each stage and layer loads on first use

**Verify:**
- [ ] `npm run build` — chunk table matches the budgets in tech-stack §6
- [ ] CI budget check fails on a deliberate top-level Phaser import

**Dependencies:** T12, T19 · **Files:** `vite.config.ts`, `package.json`, `scripts/check-bundle.mjs` · **Scope:** S

---

### ✅ Checkpoint E — lean and swept
- [ ] Navigation carries player layers only
- [ ] Entry chunk within budget; heavy deps lazy or gone

---

## Phase 5 — The remaining surfaces (parallelizable)

### Task 14: Relics with equipped-vs-candidate comparison
**Acceptance:** comparison is the default view while choosing; the diff shows losses as well as gains; virtualized above 24 items; filters survive close/reopen.
**Verify:** `npm test` — diff-state matrix, volume fixtures at 10/100/1000 asserting rendered node count. Compare against plate 02 §B and §D.
**Dependencies:** T10 · **Files:** `src/layers/relics/*` · **Scope:** M

### Task 15: Fusion
**Acceptance:** both columns — what is lost and what is gained — before the button; a deployed creature cannot be fused and says why.
**Verify:** `npm test`; compare against plate 02 §C.
**Dependencies:** T14 · **Files:** `src/layers/fusion/*` · **Scope:** S

### Task 16: World map stage as SVG — ⛔ EXCLUDED THIS PHASE, 2026-08-23

**Owner decision:** the World stage is a large, largely-unbuilt design surface in its own right
(`world-map-program.md`, `world-graph-ideal.md` — waves 1–2 built server-side, unbuilt in the FE) and
is deliberately parked out of this refactor. *"Map GUI is exclude this phase, just keep it as is —
we will have other plan for it because that is huge design, should make new GUI solid foundation
before we move to the map."* Kept in the task list, not deleted, because the module boundary
(`src/stages/world/*`) and its acceptance criteria are still the right target **when its own plan
lands** — this row is a placeholder for that future plan, not dead content.

**What "keep it as is" means concretely:** the current `/world` route stays exactly what it is today
(pre-refactor) until its own dedicated plan is written. It does **not** get swept into the developer
tree (T12) and does **not** get a stage/layer treatment in this pass — it is simply left alone.

**Acceptance / Verify / Dependencies below are the ORIGINAL scope, retained for the future plan to
start from, not a task to run now:**

**Acceptance:** nodes, lanes, ownership, fog and legions render from tokens only; pan/zoom works; no `@xyflow/react` import; selecting a sector opens the inspector as a band-2 layer with the map still mounted.
**Verify:** `npm test` — map stays mounted across inspector open/close; hex guard passes. Compare against plate 03 §A–B.
**Dependencies:** T9 · **Files:** `src/stages/world/*`, `src/layers/sector/*` · **Scope:** L — split if it exceeds 5 files

### Task 17: Expeditions and Pacts
**Acceptance:** a returned expedition is a toast plus a rail badge, never a dialog; the overdue pact's Renegotiate is disabled **with its reason inline**.
**Verify:** `npm test` — disabled-reason scan. Compare against plate 03 §C–D.
**Dependencies:** T11 (T16 dropped, 2026-08-23 — Expeditions and Pacts are stage-independent band-2
layers per [information-architecture.md](../docs/design/information-architecture.md), openable over
any stage; nothing in either layer's own code needs the World stage to exist). **One real gap this
leaves, named rather than hidden:** Expeditions' own unlock condition is *"first sector held"* —
a live end-to-end demonstration of that unlock firing needs a real sector claim, which needs World.
The layer itself is buildable and testable against fixture data regardless; only the live unlock
demo is blocked. · **Files:** `src/layers/expeditions/*`, `src/layers/pacts/*` · **Scope:** M

### Task 18: Battle stage
**Acceptance:** grid with painted range and targets; initiative track; action bar with cost, unaffordable and cooling states each carrying a reason; acknowledgement is immediate and no authoritative state paints before the response.
**Verify:** `npm test` — the four action states; assert no optimistic authoritative write. Fixture-driven e2e (see plan open question 3). Compare against plate 04 §C–E.
**Dependencies:** T9 · **Files:** `src/stages/battle/*` · **Scope:** L — split if it exceeds 5 files

**Entry-point note, 2026-08-23:** [information-architecture.md](../docs/design/information-architecture.md)
names two ways into Battle — *"commit a legion on the world map"* and *"an expedition resolving into
a fight."* With T16 excluded this phase, the **first** entry path has no stage to launch from. The
**second** (expedition → battle) does not require World and stays a real, demonstrable path — T18
builds and is fixture-e2e-tested through it. The world-map entry point returns when T16's own plan
does; this task's own scope and acceptance criteria are unchanged.

### Task 19: Almanac, Chronicle and the chart primitives
**Acceptance:** four chart shapes (horizontal bar, sparkline, meter, zero-anchored diverging bar) built from tokens; signed deltas are zero-anchored and coloured by sign; ledger paged above 240 rows; attribution expands a derived number into its sources.
**Verify:** `npm test` — volume fixture on the ledger; a `−12` renders left of centre in red. Compare against plate 05.
**Dependencies:** T8 · **Files:** `src/layers/almanac/*`, `src/layers/chronicle/*`, `src/ui/charts/*` · **Scope:** L — split if it exceeds 5 files

### Task 20: System settings, keymap and rebinding
**Acceptance:** Esc on an empty stack opens it; preferences persist to `localStorage` and survive the server being unreachable; rebinding shows a conflict before committing and names what it will cost; `F10` is listed and unbindable.
**Verify:** `npm test` — persistence with the API mocked down; conflict flow. Compare against plate 06 §C–D.
**Dependencies:** T3 · **Files:** `src/layers/system/*` · **Scope:** M

---

### ✅ Checkpoint F — every surface *in this phase's scope* exists
- [ ] Reachability matrix passes: every (stage, layer) pair **excluding World** opens, or is one of
      the three declared exceptions ([information-architecture.md](../docs/design/information-architecture.md)
      D8) — World is a **fourth**, phase-scoped exception (T16, 2026-08-23), not one of the three
      permanent behavioural ones, and the distinction matters: D8's three are rules about *when*
      travel is forbidden; this one is *"the stage does not exist yet in this build."*
- [ ] Viewport sweep passes at every declared width
- [ ] axe scan clean per layer
- [ ] All old routes redirect; none 404 — **except `/world`**, which stays on its pre-refactor route
      until T16's own plan lands (T12's sweep does not touch it)

---

## Phase 6 — Flows

### Task 21: Loadout
**Acceptance:** band-3 dialog; unavailable creatures stay visible with their reason; the matchup hint appears where it changes the decision.
**Verify:** `npm test`; compare against plate 07 §A. **Dependencies:** T14 · **Scope:** S

### Task 22: Deploy targeting
**Acceptance:** stage chrome, not a layer — no scrim; lit cells; nothing spent and no occupant created until the server admits it.
**Verify:** `npm test` — assert no optimistic occupant. Compare against plate 07 §B. **Dependencies:** T2, T18 · **Scope:** S

### Task 23: The pact offer
**Acceptance:** arrives as a toast and never opens itself; price and gift rendered at the same weight; "Decide later" is a real button with a stated expiry.
**Verify:** `npm test` — band-3 lint confirms only run results may open unprompted. Compare against plate 07 §C. **Dependencies:** T17 · **Scope:** S

### Task 24: The first-session script
**Acceptance:** four authored beats; six of eight rail entries locked at beat 1; the element lesson appears once, in place, at first elemental damage; each unlock is caused by an action, not a level number.
**Verify:** `npm test` — cold-start test asserts first paint against the script. Compare against plate 07 §D. **Dependencies:** T21, T22 · **Scope:** M

---

### ✅ Checkpoint G — done
- [ ] All twenty §19 checks green in CI
- [ ] Entry chunk ≤ 180 KB gz
- [ ] `lingui extract` clean; pseudolocale run shows no hardcoded strings and no overflow
- [ ] Coverage scope covers `shell/`, `stages/`, `layers/`, `ui/`, `lib/bus/`, `i18n/`
- [ ] Every plate has a matching implemented surface
- [ ] **Owner review and sign-off**
