# Spec: `derived-stats-tab`

**Module id:** `derived-stats-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** `actor-sheet-shell` · **Blocks:** nothing

---

## Assumptions

1. **`channelSummary` is unconditionally pending for every actor today — there is no real data this
   tab can show yet.** Same confirmed fact as `actor-sheet-shell`'s Standing/Element/Shield sections:
   `adaptActor` hardcodes `pendingWithReason("The derived-stat snapshot has no server endpoint yet")`
   regardless of input. This tab therefore ships as an honest `PendingNote`, matching the rest of this
   panel's own established convention — not a fabricated grid.
2. **The full derived-stat sheet (`spec-derived-stat-sheet.md`) has no React component or route yet —
   confirmed by grep, not assumed.** No "Open full sheet" button gets wired to a real destination in
   this pass; it would be a dead link. This module ships the doorway's *shape* (a disabled button with
   an honest reason, matching the Quit-button-on-Title-screen precedent from `fe-essentials`) so the
   slot exists, without pretending the destination exists.
3. **`statgrid` is a plate-only CSS class** (`_kit/kit.css`, used in `00-foundation.html`'s mockup) —
   there is no existing React equivalent. This module writes a small, new key-value grid component,
   not a reuse of something already built.

## Objective

A tab that becomes real the moment `channelSummary` gets a server endpoint, without shipping a fake
grid in the meantime — and a visible (if disabled) doorway to the already-specified full sheet, so the
slot exists for whoever builds that endpoint next.

**Users:** a player checking a specimen's derived combat stats without needing the standalone,
not-yet-built full sheet.

**Success is measurable:** when `channelSummary` is pending (today, always), the tab shows the honest
reason, not a fabricated table; when it becomes known (future), a small grid renders the headline
channels; the "Open full sheet" affordance never links to a route that doesn't exist.

## Design

```tsx
function DerivedStatsTab({ data }: { data: ActorView }) {
  return (
    <div data-testid="derived-stats-tab">
      {data.channelSummary.state === "known" ? (
        <StatSummaryGrid channels={data.channelSummary.value.slice(0, 4)} />
      ) : (
        <PendingNote pending={data.channelSummary} testId="derived-stats-pending" />
      )}
      <Button disabled title="Full sheet not built yet (spec-derived-stat-sheet.md)" data-testid="derived-stats-open-full">
        Open full derived-stat sheet
      </Button>
    </div>
  );
}

function StatSummaryGrid({ channels }: { channels: ActorChannelDetail[] }) {
  // A small, new component — key/value pairs, four max. Not the plate's `.statgrid` CSS (that's
  // kit-only); a plain Tailwind grid matching this app's own existing primitives.
}
```

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- DerivedStatsTab
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/ui/actor/
  DerivedStatsTab.tsx        new
  StatSummaryGrid.tsx        new (small, reusable if another tab ever needs a headline-stat grid)
  DerivedStatsTab.test.tsx   new
```

## Code style

Match `ui/actor/`'s own conventions: `PendingNote` for the not-yet-real state, plain Tailwind grid
(no new CSS file) for `StatSummaryGrid`, `data-testid` per element.

## Testing strategy

- **Pending state (today's real state)**: renders `PendingNote` with the exact reason string from
  `adaptActor`, not a fabricated grid.
- **Known state (future-proofing)**: a mocked `channelSummary` with real values renders
  `StatSummaryGrid` correctly, capped at four channels.
- **The doorway button is always disabled today**, with the exact "not built yet" reason — a
  regression test against ever silently pointing it at a route that doesn't exist.

## Boundaries

- **Always:** render the real `channelSummary` pending/known state honestly.
- **Ask first:** wiring the "Open full sheet" button once `spec-derived-stat-sheet.md`'s own component
  exists — that's a one-line change here, but confirm the route/component name first rather than
  guessing it.
- **Never:** fabricate derived-stat numbers; link to a nonexistent route.

## Success criteria

1. Pending state renders honestly today (verified — this is the only reachable state right now).
2. Known-state rendering path exists and is tested, ready for the moment the backend endpoint ships.
3. The doorway button never links anywhere broken.
