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

## Corrected during grounding, 2026-08-29 — the visual-completeness audit is stale

The 2026-08-24 audit ([visual-completeness-audit-2026-08-24.md](../design/visual-completeness-audit-2026-08-24.md))
was read and initially trusted for this map's first draft. Re-verifying its two biggest findings
against the **current** code (not the audit's 5-day-old snapshot) found both already addressed by
later, undocumented-in-this-map work:

- **Finding 1/5d (Rail orientation) is fixed.** [`Rail.tsx:19-26`](../../web/fusion-rpg-web/src/shell/Rail.tsx)'s
  own doc comment: *"T25: a vertical, left-docked icon column... not the earlier horizontal strip."*
  The JSX is genuinely `flex flex-col`, a fixed `w-[92px]` column, `border-r` — confirmed wired into a
  real stage at [`SanctumStage.tsx:185`](../../web/fusion-rpg-web/src/stages/sanctum/SanctumStage.tsx).
  **`rail-reorient` is dropped from this map — there is nothing left to build.**
- **Finding 1 (Sanctum/FocusCard) is substantially addressed.** [`FocusCard.tsx`](../../web/fusion-rpg-web/src/stages/sanctum/FocusCard.tsx)
  has **four** branches today, not the audit's claimed two: zero-creature CTA, tribute-overdue,
  expedition-returned, and the run-prompt — three of the plate's own four priority tiers, with the
  fourth (fusable-pair) explicitly and deliberately skipped (own comment: no safe client-side
  eligibility check exists; shipping a heuristic that can lie was rejected on purpose). Referenced as
  "T26" in the file's own comments.

**One piece of the original finding is still real, confirmed by direct comparison, not the audit's
claim:** the zero-creature branch ("Bind your first creature" + an "Open Creatures" CTA) is not the
same thing as plate 01 §D's authored ritual (an already-bound creature revealed in place, with an
editable name field and a "Bind" action, never leaving the Sanctum). That gap is real — verified by
reading both the current component and the plate directly this session, not inherited from the audit.

## Modules

| Module id | Responsibility | Depends on |
|---|---|---|
| `onboarding-first-run` | Build plate 01 §D's actual naming-ritual content — the sunflower reveal, editable name field, "Bind" action — in place of the current "Open Creatures" CTA redirect, for the zero-creature `FocusCard` branch specifically. The other three branches (tribute/expedition/run-prompt) are real and out of scope here | — |
| `actor-menu-scope-picker` | A **new** composition, not a new plate section — assembles the existing, already production-proven Actor ladder (`ui/actor/{ActorToken,ActorChip,ActorRow,ActorCard,ActorPanel}`) into one reusable menu that emits a `WhoSelector`-shaped value (buff-debuff-scope program: `Target` / `Type` / `UniqueDemon` / `Relation`, all four modes per owner decision). FE-only for now — no backend wiring, since the commander/aura-skill feature that would consume this is still explicitly deferred | — (independent; can build in parallel) |
| `hide-legacy-entry` | Mechanical: hide whichever existing UI currently occupies the first-run / actor-selection space, in favor of the two new pieces above. Narrow scope, owner-confirmed (see Resolved below) | `onboarding-first-run`, `actor-menu-scope-picker` (hide only once the replacement exists) |

## Build order

`onboarding-first-run` and `actor-menu-scope-picker` run independently, in parallel — `hide-legacy-entry`
follows both. (The rail-reorient sequencing rationale that originally ordered this no longer applies,
since that module is gone.)

## Explicitly not in this program

The broader gap-audit backlog from the earlier FE-implementation audit this session (10 entities with
no React implementation, 29 new Class-A components with zero implementation, migrating Relics/Pacts/
Sector/Metrics off their bespoke/legacy code) — deliberately deferred to its own later program, per the
owner's own "ship essentials first" scoping. Any backend wiring for the commander/aura-skill feature
itself — the buff-debuff-scope program's own boundary, unchanged.

## Resolved, owner, 2026-08-29

**Map approved as proposed.** `hide-legacy-entry` is **narrow**: only whatever pre-existing UI serves
first-run/actor-selection today gets hidden in favor of the two new pieces. The four already-audited
legacy surfaces (Relics, Pacts, Sector, Metrics/Chronicle) are untouched by this program — that stays
the separate, later program named above.

## Corrected during grounding for `spec-hide-legacy-entry.md`, 2026-08-29 — there is nothing left to hide

Grounding this module against current code (not the original request's assumption that a distinct
legacy surface exists to remove) found:

- **First-run has no separate legacy component to hide.** [`spec-onboarding-first-run.md`](fe-essentials/spec-onboarding-first-run.md)
  replaces `FocusCard`'s zero-creature branch **in place** (same file, same integration point) —
  confirmed the old CTA copy ("Bind your first creature") appears exactly once in the tree, in that
  same branch. Once that spec ships, there is no dangling old component left to hide separately.
- **Actor-selection has no existing competitor to hide.** `actor-menu-scope-picker` is net-new — no
  screen currently renders a "pick who a buff/debuff reaches" menu, because the commander/aura-skill
  feature that would need one doesn't exist yet. There is nothing occupying that space today.
- **A real, larger legacy candidate exists but is out of this module's stated scope.** [`DemonsPage.tsx`](../../web/fusion-rpg-web/src/features/demons/DemonsPage.tsx)
  (own doc comment: *"Demon Domain V1... Summon panel... Active/Reserve roster, Codex"*) is a full
  top-level page still directly routed (`routes.tsx:81-88`, not redirected into Sanctum like almost
  every other legacy route) with its own working nickname mechanism (`useSetDemonNickname`) —
  materially more capable than Creatures' `adaptActor` (`displayName` hardcoded `"Pending"`, per
  `spec-onboarding-first-run.md`'s own finding). It plausibly duplicates roster/actor-selection
  territory, but hiding a full summon/pity/roster/codex feature is a materially bigger action than
  "hide the first-run/actor-selection entry point," and was never named or approved in the owner's
  own scoping. Left as an explicit open question in `spec-hide-legacy-entry.md` rather than assumed
  either way.

**`hide-legacy-entry` closes as a verification module**: confirm both replacements shipped cleanly and
left no dangling old code, rather than a module with its own removal work. See the spec for the
DemonsPage question, surfaced for owner review rather than resolved silently.
