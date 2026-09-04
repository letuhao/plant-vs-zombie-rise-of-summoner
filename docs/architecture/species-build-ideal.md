# Species build — the ideal

**Status:** idea phase, 2026-09-04. **Not a spec. No build authorized.** **Four owner decisions landed
the same day — see §0.0; they are binding and should not be reopened.** This is the conversation
[class-system-map.md](class-system-map.md) §2 module 14 reserved by name: *"`DemonType`/`UniqueDemon`/
`Aspect` scopes and priced respec are named, undecided follow-ups (spec §6 'ask first')"*.

**Owner framing, 2026-09-04:** *"the lawn game and current progression system level up demon specie by
spawn them in the game"* · *"player will unlock basic variant of demon specie when play game"* · *"they
will earn bonus when specie level up by auto primary stats distributions"* · *"we have 12 primary stats
so we have 12 basic build for classes"* · *"i want demon have build favour so it will auto distribute the
bonus primary stats, this is not unique demons so to avoid overwhelm users, we should auto distribute
stats, the zomboss will do the same"* · *"will also unlock feature allow user adjust distributions"* ·
*"zomboss will have chance to rebalance his army base on user build favour (he will try to counter build
user if he have lose streak or we will randomly change the build when he level up, this is randomly and
tunable"* · *"zomboss will cheat, he can respec by change, user is not, we cost souls to respec, it will
increase by demon specie level"* · *"this will be first feature that bring primary stats distribution to
the game and bring our demon specie to the game"*.

> Quotes are lightly normalized for the owner's known input-method artifact only (`respect` → respec).
> No wording was otherwise changed.

**Reads satisfied in the session that wrote this** (DESIGN-GATE §1 rows: *Anything at all* · *Stats* ·
*Power/scaling* · *Any cap* · *Any tunable* · *Economy* · *Demon species generation* · *How the injector
talks to the game* · *Match lifecycle* · *Standalone/web RPG*):
[DESIGN-GATE.md](../DESIGN-GATE.md) (full) · [decisions.md](decisions.md) (full, both pages) ·
[stat-system.md](stat-system.md) (full) · [rpg-progression.md](rpg-progression.md) (full) ·
[class-system-ideal.md](class-system-ideal.md) §0.0–§0.1, §6–§6.3, §7c, §7b.5 ·
[class-system-map.md](class-system-map.md) §2, §5, §6 ·
[demons/spec-soul-economy.md](demons/spec-soul-economy.md) (full) ·
[power/ssot-power-scale.md](power/ssot-power-scale.md) §11.2/§11.2a ·
[actor-hub-ssot.md](actor-hub-ssot.md) §2 + the `progression.bonus.*` ban and channel rows ·
[design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3 (`AptitudePoints`) ·
[tunables-ssot.md](tunables-ssot.md) §0–§1 · [game-gui-principles.md](game-gui-principles.md) GG-1/GG-10 ·
plus direct code reads cited inline throughout.

✅ **Audited before spec, 2026-09-04, and the audit is closed.** An adversarial pass found that two of
the then-thirteen decisions were **not executable as written** (A1, A2), that **one citation in this
document was wrong** (A3), and that two verdicts needed correcting *in the design's favour* (A5, A6).
All three findings went back to the owner and were re-decided the same day — **decisions 14, 15 and 16**.
Read **§11** for the findings and **§12** for what the spec still owes. **Sixteen decisions total; none
outstanding.**

⛔ **The reading gate is now satisfied for every row this feature touches except two** — §10 lists
exactly which, and what each leaves exposed.

---

## 0.0 Decided by the owner, 2026-09-04 — do not reopen

Sixteen decisions across five rounds, all on 2026-09-04 — the last three (14-16) resolving the pre-spec audit in §11. Every one that came back richer than the options
offered is **quoted rather than paraphrased**, because in four cases the owner's reasoning is the part
worth keeping — twice it was a better argument than the one the option carried.

| # | Decision | Consequence | Where |
|---|---|---|---|
| **1** | **Souls price the respec.** | Answers the "Ask first" the code itself records (`RespecPolicy.cs:11-18`). `RespecResource` gains a value; `decisions.md`'s *Class system* row wording (*"priced in a resource fighting also costs"*) is **amended in the same change**, not quietly reinterpreted — souls satisfy it by opportunity cost, not literally. `spec-soul-economy.md`'s *"Ask first: new spend sinks"* is thereby answered too, and the spend needs its own feature endpoint and reason, never a generic one. | §5.2 |
| **2** | **Both non-lawn sources: expeditions grant species points, AND web battle is promoted out of `FUSIONRPG_SIM=1`.** | Standalone-first is satisfied twice over. **Scope warning, stated up front:** this makes a real player-facing web-battle endpoint part of this program's blast radius — it is currently `/api/test`-only (`WebMatchService.cs:495-520`, `SimFlags.cs:5-9`). Who *owns* that promotion is a live question (§7 Q3). | §4 |
| **3** | **A deterministic redistribution function, and no single-primary builds.** Owner: *"we need build a deterministic function, it will make re-distribution plan and reconcile the balance, current 12 primary stats distribution is most imbalance in the game"* · *"i suggest avoid 1 primary stat distribution, it cause some unit too strong and too weak at same time, mix up build at primary build is better"* | Rejects both offered options. The favour is an **input to a function**, not a distribution in itself. Pure 100/0 allocation is **out** — by owner reasoning, a one-stat unit is simultaneously too strong on its axis and too weak everywhere else. Raises a real technical fork the options did not: `pure` is consumed **today** by `SpeciesExpander` for base stats, so "no single-primary" has to declare whether it binds the allocation layer only, or base generation too. §3a. | §3a, §7 Q4 |
| **4** | **Zomboss does both — variety on level-up AND counter-building on a lose streak — announced *after the next fight*.** | Not the "announced before" option offered, and the difference is the good part: **both sides act on one-fight-old information.** He counter-builds against the build you last beat him with; you learn the pattern he last used. Neither holds current information about the other. That is a bounded, symmetric, tunable asymmetry — and it defuses the DDA backlash finding precisely, because what those systems were punished for was adaptation that stayed *hidden*, not adaptation that arrived *lagged*. | §6.4 |
| **5** | **"No single-primary" binds the ALLOCATION layer only. No base-stat regeneration.** Owner: *"about regenerate, i don't think we need"* | Closes §3a.1's fork on the cheap side. `pure` keeps its current meaning inside `SpeciesExpander`, all 829 generated stat files stay byte-stable, nothing re-blesses. **Consequence to carry:** base stats stay thematically skewed while allocations spread — a species' *identity* stays what it was classified as, and only its *growth* is mixed. That is coherent, and it is a deliberate split rather than an oversight. | §3a.1 |
| **6** | **The rebalance is the deterministic function's job. The LLM does not help here.** Owner: *"this rebalance LLM cannot help us"* | Rules out the `demon-seed` prompt-calibration pass that §7's earlier draft offered as an alternative route to the skew. It also restates this repo's own standing line from the other direction: the model classifies a *category*, and **balance is arithmetic** — `SpeciesExpander`'s own header already says *"Model calls: none, ever — this module is the entire reason no model ever picks a number."* The favour is a classification; the distribution is computed. | §3a |
| **7** | **The function runs at GENERATION TIME, and its output is shipped static knowledge.** Owner: *"we should make the static knowledge to avoid user confuse when play game, they can adjust at they want, but need shipped static knowledge, avoid user much learn every time play the game"* | A **player-knowledge** argument, and it is stronger than the testability one that was offered. A species' build is a fact the player learns **once** and that stays true — a Pokédex, not a per-run roll. Three real consequences: (a) the plan is **content**, so changing it is a reviewable diff, never a silent per-run difference; (b) **runtime randomisation of the baseline is ruled out** — DQM's random growth spurt is prior art this design deliberately does not take; (c) the player override sits **on top of** the static baseline, never replacing the need for one. | §3a.2 |
| **8** | **The function reconciles to DISTRIBUTION PARITY — spread species across the twelve.** | Chosen over targeting the termination invariant and the dominance matrix directly. Cheap to compute, cheap to verify, needs no combat simulation, and attacks the measured cause rather than a downstream symptom. It does **not** claim win-rate balance — it removes the input that currently guarantees imbalance. The HARD termination invariant and SOFT dominance matrix stay owned where they already are. | §3a.2 |
| **9** | ⛔ **SUPERSEDED BY THE AUDIT — see A2.** Was: *"respec price scales with species level, pinned as a fraction of expected soul income at that level."* | **The quantity it names does not exist.** Souls are player-scoped and species level is per-species, so "income at that species' level" resolves to nothing; and soul income is **flat today** anyway, because every live earn passes the Θ pin (`RpgStore.Souls.cs:29`) so `contentScale = 1.000`. The intent — an escalation that can never outrun its faucet — is sound and survives; the formula does not. **Needs re-deciding (§12 item 2).** | §11 A2 |
| **10** | **A `DemonType` allocation is keyed per-player, by `speciesId`.** | Matches `rpg_actor_progression`'s existing per-player grain and uses the demon corpus's own identity. The `game_type_id` bridge is then needed **only at the lawn boundary**, where a spawn event carries a PvZ type int — keeping PvZ ids out of the RPG layer, which is the direction seed-to-concrete has been moving. Global (shared) allocation was explicitly ruled out: one player's respec must never change another's roster. | §2, §7a |
| **11** | **Parity is measured over TOTAL ALLOCATED POINTS across the corpus — never over the primary field.** | The only reading consistent with decisions 3 and 5. Each species **keeps the lean it was classified with**; because no species is single-primary any more, it is the *remainders* the function steers, and the corpus-wide sum is what lands inside the band. The function therefore never overrides its own input — it shapes what it adds. Reassigning primaries was explicitly rejected: it would make the favour meaningless and detach a species' identity from its lore. | §3a.2 |
| **12** | **The parity target is a BAND (floor and ceiling), not a point.** | No aptitude below a floor or above a ceiling, both tunable, rather than minimising deviation from an even 8.3%. Chosen so the corpus is allowed to genuinely have more attackers than counter-attackers — that is a real property of PvZ plants, not a defect — while `Ferocity` at 2 species is still fixed. Also the cheaper acceptance test: a band is a pass/fail check, not an optimisation. The floor and ceiling numbers are **tunables a balance pass owns**, not design constants. | §3a.2 |
| **13** | **The web-battle endpoint promotion belongs to a SEPARATE program; this program declares the dependency.** | Refines decision 2 rather than retracting it. This program needs the *signal* — a resolved match that can level a species — not the *endpoint*. Promoting `/api/test/web-match` to a player-facing battle API carries its own GUI, contract and lifecycle questions, and `standalone-rpg` is its natural home. **What this program builds for standalone-first is the expedition path**; the web-battle path is a named dependency it consumes when another program ships it. | §4 |
| **14** | **The `DemonType` budget source is SPECIES LEVEL, not almanac XP.** Closes audit finding A1. | Budget = `speciesLevel × 4`, restoring the locked ordering (60 &lt; 80 &lt; 120 at L20) and making the tier **exactly symmetric with `UniqueDemon`**, which already reads "specimen level". It also matches the original ask better than XP did — *"they will earn bonus when specie level up"* — because points arrive as a visible jump at level-up rather than trickling with every placement. **Three places still say "almanac XP" and are propagations owed:** `spec-point-economy.md:37`, `PointBudget.cs:12-18`, and `aptitudes.v5.json`'s `_scopeSourcesWhy`. **And the guard test must be fixed** — `PointBudgetTests.cs:84` deliberately holds the source constant, so it cannot see a source-unit defect. | §11 A1 |
| **15** | **The respec price rises with the RESPEC COUNT on that species, and decays over time. This replaces decision 9 entirely.** | Prices **churn**, not investment — which is what §7b.5 actually wants stopped (*"with free respec there is no build, only a lookup table keyed on the opponent"*). Well-defined, per-species, and **can never become a ceiling because the player controls the rate entirely** (PS-8 satisfied by construction, no dependence on an unwired Θ). Grim Dawn's shipped shape. **Checked against the lock:** it is not a cooldown — a cooldown *forbids*, this only prices, and the decay means **being away makes it cheaper**, which is the exact failure (*"punishes being away"*) the lock rules out. Two consequences: a per-species respec counter is **new persisted state**, and `RespecPolicy.PriceOf(tuning)` gains a count argument (`RespecPolicy.cs:32`) — never a level. | §11 A2 |
| **16** | **The primary lean VARIES per species — crowded primaries lean less, rare primaries lean more.** Closes audit finding A7. | Dissolves the coupling rather than trading against it: the ceiling constraint only binds when the lean is uniform, so a per-species lean reaches any band. Reads as a rule a player can learn: **common archetypes are generalists, rare archetypes are specialists.** ⭐ **It also simplifies the spec** — combined with decisions 8/11/12, the lean stops being a separate tunable at all. The function emits a full share vector per species, and the lean **falls out of solving for the band** rather than being chosen and then checked. | §11 A7 |

---

## 0. The principles this design is bound by — restated here, not linked

A downstream session reads this document, not its links. These are the rules that decide whether a
proposal below is legal, stated in full so nobody has to go and find them.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** The lawn is
   a foundation whose events we observe and to which we contribute signed deltas. We never rewrite it,
   never read its current state, never make a feature depend on PvZ representing an RPG concept. "Can
   the lawn express a build?" is the wrong question; "does the RPG layer express it, and is that path
   wired?" is the right one.
2. **Standalone-first.** Every RPG feature must be fully playable and CI-provable **with the game
   closed**. The injector may *enrich* a feature — never *gate* one (`decisions.md`, *Standalone-first*
   row, 2026-08-21). This is the one invariant this feature, as described, currently contradicts. §4.
3. **One power ladder.** `Θ` is the single power index; `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2` the single
   magnitude function. **Contests read `Θ` (linear, difference-based); magnitudes read `P(Θ)`.** No
   subsystem owns a private `f(level)`, and §10 of the power SSOT is a **closed inventory** — a
   power-shaped number not in that table has no permission to exist yet.
4. **No hard progression ceilings.** Endless grind is the SSOT other systems reconcile *to*. A cap on a
   magnitude is a progression ceiling until proven otherwise; remove it or make it a configurable soft
   cap. Absolute bounds are derived and **throw, never clamp silently**. A ceiling need not be a `const`
   nor be named like one — **a flat rate facing a scaling sink is a cap**, and so is a threshold that
   halves a payout. This binds §5's escalating respec price directly.
5. **The balance surface is data.** Any number a balance pass would change lives in
   `data/tuning/<domain>.v{n}.json`, never as a `const`. A structural constant stays in code **and says
   why it is not tunable**. A missing tunable is a load rejection naming it, never a silent default.
6. **Magnitudes are `long`.** Widen before multiplying, divide by 1000 last and exactly once, let
   overflow throw. `float` stops being integer-exact at `Θ`=232 and per-mille `int` at 3,213 — both
   inside real play.
7. **Save inputs, never computed totals** (`stat-system.md`): persist the *allocation*, never the stats
   it composes to.
8. **An aptitude is a SOURCE, not a registered channel** (`decisions.md`, *Class system* row). An
   aptitude is never in `DerivedStatCatalog`, because `share` normalises over the actor's own total and
   a granted aptitude would silently dilute the other eleven.
9. **Scopes sum before share, never the reverse.** An actor's allocation is the sum of four scopes;
   `share` is taken on that sum (`AptitudeAllocation.cs:12-17`).
10. **No aptitude cap and no respec cap** (`decisions.md`, *Class system* row, PS-8). Respec is
    available, unlimited, and **priced in a resource fighting also costs** — not a cooldown, not a cap,
    not free.
11. **Win rate is the metric** — never fight length, damage dealt or kill time, and never under a clock.

---

## 1. What this program is for

**One sentence:** give every demon species its own aptitude allocation, filled automatically from a
per-species *build favour* as that species levels through play, with the Zomboss doing the same
visibly, and a priced respec for the player who wants to override it.

It is the join that makes three finished programs matter to each other:

- **`demon-seed`** classified 829 species and gave each one an `aptitudePrimary` — content with no
  consumer at the allocation layer.
- **`class-system`** built the twelve aptitudes, the four allocation scopes, the point budgets, the
  read functions and the Zomboss pattern table — a mechanism whose demon-facing half has no caller.
- **`RpgProgression`** already levels a plant type when you place it on the lawn — a signal whose level
  grants nothing.

Each is built. None of them is connected to the next. **This program is the wiring, plus four genuine
design decisions that the code is explicitly waiting on** (§4, §5, §6, §7).

---

## 2. The finding that reframes the request

**Four of the five mechanisms the owner described already exist in `src/` and have zero production
callers.** This is overwhelmingly a wiring program. Stated in the gate's own vocabulary:

| The idea, in the owner's words | Verdict | Evidence |
|---|---|---|
| *"the lawn game … level up demon specie by spawn them in the game"* | **built** — and it has been shipping for weeks | `RpgXpAwardMap.FromActivity` awards `(plant, typeId)` on `PlantPlaced` and `(zombie, typeId)` on `ZombieSpawned` — `src/FusionRpg.Core/Progression/RpgXpAwardMap.cs:38-41`. Applied at `src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:19-49`, stored in `rpg_actor_progression` (`RpgStore.cs:355-368`). Curve is arithmetic per kind, plant `80/32`, unlimited levels (`rpg-progression.md` §Curve) |
| *"we have 12 primary stats"* | **built** | `AptitudeCatalog.All` — twelve, as 3 postures × 4, `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:30-52`. "Primary stat" and "aptitude" are the same concept (`class-system/spec-primary-stats.md`, and the web page is titled "Primary stats") |
| *"demon have build favour so it will auto distribute the bonus primary stats"* | **built as a function — with the scope parameter already on it** | `ZombossPattern.ToAllocation(AllocationScope scope, long budget)` converts a per-aptitude permille share table into a real `AptitudeAllocation`, capped at the budget, widened-before-multiply, divided last — `src/FusionRpg.Core/Battle/Ai/ZombossPattern.cs:29-42`. **This is the auto-distributor, already written and tested.** It was written for the Zomboss; nothing stops it being called with `AllocationScope.DemonType` |
| the favour itself, per species | **built as data** — 829 species already carry it | Every anchor carries `aptitudePrimary` / `aptitudeSecondary` / `pure` (e.g. `SunFlower` → `Focus`, `none`, `pure: true`, `data/seed/demons/species/plant/sunflower-kin.json`). The primary/secondary split constant already ships: `impureSecondaryShareMilli: 300` (70/30), `data/tuning/demon-shape.v1.json` |
| the `DemonType` allocation scope | **wiring gap** | The scope exists (`AptitudeAllocation.cs:8`), its budget rate ships (`demonType: 4`, `data/tuning/aptitudes.v5.json`), and `PointBudget.PointsFor` ships with the source **already named in its own doc comment as "almanac XP"** (`src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs:12-18,31-39`). Only `Commander` is ever written — `src/FusionRpg.Server/AptitudeEndpoints.cs:76` |
| *"unlock feature allow user adjust distributions"* | **built for one scope** | `GET /api/aptitudes/{playerId}` · `POST /api/aptitudes/allocate` (`AptitudeEndpoints.cs:24,30`), SignalR `AptitudesUpdated` (`:68,70`), consumed live on the lawn (`AptitudeSubsystem` via `CheatState.cs:47-49`) and in web battle (`WebMatchService.cs:415-422`). Player-reachable surface is `ui/actor/ProgressionTab.tsx`; `layers/aptitudes/AptitudesLayer.tsx` is imported by nothing — **wiring gap** |
| *"the zomboss will do the same"* | **wiring gap** — nine patterns, no caller | `ZombossPatterns.cs:25-89` holds nine authored share tables (3 pure + 6 mixed), ordinal enumeration for determinism, throw-not-null resolution. `ZombossCommanderAllocation.cs:7-8` states in its own comment that these "already existed with ZERO production callers" |
| *"he will try to counter build user if he have lose streak"* | **real gap** | No adaptive selection exists anywhere. Live enemy composition comes from `WaveCatalog` (`src/FusionRpg.Core/Battle/WaveCatalog.cs:144`), not from any commander AI |
| *"we cost souls to respec, it will increase by demon specie level"* | **wiring gap + the exact decision the code is waiting for** | `RespecPolicy.PriceOf(tuning)` ships and returns a **flat** price with no level or scope argument — `src/FusionRpg.Core/Stats/Aptitudes/RespecPolicy.cs:32-36`, `respecPrice: 10`. Zero production callers. `RespecResource` has exactly one value, `Hunger`, and its own doc comment calls it a **"documented placeholder, not a code default masquerading as a decision"** and marks the choice **"Ask first"** (`RespecPolicy.cs:11-18`). §5 |
| a spawn event knowing which *species* it is | ⛔ **CORRECTED by the audit (A5) — this is `built`, not a real gap** | `LawnElementIndex` is exactly `(Side, GameTypeId) → DemonSpeciesDef`, built once from the catalog and already hosted injector-side, deliberately keyed on the pair because `polevaulterzombie` and `wallnut` are both type `3` (`src/FusionRpg.Core/Demons/LawnElementIndex.cs:5-45`). `StatContext` already carries `Side` and `TypeId` (`StatContext.cs:15-16`). Only the **transport** lacks a species dimension (`RpgClient.cs:363-374` is hard-coded to `Commander`) — a **wiring gap** |
| a level granting *anything* | **wiring gap** — the seam is built and empty | `static readonly LevelChangePipeline ProgressionPipeline = new();` with no handlers registered, `RpgStore.Progression.cs:17`. `progression.bonus.*` is likewise gated on a `Func<StatContext,int>?` delegate that nothing in production passes (`rpg-progression.md` §Combat power) |

**The single most useful sentence in this document:** the owner asked for an auto-distributor keyed on a
per-species favour, and `ZombossPattern.ToAllocation(scope, budget)` is that function, already written,
already taking the scope as a parameter, already obeying the overflow rules and the anti-cheat budget
cap. The favour data exists on all 829 species. The budget source is already named in code. What is
missing is a caller.

---

## 3. The build favour is real data — and it is badly skewed. Measured, not asserted.

Counted this session across the whole committed corpus (`data/seed/demons/species/**`, `_`-prefixed
files excluded), 840 rows:

| | Count | Share |
|---|---|---|
| **`pure: true`** (100/0 favour, single aptitude) | **666** | **79.3%** |
| impure (70/30 via `impureSecondaryShareMilli`) | 174 | 20.7% |

`aptitudePrimary` distribution — this is what "12 basic builds" actually looks like today:

| Aptitude | Species | Share |
|---|---|---|
| Onslaught | 332 | **39.5%** |
| Bulwark | 133 | 15.8% |
| Retribution | 113 | 13.5% |
| Focus | 89 | 10.6% |
| Precision | 50 | 6.0% |
| Fortitude | 49 | 5.8% |
| Pierce | 25 | 3.0% |
| Agility | 19 | 2.3% |
| *(unresolved)* | 11 | 1.3% |
| Vigor | 7 | 0.8% |
| Might | 6 | 0.7% |
| Composure | 4 | 0.5% |
| Ferocity | 2 | **0.2%** |

**Three consequences, and the third is the serious one.**

1. **"Twelve builds" is aspirational, not what the corpus holds.** Three aptitudes cover 68.8% of all
   species; four aptitudes (Vigor, Might, Composure, Ferocity) are carried by **19 species combined —
   2.3%**. A player fielding a normal roster would meet four of the twelve builds almost never.
2. **79.3% pure means auto-allocation produces corner builds by construction.** A pure species puts
   every earned point into one of twelve aptitudes. That is the definition of a corner.
3. ⛔ **The corpus auto-builds 133 species straight into the known-dominant corner.**
   `class-system-ideal.md` §0.0.3 records the measured result: **`Bulwark` beats all eleven other
   corners** on win rate with no clock. That finding is currently classed **SOFT** — an upper bound on
   severity, owned by the action/passive/skill layer, *"red by design today"* — and it is soft partly
   because **nothing today allocates for the player**. Auto-distribution changes that: it hands
   one-in-six species the dominant build automatically, without the player ever choosing it. **A SOFT,
   deferred finding becomes a LIVE one the day this feature ships.** That is not an argument against
   the feature; it is an argument that this feature and `residual-fit` are now coupled, and it should
   be said out loud in the spec rather than discovered in balance.

> **This section is the reason decision 3 (§3a) took the shape it did.** The owner's response to these
> numbers was not to pick a distribution rule but to require a **deterministic function that plans the
> redistribution and reconciles the balance**, and to rule out single-primary builds outright —
> *"current 12 primary stats distribution is most imbalance in the game."* Read §3a next; the table
> above is its input.

**Also worth naming:** this skew has the same shape as the one the rarity field showed before its
prompt was recalibrated (`fused` reached 55% of the corpus). Some of the Onslaught concentration is
plausibly a classification artifact rather than a fact about PvZ plants, and it is cheap to test — a
targeted `aptitude-primary` prompt-calibration pass, exactly like the one already run for rarity and
`sunwoven`. That is a `demon-seed` job, not this program's, but this program is its first real
consumer and therefore the reason to do it.

---

## 3a. The redistribution function — owner decision 3, and what it commits us to

**The owner rejected both offered shapes.** Not "follow the favour exactly", not "a bounded favour
rule" — instead:

> *"we need build a deterministic function, it will make re-distribution plan and reconcile the
> balance, current 12 primary stats distribution is most imbalance in the game"*
>
> *"i suggest avoid 1 primary stat distribution, it cause some unit too strong and too weak at same
> time, mix up build at primary build is better"*

**This reframes the favour from an answer into an input.** `aptitudePrimary`/`aptitudeSecondary` stop
being "the distribution" and become the *signal* a deterministic function reads when it plans one. That
is the same division of labour this repo already committed to everywhere else — the LLM classifies, a
deterministic generator makes it concrete, and no model ever picks a number (`SpeciesExpander`'s own
header: *"Model calls: none, ever — this module is the entire reason no model ever picks a number"*).
The favour is a classification; the distribution is generated.

It also answers §3's measured problem at the right layer. A static rule ("primary takes 60%") would
still hand 133 species the dominant corner, just at 60% instead of 100%. A function that *plans across
the corpus* can see that 39.5% of species want Onslaught and 2 species want Ferocity, and reconcile it.
The owner's own framing — *"current 12 primary stats distribution is most imbalance in the game"* —
says they consider this the game's largest live balance defect, which raises this from a nice-to-have
to the function's actual purpose.

### 3a.1 "No single-primary" has a technical fork the options did not surface

`pure` is not an inert label. It is **consumed today**, at generation time:

```csharp
var hasSecondary = !anchor.Pure && anchor.AptitudeSecondary is not null;
var primaryShareMilli = hasSecondary ? 1000L - shapeTuning.ImpureSecondaryShareMilli : 1000L;
```
— `src/FusionRpg.Core/Demons/Generation/SpeciesExpander.cs:48-50`

So 666 species (79.3%) already have **base stats** derived from a 100/0 split, before any allocation
exists. "Avoid 1 primary stat distribution" therefore has to declare its scope, and the two readings
have very different costs:

| Reading | What changes | Cost |
|---|---|---|
| **(a) Allocation layer only** | The new per-level points are always spread; base stats stay as generated | Cheap. No regeneration, no golden movement in the species corpus. But a pure species stays a 100/0 creature at its base, and the mixing only dilutes as it levels |
| **(b) Base generation too** | `pure`'s meaning changes, or `impureSecondaryShareMilli` applies universally | **Regenerates all 829 species' stats.** Every `data/generated/demons/*.json` moves; anything pinned to those numbers re-blesses. It is the honest reading of *"avoid one-stat units"*, and it is a real migration |

> ### ✅ Decided 2026-09-04: **(a) — allocation layer only, no regeneration**
>
> Owner: *"about regenerate, i don't think we need."* `pure` keeps its current meaning inside
> `SpeciesExpander`, every generated stat file stays byte-stable, and nothing re-blesses.
>
> **The consequence is worth stating plainly rather than leaving implicit:** base stats stay
> thematically skewed while allocations spread. A Sunflower remains a `Focus` creature *at its base*
> and becomes a mixed build *as it grows*. That is a coherent split — **identity is classified, growth
> is computed** — and it is the same division of labour decision 6 states for the LLM and the function.

### 3a.2 What the function needs before it can be specced

A deterministic redistribution function is not hard to write; it is hard to *aim*. Three things had to
be decided or it has no definition of done. **All three are now answered.**

**1. Where it runs — ✅ generation time, and its output is shipped static knowledge.**

> Owner: *"we should make the static knowledge to avoid user confuse when play game, they can adjust
> at they want, but need shipped static knowledge, avoid user much learn every time play the game."*

This is a **player-knowledge** argument, and it is a better one than the testability argument the
option carried. A species' build is a fact the player learns **once** and that stays true across
playthroughs — a Pokédex entry, not a per-run roll. It has three consequences the spec must honour:

- The plan is **content**. Changing it is a reviewable diff with a regenerable `--check`, exactly like
  `data/generated/demons/*.json` — never a silent per-run difference between two players.
- **Runtime randomisation of the baseline is ruled out.** Dragon Quest Monsters' random growth spurt
  (a random level 15–74, +10 in one of six attributes) is prior art this design deliberately declines,
  because a random baseline is unlearnable by construction.
- **The player override sits on top of the static plan**, never in place of it. "They can adjust at
  they want" presumes there is a stable thing to adjust *from*.

**2. Its objective — ✅ distribution parity: spread species across the twelve.**

Chosen over reconciling directly to the two existing criteria. `decisions.md`'s *Class system* row
fixes the metric as **win rate, never fight length or damage**, and names two criteria of unequal
standing — the **termination invariant** (HARD, blocks a build) and the **dominance matrix** (SOFT,
red today). The function targets neither directly. It targets the *input*: a corpus where one aptitude
is the primary of 39.5% of species and three others share 2.3% cannot produce balanced play whatever
the coefficients do.

**This is a deliberately modest claim, and stating it honestly matters more than overselling it:**
parity does not deliver win-rate balance, and must not be reported as if it did. It removes the input
that currently guarantees imbalance, and it is cheap to compute, cheap to verify, and checkable without
a combat simulation at all. The HARD and SOFT criteria stay owned exactly where they are today.

**2a. Parity over *what*, and how strictly — decided the same day.**

Two follow-ups were needed before "spread species across the twelve" is executable, because taken
naively it would have the function overriding the very classification it reads as input.

- **✅ Measured over TOTAL ALLOCATED POINTS across the corpus — never over the primary field.** Each
  species keeps its classified lean; because decision 3 removed single-primary builds, every species
  now has a **remainder**, and it is the remainders the function steers. The corpus-wide sum is what
  has to land inside the band. So the function never contradicts the favour — it decides what gets
  *added* to it. Reassigning primaries was explicitly rejected: it would make the classification
  meaningless and detach a species' build from its own lore.
- **✅ A BAND, not a point.** A floor and a ceiling, both tunable — not minimised deviation from an
  even 8.3%. Two reasons, and the second is the better one: a band is a **pass/fail check rather than
  an optimisation**, which is far cheaper to assert in CI; and it lets the corpus legitimately have
  more attackers than counter-attackers, which is a real property of PvZ plants rather than a defect
  to engineer away. `Ferocity` at 2 species still gets fixed, because the floor binds.

The floor and ceiling values are **tunables a balance pass owns** (`data/tuning/<domain>.v{n}.json`),
never constants in the function — the standing rule is that any number a balance pass would change
lives in config, and these two are the definition of that.

**3. Its relationship to `residual-fit`.** Unchanged and unasked, because it is not a fork: that module
(class-system module 12) already exists to *"simulate what the core cannot express, measure the gap, fit
the config to close it."* A corpus-wide redistribution function is the same job pointed at species
allocation instead of coefficients, and it should reuse that harness and `tools/CombatSim` rather than
grow a second balance engine. Note the two now have a clean division: **this function shapes the
inputs; `residual-fit` fits the coefficients.**

**Prior art for the bounded end state**, if the function's output needs a shape to aim at: Pokémon's
EV system caps at **252 per stat and 510 total** with **4 EV = 1 point**, so a fully-invested stat moves
by **+63** — deliberately bounded so no single stat can be everything. Natures add a flat **±10% on two
stats, 25 variants of which 5 are neutral**. Both are legible, both are bounded, and neither ever
produces a one-stat creature. Dragon Quest Monsters shows the randomized-but-bounded variant: a growth
spurt at a random level **15–74** granting **+10 in one of six attributes for 4 or 9 levels**.

---

## 4. The one real architectural tension: standalone-first

**The feature as described gates species progression on the lawn, and a locked invariant forbids
that.**

`decisions.md`'s *Standalone-first* row (2026-08-21) is explicit: *"every RPG feature must be fully
playable and CI-provable with the game closed — the injector may enrich a feature … never gate one."*

The block is one conditional, and it is deliberate:

```csharp
// Web-mode runs never level PvZ almanac type actors (audit 2026-08-21) —
// player-kind XP still flows (one economy); demon specimen XP is expedition-owned.
if (!pvzGame && award.Kind != RpgActorKinds.Player)
    continue;
```
— `src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:32-35`

So the exact signal the owner wants to build on — `PlantPlaced` → plant-type XP — **is lawn-only by
design today**. Build species levelling on it unchanged and the whole feature is unreachable with the
game closed, which is the invariant's definition of a gate.

This is a **wiring gap, not a wall** — one condition, and the surrounding machinery is side-agnostic.
What already exists on the game-closed side:

- **Expeditions are the shipped, player-facing, game-closed progression loop** — `ExpeditionEndpoints.cs:243,258,261`, resolver `ExpeditionResolver.cs`, `SpecimenXpPerBattleWon` at `:32,214`, applied at `RpgStore.Expeditions.cs:313-317`. It grants **instance** XP today, never species. **built** (the loop), **real gap** (a species signal from it).
- **Web battle resolves real matches server-side** — `BattleEngine.Resolve`, squad built from the real demon roster (`WebMatchService.cs:285-346`), ingested as real events. Its only entry point is inside the `/api/test` group and gated on `FUSIONRPG_SIM=1` (`WebMatchService.cs:495-520`, `SimFlags.cs:5-9`) — **wiring gap, default-off**.

**The shape of the answer:** a species earns build points from *fielding it in a resolved match*, and
the lawn is one source of that fact rather than the definition of it. The lawn then *enriches* (a
placement is a fact a lawn match produces) instead of *gating*.

> ### ✅ Decided 2026-09-04: **both** non-lawn sources
>
> Expeditions grant species points **and** web battle is promoted out of `FUSIONRPG_SIM=1`. The
> invariant is then satisfied twice over, and the feature is provable with the game closed by two
> independent paths rather than one.
>
> ⚠️ **Scope consequence, and it was resolved the same day rather than left to be discovered.**
> Promoting web battle means a real player-facing battle endpoint, and it is `/api/test`-only today —
> its own doc comment says *"no player-facing battle API in this module"* (`WebMatchService.cs:495-520`).
>
> **✅ Decided (decision 13): a separate program owns that promotion; this program declares the
> dependency.** This program needs the *signal*, not the *endpoint*. So concretely:
>
> - **What this program builds for standalone-first: the expedition path.** Expeditions already run
>   game-closed and already grant specimen XP; adding a species award alongside is the smallest change
>   that satisfies the invariant, and it needs no new player-facing surface.
> - **What it declares as a dependency: the web-battle path**, consumed when `standalone-rpg` (or
>   whichever program takes it) ships a real endpoint.
>
> Decision 2 is refined by this, not retracted — both sources are still wanted; only the ownership of
> the second one moved.

---

## 5. Respec: the decision the code was waiting for, and what the genre says about the curve

### 5.1 What ships today

`RespecPolicy.PriceOf(tuning)` returns `RespecPrice(RespecResource.Hunger, tuning.PointEconomy.RespecPrice)` —
flat, `respecPrice: 10`, **zero production callers** (`RespecPolicy.cs:32-36`). Two properties matter:

- **`RespecResource` has exactly one value and it is explicitly provisional.** Its own doc comment:
  *"documented placeholder, not a code default masquerading as a decision … §8 marks 'which resource
  respec costs' an 'Ask first,' a mechanism choice this module cannot make alone"* (`RespecPolicy.cs:11-18`).
  **The owner's souls proposal is answering a question the code wrote down and left open.** It is not a
  conflict with a shipped decision; it *is* the decision.
- **`PriceOf` takes no level and no scope.** "Increases by demon species level" is a signature change
  plus a curve — and a curve derived from a level is bound by principle 3: it goes through `P(Θ)`, or
  it earns a reviewed row in the power SSOT's §10 closed inventory. It may not be a private `f(level)`.

### 5.2 Souls vs. hunger — the locked rule is narrower than it looks

The lock reads: respec is *"priced in a resource fighting also costs"* (`decisions.md`, *Class system*).
Read in its own section (`class-system-ideal.md` §7b.5), the reasoning is that free respec collapses
free build into *"a lookup table keyed on the opponent, and every arrow of the cycle becomes a menu
option."* The friction must make a build a commitment.

- **`hunger` satisfies the rule literally** — it is one of the six actor resources, spent by fighting,
  and *is* Sun on the plant side.
- **Souls satisfy it only by opportunity cost.** Fighting *earns* souls (`SoulEarnPolicy`, +per kill,
  +per victory); nothing spends them in a fight. Their sink is summoning. A souls-priced respec
  therefore trades against *pulls*, not against *fighting*.

That is a real distinction, not a technicality: hunger-priced respec makes respeccing cost you the
fight you are in; souls-priced respec makes it cost you a demon you would otherwise have summoned.
**Both are defensible frictions. They are not the same friction, and only one of them is what the
locked sentence says.**

> ### ✅ Decided 2026-09-04: **souls**, and the lock is amended with it
>
> The deciding argument is one the offered options nearly buried: **hunger regenerates.** Respec is a
> *between-fights* action, and between fights a hunger pool is full — so a hunger price is close to no
> price at the exact moment it is meant to bite, which fails §7b.5's whole purpose (making a build a
> commitment rather than a menu selection). Souls are persistent and scarce, so the friction survives
> the gap between fights.
>
> **Two propagations owed when this graduates**, neither optional: `decisions.md`'s *Class system* row
> must have *"priced in a resource fighting also costs"* amended to record that souls satisfy it by
> **opportunity cost** rather than literally; and `spec-soul-economy.md`'s *"Ask first: new spend
> sinks"* is answered by this decision, with the spend taking **its own feature endpoint and reason**
> — that spec is explicit that spends are never a generic endpoint.

Note also `spec-soul-economy.md`'s own Boundaries: *"Ask first: … new spend sinks."* A respec sink is
an ask-first item there too, and spends are deliberately **not** a generic endpoint — *"only feature
endpoints spend, each with its own reason"*.

### 5.3 ⛔ An escalating respec price is the one mechanic here with two documented industry reversals

| Game | The escalating cost | What happened |
|---|---|---|
| **World of Warcraft** (vanilla → WotLK) | **1 → 2 → 5 → 10 → … → 50 gold**, decaying over time | **Removed in Legion.** Stated purpose had been *"to reinforce a bit of spec identity … and to serve as a mild gold sink"*; removed after feedback it was too restrictive in practice ([mmo-champion](https://www.mmo-champion.com/content/5701-Respec-and-Talent-Swap-Cost-Changes)) |
| **Path of Exile 2** (0.1.0e) | gold per point, scaled by character level | **Curve flattened ~40–50%.** GGG: the cost *"had a relatively aggressive curve getting more expensive with character level … we have flattened that curve so it doesn't exponentially grow as much."* Trigger: **balance nerfs forced rebuilds players could not afford** ([patch notes](https://www.pcgamesn.com/path-of-exile-2/patch-notes-respec-cost)) |
| **Guild Wars 2** | zero, anywhere out of combat | Stated rationale: *"cost hinders experimentation"* ([ArenaNet](https://www.guildwars2.com/en/news/say-goodbye-to-armor-repair-costs-and-hello-to-free-trait-resets/)) |
| **Etrian Odyssey** ("Rest") — the closest analogue, since it prices respec in **levels** | full SP refund for **−10 levels** (EO1) → **−5** → **−2** in the 3DS entries | The series **reduced its own level-scaled respec cost three times** |
| Diablo 2 (1.13) | 1 free per difficulty (max 3), then Token of Absolution (4 Essences, Hell act bosses) | unlimited but effort-gated |
| Diablo 3 | free | Jay Wilson had wanted a higher price; shipped free |
| Grim Dawn | 25 Iron Bits/point early, rising with points reassigned, **capped at 15,000/point** | a soft cap, not a wall |

**Two independent reversals of exactly the proposed shape, for the same stated reason each time: a
level-scaled respec cost taxes experimentation, and it bites hardest precisely when a *designer's*
balance change forces a rebuild the player did not choose.**

### 5.3a ⭐ But this repo has already decided this exact shape once, and it decided *for* it

Read after the first draft of §5.3, and it changes the question. `power/ssot-power-scale.md` §11.2
("Progression ceilings — all decided 2026-08-23: soft caps, never hard") settles the **enhancement
`+X`** ceiling in precisely the shape an escalating respec price would take:

> **Decided: uncapped, with a risk formula as the soft cap.** Success rate falls per level, failure can
> break the item or drop a level, and every rate and cost is **configurable** — the throttle is the
> **expected cost per level, which rises without ever hard-stopping**.

So a cost that rises with level is **already precedented and already sanctioned in this repo**, on two
conditions that are the whole of the decision: it must **never hard-stop**, and **every rate must be
configurable** rather than a constant. That is a much more useful answer than the genre's, and it
reframes §7's question 2: not *"may the price scale?"* — it may, by existing precedent — but
**"what curve, measured against what income, and where is the escape valve when a balance patch forces
a rebuild?"** GGG's free-respec-on-rebalance is the documented answer to the third part.

One live tripwire worth carrying into the spec: `DerivedStatDef.Cap` is nullable and **no channel sets
one today** — `DerivedComposer.cs:71-72` applies `Math.Min(value, def.Cap.Value)` when one is set, and
§11.2a classes it *"not a conflict yet — a facility … governed by PS-8 the moment one does."* If this
feature is ever tempted to cap a `progression.bonus.*` channel, that is the line it trips.

And there is a repo-internal version of the same hazard. Principle 4 is explicit that **a flat rate
facing a scaling sink is a cap**.

> ⛔ **Corrected by the §11 audit (A3).** This paragraph originally also cited *"tuning cannot fix a
> growth-rate mismatch"* here. That citation was wrong: `economy-principles.md`'s **P2** states that
> rule in one direction only — a *faucet* that scales needs a *sink* that scales — and **no principle
> governs a sink outrunning its faucet.** The rule that actually applies to an escalating respec price
> is **PS-8**. The hazard below is real; the authority for it is PS-8, not P2.

If respec price grows with species level while soul income grows with
`contentScale(Θ)`, then the *ratio* of the two decides whether respec becomes progressively
unaffordable — a progression ceiling arrived at by arithmetic, which PS-8 forbids. **If an escalating
price is adopted, the growth rates of price and income must be compared in the spec, not left to
tuning**, and GGG's own escape valve (a free full respec when the designers move the balance) is the
documented mitigation.

---

## 6. The adaptive Zomboss — where the genre says the line is

### 6.1 What already exists

Nine patterns, authored as permille share tables with an `AuraId`, ported from this repo's own measured
archetypes (`ZombossPatterns.cs:25-89`): `force-pure`, `finesse-pure`, `bastion-pure`, plus six mixed —
the only three (defence, breaks) pairs that are not self-cancelling, each in a guard-leaning 60/40 and
an aggro-leaning 40/60 variant. `ZombossPattern.ToAllocation` turns any of them into a real allocation
at any budget. **All of it has zero production callers** (`ZombossCommanderAllocation.cs:7-8`).

So "the zomboss will do the same" is already true in code, and *symmetric with the player by
construction* — the same `AllocationScope`, the same `PointBudget`, the same read functions.

### 6.2 The fairness question is already settled; the legibility question is the live one

`class-system-ideal.md` §6.1 point 4 states the anti-cheat rule: *"A pattern is an allocation from the
same finite pool the player draws on, so a harder Zomboss is a higher `Θ` or a better allocation —
never a stat nobody could have had. Difficulty stays inside the rules."*

**A free Zomboss respec does not break that rule** — `ToAllocation` caps spend at the supplied budget,
so his build stays in-pool however often he changes it. The owner's *"zomboss will cheat"* is an
asymmetry of **adaptation speed**, not of magnitude. It is legal.

What it does collide with is the *other* half of §6, which is the stated reason patterns exist at all:
*"He needs to be **legible** first; a blind opponent that visibly acts on old information is more
interesting than a sharp one, and it is the only version that can be tuned."* A Zomboss who
re-allocates against your build on a hidden lose-streak counter is, by construction, illegible.

### 6.3 The genre evidence is unusually one-sided here

| System | What it adapts on | Reception |
|---|---|---|
| **Left 4 Dead's Director** | per-survivor **Intensity** (rises with damage/proximity, decays), cycling **Build Up → Sustain Peak → Peak Fade → Relax**; Relax lasts **~30–45s**. Booth's stated goals are *"Promote Replayability"* and *"Generate Dramatic Game Pacing"* — **pacing, never win rate** ([design writeup](https://www.centerconsulting.com/ai-library/concepts/l4d-director)) | The canonical *success* |
| **Resident Evil 4** | hidden **Rank 1–10** driving damage, crit chance, hitbox sizes and spawn tables; community-reverse-engineered, never published ([speedrun.com guide](https://www.speedrun.com/re4console/guides/3sahj)) | Tolerated because invisible — and only *because* it was invisible for years |
| **Mario Kart World** | player performance | *"there's no feeling of mastery … the AI keeps correcting its challenge in response to your improvements"*; read as *"playing against cheaters"* ([player thread](https://gamefaqs.gamespot.com/boards/507486-mario-kart-world/80995104)) |
| **EA's EOMM / matchmaking patents** | skill, aggressiveness, **frustration rate** | The most-cited example of adaptive systems producing player distrust ([PCGamesN](https://www.pcgamesn.com/ea-matchmaking-microtransactions-eomm-engagement-patent)) |

DDA research states the mechanism directly: systems *"that raise difficulty in lockstep with player
improvement may obscure growth"*, leaving players *"treading water"* and questioning *"whether their
progress is truly earned"*; players detect the adjustment even when uninformed, and the literature's
recommendation is **transparency** ([IntechOpen](https://www.intechopen.com/chapters/1228576)).

**The split that matters: adaptation aimed at pacing and variety survives scrutiny. Adaptation aimed at
win rate does not.**

The owner already proposed both halves — *"he will try to counter build user if he have lose streak"*
(win-rate-targeting) **or** *"we will randomly change the build when he level up, this is randomly and
tunable"* (variety-targeting). **The second is the version the evidence supports**, and it costs
nothing to make the first survivable too: announce the pattern before the fight rather than after.
§6.1 point 3 of the class-system ideal already requires exactly that — a counter-build is *"only a
decision if the pattern is stable enough to recognise, and only fair if it is announced before the
fight rather than discovered during it."* A named, visible Zomboss pattern that rotates is legible; a
silent one that tracks your win rate is the thing four of the four systems above got punished for.

There is a second-order effect worth stating plainly: **a perfectly adaptive counter-builder makes
every player build equally bad**, which destroys the RPS the free-build design depends on (§7b.5's
argument, from the other direction). Whatever adaptation ships needs a rate limit, and that rate limit
is a tunable, not a constant.

---

### 6.4 The owner's answer — both adaptations, revealed one fight late

**Decision 4, 2026-09-04: Zomboss does both — rotate on level-up *and* counter-build on a lose streak
— and his pattern is announced *after the next fight*.**

This is not the "announced before" option that was offered, and the difference is the design. A
delayed reveal produces a **symmetric one-fight information lag**:

| | What it knows | What it acts on |
|---|---|---|
| **Zomboss** | the build you beat him with last time | one-fight-old information |
| **Player** | the pattern he ran last time | one-fight-old information |

**Neither side holds current information about the other.** He is not reading your build in real time,
and you are not reading his — you are each countering the other's *previous* commitment. That is a
genuinely different mechanism from the four systems §6.3 records as backlash cases, and it lands almost
exactly on the principle the class-system already borrowed from `world/spec-ai-commander.md`:

> *"He needs to be **legible** first; a blind opponent that visibly acts on old information is more
> interesting than a sharp one, and it is the only version that can be tuned."*

Here that property is made mutual. The DDA literature's complaint is specifically about adaptation that
stays **hidden** — players *"question whether their progress is truly earned"* and read undisclosed
adjustment as cheating. Lagged-but-disclosed adaptation is not that: the player always finds out, the
information is simply one fight stale, and staleness is a *tunable* (reveal after one fight, after the
match, immediately) rather than a secret.

**Two things the spec still owes this decision:**

- **A rate limit.** Perfect adaptation, even lagged, converges on "every player build is equally bad",
  which destroys the RPS that free build depends on (`class-system-ideal.md` §7b.5's argument, arriving
  from the opposite direction). How often he may re-pattern, and how strongly a lose streak may bias
  the pick, are tunables — not constants, and not unbounded.
- **What "announced" concretely means on a surface.** GG-1 says it is a layer over whatever stage the
  player is on, never a screen they navigate to (§7a). A post-fight reveal is a natural fit for the
  battle-report path that already exists.

**Note it stays inside the anti-cheat rule either way.** `ToAllocation` caps his spend at the supplied
budget, so however often he re-patterns, his build is drawn from the same finite pool the player draws
on — *"a harder Zomboss is a higher `Θ` or a better allocation, never a stat nobody could have had"*
(`class-system-ideal.md` §6.1).

---

## 7. Decision record — every question and its answer

> ## ✅ As of 2026-09-04, the design questions are CLOSED — sixteen decisions (§0.0), the last three resolving the pre-spec audit.
>
> The list below is kept as the reasoning trail, and **every entry now carries its answer.** Nothing in
> it is still waiting on the owner.
>
> **What genuinely remains before a spec, and neither item is a design fork:**
>
> 1. **Three unread gate rows** — `design/spec-derived-stat-sheet.md` (the render surface),
>    `empire-economy-ssot.md` + `economy-principles.md` (the faucet/sink reasoning decision 9's price
>    pins against), and `event-pipeline-v2-ssot.md` + `overlay-control-loops.md` + `pvz-middle-layer.md`
>    (the event contract §4 rests on). §10 states the exposure each one leaves.
> 2. **Tunable sizing, which a balance pass owns and an ideal doc must not invent** — the parity band's
>    floor and ceiling, the respec price's income fraction, and the Zomboss re-pattern rate limit. Each
>    is a `data/tuning` row by rule, not a design decision.
>
> **No further owner decision is outstanding.** Manufacturing one here to look thorough would be the
> defect this repo already names: an answerable question is a task, and a recommendation nobody
> disputed is a decision.

**Original six, with their answers.**

| # | The question | Answer |
|---|---|---|
| 1 | Which resource prices respec — souls or hunger? | ✅ **Souls** (decision 1). The deciding argument: hunger *regenerates*, so it is close to no price at the between-fights moment respec actually happens |
| 2 | If the price scales with species level, what curve? | ✅ **Pinned as a fraction of expected soul income** at that level (decision 9) — the mismatch becomes structurally impossible rather than a tuning problem |
| 3 | What is the non-lawn source of species build points? | ✅ **Both** expeditions and web battle (decision 2) — with **expeditions built here** and **web battle a declared dependency on another program** (decision 13) |
| 4 | Does the favour distribute directly, or softened? | ✅ **Neither — a deterministic function plans it**, and single-primary builds are out (decision 3) |
| 5 | How adaptive may the Zomboss be? | ✅ **Both adaptations, revealed one fight late** (decision 4) — producing a symmetric information lag rather than a hidden one |
| 6 | How is a `DemonType` allocation keyed? | ✅ **Per-player, by `speciesId`** (decision 10); global was ruled out so one player's respec cannot change another's roster |

**Created by those answers, and also closed:**

| # | The question | Answer |
|---|---|---|
| 7 | Does "no single-primary" bind base generation too? | ✅ **Allocation layer only, no regeneration** (decision 5). Base stats keep their skew; identity is classified, growth is computed |
| 8 | Can the LLM help rebalance the skew? | ✅ **No** (decision 6) — the model classifies a category; balance is arithmetic. No prompt-calibration pass |
| 9 | Generation time or runtime? | ✅ **Generation time, shipped as static knowledge** (decision 7), on a player-knowledge argument: a species' build is learned once and stays true |
| 10 | What does the function reconcile to? | ✅ **Distribution parity** (decision 8) — explicitly *not* a win-rate claim; it removes the input that guarantees imbalance |
| 11 | Parity over what? | ✅ **Total allocated points, never the primary field** (decision 11) — the function steers remainders, so it never overrides its own input |
| 12 | How strict? | ✅ **A band with a tunable floor and ceiling** (decision 12), not minimised deviation — a pass/fail check, and the corpus stays allowed to have more attackers than counter-attackers |

**Deliberately *not* open**, because they are already answered and re-asking them would reopen a lock:
the twelve aptitudes and their ids; scopes-sum-before-share; commander-smallest-through-unique-largest;
no aptitude cap, no respec cap, no respec cooldown; win rate as the metric; an aptitude being a source
rather than a registered channel.

---

## 7a. How a species bonus reaches a player — the two rules that already govern it

Added after closing the *Stats* and *UI* gate rows, because both halves turned out to be already
specified rather than open.

**Reaching the game.** `actor-hub-ssot.md:63` states the ban plainly: *"progression must never write
bare `hp`/`maxHp`/`atk` or mutate Y0. Combat flats use **`progression.bonus.*`** only."* The layer
order is `Y0 → RuntimePrimary → Derived → AppliedCombat`, where `AppliedCombat = RuntimePrimary +
progression.bonus.*` is the Writer's input (`:55-61`), and the five channels already exist:
`progression.bonus.{maxHp,atk,defense,arm1,arm2}` (`:93-97`). So the path from "this species levelled"
to "this plant hits harder on the lawn" is fully specified and already registered — what is missing is
only a producer, which is §2's empty `LevelChangePipeline`.

**Reaching the player's eyes.** The render vocabulary also already exists. `AptitudePoints` is the
**eleventh `UnitClass`**, authorised 2026-08-26 by the class-system program, and the contract union in
`web/.../contract/types.ts` already carries `"aptitudePoints"`
(`design/spec-magnitude-and-units.md:83,101-107`). Its rule is a real constraint on this feature's UI:
an `AptitudePoints` figure renders its context part as **an estimate**, and is *"allowed only on a
surface with a real allocation"* — i.e. the display may say `Might 55 → +2,200 omni power` only where
an actual allocation backs it, never as a speculative preview.

**Where the surface may live.** `game-gui-principles.md` GG-1 is binding: the player is on exactly one
**stage**, and every other surface is a **layer drawn over it**, openable from anywhere, closing back
to the identical stage state — *"a player mid-wave who wants to check a demon's loyalty must not lose
the wave to do it."* It explicitly forbids *"routing to a sibling screen in order to look at
something."* GG-10 caps depth at **three pushes** from the stage. The owner's *"unlock feature allow
user adjust distributions"* is therefore a layer, not a page — and note that the shipped-but-unimported
`web/.../layers/aptitudes/AptitudesLayer.tsx` is already named for exactly that shape, while the
currently-reachable copy lives inside `ui/actor/ProgressionTab.tsx`. Which of the two becomes the real
surface is a `commander-surface`/`actor-sheet` question this program should not answer alone.

---

## 8. What this program is not

- **Not the passive layer.** [passive-tree-ideal.md](passive-tree-ideal.md) (captured 2026-09-04) owns
  build trees and passive skills — the layer `class-system-map.md` §5 reserved to fill the dominant
  corner. This program allocates points; that one decides what a point can additionally buy.
- **Not the commander UI.** [commander-surface-ideal.md](commander-surface-ideal.md) owns the
  commander-scope surface, persistence and async handoff.
- **Not `aspect-scope`.** The fourth allocation tier was **reverted 2026-08-31** and is explicitly *"not
  authorized to build"* (`decisions.md`, *Demon program* row). This program touches `DemonType` only.
- **Not a re-run of `residual-fit`.** It does not retune coefficients. It does, per §3, hand
  `residual-fit` a much more urgent reason to run.
- **Not a new curve.** Species level → points must reuse `Θ`/`P(Θ)` and `PointBudget`; a private
  `f(level)` here is the exact defect the power SSOT exists to prevent.

---

## 9. Prior art worth carrying into the spec

Beyond §5.3 and §6.3, three findings bear on the design directly.

**Use-based progression's named failure mode is degenerate self-harm, and it is old.** Final Fantasy II
grows stats from what you do: max HP rolls on losing roughly **≥1/9 of max HP** in a battle, and the HP
gained equals the character's Stamina — which itself rises from losing HP. The documented result is
party members attacking *each other*, because enemy damage is slower and less controllable
([RPG Site](https://www.rpgsite.net/feature/11516-final-fantasy-ii-how-to-level-up-and-an-explanation-on-the-leveling-system)).
Skyrim's version is `SkillImproveMult × (skill ^ 1.95)`, gamed by the alchemy/enchanting restoration
loop at **40,000%+** effect, taking Alchemy 15 → 100 *"in seconds"* ([UESP](https://en.uesp.net/wiki/Skyrim:Leveling)).
**The lesson for "level a species by spawning it": the XP unit is a placement, so the cheapest species
spammed in a safe corner is the optimal grind.** The existing award is flat +8 per placement
(`rpg-progression.md`), i.e. exactly the shape that rewards volume over engagement. SaGa's counter is
worth noting: **Battle Rank 1–9** rises as you fight, so grinding raises the opposition too.

**Diablo 3 removed manual stat allocation, and its stated reasons are the owner's reasons.** Jay
Wilson: allocation *"was a way to break your character if you didn't know how to play"*, and for
players who did know it collapsed to one correct answer — *"this is exactly what you need for X
build"* — so it was not a real choice; predictable stats-per-level then let them design items, moving
customization there
([PureDiablo](https://www.purediablo.com/jay-wilson-exclusive-part-iv-skills-and-stats)).
**This is the strongest external support for the owner's "auto distribute to avoid overwhelming
users."** It also names the replacement: when you take allocation away, the customization has to
reappear somewhere — which is what `passive-tree` and the item program are for.

**Roster scale: the deployed cap clusters at 6–12, and owned-to-deployed runs 6–8×.** Pokémon parties
are **6** with unbounded storage; Disgaea deploys **10**; Fire Emblem's largest roster is **77
recruitable but never more than 12 deployed**, and the community's named failure mode is directly
relevant — *new recruits feel pointless because the deployment cap means they will never be used*
([Serenes Forest](https://forums.serenesforest.net/topic/94830-roster-size-and-deployment-limits-less-or-more/)).
With 829 species and no deployment cap in the RPG layer, "which species do I actually invest in" is a
question this feature creates and does not answer. No academic unit-count threshold was found; the
game-design cognitive-load literature is qualitative.

Dragon Quest Monsters is the one shipped example of a *randomized* auto-growth flourish: every monster
gets a growth spurt at a random level between **15 and 74**, granting **+10 in one of six attributes
for either 4 or 9 levels** ([Game8](https://game8.co/games/DQM-Dark-Prince/archives/438970)) — a
precedent for making auto-allocation feel like an event rather than a spreadsheet.

---

## 10. Pre-proposal checklist (DESIGN-GATE §5)

```
[x] I identified the subsystem(s) this touches.
      stats/aptitudes · power ladder · caps · tunables · soul economy · demon-seed ·
      injector event pipeline · match lifecycle · standalone/web RPG · player GUI
[~] I read every doc in the §1 row(s) for those subsystems, this session.
      ⛔ PARTIAL — and this box was ticked [x] in the first draft, which was wrong. The list below
      is the corrected record, after a second pass closed the four highest-risk rows.
      READ IN FULL: DESIGN-GATE · decisions.md (both pages) · stat-system.md ·
        rpg-progression.md · spec-soul-economy.md.
      READ IN THE PART THAT GOVERNS THIS FEATURE (sections named inline where cited):
        class-system-ideal.md §0.0-0.1, §6-6.3, §7c, §7b.5 · class-system-map.md §2, §5, §6 ·
        actor-hub-ssot.md §2 layer table + the progression.bonus.* ban + the channel rows (§7a) ·
        design/spec-magnitude-and-units.md §3 UnitClass table + the AptitudePoints rows (§7a) ·
        power/ssot-power-scale.md §11.2/§11.2a caps register (§5.3a — this one CHANGED an answer) ·
        tunables-ssot.md §0-§1 (the three classes and the test) ·
        game-gui-principles.md GG-1 + GG-10 (§7a).
      READ VIA THE §11 AUDIT, and this distinction is deliberate rather than a dodge:
        empire-economy-ssot.md + economy-principles.md (Economy) ·
        event-pipeline-v2-ssot.md + overlay-control-loops.md + pvz-middle-layer.md (Injector)
        ⚠️ These five were read by delegated audit passes that reported specific rules with
        line citations, NOT read start-to-finish by the author of this document. Every
        load-bearing claim taken from them was then re-verified against CODE first-hand
        (that is how A3's bad citation and A5's wrong verdict were caught, and how one
        agent claim — a live contract-slot ceiling — was found FALSE and discarded). That
        is stronger than an unread row and weaker than a read one. Treat A1-A11 as
        evidenced; treat any FUTURE claim sourced from these five as needing the same
        first-hand check.
      STILL NOT READ, each a named §1 row this feature touches:
        software-architecture.md (Anything at all)
        design/spec-derived-stat-sheet.md (Stats) — its sibling was read, this one was not
        power-map.md (Power) · demon-seed-map.md + demon-seed/ specs (Demon species generation)
        match-runtime.md + unique-actor-runtime.md (Match lifecycle)
        design/information-architecture.md + fe-game-foundation.md (UI)
        standalone/spec-standalone-charter.md + standalone-rpg-map.md (Standalone) — only
          decisions.md's own Standalone-first row was read, and §4 rests on it plus a code read
      Consequence, stated rather than hidden: every built/wiring-gap/real-gap verdict rests on
      first-hand CODE reads and stands on its own. The residual exposure is narrow and named —
      the render surface's own spec (spec-derived-stat-sheet.md), the economy rows behind §5.2's
      faucet/sink reasoning, and the injector-pipeline rows behind §4's "the lawn is one source of
      the fact" framing. Close those three before a spec commits to a surface, a price, or an
      event contract.
[x] I checked decisions.md for a lock covering this.
      Five apply and are quoted inline: Class system · Standalone-first · Caps ·
      Power scale · Magic numbers. None is contradicted silently; §4 names the one
      tension (standalone-first) explicitly rather than working around it.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments.
      Two comments were found stale and are NOT relied on: CommanderAllocationSource.cs:9-10
      and AptitudeSubsystem.cs:15-17 still claim "zero production callers" for the allocation
      store, which is false today (AptitudeEndpoints.cs:52,80 and four more call it).
      CheatState.cs:39-44 already corrects them.
[x] I read the surrounding section of every rule I quoted.
      §5.2 in particular reads §7b.5 whole rather than quoting the one-line lock.
[~] I tested (not assumed) any constraint I am reporting.
      The corpus skew in §3 was MEASURED this session by counting the committed corpus
      (840 rows), not quoted. The standalone-first block was read as code
      (RpgStore.Progression.cs:32-35), not inferred. NOT tested: whether wiring the
      DemonType scope moves any golden — no code was written, so nothing was run. That
      belongs to the spec, and it should be checked rather than assumed, since
      AptitudeChannelMods already feeds real battle setups.
[x] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
      §4 names the standalone-first contradiction as the feature's central open problem.
[~] Corrections are propagated.
      This is an ideal doc; nothing outside it is amended yet, by design. FOUR propagations
      are now OWED if this graduates, and decisions 1-4 (§0.0) are what make them owed:
        1. decisions.md "Class system" row — "priced in a resource fighting also costs"
           must record that SOULS satisfy it by opportunity cost, not literally (decision 1).
        2. spec-soul-economy.md — its "Ask first: new spend sinks" is answered; the respec
           spend takes its own feature endpoint and reason, never a generic one (decision 1).
        3. class-system-map.md §2 module 14's "named, undecided follow-ups" line — this
           document is the answer to it.
        4. A decisions.md row is owed for the web-battle promotion (decision 2), since it
           changes what the server exposes to players.
```

**Honest gaps, stated rather than hidden:** no code was written or run, so no golden-movement claim is
made either way. The `aptitudePrimary` skew is measured but its *cause* is not established — a
classification artifact and a real property of the corpus would look identical from the counts alone,
and distinguishing them needs a `demon-seed` calibration pass, not an assertion here.

---

## 11. Pre-spec audit, 2026-09-04 — two decisions do not survive it

Run at the owner's request before `/spec`, as an adversarial pass rather than a review. Two of the
thirteen decisions were **not executable as written** (all three findings have since been re-decided — see §12), one claim in this document was **wrong**, and one
suspicion the audit itself raised turned out to be **backwards**. Everything below is verified against
code.

### A1 ⛔ CRITICAL — the `DemonType` budget source inverts a locked ordering by ~176×

`PointBudget.PointsFor` is `sourceValue × rate` with **no unit conversion** — the tuning type's own doc
is explicit: *"a shipped rate of `3` means exactly 3 points per source unit"* (`AptitudeTuning.cs:26-32`).
But the four scopes' sources are **not in the same units**:

| Scope | Source (`spec-point-economy.md:37-39`) | Shape | Value at a normal mid-game point |
|---|---|---|---|
| Commander | `Θ_player` | an **index** | ~20 |
| **DemonType** | **type almanac XP** | an **accumulation** | **2,640 at species L12** |
| UniqueDemon | specimen level | an **index** | ~20 |

The plant XP curve is `XpToNext(L) = 80 + (L−1)×32` (`rpg-progression.md` §Curve), and that same doc's
balance note puts a player at **L12–20 after 20 matches** — so these are ordinary values, not extremes:

| Species level | Cumulative XP | DemonType budget (XP × 4) | Commander budget (Θ=20 × 3) | Ratio |
|---|---|---|---|---|
| 10 | 1,872 | 7,488 | 60 | 125× |
| **12** | **2,640** | **10,560** | **60** | **176×** |
| 20 | 6,992 | 27,968 | 60 | 466× |
| 30 | 15,312 | 61,248 | 60 | 1,021× |

This **inverts the locked ordering** — *"the commander tier is the SMALLEST and the unique tier the
LARGEST"* — which exists for a stated reason: a commander allocation replicates across the whole roster,
so a dominant one is the worst case. Wiring the DemonType source with raw XP makes the species tier
dominate everything else by two to three orders of magnitude.

**And the guard test cannot see it.** `PointBudgetTests.cs:84` holds the source constant on purpose:

```csharp
const long sameSourceValue = 100; // isolates the RATE ordering from any per-scope source difference.
```

It proves `3 < 4 ≤ 4 < 6`, which is true and does not imply the budget ordering the test is named for
(`Commander_budget_is_smallest_and_unique_largest`). The test is not wrong about rates; it is
**measuring the wrong thing for its own claim**, and it stays green straight through this defect.

**Fix, and it is forced twice over: the source must be an INDEX, not an accumulation** — species
*level*, or a Θ-shaped index derived from it. That restores the ordering (60 &lt; 80 &lt; 120 at L20) and is
independently required by the one-power-ladder rule, since cumulative XP is not `Θ` and a budget derived
from a level may not take a private path. **The spec must also fix the test** to compare real budgets
from real sources, or it will keep certifying an ordering it never checked.

### A2 ⛔ CRITICAL — decision 9's respec price is not a computable quantity

*"A fraction of expected soul income at that species' level"* cannot be evaluated, for two independent
reasons:

1. **Wrong scope.** Souls are **player**-scoped (`empire-economy-ssot.md:36`); species level is
   per-species. A player holds one balance across dozens of species at different levels, so "income at
   that species' level" names nothing.
2. **The faucet is flat today.** `SoulEarnPolicy.KillEarn` is
   `ContentScale.Apply(KillDelta, ContentScale.Milli(thetaEnemy))` (`SoulEarnPolicy.cs:74-75`), and
   `contentScale`'s **sole argument is Θ** (`ContentScale.cs:15-20`) — but every live earn passes
   `const int VanillaPvzKillAndRunTheta = 20` (`RpgStore.Souls.cs:29`, used at `:57,:58,:70`), the
   calibration pin, so `contentScale = 1.000`. **Soul income today is a constant**: 1/kill, 100/victory.
   There is no growth curve to take a fraction of.

Three reformulations, with verdicts:

| Option | Verdict |
|---|---|
| Fraction of the player's **current balance** | Well-defined and never a ceiling — but it can never *bind*, so it deletes the friction §5.2 says respec exists to create, and it is gamed by spending first |
| **Pinned to `Θ`** — `price = k × contentScale(Θ)` | The only **type-correct** option: Θ is exactly what income scales by, so the ratio is constant by construction and PS-8 is satisfied structurally. **Honest rider:** Θ is unwired at the pin today, so it degenerates to a flat price until the vanilla-PvZ Θ signal lands — a follow-up the power program already has open |
| Scaled by the species' **accumulated points** | Well-defined, but total spend is `O(species × points)` against `O(1)` income — **precisely the ceiling PS-8 forbids** |

**This needs an owner decision (§12).** As written, decision 9 cannot reach a spec.

### A3 ⛔ A citation in this document was wrong, and it was mine

§5.3a and §7 both cited *"tuning cannot fix a growth-rate mismatch"* as the rule against a **sink
outrunning a faucet**. Read in its own section, `economy-principles.md`'s **P2** states the opposite
direction only — *"A faucet that scales with holdings needs a sink that scales with holdings"* — and its
metric row measures income growth against upkeep growth. **There is no principle governing a sink that
outruns its faucet.** The rule that actually applies is **PS-8** (a cost that outruns its income becomes
a progression ceiling). Corrected in place; recorded here because quoting a rule without reading its
section is exactly the failure DESIGN-GATE evidence rule 3 names.

### A4 — `TrySpendSouls` has zero production callers

Every real sink bypasses it and appends to the ledger directly: summoning (`RpgStore.Summons.cs:91`),
fusion (`RpgStore.Fusion.cs:380`), contract upkeep and slots (`RpgStore.Contracts.cs:160,267,320,437`),
patron (`RpgStore.Patron.cs:55`). A spec naming `TrySpendSouls` as the respec seam would be specifying
against unused API. **Use the path the shipped sinks use, or wire that one deliberately.**

*(A claim that contract slots carry a live `"capacity.max"` hard ceiling was checked and is **false** —
`ContractPolicy.CanBuySlot` returns `true` unconditionally, so that cap was properly removed and only an
unreachable error branch remains. Dead code, not a PS-8 violation.)*

### A5 ✅ Correction — the species join is BUILT, not a real gap

§2's last row called "a spawn event knowing which species it is" a **real gap**. That is wrong.
`LawnElementIndex` is exactly `(Side, GameTypeId) → DemonSpeciesDef`, built once from
`DemonSpeciesCatalog.All`, already hosted injector-side, and already deliberately keyed on the pair
because *"`polevaulterzombie` and `wallnut` are both `3` in the shipped roster"*
(`src/FusionRpg.Core/Demons/LawnElementIndex.cs:5-45`). `StatContext` already carries both `Side` and
`TypeId` (`StatContext.cs:15-16`). **Verdict corrected to `built`.** What is missing is only the
*transport*: `/api/aptitudes/{playerId}` returns a flat share map hard-coded to `Commander`
(`RpgClient.cs:363-374`), with no species dimension — a **wiring gap**, and it makes this feature
materially cheaper than §2 claimed.

### A6 ⭐ The perf suspicion was backwards — and the fix makes the feature a net win

The worry was that per-species allocation adds a per-entity resolve. It does not: **the aptitude
subsystem already resolves per entity, per apply.** `AptitudeSubsystem.ContributeDerived` calls
`AptitudeResolver.Resolve` on every call (`AptitudeSubsystem.cs:51-57`), which loops every tuning edge
(~486 shipped) and calls `Share()` per edge, each a `GrandTotal()` over 12 aptitudes × 4 scopes —
**roughly 25,000 dictionary lookups per entity resolve**, and it runs on the status/hit path too
(`InjectorStatusBridge.cs:58`). A species lookup adds **two more dict hits** on top of 25,000.

The real finding is that this resolve is **uncached**, and per-species allocation is what makes it
*cacheable*: keyed `(Side, TypeId)`, it is bounded by **roster size rather than entity count**. A memo
cleared on `Stats.Invalidate()` and at the match edges where the commander cache already refreshes
(`MatchHost.cs:169,194`) makes the per-species design **net faster than today's commander-only path**.
Ship it without the memo and species aptitudes will be blamed for a cost that predates them.

Two hazards to carry, both of a class this repo has already been bitten by: `LawnElementResolverHost`
returns a throwaway empty index if `Configure()` has not run, so an early apply would silently resolve
`Empty` — the same silent-zero shape as the documented 222-point allocation that reached the writer as
nothing; and the `(Side, TypeId)` key must keep its side, because a bare type id collides.

### A7 ⭐ The parity ceiling and the primary lean are arithmetically coupled — they cannot be sized separately

Decision 3 sets how hard a species leans on its primary; decision 12 sets the parity ceiling. **These are
one decision, not two.** Onslaught is the classified primary of 39.5% of the corpus, so if every species
puts share `p` into its own primary, Onslaught's corpus-wide floor is `0.395 × p` **no matter how
perfectly the remainders are steered**:

| Primary lean `p` | Onslaught floor | 15% ceiling reachable? |
|---|---|---|
| 100% | 39.5% | no |
| 60% | 23.7% | no |
| 50% | 19.8% | no |
| **38%** | **15.0%** | the exact boundary |
| 30% | 11.9% | yes |

A 15% ceiling requires `p ≤ 38%`; 20% requires `p ≤ 50.6%`; 25% requires `p ≤ 63.3%`. **A tight ceiling
forces a weak identity, and a strong identity forces a loose ceiling.**

**The escape is real and reads well:** the constraint only binds if `p` is *uniform*. Letting `p` vary
per species — popular-primary species lean less, rare-primary species lean more — reaches any ceiling,
and states as a rule a player can understand: **common archetypes are generalists; rare archetypes are
specialists.** A design knob rather than an impossibility, but the spec must choose it deliberately.

### A8 — the Zomboss half has no lawn presence

`ZombossPattern` / `ZombossPatterns` / `ZombossCommanderAllocation` appear in **zero injector source
files** (grep across `src/`; the only non-Core hit is a comment in `LoadoutEndpoints.cs:18-24`). Lawn
enemy composition comes from the host game's own waves. **The adaptive Zomboss therefore exists only in
battle and expedition contexts** — coherent, and where the patterns were designed to run, but §6 reads
as though it applies wherever the player fights. The spec must say plainly which surfaces he appears on.

### A9 — the storage semantics of "static plan + per-player allocation" need deciding

Decision 7 makes the plan static shipped content; decision 10 makes the allocation per-player. So the
baseline is a **pure function of (static plan, that player's species level)** and does not need
persisting — only the player's *override* does. But `AptitudeAllocation` is explicit that **empty means
all-zero, never an invented default** (`AptitudeAllocation.cs:19-22`), and `LoadAllocation` returns only
persisted rows (`RpgStore.Aptitudes.cs:105-130`). So a species with no override row reads **zero, not its
baseline**. Two options: materialise baseline rows on level-up, or compose the baseline at read time.
**Composing is cleaner and matches `stat-system.md`'s "save inputs, not computed totals"** — but it means
`LoadAllocation` alone stops being sufficient, and that is a seam change the spec must name.

### A10 — the XP source rewards placement volume, which is the failure mode the prior art warned about

`PlantPlaced` awards **+8 per placement, uncapped**, and *"every place/spawn awards (not
once-per-type-per-run)"* (`rpg-progression.md`). §9's prior art is precisely about this shape: FF2's
players attacked each other because the growth signal was an *action*, not an *outcome*. Here the
cheapest plant spammed in a safe corner is the optimal way to raise a species — and under this feature
that converts directly into permanent aptitude points. SaGa's documented counter is the relevant one:
**Battle Rank rises as you fight, so grinding raises the opposition too.** Not a blocker, but the spec
should not inherit a per-placement faucet without saying whether that is intended.

### A11 ✅ Considered and NOT a defect — corpus parity vs. a player's own roster

Corpus-wide parity says nothing about the six species a given player fields; an all-attacker team stays
an all-attacker team. That is fine, for two reasons: decision 3 removed single-primary builds and
decision 11 steers remainders, so **every species carries spread points regardless of its lean**; and a
player who *chooses* an all-attacker roster getting one is a build decision, not an imbalance. Recorded
so a later reader does not re-raise it as an oversight.

---

## 12. What the audit sent back to the owner — ✅ all three closed, 2026-09-04

| Audit finding | Sent back as | Resolved by |
|---|---|---|
| **A1** — almanac XP inverts the tier ordering 176× | What feeds the `DemonType` budget? | **Decision 14: species level.** Restores the ordering and is symmetric with `UniqueDemon`'s specimen level |
| **A2** — the price names a quantity that does not exist | What formula prices a respec? | **Decision 15: rises with respec count on that species, decaying over time.** Prices churn rather than investment — which is what the friction was always for |
| **A7** — lean and ceiling are arithmetically coupled | Which do you give up? | **Decision 16: neither — let the lean vary per species.** The constraint only binds when the lean is uniform |

**Everything else in this document survived the audit unchanged**, and two verdicts moved in the
design's favour (A5 — the species join is built, not a gap; A6 — the per-species path is *cacheable*
where the commander path is not, so it can be net faster than today).

### 12.1 The audit's own leftovers — carried into the spec, not lost here

None of these needs an owner decision; all are spec obligations.

- **Fix the guard test** (A1). `PointBudgetTests.Commander_budget_is_smallest_and_unique_largest` holds
  its source constant on purpose and therefore cannot see a source-unit defect. It must compare real
  budgets from real sources, or it will keep certifying an ordering it never checked.
- **Correct three "almanac XP" citations** (decision 14): `spec-point-economy.md:37`,
  `PointBudget.cs:12-18`, `aptitudes.v5.json`'s `_scopeSourcesWhy`.
- **Use the spend path the shipped sinks use** (A4) — `TrySpendSouls` has zero production callers;
  every real sink appends to the ledger directly.
- **Ship the resolver memo with the feature, not after it** (A6), keyed `(Side, TypeId)` and cleared
  where the commander cache already refreshes. Also guard the empty-index bootstrap window, which has
  already produced one silent-zero defect in this codebase.
- **Say which surfaces the Zomboss appears on** (A8) — he has no lawn presence today.
- **Decide baseline storage** (A9): compose at read time (preferred — matches "save inputs, not
  computed totals") or materialise rows on level-up. `LoadAllocation` alone is not sufficient either way.
- **State whether a per-placement XP faucet is intended** (A10). It rewards volume, which is the exact
  failure mode §9's prior art documents.
- **New persisted state** (decision 15): a per-species respec counter, plus a decay rate. Both tunables.
