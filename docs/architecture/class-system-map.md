# Class system — capability map

**Status: AUTHORIZED 2026-08-26 -- owner's `/goal` directive commands execution of this program's plan
to completion; supersedes the earlier "awaiting owner review, no module authorized" header below, which
was missed by the pass that flipped the other 14 (plan, todo, all 12 module specs) the same week and
was only caught 2026-08-27 in an owner-requested completeness audit.** Module 1 is now **`primary-stats`**,
per the owner's 2026-08-26 instruction.

**Design record:** [class-system-ideal.md](class-system-ideal.md) — postures, aptitudes, resources; **free build**
(no player class) and classes as Zomboss patterns, per the owner correction of 2026-08-25.
**Measurement:** [../research/class-rps-balance-2026-08-25.md](../research/class-rps-balance-2026-08-25.md) — the
simulated search, the nine rules, the limitations.
**Proof:** [../research/class-analytic-balance-2026-08-25.md](../research/class-analytic-balance-2026-08-25.md) —
the closed form, its validation, and the invariance theorem.

**Artifacts:** map → this file · module specs → `docs/architecture/class-system/spec-<module-id>.md` ·
plan → `tasks/class-system-plan.md` · tasks → `tasks/class-system-todo.md`. The bare
`tasks/plan.md` / `todo.md` pair is the perf stream's ([AGENTS.md](../../AGENTS.md)).

---

## 1. What this program is for

The owner's framing, and it is the right one:

> Resolve the deterministic part first, then add tuning functions for the primary-stat and
> derived-stat distribution and scale that can be adjusted later. The simulator simulates a real
> fight, where things the math cannot control — RNG, combination, timing — live; those get resolved
> by simulation and statistical learning fine-tuning the tuning config. The simulator is a POC. A real
> system tunes on real data.

That is three layers, and **every module below belongs to exactly one of them**:

| Layer | What it is | Truth source |
|---|---|---|
| **1. Deterministic core** | Allocation → channels → per-round damage distribution → time-to-kill → win probability. Closed form. No RNG, no trials | arithmetic |
| **2. Tuning config** | Every coefficient the core reads. Versioned data, never code | a balance decision |
| **3. Residual fit** | What layer 1 cannot express — depleting pools, action order, party composition, live play. Measured, then fitted back into layer 2 | measurement |

The layers are ordered because **layer 3 is only meaningful once layer 1 exists.** Without a
prediction there is nothing for a measurement to disagree with, and a simulator with no model to
falsify is just an expensive way to produce a number.

---

## 1a. This program is the one `derived-stats` deferred to

Not a claim this program makes about itself — a handover the previous program wrote down.
[derived-stats-map.md](derived-stats-map.md) §7 *"Explicitly out of scope"*:

| Out of `derived-stats` | Why | Where it goes |
|---|---|---|
| **Primary stats** (STR/VIT/DEX/INT/SPI or the Tinh five) | Owner deferred 2026-08-24 | **Its own program, after this** |
| **`element_mastery`** — per-element progression | A progression design, not a catalog one | **With primary stats** |

So `primary-stats` inherits two things, and the second is easy to lose: the twelve, **and**
`element_mastery`. That program is **built and verified 2026-08-25** (256 channels, the four-class
`statClass` taxonomy, the ten-class `unitClass` ledger), which is what makes this one buildable —
every channel an aptitude feeds already exists and already has a reader.

---

## 2. Modules

Stable kebab-case ids. Referenced by every downstream plan and task.

| # | Module id | Responsibility | Depends on |
|---|---|---|---|
| 1 | `primary-stats` | **The twelve, as a shipped thing.** Their closed id set, posture grouping, the per-actor allocation value type and its `share` denominator, the classification decision (**an aptitude is a SOURCE, not a registered channel**), the render `unitClass`, `element_mastery`'s home, and the vocabulary split from `StatSystem`'s existing "primary" | — |
| 2 | `unit-class-close` | Fill `unitClass` for the **29 of 50** catalog families that carry `null` (counted, not quoted). Structural, not balance: it is a property of what the formula compares each against | — |
| 3 | `distribution-reconcile` | **The primary → derived path, reconciled.** Nine stubs / reserved-but-unwired seams surveyed, each given a verdict — fill, wire, replace, delete or document. Wires the battle path to a subsystem seam, hydrates `Θ`, and settles `progression.bonus.*`'s stub curve. **Adds no mechanic** — it only removes disagreement | — |
| 4 | `poise-resource` | **Register the sixth resource.** `poise` joins `ResourceIds`, `roster.json` gains its row at `ordinal: 5`, `decisions.md`'s *Resource model* goes five → six, and `stamina` stops claiming guard. **Verified to be one array element** — `DerivedStatRegistry.cs:165-171` registers all three channels in a loop, which is the *"a row, not a system"* promise actually delivered. Also fixes `SpecChannelClaimTests`, **red today on nine tokens** | — |
| 5 | `aptitude-tuning` | `data/tuning/aptitudes.v{n}.json` + a Core parser + host injection. The whole balance surface, as data | `primary-stats` · `unit-class-close` |
| 6 | `aptitude-resolve` | Aptitude points → derived channels, through the two read functions, as a registered **`IActorStatSubsystem`**. Wired into the actor's derived composition | `aptitude-tuning` · **`distribution-reconcile`** |
| 7 | `deterministic-core` | The closed form, in `FusionRpg.Core`: per-round mixture → first passage → win probability | `aptitude-tuning` |
| 8 | `balance-guard` | Balance as a CI assertion, not a periodic exercise. **Two halves with different standing** — see §4b. Runs in microseconds because it never simulates | `deterministic-core` · `aptitude-resolve` |
| 9 | `point-economy` | **FOUR point budgets, commander SMALLEST → unique LARGEST** (ideal §7c.2), one per allocation scope (commander / demon type / aspect / unique demon — ideal §7c), allocation persistence per scope, and **respec pricing** — free build has no class price, so respec cost is the only friction left holding a build together (ideal §7b.5) | `aptitude-resolve` |
| 10 | `guard-economy` | `poise` as one ratio: drains ∝ what the guard stopped, regenerates per-tick sized against peer pressure, and **converts on release into a riposte** (ideal §5b.3, §8.3, §8.9) | `aptitude-resolve` · **`poise-resource`** |
| 11 | `zomboss-patterns` | Named allocations as **content**, resolved by id like `FactionPolicies.Resolve`. The class layer, moved off the player and onto the AI (ideal §6). **Newly symmetric with the player** now that allocation is per-actor across four scopes | `aptitude-resolve` |
| 12 | `residual-fit` | Simulate what the core cannot express, measure the gap, fit the config to close it. **First two steps are fixed, not open**: (1) re-measure with elements LIVE (ideal §7c.7), (2) make `stamina` bind — it is free today and is the top reservation for 9 of 12 aptitudes (ideal §8.1b/§8.1d) | `balance-guard` · `point-economy` |
| 13 | `real-data-collect` | Phase 9's own store for V5's resolve-time metrics (P9.1). **Authorized and built 2026-08-27** — owner picked Option B (file-based JSONL log) via `/goal`'s `AskUserQuestion`; `decisions.md` "Class system real-data collection" row landed; `scripts/collect-class-system-realrun.ps1` built and proven against a live server (2/2 tests). **Does not cover per-matchup outcomes** (P9.2's own input) — see module 14 | `residual-fit` |
| 14 | `aptitude-allocation-surface` | The first player-reachable way to spend aptitude points (commander scope). **Authorized and built 2026-08-27** — owner directive ("you should complete the plan") after a completeness audit found `point-economy`'s own persistence (P6.1-P6.4) had zero production callers. `src/FusionRpg.Server/AptitudeEndpoints.cs` (REST) + `WebMatchService.AptitudeChannelMods`'s new real-allocation read wire + `src/layers/aptitudes/AptitudesLayer.tsx` (web UI, 8th rail entry) — closes the gap module 13 named. Every expedition battle a player runs now carries real aptitude signal once they allocate. `DemonType`/`UniqueDemon`/`Aspect` scopes and priced respec are named, undecided follow-ups (spec §6 "ask first") | `point-economy` |

**Build order:**

```text
primary-stats ─┐
unit-class-close ──►  distribution-reconcile        ← adjacent, NOT merged (§2c)
               └──►  aptitude-tuning                  owns AptitudeReadFunctions (§2d)
poise-resource ───────────────────────┐               (independent; blocks guard-economy only)
                          ├─► aptitude-resolve ──┐    (also HARD-blocked on distribution-reconcile)
                          └─► deterministic-core ┴─► balance-guard
                                                        ├─► point-economy ──┐   ⛔ scope 3 external
                                                        ├─► guard-economy ◄─┤   needs poise-resource
                                                        └─► zomboss-patterns┴─► residual-fit
```

**`distribution-reconcile` is a hard block on `aptitude-resolve`, not a nice-to-have.** Without it that
module's central test — *both composers resolve the same values* — cannot pass, because the battle path
has no subsystem pipeline and `Θ` is zero. See §2a.0.

No cycles. `aptitude-resolve` and `deterministic-core` are independent of each other and may be built
in parallel — they share only the config, which is the point of `aptitude-tuning` existing separately.

---

## 2a. Why `primary-stats` is first — and why it was missing

**Verified this session:** `aptitude` appears **nowhere** in `src/`, `tests/`, `data/` or `web/`. It
exists only in `tools/CombatSim/`, which is the POC and ships to no player.

The previous map went straight from `unit-class-close` to `aptitude-tuning`. That is a config file for
coefficients belonging to stats **that have no home**, feeding a resolver that reads points **nothing
persists**. Every downstream module named "the aptitude" and none of them declared it.

> **The twelve had to become a thing before anything could be said about them.** That is what module 1
> is, and it is the smallest module in the program — a closed id set, an allocation value type, and
> three classification decisions.

It also unblocks something the old order could not: `aspect-scope` and the whole four-scope allocation
model in `point-economy` both need an allocation *type* to talk about, and both previously depended on
`aptitude-tuning` for no reason other than that it happened to be first.

### 2aa ⛔ Two actions this program owed with no spec behind them

**Found 2026-08-26 when the owner pointed out that "an action" is not a category — an action needs a
spec.** The `poise` ADR was the first (now module 4). Sweeping for others found two more, and both had
been invisible because they are document edits rather than code.

#### 1. This program has no `decisions.md` row, and that is a hard boundary

`decisions.md` contains **zero** mentions of the class system, aptitudes or free build — counted, not
assumed. [AGENTS.md](../../AGENTS.md) makes *"architecture changes that lock behavior need
`decisions.md` first"* a **hard boundary**, and
[battle-timeline-map.md](battle-timeline-map.md) states the same prerequisite for itself in as many
words.

**So the row is drafted here rather than left as an action.** Its content is the deliverable; writing
it down is what makes it reviewable:

> **Class system (2026-08-26)** — **The player has no class.** Points go anywhere at one price; classes
> survive only as **Zomboss AI patterns**. **Twelve aptitudes** are the RPG primary stats and are
> **sources, not registered channels** — an aptitude is never in `DerivedStatCatalog`, because `share`
> normalises over the actor's own total and a granted aptitude would silently dilute the other eleven.
> An actor's allocation is the **sum of four scopes** (commander → demon type → aspect → unique demon),
> weighted **commander smallest, unique largest** — a commander allocation replicates across the whole
> roster, so a dominant one is the worst case. `share` is taken **on the sum**, never per scope.
> Two read functions, both PS-3: **contests read a `Θ`-free share; magnitudes read `P(Θ)`** — one
> implementation, owned by `aptitude-tuning`, called by both the resolver and the closed form.
> **Win rate is the metric** — never fight length, damage dealt or kill time, which penalise survival
> and cc builds for playing correctly, and never under a clock, which manufactures a pass by penalising
> long fights. **Two acceptance criteria with different standing**: the **termination invariant**
> (no pairing of offence-holding builds has `netAttrition ≤ 0` on both sides) is **HARD and blocks the
> build** — no later layer can repair a pool that refills faster than it drains; the **dominance
> matrix** (no corner beats every other) is **SOFT and reports with its coverage** — a dominant corner
> is what the action/passive/skill layer is for, and it is red by design today. **No aptitude cap and
> no respec cap** (PS-8): respec is available, unlimited, and **priced in a resource fighting also
> costs**. Map: [class-system-map.md](class-system-map.md); ideal:
> [class-system-ideal.md](class-system-ideal.md) §0.0. **Proposed 2026-08-26, not yet built.**

**Not a module.** A `decisions.md` row is not buildable work — drafting it *is* the spec, and landing it
is a prerequisite to building anything here, exactly as the battle program treats its own.

#### 2. The `UnitClass` union owes THREE strings, and nobody owned the edit

[web/fusion-rpg-web/src/contract/types.ts](../../web/fusion-rpg-web/src/contract/types.ts) declares
**nine** members. The ledger in
[design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3 is **twelve** once this
program's two land — and one of the three was owed before this program existed:

| String | Owed since | State |
|---|---|---|
| `"ladderIndex"` | **2026-08-24** — that spec's own *"Contract change owed"* note | **never done**; the union still has nine |
| `"aptitudePoints"` | 2026-08-26, authorised | this program |
| `"reciprocalPoints"` | 2026-08-26, authorised | this program |

> **`primary-stats` owns the edit** — all three strings, one change. It is module 1, it lands first, and
> a union edited three times by three modules is how the first one came to sit undone for two days.
> Picking up `ladderIndex` is adjacent-red housekeeping, not scope creep: leaving a two-day-old owed
> change beside two new ones guarantees the same outcome.

---

### 2b ⛔ One external dependency, owned by another program

**`aspect-scope` left this program on 2026-08-26, by owner decision.** It is the actor's element typing
made an allocation tier — `point-economy`'s **third scope**. Every file it edits belongs to the demon
program (`DemonSpeciesCatalog`, `DemonSpeciesGenerator`, the generated catalog), and that program's
`demon-core` already owns *"species link, rarity, variants, trait slots, **element typing**"*.

| | |
|---|---|
| Spec | [demons/spec-aspect-scope.md](demons/spec-aspect-scope.md) — written here, moved there intact |
| Owner | **demon program** ([demon-system-map.md](demon-system-map.md)) |
| This program's role | **requester.** It supplies the requirement, the byte-identical migration path and the tests; it does not schedule the work |
| Cost accepted | **`point-economy` scope 3 waits on another program's queue.** The other three scopes are unblocked, so that module ships three-of-four and lights up the fourth when the tier lands |

> **This is the honest version of a boundary this map had wrong.** A module that edits none of its own
> program's files is not that program's module — it is a request wearing a module's clothes.

### 2c Two modules that stay separate but must be sequenced adjacently

`unit-class-close` reads the consumers of 29 families to assign a `unitClass`.
`distribution-reconcile` §3.2a widens `BattleStatComposer`'s known-channel set over **the same
families** — `resource.*`, `skill.*`, `move.range`, `progression.*`, `status.duration/intensity.*`.

**Same activity, two outputs.** They stay separate modules because they gate different things
(`unitClass` blocks `aptitude-tuning`; the known-channel set blocks `aptitude-resolve`), but §3.2a
**consumes** those consumer readings rather than repeating them.

> **Considered and rejected: merging them.** The overlap is one sub-item of a nine-item register against
> 29 families. Merging makes an XL module out of two gates that fire at different times — the cost of
> reading a consumer twice is minutes; the cost of collapsing two gates is a plan that cannot express
> the order things unblock in.

### 2d `AptitudeReadFunctions` belongs to `aptitude-tuning`

**Decided 2026-08-26.** `aptitude-resolve` and `deterministic-core` both need `k · share^γ · P(Θ)`. This
map used to call them parallel *"sharing only the config"* — **they share the arithmetic too**, and two
implementations of it is the same defect as two configs, one layer down: a divergence would surface as
*model error* in `residual-fit` and be fitted away.

`aptitude-tuning` owns two of the four inputs (`k`, `γ`), and the function is pure math with no seam —
so it costs that module nothing and keeps the other two **genuinely** parallel.

### 2a.0 ⛔ The primary → derived path it stands on is stubs, end to end

**Owner, 2026-08-26:** *"ClassStatPlugin already exists → they are stub, need reconcile."*
*"BattleStatComposer → stub too, our primary stats and derived stats distribute need reconcile all
blast."*

Both correct. The sweep they prompted is module 3, [`distribution-reconcile`](class-system/spec-distribution-reconcile.md),
and it found **nine** items — every one of them declared, registered, documented and **inert**, each
with a green test beside it. The three that block this program:

| | Found | Why it blocks |
|---|---|---|
| **1** | `ClassStatPlugin` is on the **primary** pipeline (`StatModifier` → `StatComposer`). Aptitudes feed **83 derived** channels, reachable only via `IActorStatSubsystem` → `DerivedModifier` | It is the **wrong seam**, not an empty one. An earlier draft of `spec-aptitude-resolve.md` said to fill it |
| **2** | `BattleStatComposer` **runs no subsystems** — it never references `ActorHub`, `DerivedComposer` or `IActorStatSubsystem` | An aptitude subsystem is **invisible to battle**. **Decided 2026-08-26: the composers stay separate** — the battle-side seam is `ChannelMods`, the way `StarPolicy` already feeds progression stats in. See below |
| **3** | `Θ = 0`. `CheatState.cs:32` builds the hub with no `IPowerIndexProvider`; its own comment calls `PowerIndex` *"inert until then"* | The magnitude read is `k · share^γ · P(Θ)`, so **every magnitude collapses to `P(0) = C`** — one floor for every build. Contest edges keep working, so it reads like a coefficient bug and is not one |

#### Fix the code, or fix the documents? — asked by the owner, answered from the battle stream's own plan

**Neither: use the pattern the repo already has, and correct the document that said otherwise.**

| Evidence | Says |
|---|---|
| [StarPolicy.cs:6](../../src/FusionRpg.Core/Demons/Fusion/StarPolicy.cs) | *"ChannelMods — **never engine changes** (battle goldens stay byte-identical)"* — a progression system already feeds battle actors this way. Aptitudes become the **fifth** `ChannelMods` producer |
| [battle/spec-readiness-model.md](battle/spec-readiness-model.md) | T3 hit **this exact divergence** (*"`BattleStatComposer`'s separate known-channel set… not the same as being a real stat"*) and kept the composers separate — registering `turn.*` in **both** |
| [battle-timeline-map.md](battle-timeline-map.md) | Stat composition is **not in the battle program's scope**, and **T5 is a byte-identical freezer**. `decisions.md`'s *Golden ordering across streams*: a mover overlapping a freezer makes the freezer's proof worthless |

**So `BattleStatComposer` gets no logic change** — only its known-channel set widens, because aptitude
edges reach `resource.*`, `skill.*`, `move.range`, `progression.*` and `status.duration/intensity.*`,
which are outside `AllCombatChannelIds` and **throw** today. That is T3's own repair, reused.

**One sequencing rule falls out:** a `ChannelMods` producer moves nothing while nobody has an
allocation, so `aptitude-resolve` lands byte-identically. **The golden move arrives with
`point-economy`** — and lands before T5 opens or after its gate passes, never inside its window.

> **The lesson, sharper than "open the file":** item 1 was *found*, its registration was *verified*, a
> shipped test even pinned its order — and it was still wrong. **Finding a seam is not the same as
> reading what flows through it.**

Two more the sweep turned up that nobody was tracking: `progression.bonus.*`'s `level × 10` stub curve
is **absent from [power/ssot-power-scale.md](power/ssot-power-scale.md) §10's closed inventory**
(latent — no host passes the level delegate), and **nothing in CI can tell "wired" from "reserved"**,
which is why all nine sat green.

### 2a.1 The naming collision, resolved here rather than discovered later

**`primary` is already taken, in the same subsystem, meaning something else.**
[stat-system.md](stat-system.md): *"**StatSystem** composes **primary** channels only"* — the Unity
combat baseline, `hp` `maxHp` `atk` `defense` `arm1` `arm2`.
[design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3 uses the same sense:
*"Every derived **and primary** magnitude belongs to exactly one class."*

[DESIGN-GATE.md](../DESIGN-GATE.md) §1's *Stats* row was widened for exactly this failure — *"Two
classifications of a channel already exist and are verified against consumers. Inventing a third is the
failure this row was widened to prevent."*

**The split, decided in module 1 and binding on all eleven:**

| Word | Means | Where it appears |
|---|---|---|
| **primary stat** | the twelve | **player-facing text, and the program/module id only** |
| **aptitude** | the twelve | **all code, all channel ids, all config keys, every spec** |
| **primary channel** | `StatSystem`'s Unity baseline — unchanged | shipped code, untouched by this program |

Nothing in shipped code moves. `aptitude` is already what `tools/CombatSim/` calls them, so the POC and
the shipped module agree on day one — which matters, because `residual-fit` compares their outputs.

---

## 3. Why `unit-class-close` is still load-bearing, and what it actually is

**Counted this session, not quoted:** **29 of 50** families in
[data/seed/derived-stats/catalog.json](../../data/seed/derived-stats/catalog.json) carry
`unitClass: null` — including `resource.max`, `resource.regen`, `resource.efficiency`,
`skill.cooldown`, `skill.effectiveness` and `move.range`, which is most of what an aptitude feeds.
(Three entries also carry `statClass: null` — `progression.power`, `progression.realm`,
`progression.xpRate` — a smaller gap this module should close in the same pass.)

Until each has a class, **no coefficient anywhere in this program is a derivation — it is a guess with
a measurement attached** (ideal §8.6). It is **not tunable**, because the answer is determined by the
formula rather than chosen by a designer: a family compared against `baseLong` — the hit itself — is a
magnitude; a family feeding a bounded ratio through a small scale is a contest. Measured consequence of
getting it wrong: matchups fully **invert** across the ladder
([class-rps-balance-2026-08-25.md](../research/class-rps-balance-2026-08-25.md) §3.1).

It is a `derived-stats` leftover rather than a class-system invention, which is why it sits parallel to
module 1 and depends on nothing here.

---

## 4. What is already proven, and what is not

> **The ideal's §0.0 is the authoritative current state.** This section is the map-level summary and
> is kept in step with it; where the two disagree, §0.0 wins.

**Proven** (see the analytic record):

- The closed form predicts the simulator to **1.8% / 2.4%** on core combat, **4.1% / 7.7%** with the
  action economy, status and regeneration all live (2026-08-26 numbers; the earlier 0.4% was a
  single-phase model with two bugs since found — ideal §8.8c).
- Win rate is **exactly invariant in `Θ`** — identical from `Θ`=10 to `Θ`=5,000, by homogeneity
  rather than by measurement.
- The closed form can **solve** for a balanced cycle: spread **0.4%** in **2.3 seconds**, against
  2.1% from a simulated search that took orders of magnitude longer.

**Not proven, and each is a named module above, not a hand-wave:**

- ~~Shields move the answer by up to 32 points.~~ **Closed 2026-08-25** by phase decomposition —
  effective HP plus a gate on reflection; residual back to 0.7% with shields live and purchasable
  (analytic record §6.1). `poise` will need the same treatment when it is registered.
- **Two thirds of the distribution is unfalsifiable today.** `stamina`/`qi`/`hunger`/`spirit`,
  `skill.cooldown`, `resource.efficiency` and `move.range` all price *actions*, and the action layer
  is not built — a duel spends none of them. Those coefficients are designed, not measured, and the
  config says so in its own `_meta.measurable`.
- Nothing here has met a real player, a party, an action layer or an item. `residual-fit` exists
  because a coefficient fitted against a duel is a hypothesis about the game, not a measurement of it.

---

## 4b. The two acceptance criteria are not equals — and `balance-guard` must wire them differently

This is the single most misreadable result in the program, so it is stated at map level too.

| Criterion | Standing | Can a later layer fix it? | Day-one result |
|---|---|---|---|
| **Termination invariant** (ideal §5d) — no pairing of builds that both hold offence may have `netAttrition ≤ 0` on both sides | **HARD — blocking** | **No.** It is an economy identity; content added on top inherits the defect | ✅ **green**, net attrition +3,937 to +14,107 |
| **Dominance matrix** (ideal §8.8b) — no row beats every other, on win rate with no clock | **SOFT — reports, does not block** | **Yes.** A passive scaling damage with damage taken, a reflect build, an anti-turtle status | ⛔ red: `Bulwark` beats all 11 corners |

> **The red one is an UPPER BOUND on severity, not a verdict on the design** (ideal §8.8a). Two
> independent, measured reasons: elements were **neutralised in every run** (§7c.7), and **15–47% of
> every aptitude is reserved against an unbuilt mechanism** (§8.1d) — the corner test sees roughly two
> thirds of each build, and not the same two thirds for each.

**A red SOFT row beside a green HARD row is this design working as intended** (ideal §0.2), not a
system in two minds. `balance-guard` prints **coverage alongside verdict** so a red row reads as *"the
live part of these builds is unbalanced"* and never as *"this design is unbalanced."*

**The failure mode to design against**, because nothing else catches it: wire both halves as blocking
and the program never lands, since the soft half is red by design today; wire both as advisory and the
one defect no later layer could repair becomes a warning nobody reads.

---

## 4c. Free build — what the owner's correction changes here

**The player has no class** (owner, 2026-08-25). Points go wherever the player wants, at one price.
Classes survive only as **Zomboss patterns** — the `zomboss-patterns` module above.

This is not a subtraction. It **raises** what this program has to prove:

| | With classes | Free build |
|---|---|---|
| What must be balanced | three named allocations against each other | the **whole allocation space** — no build may be a best response to everything |
| What "correct distribution" means | the cycle closes near 65% | **every aptitude is the best point somewhere, and none everywhere** |
| Who enforces build commitment | the class price | **respec cost**, and nothing else |
| Who reads it | `balance-guard` compares three builds | `balance-guard` reads the **corners** of twelve dimensions |

**Measured against the new bar, the current coefficients do not pass** — see §4b for exactly which half
and how far. Most of the identified cause is a coefficient-sizing rule that was never written down: a
sigmoid-consumed channel and a reciprocal-consumed channel authored at the same `k` are not comparable
investments ([spec-aptitude-tuning.md](class-system/spec-aptitude-tuning.md) §2.2,
[class-system-ideal.md](class-system-ideal.md) §7b.4).

**None of this moves a dependency arrow.** It changes what `balance-guard` asserts and what
`point-economy` owns, and both were already named.

---

## 5. Reserved — sub-features that land later, and who owns each

**Owner, 2026-08-26:** *"there are many sub features for class system, includes passive skills, will be
added later."*

Named here so they are commitments rather than surprises, and so a module spec can cite an owner
instead of inventing a mechanism. **None of them blocks this program**; several of them are what
eventually turns §4b's soft red green.

| Sub-feature | What it adds to the class system | Owner |
|---|---|---|
| **Passive skills** | The layer that fills the dominant corner: a passive scaling damage with damage taken, a reflect build, an anti-turtle punish. Also the natural home for per-aptitude identity beyond a coefficient | **Later — its own module here, after `residual-fit` measures what shape it needs** |
| **Actions** | `Focus`'s balance fix is **delegated** to this layer by owner decision (ideal §8.1c) — flattening `Focus` into damage would trade a gameplay mechanism for a measurable number. Also what makes `stamina` bind | [action-map.md](action-map.md) — approved 2026-08-22, unbuilt |
| **Cooldowns / readiness** | Three of `Focus`'s largest coefficients (`skill.cooldown.*`) are unmeasurable because **neither engine has cooldowns** | [battle-timeline-map.md](battle-timeline-map.md) |
| **Active skills, elements, flavour** | Rule: **an aptitude reaches a MECHANISM, never a FLAVOUR** (ideal §4.1). Aptitudes stop at `omni`; every element slot and every per-status id belongs to the skill layer — **168 of 259 channels, 65%** | Skill / item layer |
| **Traits and starting skills** | **Derived** from `aspect`, never authored per aspect — one generator argument, not a content project (ideal §7c.4) | `aspect-scope` defines the seam; the content is the demon program's |
| **`element_mastery`** | Per-element progression, handed over by `derived-stats` §7. **Module 1 decides its home; it does not build it** | This program names it; a later module builds it |
| **Party / contagion** | `status.*.contagion` is unmeasurable in a 1v1 — there is no second host | Party simulation, unbuilt |
| **Items and affixes** | The other half of the 66%. An aptitude sets breadth; items carry depth | Item program |

> **This list is the reason §4b's soft criterion is soft.** Every row is a place a later layer can put
> an answer, and a system whose aptitude layer had already answered every question would leave them
> nothing to do — which is the owner's point exactly (ideal §0.2).

---

## 6. Standards every module must satisfy

Not aspirations — each has an audit or guard that already runs.

| Standard | Check |
|---|---|
| **Magnitudes are `long`**; never `float`; widen before multiplying; divide by 1000 last; overflow throws | `python scripts/audit-overflow.py` · [CLAUDE.md](../../CLAUDE.md) |
| **No hard progression ceilings** — a cap on a magnitude is a ceiling (PS-8). Bounded ratios are exempt **and must say so in a comment** | [power/ssot-power-scale.md](power/ssot-power-scale.md) §11 |
| **Balance surface is config** — every scale/rate in `data/tuning/<domain>.v{n}.json`, never a literal | `python scripts/audit-magic-numbers.py` · [tunables-ssot.md](tunables-ssot.md) |
| **One power ladder** — contests read `Θ` (linear), magnitudes read `P(Θ)` (PS-3). No private `f(level)` | `scripts/guard-power.ps1` |
| **Omni is additive-only** — `totalPower = omni + category`, never `omni × category` | [actor-hub-ssot.md](actor-hub-ssot.md) §3 ban |
| **Unknown channel → reject** — the catalog is closed | `DerivedStatCatalog` |
| **A contest-class family needs a counterpart** | `guard-stat-pairs.ps1` (`derived-stats` shipped it) |
| **A spec may not claim an unregistered channel** | `SpecChannelClaimTests` — this is what refuses a `resource.max` claim for **poise** until it is registered |
| **A registered contributor either contributes or declares itself inert** | **New**, owed by `distribution-reconcile` §6 test 6 — today nothing in CI can tell *wired* from *reserved*, which is why all nine items sat green |

---

## 7. Checkpoints

| # | After | Gate |
|---|---|---|
| **0** | `primary-stats` · `unit-class-close` · `distribution-reconcile` · `poise-resource` | Twelve ids closed and collision-guarded · allocation type round-trips · `share` denominator asserted · **zero** `unitClass: null` families remain · **all nine reconcile items have a landed verdict** · a subsystem-sourced modifier reaches a composed value on **both** paths with `Θ` non-zero · no registered contributor is silently empty · **six resources register** and three `poise` channels resolve · **zero goldens moved** |
| **1** | `aptitude-tuning` | Every tuning key rejects-when-missing · `familyRead` agrees with the catalog's `unitClass` · **`AptitudeReadFunctions` has exactly one implementation** (§2d), asserted by both consumers resolving identically |
| **2** | `aptitude-resolve` · `deterministic-core` | Both read **one** config object · a test asserts they resolve identical channel values for the same allocation · contest read is `Θ`-free · magnitude read is proportional to `P(Θ)` |
| **3** | `balance-guard` | **HARD half blocking and green** · **SOFT half reporting, red, and printing its coverage** · 144 corner evaluations in microseconds |
| **4** | `point-economy` · `guard-economy` · `zomboss-patterns` | Four scopes sum additively · respec priced · `poise` ADR landed before `guard-economy` merges · a Zomboss pattern resolves by id like `FactionPolicies.Resolve` |
| **5** | `residual-fit` | Its two fixed first steps done **in order**: elements live, then `stamina` binding · every coefficient's `_meta.measurable` accurate · the soft criterion re-measured and its new value recorded as a measurement rather than a verdict |

---

## 8. Related

- [class-system-ideal.md](class-system-ideal.md) — the design record, §0.0 is its authoritative present
- [derived-stats-map.md](derived-stats-map.md) §7 — the handover this program answers
- [power/ssot-power-scale.md](power/ssot-power-scale.md) §4.6 (PS-3), §10 (closed inventory), §11 (PS-8)
- [tunables-ssot.md](tunables-ssot.md) · [stat-system.md](stat-system.md) · [actor-hub-ssot.md](actor-hub-ssot.md)
- [resource-hub-ssot.md](resource-hub-ssot.md) · [design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3
- [../../tools/CombatSim/README.md](../../tools/CombatSim/README.md)
