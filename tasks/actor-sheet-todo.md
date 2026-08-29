# Tasks: actor-sheet program

Plan: [actor-sheet-plan.md](actor-sheet-plan.md) · Map:
[../docs/architecture/actor-sheet-map.md](../docs/architecture/actor-sheet-map.md) · Specs:
[../docs/architecture/actor-sheet/](../docs/architecture/actor-sheet/).

**5 tasks · 2 checkpoints.** Scope: **XS/S** ≈ under an hour · **M** ≈ a focused session.

> ## ⛔ Rules binding on every slice below
>
> **1. `ActorPanel.tsx` is edited by exactly one task at a time.** T1 first and alone; T2-T5 may build
> in any order relative to each other but never two at once — same file, same tab-switch, avoid
> merge-shaped friction.
> **2. No fabricated data, ever.** Every "not real yet" field renders its honest pending/empty/locked
> state — never a table, bar, or grid showing numbers that don't come from a real response.
> **3. No dead links.** Any button pointing at unbuilt destination (the full derived-stat sheet, a
> real action/passive system) ships disabled with the real reason, never wired to nothing.

---

## T1 — `actor-sheet-shell`

- [x] **T1: six-tab container, Overview relocated, footer buttons wired** · **M** · **Done 2026-08-29**
  - `ActorPanel.tsx` gains `useState<ActorSheetTab>("overview")`, a `TabList` between the identity
    header and the body, and a `kind`-discriminated render per tab. Today's real Overview content
    (Standing + Element-typing pending-notes) moves under the `"overview"` case unchanged; Equipment's
    pending-note moves out (becomes T2's own content — until T2 lands, that slot renders nothing
    extra, not a duplicate). The other four tabs render an empty container until their own task lands.
  - `Release`/`Deploy` footer buttons gain `onClick={() => onOpenChange(false)}`.
  - **TDD**: wrote `ActorPanel.test.tsx` first, confirmed RED (4/5 failing — six-tab, tab-switch, and
    both footer-button assertions all failed against the pre-change component; the fifth, the
    non-ready-state short-circuit, passed immediately since that behavior was already real). One real
    test-fixture bug caught and fixed before GREEN: the fixture used `absent()` for
    `channelSummary`/`elementTyping`/etc., but `PendingNote` only renders for `state: "pending"` — the
    real `adaptActor` uses `pendingWithReason(...)`, not `absent()`, for these fields. Fixed the
    fixture to match reality, then GREEN (5/5).
  - Acceptance (all met):
    - [x] Six real tabs render; clicking each shows only that tab's own content.
    - [x] Overview's Standing/Element-typing pending-notes render exactly as `main` renders them today.
    - [x] Non-ready states (loading/empty/error/locked) still short-circuit to `RungStateFallback`
      before the tab bar ever renders.
    - [x] `Release`/`Deploy` each close the panel — proven by test.
  - Verify: `npm run test -- ActorPanel` (5/5), full `npm run test` (679/679, up from 674 — 5 new),
    `npm run build` (clean).
  - **Live verification, not just unit tests**: opened the real Panel rung via
    `/actor-ladder-demo?mock=1`'s existing "Open panel" button (no bound creature exists in either
    live save today, so this reused the app's own established fixture-demo path rather than fabricate
    a workaround). Confirmed live: six tabs render cleanly with no overflow, `role="tab"` semantics
    present, switching to a not-yet-built tab (Gear) shows an empty body without breaking layout, and
    clicking Release actually closes the panel.
  - Files: `ui/actor/ActorPanel.tsx` (edit), `ActorPanel.test.tsx` (new).

### ✅ Checkpoint 1 — **closed**
- [x] `npm run test` (full suite, 679/679) and `npm run build` clean.
- [x] Manual/live check (above) — six-tab bar renders correctly, Overview unchanged, the other five
  tabs empty-but-present, nothing broken.

---

## T2-T5 — independent tabs (any order after T1; smallest-first here) — **all closed 2026-08-29**

Built via `/build auto` — one continuous TDD pass, RED confirmed before each implementation, one task
at a time against `ActorPanel.tsx` per this file's own Rule 1.

- [x] **T2: `gear-tab`** · **XS/S** · **Done**
  - `GearTab.tsx` — `equipSlots` pending → `EmptyState` (`title`/`hint`/`testId`, matching
    `AptitudesPage.tsx`'s own usage); non-pending → a visible "not rendered yet" fallback. Wired into
    `ActorPanel.tsx`'s `"gear"` case.
  - **Bug caught before GREEN**: the first draft's non-pending fallback called `PendingNote` with a
    non-pending value — `PendingNote` returns `null` unless `state === "pending"`, so that branch
    would have silently rendered nothing (looking like a bug, not a placeholder). Fixed to a visible,
    honest fallback message instead.
  - Acceptance (all met):
    - [x] Today's real (pending) state renders the honest `EmptyState`, naming
      `spec-equip-and-paperdoll.md` as the eventual doorway.
    - [x] Overview's Equipment pending-note is gone (moved, not duplicated).
  - Verify: `npm run test -- GearTab ActorPanel` — 8/8 green.
  - Files: `ui/actor/GearTab.tsx`, `GearTab.test.tsx` (new); `ActorPanel.tsx` (edit).

- [x] **T3: `locked-preview-tabs`** · **S** · **Done**
  - `LockedGridSlot.tsx` (shared) + `ActionsTab.tsx` + `PassivesTab.tsx` — every slot locked
    (including "Strike," unlike the draft plate's own mockup — there is no working basic-action call
    to wire it to today, so it ships locked like the rest), styled off `Rail.tsx`'s real locked-button
    classes (`cursor-not-allowed border-transparent text-faint opacity-60`, verified by reading
    `Rail.tsx` directly before writing this). Wired into `ActorPanel.tsx`'s `"actions"`/`"passives"`
    cases.
  - Acceptance (all met):
    - [x] Every slot in both grids is non-interactive (plain `<div>`, not a disabled `<button>` —
      deliberately not tab-stoppable for something that can never act).
    - [x] Every slot's `title` names its real, correct reason.
    - [x] Locked styling matches `Rail.tsx`'s own classes exactly.
  - Verify: `npm run test -- LockedGridSlot ActionsTab PassivesTab ActorPanel` — 15/15 green.
  - Files: `ui/actor/LockedGridSlot.tsx`, `ActionsTab.tsx`, `PassivesTab.tsx` + their `.test.tsx`
    (new); `ActorPanel.tsx` (edit).

- [x] **T4: `derived-stats-tab`** · **S** · **Done**
  - `StatSummaryGrid.tsx` (new key-value grid, plain Tailwind — `.statgrid` confirmed plate-CSS-only,
    no React equivalent existed) + `DerivedStatsTab.tsx` — `channelSummary` pending → `PendingNote`
    (today's real, only reachable state); known → the summary grid, capped at four channels; "Open
    full sheet" always disabled with the exact "not built yet" reason (confirmed by grep that
    `spec-derived-stat-sheet.md`'s own component doesn't exist in the tree). Wired into
    `ActorPanel.tsx`'s `"derived-stats"` case.
  - Acceptance (all met):
    - [x] Pending state renders the exact real reason string.
    - [x] A mocked known `channelSummary` renders the summary grid correctly, capped at 4.
    - [x] The doorway button never links to a route — disabled-with-reason only.
  - Verify: `npm run test -- StatSummaryGrid DerivedStatsTab ActorPanel` — 20/20 green.
  - Files: `ui/actor/StatSummaryGrid.tsx`, `DerivedStatsTab.tsx` + their `.test.tsx` (new);
    `ActorPanel.tsx` (edit).

- [x] **T5: `progression-tab`** · **M** · **Done**
  - `ProgressionTab.tsx` — real `level` + raw `xp` count always shown; `xpToNext` known → real
    `StatBar`; pending (today's real state) → `PendingNote`. Aptitude grid: `AptitudesPage.tsx`'s own
    `useAptitudes`/`useSaveAptitudes`/draft-dirty-budget logic, ported verbatim (same mock pattern as
    `AptitudesPage.test.tsx`, reused directly). Wired into `ActorPanel.tsx`'s `"progression"` case.
  - Acceptance (all met):
    - [x] Level/XP render correctly in both the pending and known `xpToNext` states.
    - [x] Aptitude ids come straight from the mocked server response, never a hardcoded list.
    - [x] Save/budget/no-clamp-over-budget (PS-8) behavior matches `AptitudesPage.tsx` exactly.
  - Verify: `npm run test -- ProgressionTab ActorPanel` — 6/6 green, first try (no fixture bugs this
    time — porting `AptitudesPage.test.tsx`'s own proven mock shape directly paid off).
  - Files: `ui/actor/ProgressionTab.tsx`, `ProgressionTab.test.tsx` (new); `ActorPanel.tsx` (edit).

### ✅ Checkpoint 2 (closes the program) — **closed 2026-08-29**
- [x] `npm run test` (full suite, 700/700, up from 674 before this program — 26 new) and `npm run
  build` clean.
- [x] Full `npx playwright test` (whole app, not just this program): 184/185, the one failure the same
  pre-existing `world.spec.ts` sector-slot-count issue confirmed unrelated earlier this session (its
  own file, untouched by any change here).
- [x] **Manual/live check, all six tabs**: opened the real Panel via `/actor-ladder-demo?mock=1`,
  clicked through every tab. Overview (Standing/Element/Shield pending, real identity). Progression
  (real Level 1 / 0 xp / honest "XP curve endpoint isn't wired" note, real 12-aptitude grid fetched
  live from `/api/aptitudes/1`, correct 0/3 spent (Θ=1) budget readout). Derived Stats (honest pending
  reason + disabled doorway button with its real title). Actions and Passives (4 locked slots each,
  lock icon, dimmed, correct reasons). Gear (honest `EmptyState`). Release closes the panel.
- [x] **Visual check at desktop + mobile, actually inspected**: desktop screenshots taken per-tab
  during the live check above (no overflow anywhere); a dedicated mobile (375×700) screenshot of
  Progression (the densest tab) shows the 6-tab bar wrapping cleanly to two rows and the 12-input
  aptitude grid correctly reflowing to 2 columns (`grid-cols-2 sm:grid-cols-3`) — no clipping, no
  overlap.
