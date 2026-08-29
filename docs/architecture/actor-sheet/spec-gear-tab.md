# Spec: `gear-tab`

**Module id:** `gear-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** `actor-sheet-shell` · **Blocks:** nothing

---

## Assumptions

1. **`equipSlots` is unconditionally pending today, same as `channelSummary`** — no server endpoint,
   confirmed in `adaptActor`. Unlike Actions/Passives, this is not a level-gate — there's no lock to
   explain, just genuinely nothing to equip yet. `EmptyState`, not the locked-grid treatment from
   `locked-preview-tabs`.
2. **This tab absorbs `ActorPanel.tsx`'s existing "Equipment" `PendingNote` section** (moved here per
   `actor-sheet-shell`'s own spec, not duplicated on both Overview and Gear).
3. **`spec-equip-and-paperdoll.md` already exists as its own design** (not read in full for this
   module — out of scope here). This tab names it as the eventual doorway, the same relationship
   `derived-stats-tab` has with `spec-derived-stat-sheet.md`, without building or re-specifying it.

## Objective

An honest empty state for equipment, replacing the bare pending-note this content currently gets
buried under on Overview.

**Users:** a player checking what a specimen has equipped, today finding nothing because no
acquisition system exists yet — told that plainly, not left to guess why the tab is empty.

**Success is measurable:** the tab renders `EmptyState` with real copy naming why (no acquisition
system yet) and pointing at the real, already-existing spec that owns this eventually.

## Design

```tsx
function GearTab({ data }: { data: ActorView }) {
  if (data.equipSlots.state !== "pending") {
    // future-proofing: once equipSlots has a real shape, render it — not designed in this pass,
    // deferred to whoever builds the endpoint (matches derived-stats-tab's own precedent).
    return <PendingNote pending={data.equipSlots} testId="gear-pending-fallback" />;
  }
  return (
    <EmptyState
      title="No gear slots wired yet"
      hint="Equipment has its own design (spec-equip-and-paperdoll.md) — this tab becomes real once that system has a server endpoint."
      testId="gear-tab-empty"
    />
  );
}
```

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- GearTab
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/ui/actor/
  GearTab.tsx        new
  GearTab.test.tsx   new
```

## Code style

Match the existing `EmptyState` usage pattern from `AptitudesPage.tsx`/`CreaturesLayer.tsx` — same
component, same `title`/`description`/`testId` shape, no new empty-state component invented.

## Testing strategy

- **Today's real state**: `equipSlots` pending → `EmptyState` renders with the exact copy above.
- **Future state**: a mocked non-pending `equipSlots` does not crash and renders via the fallback
  branch (not fully designed, but proven not to break).

## Boundaries

- **Always:** honest empty state, not a fabricated slot grid.
- **Ask first:** designing the real equipped-gear render once `equipSlots` has a shape — that's
  `spec-equip-and-paperdoll.md`'s job, not invented here.
- **Never:** show fake equipment; treat this as a lock (it isn't one).

## Success criteria

1. Renders the honest empty state today, matching this tab's real (pending) data state.
2. Doesn't duplicate the Equipment pending-note that used to live on Overview — it moved, not copied.
