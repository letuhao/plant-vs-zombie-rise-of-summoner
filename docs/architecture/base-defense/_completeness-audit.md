# Completeness audit — the 17 specs against the ideal

**Run 2026-09-04, after the specs were written, against
[base-defense-ideal.md](../base-defense-ideal.md) in full** (2,405 lines: §0's 30 decisions, §5's 25
subsections, §6's tunable blocks, §7's 8 costs, §8's 3 prerequisites, §11's 12 findings and 7 build
costs).

**Verdict: the specs are not complete.** Of 30 owner decisions, **18 are fully specced, 5 partially,
and 7 are missing.** Eight of §5's subsections have no module that implements them. Two of §11's
findings that the audit itself marked *"the design changes"* were dropped. Two of §8's three named
prerequisites are unspecced.

**And three specs are wrong**, not merely thin — they would build something the ideal explicitly
rejects.

---

## PASS 2 — 2026-09-04, and it found the first pass incomplete

**Pass 1 read §5.8, 5.9, 5.17, 5.18 and 5.20 in full and the rest by heading only.** Pass 2 read every
remaining §5 subsection. It found **nine more items, one reverted fix, and a root cause.**

### ⛔ P0 — the root cause: two §5 paragraphs are known-wrong and were never corrected in place

§11.4 recorded six corrections to this document's own claims. **Two of them were logged in the errata
table and left standing in the body**, so a downstream session reading §5 gets the wrong version:

| § | Said | Truth |
|---|---|---|
| **5.4** | *"world replay reuses the stored record rather than re-simulating"* | `RpgStore.WorldTurns.cs:599-606` re-simulates from turn zero with no resolver (§11.4 #2) |
| **5.12** | *"a unit that moves does not also strike that turn"*, cited to `action-corpus-ideal.md:434` | The opposite of `action-map.md:430`, and the wrong file (§11.4 #5) |

**Both are now corrected in place**, with a box saying what they used to say. An errata table is not
enough: pass 1 read §5.12's surviving text and produced finding A1 from it.

### ⛔ P1 — A1 was a FALSE finding, and its "fix" broke a correct spec. Reverted.

Pass 1 claimed `OneActionPerTurnEconomy` gives a siege unit one action per *round*, and switched the
profile to `ActionPointsEconomy`. **`action-map.md:430` forbids exactly that**:

> *"No compound move-and-attack action is required, **and no Action Points. The time cost is the
> economy** … (`ActionPoints` still ships in the timeline's economy set for modes wanting a fixed
> per-turn budget — **it is simply not what this mode needs**.)"*

*Turn* is a per-actor **activation**, reset by `ResetForNewTurn` when the caller says a boundary
happened; under readiness scheduling a fast actor activates more often, and per-action `TimeCostTicks`
makes a step cheap and a strike expensive. **The clock already decides whether you get both.**
`points: false` restored, with both errors recorded in the spec so a third session does not make a
third.

§5.19 confirms it independently: across seven surveyed games, the two that allow field construction
**both charge the unit's whole turn** — which *is* one action per activation.

### ⛔ P2 — `district-layout` grew the board. §5.1 and §5.25 both forbid it.

§5.1's title is the rule: **"The grid does not grow. The placement budget does."**

> *"Rows and columns come from **base tier** and are **fixed** … `DevelopmentLevel` buys **build
> slots**."*

and §5.25 lists `Grid dimensions = f(DevelopmentLevel)` as **rejected**. The spec's
`side = base + perDev × DevelopmentLevel + perSlot × Slots.Count` was the rejected formula.
**Corrected to a lookup keyed by base tier.**

### ⛔ P3 — the SECOND budget, and the thing that makes a fixed board legal

§5.1: *"**There are two budgets, not one**"* — legion slots (the central area) **and defense slots**
(`DevelopmentLevel`). Pass 1 specced the first and the field cap, and missed the second entirely.

**And with it, the escape valve** — without which this program has a hard progression ceiling:

> *"Once slots fill the board, further development buys **tower tier** — a magnitude, so it reads
> `P(Θ)` and rises forever. **The board stops growing; the investment never does.**"*

Added to `siege-objective` as stages 1–3 with the switch point as a tunable.

### P4–P9 — six more, all now folded in

| # | Finding | Landed in |
|---|---|---|
| **P4** | **§5.16 R3 carries XCOM's SHIPPED weights** — hit-chance **+70**, objective **+50**, kill **+15**, low-HP **+10**, cannot-counter **+10**, threat **−N**. *"Hit-chance dominates lethality 70:15 — an AI that maximises expected damage with no risk term reads as **suicidal**."* Pass 1 invented weights that **inverted** it (kill 300 > damage 100) | `siege-ai` |
| **P5** | **The anti-turtle timer** — Fire Emblem's `+ current round` term, *"monotonic and invisible"* | `siege-ai` |
| **P6** | **R2's stance is `Hold`/`Guard`/`Engage` on the ACTOR, "three values and no more"** — a different axis from §5.20's signed aggression, and pass 1 deleted it when it replaced bands. Signed aggression cannot stop *"a garrison abandoning the objective to chase a bait unit"* (the ⭐ finding) | `siege-ai` |
| **P7** | **`World/Ai/Utility/Consideration.cs` is shipped, uncalled, and has `Weakest()` giving a reason string for free.** *"A siege board has one [an economy] — so this is its first real caller"* | `siege-ai` |
| **P8** | **⛔ Never a hidden difficulty thumb** (*"Difficulty is which policy, not a stat bonus"*) · **⛔ do not put the score on `ActionTargetOrdering`** · the **auto-versus-played dial**, *"a tunable from line one"*, which fheroes2 got wrong in the other direction | `siege-ai` |
| **P9** | **A garrisoning unit costs a field-cap slot** (§5.13) — the *structure* does not count, the *body* inside it does. F7's residual, left ambiguous by pass 1 | `siege-objective` |

### Closed in pass 2 — the six smaller items

All six landed; listed so the trail is visible rather than implied:

| # | Item | Where it belongs |
|---|---|---|
| 1 | The **depot budget crosses on `BattleRequest`** (§5.13's `BattleRequest{ budget }` diagram) | `siege-seam` |
| 2 | **Defender draws sector `LoamStock`; attacker draws `CarriedLoam`** — the asymmetry is the blockade mechanic | `siege-economy` |
| 3 | **"The board never reads `WorldSlot.OwnerFactionId`"** — a Never, not a note | `siege-economy` |
| 4 | **Builder killed mid-build → total loss, `InterruptRefundMilli = 0`** (§5.19) | `siege-construction` |
| 5 | **`BuildResolver.cs` is the five-plumbing-sites reference implementation** — *"a new order kind inherits a working reference rather than a hunt"* | `siege-construction` |
| 6 | **Each obstacle kind declares `acquisitionPaths`** — a **twelfth** catalog in `structure-seed` (§5.24) | `siege-obstacles` + `structure-seed` |

**One item leaves this program:** `structure-seed`'s twelfth catalog (`acquisitionPaths`) and its
**deterministic planner stage** (decision 33) both belong to that program's own spec round. They are
recorded in [structure-seed-ideal.md](../structure-seed-ideal.md)'s consumers, not built here.

---

## PASS 4 — 2026-09-04, auditing pass 3's own changes and §2

Pass 3 predicted a fourth pass would find something, because pass 3 itself **added a module and moved
another**. It did.

Pass 4 also read [base-defense-ideal.md](../base-defense-ideal.md) **§2 in full** — the ten load-bearing
principles this program's specs cite by number **28 times** without anyone having verified the numbers.

**Seven findings. One is a self-inflicted contradiction pass 3 created — and four were found by a
ten-line script, not by reading.**

### ✅ First, the good news: every §2 citation was correct

All 28 rule-number citations across the specs check out — rule 1 (RPG layer), rule 4 (one ladder, two
reads), rule 7 (world/combat seam), rule 10 (closed vocabularies). No drift. And two of pass 2 and pass
3's own fixes are **independently confirmed** by rules I had not read:

- Rule 5 names *"**Grid dimensions per tier**"* as config — confirming pass 2's correction of
  `district-layout` from `f(DevelopmentLevel)` to a per-tier lookup.
- Rule 6 says *"A ceiling on tower **power** would not [be exempt], and must stay uncapped"* —
  confirming `siege-objective`'s stage-3 escape valve.

### ⛔ P4-1 — The build order still encoded the cycle pass 3 removed

Pass 3 moved `siege-obstacles` to level 4 and fixed every spec header. **It did not fix the map's build
order**, which still read:

```text
5.  siege-cover · siege-construction
5b. siege-obstacles                    (needs both of 5)
```

while the module table two sections above already said `siege-cover` **depends on** `siege-obstacles`.

**A dependency table and a build order that disagree is worse than either being wrong alone**, because
each looks authoritative in isolation and a builder would follow whichever they opened first.
Corrected: obstacles joins level 4; cover and construction both consume it at level 5.

### ⛔ P4-2 — §2 rule 7 says *"never a battle paused in memory"*, and decision 41 is exactly that

Rule 7, first bullet, verbatim:

> *"**Combat is stateless between turns.** A multi-turn siege is a **fresh engagement each turn, built
> from world-held state** — **never a battle paused in memory.**"*

`siege-stage` holds a paused session in the server process. The first draft argued *"the pause is
within a turn, not between turns"* — which is right, but the spec never stated **the clause that makes
it right**.

> ### ✅ SUPERSEDED, and better — decision 46 (2026-09-05)
>
> The owner asked *"we won't store battle state? maybe it correct in heroes of might and magic … they
> have reason for it, maybe we should follow."* **They do**: a battle is re-derivable from its inputs,
> so it never needs storing. A paused siege is now a **persisted decision log replayed on resume**, not
> a session in memory — which makes rule 7 **unconditionally** true, **removes** the clause below as
> scaffolding, uses §2 rule 8's own save model, and **survives a server restart** (which the in-memory
> version could not). It also closes a wiring gap §3.7 already recorded: `decisions_json` is *"read and
> never written."*
>
> **The finding stands as a finding; its fix was replaced by a better one.**

~~**Added:** *a pause must never survive a world-turn boundary.*~~ The world turn cannot commit while a
siege is paused, so a pause and a boundary can never coexist — and if a code path ever lets a paused
session outlive a commit, **rule 7 is violated for real**, because the siege would then be spanning
turns as a battle held in memory. That one clause is the whole difference between a suspended session
and the thing the rule forbids.

### P4-3 — §2 rule 8's version stamp was missing from the module that resolves sieges

> *"every resolution stamped `(engineVersion, rulesetVersion, seed)`."*

Present in `siege-supply` and `structure-state`; **absent from `siege-resolver`**. And here it is not
bookkeeping — it is what makes a `:509` / `:603` divergence **detectable**: without it, a re-derived
report that disagrees with the original looks like a UI bug; with it, the two carry different
`rulesetVersion`s and the artifact names its own cause.

### P4-4 — §2 rule 3's ban on client prediction was nowhere in the FE specs

> *"The FE renders and commands; **it never rolls**. No client prediction of the living set (the lawn
> projector's **RT-15, rejected there and rejected here**)."*

`board-render` is exactly where prediction gets added for feel — interpolate the unit toward where it
*will* be, resolve later. **RT-15 was rejected in the lawn projector for this.** Added as a `Never`
with the line drawn precisely: interpolating between two **server-confirmed** states is rendering;
extrapolating past the last one is prediction.

### P4-5 — A duplicated level label

`c3` appeared twice in the build order. `structure-instantiate` and `structure-planner` are genuinely
parallel; now labelled as such rather than as a typo.

### ⛔ P4-7 — Four MORE ordering errors, found by a script after the eye had passed

P4-1 was found by reading. **Then a mechanical check over all 29 module headers found four more that
reading had missed** — three real, one cosmetic:

| Module | Declared | Depends on | Problem |
|---|---|---|---|
| `siege-objective` | level 3 | `combatant-kind` (3) | build order said **parallel** |
| `siege-engagement` | level 7 | `siege-resolver` (7) | build order said **parallel** |
| `siege-stage` | level 8 | `board-render` (8) | `→` implied ordering the level did not encode |
| `battle-stage` | level 8 | `board-render` (8) | same |

Plus a naming inconsistency **pass 3 introduced**: the content family used plain levels `0–5` in its
headers while the map labelled them `c0–c5`, and `structure-instantiate` (added by pass 3) used `c3`.
**One family, two conventions.**

Fixed with explicit sub-levels — `3b`, `7b`, `8b` — and `c0–c5` throughout the content family.

> ### ⭐ The lesson is the method, not the four rows
>
> Passes 1–4 all read specs and found real defects. **This check took ten lines of Python and found
> four things four passes of careful reading had not.** A dependency graph is a machine-checkable
> property, and "I read it and it looked consistent" is not a check.
>
> **The graph is now verified mechanically: 29 modules, no cycles, every dependency at a strictly
> earlier level.** That assertion is reproducible, which is more than any of the prose findings above
> can claim — and it should be re-run after any module is added or moved.

### P4-6 — The line-of-fire trace could become a fifth area shape

§2 rule 10: *"the action layer already owns a grid vocabulary (`GridPos`, Chebyshev distance, **four
area shapes**, `ChosenCell` anchoring). Inventing a second grid model beside it is the exact defect the
atom program exists to stop."*

Measured: the four are `Row · Column · Square · Rectangle` (`ActionTargetSpec.cs:42-48`).
`siege-cover`'s Bresenham trace is **not** one, and must not become one — it is a traversal used to
compute a penalty, returning cells to *inspect*, never cells to *hit*. Stated, with a test: **if the
trace's output is ever passed to a targeting resolver, a fifth shape has arrived by the back door.**

---

## PASS 3 — 2026-09-04, auditing what passes 1 and 2 could not see

**Passes 1 and 2 both ran before rounds 9 and 10.** Decisions **35–45** — eleven of them — had never
been audited, and eleven specs were written or rewritten after pass 2 closed: `siege-cover` (rewritten
whole), `battle-stage`, and the six `structure-*` modules, plus round-9 edits to six more.

Pass 3 read `structure-seed-ideal.md` **§1 and §2**, which the six structure specs were written without
— a gap of exactly the shape pass 2's root cause described.

**Seven findings. Two are breaks, not gaps.**

### ⛔ P3-1 — A DEPENDENCY CYCLE, and the map's own rule forbids it

| Module | Declares |
|---|---|
| 11 `siege-cover` | *depends on `siege-positions`, **`siege-obstacles`*** |
| 19 `siege-obstacles` | *depends on `structure-state`, **`siege-cover`**, `siege-construction`* |

The map's Phase-0 rule: *"**Dependency direction, no cycles.** If two modules each need the other, they
are one module."*

**Introduced by the decision-35 rewrite.** Cover used to key off terrain, so it needed no obstacles;
the HoMM3 model has obstacles *project* cover, so it does. Meanwhile obstacles still claimed a
dependency on cover for the Trench's cover value — which is **not a dependency at all**, it is a data
field.

**Fix (applied):** `siege-obstacles` becomes the **structure-vocabulary module at level 4**, depending
on `structure-state` alone. It owns `ObstacleKind`, `AcquisitionPath`, the cover-radius fields and the
cell-entry trigger. `siege-cover` (5) and `siege-construction` (5) both **consume** it. No cycle, and
**no cascade** — every downstream level is unchanged.

### ⛔ P3-2 — The Mine has no trigger. The rewrite deleted it.

`spec-siege-obstacles.md:157`:

> *"Fired on `ScopeMembershipTransition.CellEntered`, **which siege-cover already emits.**"*

`spec-siege-cover.md` §8, after the decision-35 rewrite:

> *"**No `ScopeMembershipTransition` change.** The program's one allowed vocabulary change is **not
> spent here** — cover is evaluated per shot, so no membership is entered or left."*

**Cover released the budget and obstacles never claimed it.** The Mine — the only obstacle that
punishes the safe-looking cell — fires on nothing. `spec-combatant-kind.md:183` carries the same stale
reference, calling the cell-entry transition *"the one reviewed vocabulary change"*.

**Fix (applied):** `siege-obstacles` **owns** the transition, and both stale references are corrected.

### P3-3 — Law 1's middle layer is absent from all six structure specs

`structure-seed-ideal.md` §1 law 1, **binding**:

> *"**Seed → concrete → per-player. Three layers, and the middle one rolls.** … The **game runtime**
> rolls the concrete object per player, seeded, like Diablo loot. `Instantiator.TryInstantiate` is the
> shared SDK … **Never design a second roll.**"*

and §2.2:

> *"The concrete-roll layer has **no production caller** — `Instantiator.TryInstantiate`: **zero.**
> Every *'we need a runtime generator'* finding for structures is therefore a **wiring gap on a shipped
> SDK**, not a new build."*

**The six specs cover seed → catalog. That is two layers of three.** Nothing rolls a concrete
per-player structure instance.

**Fix (applied):** new module **`structure-instantiate` (29)** — a wiring module, explicitly not a new
roll.

### P3-4 — The fourth ownership level is missing

§1 law 4 names **four**: `AUTHORED` · `DERIVED` · **`GENERATED`** · `VALIDATED` — and *"a field with
none is a contract defect."* `spec-structure-schema.md` lists **three**. `GENERATED` (a generator emits
rows) is exactly the level `structure-pipeline`'s output needs, and it had no name.

### P3-5 — A generated corpus with no surface

§2.2, a wiring gap none of the six specs mentions:

> *"**`StructureDef.Name` has no reader** outside its own validator. **Nothing in the game or web UI can
> name a structure** — so a generated corpus has no surface today."*

Generating ~36 structures whose names nothing can display is a corpus that exists only in JSON.

### P3-6 — Two overlapping vocabularies, unreconciled

§2.3: *"**`StructureKind` has 2 values** — `LoamSource`, `Storage`. **§5.21 of the base-defense ideal
names ten roles.**"* (`siege-construction` adds `Refinery`, making three.)

So a structure carries a 3-value C# **kind** and a 10-value seed **role**, and no spec says how they
relate. Left alone, one of them silently becomes decoration.

### P3-7 — A wrong number

`spec-structure-schema.md:30` said *"~841 anchors"*. Measured: **415** plant species files, **503**
across all species. 841 was the seedsmith **stage-run** figure (841 anchors × 8 pipelines = 6,728),
carried across from a different audit. The ideal's own *"408"* is also now stale.

---

## A. The three specs that are wrong (pass 1)

These matter more than the gaps: a missing spec gets written, a wrong one gets built.

### A1 ⛔ `battle-clock-profile` gives a siege unit **one action per turn** — move *or* attack, never both

`spec-battle-clock-profile.md` §5 sets the `siege` row with `points: false`, which is
`OneActionPerTurnEconomy`. Its own source (`TurnEconomy.cs`) says exactly what that means:

> *"Exactly one action, spent once, per `ResetForNewTurn`. The simplest economy — every classic-round
> battle in this game today."* · `Scope => TurnEconomyScope.PerActor`

On a 24-cell board a unit would take **twenty-four turns to cross and never swing**. And it directly
contradicts two things the ideal already settled:

- `action-map.md:430`, whose heading is *"Move and attack: two separate actions, and the clock decides
  whether you get both"* — and which §11.4 correction **#5** records this session getting backwards
  **once already**, citing the wrong file while doing it.
- Decision 14: *"build is a third peer of move and attack."* Three peers cannot share a budget of one.

> ### ⛔ SUPERSEDED BY PASS 2 — this finding was WRONG. See P1 above.
> `action-map.md:430` says *"no Action Points … it is simply not what this mode needs."* *Turn* is a
> per-actor **activation**, not a round, so the original `points: false` was correct and this "fix"
> broke it. Left standing, struck through, because the error is the more useful record.

~~**Fix:** the `siege` row runs `ActionPointsEconomy` (`points: true`), which forces
`timeline.profiles.siege.maxPoints` to exist — `BattleModeProfileCatalog.Build` throws otherwise, so
the compiler-adjacent guard already exists. Move, attack and build each cost points; the clock decides
how many you get. This also stops being the "other half of `ITurnEconomy`" that only `hybrid-atb`
exercises today.~~

### A2 ⛔ `siege-cover` uses the wrong unit, and the ideal computed the right one

My spec writes cover as **per-mille dodge** (`cover.dodgeMilli.rough.direct = 150`). §5.17 establishes
the actual scale from shipped code:

> `BaseAccuracy(Θ) = 220 + 26·Θ` and `BaseDodge(Θ) = 26·Θ` (`BattleModels.cs:171-172`), with
> `accuracyScale: 100.0` — so **100 contest points is one sigmoid unit, and +50 dodge is half a
> unit.** … *"a flat cover value stays exactly as decisive at Θ=200 as at Θ=1."*

And it names the values: **trench +40, emplacement +80** — *flat contest points*, not per-mille. A
per-mille cover value is a *fraction of something*, and there is nothing here to take a fraction of;
it would be a second scale beside the one the contest already uses.

**Fix:** flat integer contest points added to `combat.dodge.omni`. The ladder argument (§2 rule 4 —
contests read `Θ` linearly) only works in the contest's own units.

### A3 ⛔ `siege-ai`'s risk term is the auto-cover-seek behaviour §5.17 forbids

My spec: *"`incomingThreatMilli` … **discounted by that cell's cover** — which is the one line that
makes `siege-cover` matter to the AI."* §5.17 addendum 2, marked ⛔ and binding on §5.16:

> *"Relic shipped it and then spent five patches removing it: 'Infantry will no longer prefer to take
> paths with denser cover distribution, which has often led to unpredictable behaviours' (1.3.0) … no
> sliding into cover mid-aim … 'will not pick a second cover spot if it moves them further from
> combat' (1.7.1) … **Cover should be somewhere the player decides to stand, never somewhere the
> pathfinder drifts to.**"*

**This one is worth debating rather than simply conceding — see §D1.** But as written, the spec
contradicts a ⛔ rule and must not be built unchanged.

---

## B. The seven missing decisions

| # | Decision | Where it should live | Severity |
|---|---|---|---|
| **1** | **The win condition.** *"A base has one central defense area. Lose it and you lose the base. Capture requires killing every troop standing in it."* **No spec states the objective of the game this program is building.** `combatant-kind` defines `AnyActive`; nothing says what winning is | new module | ⛔ Critical |
| **4** | **Legion slots even and paired**, and **max members per legion** — which §3.6 establishes *"does not exist today and is therefore free to choose"*. §6's `slots.legion` and `legion` tunable blocks have no owner | new module | High |
| **5** | **The field cap** — a flat authored integer per base tier, identical for both sides. §5.9 calls it *"the difficulty dial, and it is a single integer in a config file"* (Arknights, measured). `waves.maxArrivalsPerRound` in my `siege-waves` is a **per-round work bound**, an entirely different thing — I conflated them. §5.9 also names the mechanism to reuse: `CapPolicy.TryAdmit` (`Match/CapPolicy.cs`), *"built, tested, and tunable"*, reuse the pattern not the type | new module | ⛔ Critical |
| **16/17/18** | **TWO material stocks**, bulk + worked. My `siege-construction` specs only `ironwork`. The bulk stock (recommended name `rubble`; `stone`/`metal` refused as colliding with shipped content) is absent, as is *"construction-only, world-scoped, never feed fusion, die with the map"* | `siege-construction` | High |
| **24** | **A map turn resolves ONE engagement; a siege spans turns because engagements _repeat_.** Batches cycle *within* an engagement. **No spec handles an engagement that ends inconclusively and resumes next turn** — `siege-resolver` treats a district assault as one battle that resolves to a winner | `siege-resolver` + new | ⛔ Critical |
| **28** | **The refine chain** — `ironwork` is *made* from bulk material at a lossy, gated rate. Decision 28 explicitly retires two alternative framings to land on this one | `siege-construction` | High |
| **10 (half)** | *"Nothing is built inside the central area — it is a pure arena."* `siege-construction`'s five placement rules do not exclude the `Core` zone | `siege-construction` | Medium |

---

## C. Sections of §5 with no module

| § | Subject | What is missing |
|---|---|---|
| **5.18** | **The obstacle vocabulary — four kinds and one building.** Trench · Rampart · Wire · Mine · Emplacement, each existing *"only because cutting it removes a decision no other row can produce"* | **No spec builds it.** `siege-board`'s four `CellTerrain` values are *terrain*, a different layer. **`Mine` (BITE + DENY — damage on entry, single-use, ignores cover) has no home anywhere in 17 specs.** `Wire` *"multiplies the stamina cost of entering the cell"* — my `Rough` multiplies *movement cost*, a different resource |
| **5.20** | **Targeting — the five-rule minimum.** *"Every system surveyed has all five"* | `siege-ai` specs **rule 1 only** (total order + tie-break). Missing: a named, player-visible validity filter (CoC `Favourite Target`); a retarget trigger with a **stated** latency; an override channel *inside* the priority order (Arknights' signed aggression **+2…−2**, which gives *"taunt, stealth and decoy one mechanism instead of three"* — my aggro *bands* are not that); a replacement vocabulary for units whose geometry breaks the standard one |
| **5.17 rules 2/4/5** | Cover's three shipped design rules | **Rule 2** — *"beat cover with a damage type, not a bigger number"*: fire ignores trench cover. Three independent games confirm it, and *"without it the trench-warfare fantasy becomes the stalemate it is named after."* **Rule 4** — decay cover with the occupant's **stamina/hunger exhaustion**, not with turns; *"closes the loop with decision 13's block their resource and exhaust them."* **Rule 5** — show the contribution on the wire (`BlockedTarget.tsx` / `blockedPlacement.ts`, built and inert). **None specced** |
| **5.8** | `PlaceholderBattleResolver.DefenderBonusMilli` (`:79-83`) is *"currently the entire fortification model"* and *"should shrink toward nothing as real fortifications land, or the defender gets paid twice for the same thing"* | Unspecced. Verified live: `defenderWeight * DefenderBonusMilli / 1000`, gated on `DefenderStationary \|\| stance == Hold` |
| **3.5** | Wave composition is a **code const**, and *"there is no wave data file at all — this feature should fix that rather than add a second hand-written array"* | Unspecced |

---

## D. Findings and prerequisites dropped

| Ref | What | Status |
|---|---|---|
| **F12** | *"Decision 21 buys zero economy. 4 rootbeds + wells = 400/turn against a 300 cap; at equilibrium the marginal producer's entire output is destroyed as overflow."* Verdict: **"The design changes.** Capacity must grow alongside slots" | **Missed entirely.** No spec mentions it |
| **F8** | I over-corrected. F8's own verdict is ***"clock, or field cleared, whichever first — one tunable row"*** — a **hybrid**. `siege-waves` specs pure clock and argues *against* the state half, which is also decision 6's own wording (*"the field resolves, then the next batch enters together"*) | Misread my own finding |
| **F9** | Hidden mines vs the perfect-information framing; recommendation **revealed** | Unresolvable today because mines are unspecced |
| **§8 prereq 1** | **A diffing world-graph writer.** *"Decision 21 multiplies slot rows, and `RpgStore.World.cs:210-212` names the trigger by hand. **No longer a follow-up**"* | **Unspecced.** ~360 slot rows rewritten per turn commit |
| **§7 cost 3** | A new order kind must pass **five** plumbing sites — `WorldCommandKinds`, the `WorldCommand` field, `RpgStore.CommandPayload`, `WorldCommandRequest`, the `WorldEndpoints` submit mapping. *"`bind-warden` currently fails sites 4 and 5"*; the store's comment records *"exactly how `stance` was found missing"* | `siege-seam` names the command kind and **not the five sites** |
| **§7 cost 6** | *"Slot ownership does not follow sector capture … If the board is the sector zoomed in, this becomes visible and has to be fixed."* | Unspecced |
| **§7 cost 5** | `stages/` files may not name a `*Dto` (`contract/contractGuard.ts:57`) | Not in `siege-stage` |
| **Decision 22** | *"A construction stock **at capacity** halts production; nothing is wasted"* | `structure-state` specs **depletion**, which is a different thing. Capacity-halt is unspecced |
| **Decision 25** | An unoccupied building *"occupies its cell, blocks movement **and fire**"* | Only `BlocksMovement` specced. No `BlocksLineOfFire` |

---

## E. Where I contradicted a settled item

`spec-structure-state.md`'s open question asks whether structure HP scales, and recommends *authored*.
**§6's `structures` block already decided it**, and marks the mixing of the two classes as *"a defect
corrected 2026-09-04"*:

> ① **Magnitudes** — HP, damage — `long`, **derived from `P(Θ)`**. ② **Board-space and pacing
> quantities** — range (cells), footprint, build turns — **flat authored tunables, never `P(Θ)`**.

So HP **is** on the ladder. The genuinely open part is narrower than I framed it: a structure has no
level, so **which `Θ`** does it read — the sector's `DevelopmentLevel`, or the owning faction's? That
is the question to put to the owner, not whether to scale at all.

---

## F. What the audit does *not* find

Stated so the report is not all deficit. Spot-checked and sound:

- The **Gate 0 re-inventory** and its six corrections — all six re-verified in this pass.
- **`siege-supply`** against F1/F1b — the `Usable` split is the right fix and the `Source`/`Traversable`
  distinction is exactly what F1b needs.
- **`siege-seam`**'s zero-golden argument — `BattleRequest`/`BattleOutcome` really are transient.
- **`siege-resolver`**'s two-call-site requirement — §8 prerequisite 2, correctly the module's first
  criterion.
- **`siege-pathing`**'s determinism contract — C2's *"a heap would need the same tie-break written
  explicitly"* is answered with a total comparator.
- **`combatant-kind`**'s `[JsonIgnore]` correction — two shipped precedents, correctly read.
- **`district-layout`**'s S1–S4 stability contract and the `P(Θ)` box.
- **`board-render`** against C6 — budgeted at the measured scale.

The **architecture** is sound. What is missing is the **game**: objective, force limits, obstacle
vocabulary, and the multi-turn loop.

---

## G. Proposed remedy — 17 modules become 20

| Action | Module | Covers |
|---|---|---|
| **NEW** | `siege-objective` (level 3) | Decisions **1, 4, 5, 10-half** — win condition, legion slots, max members per legion, the field cap via `CapPolicy`'s pattern, the central area as a pure arena. **This is the game's rules module and its absence is the audit's headline finding** |
| **NEW** | `siege-obstacles` (level 5) | §5.18's four kinds + Emplacement; **Mine**/BITE; **Wire**/stamina; the terrain-vs-structure split |
| **NEW** | `siege-engagement` (level 7) | Decision **24** — one engagement per map turn, inconclusive outcomes, repetition across turns, the `Withdrawn`/spent/objective-fell exits |
| **REWRITE** | `battle-clock-profile` | A1 — **reverted in pass 2**; `OneActionPerTurnEconomy` stands, both errors recorded in the spec |
| **REWRITE** | `siege-cover` | A2 — flat contest points; §5.17 rules 2, 4, 5 |
| **REWRITE** | `siege-ai` | A3 — the risk-term line (see §D1 below); §5.20's rules 2–5 |
| **EXTEND** | `siege-construction` | Decisions 16/17/18/28 — the bulk stock and the refine chain; the Core-zone exclusion; §7 cost 3's five plumbing sites |
| **EXTEND** | `structure-state` | Decision 22's capacity-halt; decision 25's block-fire; §E's `P(Θ)` correction; F12's capacity-with-slots |
| **EXTEND** | `siege-waves` | F8's actual hybrid verdict; §3.5's wave data file |
| **EXTEND** | `siege-supply` | §7 cost 6 — slot ownership following capture |
| **EXTEND** | `siege-stage` | §7 cost 5 — the `*Dto` guard |
| **NEW/defer** | `world-graph-diff` (level 0) | §8 prerequisite 1. **Or** explicitly hand it to the world program — but it cannot stay unowned |

---

## H. RESOLVED — the owner answered all four, 2026-09-04

Recorded as decisions **31–34** in [base-defense-ideal.md](../base-defense-ideal.md) §0 round 8.

| # | Question | Answer | Effect |
|---|---|---|---|
| **D1** | The cover-seeking AI | **⛔ Overridden — keep the risk term as specced.** Decision 31 | `siege-ai` keeps its cover discount; the ⛔ and the residual risk are recorded in that spec, with `ai.weight.risk = 0` as the one-row rollback |
| **D2** | Which `Θ` for structure HP | **Sector `DevelopmentLevel`, × an authored MATERIAL TIER.** Decision 32 — *"llm to generate variant like stone wall, iron wall that iron wall have more defense than stone wall"*. **And decision 33: `structure-seed` needs a deterministic planner (not LLM) to prepare what it should generate first** | `structure-state` takes a `MaterialTier` ordinal and computes `MaxHpOf` from `P(Θ_development)`. The planner is `structure-seed`'s, and it is a new required stage |
| **D3** | The bulk material's name | **`rubble`.** Decision 34 | `siege-construction` |
| **D4** | Who owns the diffing writer | **Spec here, build wherever it fits** | New module `world-graph-diff` |

**Decision 32 is seedsmith Law 2 stated as content**, and worth restating: the model picks *which
material*; deterministic code turns the ordinal into a number. A wrong enum is visible; a wrong number
is not.

**Decision 33 promotes a seedsmith guideline to a required stage.** *"Order the build so the model-free
modules come first"* becomes: a planner fixes which kinds, which tiers, how many variants and which
slots — **then** the model writes identity into slots that are already open. Without it the tier ladder
(stone < iron < …) would be whatever the model happened to name, and decision 32's mechanical
difference rests on that ladder being ordered.

---

## Remedy applied — 2026-09-04

All findings in §A–§E are fixed. **17 modules → 21.**

**New:** `siege-objective` · `siege-obstacles` · `siege-engagement` · `world-graph-diff`
**Rewritten:** `battle-clock-profile` (A1) · `siege-cover` (A2) · `siege-ai` (A3 + §5.20 rules 2–5)
**Extended:** `siege-construction` (rubble, refine chain, Core exclusion, five plumbing sites) ·
`structure-state` (P(Θ) + material tier, capacity-halt, block-fire, F12) · `siege-waves` (F8's hybrid,
wave data file) · `siege-supply` (§7 cost 6) · `siege-stage` (§7 cost 5)

---

## Original §H — the four questions, as asked

### D1. The cover-seeking AI — I think the ⛔ is drawn one step too wide

§5.17 addendum 2 forbids auto-cover-seek. My `siege-ai` risk term does it. But Relic's five patches
are all about the **pathfinder**: *"paths with denser cover distribution"*, *"sliding into cover
mid-aim"*, *"a second cover spot"*, *"looking for cover when too close"*, *"an additional firing
delay"*. Every one is a unit **drifting** somewhere while doing something else.

An AI with **no** notion that a cell is dangerous walks into a kill zone every time — and then cover
is a mechanic the player must respect and the AI does not, which reads as broken in the opposite
direction.

**Proposed line, which I believe satisfies both:** the risk term evaluates **only the destination cell
of a move the AI has already decided to make**, and **never reroutes a path or adds a waypoint**.
Pathing stays cover-blind (`siege-pathing` never sees a cover value); target selection stays
cover-blind; only *"of the cells I could end my move on, which do I stop in"* reads cover. That is a
player-legible decision, not a drift. **Owner's call.**

### D2. Structure HP — which `Θ`?

§6 settles that HP is on `P(Θ)`. A structure has no level. **Sector `DevelopmentLevel`** (a developed
city has stronger walls — thematic, and it makes decision 21 matter) or **the owning faction's `Θ`**
(walls scale with the empire)? I lean `DevelopmentLevel`; it is local, already hashed, and it gives
decision 21 a second payoff.

### D3. The bulk material's name

Decision 17 leaves it open with `rubble` recommended; `stone` and `metal` refused as colliding with
shipped content. I will use `rubble` unless told otherwise.

### D4. Does `world-graph-diff` belong to this program?

§8 calls it a prerequisite, not a follow-up. But it is a `FusionRpg.Data` performance change that the
world program owns. **Recommendation: spec it here as a prerequisite module, build it wherever it
fits** — the risk to avoid is it being nobody's.

---

## I. Honest scoring

| Category | Count | Fully specced | Partial | Missing |
|---|---|---|---|---|
| Owner decisions (§0) | 30 | 18 | 5 | **7** |
| §5 subsections with mechanics | ~18 | 12 | 2 | **4** |
| §11.2 findings marked "the design changes" | 8 | 6 | 1 | **1** (F12) |
| §11.3 build costs | 7 | 6 | 1 | 0 |
| §7 costs | 8 | 5 | 1 | **2** |
| §8 prerequisites | 3 | 1 | 1 | **1** |
| **Specs that are wrong, not thin** | — | — | — | **3** |
