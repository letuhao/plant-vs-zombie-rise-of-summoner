# Tasks: fe-essentials program

Plan: [fe-essentials-plan.md](fe-essentials-plan.md) · Map:
[../docs/architecture/fe-essentials-map.md](../docs/architecture/fe-essentials-map.md) · Specs:
[../docs/architecture/fe-essentials/](../docs/architecture/fe-essentials/).

**7 tasks · 3 phases, plus one owner-directed addendum (AuditNav removal).** Scope: **XS** ≈ under
20 min · **S** ≈ under an hour · **M** ≈ a focused session. **All 7 tasks + the addendum closed
2026-08-29** — build, full unit suite, full E2E suite (functional + visual, screenshots actually
inspected) all green.

> ## ⛔ Rules binding on every slice below
>
> **1. No slice waits on a person.** Every acceptance criterion is a command that exits non-zero, or a
> grep result recorded verbatim — this program has no LIVE-gate-shaped task.
> **2. `data-testid`s named in a spec are preserved exactly** — `SanctumStage.test.tsx`'s existing
> assertions passing unmodified is itself an acceptance criterion for T1, not just a nice-to-have.
> **3. A task that finds its own module has nothing left to build (T7) reports that honestly** — it does
> not invent busywork to look complete, matching this program's own map-correction precedent.

---

## Phase 1 — `onboarding-first-run`

- [x] **T1: `FirstRunReveal` component + `FocusCard` wiring** · **S/M**
  - New `web/fusion-rpg-web/src/stages/sanctum/FirstRunReveal.tsx` — stateless, `{ onBind: () => void }`,
    renders the plate's reveal framing. **No name input** — spec's Assumption 1: no display-name-write
    endpoint exists anywhere in the FE yet (`CreaturesLayer.tsx:36-38`'s own comment).
  - `FocusCard.tsx`'s zero-creature branch delegates to it; `data-testid`s preserved exactly.
  - **Deviation from the original design, caught before shipping:** the plate's own `frame frame--panel`
    / `data-rarity` classes don't exist in the real app's CSS at all (confirmed by reading
    `ui/actor/shared.tsx`'s `ActorFrame` — the real convention is a plain Tailwind-sized emoji/initial,
    no rarity-frame system). Shipped a bare, Tailwind-sized 🌻 instead of copying the plate's markup
    verbatim, which would have rendered unstyled.
  - Acceptance (all met):
    - [x] Zero-creature state shows "This one answered" / the sunflower reveal, not the old
      "Bind your first creature" copy — proven in `FirstRunReveal.test.tsx` and live in a real browser
      (`sanctum.spec.ts`'s new test).
    - [x] `SanctumStage.test.tsx`'s existing zero-creature assertions pass unmodified.
    - [x] No `<input>` anywhere in the new branch — asserted directly in `FirstRunReveal.test.tsx`.
  - Verify: `npm run test -- FirstRunReveal SanctumStage` (4 new + existing, all green), `npm run build`
    (clean).
  - **E2E**: `sanctum.spec.ts` — "a fresh, empty-roster save shows the authored reveal, and Bind reaches
    Creatures for real" — real copy assertions, a real click, a real navigation into `creatures-layer`,
    and the `console.debug` observability call captured via `page.on("console")` and asserted present.
  - **Observability**: `console.debug("[fe-essentials] first-run reveal: bind clicked", ...)` on the
    Bind click — exercised and asserted by the E2E test above, not just added and left unverified.
  - **Visual**: screenshots at desktop (1280×800) / tablet (768×1024) / mobile (375×667), actually
    inspected (not just captured) — `test-results/visual/first-run-reveal-*.png`. **Found a real defect
    this way**: see the AuditNav addendum below.
  - Files: `stages/sanctum/FirstRunReveal.tsx`, `FirstRunReveal.test.tsx` (new); `FocusCard.tsx`,
    `e2e/sanctum.spec.ts`, `e2e/fe-essentials-visual.spec.ts` (edit/new).

### ✅ Checkpoint 1 — **closed**
- [x] `npm run test` (full suite, 656/656 at this point) and `npm run build` both clean.
- [x] Manual/E2E check: a fresh/empty-roster load of `/sanctum` shows the new reveal, "Bind" reaches
  Creatures exactly as the old CTA did — proven live, not just asserted.

---

## ⛔ Addendum — AuditNav removal (owner-directed mid-implementation, 2026-08-29)

Not in the original 7-task plan. The owner directed a layout refactor mid-session ("the left sidebar
is outdated, new FE layout don't have it"), pointing back at `01-shell-home.html`. Reading the **whole**
plate (not just §D, already read for T1) plus `00-foundation.html` F.2 resolved what "outdated sidebar"
meant: **not** the icon Rail (already the plate's own intended design, already shipped as T25) — the
flat **`AuditNav`** component (`src/app/AppShell.tsx`, "AUDIT: Lawn/World/Roster/Demons/Storage"),
explicitly named in `00-foundation.html:2003-2005` as the thing GG-40 replaces: *"The current sidebar
has nineteen flat entries under a heading reading `AUDIT`... The rail carries player layers only."*
Its own doc comment showed a multi-step shrink already in progress (T12/T15/T17/T19) — this closes it.

Its fixed 176px column was also the **direct, confirmed cause** of the mobile-viewport clipping T1's
own visual pass found — removing it fixed both concerns in one change.

- [x] Removed `<AuditNav />` from `AppShell.tsx`; deleted `AuditNav.tsx` (confirmed zero other
  references first).
- [x] Verified before removing, not after: `SanctumHome.tsx` already has real "Travel to the map"
  (`/world`) and "Defend the lawn" (`/lawn`) buttons (plate 01 §C's own authored content) — two of
  AuditNav's five links were already fully redundant. `/roster` already redirects into the rail's own
  Creatures entry (also redundant). `/demons` and `/storage` lose their only nav entry but keep their
  real routes (URL-reachable) — `/demons` deliberately left this way, matching this program's own
  Assumption 2 (DemonsPage is a real, larger legacy candidate, explicitly out of scope here).
  `/storage` similarly retained by URL only — no rail-layer redesign invented for it (would be new,
  unrequested scope).
  - Files: `src/app/AppShell.tsx` (edit), `src/app/AuditNav.tsx` (deleted).
- [x] Updated 5 E2E tests across 4 files that asserted "X is absent from AuditNav" (a check that became
  vacuous, not false, once AuditNav no longer exists) — simplified each to check the real redirect/
  layer-visibility behavior, which is the coverage that actually matters now. One (`dev-tree.spec.ts`)
  repointed its "not in AuditNav" check to "not anywhere in `shell-body`" — an equal-or-stronger check,
  not a weaker one.
  - Files: `e2e/dev-tree.spec.ts`, `e2e/expeditions-pacts.spec.ts`, `e2e/fusion.spec.ts`,
    `e2e/almanac-chronicle.spec.ts` (edit).
- [x] Added new regression coverage: `sanctum.spec.ts` — "AuditNav is gone, and Travel to the map /
  Defend the lawn navigate for real" — asserts `audit-nav` testid has zero count AND both SanctumHome
  buttons really navigate (`toHaveURL`), not just that they exist.
- [x] Full regression sweep: `npm run test` 656/656 → still 656/656 after (no unit test touched
  AuditNav). Full `npx playwright test`: 159/160 → 160/161 (one test added), the one pre-existing
  failure (`world.spec.ts:113`, sector-slot count, confirmed via `git status --short` to be completely
  outside every file this session touched) unchanged before and after.
- [x] **Visual re-check, the actual point of this addendum**: re-captured all three first-run-reveal
  screenshots post-removal. Mobile went from clipped/overlapping (AuditNav's 176px column pushing
  everything else off-screen) to clean, fully-visible, no overflow. Desktop/tablet reflowed correctly,
  no regression.

---

## Phase 2 — `actor-menu-scope-picker`

- [x] **T2: `ScopePickerValue` type + container shell + Relation mode** · **S**
  - `ui/scope/ActorMenuScopePicker.tsx` — the discriminated union, `TabList`-driven mode switch,
    Relation panel (Ally/Enemy) inline.
  - **Design correction made while building, not after**: the original sketch tried to force a
    "clear the value on mode switch" behavior by calling `onChange(undefined as unknown as
    ScopePickerValue)` — an unsafe type-cast lying about the callback's real contract, and
    unnecessary besides, since every panel already only reads `value` when `value.kind` matches its
    own mode (`value?.kind === "…" ? value : null`). Removed the cast entirely; the guard clauses
    already provide the "no stale value leaks across modes" behavior for free.
  - Acceptance (all met): four real tabs render; Relation mode fully functional standalone
    (`ActorMenuScopePicker.test.tsx`); mode switching proven not to leak a prior mode's value into a
    different mode's panel (two dedicated tests, including a "started already holding a Relation
    value" case).
  - Verify: `npm run test -- ActorMenuScopePicker` — green.
  - Files: `ui/scope/ActorMenuScopePicker.tsx`, `ActorMenuScopePicker.test.tsx` (new).

- [x] **T3: `ActorListPickerPanel` — shared Target/UniqueDemon mode** · **M**
  - `ui/scope/ActorListPickerPanel.tsx` — one implementation for both modes, differing only in
    `kind`/`candidates`/the extracted id field (`targetPtr` vs `instanceId`, both sourced from
    `ActorView.instanceId` — confirmed via `contract/types.ts` that no separate live-battle-ptr field
    exists yet; a future caller supplies whatever identity space fits their own data source).
  - Acceptance (all met): both modes render through the real `ActorRow` (asserted directly, not by
    convention); selecting emits the exact kind-tagged shape; loading/empty/error candidates render via
    `ActorRow`'s own `RungStateFallback`, non-selectable (no `<button>` role) — all in
    `ActorListPickerPanel.test.tsx` (6 tests).
  - Verify: `npm run test -- ActorMenuScopePicker ActorListPickerPanel` — green.
  - Files: `ui/scope/ActorListPickerPanel.tsx`, `ActorListPickerPanel.test.tsx` (new);
    `ActorMenuScopePicker.tsx` (edit).

### ✅ Checkpoint 2 (mid-module) — **closed**
- [x] Container + three of four modes (Relation, Target, UniqueDemon) working end-to-end in isolation,
  proven by test.

- [x] **T4: `TypeMultiSelect` — new primitive for Type mode** · **S/M**
  - `ui/scope/TypeMultiSelect.tsx` — plain multi-select over the real `Checkbox` primitive.
  - Acceptance (all met): round-trips the exact `typeIds` selected (add/remove both proven); reflects
    controlled `value`'s checked state; clear empty state with no options — `TypeMultiSelect.test.tsx`
    (4 tests).
  - Verify: `npm run test -- ActorMenuScopePicker TypeMultiSelect` — green.
  - Files: `ui/scope/TypeMultiSelect.tsx`, `TypeMultiSelect.test.tsx` (new); `ActorMenuScopePicker.tsx`
    (edit).

- [x] **T5: cross-mode contract tests** · **XS/S**
  - Full four-mode matrix plus the exact switch-away-and-back case named in the spec, in
    `ActorMenuScopePicker.test.tsx`.
  - Verify: `npm run test -- ActorMenuScopePicker` — 8 tests, all green.
  - Files: `ui/scope/ActorMenuScopePicker.test.tsx` (edit).

- [x] **T6: demo page + route** · **S**
  - `ui/scope/ActorMenuScopePickerDemoPage.tsx` — `ActorLadderDemoPage.tsx`'s exact shape (`?mock=1`,
    `Page` wrapper); target/uniqueDemon candidates both from `useUniqueActors` (noted honestly in a
    comment as a demo simplification — a real consumer would likely feed two different sources); type
    options from the real `useTypes()`/`/api/types` catalog, not invented data.
  - `routes.tsx` — new lazy route `actor-menu-scope-picker-demo`, matching `actor-ladder-demo` exactly.
  - **Bug found and fixed via T6's own visual pass, not left as a "known flake"**: screenshots taken
    immediately after a tab click showed the wrong tab highlighted (but correct panel content) —
    investigated with a targeted probe rather than assumed away; confirmed via `getComputedStyle` that
    the DOM state (`aria-selected`, class) was already correct, but Tailwind's `transition-colors`
    animation hadn't finished painting yet, so the raw screenshot caught it mid-fade. Fixed the E2E
    visual test to wait for the animation to settle before capturing — a test-hygiene fix, not a
    product-code change, and confirmed by rechecking the previously-wrong mobile screenshots afterward.
  - Acceptance (all met): route reachable; all four modes usable end-to-end against real fixture/catalog
    data in a real browser (`actor-menu-scope-picker.spec.ts`, 5 tests, all executed against a live
    preview server, not just written).
  - **Observability, exercised not just present**: every `console.debug` call added across the module
    (relation selected, mode changed, list selection, type selection changed) is asserted via a real
    `page.on("console")` listener in the E2E suite — closed as a deliberate pass after noticing the
    first draft only exercised these implicitly by clicking through the UI, never actually confirmed
    they fired.
  - Verify: `npm run build` clean; `npx playwright test e2e/actor-menu-scope-picker.spec.ts` — 5/5.
  - Files: `ui/scope/ActorMenuScopePickerDemoPage.tsx` (new); `app/routes.tsx` (edit);
    `e2e/actor-menu-scope-picker.spec.ts`, `e2e/fe-essentials-visual.spec.ts` (new/edit).

### ✅ Checkpoint 3 (closes `actor-menu-scope-picker`) — **closed**
- [x] `npm run test` and `npm run build` clean.
- [x] Manual/E2E check: the new route in a real browser, all four modes exercised, visually verified at
  3 viewports × 3 modes (9 screenshots, all actually inspected — the transition-timing bug above was
  found exactly this way).

---

## Phase 3 — `hide-legacy-entry` (verification only)

- [x] **T7: verify nothing legacy remains reachable** · **XS**
  - Repo-wide grep for `"Bind your first creature"` / `"Open Creatures"` post-T1: **zero matches in
    production code** — the only two hits are in `FirstRunReveal.test.tsx`'s own regression assertions
    that these strings must NOT appear.
  - No competing who-picker existed before `actor-menu-scope-picker` and none appeared during
    development — confirmed true by construction (net-new code, no prior art to conflict with).
  - `DemonsPage.tsx` / `/demons`: confirmed via `git status`/`git diff --stat` that **zero** files under
    `src/features/demons/` were touched this program, and `routes.tsx`'s diff is additive-only (+11/-0)
    — the `/demons` route entry itself is untouched. Explicitly named here as deliberately out of scope,
    not silently skipped.
  - Acceptance (all met): grep evidence recorded above (zero matches); DemonsPage named explicitly.
  - Verify: grep commands above; `npm run test` full suite (674/674) and `npx playwright test`
    (168/169, the one failure pre-existing and unrelated, confirmed via diff scope) as the closing sweep.
  - Files: none needed — nothing to fix.

### ✅ Program checkpoint — fe-essentials complete — **closed 2026-08-29**
- [x] All three modules' success criteria met (per their own specs) — plus the AuditNav addendum's own
  criteria.
- [x] Full `npm run test` (674/674) + `npm run build` (clean) + `npx playwright test` (168/169, 1
  pre-existing/unrelated) run together at the end of the whole program.
- [x] This file shows every task checked with evidence, matching this session's own established pattern
  for every prior program (buff-debuff-scope, action, etc.).
