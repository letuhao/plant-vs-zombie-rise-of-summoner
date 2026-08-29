# Spec: `actor-sheet-shell`

**Module id:** `actor-sheet-shell` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** nothing · **Blocks:** `progression-tab`, `derived-stats-tab`, `locked-preview-tabs`,
`gear-tab` (all four mount as tabs inside this shell)

---

## Assumptions

1. **Today's `ActorPanel.tsx` has no tabs and far less real content than the design plate shows.**
   Confirmed by reading the file directly (not inferred from the plate): the "Standing" power vector,
   resource meters, and Effects chips visible in `00-foundation.html`'s mockup were **never built as
   React code**. The real component is an identity header (avatar/side/level/phase/name, all real)
   plus four bare `<PendingNote pending={data.X} />` sections (Standing/Element typing/Shield/
   Equipment — every one unconditionally pending, per `adaptActor`'s own hardcoded reason strings)
   and two footer buttons (`Release`/`Deploy`) with **no `onClick` at all**. This module wraps that
   real content in a tab bar; it does not invent meters/vectors with no data to bind to.
2. **The footer buttons get a minimal, honest fix, not a redesign.** `Release`/`Deploy` doing nothing
   today is a pre-existing defect this module surfaces by touching the file anyway. Proposal: wire
   `onOpenChange(false)` as the bare minimum (closing the panel is always correct; it beats a button
   that visibly does nothing) — a real Release/Deploy *mutation* is out of scope here (no spec names
   what either should actually call). Correct me now if the owner wants these left exactly as
   non-functional rather than partially wired; proceeding with "close on click" otherwise.

## Objective

Replace `ActorPanel`'s flat, four-`PendingNote` body with a six-tab container — Overview /
Progression / Derived Stats / Actions / Passives / Gear — so a player has one door into everything
about a specimen instead of hunting across the standalone Primary Stats rail layer, a locked-but-
unbuilt derived-stat spec, and nothing at all for actions/passives/gear.

**Users:** any player opening a specimen's panel (rung 5, band 2, opens over any stage — GG-9's "one
canonical actor surface").

**Success is measurable:** the panel shows a real tab bar; Overview (tab 1) renders exactly what
`ActorPanel.tsx` renders today, unchanged in content, just relocated under a tab; the other five tabs
render their own module's content once built, or an inert-but-correct empty container until they are.

## Design

### Tab bar, using the shared `TabList` primitive already proven in `ui/scope/ActorMenuScopePicker.tsx`

```tsx
type ActorSheetTab = "overview" | "progression" | "derived-stats" | "actions" | "passives" | "gear";

const TABS: TabItem[] = [
  { id: "overview", label: "Overview", testId: "actor-sheet-tab-overview" },
  { id: "progression", label: "Progression", testId: "actor-sheet-tab-progression" },
  { id: "derived-stats", label: "Derived Stats", testId: "actor-sheet-tab-derived-stats" },
  { id: "actions", label: "Actions", testId: "actor-sheet-tab-actions" },
  { id: "passives", label: "Passives", testId: "actor-sheet-tab-passives" },
  { id: "gear", label: "Gear", testId: "actor-sheet-tab-gear" }
];
```

`ActorPanel.tsx` gains local `useState<ActorSheetTab>("overview")`, renders `<TabList tabs={TABS}
value={tab} onChange={...} testId="actor-sheet-tabs" />` between the identity header and the body,
then conditionally renders one tab's content — mirroring `ActorMenuScopePicker`'s own established
mode-switch shape exactly (same session, same pattern, already proven).

### What moves where

- Identity header (avatar, side badge, level tag, phase, name pending-note) — **stays exactly as-is**,
  above the tab bar, visible regardless of which tab is active (matches the plate's own header/tabs
  split).
- The four existing `PendingNote` sections (Standing/Element typing/Shield/Equipment) — **Standing
  and Element typing move into the Overview tab unchanged**; Shield has no owning tab named in this
  program (left on Overview, since no module claims it) and Equipment's pending-note **moves into
  `gear-tab`** (it's the same field `gear-tab`'s own empty state is built around — no duplicate
  pending-note between two tabs).

### Footer

Unchanged position (still `PanelShell`'s `footer` prop, always visible regardless of active tab) —
only the `onClick` wiring changes per Assumption 2.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- ActorPanel
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/ui/actor/
  ActorPanel.tsx        edited — gains the tab bar, delegates tab bodies to the other 4 modules
  ActorPanel.test.tsx   new — this module's own tests (no file exists today)
```

## Code style

Match `ui/scope/ActorMenuScopePicker.tsx`'s own conventions exactly (same repo, same session, already
reviewed): `TabList` for the switch, a `kind`-discriminated render per tab, `data-testid` on every
interactive element and every tab's root container.

## Testing strategy

- **Overview content is byte-identical to today**, just relocated: a test asserting `actor-standing-
  pending`/`actor-element-pending` (or whichever two stay) render exactly as they do on `main` today.
- **Tab switching**: clicking each tab shows that tab's own root testid and hides the others (no two
  tab bodies mounted at once).
- **Non-ready states unchanged**: loading/empty/error/locked still short-circuit to
  `RungStateFallback` before the tab bar ever renders (this module must not regress that guard).
- **Footer buttons**: `Release`/`Deploy` each call `onOpenChange(false)` — proven by test, closing the
  gap where they previously did nothing detectable.

## Boundaries

- **Always:** keep Overview's real content unchanged in substance; keep non-ready states short-
  circuiting before the tab bar.
- **Ask first:** giving `Release`/`Deploy` a real mutation — no spec names what either should call.
- **Never:** invent resource meters or a Standing power-vector with no backing `ActorView` field to
  bind to — that gap is explicitly out of this program (map's own exclusion list).

## Success criteria

1. Six real tabs render; Overview shows today's real content unchanged.
2. Non-ready states (loading/empty/error/locked) still short-circuit correctly.
3. `Release`/`Deploy` each do something detectable (close the panel) instead of nothing.
4. The other four modules have a real tab body to mount into once each ships.
