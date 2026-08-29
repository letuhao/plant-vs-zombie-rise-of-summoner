# Spec: `locked-preview-tabs`

**Module id:** `locked-preview-tabs` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** `actor-sheet-shell` · **Blocks:** nothing

---

## Assumptions

1. **`.actionslot`/`.passnode` are plate-only CSS** (confirmed by grep — zero React usage anywhere).
   This module writes new, small React components for the locked-grid look; it does not "reuse" a
   component that doesn't exist. What it *does* reuse is the **real, already-shipped locked-state
   convention from `Rail.tsx`** (`entry.state === "locked"` → `disabled`, a `title` naming the reason,
   `opacity-60`/`cursor-not-allowed` styling) — the one proven "locked, say what unlocks it" pattern
   that actually exists in this codebase today, per GG-17.
2. **Neither Actions nor Passives has a real system behind it.** The action program is "approved
   2026-08-22, not yet built" (`rpg_action` returns zero hits in `src/`); passive skills are the
   owner's own explicitly deferred sub-feature with no module id. Every slot in both grids is locked
   by construction — there is no "some unlocked, some not" state to build yet, only "all locked, for a
   named, honest reason."
3. **Content is illustrative, not a commitment.** The specific actions/passives named in the draft
   plate (Firebolt, Guard, Bloom Everlasting, etc.) are placeholder flavor text, not real catalog
   entries — no catalog exists yet for either.

## Objective

Two small, static preview grids — Actions and Passives — that tell a player "this exists, it's coming,
here's why it's not available yet," reusing the Rail's own honest locked-state convention rather than
a dead-looking disabled control or (worse) hiding the tabs entirely.

**Users:** a player exploring what a specimen will eventually be able to do, before either system
exists.

**Success is measurable:** both tabs render a locked grid using the Rail's real locked-state visual
language; every slot names its real reason on hover/focus; nothing is clickable in a way that implies
it does something.

## Design

```tsx
function LockedGridSlot({ label, reason }: { label: string; reason: string }) {
  return (
    <div
      className="grid place-items-center gap-1 rounded-md border border-dashed border-border-control p-3 opacity-60"
      title={reason}
      data-testid={`locked-slot-${label.toLowerCase().replace(/\s+/g, "-")}`}
    >
      <span aria-hidden="true">🔒</span>
      <span className="text-xs">{label}</span>
    </div>
  );
}

function ActionsTab() {
  return (
    <div className="grid grid-cols-4 gap-2" data-testid="actions-tab">
      {PLACEHOLDER_ACTIONS.map((a) => (
        <LockedGridSlot key={a} label={a} reason="Unlocks once the action system ships (approved, not yet built)" />
      ))}
    </div>
  );
}

function PassivesTab() {
  return (
    <div className="grid grid-cols-4 gap-2" data-testid="passives-tab">
      {PLACEHOLDER_PASSIVES.map((p) => (
        <LockedGridSlot key={p} label={p} reason="Passive skills are a reserved sub-feature, no target date yet" />
      ))}
    </div>
  );
}
```

No "one basic action unlocked" cell (unlike the draft plate's own mockup) — the plate could show one
because the plate is illustrative of the *intended end state*; this spec ships only what's true today,
and today there is no working basic-action call this component could wire to without inventing one.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- ActionsTab PassivesTab
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/ui/actor/
  LockedGridSlot.tsx    new (shared by both tabs)
  ActionsTab.tsx        new
  PassivesTab.tsx       new
  LockedGridSlot.test.tsx, ActionsTab.test.tsx, PassivesTab.test.tsx   new
```

## Code style

Match `Rail.tsx`'s own locked-state Tailwind classes exactly (not a new visual language), plain
`data-testid` per slot, no new CSS files.

## Testing strategy

- **Every slot is locked**: no slot in either grid is an interactive button with a working `onClick` —
  a regression test against ever accidentally wiring one to nothing.
- **Reason is present and correct**: each slot's `title` names its real reason, matching Rail's own
  hover-reveals-why convention (GG-17), not a generic "locked" string.
- **Visual consistency**: a snapshot-style test (or a direct class-list assertion) confirming the
  locked styling matches `Rail.tsx`'s own locked-button classes, not an independently invented look.

## Boundaries

- **Always:** every slot locked, every slot names its real reason.
- **Ask first:** adding a real catalog of actions/passives once either system has content — this
  module's placeholder list is illustrative only.
- **Never:** wire a slot to a fake/no-op action; imply availability that doesn't exist.

## Success criteria

1. Both tabs render, fully locked, using the Rail's real (not plate-only) locked-state convention.
2. Every slot's hover/focus reveals its real, honest unlock reason.
3. Nothing here reads as "broken" — locked-and-honest, not disabled-and-unexplained.
