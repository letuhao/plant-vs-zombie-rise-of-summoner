# Spec: `progression-tab`

**Module id:** `progression-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** `actor-sheet-shell` · **Blocks:** nothing

---

## Assumptions

1. **`xpToNext` is unconditionally pending for every actor today — there is no percentage/progress-bar
   experience to build without new backend work.** Confirmed in `adapt.ts`: `xpToNext:
   pendingWithReason(...)` unconditionally, not data-dependent. This tab shows the real `xp` count
   (a real, populated field, just never rendered anywhere today) and, when `xpToNext` is pending,
   an honest note instead of a fabricated bar — the same `PendingNote` pattern already used
   everywhere else on this panel. If `xpToNext` becomes real later, the bar activates with zero
   changes to this component's own logic (it already branches on `Pending<T>`'s `state`).
2. **Reuses the exact hooks and controls `AptitudesPage.tsx` already uses — no second allocation
   implementation.** `useAptitudes(playerId)` (returns `{ shares, budget, theta }`), `useSaveAptitudes()`
   (`.mutateAsync({ playerId, shares })`), and the `NumberInput` control — confirmed by reading
   `AptitudesPage.tsx` directly, not the draft plate's own `.stepper` mockup (that's the design kit's
   convention for the HTML plate; the shipped React app already has its own established control for
   this exact data, and consistency with it wins over matching the plate pixel-for-pixel).
3. **The twelve aptitude ids come from the server response, never a hardcoded list** — same rule
   `AptitudesPage.tsx`'s own comment states, carried over unchanged.

## Objective

One tab showing what's currently split across two disconnected places: per-actor level/XP (typed,
never rendered) and primary-stat distribution (real, but only reachable via the standalone Primary
Stats rail layer). Not a second allocation system — this tab fronts the identical save flow.

**Users:** a player who opened one specimen's sheet and wants to see and spend its progression in the
same place, without leaving to a different rail entry.

**Success is measurable:** level and real XP count render from `ActorView`; the aptitude grid renders
the server's own twelve ids with working `NumberInput`s; Save calls the same endpoint
`AptitudesPage.tsx` calls, with the same budget-exceeded refusal behavior (409, never clamped).

## Design

```tsx
function ProgressionTab({ data }: { data: ActorView }) {
  const players = usePlayers();
  const playerId = players.data?.currentPlayerId ?? 0;
  const aptitudes = useAptitudes(playerId);
  const save = useSaveAptitudes();
  // ...same draft/dirty/budget logic as AptitudesPage.tsx, verbatim, not reinvented.

  return (
    <div data-testid="progression-tab">
      <section data-testid="progression-level">
        <p>Level {data.level}</p>
        {data.xpToNext.state === "known" ? (
          <StatBar label={`${data.xp} / ${data.xp + data.xpToNext.value} xp`} value={data.xp} max={data.xp + data.xpToNext.value} />
        ) : (
          <PendingNote pending={data.xpToNext} testId="progression-xp-pending" />
        )}
        <p data-testid="progression-xp-raw">{data.xp} xp</p>
      </section>
      <section data-testid="progression-aptitudes">
        {/* the same NumberInput-per-id grid, budget bar, and Save button as AptitudesPage.tsx */}
      </section>
    </div>
  );
}
```

### Why this isn't a second aptitude implementation

`AptitudesPage.tsx`'s own draft/dirty/budget/save logic (lines 18-47 of that file) is copied here
verbatim, not reinvented with different edge-case handling — two allocation UIs with subtly different
bugs would be worse than one component used from two places. If this grows past ~20 shared lines, the
right refactor is extracting a `useAptitudeAllocation()` hook both `AptitudesPage` and this tab call —
not something this spec mandates up front, but flag it if the duplication gets large during review.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- ProgressionTab
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/ui/actor/
  ProgressionTab.tsx        new
  ProgressionTab.test.tsx   new
```

## Code style

Match `AptitudesPage.tsx`'s own conventions for the aptitude half (same hooks, same draft-state
pattern, same `NumberInput`/`Field` usage). Match `ui/actor/`'s own conventions for the level/XP half
(`PendingNote`, `data-testid` per section).

## Testing strategy

- **Level/XP**: real level and raw xp count render; when `xpToNext` is pending (today's real state),
  the pending note shows instead of a fabricated bar; a test with a mocked known `xpToNext` proves the
  bar activates correctly (future-proofing, not assuming the pending state is permanent).
- **Aptitude grid**: renders exactly the ids the mocked `useAptitudes` response returns (not a
  hardcoded twelve) — proves no hardcoded catalog crept in.
- **Save behavior parity with `AptitudesPage.tsx`**: over-budget disables Save with the same title
  text; a 409 response surfaces the same way; a successful save re-seeds the draft the same way.
- **No duplicate mutation path**: a test confirming `useSaveAptitudes` (not a new hook) is what this
  tab calls.

## Boundaries

- **Always:** reuse `useAptitudes`/`useSaveAptitudes`/`NumberInput` exactly as `AptitudesPage.tsx`
  does; degrade honestly when `xpToNext` is pending.
- **Ask first:** extracting a shared `useAptitudeAllocation()` hook, if duplication with
  `AptitudesPage.tsx` grows large enough to warrant it.
- **Never:** hardcode the twelve aptitude ids; fabricate an XP percentage when `xpToNext` is pending.

## Success criteria

1. Level and real XP count render; the progress bar activates only when `xpToNext` is genuinely known.
2. Aptitude grid round-trips through the exact same save endpoint and budget rules as the standalone
   Primary Stats page, proven by test, not by inspection.
3. No second allocation implementation — verified by the ids-from-server-response test.
