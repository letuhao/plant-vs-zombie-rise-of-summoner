# Plan: actor-sheet program

Source: [actor-sheet-map.md](../docs/architecture/actor-sheet-map.md) (5 modules, approved) and its
five specs under [actor-sheet/](../docs/architecture/actor-sheet/), all grounded against current code
2026-08-29 — not just the draft plate (see each spec's own Assumptions section for what grounding
corrected before this plan was drafted).

Task list: [actor-sheet-todo.md](actor-sheet-todo.md). Paths are prefixed per this repo's
parallel-programs convention — `tasks/plan.md`/`tasks/todo.md` belong to the perf stream.

---

## 1. Shape of the work

**5 modules, 5 tasks, 2 checkpoints.** Every task is one complete build-and-verify path, not a
horizontal layer.

```text
T1  actor-sheet-shell     (foundational — every other tab mounts into this)
    |
T2  gear-tab              \
T3  locked-preview-tabs    |  independent of each other and of T4/T5,
T4  derived-stats-tab      |  sequenced smallest-first below
T5  progression-tab       /
```

### 1.1 Why this order

`actor-sheet-shell` (T1) goes first because nothing else has anywhere to mount without it, and because
it's the smallest real lift — Overview's content relocates unchanged, it isn't rebuilt. T2-T5 are
genuinely independent (each fills exactly one of the empty tab slots T1 leaves behind, touching no
other tab's content) and are sequenced smallest-first (`gear-tab` is a two-file empty-state;
`progression-tab` is the largest, reusing an existing page's full allocation logic) so a real
regression, if one turns up, surfaces early rather than late.

### 1.2 What's explicitly not in this plan

Building real resource meters or a "Standing" power-vector for Overview (never built as React code,
`ActorView` has no HP field to bind one to regardless). The full derived-stat sheet behind
`derived-stats-tab`'s doorway (that's `spec-derived-stat-sheet.md`'s own scope). A real action or
passive-skill *system* (both tabs ship fully locked, previewing nothing that resolves). The other three
aptitude-allocation scopes (demon-type/aspect/unique-demon — commander scope only, matching what's
already built). Promote, at the owner's own instruction. All restated from the map's own exclusion
list so the boundary travels with the plan.

---

## 2. Architecture decisions

- **The tab bar is `TabList` + a `kind`-discriminated render** — the exact shape already proven in
  `ui/scope/ActorMenuScopePicker.tsx` this same session, not a new switching pattern.
- **Locked content reuses `Rail.tsx`'s own real locked-state convention** (disabled, dimmed, a `title`
  naming the reason) — not the design plate's `.actionslot`/`rail button.is-locked` CSS, which has no
  React equivalent anywhere in the tree (confirmed by grep).
- **`progression-tab` calls the exact same hooks `AptitudesPage.tsx` already calls**
  (`useAptitudes`/`useSaveAptitudes`) and the same `NumberInput` control — never a second allocation
  implementation with its own edge-case bugs.
- **Every "not real yet" field (`channelSummary`, `equipSlots`) renders via the existing `PendingNote`
  pattern** — the same discipline `ActorPanel.tsx` already applies today, just relocated under the
  right tabs, never replaced with a fabricated table.
- **No dead links.** `derived-stats-tab`'s "Open full sheet" button ships disabled with a real reason —
  confirmed by grep that the full sheet's own component doesn't exist in the tree yet.

## 3. Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `ActorPanel.tsx` growing a `switch`-shaped render across 5 sequential edits (T1 then T2-T5 each touching the same file) could produce merge-shaped friction if tasks aren't done one at a time | Low | This session's own established discipline already builds one task at a time, never two in parallel — the file is only ever edited by one task at a time in practice |
| `progression-tab`'s aptitude logic duplicating `AptitudesPage.tsx` verbatim (per the spec's own note) could drift out of sync if one is changed later without the other | Medium (named in the spec, not hidden) | The spec itself flags extracting a shared `useAptitudeAllocation()` hook as a fast-follow if duplication grows past ~20 lines — not mandated up front, but not silently ignored either |
| No E2E coverage is scoped in this plan (unit-only tasks) | Medium | Matches this program's own spec-writing pace — E2E gets added during `/goal` execution, same as every other program this session, not deferred indefinitely |

## 4. Open questions

- Whether the standalone "Primary Stats" rail entry retires once `progression-tab` ships, or stays as
  a shortcut — owner call, doesn't block any task above.
- Whether `locked-preview-tabs` should preview every designed-but-unbuilt action, or only the ones a
  given specimen's kind could ever hold — owner call, doesn't block building the locked-grid mechanism
  itself (T3's own placeholder list is illustrative either way).
- `Release`/`Deploy` closing the panel (T1) is the named minimal fix — a real mutation for either stays
  out of scope until a spec names what they should actually do.
