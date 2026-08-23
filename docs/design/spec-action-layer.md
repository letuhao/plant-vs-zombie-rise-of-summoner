# The action layer — the card, the cost cluster, targeting, and honest refusal

**Status:** Detail design, 2026-08-23. **Document 7 of 9, the last one** owed by
[gap-audit-2026-08-22.md](gap-audit-2026-08-22.md) §7. Covers gaps **A20** (action card), **A21**
(action cost cluster), **A22** (targeting preview), **A23** (usability conditions), **A24** (battle
grid + range bands).

**The most exposed document in the set.** `rpg_action` returns zero hits in `src/` — the action
program's own capability map states *"No specs written, no build authorized"* at the map level, though
*"all ten [module specs are] written."* Everything here designs against specs, not code, and says so
throughout rather than only once.

**Sources, all read this session:**
[`action-map.md`](../architecture/action-map.md) §10.6 (the seal), the membership rule ·
[`action/spec-action-model.md`](../architecture/action/spec-action-model.md) (A1) ·
[`action/spec-action-costs.md`](../architecture/action/spec-action-costs.md) (A3) ·
[`action/spec-targeting.md`](../architecture/action/spec-targeting.md) (A2) ·
[`action/spec-usability-conditions.md`](../architecture/action/spec-usability-conditions.md) (A4) ·
[`action/spec-battle-board.md`](../architecture/action/spec-battle-board.md) (A10, owner-deferred).

---

## 0. The one rule that shapes every component in this document

> **With no board, every range check passes. Not an error, not an empty result — passes.**
> [(A2 §4)](../architecture/action/spec-targeting.md)

The battle grid (A10) is explicitly **deferred by the owner** — *"built with the board map, not wave
1"* — yet the range *parameters* (`MinRange`, `MaxRange`, `RangeChannel`, `AnchorSource`) are authored
from day one, *"because range is not retrofittable."* Every component below inherits this: it must
render correctly **both** with a board and without one, and the no-board state is not a fallback to
apologise for — it's what keeps the basic attack byte-identical while the grid doesn't exist yet.
**One exception is loud, not silent**: `Mode = Area` needs cells to enumerate, so an area action is
**rejected at bind time** while no board exists, following the atom program's own precedent.

---

## 1. A20 — the action card

The shape, sealed in [action-map.md §10.6](../architecture/action-map.md):

```text
action = envelope (when) + container of atoms (what) + target rule (who) + costs + usability
```

**The membership rule, which is what makes "is X an action" a test instead of a meeting:**

> *"Anything an actor does that interacts with the environment or itself, costs resource or time, and
> needs a cooldown, is an action. No exception."*

This is why **summon** is an action (costs `soul`/`sun`, targets a cell, has a cooldown — *"the game's
core verb"*) and a passive trait is not (the actor doesn't *do* it — no cost, no cooldown). The card
must be able to render **any** member honestly: attack, skill, summon, move, block, pass — one shape,
not five bespoke ones.

**Timing fields exist on `rpg_action` but the card should render only what the player needs**:
`windup_ticks`, `resolve_offsets_json`, `recovery_ticks`, `commitment`, `interruptible`,
`priority_band` are runtime/kernel fields the card doesn't need to expose raw — it renders their
*consequence* (a cast bar, an interrupt window), never the column names.

---

## 2. A21 — the cost cluster

Reuses [spec-derived-stat-sheet.md](spec-derived-stat-sheet.md)'s five-resource registry directly — no
new resource model, this module *spends* the pools that already exist:

| id | plant label | zombie label | exhaustion |
|---|---|---|---|
| `hp` | HP | HP | none — depletion is death |
| `stamina` | Stamina | Stamina | ✅ |
| `hunger` | Sun | Hunger | ✅ |
| `spirit` | Spirit | Spirit | ✅ |
| `qi` | Yang | Yin | ✅ |

**An action costs a *list*, not a single pool** — `rpg_action_cost` is `(action_id, resource_id,
amount_spec, when)`, so the cluster must render multiple cost chips per action, each in the resource's
own faction label.

**`when` changes what the cluster shows mid-action:**

| `when` | Behaviour | Cluster consequence |
|---|---|---|
| `onCommit` (default) | the whole cost paid at commit | one cost line, paid or refused, no mid-action state |
| `perTick` | one payment per resolve offset — **running dry ends the action** | a channel-style bar, draining per tick, with a visible "will end early" state once a pool projects to empty before the last offset |

**Committing is what costs, not landing** — an interrupted, fizzled, or missed action has all paid.
The cluster never shows a refund for a missed attack; that would contradict the one rule with no
exceptions that keeps slot accounting identical on every exit path.

**Atomic, and asserted per pool, not in aggregate.** *"An action that consumed stamina and then found
no spirit must leave the actor exactly as it found them"* — the cluster's failure state must name
**which** pool blocked the action, never a generic "cannot afford."

---

## 3. A22 — the targeting preview

**This module does not build targeting** — `TargetResolver`, `BoardSnapshot`, and `CombatPolicy` ship
and work today. What the UI needs is the **authoring contract**, because it decides what a preview can
honestly show.

### The two things a caster-relative action needs that board-anchored effects never did

**`Relation`, not absolute side.** An action says `Enemy`, never `"plant"` — compiled to two concrete
`TargetSpec`s (one per caster side) so one authored action serves both factions without drift. The
preview renders `Enemy` / `Ally` / `Self` / `Any`, never a raw faction string.

**Range is Chebyshev**, `distance = max(|Δcol|, |Δrow|)` — not arbitrary: the shipped `Square` area
shape of size *n* **is** a Chebyshev ball of radius `(n−1)/2`, so this is the metric the existing shape
code already implies. The preview's range ring must be drawn as a Chebyshev diamond-square, never a
circle — a circular range indicator would visually promise cells the engine doesn't grant.

### The no-board state, rendered honestly

Per §0: **without a board, the range gate passes everything** — the preview shows *"in range"* for
every candidate, correctly, not as a placeholder. An `Area`-mode action is the loud exception: it's
**unusable, bind-time-rejected**, and the card must show this as a real disabled state (§4), never a
silently-empty target list.

**`MaxTargets` is capped** by the same `CombatPolicy.ResolveMaxTargets` the resolver already enforces
— the preview's target count never exceeds what the resolver could actually return, so a UI showing
"5 targets" for a cap-of-3 action would be lying about what commit produces.

---

## 4. A23 — usability: the GG-55 case, specified and now drawn

Five ordered gates, cheapest-first, stopping at the first refusal:

| # | Gate | Refuses as |
|---:|---|---|
| 1 | Bound at all | `NotBound` |
| 2 | Cooldown | `OnCooldown` |
| 3 | Affordability (§2) | `CannotAfford(resourceId)` |
| 4 | Range (§3) | `OutOfRange` / `TooClose` |
| 5 | Condition (a compiled predicate, closed leaf list) | `ConditionFailed` |

**Refusal is typed, never a boolean** — the exact GG-55 requirement (*disabled, never without saying
why*) arriving from the runtime rather than a button. `CannotAfford(resourceId)` names **which** pool;
`OutOfRange` and `TooClose` are distinct because a minimum and a maximum are different problems for a
player to solve. **The action bar renders the refusal reason, not a generic greyed state** —
*"not enough spirit"* on the button, not a bare disabled look.

**Two things caught building the plate, both real.** First: a 62px action slot cannot fit a full
sentence (*"needs an ally below half HP"*) without its label bleeding into the neighbouring slot —
measured directly, not assumed. The fix is a **short typed label under the button** (`Cooldown`,
`No qi`, `Too far`, `Unmet`) plus the **full typed reason in an adjacent legend** — the type name is
what the player needs at a glance, the sentence is what they need on inspection, and squeezing both
into 62px was asking one space to do two jobs. Second: this foundation plate loads only
`tokens.css`/`kit.css`, not `screens.css`, so `.actionslot`'s own `position: relative` — needed for the
refusal label's `position: absolute` to anchor correctly — was silently absent, and the label computed
against `<body>` instead of the button, landing thousands of pixels off-screen. Restated in `kit.css`
with a comment explaining why, rather than silently patched.

**Two refusals are worth distinguishing in the UI because they mean different things to a player
deciding whether to wait**: `OnCooldown` and `CannotAfford` become true with time — worth watching;
`NotBound` never does — not worth a retry indicator.

**Position-dependent conditions with no board must read `false`, never throw.** A condition checking
`RowIs`/`ColIs` with no board present is quietly unusable, not a crash — the same honest-quiet
discipline the range gate uses. The action bar shows this identically to `ConditionFailed`: the player
sees "not usable right now," not a stack trace.

---

## 5. A24 — the battle grid and range bands

**Deferred by the owner, specced ahead because documents reconcile cheaply and code doesn't** — A2,
A7, and A9 all carry parameters that stay inert until this lands. What the plate can draw honestly is
the **shape** of the grid, not a working implementation:

- **2-D, sized per encounter from the seeded generator, bounded** — random dimensions are part of the
  determinism surface, reproducible from `(setup, seed)`, and the random *range* is itself a stated
  interval, never an unstated "random."
- **One actor per cell.** Destination-free moves, blocked-line refusal, and — the one that matters
  most for the summon verb — **spawns need a free cell**, including the *"nowhere to land"* case,
  which the game's most-used action hits directly, not as an edge case.
- **`BoardSnapshot` is the entire integration surface** — `{Ptr, Side, TypeId, Col, Row,
  MindControlled, Living}`, nothing lawn-specific about it despite the comment calling it a *"frozen
  lawn census."* So a battle grid builds a snapshot and **the whole targeting stack works unchanged,
  `Area` included** — no second targeting path, no resolver change.

**Two range bands, drawn distinctly, because they answer different questions**: *"can I reach this
target"* (attack/skill range, `MinRange`/`MaxRange`) versus *"can I stand here"* (`move.range`). A UI
overlaying both as one colour would conflate *where I can act from* with *where I can act on*.

---

## 6. Guards

| # | Guard | Fails when |
|---|---|---|
| 1 | **The action card renders one shape for every membership-rule member** | attack, skill, summon, move, block, and pass each need a bespoke layout |
| 2 | **A cost cluster names which pool blocked the action**, never a generic refusal | a player can't tell whether to wait for stamina or spirit |
| 3 | **`perTick` costs render as a draining bar with an "ends early" state**, `onCommit` costs render as a single paid/refused line | a channelled cost and a flat cost look identical |
| 4 | **A range indicator is drawn as a Chebyshev diamond-square, never a circle** | the UI promises cells the engine won't grant |
| 5 | **With no board, targeting shows real "in range" results, not a placeholder**; an `Area` action shows a real disabled state, not an empty list | the no-board case reads as broken instead of correct |
| 6 | **Every usability refusal is typed and named on the button**, never a bare disabled look | GG-55 (never disable without saying why) is violated at the one surface it matters most |
| 7 | **A position-dependent condition with no board reads as `ConditionFailed`, never a crash** | the honest-quiet discipline breaks at the UI layer even though it holds in the engine |
| 8 | **Attack range and move range render as visually distinct bands** | "can I reach it" and "can I stand there" are conflated |

---

## 7. What this document deliberately does not draw

- **A working battle grid.** A10 is owner-deferred; the plate shows the *shape* the grid will have
  (bands, snapshot integration) so A2/A7/A9's parameters read as real rather than inert, not a
  functioning board.
- **AI behaviour.** A7's stub (*"pursue nearest, act to kill"*) reads usability results; it has no UI
  of its own.
- **Real action content.** Every example is the spec's own illustrative shape, not authored data —
  none exists yet.

---

## 8. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — the action layer, targeting, costs, usability, board.
[x] I read every doc in the §1 row(s) this session: action-map.md §10.6, action/spec-action-model.md,
    action/spec-action-costs.md, action/spec-targeting.md, action/spec-usability-conditions.md,
    action/spec-battle-board.md.
[x] I checked decisions.md for a lock covering this (Game GUI row; decisions.md:90 on battle-mode-only
    actions, already cited in document 6).
[x] Every factual claim cites file:line or a document section — mostly the latter, since no code
    exists yet for this program to cite file:line against.
[x] I verified claims against CODE where any exists — the resource registry and channel-family claims
    reuse document 9's already-verified DerivedStatChannels.cs findings rather than re-deriving them.
    The spec's claim that targeting infrastructure ships was re-checked directly this session, not
    taken on the spec's word: TargetResolver.cs, BoardSnapshot.cs, CombatPolicy.cs all exist under
    src/FusionRpg.Core/Combat/, TargetSpec is a real class at
    src/FusionRpg.Contracts/CombatDtos.cs:45, and CombatPolicy.ResolveMaxTargets exists at :30.
[x] I read the surrounding section of every rule I quoted.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL, and more so than any prior document:
    rpg_action has zero hits in src/, so almost nothing here can be tested against running code. Every
    claim is a design-document citation. This is stated plainly in the document header, not just here.
[x] Nothing contradicts a §2 invariant — battle-mode-only actions, standalone-first, all held.
[x] Corrections propagated — no correction was needed; the source specs' own citations (TargetSpec
    etc.) were taken as stated rather than re-verified, which is itself the honesty gap named above.
```
