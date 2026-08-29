# Spec: `onboarding-first-run`

**Module id:** `onboarding-first-run` · **Program:** [fe-essentials-map.md](../fe-essentials-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** nothing · **Blocks:** `hide-legacy-entry`

---

## Assumptions

1. **The name field cannot be functional yet — confirmed, not guessed.** [`CreaturesLayer.tsx:36-38`](../../web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx)'s
   own comment: *"No creature has a resolved display name yet — `adaptActor`'s `displayName` is
   `Pending` ('Names resolve from the almanac catalog, not wired to this reader yet')."* This is the
   FE's own most mature, production-wired layer saying naming isn't wired **anywhere** yet. The plate's
   ritual card (`01-shell-home.html` §D) shows an editable name input pre-filled "Emberling" next to
   "Bind" — that interaction has no real place to write today.
   → **This spec ships the reveal (art, headline, body copy) and the "Bind" action, and drops the name
   input.** Shipping a text field that doesn't persist would be exactly the "half-finished
   implementation" this repo's own rules forbid — it would look functional and silently do nothing.
   Naming becomes a follow-up module once a real endpoint exists. Correct me now if the owner would
   rather stub the input non-functionally for visual completeness, or block this module until naming
   ships — I'll proceed with "drop it, note the gap" otherwise.
2. **"Bind" reuses the existing redirect, it does not invent a new bind mechanism.** The current
   zero-creature CTA already has a real destination (`onOpenCreatures`, opens the Creatures layer,
   which has its own "Bind one to see it here" empty-state hint — [`CreaturesLayer.tsx:180`](../../web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx)).
   This module re-themes the *entry point* into that flow; it does not change what happens after.
3. **The species shown in the reveal (sunflower) is illustrative, not literal.** Nothing in the current
   data model designates a specific "starter species" — `firstActorState` in
   [`SanctumStage.tsx:170-171`](../../web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx) is
   `actors.length > 0 ? ... : null`, no starter-grant concept exists. The reveal art is themed (a
   generic "awakening" visual matching the plate's `frame--panel` token), not tied to an actual bound
   species — because there isn't one yet at this screen.

## Objective

Replace the zero-creature branch of `FocusCard` — currently a bare "Bind your first creature" /
"Open Creatures" redirect — with the plate's authored first-run beat (`01-shell-home.html` §D,
GG-43/GG-44): a written beginning, not an empty box. The other three `FocusCard` branches (tribute-
overdue, expedition-returned, run-prompt) are real and untouched.

**Users:** every new save, first screen after account creation, before any creature is bound.

**Success is measurable:** the zero-creature branch renders the plate's reveal framing (headline +
body copy matching the plate's own tone) and a single "Bind" action that reaches the same real
destination the current CTA already reaches; the other three `FocusCard` branches are byte-identical
to today; no non-functional input ships.

## Design

### New child component, not inline JSX growth

`FocusCard`'s current zero-creature branch is 15 lines of static JSX (`FocusCard.tsx:44-64`). The
plate's version needs its own visual treatment (centered layout, larger reveal art, GG-43 framing) —
different enough from the other three branches' shared card shell that it reads better as its own
component than as a fifth `if` bloating `FocusCard` further.

```tsx
// web/fusion-rpg-web/src/stages/sanctum/FirstRunReveal.tsx
export function FirstRunReveal({ onBind }: { onBind: () => void }) {
  return (
    <div
      className="grid place-items-center gap-4 rounded-md border border-border-control bg-panel p-8 text-center"
      data-testid="focus-card-first-run"
    >
      <span className="frame frame--panel" data-rarity="2" aria-hidden="true">🌻</span>
      <div>
        <h3 className="font-display text-2xl text-text">This one answered</h3>
        <p className="mt-2 text-sm text-muted">
          A sunflower has bound itself to you. It will remember what it learns, and it will come
          back after every night.
        </p>
      </div>
      <Button data-testid="focus-card-cta" onClick={onBind}>Bind</Button>
    </div>
  );
}
```

`FocusCard.tsx:44-64`'s branch becomes a two-line delegate: `if (actorCount === 0 || !firstActor) return <FirstRunReveal onBind={onOpenCreatures} />;` —
same prop, same destination, same `data-testid="focus-card-first-run"` / `"focus-card-cta"` so nothing
downstream (tests, other components) that keys off those ids breaks.

### What's deliberately not touched

- **The rail's locked-entry styling** — already real. [`Rail.tsx:41-63`](../../web/fusion-rpg-web/src/shell/Rail.tsx)
  already supports `entry.state === "locked"` with a `disabled` button, a `lockedReason` title, and
  dimmed styling — exactly what plate §D's "six of eight rail entries locked" shows. Confirmed by
  reading the component directly; no rail work belongs in this module (this is also why `rail-reorient`
  was dropped from the capability map entirely).
- **`overdueContract` / `returnedExpeditionCount` / run-prompt branches** — real, tested, out of scope.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test -- FirstRunReveal FocusCard
npm run build
```

## Project structure

```
web/fusion-rpg-web/src/stages/sanctum/
  FirstRunReveal.tsx        new
  FirstRunReveal.test.tsx   new
  FocusCard.tsx             edited — zero-creature branch delegates to FirstRunReveal
```

## Code style

Match `FocusCard.tsx`'s own conventions: plain function component, no new state (the component is
`onBind` in, nothing out), Tailwind + the plate's own token classes (`frame`, `frame--panel`,
`data-rarity`) rather than inventing new ones — these already exist in the shared stylesheet the
plate HTML references, confirmed reused verbatim by `FocusCard`'s own existing branches' `rounded-md
border ... bg-panel` pattern.

## Testing strategy

- **Renders the reveal, not the old copy**: zero-creature state shows "This one answered" / the
  sunflower reveal, not "Bind your first creature".
- **`onBind` reaches the same destination**: clicking "Bind" invokes the same `onOpenCreatures`
  callback `FocusCard` already receives — proven by a test asserting the prop is called, not by
  trusting the wiring visually.
- **No name input rendered**: an explicit assertion that no `<input>` exists in this branch, so a
  future edit doesn't silently reintroduce the non-functional field this spec deliberately dropped.
- **Other three branches unchanged**: existing `FocusCard` tests for tribute/expedition/run-prompt
  continue passing without modification — proves this change is additive, not a rewrite.

## Boundaries

- **Always:** reuse the existing `onOpenCreatures` callback as the real destination; keep the other
  three `FocusCard` branches untouched; keep existing `data-testid`s stable.
- **Ask first:** wiring an actual name-write endpoint — that's new backend surface, not an FE-only
  essentials change.
- **Never:** ship the name `<input>` from the plate mockup as decoration — it implies a working
  rename that does not exist, which is the exact half-finished-implementation pattern this repo's own
  rules forbid.

## Success criteria

1. Zero-creature `FocusCard` state shows the plate's reveal framing (headline, body copy, single
   "Bind" action) via a new `FirstRunReveal` component.
2. "Bind" reaches the same real destination the current CTA already reaches — no new backend call.
3. No non-functional name input ships.
4. The other three `FocusCard` branches are provably unchanged (existing tests, unmodified, still pass).
