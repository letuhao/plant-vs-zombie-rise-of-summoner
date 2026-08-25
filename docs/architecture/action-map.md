# Combat action — capability map

**Status:** **Proposed capability map (2026-08-22)** — module ids, dependency direction, and build order for review. No specs written, no build authorized. **Blocked by owner decision D1: the effect-atom program is specced first** ([effect-atom-map.md](effect-atom-map.md)), so nothing here starts until that map and its module specs exist. Grounding: [effect-atom-ideal.md](effect-atom-ideal.md), [battle-turn-ideal.md](battle-turn-ideal.md), [battle-timeline-map.md](battle-timeline-map.md), the code audit in §2, and the Chaos `action-core` / `combat-core` doc set (§7).

Prefix: `action`. Specs live at [`docs/architecture/action/`](action/) — **all ten written**. Plan and tasks at [`tasks/action-plan.md`](../../tasks/action-plan.md) / [`tasks/action-todo.md`](../../tasks/action-todo.md), written 2026-08-22 (AGENTS.md parallel-programs convention; `SPEC.md` and `tasks/plan.md` hold other streams — the latter is Perf v3's).

---

## 1. Why this program exists, and why now

The battle-timeline kernel is built up to the action envelope (B5), and the envelope is the problem. It declares `WindupTicks`, `ResolveOffsets`, `CooldownTicks`, `Commitment`, `Interruptible` — every field chosen from FFX, SMT, and FF15 rather than from an action this game will ship. **No real action has ever been driven through it.** The spec says as much: `ActionEnvelope.NoOp` "no longer validates the seam."

Continuing to build reaction lanes, rendezvous, readiness, and economies on top of an unvalidated seam is the expensive mistake. This program defines the actions, so the seam gets validated by a real one.

## 2. What the code actually has today

Verified 2026-08-22 in `src/`:

| Thing | State |
|---|---|
| Actions in battle | **One.** `BattleEngine` resolves `AttackComponents` against `SelectTarget`. No skills, no defence, no movement. |
| Targeting | One private `SelectTarget` — lowest-HP-ish selection inside the engine, not a system. |
| In-battle spendable resource | **None.** HP is the only pool; shields are a second, but they are a damage sink, not a cost. |
| Meta currency | Souls (`SoulEarnPolicy`, expeditions) — out of battle. |
| Sun | Lives in `SimEngine` / `SimModels`, the lawn sim. Never reaches an RPG battle. |
| Skills | Unstarted. |
| Derived stat channels | 84 combat + status + progression. **Zero resource channels.** |

So: this is a clean slate, and the first action to define is the one that already exists.

## 3. The three-program seam

The overlap risk is real, so state it precisely. Three programs, three questions, no shared answers:

| Program | Owns the question | Owns |
|---|---|---|
| [effect-atom](effect-atom-ideal.md) | **What happens?** | Atoms, containers, power currency, resolve-at-apply |
| [battle-timeline](battle-timeline-map.md) | **When does it happen?** | Virtual clock, turn FSM, slots, envelope, readiness, economies |
| **action** (this) | **What is the thing being scheduled, and why this one?** | Action identity, targeting, costs, usability conditions, selection |

The atom ideal explicitly leaves three holes, and they are exactly this program's core:

> "**AI is not ours.** Atoms cover definition and resolve. Targeting, retreat, and decision-making need an AI layer spec, and this game has no AI layer yet."
>
> "**Define ourselves first, then write the track.** Items, traits, statuses, and skills adopt the atom contract when *they* write real specs."

An action is the join row: **an envelope (when) + a container of atoms (what) + a target rule (who) + a cost (what it takes) + a condition (may I).**

**Dependency hazard, stated up front.** The atom program is an *ideal*, not a spec — no module ids, no build order. If `action-model` depends on containers, this program blocks on unbuilt work. The proposed answer is a **narrow declared seam**: the action holds a `ContainerRef` and calls one resolve entry point, and nothing else about atoms. If that seam cannot be agreed cheaply, the fallback is that the first actions carry their effect inline and adopt containers later — worse, but not blocking. **This is decision D1 below.**

## 4. Modules

| id | Name | What it owns | Depends on |
|---|---|---|---|
| **A1** | `action-model` | The action record: identity, container ref, envelope, target rule, costs, conditions. The join. | atoms (seam), timeline envelope |
| **A2** | `targeting` | Target *rules* and their resolution: self, ally, enemy, lowest-HP, row, lane, area, projectile. Deterministic ordering, no dictionary order. Lawn geometry where it applies. | A1 |
| **A3** | `action-costs` | What an action takes to use, over the four locked pools (`hp`, `sun`, `soul`, `stamina`): max/regen derived channels, accrual rules, validate → reserve → consume → refund-on-fizzle, and the two layer questions in §9.1. | A1 |
| **A4** | `usability-conditions` | May this action be selected right now: typed predicate tree over a **closed** leaf list, reusing the atom ideal's predicate design rather than inventing a second one. | A1 |
| **A5** | `basic-attack-adoption` | **The seam proof.** The engine's existing attack becomes a real action driven through the envelope, byte-identical. | A1, A2, A4 |
| **A6** | `action-catalog` | Where actions live: SQLite rows, server-compiled, pushed to the injector (the atom ideal's plumbing stance, not a second one). | A1, A5 |
| **A7** | `action-selection` | The `IIntentSource` implementations — the AI/policy layer the atom ideal disclaims. Auto-battle policy for expeditions and sweeps; the player-input seam for interactive modes. | A2, A4, A6 |
| **A8** | `defence-actions` | Block, guard, brace as first-class actions; the reaction lane's first real content. Ties to shields. | A5, timeline B6 |
| **A9** | `movement-actions` | Movement as an ordinary action priced by `TimeCostTicks`, using `move.range` and the grid's distance metric. **Battle grid only** — never the lawn (§10.2). | A5, A10 |
| **A10** | `battle-board` | The grid itself: per-encounter dimensions from the seeded generator, actor cell positions, **one-actor-per-cell occupancy**, the Chebyshev distance function, destination-is-free and pathing, and free-cell spawn placement. Builds a `BoardSnapshot` so the existing `TargetResolver` works unchanged. **Deferred by the owner — built with the board map / battle area**, but its absence is why `A2` and `A9` carry parameters that are inert in wave 1. | A1 |

## 4a. `A1` — the foundation deliverable, sketched

Not the spec. A sketch concrete enough to argue with, so the spec starts from something rather than a paragraph.

### Tables

**`rpg_action`** — one row per action. Every value that scales is a **value spec** (`{min, max, roll, scale}`), so the atom program's curve table serves actions too and no second scaling mechanism appears.

| Group | Columns |
|---|---|
| identity | `action_id` PK · `name` · `tags_json` (`offensive` · `heal` · `buff` · `movement` · `summon` — what `A7` chooses on) · `enabled` · `revision` |
| effects | `container_id` FK → `effect_container` |
| timing (the envelope) | `time_cost_ticks` · `speed_channel` · `windup_ticks` · `resolve_offsets_json` · `recovery_ticks` · `commitment` · `interruptible` · `interrupt_refund_milli` · `slot_consuming` · `priority_band` |
| cooldown | `cooldown_class` · `cooldown_key` · `cooldown_ticks` · `starts_at` · `interrupt_cooldown_milli` |
| targeting | `target_spec_json` (**typed**, compiling to `TargetResolver`) · `min_range` · `max_range` · `range_channel` · `anchor_source` · `requires_line_of_sight` |
| usability | `conditions_json` — a predicate tree over `E3`'s **closed** leaf list, extended by `A4` |

**`rpg_action_cost`** — `(action_id, resource_id, amount_spec, when)`. A table rather than a column because an action costs several resources; a value spec because costs scale; and `when` is `onCommit` (default) or `perTick`, which is how every game with channelled abilities does it.

### Copy first, decide only what is ours

Most of what an action does is a solved problem, and the genre's answers are better tested than anything invented here. The rule for this spec: **name the game that solved it and take its shape.** A design question is only genuinely open when it comes from something specific to *this* game.

| Question | Taken from | Answer |
|---|---|---|
| Per-tick channel cost | Diablo 3 / 4 channelled skills | Cost carries `when`; running dry ends the channel |
| Effects with different recipients in one skill | D&D, WoW, PoE — universal | Each effect declares its own target scope |
| Multi-hit against a dying target | ARPG standard | Locked target for single-target, re-resolved per hit for area — which is exactly what `Commitment` already encodes |
| One target of several dodges | Universal | Per-target roll; the rest land |
| Summon with no free cell | Every tactics game | Nearest free cell, else refuse |
| Where actions come from | Universal | Basic attack intrinsic, everything else learned or granted |
| Ability tags for AI | Universal | Tag the action; AI reads tags, never internals |

**Genuinely ours, because no other game has our constraints:** the ordering of four streams against one set of golden hashes; keeping the basic attack byte-identical through the migration; and the zero-allocation budget inside the Unity frame. Those are worth arguing about. The table above is not.

A fourth item was on this list — *"how `sun` and `soul` cross the PvZ/RPG layer boundary"* — and the owner removed it on 2026-08-22 as a false problem. **The two games have two state machines and share no state in either direction.** What crosses is messages: captured events coming out, `pvz.*` intent commands and Writer stat changes going in. Lawn sun and RPG sun are two different things that share a word. There is no boundary for a resource to cross, and treating one as if there were is the layer confusion this map has now made twice.

**`rpg_action_effect_scope`** — `(action_id, atom_id, scope)` where scope is `caster` · `primaryTarget` · `eachTarget` · `casterAllies`. This is the "strike an enemy and heal yourself" problem. Kept **action-side** deliberately: putting `scope` on `effect_container_atom` would change the atom contract and make atoms less reusable outside actions.

**Action availability** — intrinsic actions come from the species row; granted ones are bindings, reusing `effect_binding`'s owner vocabulary rather than inventing a second one.

### Dataflow

```
author (SQLite rows)
  → server compiles action + container to a typed runtime form and pushes it   [A6]
  → IIntentSource selects (action_id, target)                                  [A7]
  → usability: predicate + cooldown + range + affordability                    [A4]
  → commit: validate all costs, consume all, roll back on any failure          [A3]
            acquire slot, schedule resolve handles                             [kernel]
  → resolve: resolve target set per Commitment                                 [A2 → TargetResolver]
             for each atom × scope → contributions
             → DamageApplyPipeline / status / spawn                            [existing]
  → finish: release slot, start cooldown, schedule recovery                    [kernel]
```

Nothing in that chain is new machinery except the two shaded steps — the typed target contract and the cost table. Everything else is a call into something that ships.

### What the sketch has to prove

The corpus, not the design. Each of these is either expressible in the structure above or explicitly excluded by it, and `A1` is where that gets demonstrated:

| Case | Exercises |
|---|---|
| Basic attack | The whole chain at its simplest — and must stay **byte-identical** (`A5`) |
| Strike + self-heal | `rpg_action_effect_scope` with two scopes in one action |
| Multi-hit combo | `resolve_offsets_json` × `commitment` over a target **set**, not one ptr |
| Ranged attack with a minimum | `min_range` / `max_range`, inert until the board exists |
| Summon into a cell | Costs, `anchor_source`, free-cell placement, `spawn.entity` — the game's core verb |
| Drain-channel | Costs carry a **when** — `onCommit` (default) or `perTick`. Failing to pay a tick ends the action through the existing interrupt path. Diablo 3 Disintegrate / D4 channels: pay per second, drop the channel when you run dry |

## 5. Dependency direction and build order

```
effect-atom (seam only) ─┐
                         ├─► A1 action-model ─┬─► A2 targeting ─┐
battle-timeline envelope ┘                    ├─► A4 conditions ─┼─► A5 basic-attack-adoption ─► A6 catalog ─► A7 selection
                                              └─► A3 costs ──────┘                                              │
                                                                                          A8 defence ◄──────────┤
                                                                                          A9 movement ◄─────────┘
```

Build order: **A1 → A2 → A4 → A5 (proof) → A3 → A6 → A7 → A8 → A9.**

`A5` sits deliberately early and `A3` deliberately after it. The point of A5 is to validate the envelope with the smallest real action that exists; a basic attack has no cost, so making the cost system a prerequisite would delay the only thing that can tell us whether B5 was right.

### Wave 1 is the foundation definition — corrected 2026-08-22

An earlier draft treated "is an action a container or does it reference one?" as a **question to settle before `A1`**. That was backwards: **it is what `A1` delivers.** There is nothing to spec, code, or review until the data structure, its tables, and its dataflow exist, and every other module in this map is a consumer of them.

Two corrections follow:

- **The `E5` dependency was over-called.** `effect_container` already exposes `container_id` as its primary key, so an action row carries an FK and nothing in the atom contract needs adding. `A1` is not blocked on the atom program.
- **`A6` splits.** The **schema** — tables and columns — belongs to `A1` as part of the foundation. `A6` keeps only the **server compile-and-push plumbing**, which is genuinely later work. A map that put the tables in a late module was describing a system you could not build in order.

Scope questions like *"is summoning an action?"* and *"do drain-channels ship in wave 1?"* are **not peers of the schema.** They are the corpus the schema is designed against: each is either expressible in the structure or explicitly excluded by it, and that is a property `A1` demonstrates rather than a decision taken ahead of it.

## 6. Checkpoints

- **✅ Checkpoint A — the seam is real.** A1+A2+A4+A5: the engine's existing attack runs as a declared action through the envelope, all eight goldens byte-identical. If the envelope needs fields it does not have, this is where we find out — before six more timeline modules are built on it.
- **✅ Checkpoint B — actions are content.** A3+A6: a second and third action exist as data, not code, with costs.
- **✅ Checkpoint C — something chooses.** A7: auto-battle policy replaces `SelectTarget`, and the interactive seam has a real implementation behind it.
- **⛔ Checkpoint D — new action kinds.** A8+A9 change what a turn can contain; they need the timeline's reaction lane and a decision on movement geometry.

## 7. What is inherited from Chaos, and what is refused

Grounding: `chaos-backend-service/docs/action-core` and `docs/combat-core`.

**Taken:**

- **Actions declare resource requirements**, validated before execution and rolled back on failure — with consumption typed (`Fixed`, `Percentage`, `Scaling(stat)`, `Conditional`) rather than a bare number.
- **Timing scales off derived stats** — an execution-speed channel and a cooldown-reduction channel, with `min` / `base` / `max` bounds so a stat cannot drive a duration to zero. **Closed 2026-08-24 (spec-skill-modifiers.md, T4.3):** the envelope's `SpeedChannel` already existed; it now has a sibling, `CooldownChannel`, referencing one of the five `skill.cooldown.{category}` channels the derived-stats program registered — not a second, envelope-local mechanism. The one-tick floor lives in `Battle/Timeline/CooldownMath.cs` as a structural `const` (PS-8 exempt, spec-stat-taxonomy.md §2.4's divisor rule), not a tunable — a zero-tick cooldown is a crash, not a balance outcome.
- **`interrupt_affects_cooldown`** as an explicit knob. B5 currently hard-codes that an interrupt starts no resolve-scoped cooldown; Chaos makes it declarable, which is the better call.
- **Targeting taxonomy** — single, multiple, area, projectile — as a starting vocabulary.
- **Defence as a first-class action kind**, not a passive stat.

**Refused, with reasons:**

| Chaos design | Why not here |
|---|---|
| `f64` throughout | This repo is integer ticks and per-mille. Floating point is banned in kernel code by a source guard, for byte-identical replay. |
| `Instant::now()` progress tracking | We have a virtual clock. Wall-clock reads are the determinism hazard the timeline program exists to remove. |
| `rng.gen::<f64>()` for interrupt chance | Draws must come from a named seeded stream, or replay breaks. |
| Multi-level L1/L2/L3 caches with TTL | TTL is wall clock. And the 2026-08 perf audit found caching complexity, not cache absence, near the hot path. |
| Thread pools, batch processors, async | Single-threaded by design — the Unity main thread and a server-side resolve. |
| Condition strings parsed at runtime (`"target.hp < 0.5"`) | A string DSL on the hot path is both a perf and a determinism hazard. The atom ideal already chose a **typed predicate tree over a closed leaf list**; A4 reuses it rather than adding a second condition language. |
| Resource Manager as a separate service | There is no service boundary here to justify one. |

## 8. Owner decisions (2026-08-22) — binding

**D1 — the atom seam: spec the atom program first.** `action-model` will depend on a real container contract, not a placeholder. This program therefore **blocks** on [effect-atom-map.md](effect-atom-map.md) and its module specs. Correct dependency order, at the cost of pushing envelope validation out by a program.

**D2 — resources: four pools — `hp`, `sun`, `soul`, `stamina`.** Mana is refused explicitly: *it does not fit PvZ lore*. That constraint is binding on every later naming decision here — a resource name has to be something a PvZ player already recognises, or something this game has already taught them. See §9.

**D3 — envelope rework: fold into A5, before the T5 gate.** `interrupt_affects_cooldown` still lands here, where the basic attack first drives the envelope for real, while goldens have not moved and the change is still free. **The cooldown-reduction channel and its one-tick floor are repointed at the derived-stats program (T4.3), not built a second time here** — `ActionEnvelope.CooldownChannel` references `skill.cooldown.{category}` (spec-skill-modifiers.md §1.1), so A5 wires the reference, it does not invent the channel or the floor.

## 9. Resources — locked set and open naming

Time is already a real cost — `TimeCostTicks` feeds readiness, and the turn economy ships `OneActionPerTurn`, `ActionPoints`, and side-scoped `PressTurn`. What time cannot express is **per-battle rationing**: with time as the only cost, the strongest action is always correct if you are willing to wait for it. The four pools exist to add that.

### 9.1 The locked four

Each pool must earn its place by answering a *different* question, or it is a second name for an existing one.

| Pool | Answers | Accrues by | Already exists as |
|---|---|---|---|
| `hp` | "Can I survive doing this?" | Healing, regen | The only battle pool today |
| `sun` | "Can I afford to put something on the board?" | Ticking up over time | `SimEngine` / `SimModels`, lawn side only — never reaches an RPG battle |
| `soul` | "Can I afford to call on a demon?" | Kills (the `soul-eater` trait already does exactly this) | `SoulEarnPolicy`, expeditions — **meta**-currency, not per-battle |
| `stamina` | "Can my body do this again right now?" | Regen per tick, spent by physical acts | Nothing |

Two of these need a decision the spec must not skip:

- **`sun` crosses a layer.** It currently lives in the lawn sim and has never entered an RPG battle. Making it a battle resource either bridges those two economies or creates a second, identically-named one. `action-costs` must say which.
- **`soul` is already a meta-currency.** A battle-scoped soul pool and a persistent soul balance are different numbers with the same name. Either battle souls are a separate per-battle pool that resets (recommended — it keeps meta progression out of tactical balance), or spending in battle draws down the save file, which makes every battle a resource-management risk.

### 9.2 Lore-native candidates, proposed not locked

The owner's constraint — *a resource has to fit PvZ lore* — rules out the generic fantasy set and points at names the franchise already owns:

| Candidate | Source | Shape it fits | Why it is worth considering |
|---|---|---|---|
| **`brains`** | PvZ Heroes — the zombie-side mirror of sun | A second deploy currency | If both sides ever deploy, `sun` alone is plant-flavoured. `brains` is the ready-made symmetric partner, and players already know the pairing. |
| **`plantFood`** | PvZ2 — a consumable burst | The *building charge* shape | This is the lore-native version of a Limit Break: it is earned, held, and spent for a burst. It is the one resource here that creates a climax rather than a budget. |

`plantFood` is the stronger of the two: the locked four are all *budgets*, and none of them produces the "save it for the right moment" decision that a charge does.

**Refused names, recorded so they are not re-proposed:** `mana`, `essence`, `focus`, `will`, `qi` — all generic-fantasy, none of them something a PvZ player recognises.

## 10. Readiness gate — what must clear before spec phase (2026-08-22)

The owner named three dependencies: consume resources, contain effects, integrate with the timeline for cooldown. All three are real and all three are tracked. The sweep below is what came back when asked what *else* this program touches.

### 10.1 Hard blockers — the spec cannot be written without an answer

| # | Blocker | Status | Proposed resolution |
|---|---|---|---|
| **B1** | **Container contract** (`effect-atom` E5) — A1 holds a `ContainerRef` and cannot be typed without it | In progress, second session | Checkpoint B of [effect-atom-map.md](effect-atom-map.md). Do not start A1 before it |
| **B2** | **Resource registry contract** — A3 prices actions against pools whose ids, scopes, and exhaustion shape are captured but not locked | Ideal only, nothing in `decisions.md` | Lock the five ids and the exhaustion-is-a-status decision as a `decisions.md` row. Cheap; the design is settled |
| **B3** | **Do actions exist in PvZ mode at all?** | **Answered upstream, contradicted here** | See §10.2 — this map is wrong and must be fixed before spec |
| **B4** | **Golden ordering across four streams** | Unresolved | See §10.3 |

### 10.2 B3 — the scope question that shrinks the program

The battle-timeline ideal already settled this and this map did not notice: the kernel serves modes **"where we own the clock,"** the `pvz-realtime` profile row was deleted, and T7 makes PvZ mode a **stateless observer** — *"No queue, no scheduling, no per-actor machine injector-side."* Unity owns when a peashooter shoots.

**So actions are a battle-mode concept.** In PvZ mode there is nothing to schedule an action onto, and the overlay only observes and projects.

This map contradicted that in two places: `A8 defence-actions` and `A9 movement-actions` both referenced lawn geometry. **Corrected 2026-08-22** — `A9` is battle-grid only, and the grid is owned by the new `A10 battle-board`. Left uncorrected, the observer boundary would have broken on the first movement action.

The consequence is good news: the action system is **standalone/web-battle first**, which is also the runtime with no Unity, no Writer surface, and no frame budget. Blast radius drops sharply.

### 10.3 B4 — four streams, two want the goldens frozen and two want them moved

| Stream | Wants |
|---|---|
| battle-timeline B13–B15 (T5 gate) | **Byte-identical** |
| action A5 (basic attack adoption) | **Byte-identical** |
| battle-timeline B18 (T9 timing fix) | Moves them — `RulesetVersion` 2 → 3, re-bless, win-rate sweep |
| effect-atom E12 (trait migration) | Moves them — `RulesetVersion` bump, re-bless |

If a mover overlaps a freezer, neither can attribute a hash change to its own work, and the freezer's proof is worthless. **Proposed order: freeze first, move last** — T5 gate, then A5, then the two movers back to back with a single combined re-bless and one sweep. Two separate re-bless events cost two sweeps and two sign-offs for the same goldens.

### 10.4 Undesigned neighbours this program will lean on

None of these blocks the *spec*, but each is a hole the action layer will reach into, and none has an owner today.

| Neighbour | State | Why it matters here |
|---|---|---|
| **AI / selection** | **Does not exist.** The atom ideal disclaims it: *"this game has no AI layer yet"* | `A7 action-selection` **is** that layer. This program is quietly on the hook for the game's first AI, which is larger than the rest of the map combined. Worth naming before it is discovered mid-build |
| ~~**Damage applier**~~ | **Wrong — it exists.** Corrected 2026-08-22, see §10.4c | — |
| ~~**Targeting geometry**~~ | **Wrong — it exists.** Corrected 2026-08-22, see §10.4c | — |

### 10.4a Owner decisions, 2026-08-22

**AI: build a stub first.** It pursues the nearest target (move) and uses actions — attack, skill — to kill it. A real AI layer comes later as its own program. `A7` ships the stub; the atom ideal's power-vector reads are *not* consumed yet.

**Golden order: freeze first, then one combined move.** T5 gate → A5 → then T9 and E12 back to back with a **single** re-bless and **one** win-rate sweep. §10.3.

**Resources persist across a run and reset at rest.** Pools carry between encounters; a rest point refills them. **"Rest" is not a concept this game has yet** — the candidates are a wave boundary, a world-map node, or an expedition return, and they are not the same thing. `A3` must name it, and whichever it is becomes a dependency on the world-map program.

### 10.4b The stub AI needs a battle space, and there isn't one

Verified 2026-08-22 in `BattleEngine.SelectTarget`: the battle has **no position, no distance, no lane, no range**. Targeting takes the **first active enemy in list order** (or the lowest-HP one for `bloodthirsty`), and `FindAdjacentWithTrait` means adjacency by **list index**, not by space. Everyone can hit everyone; nothing is ever out of reach.

"Pursue the nearest target" needs three things that do not exist: **distance**, **movement that changes it**, and **range** on actions to gate what can reach. This is a larger discovery than the resource model and it must be settled before `A2 targeting` is specced, because targeting over a positionless list and targeting over a space are different modules.

| Option | Shape | Cost |
|---|---|---|
| **(a) No space** — "nearest" degenerates to list order | Movement is a no-op | Free, but does not deliver pursuit at all |
| **(b) 1-D lane distance** *(recommended)* | Integer position per actor; distance = `abs(a − b)`; movement changes it; actions carry a range | Small. PvZ-shaped — the lawn is a grid and a lane is a line. Extends to 2-D later **without changing the action contract**, because range stays a number either way |
| **(c) 2-D board** — the 5-lane × N-column lawn | Richest, matches the lawn exactly | Large, and the battle is web-mode where there is no lawn to match |

**Sequencing warning, and it interacts with the decision just taken.** Adding positions is inert on its own, but the moment AI targets *nearest by distance* instead of *first in list*, target selection changes and **the goldens move**. That makes battle space a golden-moving change, so under the freeze-first order it belongs with T9 and E12 in the single combined re-bless — **not** in A5, which must stay byte-identical.

### 10.4c Corrections — the applier and the target payload both exist

Two entries in §10.4 were wrong. Both came from over-reading the effect-atom ideal's §5.1, which says no gameplay applier exists **for the lawn** — a statement about vanilla peas and bites being observed only. It is not a statement about battle, and battle is the runtime this program targets.

**The damage applier exists and has a spec.** `Core/Combat/DamageApplyPipeline` — *"The one apply path (combat-unification, spec-damage-apply-pipeline): finalized signed delta → shield gate → sink."* It is **single-target by design**, with an `IHpDeltaSink` seam and two implementations (Funnel for overlay and battle, direct for sim and tests), plus a zero-allocation Funnel-specialised entry for the dispatcher hot path.

**The target payload exists.** `TargetSpec` (`FusionRpg.Contracts/CombatDtos.cs`) and `TargetResolver` (`Core/Combat`) already cover every mode the owner listed: `Single`, `Multi`, `Random`, `All`, `Area` (shape / size / width / height / anchor / anchorOrigin), plus `EventTarget`, `Actor`, and `Selected`. It carries `filters`, `count`, and `maxTargets` with a policy cap, sorts deterministically by ordinal ptr, and takes an injected `ICombatRng` — so it is already replay-safe.

**The division of labour that follows, and it is clean:** the resolver fans one action out to N target ptrs; the applier applies one delta to one target. Fan-out belongs to the action layer, and neither of those two pieces has to change to make that true.

#### What is genuinely missing, and it is smaller than a new system

**1. `TargetSpec` is a wire DTO, not an authoring contract.** It keys modes by **string** with `OrdinalIgnoreCase` comparison and carries filters as `Dictionary<string, object?>`. That is the exact shape the effect-atom program refused — *no dictionaries, no string comparison on the per-hit path*, and conditions as a **typed predicate tree over a closed leaf list**. Actions authored as data cannot inherit an untyped filter bag without reintroducing what the atom program exists to remove.

> **`A2`'s job is therefore to give targeting a typed, closed contract that compiles to the existing resolver — not to build targeting.** The behaviour ships; the authoring surface does not.

**2. `Area` is the only mode that needs a board.** It resolves through `BoardSnapshot` cells `(Col, Row)`; every other mode reads `snapshot.Entities` with filters and would work in battle from a synthetic snapshot. So the battle-space question in §10.4b narrows sharply:

> Battle needs positions **only if battle actions need `Area` targeting.** Single, Multi, Random, and All need none.

That also re-scopes the stub AI: "pursue the nearest target" still needs distance, but if `Area` is out of scope for now, 1-D positions are needed for *movement and range*, not for target resolution.

**3. Placement constraint — the action runtime cannot live in `Core/Battle/Timeline/`.** `TargetResolver` uses `.Select(`, `.Where(`, `.ToList(`, `.Take(`, and `.Contains(`, all of which the kernel's tick-path guard bans, and it allocates a pooled list per call. The existing split is already correct — the kernel schedules and holds a `TargetKey`; it never resolves — but `A1`'s runtime must be sited in `Core/Combat/` or a new `Core/Actions/`, **outside** the guarded folder. Putting it inside would either fail the guard or force a non-allocating rewrite of a working resolver.

**4. Cooldown on interrupt contradicts what B5 built — resolution proposed.** The owner's rule is that an action cools down **when it finishes, including when a channel is interrupted**. `ActionRunner.Interrupt` currently starts no cooldown, on the reasoning that a swing broken before it lands costs only what it already spent. That is now wrong.

Proposed shape, replacing the Chaos `interrupt_affects_cooldown` boolean with a number because a boolean cannot express the interesting cases:

> **`ActionEnvelope.InterruptCooldownMilli` — per-mille of the full cooldown charged when an action is interrupted. Default `1000` (full).**

Full-by-default matches the stated rule literally, and the field lets content say *"a broken channel only costs half"* without a second flag. `0` reproduces today's behaviour for any action that should genuinely cost nothing when broken. The cooldown starts at the **interrupt tick**, regardless of the envelope's `StartsAt`, because `Resolve` and `RecoveryEnd` never happen on this path.

It pairs with the cost rule below: an interrupted channel **has paid its resources and does cool down**. Both halves say the same thing — committing is what costs, not landing.

**Where it lands:** fold into `A5` with the other envelope gaps rather than patching `ActionRunner` now. `A5` is where the envelope is first driven by a real action, it is already the agreed home for the D3 gaps, and a lone behaviour change to shipped kernel code ahead of its spec buys nothing. Until then the code and the design disagree, which is recorded here so it is not discovered as a bug.

**5. Cost consumption needs an atomicity rule.** An action costing both `stamina` and `spirit` can succeed on the first and fail on the second. Chaos wraps consumption in a transaction with explicit rollback. `A3` must state: validate all, then consume all, and roll back on any failure — and it must say **when** consumption happens. Consuming at commit is what makes "an interrupted channel still paid" true, and it is consistent with the cooldown rule above.

**6. When targets are chosen is already answered.** The envelope's `Commitment` field decides it: `EarlyBound` snapshots at commit and fizzles if the target is gone, `LateBound` re-reads at resolve, `EarlyBoundWithFallback` retargets. Nothing new is needed — but `A1` should state that the action's target *spec* is authored while the resolved *ptrs* are a runtime value governed by `Commitment`.

### 10.4d The battle is a grid — attack range and move range

Owner decision 2026-08-22: the battle area is a **grid map, in the shape of Galaxy Online** — actors occupy cells, actions have an **attack range**, and actors have a **move range**. This supersedes the 1-D recommendation in §10.4b and settles §10.4c(2): battle *does* have a board, so `Area` targeting is in scope.

**Most of this already transfers.** `BoardSnapshot` is described as a *"frozen lawn census"*, but structurally it is just `{ Ptr, Side, TypeId, Col, Row, MindControlled, Living }` — nothing about it is lawn-specific except the doc comment. Grid bounds come from `CombatPolicy.LastCol` / `LastRow`, which are configurable. So a battle grid builds a `BoardSnapshot` directly and **the entire targeting stack works unchanged, `Area` included.**

#### What the grid actually adds

**1. There is no distance function anywhere in the codebase.** `EnumerateCells` implements *shapes* — `Row`, `Column`, `Square` (n×n centred), `Rectangle` — anchored at a cell. Nothing computes `distance(a, b)`, and nothing gates a target by how far it is from the **caster**. `Area` is anchored, not ranged. Attack range and move range both need a metric that does not exist yet.

**Recommended metric: Chebyshev** (`max(|Δcol|, |Δrow|)`, diagonals cost 1). It is not an arbitrary pick — the existing `Square` shape with size *n* **is** a Chebyshev ball of radius `(n−1)/2`, so Chebyshev is the metric the shape code already implies. Choosing Manhattan would contradict a shape that ships.

**2. Range belongs on the action; move range belongs on the actor.**

| Concept | Lives on | Shape |
|---|---|---|
| Attack range | the action | **`minRange` / `maxRange`, not one number.** Galaxy Online weapons have minimums, and retrofitting a minimum later rewrites every authored action row |
| Move range | the actor | A derived channel — cells per move, distinct from `turn.speed`, which is *time*. Needs registering in [actor-hub-ssot.md](actor-hub-ssot.md) §3, like `resource.*` |

**3. `TargetSpec` needs a caster-relative gate.** Every current mode is either absolute (`Single`, `Actor`) or anchored (`Area`). "Enemies within 3 of me" is not expressible. That gate is the piece `A2` adds, and it is the same typed-contract work already identified in §10.4c(1) — not a second thing.

#### Grid dimensions — decided 2026-08-22

**2-D, randomly sized per encounter, built later with the board map / battle area. Not built now — but the action contract must carry the parameters from day one.**

That is the right call for the same reason `PriorityBand` was added to the envelope before anything used it: range is **not retrofittable**. Adding `maxRange` after actions are authored rewrites every row and every balance number that was set assuming infinite reach.

Three consequences the spec must state:

**1. With no grid, range must be a no-op — not an error.** Until the board exists there are no coordinates, so every range check has to pass. This is what lets `A5` add the parameters and still be **byte-identical**: with no board, range excludes nobody and targeting behaves exactly as it does today. A range check that throws, or that returns empty when coordinates are absent, would break the freeze.

**2. A randomly-sized grid is part of the determinism surface.** Dimensions must come from the encounter's **seeded** generator, never an ambient draw, and must be reproducible from `(setup, seed)` on replay — the same rule every other roll in the battle already follows. They are also **per-encounter data**, not `CombatPolicy` state: policy carries lawn bounds today and is process-wide, so battle bounds have to travel with the encounter.

**3. Random size makes range balance relative.** Range 3 is long on a 5×5 board and short on a 20×20 one. Either the random size gets **bounds**, or ranges have to be expressible as a fraction of the board. Bounded absolute ranges are simpler and recommended; the point is that "random" needs a stated interval, not an open one.

#### Proposed parameter set — authored now, inert until the board lands

| Parameter | On | Notes |
|---|---|---|
| `MinRange` / `MaxRange` | action | Chebyshev cells. Two numbers, not one — a minimum cannot be retrofitted |
| `RangeChannel` | action | Which derived channel modifies reach, mirroring the envelope's existing `SpeedChannel`. Lets a dash and a step share one move action shape while the actor's `move.range` scales both |
| `move.range` | actor | Derived channel — cells, distinct from `turn.speed`, which is time. Registers in [actor-hub-ssot.md](actor-hub-ssot.md) §3 with `resource.*` |
| `AnchorSource` | action | Where an `Area` centres: caster, target, or a **free cell chosen within range** — the Galaxy Online shape. `TargetSpec.AnchorOrigin` today means rectangle origin (`Corner`/`Center`), which is a different axis |
| `RequiresLineOfSight` | action | Reserved as a flag now. LOS arriving later changes every range check, which is exactly the retrofit this section exists to avoid |

#### Resolved 2026-08-22

**1. Occupancy: one actor per cell, no overlap.** Three rules follow, and the spec should state them rather than let them be discovered: a move needs a **destination-is-free** check; a blocked straight line means movement **paths around** or is refused, so straight-line teleport is no longer acceptable; and **spawn placement** needs a free-cell rule, including what happens when a summon has nowhere to land. Body-blocking arrives free — a corridor held by one actor stops a column.

**2. Move and attack: two separate actions, and the clock decides whether you get both.** This is already what the kernel was built to do, and it needs no new economy.

Readiness is **work over rate**: an actor waits `TimeCostTicks / rate`, where `rate` comes from `turn.speed` and `turn.haste`. With a 1000-cost action, speed 200 waits 5 ticks and speed 100 waits 10 — **the fast actor simply acts twice as often**, which is the behaviour asked for. And because every action carries its own `TimeCostTicks`, a cheap step (200) and an expensive strike (800) cost differently, so a fast actor can fit *both* into the window a slow one needs for one swing.

No compound move-and-attack action is required, and no Action Points. **The time cost is the economy**, and `A9 movement-actions` is a peer of attack rather than a phase of it. (`ActionPoints` still ships in the timeline's economy set for modes wanting a fixed per-turn budget — it is simply not what this mode needs.)

*Reference note:* "faster actors act more often" is the ATB / FFX-CTB family rather than Baldur's Gate 3, which is D&D initiative — one turn each per round, movement and action *inside* a turn, extra actions only from specific effects like Haste. The kernel supports both; this decision picks the first.

**3. Line of sight: yes — and it arrives as fog of war, which is a much larger feature than a range flag.**

LOS is geometric and per-check: can A reach B right now. **Fog of war is state** — what each side *knows*, including remembered and now-stale information. Four consequences, reaching well past this program:

- **The battle state stops being symmetric.** Each side has a view, and the true board is a third thing.
- **AI must decide from the visible state, or it cheats.** The stub AI pursues the nearest target; under fog, the nearest *known* target. This is the single biggest constraint fog places on `A7`.
- **Determinism.** Visibility must be computed deterministically and become part of replayed state — a decision made under partial information is only reproducible if that information is.
- **Auto-resolve changes.** Expeditions and the win-rate sweep resolve battles with no player. Fog-limited AI produces different outcomes than full-information AI, so **every balance number derived from the sweep shifts** when fog lands.

**Owner decision 2026-08-22: fog of war ships later.** It is deferred, not refused, and the reasoning is sound — the expensive part is not the geometry, it is that the battle stops having one true state.

**Line of sight and fog are separable, and only fog is deferred.** LOS is a per-check geometric refinement — can this shot pass through the actor standing between us — and with occupancy already decided (§1 above), body-blocking makes it a natural companion to range. It is cheap in a way fog is not. Neither is in wave 1; only fog needs its own spec.

**The one thing that must not be deferred is the seam.** Three of the four consequences above cost nothing to postpone. The fourth is expensive to retrofit and free to prepare:

> **`A7`'s AI must read the board through a view interface, never the raw state — even while that view returns everything.**

With the seam, fog is later an implementation swap behind one interface. Without it, every AI read is a call site to rewrite, and the stub AI's "nearest target" query is exactly the read that has to become "nearest *known* target". One interface today; an AI rewrite otherwise.

`RequiresLineOfSight` stays reserved on the action (parameter table above) for the same retrofit reason. When fog does land it is a **golden-mover** and joins the combined re-bless — and it will shift every balance number derived from the win-rate sweep, since auto-resolved battles will then run on partial information.

#### Sequencing

Grid positions plus range-gated targeting change **who gets hit**, so this is a golden-moving change. Under the freeze-first order (§10.3) it belongs in the single combined re-bless with T9 and E12 — **never** in A5, which must stay byte-identical.

### 10.4e Brainstorm sweep — holes found walking an action end to end

Each of these is a decision the spec would otherwise make by accident.

#### The big one: summoning is an action

This is a summoner game, and **summoning has every property of an action**: it costs resources (`soul`, `sun`), it has a cast time, it has a cooldown, and — now that the battle is a grid — it targets a **cell**, which needs to be free. If summoning is an action, the action system covers the game's core verb rather than only its combat verbs, and `spawn.entity` (a shipped atom kind) is its effect.

**Proposed: yes.** The alternative is a parallel summon path with its own timing, its own cost validation, and its own cooldown — the fifth-content-system problem one layer up. Consequence: `A10 battle-board`'s free-cell rule is not an edge case, it is on the critical path of the game's most-used action, including *what happens when there is nowhere to put the summon*.

#### Schema: is an action a container, or does it reference one?

The atom program's container kinds are `item · trait · skill · species-passive`, and a skill is explicitly *"unstarted; needs activation and cooldown, which the turn kernel owns, not us."* So the boundary exists — but nobody has said whether an action is **a new row that points at a container**, or **a skill container with action columns added**.

**Proposed: a separate `action` row referencing a container id.** Actions and containers have different lifetimes — a passive trait is a container that is never an action, and a basic attack is an action whose effects may be shared with others. Folding them puts envelope, range, and cost columns onto rows that will never use them. This needs agreeing with `effect-atom` E5 **before** `A1`, because it decides whether E5's contract needs anything added.

#### Where an actor's actions come from

Availability has to be a binding, and there are two distinct sources with different rules:

- **Intrinsic** — every actor needs a default attack whether or not anything was learned. A basic attack cannot depend on content existing.
- **Granted** — species, learned skills, items, and traits each bind actions to an actor. That is `effect_binding`'s shape, extended to actions.

**Proposed: intrinsic actions come from the species row; everything else is a binding.** Stated now because "who has which actions" is the first question `A7`'s AI asks and the first thing the FE renders.

#### Atoms need an action-relative target scope

An action resolves to N target ptrs and carries M effect atoms. Nothing currently says **which atoms hit which targets**. "Strike an enemy and heal yourself" is one action with two atoms and two different recipients, and the atom's `when_json` describes *triggers*, not action-relative targeting.

**Proposed: each atom in an action-bound container declares a scope — `caster` · `eachTarget` · `casterAllies` · `primaryTarget`.** This is a small closed enum, and it belongs to the action contract rather than to the atom, so atoms stay reusable outside actions.

#### Multi-hit × multi-target is undefined

`ResolveOffsets` gives *n* hits; targeting gives *m* targets. Is that `n × m` applies? Does hit 2 re-resolve the target set, or reuse hit 1's?

**Proposed: `Commitment` already governs this and should be stated to cover the set, not just one ptr.** `EarlyBound` resolves the target set once at commit and reuses it for every hit, fizzling per target as each dies. `LateBound` re-resolves the whole set at each offset — which is what a spinning-blade multi-hit wants, and what a targeted combo does not.

#### A4's predicate leaves must extend a closed list owned by another program

Usability asks things the effect-atom predicate tree has no leaves for: *is the target in range*, *is a cell free*, *can I afford this*, *is this on cooldown*, *am I silenced*. E3's leaf list is **closed**, and growing it is a reviewed code change there — not something `A4` can do unilaterally.

**Proposed: `A4` contributes leaves to E3's list rather than starting a second predicate language**, and the leaf additions are agreed with the atom program at the same time as the container contract.

#### Smaller, but each is a real branch

- **Channelling is already expressible** — wind-up plus several `ResolveOffsets` plus `Interruptible` *is* a channel. Worth saying so, so nobody builds a second mechanism.
- **Per-tick channel costs are not.** A channel that drains `spirit` each tick cannot be expressed by consume-all-at-commit. Either costs gain a per-offset form, or drain-channels are out of scope in wave 1. Recommend the second, stated explicitly.
- **Partial miss.** With accuracy and dodge already shipped, an action can miss one of five targets. The others land; the action still paid, still cooled down, still held its slot. Same rule as fizzle — committing is what costs.
- **Interrupt and the reaction lane are different mechanisms.** A defender blocking is a reaction (timeline B6, separate `WReact` pool); an interrupt breaks the attacker's committed action. Stating it prevents one being built as the other.
- **Action tags.** The atom ideal's AI contract reads *tags*, never internals. Actions need their own — `offensive` · `heal` · `buff` · `movement` · `summon` — or `A7` has nothing to choose on.
- **Deterministic tie-breaks in the AI.** "Nearest target" ties must break on ordinal ptr, exactly as `TargetResolver` already sorts, or replay diverges.

### 10.5 Smaller items, each cheap to settle now and expensive later

1. **Resource lifetime.** Do pools reset per encounter, per wave, or persist across a session? A persisting stamina pool is a different game from a resetting one, and it changes the store, the save format, and every cost number.
2. **Time agreement across save/load.** `CooldownLedger` stores absolute ticks and cooldowns keep running while suspended; resources resolve lazily from `(value, lastTick)`. Both are correct alone, and they must agree on what `now` means across a save, a load, and a mode switch — the Chaos grounding carries an explicit mode-switching artifact policy for exactly this.
3. **Nothing is in `decisions.md`.** AGENTS.md requires a row before behaviour locks. Only the battle time model has one; the resource model, action model, and atom model have none.
4. **Two sessions are editing this repo.** The second is active on the effect-atom program. A division of files matters more than usual while both are writing architecture.

## 10.5a Verified against the atom program's shipped code (2026-08-22)

The atom program moved from spec to build, so the contract this map depends on is now readable in `src/FusionRpg.Core/Effects/Atoms/` (25 types) and `src/FusionRpg.Data/Sqlite/RpgStore.{Atoms,Containers,AtomInstances}.cs`. Three assumptions confirmed, one broken.

**Confirmed — the seam, in their words, in code.** `ContainerRow`'s own summary:

> *"Containers are mechanism, not content. This holds **what a skill contains** — never **when it fires**. Activation, cooldown, and targeting belong to the turn kernel and the action layer."*

That is the three-program boundary asserted from the other side. It is no longer an interpretation.

**Confirmed — the compiled contract, and what it does not carry.** `RunnerEntry` is what reaches the runner: a **compiled** predicate (never a tree), `ChanceMilli`, `IcdMs`, `IcdKey`, and `Values` as curve-scaled bounds with `OnApply` ranges preserved for per-hit rolls. It has **no target field and no activation field**. Targeting and activation are ours by absence, not just by agreement.

**Confirmed — no change is needed to their closed enum.** `ContainerKind` is `Item · Trait · Skill · SpeciesPassive · Patron · WorldBuff`, and adding one is a reviewed change. There is no `Action` kind and there does not need to be: `A1`'s sketch of a separate `rpg_action` row carrying a `container_id` FK works against `Skill` unchanged. **The dependency on the atom program is now zero API surface** — a foreign key into a table that exists.

**Broken — container order is not execution order.** `ContainerAtomRow.Seq` is documented as:

> *"Authoring order, and stable. **Not an execution guarantee** — execution order belongs to the actor's effect list, which sorts by priority across every container it holds."*

`A1`'s sketch assumed an action's atoms resolve in container order. They do not: ordering is **actor-global by priority**, so a passive trait's atom can land between an action's damage atom and its heal atom.

This is deterministic, so replay is safe, and for independent atoms it does not matter. It matters when an action's atoms are **dependent** — *"heal yourself for the damage this dealt"* requires the heal to observe the strike. Two ways out, and `A1` must pick one:

| Option | Shape | Cost |
|---|---|---|
| **Action resolves its own atoms** | The action layer applies its container's atoms directly at its resolve tick, not through the actor's global effect list | Bypasses a shipped ordering rule; needs agreeing with the atom program |
| **Actions declare a batch** | Atoms belonging to one action resolve as a unit, keeping their relative order inside the global sort | Needs a grouping concept the runner does not have today |

Related note for `A1`: `IcdKey` merges atoms that share a key **into a single grant with a shared clock, by construction**. An action whose atoms merge that way is one grant, not several — which interacts with per-atom target scope (§10.4e) and should be checked rather than assumed.

## 10.7 Spec phase status — 2026-08-22

Seven modules specced, three deliberately not. Specs live at `docs/architecture/action/spec-<module-id>.md`.

| Module | Spec | Note |
|---|---|---|
| **A1** `action-model` | ✅ | The foundation — tables, dataflow, six-case corpus |
| **A2** `targeting` | ✅ | Typed contract; gained `Ordering` after `A5` found the two orders disagree |
| **A4** `usability-conditions` | ✅ | Five ordered gates, typed refusals; asks `E3` for two resource leaves |
| **A5** `basic-attack-adoption` | ✅ | The byte-identity gate; seven hazard fixtures |
| **A3** `action-costs` | ✅ | Five resources, lazy regen, exhaustion-as-status, run lifetime |
| **A6** `action-catalog` | ✅ | **Shrank in the writing** — actions are server-side, so there is no push |
| **A7** `action-selection` | ✅ | The stub AI, and the game's first AI layer |
| **A8** `defence-actions` | ✅ | Stance vs reaction; **builds** after timeline **B6** |
| **A9** `movement-actions` | ✅ | One row, no new runtime; **builds** after `A10` |
| **A10** `battle-board` | ✅ | The grid; **builds** with the board map / battle area |

**All ten specced.** An earlier draft held the last three back on the grounds that specs written ahead of their dependencies rot. The owner's standing principle overrides that: *"we can ideal/spec and plan first because easy to reconcile."* Documents reconcile cheaply; code does not — which is the same reasoning that sequenced this whole program behind the atom build. The last three are **specced, not scheduled**: each names the dependency it builds behind.

### What the spec phase changed

Writing the specs against shipped code — rather than against the map — moved four things:

1. **`A5` found a golden-mover the docs could not show.** `SelectTarget` takes the first active enemy in **list order**; `TargetResolver` sorts by **ordinal ptr**. Routing the basic attack through the resolver unchanged would have retargeted it and moved every golden. Resolved by making the choice a data value (`A2`'s `Ordering`) instead of two code paths that silently disagree.
2. **`A6` shrank to a third of its assumed size.** Actions are battle-mode, battle is server-side, so the injector never needs one. There is no push, and there is no second push mechanism to maintain.
3. **`A4` needed less from `E3` than expected.** *"Silenced"* is already `HasStatus`. Only two leaves are genuinely missing, and both generalise the existing `HpBelowMilli` shape.
4. **`A7` is golden-neutral today**, because with no board there is no distance and "nearest" falls back to source order — which is what `SelectTarget` already does. It becomes a mover the moment `A10` lands.

### Cross-program asks — all three cleared 2026-08-22

Owner-approved and **written into the effect-atom docs directly** (that program is in build phase; documents reconcile freely).

| # | Ask | Outcome |
|---|---|---|
| 1 | **Does an action apply its own atoms**, outside the actor's effect list? | **Already answered by their sealed docs.** `definitions.md`: *"The attack raises an event. An atom on that actor's effect list responds."* And all seven triggers are reactive — no `OnActionUsed`, no `OnCast` — so an action's atoms *cannot* be list responders; they would have nothing to respond to. Not a preference: the only option the vocabulary supports |
| 2 | **Two resource predicate leaves** + `EntityFacts` resource values | Approved. Written into [effect-atom/spec-predicate-tree.md](effect-atom/spec-predicate-tree.md) as `resourceBelowMilli` / `resourceAboveMilli` — a generalisation of the existing `hpBelowMilli` pair, not a new idea |
| 3 | **Action rows join the content hash** | Approved. Written into [effect-atom/spec-content-hash.md](effect-atom/spec-content-hash.md) as a later version registration, matching how `effect_element` and `power_coefficient` already arrive |

**A refinement that came out of closing #1**, and it makes the effect list friendlier than it first looked: their execution order is `(priority DESC, container_id ASC, seq ASC)`, ordinal. Because `container_id` is the second key, **atoms from one container are contiguous and in `seq` order at equal priority**. `seq` is not an execution guarantee across the whole list, but it is one *within* a container — which is precisely the guarantee an action needs.

### Coverage boundary — this program does not prove `W`

`A5`'s basic attack is `slot_consuming = false` (the round loop has no contention) and `A9`'s movement is slot-free deliberately. **No module here exercises a slot-consuming action**, so the action → slot path — commit acquires, resolve releases, fizzle releases, interrupt releases — is unproven by this program.

The kernel's own slot tests are thorough but drive `ActionSlots` directly. The timeline's **B12** — a real action under a real profile — is the natural owner. Recorded as a dependency rather than left as a hole, because "the slot tests pass" is otherwise easy to mistake for coverage that does not exist.

### Spec audit — 2026-08-22

[audit-2026-08-22.md](action/audit-2026-08-22.md): three Critical, six Important, one Minor, all fixed in the specs they affect. Every Critical was found by reading shipped code rather than the specs:

- **C1 — `Core/Actions/` had no determinism guard.** `A1` §9 correctly sites the runtime outside the *tick-path* rules, but that silently dropped the *purity* rules too — so a wall-clock read, an ambient `Random`, or a `double` would compile, pass CI, and break every replay. Fixed: scan the directory with purity rules, tick-path exempt, reusing the mechanism that already exempts `BattleTrace.cs`.
- **C2 — `Random` targeting had no RNG stream.** The battle names `initiative`, `crit`, `essence`, `status` — there is no `target`. An unnamed draw is nondeterministic; a borrowed one desyncs everything after it, which is worse because the battle still looks plausible.
- **C3 — a claimed property the code does not have.** `A2` said precompiling `TargetSpec` avoids a per-call dictionary; `FilterPool` re-parses the filter dictionary on **every** resolve, inside the shipped resolver, and `A7` calls it per candidate.

## 10.6 Seal — pre-spec state, 2026-08-22

Everything below is settled. The spec phase starts from these and does not reopen them.

**Shape.** An action = envelope (when) + container of atoms (what) + target rule (who) + costs + usability condition. **Battle mode only** — PvZ mode observes and never schedules. `A1` delivers the data structure, its tables, and its dataflow; the schema is wave 1, not a late module.

**Resources.** Five ids, one shared set, faction differences are display labels. Persist across a run, refill at rest — a rest is *returning to base*, a run is *a sortie away from it*. Costs: validate all, consume all at commit, roll back on failure. Costs carry `when` (`onCommit` / `perTick`); running dry ends the action through the interrupt path.

**Grid.** 2-D, randomly sized per encounter from the seeded generator, one actor per cell, Chebyshev distance. Deferred — but the parameters (`MinRange`/`MaxRange`, `RangeChannel`, `AnchorSource`, `RequiresLineOfSight`, `move.range`) land now, because range is not retrofittable. **With no board, every range check passes**, which is what keeps the basic attack byte-identical.

**Time.** Move and attack are two ordinary actions; readiness (`TimeCostTicks / rate`) decides whether an actor gets both. No compound action, no Action Points. Lazy within a battle, concrete between battles; cooldowns do not survive a battle boundary.

**AI.** A stub: pursue the nearest target, act to kill it. It reads the board through a **view interface** from day one so fog of war is later an implementation swap, not a rewrite.

**Deferred, not refused:** fog of war, the battle board itself (`A10`), line of sight.

**Golden ordering:** freeze first, move last — T5 gate → `A5` → then T9 + E12 + grid + fog together, one re-bless, one sweep, `RulesetVersion` advances once.

**Method:** copy the genre. A design question is only open when it comes from something specific to this game — which now means exactly three things: the golden ordering, the byte-identical migration, and the zero-allocation frame budget.

### What an action *is* — the membership rule (owner, 2026-08-22)

> **Anything an actor does that interacts with the environment or itself, costs resource or time, and needs a cooldown, is an action. No exception.**

This replaces case-by-case argument with a test, the same way Body / Energy / Essence did for resources. Asking "is summoning an action?" was the wrong question — the rule answers it, and every other case, without a meeting.

| Is an action | Because |
|---|---|
| Basic attack, skill | Costs time, interacts with another actor |
| **Summon** | Costs `soul` / `sun`, targets a cell, has a cooldown. **The game's core verb** |
| Move | Costs time, changes the actor's relationship to the environment |
| Block / guard / brace | Costs time or resource; scheduled on the reaction lane, but still an action |
| Pass | Costs time (`PassQuantum`) |

| Is **not** an action | Because |
|---|---|
| Passive trait, species passive | The actor does not *do* it; no cost, no cooldown |
| Status pulse | The status acts, not the actor |
| Exhaustion debuff | A consequence, not a choice |

**Consequence for the corpus:** summoning is in `A1`'s test set, and `A10`'s free-cell rule sits on the critical path of the most-used action in the game — including what happens when a summon has nowhere to land.

**Naming note:** the rule is broader than combat — it covers summoning, movement, and environment interaction. The program prefix `action` is therefore narrower than its own subject. Renaming to `action` costs three references today and every spec path later.

### Sequencing against the effect-atom program (owner, 2026-08-22)

The atom specs are **audited and sealed**, so nothing here waits on their review.

> **Spec and plan now; build after they build.** Documents reconcile cheaply; code does not.

`A1` references `effect_container.container_id`, which exists. If the sealed contract shifts during their build, a spec paragraph changes — which is exactly the cost this sequencing is chosen to pay instead of a code migration.

### Still open — one item

| # | Item | Why it matters |
|---|---|---|
| 1 | **File division between the two active sessions** | Both are writing architecture docs in this repo. `actor-hub-ssot.md` and `README.md` have already been touched by both |

## 11. Success criteria

1. The engine's existing attack runs as a **declared action** through the envelope with all eight goldens byte-identical — the proof B5 could not produce.
2. A second action exists **as data**, not as a C# catalog — the fifth-content-system problem the atom ideal exists to stop.
3. Targeting is a system with deterministic ordering, not a private method.
4. `SelectTarget` is gone, replaced by an `IIntentSource` implementation that both auto-battle and interactive play use.
5. No condition language, no power currency, and no effect vocabulary is invented here that the atom program already owns.
