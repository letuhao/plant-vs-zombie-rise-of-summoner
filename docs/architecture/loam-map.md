# Capability map: loam and the Fracture

**Status:** **SEALED 2026-08-23 — owner-approved. Build authorized through the ⭐ gate.**
All ten audit findings and all nine spec findings carry a verdict below. Post-gate modules stay
unspecced and unplanned on purpose: the gate decides whether they happen.

**Post-gate authorized 2026-08-23 (owner decision, gate verdict via automated test-suite coverage —
see `tasks/loam-todo.md` Checkpoint 5).** All five post-gate modules are cleared to be specced and
built in the build order below, and **all five are now sealed** (all design calls resolved, per the
owner's follow-up authorization to "clear every missing" rather than leave open items for a second
round): [spec-loam-legions.md](loam/spec-loam-legions.md) · [spec-loam-ai.md](loam/spec-loam-ai.md) ·
[spec-structure-substrate.md](loam/spec-structure-substrate.md) ·
[spec-loam-structures.md](loam/spec-loam-structures.md) ·
[spec-loam-texture.md](loam/spec-loam-texture.md). **None are built yet** — this is Phase 1 (Specify)
of spec-driven-development, complete for all five modules; Phase 2 (Plan/Tasks) and implementation
have not started for any of them.
**Design source:** [empire-economy-ideal.md](empire-economy-ideal.md) (the mechanism) ·
[economy-principles.md](economy-principles.md) (the tests any of it must pass).
**Slots into:** [world-map-program.md](world-map-program.md), ahead of `sector-development` — this
program eventually builds the substrate that module was going to assume, but deliberately not first
(see A10).

## What this program is

The empire holds ground by keeping it **real**. Loam is what pays for that; the Fracture is what
takes it back. This program builds the resource, the decay, the objects that produce and consume
them, and the map shapes that make the whole thing bite.

It is **cross-cutting by nature**: it adds state to `WorldState`, wakes two dormant turn phases,
eventually introduces the structure model every future building will use, changes what a legion is, and
rewrites what the AI wants. That is why the build order below is the most important section in this file.

---

## 1. Audit — ten findings before anything is built

An adversarial pass over the ideal and over this map itself. A1 and A2 change the design; **A10 changes this document** — the owner caught the build order backwards and it has been rewritten.

### A1 · **High** — endless grind breaks every one-time-cost mechanic

The frame change to *"no campaign, endless RPG"* (ideal §12.1) has a consequence nobody priced:

> **Any permanent solution to a recurring cost is eventually free.**

Wardens are the clearest case. Bind one demon, and that sector never fades again — *forever*. With an
endless summon faucet, a player eventually wardens everything and the loam constraint evaporates. The
same defect sits in **deep root** (a permanent upkeep reduction, so buy it everywhere) and in
**scorched root** (a one-shot with no replacement cost).

This is not a reason to cut them. It is a test every mechanic in the set now has to pass:

> **The 500-hour test: what does this mechanic look like after 500 hours of grinding?** If the answer
> is "free", it needs an escalating cost, a cap tied to something genuinely scarce, or an expiry.

> ✅ **CLOSED 2026-08-23.** Bounded worlds dissolved most of it — deep root, scorched root, granaries
> and waystations are world-scoped and die with the map, so "permanent" means "for this world".
> Wardens keep their cure: binding one permanently consumes a `demon-contracts` binding slot, already
> Soul-priced and scarce. See `empire-economy-ssot.md` §7.

### A2 · **High** — if you can never hold the whole map, what does "completing" one mean?

Ideal §12.4 says most sectors lose money, so upkeep forces an **equilibrium empire size**. Combined
with §12.1's map-completion progression, this produces a contradiction nobody has stated: **you can
never conquer a map, so completion cannot be conquest.**

The design already contains the answer — §12.3: *taking his capital collapses his economy*. So:

> **Completing a map = taking Zomboss's capital**, not holding every node.

That is a better objective anyway (it gives the map a target rather than a checklist), but it lands a
hard requirement on `world-generator`: **the enemy capital must be reachable and takeable at
equilibrium empire size**, which is a very different constraint from "the map must be holdable".

> ✅ **CLOSED 2026-08-23** — confirmed, and recorded as a `world-generator` constraint in
> `empire-economy-ssot.md` §9.

### A3 · **Medium-high** — two multipliers is one too many

Ideal §8.3 scales upkeep by **distance**; §12.6 scales it by **chaos intensity**. Both survive in the
document and they double-count the same intuition.

**I would drop distance from the upkeep formula.** Distance already costs you — in logistics, in
getting loam there at all (§10). Charging it a second time inside upkeep is invisible to the player
and unfalsifiable in tuning: when an empire stalls you cannot tell which multiplier did it. Chaos
intensity carries "deep is expensive" on its own, is authored per sector, and is *visible*. **P7.**

> ✅ **CLOSED 2026-08-23** — distance dropped. Recorded in `spec-loam-calc.md`.

### A4 · **Medium** — the AI-memory hypothesis is unproven and load-bearing

Ideal §11.1 G12 claims the AI needs no cross-turn memory because *"the world state already is the
memory."* It is an elegant argument and it is **untested**. If it fails, multi-turn logistics needs
persistent AI state, which is an ask-first architectural boundary.

**Test it cheaply and early** — a scripted scenario where a stateless policy must stage loam over
three turns and then build. If that oscillates, we learn it in wave C rather than in wave G.

> ✅ **LARGELY CLOSED 2026-08-23 (owner push).** The risk was overstated, and the mis-statement was
> mine: I described this as being about *AI memory* without saying that the worry is **determinism,
> not storage volume**. Storage would be trivial — this repo already compacts ledgers. The real worry
> was that memory kept in world state gets **hashed**, which couples the save format to the AI
> implementation: change the policy later and every existing save's hash is wrong.
>
> **That coupling is avoidable, because the memory already exists.** `rpg_world_commands` stores every
> AI order with its reason, and `FillAiCommandersUnlocked(db, tx, worldId, turn, …)` runs inside the
> transaction beside `ListWorldCommandsUnlocked(db, tx, worldId, turn)` — so a policy deciding turn N
> can be handed its own orders from turn N−1 with **no new plumbing and no new state**.
>
> It is airtight rather than a trick: **the command log *is* the save** (`RpgStore.WorldTurns.cs:515`
> replays from the identical source), so a policy reading its own past orders reads the same bytes
> live and on replay.
>
> 📌 **L20 still tests it** — but the stop condition is gone. If the stateless goal function
> oscillates, the fix is passing the previous turn's orders in, not an architectural decision.

### A5 · **Medium** — `O(V⁴)` at sixty nodes has never actually been measured

`spec-world-topology.md:52` asserts `ReconnectionCost` is *"fine at six sectors and fine at sixty."*
The six is proven daily. **Nobody has ever run sixty.** DESIGN-GATE evidence rule 4 — *test the
constraint before you declare it* — applies to our own docs too, and the `huge`/`giant` map tiers
(§12.2) are sized on that unmeasured claim. One benchmark settles it.

> 📌 **SCHEDULED as task L11**, which has no dependencies and can run at any time. Still genuinely
> unmeasured.

### A6 · **Medium** — the ideal now has three layers of retraction, and will mislead

`empire-economy-ideal.md` currently reads: §4 superseded by §7.9 · §7.3 amended by §8.1 then restated
by §12.7 · §8.7 retracted by §12.3 · G6 retracted by §12.5. A reader starting at §1 absorbs several
superseded claims before reaching the correction.

DESIGN-GATE §4 is an incident log of exactly this failure. **Consolidate the ideal into a clean
statement before building from it** — the retraction trail belongs at the end, not inline through the
argument.

> ✅ **DONE 2026-08-23.** `empire-economy-ssot.md` is the consolidation; the ideal carries a
> supersession banner and is the reasoning trail only. Every module spec's *Design source* now points
> at the SSOT.

### A7 · **Medium** — the Unmade are a spawner, and spawners are farms

In an endless-grind game, anything that spawns hostiles on a timer is an XP and loot faucet. Faded
sectors producing Unmade means **deliberately abandoning ground could become optimal**.

> ✅ **CLOSED 2026-08-23, and the owner overruled my recommendation — correctly.** I argued for
> pressure-only from 4X instincts; this is an endless-grind RPG, where renewable content is the point.
> They **are** a farm, deliberately, and the throttle is that **farming costs loam** (a legion parked
> in barren ground burns what it carries) plus local depletion plus their own spread. They never drop
> loam. Full design in `empire-economy-ssot.md` §7a.

### A8 · **Low-medium** — is developing a sector rational at all?

Ideal §8.3 says raising development level costs a lump **and** permanently raises upkeep. §12.4 says
most sectors already lose money. Unless yield rises *faster* than upkeep, development is a trap and
nobody will ever do it.

That is fine and probably intended — **development is how a sector escapes the deficit** — but it has
to be stated, or the first tuning pass will price it as a pure cost and quietly kill the builder
layer.

> ✅ **CLOSED 2026-08-23** — stated as a `sector-development` constraint in `empire-economy-ssot.md` §9.

### A9 · **Low** — instrumentation must ship with the calculators, not after them

`economy-principles.md` §13 defines net flow, sink share, binding frequency and payback period. If
those land at the end of the program, every number in waves C–F is guessed and re-guessed. They are
cheap when built alongside the pure calculators and expensive to retrofit.

> ✅ **CLOSED** — the harness is task **L9**, in the same phase as the calculators.

### A10 · **High** — I had the build order backwards (owner, 2026-08-23)

The first draft of §3 put `structure-substrate` first, arguing *"every future building lands on it, so
retrofitting later means touching every one of them."*

**That argument is true at wave 4 and false at wave 1.** The retrofit cost of a foundation is
proportional to how much already sits on it — and at wave 1 nothing does. I applied a foundation-first
instinct to a foundation with no dependents, which is exactly the "generalise before the third use
case" mistake the code-review skill warns about.

**And loam does not need structures at all.** `SlotTypeDef.Yields` already exists, is set on five slot
types, and has **exactly one reader** — a self-consistency check inside its own catalog
(`SlotTypeCatalog.cs:103`). Its comment says *"produces something over time once developed (economy
is a later module)."* The model already anticipates slot-level yield; nothing has ever consumed it.

So a **rootbed slot can seep loam with no structure model in existence.** The habitability rule is
unchanged in wording — *a sector is habitable iff it has a working loam source* — only the **set of
sources** grows later:

| Wave | Sources | Fiction |
|---|---|---|
| 1–4 | the rootbed slot itself | Untended ground seeps a trickle |
| 5+ | a **well** built on it; a **waystation** on a Seat | A well multiplies the seep; a waystation makes ground where there was none |

That is a lower tier of the same thing, not a placeholder to be thrown away. Building loam first also
means that when `structure-substrate` *is* designed, we will have played the economy and will know
exactly what a generator must express — instead of guessing its shape and discovering it is wrong
after four modules depend on it.

**One thing this ordering gives away for free:** with only natural sources, wave 4 has *leap*
expansion and no *creep*. That is not a gap — it is the cleanest possible test of whether waystations
are needed at all. If the map is already interesting when habitable ground is fixed and contested,
creep may be complexity we can decline.

---

## 2. The cut list — what is in the first build

The ideal contains roughly twenty mechanics. The first build contains **five**.

| In | Out, for now |
|---|---|
| Loam as per-sector stock | Granaries and storage caps |
| Chaos intensity per sector | Fade contagion |
| One structure kind: **rootworks** | The Unmade · wardens · prospecting |
| Production and upkeep in the turn | Deep tap · scorched root · reavers · surges |
| Fade → sector lost | Rootwains · map progression |

Everything in the right column is good design. None of it is needed to answer the only question that
matters first: **does anchoring make the map interesting to play?** If the answer is no, the right
column is wasted work.

---

## 3. Modules

**Revised per A10 — loam first, structures fifth.**

| Module id | Responsibility | Depends on | Wave |
|---|---|---|---|
| `loam-model` | Loam stock + capacity on `WorldSector`, `FractureIntensity`, the **rootbed** slot type, canonical form, hash, persistence, validation rules | — | **1** |
| `loam-calc` | Pure and unwired: production from slot sources, upkeep composition, chain gating, fade rate, habitability — **plus the §13 instrumentation harness** | `loam-model` | **1** |
| `loam-turn` | `Production` and `Pressure` wake up; shortfall drains `StabilityMilli`; zero ⇒ `Lost`; the settlement rule; `RulesetVersion` 4 | `loam-calc` | **2** |
| `loam-maps` | Templates with rootbeds, barren ground and a chaos gradient; the five-tier size ladder; `first-light` re-authored | `loam-turn` | **2** |
| **— gate —** | **⭐ Playable here. Does anchoring make the map interesting?** Everything after this is justified by the answer | | |
| `loam-legions` | `WorldEntityMember.Role`, carried loam, bearers, the leash, G1's bootstrap spend; **replaces attrition** | `loam-turn` | **3** |
| `loam-ai` | `ValueMap` habitability gate + loam axes, the `Sever` rule, the march loam gate. **Proves or kills A4** | `loam-maps`, `loam-legions` | **3** |
| `structure-substrate` | `WorldSlot.StructureId`, `StructureCatalog` (kind, cost, upkeep, yield), validation, schema, DTO. **Designed with the economy already played** | `loam-turn` | **4** |
| `loam-structures` | Wells multiply a rootbed's seep; waystations create a source on a Seat; the range rule; construction cost and time | `structure-substrate`, `loam-legions` | **4** |
| `loam-ai-survival` | **One rule**: do not keep what you cannot sustain. Plus `UpkeepHandicapMilli` as a declared, reported balance lever | `loam-turn` | **2** |
| `loam-fe` | Light-in-the-dark overlay, the loam gauge, per-sector net flow on the wire, the abandonment surface. **Pre-gate — the owner cannot judge what they cannot see** | `loam-turn` | **2** |
| `loam-texture` | Whatever survives A1's 500-hour test: granary, contagion, Unmade, wardens, prospecting, surges | `loam-structures` | 5 |

Module specs — **the whole pre-gate slice is specced**: [spec-loam-model](loam/spec-loam-model.md) · [spec-loam-calc](loam/spec-loam-calc.md) · [spec-loam-turn](loam/spec-loam-turn.md) · [spec-loam-maps](loam/spec-loam-maps.md) · [spec-loam-ai-survival](loam/spec-loam-ai-survival.md) · [spec-loam-fe](loam/spec-loam-fe.md) — all **sealed** 2026-08-23.

**Post-gate, sealed 2026-08-23** (owner authorized the post-gate program, then authorized resolving
every open item rather than leaving them for a second pass): [spec-loam-legions](loam/spec-loam-legions.md)
· [spec-loam-ai](loam/spec-loam-ai.md) · [spec-structure-substrate](loam/spec-structure-substrate.md) ·
[spec-loam-structures](loam/spec-loam-structures.md) · [spec-loam-texture](loam/spec-loam-texture.md).

**Adversarially audited 2026-08-23** (owner: "audit the spec, debate and strengthen") — three
independent passes, one real problem confirmed with code quotes and fixed in the specs themselves:
`FadePolicy.DecayFor`'s surge multiplier was originally worded to scale the function's *output*
(post-clamp), which would have let a sector exceed `MaxDecayMilli`'s "no single turn can zero a sector
outright" guarantee; corrected to scale the pre-clamp input instead. Also found and fixed: all five
specs had independently reopened a golden-move budget `tasks/loam-plan.md` explicitly closed at two —
now one batched move across the whole post-gate slice, not five; a habitability claim that assumed
belief already carried structure data it does not; a `Sustain`-timing contradiction inside
`spec-loam-legions.md` itself; a misdescription of `Lost`-handling as already clearing structure state
when it does not; an unbuildable march-loam-gate algorithm, replaced with one the shipped topology code
can actually run; and three accepted, explicitly-stated risks (severance reads near-zero without real
scouting, by design; a homeworld-loss lockout on the range rule, by design; a warded sector still costs
its component's pool and loses its binding on capture). **All five specs are sealed with these fixes
folded in. None of the five post-gate modules are implemented yet** — Phase 2 (Plan/Tasks) has not
started.

Plan and tasks: [tasks/loam-plan.md](../../tasks/loam-plan.md) · [tasks/loam-todo.md](../../tasks/loam-todo.md) — 24 tasks, 5 phases, ending at the gate. The bare `plan.md`/`todo.md` pair belongs to Perf v3 and was not touched.

**Build order:**
`loam-model` → `loam-calc` → `loam-turn` → `loam-maps` → `loam-ai-survival` → `loam-fe` → **⭐ gate** → `loam-legions` → `loam-ai` →
`structure-substrate` → `loam-structures` → `loam-texture`, with `loam-fe` parallel from `loam-turn`.

### What loam needs that already exists

This is why it can go first. Almost nothing is missing.

| Needed | Status |
|---|---|
| Somewhere to put the fade countdown | `StabilityMilli` — shipped, hashed, **unread** |
| A local danger signal to correlate intensity with | `DangerBand` — shipped |
| The chain rule | `SupplyGraph.ConnectedSectors` — shipped, and already computes exactly this set |
| Phases to run production and upkeep in | `Production` and `Pressure` — shipped as `return world;` pass-throughs |
| Slot-level yield | `SlotTypeDef.Yields` — shipped, one reader, that reader is its own validator |
| A terminal state for faded ground | `SectorPhase.Lost` — shipped |
| Report entries that name their ground | `TurnReportEntry.SectorId` — shipped in W39 |
| Version refusal across a ruleset bump | Shipped and already correct |

**Genuinely new in wave 1: two fields on `WorldSector`, one slot-type row, and the arithmetic.**

---

## 4. Why this order — six hazards, five of them learned the hard way

1. **Answer the only question that matters first.** *Does anchoring make the map interesting to play?*
   `loam-maps` answers it with two fields and some arithmetic. Every module after the gate is
   justified by that answer; none of them is worth building if it is no.

2. **Do not build the foundation before it has dependents** (A10). `structure-substrate` designed
   today is a guess about what a generator must express. Designed after wave 4, it is a description of
   something we have played.

3. **Pure calculators before wiring.** The pattern that worked in the AI program: W25–W34 built every
   evaluation table against hand-built fixtures with nothing wired, and checkpoint 9 was literally
   *"the tables exist and still nothing has an opinion."* It caught real bugs early and cheaply.

4. **Maps before AI — non-negotiable.** W37 warned that `first-light` would under-exercise the AI, and
   it did: `Explore` fired three times and never again. Tuning a loam AI against a map with no
   rootbeds, no barren ground and a flat chaos field would teach us nothing true.

5. **AI last within its wave.** *"Zomboss had a faction, a fortress and no army"* — every suite passed,
   the AI worked, and there was simply nobody to be. If loam ships without the AI understanding it,
   Zomboss plays badly and we debug the mechanic instead of the policy.

6. **Instrumentation in wave 1, not wave 5** (A9). Numbers should be *found*. A scripted hundred-turn
   run reporting net flow per stock is the cheapest way to find them, and useless if it arrives last.

---

## 5. What this moves

Known and budgeted, not discovered later:

| Thing | Why | When |
|---|---|---|
| **Every world golden** | `WorldCanonical` hashes sector and slot rows field by field (`WorldCanonical.cs:34,39`). New fields change every hash | Once in wave 1, one reason on the constant |
| **`RulesetVersion` → 4** | `Production` and `Pressure` stop being pass-throughs. Stored reports refuse to re-derive across versions — already built, already correct | Wave 2 |
| **`first-light` again** | It needs rootbeds, barren ground and a chaos gradient, or it cannot exercise any of this | Wave 2 |
| **The FE world fixture** | Regenerated with `FUSIONRPG_BLESS_WORLD_FIXTURE=1`, as before | Wave 2 |
| **New `WorldValidation` rules** | e.g. a rootworks requires a rootbed or Seat slot beneath it; intensity within range | Wave 1 |

Unaffected: the injector (no changes anywhere), `BattleEngine` semantics, the effect Funnel and
Writer paths, and every existing guard — `World/Ai` still may not touch `WorldState`, and the new AI
axes must read belief only.

---

## 6. Open items, triaged by *why* they are open (2026-08-23)

The useful question is not "what is open" but "why". Three answers, and only one of them is a real
question.

### 6a · Decisions I had already made and labelled as questions — **now closed**

Every module spec grew an "Open questions" section by habit, and most entries were recommendations
nobody had disagreed with. That is a way of not deciding that costs the owner attention without buying
anything. Twelve are now closed in place, each with its reasoning:

| Closed | Answer |
|---|---|
| **A3** — distance multiplier | **Dropped.** It sat as an "assumption on an open decision" for a full day of design work. Intensity carries remoteness |
| **S7** — dead development term | Templates author varied development levels, so the term is exercised in wave 2 |
| `MaxIntensityMilli` | **3000.** Past 3× baseline a multiplier drowns its own operands and stops being a gradient |
| `LoamCapacity` as a field | No — a column holding the same number in every row is not data |
| `f(Development, Danger)` | One term, so A8's relationship stays visible |
| Overflow reporting | Per sector; a faction summary hides the only actionable half |
| Capacity constant's home | `LoamPolicy`, with every other number |
| `medium` node count | A **range**, 14–18; a catalog demanding exactly sixteen makes the map serve the catalog |
| Default template | `first-light` until the gate — an unreviewed map should not become every new save |
| FE pinning | After the gate; the automatic rule is playable |
| Gauge placement | World panel — empire scope, per `resource-hub-ssot.md` §4 |
| Player abandonment advice | Yes, as advice, never as an action |

### 6b · Tasks nobody scheduled, mistaken for questions — **convert, do not discuss**

An "open question" with a known method to answer it is not a question. It is an unscheduled task.

| Item | The task that closes it | Due |
|---|---|---|
| **A5** — `O(V⁴)` at sixty nodes | A benchmark. Analytically 60⁴ ≈ 13M inner ops (sub-second) and 128⁴ ≈ 268M (seconds) — which *matches* the spec's claim, but DESIGN-GATE rule 4 says measure, and our own docs are not exempt | wave 1 |
| **A4** — the AI-memory hypothesis | A scripted scenario where a stateless policy must stage over three turns and then act | wave 2, with `loam-ai-survival` |
| **A6** — the ideal's retraction layers | A consolidation pass. It has grown four layers because each round appended instead of restructuring | **before `loam-maps`** |
| **Every number** | The harness. That is what `loam-calc` builds it for | wave 1 |

### 6c · Genuinely the owner's — **and one of them is the request that started all this**

| Item | Why it cannot be derived |
|---|---|
| ~~**A2**~~ | **Closed 2026-08-23** — yes, and it is now a stated `world-generator` constraint in `empire-economy-ssot.md` §9 |
| ~~**§6.3 — the soul mine**~~ | **Closed 2026-08-23** — a plain building, throttled by loam, in `empire-economy-ssot.md` §5. Formerly: ⚠️ the original ask — *"we need a soul generator building for the summon feature, consider it a mine."* The conversation went to principles, then to loam, and never came back. It is correctly *sequenced* after the gate (it needs structures), but it is not answered, and it is a balance decision about the summon economy that no derivation settles |
| ~~**A1's cures**~~ | **Closed 2026-08-23.** Bounded worlds dissolved most of it (world-scoped mechanics die with the map); wardens consume a binding slot; the Unmade are throttled by loam, depletion and their own spread. See `empire-economy-ssot.md` §7 and §7a |
| ~~**Map progression**~~ | **Closed 2026-08-23** — `empire-economy-ssot.md` §4. *You keep who you are, you lose where you were*; and `rpg_worlds.state` already modelled it |

## 6d · The one gap that threatens the gate itself — **G-F, the reward hole**

Found by walking the build, 2026-08-23, and it is not a missing rule. It is a missing *reason*.

**At the gate, loam is the only thing the map produces.** Essence, souls and materials all belong to
`sector-development`, and the Tier-1 → Tier-2 seam (ideal §6.1) is still undecided, so **nothing the
player earns on the map reaches anything they care about.** You hold ground in order to hold ground.

That does not make the gate invalid, but it narrows what it can honestly answer:

| The gate **can** answer | The gate **cannot** answer |
|---|---|
| Is *retrenchment* interesting — is choosing what to let go a real decision? | Is *expansion* interesting for its rewards? |
| Does the fade read as tense or as bookkeeping? | Does territory feel worth fighting for? |
| Is a split economy legible and frightening? | Does the map feed the game the player is actually playing? |

There **is** a thin expansion loop even so — rootbed-dense sectors are net-positive (§12.4 says *most*
sectors lose money, not all), so taking them buys capacity to take more. That is a legitimate 4X loop
and it is enough to test the mechanic. It is not enough to test the *game*.

> **So the gate question must be asked narrowly**, and a "yes, but it feels pointless" answer read
> carefully: that is far more likely to be the missing reward layer than a broken mechanic. Judging
> anchoring by whether the map is *fun* would condemn it for something it was never given.

**Three ways to close it, none free:**

1. **Accept and frame it** *(recommended)* — ask the narrow question at the gate, and schedule the
   reward layer immediately after. Cheapest, and honest as long as the framing is written into the
   playtest brief rather than remembered.
2. **Let rootbeds also yield essence into the treasury** — small, and it makes territory feed fusion
   for the first time. But it needs ideal §6.1's shipping decision, which is open, and it drags a
   second resource into a program that deliberately shipped one.
3. **Pull a slice of `sector-development` forward** — the honest fix and the expensive one. It is a
   different program, and doing it here is how a program becomes two.

## 7. Spec audit — eight findings against my own specs (2026-08-23)

An adversarial pass over the four pre-gate specs. **S3 and S6 have a common cause and a common fix.**

### S6 · **Blocker** — `two-hearths` is invalid by construction

`WorldValidation.cs:149` refuses any world that does not have **exactly one** sector flagged
`SectorTypeFlags.Home`. The gate map is named for having two capitals. It cannot be built, and the
spec that describes it never checked.

### S3 · **Blocker** — the accounting unit is undefined, and the obvious reading breaks the design

`spec-loam-turn` charges each sector's upkeep and never says **where the loam comes from**. Read
literally against `spec-loam-model`'s per-sector `LoamStock`, each sector pays from its own pocket —
which means a deficit sector starves immediately **no matter how rich the empire is**.

That destroys ideal §12.4. If most sectors lose money and nothing subsidises them, the only holdable
ground is ground that already pays for itself, "upkeep is an unavoidable tax every empire must pay"
never happens, and the game becomes *hold the four good sectors*.

**The fix that also resolves S6:** make loam **fungible within a connected component** of a faction's
territory. Sources produce locally; upkeep is paid from the component's pool; **severing a chain
splits the pool.** One rule does three jobs — it is ideal §10.7's automatic flow, it makes severing
economically real without a routing algorithm, and it removes the chain-to-*homeworld* framing
entirely, which is what §12.7 already concluded when it demoted the homeworld to "the densest
concentration" rather than a mechanical exception.

With that, nothing in the loam rules reads `Flags.Home` at all, `WorldValidation` stays untouched, and
Zomboss needs a dense cluster rather than a homeworld of his own.

### S1 · **High** — `loam-fe` is pre-gate, and it is unspecced

The capability map lists FE as "wave 2+, parallel". But **the gate is an owner playtest**, and without
the map overlay and the loam readout the owner can only judge the mechanic from narrative scripts I
write. The VFX stream's standing lesson is the opposite: *trust the owner's eyes over event
telemetry.* FE is **inside** the committed slice, not beside it.

### S2 · **High** — nothing projects the numbers the central decision needs

The decision the whole mechanic exists for is *what do I let go?* Making it requires per-sector
production, upkeep and net flow. All of that is Core-side and pure; **no spec puts any of it on the
wire.** `spec-loam-model` covers raw stock and intensity and stops there. Derived numbers need a
projection, under the same owner-only rule as stock.

### S5 · **Medium-high** — Zomboss will visibly collapse at the gate

No AI work is scheduled before the gate, so at the playtest Zomboss holds ground he cannot afford,
fades, and loses his empire to arithmetic. The owner would be watching a broken opponent and judging
the *mechanic*.

This is *"a faction, a fortress and no army"* wearing a new costume. The ordering rule that put AI
after maps was about **not tuning** against a bad map; giving the AI a single survival rule is not
tuning. A minimal slice — *do not keep what you cannot sustain* — may belong before the gate, with
`Sever` and the value axes staying after it.

### S7 · **Low** — a dead term in wave 2

`LoamUpkeep`'s `f(DevelopmentLevel, DangerBand)` cannot vary in wave 2: nothing raises development
until `sector-development`. Either the templates author varied development levels so the term is
exercised, or the term waits. Templates are the cheaper answer and keep the formula whole.

### S8 · **Low** — one named scenario depends on S3

`spec-loam-turn`'s *"a sector is saved by its own yield"* only means anything under per-sector
accounting. Under component pooling it becomes *"a component is saved by its yield"*, which is a
different and better test. The scenario list is re-derived once S3 is answered.

### S9 · **Low** — the specs never say what the FE fixture carries

`spec-loam-maps` regenerates `world.fixture.json` but no spec states what the new fields look like on
the wire, so the FE work would begin by guessing. Folds into S1 and S2.

### Resolutions (owner, 2026-08-23)

**S3 + S6 — pool per connected component.** Loam is fungible across any chain-connected block of a
faction's territory. Sources produce **locally and unconditionally**; upkeep is paid from the block's
pool; **severing splits the pool.** Consequences:

- Ideal §12.4 becomes buildable — a rich core subsidises a poor frontier, which is the whole point of
  calling upkeep a tax.
- §8.1's "chain to the homeworld" is replaced by "chain to *each other*". A cut source does not go
  dark; it simply can no longer help the rest of you. This is §12.7 followed to its conclusion.
- **Nothing in the loam rules reads `Flags.Home`.** `WorldValidation.cs:149` stays untouched, and
  **S6 dissolves** — Zomboss needs a dense cluster, not a homeworld of his own.
- Severing becomes economic warfare with no routing algorithm anywhere.

**S5 — a minimal survival rule before the gate, plus a declared handicap.** One rule (*do not keep
what you cannot sustain*) lands pre-gate as `loam-ai-survival`. The owner also authorised a balance
lever for Zomboss *"if building the AI is so hard for now"*:

> **It is a `handicap`, never a "cheat".** `FusionRpg.CheatCore` already owns that word for debug
> tooling, and more importantly a hidden fudge cannot survive replay. This is
> `WorldFaction.UpkeepHandicapMilli` — hashed state, 1000 = normal, applied inside `LoamUpkeep`, and
> **named in the turn report when it is not 1000.** A handicap that is visible is a balance lever; one
> that is silent is a bug that explains itself away.

An upkeep discount is the right lever rather than a production bonus: it makes Zomboss *resilient to
bad decisions* instead of merely richer, which is precisely the gap a thin policy leaves.

**S1 + S2 — `loam-fe` moves into the committed pre-gate slice**, and the wire projection of derived
per-sector numbers (production, upkeep, net) comes with it, under the same owner-only rule as stock.
