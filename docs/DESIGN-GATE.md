# Design gate — read before you propose anything

**Status: binding.** Applies to every session, human or automated, before any spec, plan, proposal,
ADR, audit finding, or sentence beginning "we should".

---

## 0. The rule

**Before you propose a change to a subsystem, you must have read that subsystem's authoritative
documents in the current session, and you must cite them.**

Not skimmed. Not recalled from a summary. Not inferred from a code comment or a filename.

If you have not read them, you do not yet have an opinion. Say *"I need to read X first"* and read it.
That costs one tool call. Proposing against a system you have not read costs the owner an hour of
correcting you, and it is the single most common failure in this repo's history.

**The sequence is: read → verify against code → then propose.** Never propose → get corrected → read.

---

## 1. The reading gate — topic index

Find the row for what you are about to touch. Read the middle column **before** you write anything.
The right column is the thing sessions actually get wrong; read it as a warning, not a summary.

| If you are about to touch… | You MUST have read | What sessions get wrong |
|---|---|---|
| **Anything at all** | [architecture/software-architecture.md](architecture/software-architecture.md) · [architecture/decisions.md](architecture/decisions.md) | Proposing something already locked in `decisions.md` |
| **Product vision / what the game is / which loops exist** | [guide/the-game.md](guide/the-game.md) · [guide/the-loops.md](guide/the-loops.md) · `decisions.md` **Product vision** row | Re-pitching the genre (lawn overlay, “world afterward,” Fusion as mere extension). The player guide is the product-vision SSOT; architecture owns system invariants (including gameless-first as capability). A feature must name a loop on `the-loops.md` — do not invent a parallel pitch. Do not collapse standalone-first to delete it |
| **How the injector talks to the game** | [architecture/event-pipeline-v2-ssot.md](architecture/event-pipeline-v2-ssot.md) · [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) | **Record-then-drain.** Hooks record a struct and return; effects are decided later in a budgeted drain, and records carry to the next frame. G5: worst case degrades to **delayed effects, never frame drops**. Never argue a design from "it must complete inside the frame" |
| **Where logic may live (server vs injector)** | [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) · [architecture/pvz-middle-layer.md](architecture/pvz-middle-layer.md) | **The RPG never reads PvZ's current state and never guesses it.** Two async systems. The RPG observes *past* events and contributes a **signed delta** later. It does not compute damage at the moment of the hit |
| **Combat damage / HP** | [architecture/combat-damage-ssot.md](architecture/combat-damage-ssot.md) · [architecture/effect-funnel.md](architecture/effect-funnel.md) | Deltas, never absolutes. `mode=set` on current HP is rejected. FA10 is `Add`. FA10 never calls Unity `TakeDamage` |
| **Stats** | [architecture/stat-system.md](architecture/stat-system.md) · [architecture/actor-hub-ssot.md](architecture/actor-hub-ssot.md) · [design/spec-derived-stat-sheet.md](design/spec-derived-stat-sheet.md) · [design/spec-magnitude-and-units.md](design/spec-magnitude-and-units.md) | One writer. Combat writes go through `EntityStatWriter`, never ad-hoc Unity patches. **Two classifications of a channel already exist and are verified against consumers** — the nine-class `UnitClass` ledger and the sheet's six render states. **Inventing a third is the failure this row was widened to prevent** (2026-08-24: a spec set proposed a parallel `magnitude`/`bounded-ratio` scheme, and separately concluded "no UI exists", because the two `design/` docs were not in this index) |
| **Any cap, ceiling, limit or throttle** | [architecture/power/ssot-power-scale.md](architecture/power/ssot-power-scale.md) §11 | **Endless grind is the SSOT other systems reconcile *to*.** A cap on a magnitude is a progression ceiling until proven otherwise (PS-8) — remove it or make it a configurable soft cap; absolute bounds are derived and **throw, never clamp silently**. Structural limits, bounded ratios and per-frame caps are exempt **and must say so in a comment**. A ceiling need not be a `const` nor be named like one: an inline `Math.Min`, a narrowing `(int)` cast, a flat rate facing a scaling sink, and a threshold that halves a payout are all caps |
| **Any tunable number (costs, rates, yields, chances)** | [architecture/tunables-ssot.md](architecture/tunables-ssot.md) | **The balance surface is config, not code.** Writing a balance number as a `const` is a tax paid every time the game is tuned — and it makes a rebalance indistinguishable from a code regression when a golden moves. Policy/Catalog/Rules/Ruleset/Math files are the balance surface |
| **Any numeric magnitude (types, widths, overflow)** | [../CLAUDE.md](../CLAUDE.md) "Numeric overflow" · [architecture/power/ssot-power-scale.md](architecture/power/ssot-power-scale.md) §9.4 | **The ladder is quadratic, so old type choices are now wrong.** `float` stops being integer-exact at `Θ`=232 and `int` per-mille at 3,213 — both inside real play. Choosing `int` because "the numbers are small today" is the defect; they are small only at the calibration point |
| **Power / scaling / any magnitude from a level** | [architecture/power/ssot-power-scale.md](architecture/power/ssot-power-scale.md) · [architecture/power-map.md](architecture/power-map.md) | **One ladder: `Θ`, one function: `P(Θ)`.** Its §10 inventory is closed — a power-shaped number not in that table has no permission to exist. **Contests read `Θ` (linear, difference); magnitudes read `P(Θ)`.** Writing a new `f(level)` anywhere is the defect this SSOT was created to end — three incompatible curves shipped simultaneously before it |
| **Effects (Foundation)** | [architecture/effect-system.md](architecture/effect-system.md) · [architecture/effect-data.md](architecture/effect-data.md) · [architecture/effect-runtime.md](architecture/effect-runtime.md) · [architecture/effect-funnel.md](architecture/effect-funnel.md) | Foundation is **sealed**. The Funnel is the only Secondary → Bag path |
| **The atom / Secondary effect layer** | [architecture/effect-atom/definitions.md](architecture/effect-atom/definitions.md) **(wins over any spec)** · [architecture/effect-atom/atom-catalog-ssot.md](architecture/effect-atom/atom-catalog-ssot.md) · [architecture/effect-atom-map.md](architecture/effect-atom-map.md) | The vocabulary is **closed**: **7 attach points, 16 kinds, 13 triggers** (`AtomKindRegistry.AttachPointCount` / `.KindCount` / `.TriggerCount` — verified by counting, 2026-09-05; `decisions.md:112` already carried the correct seven). **This row has now gone stale twice.** It said 5/12/7 until 2026-09-03, when only the trigger count was corrected to 8 — while E34 had already taken triggers to 13, E35/E36/E37/E41 had taken kinds to 16, and E35/E41 had taken attach points to 7. None of those propagated here. Because this file **wins over any spec**, a stale count here outranks every correct one downstream, so a module that widens the vocabulary is not finished until this line moves with it. Adding one is a reviewed change, not a convenience |
| **Affix/container authoring, rolled instances, the L1-L4 resolution model** | [architecture/effect-pipeline-ideal.md](architecture/effect-pipeline-ideal.md) §5 · [architecture/effect-pipeline-map.md](architecture/effect-pipeline-map.md) · [architecture/effect-atom/spec-container-schema.md](architecture/effect-atom/spec-container-schema.md) | **Added 2026-09-01, `seed-to-concrete` T0.8** (checkpoint 0's own read-back found this row missing). `Instantiator`/`TryInstantiate` are **built but had zero production callers** until `effect-pipeline` module 4 wires the call — do not conclude the atom layer is unreachable without checking whether that module has landed yet. The pool's roll unit is an **affix** (a named bundle, possibly spanning a slot), not a bare atom — see `definitions.md` §4a |
| **Item rarity, the ten-rung ladder, demon rarity** | [architecture/item/ssot-rarity.md](architecture/item/ssot-rarity.md) §3.3/§4.3 | **Demons share this ladder as of 2026-09-01** — `DemonRarity`'s old four-value ladder is a **migration shim only** (§4.3), not a parallel system to design against |
| **Actions, skills, targeting, action costs or the action corpus** | [architecture/action-ideal.md](architecture/action-ideal.md) (sealed, 26 decisions) · [architecture/action-map.md](architecture/action-map.md) · [architecture/action/spec-action-seeding.md](architecture/action/spec-action-seeding.md) · [architecture/action-corpus-ideal.md](architecture/action-corpus-ideal.md) | **Added 2026-09-02.** Three vocabularies are already closed and a fourth is a trap: `ActionCategory` (5), `ActionTag` (8), `ActionKind` (3), `ActionTargetMode` (6) + 4 area shapes. *"Inventing a third vocabulary is the exact defect the atom program exists to stop"* (`spec-action-seeding.md` §3). **`rung(n) = min(earnCount, cap)` — rung is progression, never an action property**, so a per-action power difference is a second curve and `action-ideal.md` §1.3 already rejected one. `A13`'s runtime roll is **built**; what is missing is the corpus and `type-weights.json`. Prior art with numbers: [research/action-taxonomy/](research/action-taxonomy/) |
| **PvZ mechanics, the host game's own systems, or "how do other games do X"** | [research/genre-mechanics/README.md](research/genre-mechanics/README.md) · [research/action-taxonomy/README.md](research/action-taxonomy/README.md) · [research/game-design/README.md](research/game-design/README.md) | **Added 2026-09-02, after two research rounds (~16,700 lines, ~14 passes).** **Read the `What I could not find` sections before commissioning any search** — 17 files carry one, and between them they record well over a hundred named absences and access blocks. The host game's own fusion API, rarity enums and difficulty ladder are documented **from its shipped assemblies** in [research/genre-mechanics/02-pvz2-chinese-and-fusion.md](research/genre-mechanics/02-pvz2-chinese-and-fusion.md) — do not re-derive them from wikis |
| **Demon species generation / seedsmith's demon pipelines** | [architecture/demon-seed-map.md](architecture/demon-seed-map.md) · [architecture/demon-seed/](architecture/demon-seed/) module specs | **Seed → concrete → per-player is binding for every generator, demons included** (`tasks/seed-to-concrete-plan.md`). Species *stats* are deterministic and shared; only *effects* roll, per player, at runtime — never assume a species table is finished content once generated |
| **Economy / currencies / yields** | [architecture/empire-economy-ssot.md](architecture/empire-economy-ssot.md) · [architecture/economy-principles.md](architecture/economy-principles.md) · [architecture/demons/spec-soul-economy.md](architecture/demons/spec-soul-economy.md) | **Every faucet names its sink in the same change.** Territorial income needs territorial upkeep — tuning cannot fix a growth-rate mismatch. A second stock earns its name only if some cost is a `min(x,y)` bottleneck. The repo has already paid for this once: uncapped +2/kill hit ~20-25 pulls/hour against a ~5-8 target. **`empire-economy-ideal.md` is superseded** — reasoning trail only, and it carries four retraction layers |
| **Resources / actor pools** | [architecture/resource-hub-ssot.md](architecture/resource-hub-ssot.md) | **One shared set of SIX — `hp` `stamina` `hunger` `spirit` `qi` `poise`** (`DerivedStatChannels.cs:521`, verified by counting 2026-09-05); faction difference is a display label, never a branch or an id. **This row said "five" until 2026-09-05** — `poise` was registered 2026-08-26 and four separate documents had already recorded the drift as errata without anyone propagating it back here. Because this file outranks downstream specs, a stale count here poisons every session that reads it: **a module that widens the resource set is not finished until this line moves with it.** ⛔ **All six are legal action costs** (owner, 2026-08-30) and **every derived-stat family touching a resource must cover all six** (owner, 2026-09-02) — a family covering a subset is a defect, never a feature. Also: the plant pool labelled "Sun" is `hunger` at actor scope — it is **not** the lawn sun bank, which is `pvz.*` and match-scoped. `resource-hub-ideal.md` is superseded; its §2 and §10.2 are stale |
| **Status effects** | [architecture/status-ssot.md](architecture/status-ssot.md) | `StatusCatalog` is ADR-locked code-first. 21 declared, ~13 functional |
| **Elements** | [architecture/element-hub-ssot.md](architecture/element-hub-ssot.md) | Two matrices, not one — the shield matrix is asymmetric with the combat ring |
| **Data / SQL / schema** | [architecture/data-architecture.md](architecture/data-architecture.md) · [contributing/architecture-map.md](contributing/architecture-map.md) | SQL lives **only** in `FusionRpg.Data`. `guard-dal.ps1` enforces it — and scans only `src/`, so `tools/` is a blind spot |
| **Match / actor lifecycle** | [architecture/match-runtime.md](architecture/match-runtime.md) · [architecture/unique-actor-runtime.md](architecture/unique-actor-runtime.md) · [architecture/unique-entity-effects.md](architecture/unique-entity-effects.md) | IL2CPP reuses pointers. `entity:{ptr}` grants must be withdrawn on death before reuse |
| **Performance** | [runbook/perf-probe-plan.md](runbook/perf-probe-plan.md) · [research/perf/00-baseline.md](research/perf/00-baseline.md) | Lag is **main-thread scans and uncached resolves**, not SignalR or the server. Do not re-litigate transport without new probe data |
| **Battle / turns** | [architecture/battle-timeline-map.md](architecture/battle-timeline-map.md) · [architecture/battle-turn-ideal.md](architecture/battle-turn-ideal.md) | Battle consumes FA10 only; it never grants and never calls `OnEvent` |
| **World map** | [architecture/world-map-program.md](architecture/world-map-program.md) | Specs pending owner review — no build authorized |
| **Anything a player sees (UI)** | [architecture/game-gui-principles.md](architecture/game-gui-principles.md) · [design/information-architecture.md](design/information-architecture.md) · [design/README.md](design/README.md) · [architecture/fe-game-foundation.md](architecture/fe-game-foundation.md) | **A game is a stage with layers, not a document with pages.** Proposing another top-level route, another sidebar entry, or a screen the player must *navigate to* — GG-1 says menus open over where the player already is. Also: engine vocabulary (`typeId`, `Intent`, `UniqueActor`) on a player surface, and mixing developer surfaces into game navigation |
| **Standalone / web RPG** | [architecture/standalone-rpg-map.md](architecture/standalone-rpg-map.md) · `decisions.md` Standalone-first row (capability/CI) · Product vision row | **Gameless-first is a capability rule**, not the genre. Do not generalise the injector's constraints onto the web, or its model onto the injector. Do **not** re-pitch the product from this row — read the **Product vision** row above (`guide/the-game.md` + `guide/the-loops.md`). Quoting *"web RPG is the core; PvZ is extension"* as *"Fusion is optional DLC"* is a misread |

Full map: [README.md](README.md). The capability map of a program is the index of what exists for it —
never guess which spec is active from a filename.

> **⚠️ `docs/design/` is a parallel spec set, and most rows above do not name it.** The rows were
> written from `docs/architecture/`, but `docs/design/` holds per-surface specs — `spec-derived-stat-sheet.md`,
> `spec-magnitude-and-units.md`, `spec-shield-and-elements.md`, `spec-action-layer.md`,
> `spec-equip-and-paperdoll.md`, `spec-item-card.md`, `spec-sockets-and-sets.md`,
> `spec-inventory-and-workshop.md`, `spec-comparison.md` — that **verify their claims against `src/`**
> and are normative for how a number reaches a player.
>
> **Before proposing in any subsystem, check `docs/design/` for a matching `spec-*.md` even when your
> row does not name one.** Added 2026-08-24 after a session wrote twelve specs against derived stats
> without reading either of the two that already covered them — see §4's last two rows.

---

## 2. Load-bearing invariants

These are settled. Re-deriving them from scratch is how sessions arrive at confident wrong answers.
If your proposal contradicts one, you have found either a real architectural change (say so
explicitly, and expect a decision) or your own misunderstanding (far more likely).

1. **Two async systems.** The RPG and PvZ do not share a clock and do not wait for each other. The RPG
   works from **past events**, never current game state, and never guesses it.
2. **Record-then-drain.** Hooks record and return. Decisions happen in a later budgeted drain.
   **Delay is the designed degradation mode**, not a failure to engineer around.
3. **Deltas, not absolutes.** Overlay mutations are signed deltas through the Funnel. Absolute HP/ATK
   from an overlay snapshot is rejected by contract.
4. **Single writer.** All combat writes go through `EntityStatWriter`.
5. **The Funnel is the only Secondary → Bag path.**
6. **SQL only inside `FusionRpg.Data`.**
7. **The game is the simulation, not a thin client** — but that does *not* make the overlay
   latency-bound. See invariant 2. Both halves of this sentence matter.
8. **Foundation is sealed** at its contract version. Secondary builds on top; it does not edit it.
9. **Standalone-first (capability).** Every RPG feature must be playable with the game closed. The
   injector may *enrich* a feature, never *permanently gate* one. This is a **capability and CI**
   rule, not a claim that the lawn is optional flavor — product loops (lawn first core, idle,
   empire, …) live in [guide/the-loops.md](guide/the-loops.md). See `decisions.md` Standalone-first
   and Product vision rows.
10. **Perf is a main-thread problem.** Settled by measurement in 2026-08.
11. **No hard progression ceilings.** Endless grind is the SSOT; caps on magnitudes are removed or
    made configurable soft caps, and absolute bounds throw rather than clamp
    ([architecture/power/ssot-power-scale.md](architecture/power/ssot-power-scale.md) §11).
12. **The balance surface is data.** A number a balance pass would change lives in
    `data/tuning/<domain>.v{n}.json`; a structural constant stays a `const` and says why it is not
    tunable ([architecture/tunables-ssot.md](architecture/tunables-ssot.md)).
13. **Magnitudes are `long`.** The power ladder is quadratic; `float` and per-mille `int` both fail
    at indices reachable in normal play (232 and 3,213). Widen before multiplying, divide last,
    let overflow throw.
14. **One power ladder.** Every magnitude derives from `P(Θ)` and every contest from `Θ`
    ([architecture/power/ssot-power-scale.md](architecture/power/ssot-power-scale.md)). No subsystem
    owns a private level curve. Contests are decided by *differences*, which is why the contest read
    must stay linear — a geometric curve makes a fixed level gap unboundedly decisive.

---

## 3. Evidence rules

1. **Cite `file:line`.** A claim without a location is an opinion.
2. **Code beats documentation; documentation beats comments. A comment is not evidence.**
   A file comment saying it mirrors another file is not a coupling — open the file and check.
3. **Read the section, not the line.** Before quoting a rule as a general law, read its heading and
   its neighbours. A rule under *"What the Server may do during a run"* constrains the server during a
   run; it is not a universal principle.
4. **Test the constraint before you declare it.** "This would move the goldens" and "this needs owner
   sign-off" are *claims*. Run the suite. An assumed constraint that costs the owner a decision they
   did not need to make is the same defect as a wrong line of code.
5. **Verify counts by counting.** Not by trusting a number written elsewhere in the same doc set.
6. **When you correct something, propagate it.** A fix that lands in prose but not in the sibling
   Structure / Testing / Boundaries block, the map, and the task list has not landed. Re-grep after.

---

## 4. Failure log

Real incidents. Added to whenever a session burns owner time on a misconception. This section is the
argument for the gate — keep it factual and keep it growing.

| Date | The misconception | Root cause | What would have caught it |
|---|---|---|---|
| 2026-08-22 | "The proc roll must happen in the injector because a server round-trip cannot complete inside a frame." | Never read `event-pipeline-v2-ssot.md`. The pipeline is record-then-drain and **G5 explicitly makes delayed effects the designed worst case**. Argued from a constraint the architecture rejects | Reading the pipeline SSOT before reasoning about pipeline timing |
| 2026-08-22 | Quoted *"Server must not own authoritative proc RNG for lawn hits"* as a general architectural law | It sits under *"What Server **in a run** may do"* and concerns the UniqueActor FSM, not a ban on server-side rolling. Read the line, not the section | Evidence rule 3 |
| 2026-08-22 | "Fixing the effect RNG will move goldens, so it needs owner sign-off" | Assumed rather than tested. All 7 chance-gated fixtures use `chance: 1.0`, and the code short-circuits the draw at `chance >= 1.0` — the RNG was never consulted. **Zero goldens moved** | Evidence rule 4 — run the suite before escalating |
| 2026-08-22 | Treated a `VfxCatalog` comment (*"mirroring EffectSeedCatalog"*) as a cross-stream blocker | The file contains zero `fx.*` ids and keys on statusIds. The comment was stale prose | Evidence rule 2 |
| 2026-08-22 | "Plants have no armor" | Asserted from vanilla `arm1`/`arm2` being zombie-only, without reading the shield/resistance layer, which is side-agnostic | Reading `status-ssot.md` / the shield program before claiming a mechanic does not exist |
| 2026-08-23 | A power-scale SSOT was drafted asserting *"player level enters the formula nowhere"* and *"`scaleAt` shape is open — pick exponential, polynomial or soft-cap"* | Written without reading the shipped curves. `decisions.md` P1 already locked level → power; `BattleRuleset.BaseHp` already **was** the shared curve; the overflow analysis solved a problem the shipped linear math does not have | Opening `BattleModels.cs` and `IProgressionPowerProvider.cs` before writing a document about level curves. The sweep that followed found **14** power-shaped scales, three of them mutually incompatible |
| 2026-08-23 | `ProgressionPowerCurve = 2^min(L,12)` shipped as a "POC curve" and went unexamined | An exponential feeding a *difference*-based contest. At L12 a matched pair produces `netFactor = 4096` — a base-20 status deals 81,920. Latent only because `SetLevel` has no caller. **The stub value `1.0` is the one value at which broken and correct agree**, so a green test sat on top of it | Probing the evaluator across levels instead of trusting a passing test at the stub value |
| 2026-08-24 | A 12-spec program for derived stats concluded *"no UI surface exists for 157 new channels"* and proposed a fresh `magnitude`/`bounded-ratio` classification | **Both already existed in `docs/design/`.** `spec-derived-stat-sheet.md` designs the surface (six render states, the `no-producer` state these channels land in); `spec-magnitude-and-units.md` §3 is a **nine-class `UnitClass` ledger, each class verified against its consumer in `src/`**, already bound in the web contract. The §1 *Stats* row named only the two `architecture/` docs, so neither was ever opened | The `docs/design/` note under §1's table — added because of this |
| 2026-08-24 | *"`DerivedStatRegistryTests.cs:22` asserts a literal 84; replace it with the formula"* | The test **already computes** `families.Count × (roster.Count + 1)`; the literal on the line above is a deliberate canary asserting what the formula currently equals. A sibling test is named `The_channel_count_is_the_formula_not_the_literal_eighty_four`. The spec would have had someone rewrite tests that were already correct | Reading the whole test body, not the cited line. **Evidence rule 3 applies to code, not just prose** |

---

## 5. Pre-proposal checklist

Paste and complete before presenting any design work.

```
[ ] I identified the subsystem(s) this touches.
[ ] I read every doc in the §1 row(s) for those subsystems, this session.
[ ] I checked decisions.md for a lock covering this.
[ ] Every factual claim cites file:line.
[ ] I verified claims against CODE, not comments.
[ ] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting - "moves goldens",
    "needs sign-off", "breaks X" - and said what I ran.
[ ] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
[ ] Corrections are propagated to prose, Structure, Testing, Boundaries, map, and tasks.
```

**If you cannot tick a box, say so in the proposal.** An honest gap costs a sentence. A hidden one
costs the owner an hour.
