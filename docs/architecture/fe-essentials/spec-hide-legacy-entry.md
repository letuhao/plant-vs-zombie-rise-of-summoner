# Spec: `hide-legacy-entry`

**Module id:** `hide-legacy-entry` · **Program:** [fe-essentials-map.md](../fe-essentials-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** `onboarding-first-run`, `actor-menu-scope-picker` (verification follows both)

---

## Assumptions

1. **There is no separate legacy component left to remove — grounded, not assumed.** See
   [fe-essentials-map.md's "Corrected during grounding" section](../fe-essentials-map.md). The old
   first-run CTA ("Bind your first creature") lives in exactly one place — the same `FocusCard`
   branch `onboarding-first-run` already replaces in place — confirmed by a repo-wide search that
   found a single match. `actor-menu-scope-picker` has no existing competitor: nothing currently
   renders a who-picker menu, because the feature that would need one doesn't exist yet.
   → This module cannot remove code that doesn't exist. It closes as **verification**: confirm both
   replacements landed cleanly and nothing old was left reachable, not as its own removal work.
2. **`DemonsPage` is a real, larger legacy candidate, deliberately left as an open question, not a
   decision.** [`DemonsPage.tsx`](../../web/fusion-rpg-web/src/features/demons/DemonsPage.tsx) is a
   full "Summon panel, pity counters, reveal with nickname/lock, Active/Reserve roster, Codex" page
   (its own doc comment), still directly routed at `/demons` — unlike almost every other legacy route
   in `routes.tsx`, which already redirects into a Sanctum panel. It has a working nickname mechanism
   (`useSetDemonNickname`) that Creatures' own actor adapter still lacks. It plausibly overlaps
   "actor selection," but hiding a full summon/roster/codex feature is a materially bigger action than
   what "hide the first-run/actor-selection entry point" was scoped and approved to mean — and nothing
   in the owner's own framing named it. **Recommendation: leave `DemonsPage` and its `/demons` route
   untouched in this module** — same treatment as the four already-named legacy surfaces (Relics,
   Pacts, Sector, Metrics/Chronicle) — and revisit it explicitly if/when the broader legacy-migration
   program (already deferred per the map's "Explicitly not in this program" section) picks it up.
   Correct me now if the owner intends `/demons` in scope here; I'll proceed with "leave it" otherwise.

## Objective

Verify that once `onboarding-first-run` and `actor-menu-scope-picker` ship, nothing legacy remains
reachable in the specific space they replace — the first-run zero-creature moment, and (in the future,
once a real consumer exists) actor-selection for scope-picking. Not a removal task in its own right,
per the Assumptions above; a confirmation pass, sized to what was actually found.

**Success is measurable:** no reachable path in the app shows the old "Bind your first creature" /
"Open Creatures" copy; `DemonsPage` and every other already-named legacy surface remain deliberately
untouched and are named as such, not silently swept in.

## Design

Two checks, not two builds:

1. **First-run**: after `onboarding-first-run` ships, grep the tree for the old copy string
   ("Bind your first creature") — expect zero matches. If any survive (a second copy in a snapshot
   test fixture, a docs reference, etc.), remove or update just that reference — not a redesign.
2. **Actor-selection**: confirm `actor-menu-scope-picker` has no live integration yet (by design — its
   own spec ships it standalone, ahead of a consumer) and that no other component in the tree renders
   a competing who-picker. If grounding for a future consumer module later finds one, that module
   handles its own migration — not retroactively assigned to this one.

No new component, no new route change, no `DemonsPage` work — per Assumption 2.

## Commands

```powershell
cd web/fusion-rpg-web
npm run test
npm run build
```

No new test files — this module verifies via the other two modules' own tests plus a repo-wide grep,
not a new component of its own.

## Project structure

No new files expected. If the first-run grep check finds a stray reference, the fix lands wherever
that reference lives (most likely a test fixture or docs page) — not a new directory.

## Code style

N/A — no new code expected under the Assumptions above.

## Testing strategy

- **Grep-verified, not assumed**: the "zero matches for the old copy" check is a literal repo search
  run and its output recorded, the same discipline this program's own map correction used — not a
  claim taken on faith.
- **`onboarding-first-run` / `actor-menu-scope-picker`'s own test suites** are the real coverage for
  "the replacement works" — this module doesn't duplicate them.

## Boundaries

- **Always:** verify via search before claiming anything is clear; name `DemonsPage` explicitly as
  untouched rather than silently ignoring it.
- **Ask first:** touching `/demons`, `DemonsPage.tsx`, or its route — explicitly out of scope per
  Assumption 2 unless the owner says otherwise.
- **Never:** remove or hide a legacy surface this module didn't name and get confirmed.

## Success criteria

1. Zero matches for the old first-run copy anywhere in the tree after `onboarding-first-run` ships.
2. No competing actor-selection UI found for `actor-menu-scope-picker` to have displaced.
3. `DemonsPage`/`/demons` and the four already-named legacy surfaces remain explicitly, deliberately
   untouched — named in the report, not silently skipped.
