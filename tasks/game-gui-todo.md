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
