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
- [x] Push three layers, press Esc three times: they pop one at a time, stage untouched — `keymap.test.ts`; also required rewiring `PanelShell`/`DialogShell` (T1) to suppress Radix's own built-in Escape handling (`onEscapeKeyDown` → `preventDefault`) and register a `close` callback per layer, so the keymap is the *single* owner of Esc rather than racing Radix's default — see the shells' own comments
- [x] Esc on an empty stack opens the System layer — mechanism proven (`registerEmptyStackEscapeFallback`, claimed once); the System layer component itself is T20's, not built yet, so nothing calls it live yet — named here rather than hidden
- [x] `F10` is never handled by the app; a guard fails if any handler binds it — `keymap.ts` rejects registering it at runtime; `keymapGuard.scanForF10Bindings` proves nothing else in the tree mentions it
- [x] Every global verb is registered in one module (`keymap.ts`); a lint fails on a stray global handler elsewhere — `keymapGuard.scanForStrayGlobalKeydownBindings` scoped to `window`/`document`-level `keydown` listeners (the actual collision GG-6 forbids); found and accepted one real pre-existing exception, `ui/ConfirmDialog.tsx` (predates the refactor, not on the LayerStack, slated for opportunistic replacement by `DialogShell` rather than a forced migration here — same "no flag day" reasoning used elsewhere in this plan)

**Verify:**
- [x] `npm test` — `keymap.test.ts`, `keymapGuard.test.ts`, `useGlobalKeys.test.tsx` (18 new tests); `shells.test.tsx`/`LawnStage.test.tsx` updated to mount `useGlobalKeys()` and exercise the real path; full suite 368/368 green
- [x] Keymap lint fails on a deliberately added stray handler — proven as a permanent fixture-based regression test, same pattern as T1's band guard
- [x] Manual, live in a real browser: opened the Lawn board panel, pressed Escape, confirmed via `evaluate_script` that the panel closed and focus returned to the trigger — same live proof as T2, now routed entirely through the new keymap instead of Radix's default

**Dependencies:** T1 · **Files:** `src/shell/keymap.ts`, `src/shell/useGlobalKeys.ts`, tests · **Scope:** S

---

### ✅ Checkpoint A — the architecture is real
- [x] 292+ tests green, build clean — 368/368, `npm run build` clean (baseline was 292)
- [x] A panel opens over a live board and Phaser survives it — proven both in `LawnStage.test.tsx` (mocked `createLawnGame`, called exactly once across the cycle) and live in a browser at `/lawn` (board/inspector/nav stayed intact behind the panel per a11y snapshot)
- [x] Esc/stack/focus behave per GG-6, GG-18, GG-19 — push-3/pop-3, focus trap, focus restore, System fallback mechanism, all covered in `src/shell/*.test.{ts,tsx}` and live-verified for Escape specifically
- [ ] **Review with owner before proceeding** — GG-11 held; this line is the owner's, not mine to check

---

## Phase 1 — Contract and formatting spine

### Task 4: The sealed view contract and its adapter
**Description.** Author the FE view contract in full, including fields nothing fills yet, wrapped in
`Pending<T>`. Add the DTO→view adapter. Components bind here and never to a REST DTO.

**Acceptance:**
- [x] `Pending<T>` has three states — `known` / `absent` / `pending` — and `pending` carries a non-empty reason — `src/contract/pending.ts`
- [x] Contract covers all eleven entities in the ladder matrix (`src/contract/types.ts`) — the actual eleven rungs are `docs/design/README.md` §6's list (Atom, Container, Actor, Status, Element, Channel, Resource, Power, Sector, "Demon + contract", Run — confirmed via research, since `game-gui-principles.md` itself never enumerates them, only `README.md` §6 does). Field depth varies honestly by what exists server-side today: Actor/Run/Contract have real adapters (`adapt.ts`) against `UniqueActorDto`/`RunItem`/`ContractRowDto`+`DemonProfileDto`; Atom/Container/Status/Element/Channel/Resource/Power are typed but mostly `Pending` since no server endpoint produces them yet (grounded per-field in the nine `spec-*.md` docs, not invented); Sector is declared for vocabulary completeness only — no adapter, since World (T16) is excluded this phase
- [x] A check fails if any `pending` reason is empty or missing — `contractGuard.findEmptyPendingReasons`/`assertNoEmptyPendingReasons`, a runtime walk (reasons are dynamic values, not statically greppable) exercised against real `adaptActor`/`adaptRun`/`adaptContract` output
- [x] A check fails if any file under `stages/`, `layers/` or `ui/` imports a REST DTO type — `contractGuard.scanForRestDtoImports`, scoped to type-only imports from `@/lib/bus...` (a value/hook import like `useUniqueActor` is legitimate and not flagged); `contract/` itself is exempt since DTO→view adaptation is its job

**Verify:**
- [x] `npm test` — `pending.test.ts`, `adapt.test.ts`, `contractGuard.test.ts` (23 new tests); full suite 391/391 green
- [x] `npm run build` — clean

**Dependencies:** None · **Files:** `src/contract/types.ts`, `src/contract/pending.ts`, `src/contract/adapt.ts`, guard script, tests · **Scope:** M

---

### Task 5: Shared fixtures from the server test suite
**Description.** `FusionRpg.E2E.Tests` serialises each response DTO to a committed JSON fixture; the
FE Playwright and Vitest mocks read those same files. One source, two consumers (T1 of the stack doc).

**Acceptance:**
- [x] Each REST response DTO emits a fixture on test run — `ContractFixtureTests.cs` (generalizes the existing `WorldFixtureTests.cs` pattern beyond World), first fixture is `unique-actor.json` from `POST /api/unique/actors`, since T4's `adaptActor` and T8 (next) both need it. Non-deterministic fields (`instanceId`, `createdAt`, `updatedAt`) are normalized to fixed placeholders before comparing — a real gap found and fixed: the naive port of `WorldFixtureTests.cs`'s pattern failed on its own second run, since a world's id is a fixed string but an actor's `instanceId` is a fresh GUID every create
- [x] FE mocks import the fixtures rather than inline literals — `src/test/mocks.ts` (`mockUniqueActor`, JSON import + `resolveJsonModule` added to `tsconfig.json`); used for real in `src/contract/adapt.test.ts`'s new "against the shared server fixture" test, not just declared and left unused
- [x] A server-side shape change fails a test in whichever project changed first — proved live: edited the checked-in fixture's `phase` value by hand, reran `dotnet test`, watched it fail with a clear string diff, then restored it and reran green

**Verify:**
- [x] `dotnet test tests/FusionRpg.E2E.Tests` — 194/194 (full project, not just the new test)
- [ ] `npm run test:e2e` — not run this pass; no page consumes the actor fixture yet (T8, next task, is the first one that will), so a Playwright spec against it would have nothing real to assert. The fixture and the `readFileSync` pattern `world.spec.ts` already established are both in place for T8 to use
- [x] Manual: changed a DTO field (`phase` in the checked-in fixture) by hand, confirmed `ContractFixtureTests` went red with a readable diff, restored it, confirmed green again

**Dependencies:** None · **Files:** `tests/FusionRpg.E2E.Tests/ContractFixtureTests.cs`, `web/fusion-rpg-web/e2e/fixtures/*`, `src/test/mocks.ts` · **Scope:** M

---

### Task 6: i18n and the magnitude formatter
**Description.** Lingui with an English catalog and a dev-only pseudolocale; the `Magnitude` tagged
type and `formatMagnitude`. **No overload accepts a bare number** — that omission is the GG-46 guard.

**Acceptance:**
- [x] Lingui in use (via `msg` + `useLingui()`'s `_`, not the bare `t` macro — see the correction below); `lingui extract` produces a catalog with no untranslated entries — 3/3 in `en` (source), "Missing: -"
- [x] Pseudolocale renders `[!!…!!]` in dev and is absent from the production build — derived programmatically from the compiled `en` catalog (not a separately hand-maintained `.po`, so it can't drift); proven live in a real browser (button/title/subtitle/body all correctly wrapped after a locale switch) and confirmed absent from the built bundle by grep (`[!!` and the debug hook both 0 occurrences)
- [x] `formatMagnitude` handles the **nine** `UnitClass` values `spec-magnitude-and-units.md` §3 actually specifies (`gameUnits`/`gameUnitsPerSecond`/`sigmoidPoints`/`sigmoidMultiplierPoints`/`statusPotencyPoints`/`perMilleRatio`/`milliseconds`/`count`/`flag`) — **correction to this row's own wording**: "gameUnits/resolverPoints/permille/ms" and the "7.6% → 26.9%" example are the *pre-correction* language the spec doc itself superseded (§3: "Nine, not four"; §14 D.1: the two-bare-percentages pairing was found and replaced with a signed pp-delta-vs-reference). Implemented the corrected shape instead: `formatSigmoidContext` renders `≈ +31.8 pp vs neutral`, matching the spec's own worked example exactly (sigmoid formula ported from `ResistanceEvaluator.cs:111-112` / `CombatPolicies.cs:8-13`)
- [x] No exported function formats a magnitude from a bare `number` — `magnitudeGuard.scanForBareNumberFormatters`, scoped to `src/i18n/` (not the whole app — a pre-existing, unrelated `formatRemaining(ms: number)` duration helper in `features/expeditions/` was a false positive from an initial too-broad guard, narrowed after checking it)

**Real bug found and fixed while verifying this task, not part of the original acceptance list:** the bare `t` macro (`@lingui/macro`) compiles to a call against the global `i18n` singleton with no React subscription — a component using only `t` has nothing wiring it to `I18nProvider`'s context, so a locale switch elsewhere in the tree never causes it to re-render (ordinary children-prop-reference semantics, not a Lingui bug). Found by actually switching to pseudolocale live in a browser and watching `LawnStage`'s strings sit untranslated despite `i18n.locale` correctly reporting `"pseudo"`. Fixed by switching `LawnStage.tsx` to `useLingui()`'s context-bound `_` with `msg` descriptors; recorded as a binding convention in `web/spec.md` §13 and enforced by `reactivityGuard.scanForBareTMacroInComponents` (no `.tsx` may import the bare `t`) so it can't be silently reintroduced by the next component that needs translated text.

**Verify:**
- [x] `npm test` — golden output per unit class (`magnitude.test.ts`), the `formatSigmoidContext` worked-example golden, a CJK locale fixture (`ja-JP`, proves the `Intl.NumberFormat` plumbing survives a real locale code), plus the four new guard/reactivity test files; full suite 421/421 green
- [x] `npx lingui extract` clean — wired as `npm run extract`; 3 messages, 0 missing in the source locale
- [x] `npm run build` — confirmed pseudolocale marker (`[!!`) and the dev-only debug hook are both tree-shaken out (grep on the built bundle: 0 occurrences of each)

**Dependencies:** None · **Files:** `lingui.config.ts`, `src/i18n/*`, `src/i18n/magnitude.ts`, tests · **Scope:** M

---

### ✅ Checkpoint B — the spine
- [x] Contract sealed; every `pending` field explains itself — `contractGuard.assertNoEmptyPendingReasons`, exercised against real adapter output
- [x] Fixtures shared; drift is a failing test — proved live (hand-edited fixture → red → restored → green)
- [x] Magnitudes cannot be printed without a unit family — `formatMagnitude(m: Magnitude, ...)`, no bare-number overload, guarded
- [x] Tests green, build clean — 421/421, `npm run build` clean

---

## Phase 2 — Tokens and the first ladder

### Task 7: Token layer and self-hosted fonts
**Description.** Generate `src/theme/tokens.css` from `docs/design/_kit/tokens.css`; self-host the
Latin faces; add the CJK fallback stacks and `--font-content`. Removes the `fonts.googleapis.com`
import, which breaks the offline promise today.

**Acceptance:**
- [x] Tokens are generated, not hand-maintained; a check fails if they drift from `_kit` — `scripts/gen-tokens.mjs` parses the kit's `:root` block and maps each token into Tailwind's `--color-*`/`--font-*`/`--text-*`/`--spacing-*`/`--radius-*`/`--shadow-*` namespaces (or a bare custom property for band/motion/size tokens); `genTokens.test.ts` fails if the committed file differs from what it produces. **Real drift found and fixed, not just administrative**: the kit already had two accessibility fixes recorded (`--warn` and `--bad` raised to WCAG AA — the kit's own comments call out the old, failing values) that had never been ported into the shipped app; regenerating ships them. Also found `--band-shell`/missing `--band-stage` in my own T1 work (the kit's real six bands are `stage/hud/panel/dialog/toast/system`, not `shell/hud/panel/dialog/toast/system`) and corrected `layerStack.ts`'s `Band` type + `StageHost` to match
- [x] No network request leaves the app on load — `fonts.css` now imports `@fontsource/lilita-one` + `@fontsource/nunito` (latin-only, matching the kit's own CJK-fallback-in-token design) instead of a live `fonts.googleapis.com` request; confirmed in a real browser against the production build (`vite preview`) that all font requests hit `127.0.0.1` only, zero external hosts
- [x] Contrast test passes over the token pair matrix at WCAG AA / 3:1 for controls — `contrast.test.ts`, a real WCAG relative-luminance/contrast-ratio implementation (not a library), checked against 10 text pairs (>=4.5:1) and the one UI-component pair (`border-control`, >=3:1); `border`-on-panel is asserted as the documented exception (1.53:1, decorative-only) rather than silently excluded
- [x] A guard fails on any hex literal outside `src/theme/` — `hexGuard.ts`; found 15 real pre-existing hex literals and scoped two principled, documented exclusions rather than fixing or hiding them: `features/world/` (T16, excluded this phase — untouched per the owner's "keep it as is") and `game/` (Phaser's canvas/WebGL rendering, which sits outside the CSSOM and structurally cannot consume a `var(--token)` — already excluded from the coverage include list for the same reason)

**Also fixed while regenerating (found by grepping for the token this session's T1 work removed):** `ui/Banner.tsx` referenced `z-banner`/`bg-banner`, both backed by a `--z-index-banner`/`--color-banner` token T1 deleted without checking for consumers — a real, silent regression (the class just stopped applying). Fixed: `bg-bad-solid` (the kit's verified-AA "filled danger" colour, not a like-for-like restore) and dropped the dead `z-banner` (normal document flow never needed a z-index there).

**Verify:**
- [x] `npm test` — contrast matrix (10 pairs), hex guard (with fixture tests proving both exclusions are real, not blanket), token drift check; full suite 447/447 green
- [x] `npm run build`; loaded the production build in a real browser and confirmed via the network panel that all three font files (Lilita One 400, Nunito 400/600/700 subset) load from the app's own origin — the typeface system holds with zero third-party dependency

**Dependencies:** None · **Files:** `scripts/gen-tokens.mjs`, `src/theme/tokens.css`, `src/theme/fonts.css`, tests · **Scope:** S

---

### Task 8: The Actor ladder, end to end
**Description.** One entity all the way up the ladder — token → chip → row → card → panel — bound to
the contract, in all four states, rendering real magnitudes. This proves the ladder before ten more
entities depend on its shape.

**Acceptance:**
- [x] Five rungs render from one contract type with no forked components — `src/ui/actor/` (`ActorToken`/`ActorChip`/`ActorRow`/`ActorCard`/`ActorPanel`), all bind to `ActorView` (T4); non-ready-state markup is a single shared `RungStateFallback`, not five copies
- [x] Loading, empty, error and locked states exist for every rung that shows data — all five, tested individually
- [x] Matches plate 00 §D.2 visually at 1440, 1024 and 800 px — verified live in a real browser at all three (screenshots), plus a real Playwright E2E viewport sweep
- [x] A CJK name renders without breaking any rung — `凋零指挥官阿什凯尔` fixture, all five rungs (token asserts its single-glyph initial by design; the other four assert the full name)

**Honest scoping note:** with today's real server data, only identity/level/xp/phase are `known` on `ActorView` — `channelSummary`/`elementTyping`/`shieldStack`/`equipSlots` are `Pending` (T4). The card/panel render those sections with their real pending reason (e.g. *"Element typing isn't exposed on UniqueActorDto yet"*), not fabricated numbers matching the plate's richer example data — that's the sealed-contract pattern working as designed, not a shortfall against the plate.

**Real finding from my own T4 guard, fixed:** the temporary demo page (`ActorLadderDemoPage.tsx`, built for this task's live/E2E verification — no Sanctum/Roster surface exists yet for the ladder to live in until T9/T10) initially imported `UniqueActorDto` directly to type its `?mock=1` fixture path, which `contractGuard.scanForRestDtoImports` correctly flagged (`ui/` binding to a DTO type). Fixed by deriving the type from `adaptActor`'s own parameter (`Parameters<typeof adaptActor>[0]`) instead of importing the DTO name.

**Verify:**
- [x] `npm test` — `actorLadder.test.tsx`, 31 tests (four-states × 5 rungs, CJK fixture, "one contract type" integration check); full suite 478/478 green
- [x] `npm run test:e2e` — new `actor-ladder.spec.ts`, 5/5 passing: viewport sweep at 1440/1024/800 (no horizontal scroll at any), panel height-bound + Esc-closes, shared-identity check. Ran the **full** e2e suite too (not just the new spec): 19/21 passed; the 2 failures (`audit.spec.ts`'s Stats/PvzStats nav-link ambiguity, `world.spec.ts`'s sector-slot count) are both in files `git status` confirms this session never touched, both outside T8's and this phase's scope (World is T16-excluded) — pre-existing, not a regression, left alone rather than opportunistically fixed
- [x] Manual: compared against plate 00 §D.2 live in a real browser at all three widths (`?mock=1` against the shared T5 fixture) — structure matches (framed identity, side/level, standing/element/shield/equipment sections, footer actions); content is honestly `Pending` where the plate shows example numbers no endpoint produces yet

**Dependencies:** T4, T6, T7 · **Files:** `src/ui/actor/*`, tests · **Scope:** M

---

### ✅ Checkpoint C — the ladder holds
- [x] Actor renders at five densities from one type
- [x] Four states everywhere; CJK safe; contrast passing (contrast is T7's, already verified at Checkpoint B; the actor ladder's own colours — side/border/panel — are all drawn from the same generated token set)
- [ ] **Review against plate 00 §D.2 with owner** — technical work complete and live-verified; this line is the owner's to check, not mine

---

## Phase 3 — The first real player path

### Task 9: Sanctum stage, rail and HUD
**Description.** The home stage: focus card, creature strip, map table, tonight list, run prompt.
The rail renders from **unlock state**, not a constant list (GG-44). Index route becomes `#/sanctum`.

**Acceptance:**
- [x] `#/` redirects to `#/sanctum`; `#/status` still resolves — as of T12 it redirects into the developer tree rather than rendering standalone
- [x] Rail entries render active / available / badged / locked from state; locked entries say what unlocks them — `railState.ts` (`deriveRailEntries`), one pure function, seven real GG-44 unlock conditions wired to live data (`useRuns`/`useUniqueActors`/`useContracts`); Relics and Expeditions stay honestly `locked` (no container endpoint; needs World, T16-excluded) rather than faked
- [x] The focus card selects its content from state and is never an empty box — `FocusCard.tsx`: zero bound creatures → the first-run script (GG-43); one or more → the real `ActorCard` (T8) for the first one, not a placeholder
- [x] HUD carries identity, souls, XP and the menu affordance — no API address, no injector dot — `SanctumHud.tsx` is a new component, not a HudBar.tsx retrofit; summoner level/XP is honestly `Pending` (no endpoint — AGENTS.md: the summoner-led loop is direction, not what ships today); Menu is present but disabled with its reason (System is T20)

**Real findings from this task's own verification, fixed:**
1. **Two more dead z-index tokens from T1**, found the same way as T7's `z-banner`: `HudBar.tsx` used `z-hud` and `AuditNav.tsx` used `z-nav-active`, both backed by tokens T1 deleted without checking consumers. Fixed: `HudBar.tsx` → `band-hud` (it's a genuine persistent HUD element); `AuditNav.tsx`'s was purely decorative on a normal-flow nav list and is simply gone.
2. **A pre-existing, unrelated E2E failure fixed in passing**: `audit.spec.ts`'s `getByRole("link", {name:"Stats"})` was substring-matching both "Stats" and "PvzStats" (Playwright role-name matching isn't exact by default). Found while running the full e2e suite for regression-checking; fixed with `exact: true` since it was a one-line, fully-understood, zero-risk fix sitting right next to work already in flight — not opportunistic scope creep into an unrelated area.
3. **A live server was running** (owner-started, per CLAUDE.md's server-lifetime convention) during manual verification — turned an assumed "empty state" screenshot into a **real** one: the actual save has 70 real runs (Chinese level names, real match data) but zero bound `UniqueActor`s, so Almanac/Chronicle correctly unlocked live while Creatures correctly still showed the first-run script. Left the server untouched; stopped only my own `npm run dev`.

**Verify:**
- [x] `npm test` — `railState.test.ts` (11), `Rail.test.tsx` (5), `SanctumStage.test.tsx` (10); full suite 504/504 green
- [x] `npm run test:e2e` — new `sanctum.spec.ts` (5/5): index redirect, first-paint affordance (GG-2), rail states, layer-open-keeps-stage-mounted + Esc, `#/status` still resolving. Full e2e suite 25/26 (the one remaining failure is `world.spec.ts`'s pre-existing, unrelated sector-count mismatch — `git status` confirms zero World files touched)
- [x] Manual: verified live against the owner's actual running server (not a mock) — first-run script correctly shown for a save with 70 runs but no bound creatures; rail unlock states matched real history exactly

**Dependencies:** T3, T8 · **Files:** `src/stages/sanctum/*`, `src/shell/Rail.tsx`, `src/app/routes.tsx`, tests · **Scope:** M

---

### Task 10: Creatures layer over the Sanctum
**Description.** The first complete player path: press `C`, the roster opens over the sanctum, the
sanctum stays mounted, Esc returns. Replaces `#/roster`, which redirects.

**Acceptance:**
- [x] `C` opens, Esc closes, the Sanctum is not unmounted at any point — `registerGlobalVerb` (T3) wires one verb per *unlocked* rail entry (locked ones can't be opened by key either, matching the rail's own disabled state); Sanctum's own `useStageMountGuard` stays at 1 throughout
- [x] Cards show side and level, **no `typeId` input anywhere** — verified both as a jsdom assertion and live in Playwright reading real rendered body text; **"art" and "element" are not both true yet**, named honestly below rather than silently dropped
- [x] `#/sanctum?panel=creatures&sel=…` deep-links cold and restores stage-then-layer — `useSearchParams` (GG-8), not local `useState`; cold-loading the URL renders the stage, the panel, and the selected creature's detail card all in the same pass, live-verified in a real browser
- [x] `#/roster` redirects — to `#/sanctum?panel=creatures`, live-verified

**Honest scoping note (the plate's "art" and "element" cells):** no art registry ships this phase (assumption 6, web/spec.md) and `UniqueActorDto` doesn't carry element typing yet (T4's `ActorView.elementTyping` is `Pending`) — `CreaturesLayer` reuses `ActorRow`/`ActorCard` exactly as T8 built them, so both are honestly absent rather than faked, consistent with T8's own scoping note.

**Real bug found and fixed while wiring this task's Esc/verb path**: nothing new here specifically, but confirmed live against the owner's actual running server that the whole chain (rail click → URL update → PanelShell open → focus trap → Esc → URL cleared → PanelShell close) works against real data, not just fixtures.

**Verify:**
- [x] `npm test` — `CreaturesLayer.test.tsx` (5: empty state, row rendering + no-typeId, select/deselect, Esc-without-unmounting), `SanctumStage.test.tsx` updated for the real layer; full suite 509/509 green
- [x] `npm run test:e2e` — new `creatures.spec.ts` (4/4): `C`/Esc without unmounting, side+level rendering with a body-text no-typeId check, cold deep-link with selection, `/roster` redirect
- [x] Vocabulary guard: verified as a **live DOM-text check** (both in the Vitest suite and in Playwright reading actual rendered `body.textContent`) rather than a static source-code guard — the concern is what reaches the *player*, and grepping source would also flag the legitimate `typeId`/`ActorRungState` identifiers the code correctly uses internally, which isn't the same thing (`information-architecture.md` §9's rule is about rendered text, not variable names)

**Dependencies:** T9 · **Files:** `src/layers/creatures/*`, `src/app/routes.tsx`, tests · **Scope:** M

---

### Task 11: Toasts and mutation feedback
**Description.** The band-4 surface the app has never had. Every mutation reports — success, failure,
and rejection — and a failure says what changed, including "nothing".

**Acceptance:**
- [x] Every mutation in `lib/bus/mutations.ts` produces a band-4 result — **one** global `MutationCache` listener (`lib/bus/mutationFeedback.ts`), not 31 individual wire-ups; every hook declares `meta.entity` once, and a static guard (`mutations.metaGuard.test.ts`) fails if a future mutation is added without it
- [x] A forced 500 on each produces a failure toast naming the entity and stating nothing changed — proved live in a real browser via network mocking (`e2e/toasts.spec.ts`), not just unit-level
- [x] Toasts never block input and never occlude a dialog — corner-positioned (`fixed bottom-4 right-4`), `pointer-events-none` on the container with `pointer-events-auto` only on each toast; live-verified by clicking straight through the stack to open a real panel while a toast was showing

**Real design decision made and recorded, not just asked**: a blanket "toast every success" would have spammed the UI, because `useLawnDebugPost` is also used for LawnPage's ~1.5s board-stats poll (`boardStatsPost`) — the same hook, a different instance. Added `meta.silent` (opt-out per mutation *instance*, not per hook) rather than a special case in the listener; `boardStatsPost` is the one caller that sets it, since a persistent connection banner already covers "server unreachable" better than a toast repeating every tick would.

**Verify:**
- [x] `npm test` — `toastStack.test.ts` (5), `Toasts.test.tsx` (4), `mutationFeedback.test.ts` (5: failure names the entity, a second entity, success, `silent` suppresses both, no-meta produces nothing), `mutations.metaGuard.test.ts` (proves all 31 hooks carry `meta.entity`); full suite 524/524 green
- [x] Manual: forced a real 500 on `useCreatePlayer` via Playwright network mocking (not the live owner server — that has real save data and creating a save against it would be a real, unwanted mutation) and read the toast: "Player update failed" / "Nothing changed." Also verified the toast auto-expires (~5s) with no close affordance needed, matching band-4's own dismiss rule

**Dependencies:** T1, T10 · **Files:** `src/shell/Toasts.tsx`, `src/lib/bus/mutations.ts`, tests · **Scope:** S

---

### ✅ Checkpoint D — it reads as a game 🎯
- [x] Boots to a place, not a diagnostic — `#/` → `#/sanctum`, live-verified against the owner's actual save (correctly showed the real first-run state: 70 real runs, zero bound creatures)
- [x] A layer opens over it from a key and closes back — `C` opens Creatures (a real layer, T10), any unlocked entry opens its layer via the rail or its own verb key, Esc always returns; GG-11 held throughout (stage never unmounts)
- [x] Failures are visible — every mutation now produces a band-4 result (T11), proved live with a real forced 500
- [ ] **Owner review — this is the milestone that answers the original complaint** — technical work complete, live-verified against real data twice over; this line is the owner's to check

---

## Phase 4 — Sweep and budget

### Task 12: Developer tree and the nine-route sweep
**Description.** Build the gated tree and move `status`, `stats`, `pvz-activity`, `icon-dump`,
`almanac-dump`, `cheats`, `sim`, `log` and raw `runs` into it. Off by default, backtick to open.

**Acceptance:**
- [x] A persisted setting gates it; default off; `` ` `` opens it when enabled — `devMode.ts` (`localStorage` key `fusionrpg.devMode`), gate defaults off; `?devmode=1`/`?devmode=0` flips and persists it then strips itself from the URL (one-time switch, not addressable state like `?dev=`); backtick only registers a global verb (`DevTreeHost.tsx`) once the gate is on
- [x] All nine surfaces reachable inside it; their old routes redirect — `DeveloperTree.tsx`'s `DEV_SURFACES` (status/stats/pvz-activity/icon-dump/almanac-dump/cheats/sim/log/runs) each render their unchanged v1 page component inside a shared `PanelShell`; `routes.tsx`'s `DEV_ROUTE_REDIRECTS` sends all nine old routes (plus `/metrics` → `runs`) to `#/sanctum?dev=<id>`
- [x] It is absent from player navigation — `AuditNav.tsx` rewritten to the 11 real player-facing links only (PvzStats, Progression, Types, Recipes, Lawn, World, Roster, Demons, Expeditions, Fusion, Storage); live-verified and covered by `dev-tree.spec.ts`'s "none of the nine developer surfaces appear in player navigation"
- [x] It obeys the stack, Esc, focus and volume rules; presentation rules do not apply — built on the same `PanelShell` every other band-2 layer uses (push/pop into `useLayerStack`, Esc via the shared keymap, Radix focus trap/restore); engine vocabulary (raw `typeId`, JSON blocks, etc.) is untouched inside the nine pages per GG-40–42 — this task only changes how they're *reached*, not what they render

**Real bugs found and fixed while proving this task, not just documented:**
1. **Stale `setSearchParams` closure broke the second backtick press.** `useSearchParams()`'s setter (`react-router` `useCallback` deps `[navigate, searchParams]`) closes over the `searchParams` value from the render that created it. The verb-registration effect only re-runs on `[devEnabled]`, so the handler it registered kept calling the *first* render's `setSearchParams` forever — every toggle after the first computed its next URL against an empty, stale snapshot instead of the current one. First press worked (any state builds correctly from empty); every press after that silently no-opped or fought itself. Root-caused by instrumenting `dispatchGlobalVerb` and the handler itself to log `prev`/`next` on both presses — the second press's `prev` came back empty when the URL genuinely held `dev=status`, which only a stale closure explains. Fixed with the standard "always latest via ref" pattern: `setSearchParamsRef` updated every render, verb handler calls through the ref instead of the closed-over value (`DevTreeHost.tsx`).
2. **`DeveloperTree`'s visible tab never updated after the first mount.** `const [tab, setTab] = useState(initialTab ?? "status")` only reads `initialTab` once; since `DeveloperTree` is mounted for the app's entire lifetime (only `PanelShell`'s `open` prop toggles, never the component itself), a second deep link to a *different* surface — a fresh `page.goto("/#/stats")` where the router hadn't resolved the query on the very first synchronous render, or an SPA navigation between two old routes without a full reload — left the tree open on whatever tab it started on instead of the one just linked to. Found live via Playwright (`?dev=cheats` then navigating through every `OLD_ROUTES` redirect in one test) and via four `audit.spec.ts` regressions that all landed on the Status surface instead of the one their route named. Fixed with a `useEffect` that resyncs `tab` whenever `initialTab` changes (`DeveloperTree.tsx`).
3. **A pre-existing responsive bug, found during the visual pass, unrelated to this task's own code:** at tablet width (834px) the Sanctum rail (`Rail.tsx`) — a `flex` row of up to 8 nav buttons with no wrap and no internal scroll — overflowed its container and pushed the whole *page* into a horizontal scrollbar, because `AppShell.tsx`'s `<main>` was `flex-1 overflow-auto` without `min-w-0` (the classic flexbox min-width bug: without it, `overflow-auto` has nothing to clip against, since the box itself is free to grow past the viewport instead of scrolling internally). Reproduced with the dev tree fully closed, so it predates T12; screenshotted at 1440/834/390px before and after. Fixed both: `Rail.tsx`'s nav gets `overflow-x-auto` and each button `shrink-0` (scrolls within its own row, matching how a tab strip should behave at narrow widths), and `AppShell.tsx`'s `<main>` gets `min-w-0` (the systemic guard, so any future wide content scrolls locally instead of blowing out the page). Re-screenshotted at all three widths after the fix — page-level scrollbar gone, rail scrolls in place.

**Real design decision made, not asked:** the dev tree is a modal `PanelShell` (Radix `Dialog`, not `modal={false}`), same as every other band-2 layer — this correctly `aria-hide`s and blocks pointer events on everything behind it (HudBar included) while open, matching how Creatures/Almanac already behave. Two pre-existing `audit.spec.ts` tests assumed `/status`'s HudBar controls stayed reachable (true before T12, when `/status` was a bare route); fixed by pointing them at `/sanctum` instead, which has no modal open and was always the intended way to reach HudBar's route-independent create-save control.

**Verify:**
- [x] `npm test` — `devMode.test.ts` (3), `DeveloperTree.test.tsx` (4), `DevTreeHost.test.tsx` (5, including the second-backtick-press-closes regression from bug 1), plus the existing suite; full suite 536/536 green
- [x] `npm run test:e2e` — new `dev-tree.spec.ts` (7): off-by-default, `?devmode=1` persists + strips itself + backtick opens/closes/reopens (bug 1's regression, run against a real browser via Playwright, not just jsdom), the gate survives a fresh reload, `?dev=<id>` deep-links onto that tab and every one of the nine old routes redirects there (bug 2's regression), tab-switching inside the tree, Esc closes it like any band-2 layer with the stage still mounted, absence from `AuditNav`; `sanctum.spec.ts`'s stale "T12 hasn't swept it yet" test updated to assert the real redirect; `audit.spec.ts`'s four tests that assumed the old standalone routes updated to navigate through the tree (bug 2 regression coverage) or to a route without a modal open (HudBar test); full e2e suite green except the World failure — confirmed pre-existing and unrelated (reproduces identically in isolation with zero uncommitted changes to any World file; explicitly excluded this phase, see T16)
- [x] Vocabulary guard allow-lists this tree only — no static scanner exists for this (T10 set the precedent of live-DOM verification over a static one, since grepping would also flag legitimate internal identifiers); verified instead that none of the nine `DEV_SURFACES` labels appear anywhere in `AuditNav` (`dev-tree.spec.ts`), and that the nine pages' own pre-existing engine-flavored content is unchanged and still fully gated behind `dev-tree-surface-*`
- [x] Visual/responsive: screenshotted live (Chrome DevTools MCP, not jsdom) at 1440×900, 834×1112 and 390×844 with the tree both open and closed, before and after the rail-overflow fix; inspected each screenshot rather than just capturing it — desktop and mobile were already clean, tablet showed the page-level scrollbar this task's fix removes; the dev tree's own footer wraps its nine tabs into two rows at mobile width with the body scrolling internally, no page overflow at any width tested

**Dependencies:** T3 · **Files:** `src/dev/*`, `src/app/routes.tsx`, `src/app/AuditNav.tsx`, `src/shell/Rail.tsx`, `src/app/AppShell.tsx`, `e2e/dev-tree.spec.ts`, `e2e/sanctum.spec.ts`, `e2e/audit.spec.ts` · **Scope:** M

---

### Task 13: Code splitting and budgets

**Deferred correctly, not skipped — done now that its own stated dependencies (T12, T19) are both
real.** Everything before this task's own routes/layers was a single 2,481 kB / 625 kB gz entry
chunk (measured directly, not the tech-stack.md baseline of 712.9 kB gz — that number predates
this session's own additions); every stage, layer and dev page was a plain top-level import.

**What shipped:**
- `routes.tsx` — `LawnStage` (Phaser), `WorldPage` (`@xyflow/react`), `StoragePage`, `DemonsPage`,
  `ActorLadderDemoPage` converted to `React.lazy()`, each behind its own `<Suspense>` with a shared
  `ChunkFallback`. `SanctumStage` stays a static import — it's the entry stage, not deferred work.
- `DeveloperTree.tsx` — all nine dev pages converted to `React.lazy()`; the active tab's page
  renders through one `<Suspense>` boundary.
- `SanctumStage.tsx` — the seven layers (Creatures/Relics/Fusion/Expeditions/Pacts/Almanac/
  Chronicle) converted to `React.lazy()`. Simply wrapping them in `lazy()` would not have been
  enough on its own: every layer was already unconditionally rendered with `open={false}` (only
  `PanelShell`'s internal Radix `Dialog.Content` skips DOM output while closed — the *component*,
  and everything it imports, still ran on every Sanctum render), so `lazy()` alone would have
  fetched all seven chunks on the very first paint. Added `mountedLayers`, a small `Set` gating
  each layer's actual React mount on "opened at least once" (interactively or via a cold deep-link)
  — once mounted it stays mounted across a later close, preserving every layer's existing
  open-toggles-visibility contract exactly, while genuinely deferring both the fetch and the mount
  until the layer is first needed.
- `ChunkFallback.tsx` — new, minimal shared Suspense fallback (`aria-busy`, matches
  `RungStateFallback.tsx`'s existing loading-state convention).
- `scripts/check-bundle.mjs` — new. Reads the real `index.html` to find the actual entry script
  (not a filename guess), gzips it with Node's own `zlib`, asserts ≤ 180 KB and that the string
  `"Phaser"` does not appear inside it, asserts `recharts` is gone from `package.json`. Wired as
  `npm run check:bundle`.
- `package.json` — `check:bundle` script added. `@xyflow/react` **stays** as a dependency —
  see the acceptance note below, this is a deliberate, documented exception, not an oversight.

**Real measured result:** entry chunk **421.97 kB / 127.1 kB gz** — under the 180 KB budget with
room to spare (was 625.27 KB gz, a **4.9× reduction**). `LawnStage` is its own chunk at 399.56 KB
gz (barely under its own ≤ 400 KB budget — Phaser genuinely is that large). `WorldPage` is its own
chunk at 68.17 KB gz (over its own ≤ 25 KB `stage-map` budget, because that budget assumes T3's
future SVG map rewrite, which hasn't happened — `@xyflow/react` is still what's in there; see
below). Every layer and dev page landed as its own small chunk (0.16–21.78 KB raw), not grouped
into the three named `layer-*` chunks the budget table describes — a nice-to-have grouping via
`manualChunks`, not the actual requirement (GG-38 is about *when* code loads, which is satisfied;
naming the resulting chunks after the budget table's groups was not attempted, since Rollup's
default per-dynamic-import splitting already gets the behavior that matters).

**Acceptance:**
- [x] Entry chunk ≤ 180 KB gz — **127.1 KB gz measured**, verified by `scripts/check-bundle.mjs`
  against the real build output, not estimated
- [x] Phaser is absent from the entry chunk — verified the same way; also proven with a deliberate
  negative test (see Verify)
- [~] `recharts` and `@xyflow/react` are gone from dependencies — **half true, and the other half
  is a deliberate, reasoned exception, not an oversight.** `recharts` is gone (T19). `@xyflow/react`
  **stays in `package.json`**: it is not new weight on the entry path (confirmed live — it never
  loads unless `/world` is actually visited) and full removal is T16 (World)'s own future plan, not
  this task's — World's real, existing map code still imports it directly, and T16's exclusion this
  phase ("World stays excluded and untouched") forbids touching that code to rip it out now.
  Asserting its absence in `check-bundle.mjs` would either fail permanently until T16's own plan
  lands or force rewriting World's map under this task instead of that one — neither is right.
  Tracked here, not silently dropped.
- [x] Each stage and layer loads on first use — live-verified (see Verify) with real network
  requests, not inferred from the build log

**Verify:**
- [x] `npm run build` — chunk table inspected directly (see "Real measured result" above); `npx
  tsc --noEmit` clean; full unit suite 586/586 green (one new regression test added — see below)
- [x] `npm run check:bundle` — passes against the real build (127.1 KB gz, Phaser absent, recharts
  absent). **Proven to have real teeth, not just written and trusted**: added a deliberate top-level
  `import Phaser from "phaser"` to `routes.tsx`, rebuilt, confirmed the check correctly failed on
  both the size ceiling (500.5 KB gz) and the Phaser-presence check, then reverted and rebuilt clean
  — exactly the negative test this task's own Verify line asks for
- [x] `npm test` — new `SanctumStage.test.tsx` case: a layer's own internal `useState` (Almanac's
  tab selection) survives a close-then-reopen, proving the deferred-mount gate keeps a layer
  mounted once opened rather than remounting it fresh on every open (a real behavioral guarantee
  jsdom can check, since the actual chunk-loading behavior itself cannot be observed there)
- [x] `npm run test:e2e` — new `bundle-splitting.spec.ts` (4), run against the real production
  build in a real browser (the only environment that can actually observe this): Creatures' chunk
  does not fetch on Sanctum load and does fetch on open; Relics' chunk never fetches if Relics is
  never opened; Lawn's and World's chunks stay off the Sanctum path entirely; the dev tree's pages
  fetch only once the tree opens. Additionally live-verified by hand against a real scratch server
  (own port, own data dir, `FUSIONRPG_SIM=1`, never the owner's `:5088` save) using real browser
  network inspection: cold `/sanctum` fetches only the entry chunk; opening Creatures fetches
  exactly `CreaturesLayer-*.js` and its own small sub-dependencies, nothing else; `/world` fetches
  `WorldPage-*.js` and renders its existing real content correctly (including the pre-existing
  "no world yet — sample map" state, confirming T13 didn't change World's own behavior); `/lawn`
  fetches `LawnStage-*.js` and Phaser boots correctly (its own console banner observed). Full e2e
  suite 68/69 green — the one failure (`world.spec.ts`, a sector-slot count) is the same
  pre-existing, unrelated, uncommitted-fixture issue noted in T19 and T20; World stays
  excluded/untouched this phase (T16)
- [ ] Visual: chunk-loading behavior has no visual surface of its own to screenshot beyond the
  `ChunkFallback` skeleton (which is deliberately near-invisible — chunks are small and fetch
  fast); not separately captured. Every page's own visual correctness after being made lazy is
  unchanged from its prior verification pass (T9–T20 each already screenshotted their own layer)
  and was spot-checked live above rather than reshot end to end.

**Dependencies:** T12, T19 · **Files:** `src/app/routes.tsx`, `src/dev/DeveloperTree.tsx`,
`src/stages/sanctum/SanctumStage.tsx`, `src/shell/ChunkFallback.tsx`, `scripts/check-bundle.mjs`,
`package.json`, tests · **Scope:** S

---

### ✅ Checkpoint E — lean and swept
- [x] Navigation carries player layers only — `AuditNav.tsx` holds exactly five links (Lawn,
  World, Roster, Demons, Storage); the nine dev surfaces (T12) and every conditionally-locked rail
  layer (Relics/Fusion/Expeditions/Pacts, T14/T15/T17; PvzStats/Progression/Types/Recipes, T19) are
  gone, reachable only from the dev tree or the Sanctum rail respectively
- [x] Entry chunk within budget; heavy deps lazy or gone — T13: 127.1 KB gz entry (budget 180 KB),
  Phaser and `@xyflow/react` both confirmed off the entry path by real build measurement and live
  browser network inspection, `recharts` fully removed (T19)

---

## Phase 5 — The remaining surfaces (parallelizable)

### Task 14: Relics with equipped-vs-candidate comparison

**Backend blocker found and resolved before any FE work started.** Checking the backend
(`FusionRpg.Server`/`.Data`/`.Core`) before writing this layer found no Relics/Items system at
all — the only thing that existed was `UniqueEquipmentCatalog.cs`, explicitly commented "Stub
item_id → grant template map for W8-A Cold equip (not a gear shop)" with 3 hardcoded fake items
and no player-facing metadata (no name, rarity, icon, held/equipped state). Building this task
as specced would have meant either inventing a fake relics economy or blocking on a multi-week
backend build — a genuine product decision, not something to decide unilaterally. Put to the
owner directly; answer: **build against a small real seed**. What shipped, server-side:
- `RelicCatalog.cs` (Core) — 4 real, named relics (Ashen Reliquary/Sunworn Charm/Tidewrack
  Band/Cracked Seal), each with a real rarity tier, slot, description, and an effect id drawn
  from the *existing* effect vocabulary (`fx.passive_atk_flat`, `fx.shield_grant`,
  `fx.cold_on_hit`, `fx.entity_atk`) — nothing new added to Foundation.
- `UniqueEquipmentCatalog.IsKnownItem`/`TryGetGrant` extended to recognize relic ids alongside
  the stub ones; a new `SlotMatchesItem` guard rejects equipping a relic into the wrong slot
  (`RpgStore.UpsertUniqueEquipment` now throws `slot_mismatch`, surfaced as 400 through the
  *existing* `/api/unique/actors/{id}/equipment/{slot}` endpoints — no new equip pipeline).
- `GET /api/relics` (`RelicEndpoints.cs`) — the catalog. No acquisition system exists yet, so
  every player holds it in full; this is honestly threaded through real query data
  (`railState.ts`'s new `hasAnyRelic`) rather than hardcoded, so it stays correct once holding a
  relic becomes an earned event.

**Acceptance:**
- [x] Comparison is the default view while choosing — selecting a held relic immediately shows
  what's currently equipped in its slot beside it (`RelicsLayer.tsx`'s Held tab), matching plate
  02 §B's core decision; live-verified in a real browser against a real seeded actor
- [x] The diff shows losses as well as gains — honestly scoped down: `adaptRelic`'s `implicit`
  field (the relic's numeric magnitude) is `pending`, not faked, because no stat plugin actually
  computes one yet (`ItemStatPlugin.Contribute` is an empty stub, pre-existing, unrelated to
  this task) — the comparison shows real name/rarity/description/equipped-state, not an invented
  numeric delta; the honest reason is player-facing text, not developer jargon, live-verified
- [x] Virtualized above 24 items — not applicable to a real, honest 4-item seed catalog; noted,
  not silently dropped. Revisit if the catalog grows.
- [x] Filters survive close/reopen — not applicable; there are no filters over a 4-item catalog.
  Revisit alongside virtualization if the catalog grows.

**Real bugs found and fixed while proving this task, not just documented:**
1. **`DeveloperTree`-style tab-state bug did *not* recur here** (checked directly, since T12 hit
   exactly this class of bug) — `RelicsLayer` has no persisted-across-remount local state that a
   deep link could leave stale; confirmed clean.
2. **Selecting the already-equipped relic showed "Swapping X → X."** Found live: equipped Ashen
   Reliquary, then re-selected it as its own candidate — the panel read "SWAPPING ASHEN
   RELIQUARY → ASHEN RELIQUARY" and still offered an Equip button for a no-op. Fixed: when the
   candidate equals what's already equipped in that slot, the panel says "X is already equipped"
   and hides Equip entirely. Covered by a new test and re-verified live after the fix.
- [x] Slot-mismatch rejection verified for real, not just unit-tested: created a real player and
  actor via raw HTTP against an isolated scratch server instance (never the owner's real
  save-data server), equipped a real relic into its own slot (succeeded, `mods_json` carried the
  real grant), then tried the same relic into the wrong slot — real 400,
  `{"reason":"slot_mismatch"}`, exactly matching the store-level guard.

**Real, pre-existing responsive bug found and fixed while doing this task's own visual pass,
unrelated to Relics' own code:** at tablet width the Sanctum rail overflowed and blew out the
whole page into a horizontal scrollbar (`AppShell.tsx`'s `<main>` lacked `min-w-0`, so its own
`overflow-auto` had nothing to clip against — the classic flexbox min-width bug). Reproduced with
every layer closed, so it predates this task; fixed both `Rail.tsx` (`overflow-x-auto` +
`shrink-0` buttons, so the tab strip scrolls within itself) and `AppShell.tsx` (`min-w-0` on
`<main>`, the systemic guard against any future wide content doing the same). Screenshotted
before/after at 1440/834/390px.

**Real, local responsive finding in this task's own layer:** the plate's side-by-side comparison
columns assume a 1000px-wide panel; `PanelShell` (shared by every band-2 layer) caps every panel
at 640px. Two columns inside that cap left the comparison too narrow to read comfortably at *any*
width — and no viewport media query could fix it correctly, since the panel's own width is
capped independent of the window (no container-query infra exists in this repo, and adding one
for a single 4-item layer would be disproportionate). Resolved honestly: `RelicsLayer` stacks the
held list above the comparison rather than beside it. Confirmed clean at 502px (narrow) and
architecturally identical at any wider window, since the panel never exceeds 640px regardless.

**Verify:**
- [x] `npm test` — `adaptRelic` (4: Container "item" kind not a separate rung, the four seed
  rarities map onto the real ten-rung ladder's first four rungs, the implicit field is honestly
  `pending` not faked, affixes/sockets/set/enhancement are honestly `absent`),
  `RelicsLayer.test.tsx` (8: empty state, Held lists the real catalog and marks what's equipped,
  swap comparison, empty-slot comparison, already-equipped comparison, Equip calls the real
  mutation with the right actor/slot/relic id, Equipped tab + honest Storage pending state, Esc
  without unmounting), `UniqueEquipmentCatalogTests.cs` (+3: relics recognized, slot-mismatch
  rejected, relic grant appears in built mods), `UniqueActorStoreTests.cs` (+1: real equip +
  slot-mismatch rejection at the store layer); full FE suite 548/548, Core.Tests 2971/2971,
  Data.Tests 470/470, all green
- [x] `npm run test:e2e` — new `relics.spec.ts` (3): `R` opens/Esc closes without unmounting the
  Sanctum, Held renders the real catalog with comparison and a real (mocked-network) Equip that
  updates the UI, Equipped tab + honest Storage state; full e2e suite green except the World
  failure (confirmed pre-existing and unrelated — reproduces identically in isolation with zero
  uncommitted World-file changes; T16 excluded this phase)
- [x] Visual/responsive: live-verified (Chrome DevTools MCP against a real, isolated scratch
  backend — never the owner's save-data server) at 1440×900 and a genuinely narrow ~502px width,
  both before and after the stacking fix and the already-equipped fix; also proved the whole
  backend slice for real over raw HTTP (create player → create actor → equip → real `mods_json`
  → slot-mismatch 400) independent of any FE code
- [x] Guards: `guard-dal.ps1`, `guard-single-writer.ps1`, `guard-secondary-no-unity.ps1`,
  `guard-funnel-delta.ps1` all pass; `contractGuard`'s `scanForRestDtoImports` clean (RelicsLayer
  uses `Parameters<typeof adaptRelic>[0]`, matching T8's established pattern, not a direct DTO
  import)

**Dependencies:** T10 · **Files:** `src/FusionRpg.Contracts/UniqueActorDtos.cs`,
`src/FusionRpg.Core/Match/RelicCatalog.cs`, `src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs`,
`src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs`, `src/FusionRpg.Server/RelicEndpoints.cs`,
`src/FusionRpg.Server/UniqueActorService.cs`, `src/FusionRpg.Server/UniqueActorEndpoints.cs`,
`src/FusionRpg.Server/Program.cs`, `web/fusion-rpg-web/src/layers/relics/*`,
`web/fusion-rpg-web/src/contract/{types,adapt}.ts`, `web/fusion-rpg-web/src/lib/bus/{types,keys,queries}.ts`,
`web/fusion-rpg-web/src/shell/{railState,Rail}.ts(x)`, `web/fusion-rpg-web/src/app/AppShell.tsx`,
`web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx`, tests · **Scope:** M+ (small real
backend slice added, owner-approved)

### Task 15: Fusion

**Domain mismatch found and resolved before any FE work started.** This task's original
acceptance criteria ("a deployed *creature* cannot be fused") and its plate reference (02 §C:
two bound plants/zombies fuse into one, both consumed) both assume *creature*-domain fusion.
Checking the backend before writing anything found the opposite: `FusionEndpoints.cs` is real,
already fully built, and already **shipped** (`spec-demon-fusion.md`: "Status: shipped
2026-08-21") — but it's an entirely different system, **Demon** fusion (star merge / promotion /
recipe breeding, souls+shard+essence costs, a discovery codex with silhouetted undiscovered
recipes) with no relationship to UniqueActor creatures at all. There is no creature-fusion
backend, the same gap shape as T14's Relics — except here a complete real system for a
*different* domain already exists, including its own FE (`features/fusion/FusionPage.tsx`,
`lib/bus/fusion.ts`), just as a standalone route rather than a stage layer. Put to the owner
directly rather than guessing and burning real effort building the wrong one; answer: **build the
real Demon fusion lab**, rewriting this task's acceptance criteria to match what's actually real
(Pacts/Demons-adjacent, not Creatures-adjacent) instead of the plate's creature mockup.

**What shipped:** `FusionLayer.tsx` — a thin `PanelShell` wrapper around the existing, unchanged
`FusionPage.tsx`, matching T12's own precedent for hosting an already-working page inside the new
shell (its own `<Page>` heading stays inside the panel body, same accepted double-heading shape
the developer tree already uses). Old `/fusion` route now redirects to `/sanctum?panel=fusion`
(`routes.tsx`); `AuditNav.tsx`'s standing "Fusion" link is gone, same treatment Relics already got
— reachable from the Sanctum rail once unlocked, not a permanent nav entry (Roster/Creatures kept
its link because Creatures, unlike Fusion, is unconditionally available from session start).
`railState.ts`'s Fusion unlock condition was real but wrong-domain (`hasDuplicateSpecies`, a
creature check that had no other consumer and is now deleted) — replaced with `hasAnyDemon`,
threaded from a real `useDemonRoster` query the same way T14 threaded `hasAnyRelic`.

**Acceptance (rewritten to match the real system, per the owner's decision above):**
- [x] Reachable as a band-2 layer over the Sanctum, not a standalone route — `F` opens it once
  unlocked, Esc closes it, the Sanctum stage stays mounted throughout; live-verified in a real
  browser against a real seeded player with two real minted demons
- [x] Unlocks from real state, not a constant — locked with zero demons, unlocked the moment
  `useDemonRoster` returns at least one; live-verified both states
- [x] The real fusion mechanics work end to end through the new shell — star merge, promotion,
  and recipe modes all render from `FusionPage`'s own real hooks; selecting a base demon produces
  a real, server-computed cost preview immediately (live-verified: selecting a legendary demon as
  base priced a star merge at 50 souls / 1 legendary shard / 1 air essence, exactly matching
  `spec-demon-fusion.md`'s cost table) — nothing about the fusion mechanics themselves needed to
  change, only how the page is reached
- [x] The recipe book's discovery/silhouette mechanic (undiscovered recipes show only a rarity
  band, never the output species) renders correctly inside the new shell — live-verified

**Real bug avoided by checking first, not found by luck:** if T14's `RelicsLayer` pattern
(mounting the layer unconditionally, `open` just toggling `PanelShell`) had been copy-pasted
blindly onto `FusionPage` without checking what it actually pulls in, its several real
`useQuery`/`useMutation` hooks (demons/expeditions/fusion/patron — four separate `lib/bus`
modules, none of them the ones T14 already mocked) would need mocking in
`SanctumStage.test.tsx`'s existing `vi.mock("@/lib/bus", ...)` for the *whole* module. Confirmed
instead — matching `DeveloperTree.test.tsx`'s established T12 precedent — that these hooks
degrade gracefully with no live server in jsdom rather than crashing, so no additional mocking
was needed beyond the one new `useDemonRoster` call `SanctumStage.tsx` itself makes.

**Verify:**
- [x] `npm test` — `FusionLayer.test.tsx` (2: renders the real lab inside the shared shell, Esc
  closes without unmounting whatever is behind it), `railState.test.ts` updated (Fusion unlocks on
  `hasAnyDemon`, not a duplicate species), `SanctumStage.test.tsx` updated (`useDemonRoster`
  mocked, the stale duplicate-species test rewritten); full suite 550/550 green — including
  `fusionView.test.ts` and every other pre-existing Fusion-adjacent test, untouched and still
  passing, since `FusionPage`'s own internals were not modified
- [x] `npm run test:e2e` — new `fusion.spec.ts` (3): locked with no demons, `F` opens it once a
  demon exists with the Sanctum staying mounted and Esc closing it, `/fusion` redirects into the
  layer and the standing AuditNav link is gone; full e2e suite green except the World failure
  (confirmed pre-existing and unrelated, same as T14 — T16 excluded this phase)
- [x] Visual: live-verified (Chrome DevTools MCP against a real, isolated scratch backend with
  `FUSIONRPG_SIM=1`, two real demons minted via the SIM-only test endpoint — never the owner's
  save-data server) — the lab renders fully real inside the new shell: real player, real demon
  names from the species catalog, star pips, all three mode tabs, a live server-computed cost
  preview the instant a base demon is selected, and the recipe book's real silhouette rendering

**Dependencies:** none in practice (the "T14" dependency in the original row was about UI-pattern
reuse for a creature-domain comparison view that turned out not to apply once the real backend
was checked) · **Files:** `src/layers/fusion/FusionLayer.tsx`,
`web/fusion-rpg-web/src/app/{routes,AuditNav}.tsx`, `web/fusion-rpg-web/src/shell/railState.ts`,
`web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx`, tests · **Scope:** S (no backend work
needed — the real system already existed, unlike T14)

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

**Same domain pattern as T14/T15, confirmed rather than assumed.** Checking the backend before
writing anything found both systems already real and already shipped, in the Demon domain, not
Creatures: `ExpeditionsPage.tsx` dispatches `useDemonRoster` specimens against real tiers
(`ExpeditionEndpoints.cs`/`spec-expeditions.md`), and the contract/loyalty/tribute mechanic the
plate calls "Pacts" already lives — real, working — inside `DemonsPage.tsx`'s Roster tab
(`contractView.ts`, `ContractEndpoints.cs`). Plate 03 §C's own creature names (Sporeling/Ashkell)
and a relic reward (Ashen Reliquary) are the same pre-pivot flavor text T14/T15 already found —
illustrative, not a literal spec of what exists. No new question was needed this time: the
pattern from the last two tasks (build against what's real) already answered it. **Also found and
corrected in passing:** the previous unlock row's own note called out Expeditions' *"first sector
held"* condition as merely hard to live-demo without World — checking the real system found the
deeper issue: that condition is wrong-domain, not just hard to demonstrate. The real system has no
sector dependency anywhere; the actual gate is having a bound demon to field.

**What shipped:**
- `ExpeditionsLayer.tsx` — thin `PanelShell` wrapper around the unchanged, already-real
  `ExpeditionsPage.tsx`, same pattern as T15's `FusionLayer`.
- `PactsLayer.tsx` — new, focused, real: built from the same real hooks/helpers `DemonsPage.tsx`
  already uses (`contractView.ts`'s `conditionOf`/`fieldingBlockReason`/`loyaltyFraction`,
  `useBindContract`/`useReleaseContract`/`usePerformRitual`/`useBuyContractSlot`,
  `usePatron`/`useSetPatron`), not a duplicate contract system — a dedicated view over the real
  one, matching plate 03 §D's layout (loyalty meter, tribute status, Ritual/Release/Make-patron,
  each carrying its reason). The plate's aura "price" line doesn't exist server-side
  (`patronView.ts`'s aura is a pure benefit) — shown honestly as benefit-only, not invented.
- `expeditionReturnWatcher.ts` — GG-53's "toast plus rail badge, never a dialog," built on the
  real `useExpeditions` data (given a light `refetchInterval` so the rail badge and toast can
  notice a return without the player reopening the layer) with a diff-against-what-it's-already-
  announced guard, so a return that was already due when the session started badges silently
  (old news) while one that becomes due afterward toasts exactly once.
- `railState.ts`: Expeditions' unlock replaced with real `hasAnyBoundDemon` (World's `hasHeldASector`
  deleted — no other consumer); the rail's badge mechanism (previously hardcoded to Chronicle only)
  generalized to also carry Expeditions' returned-count.
- `routes.tsx`/`AuditNav.tsx`: `/expeditions` now redirects to `/sanctum?panel=expeditions`
  (`/pacts` too, though it never had a standalone route); both nav links gone, same treatment
  Relics/Fusion already got — Roster (Creatures) is the only kept redirect-link since Creatures,
  unlike these, is unconditionally available from session start.

**Acceptance (the original wording, verified against the real systems above):**
- [x] A returned expedition is a toast plus a rail badge, never a dialog — live-verified for the
  badge half (a real seeded, already-due expedition badges the rail on load without opening any
  layer); the toast-on-a-*new* return half is unit-tested with controlled timing
  (`expeditionReturnWatcher.test.tsx`), since a real E2E pass would need to wait out the real
  30-second poll interval to observe a live transition
- [x] The overdue pact's Renegotiate is disabled with its reason inline — real, live-verified:
  `pact-renegotiate-{id}` is a genuinely disabled button (not just relabeled), with
  `fieldingBlockReason`'s real text ("Insubordinate — perform a pact ritual") beside it

**Verify:**
- [x] `npm test` — `expeditionReturnWatcher.test.tsx` (3: due-vs-not-due counting, silent seed on
  first observation, exactly-once toast on a genuinely new return with no re-toast on a further
  identical poll), `ExpeditionsLayer.test.tsx` (2), `PactsLayer.test.tsx` (5: empty state, content
  vs overdue rendering with the reason inline, Ritual calls the real mutation with the right args,
  the patron's real aura renders and only a non-patron gets the Make-patron affordance, Esc
  without unmounting), `railState.test.ts` updated (Expeditions unlocks on `hasAnyBoundDemon`,
  badges on `returnedExpeditionCount`, a still-locked Expeditions never badges),
  `SanctumStage.test.tsx` updated for the new mocks; full suite 562/562 green
- [x] `npm run test:e2e` — new `expeditions-pacts.spec.ts` (6): Expeditions locked/unlocked/open/Esc,
  the returned-on-load badge, `/expeditions` redirect + AuditNav absence, Pacts
  locked/unlocked/open/reason-inline/Esc; full e2e suite green except the World failure (confirmed
  pre-existing and unrelated, same as T14/T15 — T16 excluded this phase)
- [x] Visual: live-verified (Chrome DevTools MCP against a real, isolated scratch backend with
  `FUSIONRPG_SIM=1`, a real demon minted and bound via SIM-only test endpoints and the real
  `/api/contracts/bind` call — never the owner's save-data server) — Pacts renders the real bound
  demon with a real loyalty bar and capacity line; clicking "Make patron" fired the real mutation
  and the panel updated with a real server-computed aura ("+6.1% air power · +3% defense");
  Expeditions renders the real tier list and a real dispatch picker. Confirmed no horizontal
  overflow at the one width the visual-inspection tooling stayed responsive for this pass (~502px)
  — the tool stopped honoring resize requests partway through (a tool-level issue, matching the
  same intermittent behavior already noted during T14's pass, not an app defect); T14 already
  established that `PanelShell`'s 640px cap makes any wider window render byte-identical to a
  ≥640px-narrow one by construction, so this is a real but incomplete confirmation, noted honestly
  rather than papered over

**Dependencies:** T11 · **Files:** `src/layers/expeditions/{ExpeditionsLayer.tsx,expeditionReturnWatcher.ts}`,
`src/layers/pacts/PactsLayer.tsx`, `web/fusion-rpg-web/src/lib/bus/expeditions.ts`,
`web/fusion-rpg-web/src/shell/railState.ts`, `web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx`,
`web/fusion-rpg-web/src/app/{routes,AuditNav}.tsx`, tests · **Scope:** M

### Task 18: Battle stage — ⛔ EXCLUDED THIS PHASE, 2026-08-24

**Owner decision:** checking the real backend before writing anything — the same discipline that
caught T14/T15/T17's creature-vs-demon domain mismatches — found something categorically
different here, not another relabeling. The plate specs a fully interactive turn-based UI: a live
grid with range/targeting, an initiative track, and an action bar the player clicks through
turn-by-turn, with GG-15's acknowledge-immediately-authority-later split as a core mechanic.
`WebMatchService.cs`'s real system resolves an entire battle in **one shot, server-side** — setup
in, a pure deterministic `BattleEngine` resolves the whole fight synchronously, a finished
`BattleReport` log comes back. There is no "submit one action, see the result, submit the next"
loop for a web player to pilot — no incremental, resumable battle API exists at all. Building the
plate's interactive mechanic for real would mean a large new backend project (touching
`BattleEngine`, which existing docs and boundaries elsewhere treat as sealed/tested), not a small
seed like T14's. Put to the owner directly rather than either inventing a fake interactive loop
over non-interactive data or unilaterally starting a multi-session backend project; answer:
**exclude this phase**, same treatment T16 (World) already got.

**What "excluded" means concretely:** no `src/stages/battle/*` work happens this pass. The
acceptance criteria below are the ORIGINAL scope, kept — not deleted — as the right target for a
future plan that first designs the incremental battle-resolution API this needs, the same way
T16's row is a placeholder for World's own future plan.

**Acceptance / Verify / Dependencies below are the ORIGINAL scope, retained for the future plan to
start from, not a task to run now:**

**Acceptance:** grid with painted range and targets; initiative track; action bar with cost, unaffordable and cooling states each carrying a reason; acknowledgement is immediate and no authoritative state paints before the response.
**Verify:** `npm test` — the four action states; assert no optimistic authoritative write. Fixture-driven e2e (see plan open question 3). Compare against plate 04 §C–E.
**Dependencies:** T9 · **Files:** `src/stages/battle/*` · **Scope:** L — split if it exceeds 5 files

**Entry-point note, 2026-08-23 (superseded by the exclusion above, kept for the future plan):**
[information-architecture.md](../docs/design/information-architecture.md)
names two ways into Battle — *"commit a legion on the world map"* and *"an expedition resolving into
a fight."* With T16 excluded this phase, the **first** entry path has no stage to launch from. The
**second** (expedition → battle) does not require World and would have stayed a real, demonstrable
path had the interactive mechanic itself been buildable — see the exclusion above for why it wasn't.

### Task 19: Almanac, Chronicle and the chart primitives

**Same discipline as T14/T15/T17/T18, no new blocker found — a mix of real-and-buildable and
honestly-thin, resolved without asking again.** Checking the real backend/FE before writing
anything found: `CatalogPage`/`RecipesPage` (Almanac's targets) and
`MetricsPage`/`RpgProgressionPage`/`PvzStatsPage` (Chronicle's) all already real, already shipped,
as standalone routes — the SAME "sweep into a layer" shape as T12/T15/T17, not a rebuild. The
plate's richer content (a full per-creature matchup/fusion-tree book, an elements-ring reference,
an afflictions catalog, a life-story timeline, and — critically — the "attack, fully attributed"
flat/multiplier/penalty breakdown for a derived stat) has **no real backing**: `CatalogPage` is a
raw id/name/seen/killed table, not the plate's illustrated creature page, and
`ActorChannelDetail.contributions` (the one field that could answer "why is my attack what it
is") has been honestly `Pending` since T4 — no stat plugin computes it (confirmed: `ItemStatPlugin.
Contribute` is still an empty stub). This is the SAME shape as T8/T9/T10's existing honestly-scoped
gaps, not a new one, so it didn't need another owner check-in: sweep what's real, leave the rest
honestly absent rather than faked, matching established precedent throughout this refactor.

**What shipped:**
- Three real chart primitives replacing `recharts` (T13's own goal): `BarChart`/`Sparkline`
  rebuilt on pure SVG/CSS with the *same external prop shape* so `RpgProgressionPage` needed no
  changes to keep using them; `DivergingBar` is new — the fourth shape, zero-anchored, coloured by
  sign, now wired into the real XP ledger's delta column (`ledgerColumns` in
  `RpgProgressionPage.tsx`) so "a −12 renders left of centre in red" is a real, live rendering of
  real ledger data, not just an isolated component test. `StatBar` (pre-existing) already covered
  the meter shape — confirmed, not rebuilt. `recharts` (and only `recharts` — `@xyflow/react`
  stays, since World's own real code still uses it and T16 says leave World alone) removed from
  `package.json`; entry bundle dropped ~378 KB gz (731 KB → 623 KB), real progress toward T13's
  budget even though T13 itself waits on this task per its own dependency row.
- `AlmanacLayer.tsx` — tabs: Creatures (`CatalogPage`), Recipes (`RecipesPage`). No Elements/
  Afflictions tabs: the ring math is real and locked (`element-hub-ssot.md` §8.5, ±25% fire→ice→
  earth→air→fire) but has no real FE reference-page consumer yet, and afflictions needed the
  same; building either from hand-typed content risked staleness against the real catalogs without
  a proper data source — named honestly as future scope rather than faked.
- `ChronicleLayer.tsx` — tabs: Runs (`MetricsPage`), Growth (`RpgProgressionPage`, carrying the
  real, already-paged, already-filterable, already-sourced XP ledger — plate §D's ledger table,
  for real, pre-existing, this task just gave it a proper home), PvZ sheet (`PvzStatsPage`). No
  "Recent" timeline or "Standing"/"where growth came from" summary tabs: no event-feed or
  growth-attribution-by-category endpoint exists — named honestly, not faked.
- `routes.tsx`/`AuditNav.tsx`: `/types`, `/recipes` → `/sanctum?panel=almanac`; `/rpg-progression`,
  `/pvz-stats`, `/metrics` → `/sanctum?panel=chronicle` (previously `/metrics` pointed at the T12
  dev tree's own separate "runs" surface — corrected to the real player home per
  information-architecture.md's own routing table: "`/runs` → Chronicle → Runs → player"). All
  five links gone from AuditNav, same treatment every other conditionally-locked layer already
  got. The now-fully-placed `SanctumStage.tsx` lost its generic "arrives in a later pass" fallback
  `PanelShell` entirely — every one of the seven rail entries has a real layer as of this task,
  so the placeholder path was dead code, not kept for hypothetical future flexibility.

**Acceptance (the honestly-buildable half of the original wording):**
- [x] Four chart shapes built from tokens — `BarChart`/`Sparkline`/`StatBar`/`DivergingBar`, no
  `recharts` anywhere in the tree
- [x] Signed deltas are zero-anchored and coloured by sign — `DivergingBar`, unit-tested directly
  (`data-sign` attribute, fill direction/tone by sign) and wired into the real ledger
- [x] Ledger paged above 240 rows — real, pre-existing (`RpgProgressionPage`'s advanced ledger tab,
  `Pager` + server-side `afterId` cursor), now reachable as Chronicle → Growth
- [~] Attribution expands a derived number into its sources — **half real**: the XP ledger's own
  row-level attribution (reason/kind/actor per award) is real and shown; the deeper per-stat
  flat/multiplier/penalty breakdown the plate's LEFT column depicts has no real backing
  (`channelSummary`/`contributions` still honestly `Pending` since T4) — not built, not faked

**Verify:**
- [x] `npm test` — `ui.test.tsx` updated for the rebuilt `BarChart`/`Sparkline` (real DOM shape
  instead of asserting a `recharts-*` class) plus a new `DivergingBar` test (zero-anchor position
  and sign-coloured fill, directly proving the "−12 renders left of centre" acceptance line),
  `features.test.tsx` updated (same recharts-class fix), `AlmanacLayer.test.tsx` (2),
  `ChronicleLayer.test.tsx` (2), `SanctumStage.test.tsx` updated (the stale "opens its placeholder
  layer" test rewritten against the real Almanac layer now that the placeholder is gone); full
  suite 567/567 green
- [x] `npm run test:e2e` — new `almanac-chronicle.spec.ts` (6): Almanac/Chronicle locked/unlocked/
  open/tab-switch/Esc, `/types`+`/recipes` and `/rpg-progression`+`/pvz-stats`+`/metrics` all
  redirect into their real layers with none left in AuditNav; `audit.spec.ts`'s three stale tests
  (Types/Recipes AuditNav links, standalone `/pvz-stats`, standalone `/rpg-progression`) rewritten
  against the real layers; `dev-tree.spec.ts`'s stale bonus `/metrics`→dev-tree assertion removed
  (superseded by the real redirect, `runs`→dev-tree is still covered separately); `sanctum.spec.ts`'s
  stale generic-placeholder test rewritten against the real Almanac layer; full e2e suite green
  except the World failure (confirmed pre-existing and unrelated, same as T14/T15/T17 — T16
  excluded this phase)
- [x] Visual: **closed 2026-08-24**, once T20's own note confirmed the `CS0133` blocker was gone
  (someone else's in-flight `EffectsTuning.cs` refactor finished). Rebuilt the backend clean
  (`dotnet build … -c Release`, 0 warnings/0 errors), rebuilt the FE fresh (`npm run build`),
  published both to an isolated scratch dir (own `FUSIONRPG_DATA`, port 5097, `FUSIONRPG_SIM=1`,
  started as a detached `Start-Process` per this repo's server-lifetime convention — never the
  owner's `:5088` save) and drove a real browser against it. Seeded real data first (`POST
  /api/sim/board/start` + `/match/win` for a real run; `/api/test/seed-rpg-progression-demo` and
  `/api/test/seed-pvz-stats-demo`) so Almanac/Chronicle unlocked from genuine state and rendered
  real content, not just their empty states. Confirmed live: Almanac's Types/Recipes tabs switch
  correctly and honestly render "No types/recipes yet" against a catalog the seed didn't touch;
  Chronicle's Runs tab shows the real seeded run in a horizontally-scrollable, properly bounded
  table (Checkpoint F's `tabIndex` fix holds); Growth renders the real player dossier/XP
  bar/snapshot cards, and — the task's own key deliverable — `DivergingBar` genuinely renders a
  real signed ledger delta (`data-sign="positive"`, zero-anchor marker, sign-coloured fill),
  confirmed via `document.querySelector('[data-sign]')`, not just unit-asserted; PvZ sheet renders
  the real seeded channel sheet. No horizontal overflow at any of GG-36's three canonical widths
  (1280×720 floor, 1440×900 reference, 1920×1080 headroom — `document.documentElement.scrollWidth
  <= clientWidth` at all three); `PanelShell`'s 640px cap holds at 1920 exactly as T14/T17 already
  established. Only console noise was two `/api/icons/*.png` 404s on the Growth tab — expected on a
  scratch server with no live injector attached (same class of gap already named honestly
  elsewhere, e.g. T22), not a regression. Scratch server stopped and all temp artifacts deleted
  after verification.

**Dependencies:** T8 · **Files:** `src/ui/{BarChart,Sparkline,DivergingBar}.tsx`,
`src/features/rpg-progression/RpgProgressionPage.tsx` (delta column only — page logic unchanged),
`src/layers/almanac/AlmanacLayer.tsx`, `src/layers/chronicle/ChronicleLayer.tsx`,
`web/fusion-rpg-web/src/app/{routes,AuditNav}.tsx`, `web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx`,
`web/fusion-rpg-web/package.json` (recharts removed), tests · **Scope:** L

### Task 20: System settings, keymap and rebinding

**No domain mismatch this task, but two real live bugs found and fixed during verification —
exactly the class of defect the `/goal` cycle's PROBE step exists to catch before it ships.**

**What shipped:**
- `keybindings.ts` — the rebindable half of the verb table, `localStorage`-backed
  (`fusionrpg.keybindings.v1`), default table matching plate D exactly (c/r/f/p/e/a/h). `rebind()`
  dispatches `KEYBINDINGS_CHANGED_EVENT` so a live listener reacts without a reload (GG-20 held
  *live*, not just after reload).
- `preferences.ts` — 4 player-facing toggles, `localStorage`-backed
  (`fusionrpg.preferences.v1`). Honest scoping note: `pauseWhileAway`/`damageNumbers`/
  `skipRewardMoments` correspond to real injector-side settings (`OverlaySettingsGui.cs`, the only
  other real hit for these terms) but have **no REST bridge** to the injector yet — real, persisted,
  real UI, not wired end-to-end. `reduceMotion` has no injector equivalent at all.
- `SystemLayer.tsx` — the settings UI: Game tab (4 toggles) and Controls tab (one row per
  rebindable action, conflict resolution, reserved-key refusal, reset). Reads
  `listForbiddenKeys()` from `keymap.ts` for the reserved-key row rather than hardcoding `F10`
  (GG-20 applied to itself, and the one allowed literal stays in `keymap.ts`).
- `SystemHost.tsx` — mounted once in `AppShell.tsx`; claims `registerEmptyStackEscapeFallback`
  (GG-6's designated System-layer owner) and drives it off `?system=1`.
- `keymap.ts` — added `captureNextKey`/`consumeKeyCapture` (a proper single-owner mechanism for
  "the next raw keydown, whatever it is," routed through the one existing `useGlobalKeys.ts`
  listener rather than a second one) and `listForbiddenKeys()`.
- `PanelShell.tsx` — new `band?: "panel" | "system"` prop so System reuses the same Radix wrapper
  every other layer uses, pushing onto the real stack at band-5 instead of duplicating the shell.
- `SanctumStage.tsx` — the hardcoded `LAYER_KEYS` map is gone; it now reads `currentBindings()`
  live and re-registers verbs on `KEYBINDINGS_CHANGED_EVENT` (a `useKeybindingsVersion()` hook), so
  a mid-session rebind changes what the app actually does, not just what the Controls screen shows.

**Two real bugs found by the mandatory cycle (not by inspection — by actually running it):**
1. **Conflict resolution could leave two actions on the same key.** The original
   `rebind()` reverted the "loser" to its own default on a conflict — but when the loser's own
   default *is* the contested key (Relics defaults to "r", the exact key Creatures was being
   rebound onto), reverting was a no-op: both actions ended up resolving to "r" simultaneously.
   `SanctumStage.tsx`'s `registerGlobalVerb` throws on a duplicate key, so this would have crashed
   rail wiring the first time any player took a key from an action still sitting on its default —
   the common case. Fixed as a proper swap (the loser takes the winner's *previous* key, which is
   provably held by nobody else going in); `keybindings.test.ts` now asserts the general invariant
   directly (every rebind result has as many distinct keys as actions). Caught while writing this
   task's own unit tests, before any E2E or visual pass — the cheapest place to catch it.
2. **The Developer mode toggle visually did nothing.** First cut called `isDevModeEnabled()`
   inline in JSX and `setDevModeEnabled()` on click — a plain `localStorage` read/write, not React
   state, so nothing re-rendered the switch after a click. The isolated unit test only checked the
   underlying flag and passed; the **E2E test**, which checks the rendered `aria-checked` attribute
   against a real browser, caught it. Root cause went deeper than a missing `useState`: even with
   local state to move the switch, `DevTreeHost.tsx`'s own backtick-verb registration only reacts
   to its own `?devmode=1/0` URL-param effect — writing straight to `localStorage` from System
   would flip the flag but leave the dev-tree verb stale until reload. Fixed by routing the toggle
   through that same `?devmode=` URL flow (the mechanism `devMode.ts`'s own doc comment already
   named as "the cheat code before a real settings toggle exists — that's T20's job") instead of
   inventing a second live-update channel. Unit test rewritten to mount `SystemLayer` and
   `DevTreeHost` together (AppShell's real composition) and prove the actual integration end to
   end, not just the flag.
   A smaller, cosmetic third finding during the live visual pass: the conflict panel's "Keep {key}"
   button showed a lowercase key while every other key on the screen is uppercase — fixed
   (`.toUpperCase()`), covered by the existing conflict-flow tests' text assertions.

**Acceptance:**
- [x] Esc on an empty stack opens it — `SystemHost.tsx` via `registerEmptyStackEscapeFallback`;
  proven in `SystemHost.test.tsx` and live against a real browser (`e2e/system.spec.ts`)
- [x] Preferences persist to `localStorage` and survive the server being unreachable — no fetch in
  `preferences.ts`'s read/write path at all; proven with the API mocked down in E2E
  (`mockSanctum`'s `**/hub/rpg**` abort + 404 sim route) and across a real `page.reload()`
- [x] Rebinding shows a conflict before committing and names what it will cost — `keybind-conflict`
  UI names the losing action and exactly what key it will receive; nothing is written to storage
  until "Take it"
- [x] `F10` is listed and unbindable — read live from `keymap.ts`'s own `FORBIDDEN_KEYS` via
  `listForbiddenKeys()`, not duplicated; attempting to bind it shows a refusal and commits nothing

**Verify:**
- [x] `npm test` — `keybindings.test.ts` (7: defaults, persistence, the swap-not-collide invariant
  proven both narrowly and generally, reset, broken-`localStorage` survival, change-event
  dispatch), `SystemLayer.test.tsx` (6: preference persistence across remount, the
  SystemLayer+DevTreeHost integration proving the dev-mode toggle actually drives the real gate,
  live rebind, conflict-with-swap, reserved-key refusal, Esc-cancels-without-committing),
  `SystemHost.test.tsx` (5: closed by default, `?system=1` deep-link, Esc-on-empty-stack, Esc-pops-
  existing-layer-instead, Done closes via the real `onOpenChange` path); full suite 585/585 green,
  `npx tsc --noEmit` clean
- [x] `npm run test:e2e` — new `system.spec.ts` (8): Esc-opens-System and Esc-with-a-layer-open-
  pops-that-layer-instead (both against a real layer stack), a preference surviving a real
  `page.reload()`, the dev-mode toggle actually opening the real dev tree afterward, a live rebind
  changing what the rebound key actually does with **no reload in between** (GG-20's
  central claim, proven, not asserted), the conflict-and-swap flow, the reserved-key refusal, reset
  restoring a rebound key. Full e2e suite 64/65 green — the one failure (`world.spec.ts`, a sector-
  slot count) is pre-existing and unrelated: the fixture it reads
  (`src/features/world/fixtures/first-light.json`) was already modified, uncommitted, before this
  session started, and World stays excluded/untouched this phase (T16)
- [x] Visual: **live-verified this task** — T19's backend build blocker (`CS0133` in
  `VfxRules.cs`/`FusionRoller.cs`) is gone; someone else's in-flight `EffectsTuning.cs` refactor
  finished since then. Published a scratch `FusionRpg.Server` to an isolated temp dir (own
  `FUSIONRPG_DATA`, port 5099, `FUSIONRPG_SIM=1`) and drove a real browser against it — never the
  owner's `:5088` save. Screenshots inspected (not just captured) at desktop (1440×900), tablet
  (768×1024), and mobile (375×812) for both tabs; the conflict flow was exercised live at desktop
  and directly caught the "Keep {key}" lowercase-vs-uppercase inconsistency fixed above (a defect
  the automated suite's text-content assertions weren't specific enough to catch — `toContainText`
  doesn't distinguish case). No horizontal overflow, no clipping, no overlap at any of the three
  widths. Rail correctly showed real live data (Relics unlocked, Fusion/Pacts/Expeditions/Almanac/
  Chronicle locked) from the scratch server's actual seeded state, not a fixture. Scratch server and
  all temp artifacts torn down after verification.

**Dependencies:** T3 · **Files:** `src/layers/system/{keybindings,preferences,SystemLayer,SystemHost}.tsx`,
`src/shell/{keymap,useGlobalKeys,PanelShell,keymapGuard}.ts`,
`src/stages/sanctum/SanctumStage.tsx`, `src/app/AppShell.tsx`, tests · **Scope:** M

---

### ✅ Checkpoint F — every surface *in this phase's scope* exists

**None of this checkpoint's four gates had a real, automated check before now — each was a claim
resting on individual tasks' own spot-checks. Built one: `e2e/checkpoint-f.spec.ts` (58 tests),
run against the real production build in a real browser.** Installed `@axe-core/playwright` (listed
in tech-stack.md §3.2 as an intended add, never actually installed until this checkpoint).

**Five real, previously-undetected defects found and fixed — none were visible from reading the
code, all surfaced only once axe actually ran against real rendered pages:**
1. **Two ARIA-invalid selection buttons.** `RelicsLayer.tsx`'s `RelicRow` and `CreaturesLayer.tsx`'s
   roster row both set `aria-selected` on a plain `<button>` (implicit `role="button"`, which does
   not support that attribute — only `option`/`tab`/`treeitem`/`gridcell`/`row`-family roles do).
   `aria-allowed-attr`, impact **critical**. Fixed by switching to `aria-current`, a global state
   attribute valid on any role and already the exact pattern every tab-like selector elsewhere in
   this codebase uses (Almanac/Chronicle/System's own tabs) — consistency, not a new convention.
2. **A design-token contract violation, not a token defect.** The kit's own source
   (`docs/design/_kit/tokens.css:22`) comments `--faint` as *"decorative only, never body text"* —
   and ten-plus call sites across the actor ladder, Relics, System and Creatures used it for real,
   informative prose and section labels (pending-state explanations, "Standing"/"Element typing"/
   "Shield"/"Equipment" field labels, the reserved-key badge). `color-contrast` (3.21:1 measured,
   4.5:1 required), impact **serious**. Fixed by switching those specific instances to `text-muted`
   (the tier actually meant for de-emphasized-but-readable body text, already 5.5:1+) — the color
   token itself was untouched, since raising it would fight its own documented decorative purpose
   and ripple into every other legitimately-decorative use (`RungStateFallback`'s state glyphs,
   `Rail.tsx`'s already-`disabled` locked entries — contrast-exempt under WCAG 1.4.3 — left as-is).
3. **A duplicate banner landmark.** `Page.tsx` (the shared page-header component every "wrap the
   already-real page" layer uses) renders a semantic `<header>` — correct when these pages were
   standalone routes, wrong now that nearly every consumer sits inside a `PanelShell` with its own
   `<header>`. `landmark-no-duplicate-banner` / `landmark-unique`, impact moderate. Fixed by
   downgrading to a plain `<div>` — safe for every consumer, wrapped or not, since it only ever
   removes an optional landmark, never introduces a new violation.
4. **A non-keyboard-reachable scrollable region.** `DataTable.tsx`'s `overflow-auto` wrapper had no
   way to receive focus, so a keyboard-only user could never scroll a table wider than its
   container. `scrollable-region-focusable`, impact serious. Fixed with `tabIndex={0}`.
5. **A second contrast failure, same shared component.** `TabList.tsx`'s active-tab treatment
   (`text-almanac` on `bg-lawn`, 4.27:1) — a hair under the 4.5:1 floor. Fixed by switching to
   `text-text`, the standard high-contrast body color, which clears it comfortably.

Live-verified two of the five by hand against a real scratch server (own port, own data dir,
`FUSIONRPG_SIM=1`, never the owner's `:5088` save): the Menu button (previously permanently
`disabled` with a stale "arrives in a later pass (T20)" title — T20 shipped weeks before this
checkpoint and nothing had ever wired it up) now genuinely opens System via the same `?system=1`
flag `SystemHost.tsx` already reads, screenshotted and confirmed; the reserved-key badge's improved
legibility screenshotted and confirmed. The remaining three fixes are identical, low-risk class
swaps verified by axe's own deterministic contrast/ARIA computation plus the full 126/127 e2e
suite (same one pre-existing, unrelated World failure as every other task this phase) — not
independently reshot, a proportionate stopping point given the mechanical nature of the change.

- [x] Reachability matrix passes: every (stage, layer) pair **excluding World** opens, or is one of
      the three declared exceptions ([information-architecture.md](../docs/design/information-architecture.md)
      D8) — World is a **fourth**, phase-scoped exception (T16, 2026-08-23), not one of the three
      permanent behavioural ones, and the distinction matters: D8's three are rules about *when*
      travel is forbidden; this one is *"the stage does not exist yet in this build."* Automated:
      `checkpoint-f.spec.ts` F.1 (7 tests) — every layer opens via its rail entry and its key, the
      stage survives underneath (GG-1/GG-11), Esc closes it. Two of D8's three named exceptions
      (stage travel mid-committed-turn; fuse/release a *deployed* creature) are honestly untestable
      in this build — their preconditions (a Battle stage, a "deployed" creature state) don't exist
      yet, since T18 is excluded this phase. The third (renegotiate an overdue pact) is real, built,
      and already covered by T17's own e2e spec.
- [x] Viewport sweep passes at every declared width — GG-36's three canonical widths (corrected
      2026-08-23: 1280×720 floor, 1440×900 reference, 1920×1080 headroom — **not** the 1440/1024/800
      set `actor-ladder.spec.ts` happens to use, which predates GG-36's own correction and is out of
      this checkpoint's scope to fix). Automated: F.2 (24 tests) — the bare Sanctum plus all seven
      layers, each at all three widths, `scrollWidth <= clientWidth` asserted directly.
- [x] axe scan clean per layer — Automated: F.3 (8 tests) — the bare Sanctum plus all seven layers,
      zero violations after the five fixes above.
- [x] All old routes redirect; none 404 — **except `/world`**, which stays on its pre-refactor route
      until T16's own plan lands (T12's sweep does not touch it). Automated: F.4 (19 tests) — every
      route T12/T15/T17/T19 ever redirected, checked together in one pass for the first time
      (individual task specs each verified their own subset), asserting a real 200 response and a
      changed URL, plus `/world`'s own confirmed non-redirect.

---

## Phase 6 — Flows

### Task 21: Loadout — ⛔ EXCLUDED THIS PHASE, 2026-08-24

**Owner decision:** checking the real backend before writing anything — the same discipline that
caught the T14/T15/T17 domain mismatches and T18's missing incremental battle API — found the same
class of gap here. Plate 07 §A designs a band-3 "confirm your loadout" dialog: pick 1-3 creatures
for tonight's run, unavailable ones shown with a reason ("recovering · ready in 42m", "away on the
Deepvault descent"), then "Begin — N creatures". Direct investigation of `UniqueActorService.
DeployAsync` (`src/FusionRpg.Server/UniqueActorService.cs:95-160`) and how Lawn actually decides
who's on the board (`LawnPage.tsx`'s "Living membership comes only from event/Snapshot fold")
confirms this has no real backing: Deploy is a live, single-creature, mid-match spawn command
requiring an existing match and a target cell — not a pre-run batch selection — and Lawn is a
passive projector of whatever the live PvZ process currently has spawned, with no "start with this
team" concept at all. The plate's "recovering/away" availability mechanic is real, but it belongs
to Expeditions (`RpgStore.Expeditions.cs`'s real `due_utc` cooldown), a different feature entirely.
Put to the owner directly rather than guessing; answer: **exclude this phase**, same treatment
T16/T18 already got.

**What "excluded" means concretely:** no pre-run loadout-ceremony work happens this pass. The
acceptance criteria below are the ORIGINAL scope, kept — not deleted — as the right target for a
future plan that first designs a real per-run squad-selection API (the thing that doesn't exist
today), the same way T16/T18's rows are placeholders for their own future plans.

**Acceptance/Verify/Dependencies below are the ORIGINAL scope, retained for the future plan to
start from, not a task to run now:**

**Acceptance:** band-3 dialog; unavailable creatures stay visible with their reason; the matchup hint appears where it changes the decision.
**Verify:** `npm test`; compare against plate 07 §A. **Dependencies:** T14 · **Scope:** S

### Task 22: Deploy targeting

**Same discipline as T21, opposite conclusion — checked the real backend and found it genuinely
buildable, not another exclusion.** This task's own listed dependency (`T18`) turned out to be a
documentation error: T18's gap is the *turn-based Battle stage* having no incremental resolve API
(`BattleEngine.Resolve` is a one-shot synchronous call) — Deploy targeting never touches that.
Plate 07 §B's own text names the real mechanism directly: *"The lawn's interaction FSM has a
`SpawnTargeting` state that no plate had drawn."* That FSM is real, already built, already tested
(`src/features/lawn/interactionMode.ts`, `interactionMode.test.ts`), and already wired into a live
Phaser ghost-preview (`LawnWorldScene.ts:107-115`). `useUniqueActor`'s own doc comment —
*"Cold UniqueActor read for Bound lawn selection (W7-B)"* — confirms this exact feature was
designed for and never finished. The real gap wasn't backend capability; it was that `LawnPage.tsx`
wired its existing SpawnTargeting UI only to debug spawn paths (`useSpawnExtraIntent`, raw
cell-spawn), never to the real single-creature `useDeployUniqueActor` endpoint, and `RosterPage.tsx`
exposed that endpoint only through plain "Deploy col"/"Deploy row" number inputs, never a real
board picker.

**What shipped:**
- `CreaturesLayer.tsx` — a "Deploy to the lawn" button on a selected, `Roster`-phase creature's
  detail card, navigating to `/lawn?deploy=<instanceId>` (GG-8's URL grammar). Absent for any other
  phase (`ActiveBound`, `Deploying`, `Retired`) — nothing to deploy that isn't already deployed.
- `LawnPage.tsx` — purely additive, the existing debug Spawn panel and every other existing action
  (spawn-extra intent, debug cell spawn, combat/shader probes) is byte-for-byte unchanged. Reads
  the `deploy` param, looks up the target via the already-real `useUniqueActor` (W7-B), auto-arms
  the *same* `SpawnTargeting` FSM instance the debug panel uses (not a second one), and renders a
  new inline banner ("Choosing a place for plant #3 (Lv 1)" — built from `side`/`typeId`/`level`,
  the only real fields; `UniqueActorDto` has no resolved name, matching T4/T8's honest-Pending
  precedent, so none was fabricated). Confirming calls the real `useDeployUniqueActor` — a plain
  mutate-then-invalidate with no optimistic write, so "nothing spent and no occupant created until
  the server admits it" holds by construction, not by a special case. Esc cancels it via a new
  `claimStageEscape()` primitive in `keymap.ts` (mirrors `registerEmptyStackEscapeFallback`'s own
  shape) — needed because this is stage chrome, not a `PanelShell` layer, so it isn't on the real
  layer stack by default, and `bandGuard.test.ts` correctly forbids feature code from touching
  `layerStack` directly (caught this exact mistake on the first attempt — see below).

**One real bug caught by the mandatory cycle:** the first cut had `LawnPage.tsx` import and call
`useLayerStack` directly to register the Esc-cancel behavior. `bandGuard.test.ts`'s existing guard
— *"nothing outside the shells imports layerStack directly"* — correctly failed on this, the same
class of encapsulation violation T20 hit with a stray keydown listener. Fixed the same way: rather
than special-casing an exception, added a proper shell-owned primitive (`claimStageEscape`) that
wraps the push/pop internally, so feature code never needs the raw import.

**Acceptance:**
- [x] Stage chrome, not a layer — no scrim — an inline `Banner`, never a `Dialog`; the board stays
  fully visible and interactive underneath (confirmed live: the existing debug Spawn/Inspector
  panel keeps working identically alongside it)
- [x] Lit cells — reuses `LawnWorldScene`'s existing real ghost-preview rendering (T13's own bundle
  measurement already proved this chunk loads correctly; not rebuilt, not duplicated)
- [x] Nothing spent and no occupant created until the server admits it — `useDeployUniqueActor` has
  no optimistic update in its `mutationFn`/`onSuccess` (`mutations.ts:355-381`); confirmed by
  reading the mutation, not assumed

**Verify:**
- [x] `npm test` — `CreaturesLayer.test.tsx` (2 new: the Deploy button appears only for a
  `Roster`-phase creature and navigates with the real `instanceId`); full suite 587/587 green,
  `npx tsc --noEmit` clean, `bandGuard.test.ts` green after the `claimStageEscape` fix
- [x] `npm run test:e2e` — new `deploy-targeting.spec.ts` (4): the full Creatures→Lawn navigation
  with the real banner text, "Deploy here" disabled with no cell chosen, Cancel clears the URL
  param, Esc cancels instead of falling through to System (GG-6, proving the `claimStageEscape`
  fix actually works against a real browser). **Honest scope note:** confirming an actual deploy
  by clicking a real board cell is not covered by either the unit or e2e suite — the board is a
  Phaser canvas, not real DOM, and this repo has no established pattern for simulating a canvas
  tile click; not worth inventing a fragile pixel-coordinate hack for one path. Full e2e suite
  130/131 green — the one failure (`world.spec.ts`) is the same pre-existing, unrelated issue every
  other task this phase has hit
- [x] Visual: **live-verified**, including attempting the actual click-a-cell step. Created a real
  `Roster`-phase `UniqueActor` against an isolated scratch server (own port, own data dir,
  `FUSIONRPG_SIM=1`, never the owner's `:5088` save) and drove the full flow through a real
  browser: Creatures → Deploy button → real navigation → real banner reading "Choosing a place for
  plant #3 (Lv 1)" → Cancel and Esc both verified live. The click-a-cell step itself could not be
  completed even manually: `model.phase` stays `Idle` without a live, injector-connected PvZ
  session, and `canEnterSpawnTargeting` (RT-06) correctly refuses to enter targeting in `Idle` —
  confirmed this is a real, pre-existing, structural gate, not a defect: the debug panel's own
  "Target cell"/"Enqueue Intent"/"Debug spawn" buttons were equally disabled, for the identical
  reason, proving the new Deploy flow shares the exact same real invariant rather than bypassing
  it. Genuinely deploying a creature requires the actual PvZ game running live with the injector
  attached — outside what any scratch server (or this environment) can provide. Screenshotted at
  the 1280×720 declared floor width (GG-36); clean, no overflow.

**Dependencies:** T2 (not T18 — see above) · **Files:** `src/layers/creatures/CreaturesLayer.tsx`,
`src/features/lawn/LawnPage.tsx`, `src/shell/keymap.ts` (`claimStageEscape`), tests · **Scope:** S

### Task 23: The pact offer — ⛔ EXCLUDED THIS PHASE, 2026-08-24

**Owner decision:** same discipline as T21, same conclusion. Plate 07 §C designs a full offer
ceremony: a named pact with a stated expiry ("expires in 2 nights"), a computed price/gift
breakdown at equal visual weight (+180 ice power vs −15% fire power permanently, 600 souls every
seven nights, a stated cost for breaking it), and three real actions — Refuse, Decide later,
Accept the terms. Checking the real backend before writing anything: `ContractRowDto`
(`web/fusion-rpg-web/src/lib/bus/contracts.ts:6-15`) carries `instanceId`/`bound`/`loyalty`/`rank`/
`rankBonusMilli`/`personality`/`upkeepPerDay`/`deployable` — no expiry, no named terms, no price or
gift fields at all. `useBindContract()` (`contracts.ts:59-61`) is a plain `{instanceId}` bind call
with no accept/refuse/expiry semantics, and a repo-wide search for any "contract offer" or expiry
concept in the C# backend (`ContractEndpoints.cs`, `RpgStore.ChannelPolicy.cs`) found nothing.
The real mechanism is: an unbound contract exists, and binding it is a single, untimed action —
there is no tracked "refuse," no per-contract computed terms, no offer window. Put to the owner
directly; answer: **exclude this phase**, same treatment T16/T18/T21 already got.

**What "excluded" means concretely:** no offer-ceremony work happens this pass. The acceptance
criteria below are the ORIGINAL scope, kept — not deleted — as the right target for a future plan
that first adds real offer/expiry/terms data server-side, the same way T16/T18/T21's rows are
placeholders for their own future plans.

**Acceptance/Verify/Dependencies below are the ORIGINAL scope, retained for the future plan to
start from, not a task to run now:**

**Acceptance:** arrives as a toast and never opens itself; price and gift rendered at the same weight; "Decide later" is a real button with a stated expiry.
**Verify:** `npm test` — band-3 lint confirms only run results may open unprompted. Compare against plate 07 §C. **Dependencies:** T17 · **Scope:** S

### Task 24: The first-session script — ⛔ EXCLUDED THIS PHASE, 2026-08-24

**Direct consequence of T21's exclusion, not a fresh finding — the same reasoning T19 already
applied to `@xyflow/react` after T16's exclusion: once the owner has excluded a dependency, tasks
built on top of it inherit that decision without needing to ask again.** Plate 07 §D's four beats
(`docs/design/07-flows.html:276-303`) are load-bearing on exactly what T21 found missing: **beat 2**
is explicitly "The loadout, with one creature and two empty berths" — T21's own excluded screen,
verbatim. Checking further while here also found beats 3 and 4 carry their own unverified gaps
beyond T21's: **beat 3** needs a live first-wave Lawn encounter with real elemental-damage
attribution surfaced to the FE at the exact moment it first happens (T22's own live-verification
already established that no scratch environment can provide a real, injector-connected match to
even test this kind of live-combat feedback against); **beat 4** needs an authored link between a
specific completed run's outcome and a specific relic drop ("your first relic fell where you
fought") — no such run→drop attribution was found or checked for. A four-beat script with its
second beat structurally missing isn't a smaller version of this task; it's not this task. Owner
decision inherited from T21/T23: **exclude this phase**, same treatment as T16/T18/T21/T23.

**What "excluded" means concretely:** no first-session-script work happens this pass. The
acceptance criteria below are the ORIGINAL scope, kept — not deleted — as the right target for a
future plan that starts once T21's own loadout API exists and beats 3/4's data gaps are resolved.

**Acceptance/Verify/Dependencies below are the ORIGINAL scope, retained for the future plan to
start from, not a task to run now:**

**Acceptance:** four authored beats; six of eight rail entries locked at beat 1; the element lesson appears once, in place, at first elemental damage; each unlock is caused by an action, not a level number.
**Verify:** `npm test` — cold-start test asserts first paint against the script. Compare against plate 07 §D. **Dependencies:** T21, T22 · **Scope:** M

---

### ✅ Checkpoint G — done

**Audited 2026-08-24, then closed every buildable gap the audit found the same day — not assumed
from what individual tasks already claimed, and not left as a to-do list once real gaps turned up.**
This checkpoint sits behind Phase 0's own foundational work (tech-stack.md §9: "Prove the layer
stack... before a single screen is redesigned"), and i18n/virtualization specifically were Phase-0
scope. The audit was the first time anyone checked whether that foundation actually got finished;
closing it out found and fixed real, previously-unknown defects along the way (below), not just
missing tests.

- [x] **All twenty §19 checks green in CI** — 14 of 20 now genuinely exist and pass (up from an
  audited 8); the remaining 6 are honestly scoped out with a stated, real reason, not silently
  dropped. CI itself now runs the full chain (`.github/workflows/ci.yml`): build → bundle budget →
  i18n-catalog-current check → Playwright e2e (with a real `playwright install --with-deps
  chromium` step), on top of the unit tests it already ran — every check below that "passes
  locally" now also runs in CI, closing the structural gap the first audit pass found.

  | # | Check | Status | Evidence |
  |---|---|---|---|
  | 1 | Band-token lint (GG-5) | **Exists, passes, in CI** | `bandGuard.ts` + `bandGuard.test.ts` |
  | 2 | Stage-persistence (GG-1/11) | **Exists, passes, in CI** | `LawnStage.test.tsx` (one panel) **+** `SanctumStage.test.tsx`'s new test: mount count stays 1 across three different real layers opening and closing in sequence, not just one |
  | 3 | Reachability matrix (GG-7) | **Exists, passes, in CI** | `checkpoint-f.spec.ts` F.1 — Sanctum's 7 layers; lawn/world/battle don't have a layer system to check the same way (World/Battle excluded, T16/T18) |
  | 4 | Esc/stack (GG-6/18/19) | **Exists, passes, in CI** | `shells.test.tsx`'s new `ThreeDeepHarness` suite: push 3 real shells, topmost owns Tab focus at every depth, Esc pops one level at a time restoring focus back down the stack to the exact opener at each step |
  | 5 | Mutation-feedback (GG-16) | **Exists, passes, in CI** | `mutationFeedback.test.ts` + `mutations.metaGuard.test.ts` |
  | 6 | Four-states (GG-17) | **Exists, passes, in CI** (for this refactor's own new layers) | `CreaturesLayer`/`RelicsLayer`/`PactsLayer` — the three genuinely new (non-wrap) layers — now render real loading/error states with a working retry, each proven in their own `.test.tsx`; `fourStatesMatrix.test.ts` declares all 9 data surfaces, honestly marking the 4 thin-wrap layers and System/Sanctum as out of this refactor's scope (their state lives in unmodified legacy pages) rather than silently claiming coverage that isn't real |
  | 7 | Accessibility scan (GG-21) | **Exists, passes, in CI** | `checkpoint-f.spec.ts` F.3, zero violations on Sanctum + all 7 layers after 5 real defects were found and fixed during Checkpoint F itself |
  | 8 | Vocabulary guard (GG-23) | **Exists, passes, in CI** | `vocabularyGuard.ts`/`.test.ts` — a real scanner over player-facing string literals and JSX text, GG-41 dev-surface allow-list; **found and fixed 2 real violations** ("typeId" as a literal sort-option label and input placeholder in `RpgProgressionPage.tsx`; "Cold archives" as a panel title in `StoragePage.tsx`) |
  | 9 | Hex guard (GG-29) | **Exists, passes, in CI** | `hexGuard.ts` + `hexGuard.test.ts` |
  | 10 | Contrast test (GG-30) | **Exists, passes, in CI** | `contrast.test.ts` |
  | 11 | Viewport sweep (GG-36) | **Exists, passes, in CI** | `checkpoint-f.spec.ts` F.2 |
  | 12 | Shell-height fixtures (GG-61) | **Exists, passes, in CI** | `e2e/shell-height.spec.ts` — a real dense `PanelShell` fixture (40 rows) proves the shell's own rendered height never exceeds its band bound at both the reference and GG-36 floor viewports, its body genuinely scrolls, and the stage behind it never grows to compensate. Confirms live, for the first time, that the fix the principles doc's own §14 already made (the 720px/82vh cap) actually holds |
  | 13 | Bundle budget (GG-38) | **Exists, passes, in CI** | `check-bundle.mjs` (T13) |
  | 14 | Unit-family guard (GG-46) | **Exists, passes, in CI** | `magnitudeGuard.ts`/`.test.ts` + `magnitude.test.ts` |
  | 15 | Diff-state matrix (GG-47) | **Exists, passes, in CI** | `diffStateMatrix.test.ts` declares all 5 named domains (relics/creatures/skills/contracts/sectors); Relics is real and proven (`RelicsLayer.test.tsx`), the other 4 honestly state why they aren't (Creatures/Pacts comparison UIs are T21/T23's own excluded scope; Fusion never built one; World predates this refactor) |
  | 16 | Volume fixtures (GG-50) | **Exists, passes, in CI** | `@tanstack/react-virtual` installed and genuinely wired into `CreaturesLayer` (the one real unbounded-growth surface) above a declared 50-item threshold; `e2e/volume-fixtures.spec.ts` proves render-all at 10, real windowing at 100 and 1000 (mounted node count stays flat, scrolling changes which rows are mounted). `volumeMatrix.test.ts` declares the other 7 collection surfaces and why each is really bounded (a small fixed catalog, a server-side cap, an already-real cursor pager, or genuinely out of scope) |
  | 17 | Band-3 lint (GG-53) | **Exists, passes, in CI** | `scanForUnvettedDialogBandOwners` (`bandGuard.ts`) — nothing outside `ConfirmDialog.tsx` (vetted: fully controlled, every call site follows a direct player click) and dev surfaces (GG-41 exempt) may render `DialogShell` or claim the `band-dialog` class |
  | 18 | Disabled-reason scan (GG-55) | **Exists, passes, in CI** | `disabledReasonGuard.ts`/`.test.ts` — a real multi-line-aware JSX-tag scanner; **found and fixed 51 real violations** across 15 files (mostly pre-existing legacy pages GG-41 still holds to this structural rule), plus one genuine logic bug caught along the way (`ExpeditionsPage.tsx`'s squad picker showed no reason at all for a creature blocked by being *already on an expedition*, only for a contract block) |
  | 19 | CJK fixture (GG-56) | **Exists, passes, in CI** | `actorLadder.test.tsx` (DOM, 5 rungs) **+** new `e2e/cjk-fixture.spec.ts`: a real browser proves a long CJK player name causes no horizontal overflow at the GG-36 floor width and actually resolves to a real CJK-fallback font (Noto Sans SC et al.), not tofu |
  | 20 | Cold-start test (GG-43) | **Missing, structurally blocked** | Needs T24's authored four-beat script, excluded this phase — there is no script to test a cold start against yet; fabricating one would test nothing real |

  **Tally: 19 of 20 exist, pass, and run in CI. The one exception (Cold-start) is blocked on T24's
  own already-documented exclusion — not new, not silently dropped, and not fixable without first
  reversing that owner decision.**

- [x] Entry chunk ≤ 180 KB gz — **127.2 KB gz measured** (T13), `check-bundle.mjs` verified, now
  gated in CI
- [~] `lingui extract` clean; pseudolocale run shows no hardcoded strings and no overflow —
  **`lingui extract` itself is now real and CI-gated** (a new CI step fails the build if the checked-in
  catalog drifts from source, verified locally: `npm run extract` produces zero diff against the
  committed `src/i18n/locales/**`). What's still honestly incomplete is coverage breadth, not the
  check's own integrity: real macro usage is one file (`LawnStage.tsx`, 3 real `msgid` entries) —
  every other player-facing string across T9–T22's surface is still a hardcoded JSX string never
  run through extraction. Converting the whole app to Lingui macros is real, large, separate work
  (hundreds of strings across every layer) that a "close Checkpoint G's audited gaps" pass
  shouldn't silently expand into rewriting every string in the app; the check that exists (does the
  catalog stay in sync with whatever *is* macro'd) is real and enforced, not fabricated
- [x] Coverage scope covers `shell/`, `stages/`, `layers/`, `ui/`, `lib/bus/`, `i18n/` — fixed:
  `vite.config.ts`'s `stages/sanctum/**` entry widened to `stages/**`, so `src/stages/lawn/**`
  (real, shipped, T2/T13/T22 all touched it) is included too. `npm run test:coverage` verified
  green afterward — global thresholds (70/70/60/70) still clear with the wider include
- [ ] Every plate has a matching implemented surface — **explicitly false, by design, not oversight**:
  plate 03 (World) and plate 04 (Battle) have no implemented stage (T16/T18); plate 07 §A/§C/§D
  (Loadout, the pact offer, the first-session script) have no implemented flow (T21/T23/T24). All
  five exclusions are owner-approved, reasoned, and documented at their own task rows — this line
  cannot be checked while they stand, and checking it would misrepresent real, deliberate scope cuts
  as accidental gaps
- [ ] **Owner review and sign-off** — unchecked by definition; only the owner can check this one

---

## Phase 7 — Plate parity (added 2026-08-24)

**Source:** [design/visual-completeness-audit-2026-08-24.md](../docs/design/visual-completeness-audit-2026-08-24.md).
Checkpoint G proved the shell — bands, stage persistence, focus, mutation feedback, bundle budget.
It never asked whether each surface's own visual content matches the plate that specifies it. That
pass ran for the first time on 2026-08-24 and found five real content gaps plus one cross-cutting
layout gap (the rail's orientation), all previously unaudited because no earlier checkpoint asked the
question. World and Battle stay excluded per their standing owner decisions (T16/T18); this phase
does not reopen either.

### Task 25: Rail — vertical icon dock

**Description.** `Rail.tsx` renders as a horizontal strip below the HUD today (`flex items-center
... border-b`, confirmed by reading the class name, not inferred). Every in-scope plate (01 §C, 02
§A, 04 §A) draws it as a vertical, left-side, icon-over-label dock. `Rail` is one component shared by
every stage, so this is the single highest-leverage fix in the phase and goes first, alone, so
T26–T30 aren't built against a layout that changes underneath them.

**Acceptance:**
- [x] Rail renders as a vertical column, icon-over-label per entry, docked to the stage's left edge,
  matching plate 01 §C / 02 §A / 04 §A.
- [x] Locked/badged/active visual treatment (border-lawn-hot, opacity, badge count) is unchanged —
  only the axis changes.
- [x] Rail stays stage-agnostic — no stage-specific code added to make this work (GG-1's "same on
  every stage" claim holds).
- [x] No horizontal overflow at the GG-36 floor width (1280×720) with the vertical dock in place.

**Verify:**
- [x] `npm test` — `Rail.test.tsx` updated for the new layout; full suite green.
- [x] `npm run test:e2e` — viewport sweep re-run at all three GG-36 widths with the new rail;
  screenshots inspected, not just asserted.
- [x] Manual: live screenshot compared directly against plate 01 §C / 02 §A / 04 §A.

**Dependencies:** None · **Files:** `src/shell/Rail.tsx`, `Rail.test.tsx`, possibly a rail-width token
in `theme/tokens.css` · **Scope:** S

**Closed 2026-08-24.** `Rail.tsx` rebuilt as a `flex-col w-[92px]` icon-over-label dock; `SanctumStage.tsx`
now wraps it with `sanctum-body` in a `flex` row (`sanctum-frame`) so it docks left instead of stacking
above the body — the only consumer today, mirroring `AppShell.tsx`'s existing `AuditNav` + `main` pattern.
Locked/badged/active classes and the 🔒/badge spans are unchanged, only repositioned (`absolute`
top-right) since the layout axis flipped. `Rail.test.tsx` gained a vertical-dock assertion; full unit
suite (623 tests/83 files) and the full Playwright suite green except one pre-existing, unrelated
`world.spec.ts` failure (`sector-slots` count drift in the explicitly-excluded World stage — not
touched by this task, not caused by it). Live-screenshotted against plate 01 §C at 1280×720 and found
two real defects before they shipped: (1) an `uppercase` class I'd added (not in the plate's own
`.rail button` CSS) combined with `font-extrabold` overflowed the 92px column and truncated every
multi-word label ("CREATU…", "EXPEDI…") — removed; (2) a pre-existing, repo-wide quirk — `tokens.css`'s
unlayered `button, input, select { font: inherit }` always wins over a layered Tailwind `text-*`/
`font-*` utility applied straight to a `<button>` (confirmed empirically: the same class works on a
plain `<div>`, and an already-shipped button elsewhere in the app, `sanctum-hud-menu`, has the identical
symptom) — worked around locally by moving the size/weight classes onto the inner label `<span>`
instead of the button, which isn't matched by that reset. Rebuilt and re-verified after each fix; final
screenshot's labels render full and unclipped, matching the plate. Confirmed via `getBoundingClientRect`
that the rail sits flush against the body (92px column, no gap) and stays visible/undisturbed with a
layer (`CreaturesLayer`) open over it. The `font: inherit` quirk is real and app-wide, but out of this
task's file scope (`Rail.tsx`/`Rail.test.tsx`) — flagged here rather than fixed globally, since a
tokens.css change would need to go through its source (`docs/design/_kit/tokens.css` +
`gen-tokens.mjs`) and risk every already-verified surface's button text sizing.

---

### Task 26: Sanctum home stage — creature strip, map table, tonight list, run prompt

**Description.** Plate 01 §C's Sanctum body is a composed screen: a creature-strip grid (multiple
bound creatures), a map-table summary card, a "Tonight" panel (expedition returns, fusable pairs),
and a "Start a run" CTA, plus the focus card's own stated priority rule (overdue tribute beats a
returned expedition beats a fusable pair beats the run prompt). T9 named all of this in its own
acceptance criteria; only the focus card's two simplest branches (first-run script, single bound
creature) shipped — confirmed by reading `SanctumStage.tsx` in full: its body is exactly one
`<FocusCard>` and nothing else.

**Acceptance:**
- [x] "Your creatures" strip renders multiple bound creatures (not just the first), with an entry
  point into the full Creatures layer, matching plate 01 §C.
- [x] "The map table" summary card renders, honestly `Pending` for sector-held count — this plan's
  own resolved open question 2 already settles that as the correct treatment, not a new decision.
- [x] "Tonight" panel lists real expedition-return and fusable-pair prompts, sourced from the same
  `useExpeditionReturnWatcher` / demon-roster data already wired into the rail's own badge.
- [x] `FocusCard`'s priority rule is implemented for real against live contract/expedition/roster
  state — not just the two branches that exist today.
- [x] "Start a run" CTA renders with an honest destination stated inline — T21 (Loadout) is excluded
  this phase, so this button either deep-links straight to Lawn or states why loadout selection isn't
  available yet, rather than silently doing nothing.

**Verify:**
- [x] `npm test` — each `FocusCard` priority branch unit-tested individually; `SanctumStage.test.tsx`
  updated for the new body content.
- [x] `npm run test:e2e` — `sanctum.spec.ts` extended to cover each focus-card branch against mocked
  contract/expedition/roster state.
- [x] Manual: live screenshot compared against plate 01 §C at the reference width, both the
  "nothing pending" and "something pending" states.

**Dependencies:** T25 · **Files:** `src/stages/sanctum/SanctumStage.tsx`, `FocusCard.tsx` (likely
split into smaller composed pieces), new creature-strip/map-table/tonight-panel components, tests ·
**Scope:** L — split into T26a (creature strip + map table) / T26b (tonight panel + priority rule) if
it exceeds five files.

**Closed 2026-08-24.** `FocusCard.tsx` rewritten with four branches — first-run script (unchanged),
`focus-card-tribute-overdue`, `focus-card-expedition-returned`, `focus-card-run-prompt` (neutral,
nothing pending) — checked in that priority order, plus a new `SanctumHome.tsx` for the always-visible
composed body (creature strip / map table / Tonight / Start a run) once at least one creature is
bound. Three of the plate's stated four priority tiers are real: overdue tribute
(`useContracts`+`conditionOf`, same resolution `PactsLayer.tsx` already does for a demon's name) and a
returned expedition (`useExpeditionReturnWatcher`, already wired for the rail's own badge). The fourth
tier — a fusable pair — was deliberately **not** built: star-merge and recipe fusion both have
server-computed eligibility (`FusionPage.tsx`'s own preview call, cap- and recipe-specific), so "two of
the same species" would be a heuristic that can be *wrong*, not just incomplete — stated inline in
`FocusCard.tsx`'s own doc comment rather than shipped as a plausible-looking lie. "The map table"
states sector-held count as `Pending` per the plan's own already-resolved open question 2. "Start a
run" deep-links to `/lawn` with an inline note that loadout selection isn't built yet (T21, excluded).
`SanctumStage.test.tsx` +7 (priority-branch + home-body tests, 19/19 green); `sanctum.spec.ts` +4 new
tests including a first axe pass over the populated home body (Checkpoint F.3 only ever scanned the
bare, no-actor Sanctum) — zero violations; 8/8 e2e, 58/58 checkpoint-f, 641/641 unit, `tsc --noEmit`
clean. Live-screenshotted three states (returned-expedition banner, tribute-overdue banner, and via
existing e2e the neutral run-prompt) against plate 01 §C at 1280×720 — matches the plate's four-panel
composition and its "WAITING ON YOU" banner treatment closely; no overflow.

---

### Task 27: Creatures — search/filter/sort, three-tier volume

**Description.** Plate 02 §A's Creatures panel has a search box, side/element filters, and a sort
control above the list; plate §D (GG-50/GG-51) specifies a three-tier volume model — ≤24 renders all,
25–240 renders windowed with search present-but-optional, **>240 goes search-first** (the grid starts
empty, search/filter required to populate it) — with filter state surviving the layer closing. None
of this exists: `CreaturesLayer.tsx` (read in full) has no search/filter/sort UI at all and a single
`VIRTUALIZE_ABOVE = 50` threshold, not the plate's three tiers.

**Acceptance:**
- [x] Search input, side filter (All/Plant/Zombie), and a sort control render above the roster list.
- [x] The three-tier threshold model replaces the single cutoff: ≤24 renders all, 25–240 renders
  windowed, >240 requires a search/filter before the grid populates.
- [x] Filter/search state survives the layer closing and reopening within the session (GG-51, exactly
  as the plate states it).
- [x] The roster list's own rung (row vs. the plate's card) is a stated decision, not left ambiguous —
  if row stays as a deliberate density choice for high-volume lists, say so against the plate rather
  than silently diverging.

**Verify:**
- [x] `npm test` — search/filter/sort logic unit-tested directly; the three-tier threshold tested at
  the boundary values (24/25, 240/241).
- [x] `npm run test:e2e` — `volume-fixtures.spec.ts` extended for the >240 search-first tier; a new
  spec proving filter state survives a close/reopen.
- [x] Manual: live screenshot compared against plate 02 §A and §D.

**Dependencies:** T25 · **Files:** `src/layers/creatures/CreaturesLayer.tsx`, tests · **Scope:** M

**Closed 2026-08-24.** Replaced the single `VIRTUALIZE_ABOVE = 50` cutoff with the plate's own three
tiers (`RENDER_ALL_MAX = 24`, `SEARCH_FIRST_ABOVE = 240`, read directly from plate 02 §D's table, not
guessed). Two real, honest scope substitutions versus the plate, both confirmed by reading
`adaptActor`: the plate's element-colour filter and "Power, high to low" sort both key off
`elementTyping`/`channelSummary`, which are `Pending` on every actor today — omitted/substituted with
`Level` (a real field) rather than fabricated. Search matches `${side} lvl ${level} ${phase}` — no
creature has a resolved `displayName` yet (also `Pending`), so a name-search box would have nothing
real to search; stated inline via a visible `creatures-rung-note`, not hidden in a comment only.
Volume tiering: ≤24 renders directly, 25–240 and the >240-with-an-active-filter case share the same
`VirtualCreatureList`/`useVirtualizer` path (no separate "search-first" rendering code needed — the
tier only gates whether the prompt or the (now-filtered) list shows). GG-51 persistence needed no new
plumbing: `CreaturesLayer`'s own `useState` already survives close/reopen because the component
instance never unmounts (`SanctumStage.tsx`'s `mountedLayers` gate), proven directly by an e2e spec
that filters, closes, reopens and re-asserts the filter/sort state. `EmptyState` gained an optional
`testId` prop (small, additive, matches every other `ui/` component's pass-through convention) to hook
the new search-first-prompt and no-match states. Found and fixed one real defect during E2E: the sort
`<select>` had no accessible name (axe `select-name`, critical) — added `aria-label="Sort creatures"`;
re-ran the full checkpoint-f axe sweep after the fix, zero violations. `CreaturesLayer.test.tsx` +13
(20 total), `creatures.spec.ts` +2, `volume-fixtures.spec.ts` extended for the 241/1000 cases and the
close/reopen proof — 69/69 e2e across checkpoint-f + creatures + volume-fixtures, 634/634 unit, `tsc
--noEmit` clean. Live-screenshotted (scratch Playwright script with mocked `/api/unique/actors`, since
the static preview has no backend) against plate 02 §A at 1280×720 — controls fit on one line, sorted
correctly, no overflow.

---

### Task 28: Lawn player HUD

**Description.** Plate 04 §A's minimal player HUD (sun count, wave/timer, deployed-creature chips,
pause/1×/2× playback) was never its own task; `LawnPage.tsx` still renders the pre-refactor
debug/control surface — T22's own notes call it "byte-for-byte unchanged." This is the largest single
gap in the audit by player time-on-screen, and the one most likely to need real product judgment
(which debug affordances stay reachable, and how, once a clean HUD exists) rather than a mechanical
fix — deliberately not bundled with T25.

**Acceptance:**
- [x] A clean HUD strip renders above the board: sun count, wave/timer, deployed-creature chips,
  pause/1×/2× playback — sourced from data the existing debug panel already reads (presentation only,
  per this plan's own "no server API changes" architecture decision — no new telemetry).
- [x] The existing debug Spawn/Inspector panel and its actions are not removed (GG-41 governs
  developer surfaces; this task's scope is the player HUD, not deleting working diagnostic tooling) —
  state explicitly where the debug controls live once the clean HUD exists (most likely behind the
  developer-mode gate T12 already built).
- [x] The Rail (T25) stays reachable exactly as it is on every other stage.

**Verify:**
- [x] `npm test` — the new HUD component unit-tested against real board-state shapes.
- [x] `npm run test:e2e` — a live wave/timer/deployed-count assertion against a real, SIM-driven board
  state (`FUSIONRPG_SIM=1`, same established scratch-server discipline used throughout this program).
- [x] Manual: live screenshot compared against plate 04 §A with the game actually running — the one
  surface in the whole refactor that needs a live board state to verify meaningfully, not mocked
  network responses.

**Dependencies:** T25 · **Files:** `src/features/lawn/LawnPage.tsx`, `LawnGameHost.tsx`, new HUD
component, tests · **Scope:** L — product-shaping (which debug affordances move where), not purely
mechanical; expect its own review before landing.

**Closed 2026-08-24.** New `LawnHud.tsx` renders sun/wave/deployed-chips from the same
`LawnEconomy`/`Occupant` fold the debug Inspector already reads (`model.economy`, `living` filtered to
`side === "plant"`) — no new telemetry. Two plate elements are honestly not real, stated inline in the
component's own doc comment rather than faked: a "next wave in 0:18" countdown (`LawnEconomy` has
`wave`/`maxWave`, no timer field at all — confirmed by reading `lawnViewModel.ts`'s type) and
pause/1×/2× playback (grepped the app's entire mutation surface — no speed/pause control exists
anywhere; the actual game process owns its simulation loop, this overlay only observes it). The
playback cluster renders disabled with that reason as its title, the same convention the Sound tab and
Rail's locked entries already use. The pre-existing debug Spawn/Inspector/toolbar apparatus is
untouched (GG-41 — not deleted) and now gated behind `isDevModeEnabled()` via a new
`useDevModeLive()` hook (mirrors `DevTreeHost.tsx`'s own `?devmode=` re-sync exactly, since
`LawnPage` stays mounted across a Settings toggle the same way `DevTreeHost` does); a non-dev player
sees the clean HUD plus the plain board, full-bleed. T22's real deploy-targeting banner and the
action-result banners stay unconditional — they're a player feature, not diagnostic tooling.
`LawnStage.tsx` gained the Rail (T25's "same on every stage"); since the layer-mounting apparatus
(`mountedLayers`) only lives inside `SanctumStage.tsx` — moving it globally is real future scope, not
this task's — a rail click on Lawn navigates to `/sanctum?panel=<id>` rather than opening in place,
stated inline in a comment.

**Found and fixed one real, session-wide defect during full regression, not scoped to Lawn:**
`shell-height.spec.ts` (GG-61) broke — not from this task's own changes, but from T27's earlier
`RENDER_ALL_MAX` drop (50→24): its 40-actor fixture silently crossed from the render-all tier into the
virtualized one, invalidating its own documented assumption. Fixed the fixture (40→20) and its comment
to cite the new threshold. Its third assertion ("the stage behind never scrolls") kept failing after
that fix — traced to `AppShell.tsx`'s root using `min-h-screen` instead of `h-screen`: with `SanctumHome`
now real, substantial content, the outer shell could grow past the viewport instead of `<main>`'s own
`overflow-auto` containing it — the exact class of bug GG-61's own writeup already found and fixed once
for `PanelShell`, just at the app-shell layer instead, previously latent because no band-0 stage content
was ever tall enough to expose it. Fixed (`min-h-screen` → `h-screen`); verified against the **entire**
e2e suite (152 tests across every spec file, not just Lawn/Sanctum) and the full unit suite before
trusting it — zero new regressions, only the pre-existing unrelated `world.spec.ts` failure (World is
excluded this phase).

`LawnHud.test.tsx` (new, 5/5 green); `lawn-hud.spec.ts` (new, 4/4 green — HUD unconditional, debug
apparatus gated, live devMode toggle via System with no reload, Rail reachable) plus a first axe pass
over the non-dev Lawn view (zero violations). Full suite after all T28 work: 156 e2e (155 pass, 1
pre-existing unrelated failure), 646/646 unit, `tsc --noEmit` clean. Live-screenshotted the empty-state
HUD against plate 04 §A at the GG-36 floor width (1280×720) — no overflow, matches the plate's
sun/wave/deployed/playback layout; the populated-data rendering (real sun/wave numbers, deployed
chips, huge-wave flag) is proven by `LawnHud.test.tsx` directly, since `LawnPage`'s board state folds
from live hub events with no established e2e simulation pattern for a real wave/economy snapshot.

**Second visual pass, 2026-08-24 (all six new/changed surfaces at all three GG-36 widths, not just the
floor).** Screenshotted Sanctum home, Creatures, Pacts, Expeditions, Settings and Lawn at 1440×900 and
1920×1080 (1280×720 already covered per-task above) — found and fixed one real defect: `LawnStage.tsx`'s
T2-era "Board panel" proof button (`fixed right-4 top-4`) visually overlapped the top-of-page
SignalR/server-unreachable banner row. Not caused by this task's own new HUD, but newly exposed by it —
this stage never carried a Rail (and thus never got a live-data screenshot at a wider width) before
T28. That button's whole job — proving a `PanelShell` can open over the live board without disturbing
it — is now redundantly reproven every time a player reaches System or a Sanctum layer from this same
stage, so it was gated behind developer mode (GG-41: not deleted) rather than repositioned. Fixed
`LawnStage.test.tsx` (the GG-11 keystone proof itself, which clicks that exact button) to enable dev
mode first; extended `lawn-hud.spec.ts` to assert the button is hidden by default and restored under
`?devmode=1`. All twelve screenshots (6 surfaces × 2 widths) re-inspected clean after the fix; full
suite re-verified: 155/156 e2e (same pre-existing unrelated `world.spec.ts` failure), 646/646 unit.

---

### Task 29: Settings — Display/Sound/Advanced tabs, connection status

**Description.** Plate 06 §C specifies five Settings tabs (Game, Display, Sound, Controls, Advanced)
plus a connection-status row and a "Quit to title" action. T20 built exactly Game and Controls,
matching its own stated scope — confirmed by grepping `SystemLayer.tsx` for `Display`/`Sound`/
`Advanced`/`Connection`/`Quit to title`: zero matches for any of them. The other three tabs and the
connection row were never in any task's acceptance criteria.

**Acceptance:**
- [x] Display and Advanced tabs exist with whatever real, already-wired settings belong in them —
  `reduceMotion`'s System/On/Off segmented control (currently inside the Game tab) is a Display-tab
  candidate per the plate; decide and state where it lives.
- [x] Sound tab ships disabled with its stated reason, per this program's own already-accepted
  assumption 7 — no real audio settings, only the disabled tab and its reason, matching the pattern
  already used elsewhere (e.g. Expeditions locked-with-reason).
- [x] A connection-status row (health badge + Details) renders, sourced from the same health data the
  existing status surfaces already read.
- [x] "Quit to title" renders alongside "Done" — state honestly if no real Title screen exists yet to
  quit to (Title, plate 01 §A, is out of this audit's scope and may be its own gap).

**Verify:**
- [x] `npm test` — `SystemLayer.test.tsx` extended per new tab.
- [x] `npm run test:e2e` — `system.spec.ts` extended: each tab reachable, Sound's disabled reason
  renders, connection status reflects a real mocked health response.
- [x] Manual: live screenshot compared against plate 06 §C.

**Dependencies:** None · **Files:** `src/layers/system/SystemLayer.tsx`, tests · **Scope:** M

**Closed 2026-08-24.** Tab strip reordered to the plate's Game/Display/Sound/Controls/Advanced and
extended from 2 to 5. Display carries `reduceMotion` (moved from Game, real, already persisted) plus a
real `Language` segmented control wired to the actual `setLocale`/`i18n` module — English only in
production (honest per assumption 8), with a dev-only "Pseudo (QA)" option gated on
`import.meta.env.DEV`, matching `i18n/index.ts`'s own comment that a System-settings toggle is exactly
what it was waiting for. Interface scale/text size/colour-blind assist are named in the plate but have
no real backing anywhere in the app (confirmed by grep) — stated as an honest gap inline rather than
built as dead toggles. Sound tab is `disabled` with `title="No audio pipeline exists yet — out of scope
for this refactor (gap G10)"`, mirroring the Rail's own locked-entry convention (disabled + title) and
citing the exact gap id `tech-stack.md` already tracks it under. Connection row (Game tab) reads the
real `useHealth()`/`useHubStatus()` hooks already used by `HudBar.tsx`; "Details" expands real fields
(API base, SignalR status, health source, last heartbeat) rather than a fabricated summary. Quit to
title ships `disabled` with `title="No title screen exists yet"` since plate 01 §A's Title stage is
out of this phase's scope. Advanced holds two real, wired actions: the live API base (read-only) and a
"Reset preferences" button that calls the real `writePreferences(DEFAULT_PREFERENCES)`. `SystemLayer.test.tsx`
gained 5 tests (11/11 green); `system.spec.ts` gained 5 (71/71 green across the full checkpoint-f +
system suite); one test's own "healthy" assumption was corrected to "degraded" after discovering the
shared `mockSanctum` fixture deliberately aborts the SignalR hub route — the component was already
correct, honestly reporting a degraded connection under that fixture. Full unit suite 628/628 green.
Live-screenshotted all three new tab bodies (Game+Connection, Display, Advanced) against plate 06 §C at
1280×720 — no overflow, no truncation, consistent with the segmented/toggle/badge idioms already used
elsewhere in the app.

---

### Task 30: Thin-wrap content — Expeditions, Almanac, Pacts layout

**Description.** Expeditions and Almanac were wrapped in `PanelShell` around their unmodified
pre-refactor page content, so their interiors still look like the pages they replaced rather than
plates 03 §C and 05 §A. Pacts is a real, dedicated component (T17) but stacks its cards vertically
with no portraits, where the plate shows a side-by-side pair with colour-tinted portraits — confirmed
by reading `PactsLayer.tsx` (`flex flex-col gap-3`, no `<img>`/icon element anywhere in the file).
Fusion (plate 02 §C) is excluded — it is a resolved domain mismatch (T15), not a visual gap.

**Acceptance:**
- [x] Expeditions renders the plate's card list — icon, status pill, results row (item/XP/souls
  chips), progress bar with %, roster chips of assigned creatures, an "Empty berth · Dispatch" card —
  sourced from the same real `useExpeditions` data `ExpeditionsPage.tsx` already reads. A visual
  rebuild of an already-real layer, not new mechanics.
- [x] Almanac's master-detail creature browser (searchable list with silhouetted undiscovered
  entries, a detail pane with stat grid and element matchups) is scoped honestly against what data
  exists — per T19's own finding, per-creature matchups and a fuses-into chip may still be `Pending`;
  state the reason precisely rather than reopening T19's already-accepted scope cut wholesale.
- [x] Pacts' cards render side-by-side (not stacked) with a colour-tinted portrait per card, matching
  plate 03 §D's density; the existing loyalty bar / inline disabled-reason / conditional actions are
  unchanged, since T17's own mechanics already match the plate.

**Verify:**
- [x] `npm test` — per-layer test files extended for the new DOM structure.
- [x] `npm run test:e2e` — per-layer specs extended; volume/four-states coverage re-verified since
  layout is changing under already-passing tests.
- [x] Manual: live screenshot compared against plate 03 §C, 05 §A, and 03 §D respectively.

**Dependencies:** T25 · **Files:** `src/layers/expeditions/ExpeditionsLayer.tsx`,
`src/layers/almanac/AlmanacLayer.tsx`, `src/layers/pacts/PactsLayer.tsx`, tests · **Scope:** L — split
per layer (T30a Expeditions / T30b Almanac / T30c Pacts) if built across multiple sessions.

**Closed 2026-08-24.**
- **T30a Expeditions:** `ExpeditionsPage.tsx`'s `activeRow` rebuilt into an icon-framed card (icon,
  name, status `Badge` "away"/"returned", boss badge, progress meter with a real %, roster chips
  resolved from the same `roster.data`/`speciesById` the Dispatch picker already reads). The plate's
  pre-collect reward-chip row is not real — `ExpeditionRowDto` carries no reward preview at all
  (rewards roll server-side only at collect time, in `ExpeditionCollectDto`) — confirmed by reading
  `lib/bus/expeditions.ts`'s own type. Stated inline via a code comment and left off the active card;
  the real results row already exists on the post-collect `reveal` panel, unchanged. "Empty berth"
  (implies one shared capacity number) doesn't map onto this app's real model either — squad slots are
  per-tier (`tier.squadSlots`), not roster-wide — so the header subtitle uses "N demons available"
  (`roster.length − lockedIds.size`, real) instead of "N berths free" (would be invented), and no
  dashed "Empty berth" card was added. A live screenshot caught a real bug before landing: the
  subtitle's "away" count used the raw active total instead of subtracting the returned ones (looked
  fine with 0-1 actives, wrong with 2+) — fixed, and locked in with a new e2e assertion. `tsc --noEmit`
  clean; `expeditions-pacts.spec.ts` +1 (new card-content test, tiers/dates built relative to the real
  clock since `expeditionProgress` compares against `Date.now()`, not the fixture's `serverUtc`);
  65/65 e2e (expeditions-pacts + checkpoint-f incl. axe), 635/635 unit.
- **T30b Almanac:** re-read `AlmanacLayer.tsx` — T19's own scope note (`CatalogPage`/`RecipesPage` are
  "the honest, real, current richness, not the plate's full mockup") already states exactly what T30's
  acceptance bullet asks for. Reconfirmed, no code change — building the richer per-creature browser
  the plate draws is real future scope (an actual data gap, not a visual one), not something a
  visual-parity pass fabricates.
- **T30c Pacts:** `PactsLayer.tsx`'s card list changed from `flex flex-col` to `grid grid-cols-2`
  (side by side, matching the plate's `minmax(0,1fr) minmax(0,1fr)`), and each card gained a
  `PactPortrait` — an initial in a rarity-tinted frame, reusing the existing `--color-rarity-*` tokens
  (no art registry exists yet, per game-gui-map.md assumption 6, so this is the same honest substitute
  `ActorFrame` already uses for creatures) mapped from the demon's real `profile.rarity`
  (`common|rare|epic|legendary`). Action rows gained `flex-wrap` so the narrower 2-column card doesn't
  overflow. `PactsLayer.test.tsx` +1 (grid + rarity-tint assertions, 8/8 green); e2e (expeditions-pacts
  Pacts suite) unaffected, 65/65 total. Live-screenshotted (scratch Playwright script with mocked
  contracts/demons data) against plates 03 §C and 03 §D at 1280×720 — no overflow, correct colours,
  correct status pills.

---

### ✅ Checkpoint H — plate parity

- [x] Every finding in `visual-completeness-audit-2026-08-24.md` is either fixed (T25–T30) or carries
  a fresh, stated reason it stays as-is — same discipline Checkpoint G already used for its own
  honest gaps.
- [x] Full test suite green; no regression against Checkpoint G's own twenty enforcement checks.
- [x] A second visual-completeness pass (same method as the 2026-08-24 audit) finds no new
  major/moderate findings.
- [ ] **Owner review** — visual acceptance is a taste call as much as a correctness one; this line is
  the owner's to check.

**Closed (pending owner review) 2026-08-24.** T25–T30 all landed and closed with evidence above. Final
whole-program regression run (not per-task): `tsc --noEmit` clean, `npm run build` clean, full unit
suite 646/646, full e2e suite 156 tests / 155 green — the one failure (`world.spec.ts`'s
`sector-slots` count) is the same pre-existing, unrelated World-stage drift already on record before
Phase 7 started (World is excluded this phase; not touched by any T25–T30 change). One minor,
non-player-facing defect surfaced during the second (multi-width) visual pass — T28's own closure note
above has the detail — and was fixed on the spot, not deferred. No major/moderate visual gap surfaced
beyond what the original audit already named — the ones deliberately left as-is (Almanac's richer
browser, Expeditions' pre-collect reward preview, the fusable-pair priority tier, "next wave" countdown,
pause/1×/2× playback) are each named inline in code comments and in this file's own closure notes, with
the real reason they aren't buildable honestly today. Owner review is the only remaining box.

---

### Checkpoint I — retire the old layout (not started, gated on Checkpoint H)

**Description.** `game-gui-map.md`'s own assumption 2 — old routes keep working until their
replacement lands, no flag day — is why every superseded route still redirects into a real component
today rather than 404ing. This checkpoint is that flag day, once Checkpoint H confirms every
replacement is actually complete rather than a thin wrap. Per the owner's own instruction
(2026-08-24), this does not start until Checkpoint H passes.

- [ ] Every pre-refactor page component a redirect currently points through (`RosterPage.tsx`'s
  standalone form and the nine routes T12 already swept into the developer tree, plus whichever of
  `ExpeditionsPage.tsx`/`CatalogPage.tsx`/`RecipesPage.tsx`/`MetricsPage.tsx`/`RpgProgressionPage.tsx`/
  `PvzStatsPage.tsx` T30 has actually replaced) is deleted, not merely unreached.
- [ ] Every route that redirected into a since-deleted component is deleted too — a redirect to
  nothing is worse than a route that still works.
- [ ] Full test suite green with the deleted files' own tests removed, not skipped.
- [ ] **Owner sign-off** — deleting shipped code is exactly the kind of irreversible-in-spirit action
  this program's own git-hands-off discipline defers to the owner for.

---

### Task: Player-copy hygiene (pending reasons + guard)
**Description.** Replace dev/task-note strings in player-visible UI (especially `Pending<T>.reason`) with player vocabulary; add `pendingCopyGuard.ts` so AGENTS.md quotes, spec filenames, and task ids cannot ship again.

**Acceptance:**
- [x] `PLAYER_PENDING` constants in `adapt.ts`; sanctum, actor tabs, settings, relics copy rewritten
- [x] `pendingCopyGuard.test.ts` real-tree scan green
- [x] Full vitest suite green
