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

## A. The three specs that are wrong

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

**Fix:** the `siege` row runs `ActionPointsEconomy` (`points: true`), which forces
`timeline.profiles.siege.maxPoints` to exist — `BattleModeProfileCatalog.Build` throws otherwise, so
the compiler-adjacent guard already exists. Move, attack and build each cost points; the clock decides
how many you get. This also stops being the "other half of `ITurnEconomy`" that only `hybrid-atb`
exercises today.

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
| **REWRITE** | `battle-clock-profile` | A1 — `ActionPointsEconomy` |
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
