# Plan: world stage

**Status: proposed 2026-09-03, pending owner review. No build authorized by this document.**

**Specs:** [world-stage-map.md](../docs/architecture/world-stage-map.md) (15 modules, 5 levels, and
the **arbitration section** that wins when a module spec disagrees) · the 15 module specs in
[docs/architecture/world-stage/](../docs/architecture/world-stage/).
**Ideal:** [world-stage-ideal.md](../docs/architecture/world-stage-ideal.md) — 16 owner decisions
across four rounds, plus a four-perspective review (§8c) and a five-audit pass over the specs (§8c.7).
**Tasks:** [world-stage-todo.md](world-stage-todo.md).

**Sibling program:** `sector-development` is wave 3 of `world-map-program`, not of this one. Its
tasks append to [world-map-todo.md](world-map-todo.md) as Phase 12. The two programs meet at exactly
two places, both named in §"The seam with sector-development" below.

---

## Overview

Rebuild `#/world` as a **stage**. Today it is a flowchart library in a 620px box inside a scrolling
document: every verb is a text button in a 300px sidebar, the economy prints raw engine strings at the
player, and once a sector is selected there is no way back to nothing selected. It is the only route
in the app that never entered the new shell.

Fifteen modules, sliced here into **five phases and two gates**.

## How this plan slices, and why not the other way

§8d.4 settled the shape of this question before the plan existed. A review proposed cutting the design
to a ~25–40 task minimum; the owner rejected that as a **scope** reduction and adopted it as a
**planning** technique: *"we will make spec and plan with multiple slice to build — this is idea phase,
we need solid idea first."*

So the full scope is the destination, and the slicing lives here. Two consequences shape every phase
below:

1. **Phases are vertical where the architecture allows and honest where it does not.** Levels 1–2 are
   genuinely a horizontal seam — a contract, a projection and a command surface serve every later
   module, and pretending otherwise would mean building six partial versions of each. The skill warns
   against horizontal slicing and it is right to; this plan deviates for the first two phases only,
   states the reason here, and is vertical from Phase 2 onward, where each phase delivers a
   **playable capability** rather than a layer.
2. **The playtest gate comes as early as a playable stage exists, not at the end.** That is the loam
   program's own Checkpoint 5 precedent — gated on *play* rather than on completeness — and it is what
   makes the remaining phases arguable from evidence instead of from Amplitude.

## Architecture decisions carried in from the specs

Recorded here so a task never re-argues one. Each is settled in the map's arbitration section.

| Decision | Consequence for this plan |
|---|---|
| **`world-commands` takes `RulesetVersion` 5 → 6; `sector-development` takes 6 → 7** | A hard ordering between two programs. Phase 0 must land before `world-map` Phase 12's engine work, or the second bumper reads the wrong current value |
| **The pure layer moves, it does not die** | `worldSelection.ts`, `worldViewModel.ts`, `turnPlayback.ts`, `commanderIntent.ts` and both fixtures move to `stages/world/` at their consuming phase. `features/world/` is deleted in **Phase 4**, not Phase 1 |
| **`bind-warden` (sector) and `ward` (lane) are two kinds** | The naming collision was repaired once in plate 11; it must not return through a task title |
| **Arrows pan; `W` cycles** | `WASD` was removed for this exact collision. No task reintroduces it |
| **Do not wrap a `registerGlobalVerb` throw** | Collisions are prevented at source — `keybindings.ts` refuses a rebind onto `1`–`9` |
| **`world-wire` carries four obligations three other specs assigned it** | Per-lane march cost, the `LoamUpkeep` operand breakdown, a legion display name, and a `supply.restored` line. Phase 0's scope is nine additions **plus four** |
| **The live scrim defect is `PanelShell.tsx:61`**, not the kit's `.scrim` | Phase 2's HUD task must touch that file, or the fix ships believing itself done |
| **GG-50: five surfaces to register, `toHaveLength(8)` → `(13)`** | A shared task in Phase 4, listed by all four owning modules so it cannot be dropped |

## Dependency graph

```text
PHASE 0 — the seam                    (Gate A)
  world-contract ─┐
  world-wire ─────┼── all three parallel, no deps between them
  world-commands ─┘

PHASE 1 — the map is a place
  world-shell ──── world-contract
  world-numbers ── world-contract          (parallel with shell)
        │
        ├── world-render ── world-shell + world-contract
        └── world-hud ───── world-shell + world-contract

PHASE 2 — the map is playable            (→ Gate B: the ten-turn playtest)
  world-inspector ── world-render + world-numbers
  world-targeting ── world-render + world-commands
  world-playback ─── world-contract + world-wire     (parallel; no stage dep)

PHASE 3 — the empire is legible
  world-turn ────── world-hud
  world-notify ──── world-hud + world-turn
  world-outliner ── world-hud (+ world-turn's unresolvedLegions.ts — a map omission, real)

PHASE 4 — depth, and retirement
  world-lenses ──── world-render
  world-confirms ── world-inspector + world-commands
  retirement ────── everything above
```

**Two back-edges the map's table does not show**, both real and both inside a phase rather than
across one: `world-outliner` imports `world-turn`'s `unresolvedLegions.ts`, and `world-inspector`
needs the cede capability flag `world-commands` produces. Neither inverts a phase.

---

## Phases

### Phase 0 — the seam

The three level-1 modules, in parallel. Nothing above them is safe to build first, and the reason is
recorded in the repo's own words: *"getting a field wrong is cheap while nothing binds to it and
expensive once eleven modules do."*

The `typeId` narrowing is the first thing in the program and it costs an ADR, not a decision.

**→ Gate A.**

### Phase 1 — the map is a place

The stage exists, fills the viewport, and **the page never scrolls**. The map renders from tokens with
no state carried by colour or opacity alone, and a band-1 HUD says what the empire earns and spends.

**On `@xyflow/react`, corrected 2026-09-03 while breaking down this phase:** the *stage* is
library-free here and a test guards that, but the **dependency cannot leave `package.json` until
Phase 4**. Its three importers — `WorldPage.tsx`, `SectorNode.tsx`, `LaneEdge.tsx` — are the *old
page's* view layer, and the arbitration puts that page's deletion in the retirement task. An earlier
draft of this phase said "`@xyflow/react` is gone", which was not deliverable here.

This is the phase that answers the owner's literal complaint. It is not yet playable.

### Phase 2 — the map is playable, and it speaks

Selection opens a bounded, left-docked inspector. Orders are given **on the map** with routes,
ranges and translated refusals. And the turn report stops printing engine strings — `world-playback`
is in this phase rather than a later one because GG-23 is Tier-1, and a stage that has fixed the
layout but not the vocabulary has fixed one of the two failures §7 names.

**→ Gate B — the ten-turn playtest.** Everything in Phases 3 and 4 is re-argued from what it finds.

### Phase 3 — the empire is legible

The turn cluster that knows what is unresolved; two notification classes that flush on End Turn except
blockers; the outliner that makes 28 rows scannable and gives the map its first keyboard entry point.

### Phase 4 — depth, and retirement

Lenses, the band-3 confirms, and then the retirement task: `features/world/` deleted, its three
standing exemptions retired together, and the GG-50 registry closed.

---

## The seam with `sector-development`

The two programs meet at exactly two points, and both are already settled:

1. **`RulesetVersion`.** Phase 0 takes 5 → 6. `world-map` Phase 12 takes 6 → 7. Ordering is required;
   parallelism after that is fine.
2. **The legion count.** §8e.3 fixed it at **6–10, tunable**, which is what let both programs be
   specced independently (§8e.4). `sector-development` authors the tuning row; this program sizes its
   fixtures against the number. Neither waits for the other.

`world-stage` consumes the unit count; it never designs recruitment.

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **A command field is lost in the store round-trip** — six sites, two of which fail silently, and this has already happened once with `stance` | High | The property test over **every kind in `WorldCommandKinds.All` × every optional member**, adopted from `sector-development`'s spec. It closes the defect *class*, not the two instances. In Gate A |
| **The scrim fix misses the live defect** | High | Named explicitly: `PanelShell.tsx:61`. The kit and GG-5 amendment stop it being re-authored; the component fix is what removes it |
| **The GG-50 registry turns a green test red** | Medium | A shared Phase 4 task, listed in all four owning specs |
| **Two programs collide on `RulesetVersion`** | Medium | Ordered above; each reads the current value rather than hard-coding 6 |
| **The playtest is deferred until "it's ready"** | High | Gate B is a phase boundary, not a milestone. Phase 3 does not start until it is answered |
| **A superseded claim is built from a spec rather than the map** | Medium | The map's arbitration section wins by declaration, and every contested row names both sides |
| **Fixtures the plan assumes nobody owns** — an 18-sector / 10-legion fixture, a `two-hearths` fixture | Medium | Assigned in Phase 0 to `world-wire`, whose generator already produces the shipped one |

## Checkpoints

**Gate A — the seam holds** (end of Phase 0). The `typeId` ADR is recorded with its version bump ·
`contractGuard` catches a feature-local DTO import · **all nine `world-wire` additions** plus the four
re-homed obligations reach a client, fixture re-blessed once · a command of **every kind** survives the
reveal round-trip · `turn-report`'s golden exists as `first-light-turn.json` and carries one entry of
each visibility class · the fog fix is asserted at all **three** sites.

**Gate B — the owner plays it** (end of Phase 2). Ten turns on `two-hearths`, answering three
questions no test can: **did you scroll · could you tell what happened last turn without reading an
engine string · did you ever reach for a control you could not find.** Phases 3–4 are re-argued from
the answers.

**Gate B outcome (2026-09-05, played by the assistant — owner directed the assistant to run
playtest/review gates directly).** Full answers and how they were produced live in
`world-stage-todo.md`'s Gate B section; the re-argument itself:

- **Did you scroll?** No, at 1440×900, all ten turns. No bearing on Phase 3/4's order — nothing in
  either phase is scoped around a scrolling concern.
- **Could you tell what happened without reading an engine string?** Mostly, with one gap: a
  multi-hop march's intermediate-waypoint report line carries no visible link back to the order that
  produced it. This is `world-playback`'s own territory (Phase 2, already built) rather than anything
  Phase 3/4 owns — none of `world-turn`/`world-notify`/`world-outliner`/`world-lenses`/`world-confirms`
  touch how a keyframe's text is composed. Recorded as a `world-playback` finding, not a Phase 3/4
  re-ordering.
- **Did you ever reach for a control you could not find?** Twice — clearing a guard has no control
  anywhere, and re-selecting an already-selected legion silently deselects it. **Neither touches
  Phase 3/4's five modules either.** Guard-clearing is a `world-targeting`/inspector command (Phase 1,
  already shipped, `worldSelection.ts`'s own `clear` kind never gets a filing control); the selection
  toggle is `world-render`'s legion-marker click handler (also Phase 1). Both are real, both are
  recorded (`world-map-todo.md`'s Checkpoint 4, `world-stage-todo.md`'s Gate B section) — neither is a
  Phase 3/4 task today, and inventing one to hold them would be scope the playtest didn't actually ask
  for.

**Conclusion: Phase 3/4's order stands, unchanged.** Every finding Gate B produced lands in Phase 1/2's
territory (already shipped) rather than in anything Phase 3/4 was going to build — `world-turn` first
(feeding `world-outliner` its `unresolvedLegions.ts`), `world-notify` and `world-outliner` next,
`world-lenses` and `world-confirms` in Phase 4, exactly as this plan already had it. The playtest
answering "no reorder needed" is still an answer, not a skipped step — the alternative (reordering
without evidence, or refusing to say so because nothing moved) is what this gate exists to prevent.

**Checkpoint C — complete** (end of Phase 4). All 15 modules built · `features/world/` deleted and its
three exemptions retired in the same change · GG-50 registry at 13 · the four boundary guards green ·
the full web and .NET suites green.

## Open questions

**One, and it is `world-turn`'s, recorded rather than resolved:** `useGlobalKeys.ts:25` passes only
`event.key` to `dispatchGlobalVerb`, with no modifier state, so `Shift+Enter` is indistinguishable
from `Enter` and the force-end shortcut is **not expressible as drawn**. Two resolutions are costed in
that spec; the pointer path ships either way, so this constrains the shortcut and not the feature.
It needs an answer before Phase 3, not before Phase 0.
