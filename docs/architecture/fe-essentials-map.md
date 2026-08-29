# Capability map: fe-essentials

Source: [docs/design/README.md](../design/README.md) (foundation methodology), the 2026-08-29 FE
implementation audit (this session — one entity, Actor, actually built; the rest are type stubs or
dead adapters), and [docs/design/visual-completeness-audit-2026-08-24.md](../design/visual-completeness-audit-2026-08-24.md)
(plate-vs-built comparison, not yet acted on). **Status: proposed, pending owner approval.**

## What this program is

The owner's own scoping (2026-08-29): *"hide legacy first and make new onboarding screen and actor
menu... legacy can reuse or migration later, ship essentials first."* Deliberately narrow — **not**
the full gap-audit backlog (10 missing entities, 29 new Class-A components, 4 legacy pages) from the
earlier FE-implementation audit. That stays a separate, later program.

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `rail-reorient` | Fix `src/shell/Rail.tsx` from a horizontal strip (confirmed by class name: `flex items-center ... overflow-x-auto border-b`) to the vertical icon dock plate 01/02/04 all draw, including the visibly-locked (not hidden) state for not-yet-reachable stages (plate 01 §D: *"Six of the eight rail entries are locked, and visibly so"*) | — |
| `onboarding-first-run` | Build plate 01 §D's real "First run" content for real — a naming ritual (bound-creature reveal → name input → "Bind"), not an empty state. `FocusCard.tsx`'s existing zero-creatures branch was already found to match the plate "reasonably" (visual-completeness audit, Finding 1) — this module's job may be narrower than a rebuild: confirm/polish what exists against the plate's exact copy and layout, not necessarily start from nothing | `rail-reorient` (sequencing, not a hard dependency — the visual-completeness audit's own recommendation: fix the cross-cutting rail before redoing content around it, so content work isn't redone) |
| `actor-menu-scope-picker` | A **new** composition, not a new plate section — assembles the existing, already production-proven Actor ladder (`ui/actor/{ActorToken,ActorChip,ActorRow,ActorCard,ActorPanel}`) into one reusable menu that emits a `WhoSelector`-shaped value (buff-debuff-scope program: `Target` / `Type` / `UniqueDemon` / `Relation`, all four modes per owner decision). FE-only for now — no backend wiring, since the commander/aura-skill feature that would consume this is still explicitly deferred | — (independent of the other two; can build in parallel) |
| `hide-legacy-entry` | Mechanical: hide whichever existing UI currently occupies the first-run / actor-selection space, in favor of the two new pieces above. **Scope not yet nailed down** — flagged honestly rather than guessed at (see Open below) | `onboarding-first-run`, `actor-menu-scope-picker` (hide only once the replacement exists) |

## Build order

`rail-reorient` → `onboarding-first-run` (sequencing preference, not a hard block) → `hide-legacy-entry`,
with `actor-menu-scope-picker` running independently, in parallel with any of the above.

## Explicitly not in this program

The broader gap-audit backlog from the earlier FE-implementation audit this session (10 entities with
no React implementation, 29 new Class-A components with zero implementation, migrating Relics/Pacts/
Sector/Metrics off their bespoke/legacy code) — deliberately deferred to its own later program, per the
owner's own "ship essentials first" scoping. Any backend wiring for the commander/aura-skill feature
itself — the buff-debuff-scope program's own boundary, unchanged.

## Open — needs the owner's call, not guessed

**`hide-legacy-entry`'s exact target is unclear from what's been read so far.** Two readings are both
plausible from the request text alone:
1. Narrow: whatever specifically serves first-run/actor-selection today (if anything predates the new
   pieces) gets hidden — nothing else touched.
2. Broader: this is shorthand for starting to hide the four already-audited legacy surfaces (Relics,
   Pacts, Sector, Metrics/Chronicle) generally, with onboarding/actor-menu as the first two *replacements*
   shipped alongside that hiding.

Worth confirming before this module gets its own spec, since the two readings differ by an order of
magnitude in scope.
