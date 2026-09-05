# Spec `battle-resources` — give a battle actor its six pools

**Program:** `battle-tempo` · [capability map](../battle-tempo-map.md) · [plan](../../../tasks/battle-tempo-plan.md)
**Module id:** `battle-resources` (stable, kebab-case)
**Status:** specced 2026-09-05, unbuilt.
**Parent SSOT:** [resource-hub-ssot.md](../resource-hub-ssot.md) · [decisions.md](../decisions.md) *Resource model*
**Unblocks:** `battle-tempo-todo.md` `RL2` (partial → complete) and `RL3` (blocked → measurable).

---

## 1. Objective

**Every battle actor's six resource pools are empty, always.** `ReactionCounter.TryCounter` therefore
declines every counter it is ever offered — correctly, honestly, and uselessly. The mechanism
`reaction-lane` built works; nothing gives a battle actor anything to spend.

This is a **wiring gap, not an architectural wall** (CLAUDE.md's own framing): the channels are
registered, the aptitude edges exist, the pool type ships and is production-used. One composer never
learned to seed them.

### The gap, cited

| Fact | Evidence |
|---|---|
| Six resources, closed set, `poise` is the 6th | `DerivedStatChannels.cs:521` · `decisions.md` *Resource model* (**six**, 2026-08-26) |
| `resource.max.*` / `resource.regen.*` are registered and shipped | `resource-hub-ssot.md` §8 · `ActorChannelsTests.cs` (`MaxAndRegenUncapped`) |
| `poise` has full aptitude edges — max, regen, efficiency, restore | `data/tuning/aptitudes.v5.json:2570-2762` (12 sources on max and on regen) |
| **`BattleStatComposer.Compose` seeds no `resource.*` channel at all** | `BattleStatComposer.cs:120-128` — the `FromValues` array is defense/accuracy/dodge/crit-rate/crit-resist/`turn.speed` and nothing else |
| So a battle actor's pool maxes are 0 | `LawnActorResourcePools.GetOrCreate` builds the pool from derived values; absent channel ⇒ 0 |
| The counter declines, by correct logic on empty input | `battle-tempo-todo.md` `TD4` evidence |

**This is not poise-specific.** All six are unseeded. An action in battle can cost nothing at all
today, which is why no cost has ever been observed to bite.

---

## 2. Design

### 2.1 Decision A — seed all six, never poise alone

`resource-hub-ssot.md` §8 carries a **normative, owner-set rule (2026-09-02)**:

> Every derived-stat family that affects a resource MUST cover all six resources. […] A family that
> covers a subset is a **defect, never a feature**.

Shipping a poise-only seed would be exactly that defect, in the same week the rule was written to
forbid it. The module seeds `resource.max.{id}` and `resource.regen.{id}` for **all six ids**, by
looping `DerivedStatChannels.ResourceIds` — never a hand-typed list, per the same section's
*"derive, never hand-list"* fix direction.

⭐ **Consequence worth stating plainly:** this module is what first makes *any* action cost real in a
battle. `poise` is the one the caller is waiting on; it is not the one the design is about.

### 2.2 Decision B — a PROJECTION of the shipped ladder, not a new scale

A resource max is a **magnitude**, so `ssot-power-scale.md` requires it to read `P(Θ)`. It does —
but as a **projection of an existing ladder entry**, not as a new one:

```text
resource.max.{id} = BaseHp(theta) × poolShareMilli[id] / 1000
```

⭐ **Why a projection and not six new `power-scale.v{n}.json` channel rows** (which is what an earlier
draft of this spec implied):

1. **It is the pattern this program already shipped.** `SpeciesTempoProjection`'s own doc comment
   makes exactly this argument — *"A formula, not a lookup, is what keeps this a projection rather
   than a private curve — `ssot-power-scale.md`'s 'one ladder' rule holds by construction because the
   only new number is `referenceIntervalMs` itself."* Here the only new numbers are six shares.
2. **No new power-shaped scale is created, so no §10 inventory row is owed.** §10 is a closed list of
   *scales*; a constant share of an existing scale is not a second scale, it is the same one read
   through a ratio. Writing a fresh `f(level)` would be the defect — this reads `P(Θ)` and multiplies.
3. **`publish.py` cannot add a channel row anyway.** The tool *"refuses to invent a key by design"*,
   so six new `channels` entries in `power-scale.v{n}.json` would require hand-editing a versioned
   tuning file — which `tunables-ssot.md` T4 forbids outright. The projection needs no such row.

⚠️ **Arithmetic discipline** (CLAUDE.md "Numeric overflow"): `long` throughout, **widen before
multiplying** (`BaseHp` already returns `long`), and **divide by 1000 exactly once, last**.

### 2.2a Why its own tuning file, not a `battle.v{n}.json` section

`tempo-content` put its one number in `battle.v{n}.json`, and `BattleStatComposer` already receives
that tuning — so extending it looked cheaper. **It is not possible:** `publish.py`'s `set` path
refuses to create keys, so adding a `resources` section to `battle.v4.json` means hand-editing it.
A **new domain file's v1** is how `action-timing.v1.json` and `reaction-lane.v1.json` were both
legitimately created in this same program.

⚠️ **Known cost, stated because it already bit once this session:** a new tuning file needs a
bootstrap line in every host that resolves battles. Missing it is what produced
`ReactionLanePolicy.Configure(...) has not run` in `MeasProbe` during `LAND1`'s sweep. §4 lists the
call sites.

### 2.3 Decision C — the shape follows `tempo-content`'s own precedent

`tempo-content` solved the structurally identical problem one module ago: a value projected into a
derived channel with a config-driven fallback (`BattleStatComposer.cs:113-118`,
`SpeciesTempoProjection.SpeedFor`). `battle-resources` reuses that shape rather than inventing a
second one:

```text
resource.max.{id}   = BattleRuleset.BaseResourceMax(theta, id)     // from the ladder
resource.regen.{id} = 0 for every id in v1                        // see §2.6 — deliberate
```

Aptitude investment, traits and equipment then overlay **additively through the paths that already
exist** (`ChannelMods`, `TraitAtomSource`, `EquipAtomSource` — `BattleStatComposer.cs:135-164`). No
new bridge into the aptitude subsystem, and no second seam.

⛔ **No per-species authored override in v1.** An earlier draft of this spec claimed *"an actor with
an authored per-species override uses it"* — **`BattleActorSetup` has no such field**, so that claim
described a system that does not exist. Adding one is a real change to the setup contract and every
caller that builds it; it is named as a follow-up (§10), not smuggled in here.

### 2.4 Seeding `max` alone is sufficient — verified, not assumed

`ActorResourcePools.CreateFull` (`ActorResourcePools.cs:21-27`) starts **every pool at its max**,
reading `ResourceChannelReader.Max(derived, id)` per id. `max` and `rate` are then re-read fresh from
the snapshot on every `Resolve`/`TrySpend` rather than cached (`ActorResourcePools.cs:51-78`), so a
seeded channel takes effect immediately and dynamically.

That is the whole mechanism of today's bug: with `resource.max.poise` absent, `Max` returns 0,
`Resolve` clamps to `[0, 0]`, and `TrySpend` refuses every amount above zero. **Seeding `max` is
therefore both necessary and sufficient to make a counter possible** — no change to the pool type,
`PoiseLedger`, or `ReactionCounter` is required.

### 2.5 ⛔ Decision D — the regen quantization cliff, and why v1 regen is zero

**This is the module's central hazard and the earlier draft missed it entirely.**

`ResourceChannelReader.RegenPerTick` rounds the channel to a **whole `long`**:

```csharp
(long)Math.Round(snap.Get(DerivedStatChannels.ResourceRegen(resourceId)), MidpointRounding.AwayFromZero)
```

and `ResourcePoolState.Resolve` accrues `ratePerTick * elapsed`. So in-battle regen has **no usable
small values** — it is 0, or it is at least 1 per tick, with nothing between.

Size that against the shipped tick scale (`action-timing.v1.json`): the basic attack alone is
**150 wind-up + 50 recovery ticks**, and category time-costs are 80–120, so a single round runs
**several hundred ticks**. At the smallest non-zero rate, 1/tick:

| | per 300-tick round |
|---|---|
| Regen at rate 1 | **+300 poise** |
| `reaction-lane.v1.json` `poiseSpend` | **100** |

⚠️ **The smallest expressible regen refills three counters' worth every round** — which erases the
scarcity the entire `poise` design exists to create (`resource-hub-ssot.md` §2: a counter "competes
with guarding"; `RL2`'s own tested property is that "a bigger spend leaves less poise for what comes
next"). An aptitude-invested actor is worse: `aptitudes.v5.json`'s `resource.regen.poise` reaches
`kMilli: 60` on Bulwark, which composes well past 1/tick.

**Decision: `BaseResourceRegen` returns 0 for every id in v1, and this is a design position, not a
placeholder.** Two independent reasons:

1. **The SSOT already says so.** §11: *"Pools persist across a run and refill **at rest**. They are
   not per-encounter."* A battle is not a rest. A pool that refills mid-battle is the per-encounter
   model the hub explicitly rejects.
2. **The cliff makes any non-zero value wrong.** Not "needs tuning" — *unrepresentable*. There is no
   value between "nothing" and "three counters a round."

⚠️ **`hp` regen is 0 for the same reason and one more:** a non-zero `resource.regen.hp` would heal
every actor mid-battle, which is a combat-model change wearing a resource-seeding costume.

⭐ **The follow-up is named, not hidden (§10):** if a balance pass later wants real in-battle regen,
it needs a sub-tick unit — a per-mille-per-tick read, or accrual against a coarser clock. That is a
change to `ResourceChannelReader`, which the lawn path also uses, so it is **cross-cutting and its own
module**. Seeding regen at 0 today does not foreclose it.

### 2.5a `resource.efficiency.*` is deliberately not seeded

The third registered family is left unseeded, which resolves to 0 — and **0 is the correct neutral**:
efficiency reduces cost, so absent means "no discount," not "no efficiency." Seeding it would be
inventing a discount nobody asked for. Called out because §2.1's six-coverage rule is about families
that *affect* a resource, and a reader could otherwise think this module skipped a third of its job.

### 2.4 Decision D — every number is a tunable, and honestly marked unmeasured

New `data/tuning/battle-resources.v1.json`, published via `python tools/tuning/publish.py`, never
hand-edited. Per-resource base and per-Θ growth for `max`.

⚠️ **Regen is a structural `0`, not a tuning row** (§2.5) — putting it in the file would invite a
balance pass to set it to 1, which the cliff makes catastrophic. When follow-up 1 lands a sub-tick
unit, regen earns its rows then. The file's `_meta` says this, so the absence reads as a decision
rather than an omission.

⚠️ **`_meta.balanceStatus` marks every coefficient an unmeasured placeholder**, exactly as
`action-timing.v1.json` did for its own first landing (`AT1`'s evidence). These numbers decide how
often a guard can be raised and how much a counter can spend; they are a balance pass's job, and
pretending the first authored value is calibrated is the failure that convention exists to prevent.

⚠️ `long` for every magnitude; widen before multiplying; divide by 1000 last; overflow throws.

### 2.6 Decision E — `hp` is seeded but not owned here

`hp` is in `ResourceIds` and so is covered by the six-coverage rule, but a battle actor's HP is
already owned by `BattleActorSetup.MaxHp` and the turn FSM's `Downed` state
(`resource-hub-ssot.md` §1, §10). This module writes `resource.max.hp` to keep the family complete
and **must make it agree with `setup.MaxHp` rather than introduce a second HP number** — two
disagreeing HP maxima is a worse outcome than an incomplete family, so the seed reads `setup.MaxHp`
directly instead of the ladder. Stated here because it is the one id where "cover all six" and "one
source of truth" pull against each other, and a later session will otherwise re-litigate it.

⛔ **`resource.max.hp` is a mirror, never a second writer.** Nothing in this module may *change* an
actor's HP, drive death, or be read as current HP — `hp` depletion is death and belongs to the turn
FSM. The channel exists so the family is complete and so a future `hp`-sacrifice cost
(`decisions.md`, 2026-08-30) has a max to price against.

### 2.7 Why goldens cannot move — the strong form

The earlier draft argued this only from "resource channels never join `CombatChannelFamilies`". That
is true but it is the weaker half. The decisive fact:

⭐ **`state.ResourcePools` has exactly ONE reader in the entire codebase** —
`Actions/TimelineDispatch.cs:167`, inside `RunTimelineActionPhase` — and that method runs only when
`BattleModeProfile.UsesTimelineDispatch` is `true`, which **no row in `BattleModeProfileCatalog`
sets** (`spec-timeline-dispatch.md` §8.1, test-enforced). So in every shipped battle the seeded pools
are constructed, never read, and cannot influence a single resolved number.

Both halves together: the channels are inert to the combat roster *and* the pools are unreachable on
the shipped path. ⛔ **Both are still claims until §6.6 runs the suites** — DESIGN-GATE evidence rule
4 exists because "this cannot move goldens" has been wrong before in this repo.

---

## 3. Commands

```powershell
dotnet build src/FusionRpg.Core
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~BattleResource"
python tools/tuning/publish.py                     # never hand-edit the tuning json
python scripts/audit-overflow.py --paths src/FusionRpg.Core/Battle
python scripts/audit-magic-numbers.py --summary    # M1 must stay 0
.\scripts\guard-single-writer.ps1 ; .\scripts\guard-funnel-delta.ps1
.\scripts\guard-dal.ps1 ; .\scripts\guard-secondary-no-unity.ps1
```

---

## 4. Project structure

| Path | Role |
|---|---|
| `data/tuning/battle-resources.v1.json` | **New.** Per-resource base/growth/regen; `_meta.balanceStatus: unmeasured` |
| `src/FusionRpg.Core/Battle/BattleResourceTuning.cs` | **New.** Pure parser. ⛔ A missing key is a rejection naming it, never a default |
| `src/FusionRpg.Core/Battle/BattleRuleset.cs` | Gains `BaseResourceMax` / `BaseResourceRegen`, beside the existing `BaseAccuracy`/`BaseDodge`/… |
| `src/FusionRpg.Core/Battle/BattleStatComposer.cs` | The seeding loop over `ResourceIds`, next to the existing `turn.speed` seed |
| `docs/architecture/power/ssot-power-scale.md` | §10 inventory gains the resource scale (§2.2) |
| `tests/FusionRpg.Core.Tests/Battle/BattleResourceSeedTests.cs` | **New.** |

---

## 5. Code style

Match `BattleStatComposer`'s existing seeding block — the new channels join the same
`ActorDerivedSnapshot.FromValues` construction rather than a second pass, and the comment says *why*
(the wiring gap) rather than *what*:

```csharp
// battle-tempo `battle-resources` -- every battle actor had all six pools at max 0, so no action
// could cost anything and reaction-lane's counter declined every time (TD4's finding). Loops
// ResourceIds rather than listing ids: resource-hub-ssot.md §8's six-coverage rule is normative,
// and "derive, never hand-list" is its own stated fix direction.
foreach (var id in DerivedStatChannels.ResourceIds)
{
    seeds.Add(new(DerivedStatChannels.ResourceMax(id), BattleRuleset.BaseResourceMax(theta, id, setup.MaxHp)));

    // Regen is 0 for every id, deliberately -- spec §2.5. ResourceChannelReader.RegenPerTick rounds
    // to a whole long, and a round is several hundred ticks, so the smallest non-zero rate refills
    // ~3x poiseSpend per round and erases the scarcity poise exists to create. The hub's own §11
    // ("pools refill AT REST") says the same thing from the design side. A sub-tick unit is a named
    // follow-up, not a value to guess at here.
    seeds.Add(new(DerivedStatChannels.ResourceRegen(id), 0));
}
```

---

## 6. Testing strategy

1. ⭐ **A falsifier for every behavioural assertion.** Break the seed on purpose and confirm the test
   reddens — a passing test proves nothing until it can fail.
2. **All six covered, by construction not by enumeration.** A test asserting the seeded channel set
   equals `ResourceIds` projected through `ResourceMax`/`ResourceRegen` — so a seventh resource is
   covered automatically and a hand-listed regression fails loudly.
3. **A counter actually fires.** The `RL2` acceptance line that has never once been observed true:
   with pools seeded, `ReactionCounter.TryCounter` commits poise and deals `Riposte` damage in a real
   battle. ⭐ This is the assertion the whole module exists to make possible.
4. **An exhausted actor still declines**, and declining stays observable as a typed refusal — the
   affordability judgement must survive the pools becoming non-empty. ⭐ Drain the pool below
   `poiseSpend` and assert the refusal, so "declines" is proven to still mean something.
5. **`hp` agrees with `setup.MaxHp`** (§2.6), asserted directly — no second HP number.
5a. ⛔ **Regen is exactly 0 for every id** — asserted as a *property*, not left implicit. ⭐ Its
   falsifier is the one that matters most: set `resource.regen.poise` to 1, run a multi-round battle,
   and assert the pool refills faster than `poiseSpend` drains it — proving §2.5's cliff is real and
   that the zero is load-bearing rather than a value nobody chose.
5b. **Nothing heals.** A multi-round battle's per-actor `HpRemaining` is monotonically
   non-increasing — the direct guard on §2.6's "seeding must not become a second HP writer."
6. ⛔ **Golden movement is measured, not predicted** (DESIGN-GATE evidence rule 4). §2.7 gives two
   independent reasons the expectation is *zero* — resource channels never join
   `CombatChannelFamilies`, and `state.ResourcePools`' only reader (`TimelineDispatch.cs:167`) is
   unreachable on every shipped profile. **Run `BattleGoldenTests`, `HybridAtbSweepTests` and
   `MeasProbe` (byte-for-byte against its recorded baseline) and report what actually moved.** A small
   delta is a finding, not a failure; an assumed zero is the defect this rule exists to stop.
7. **Overflow + magic numbers** clean on every touched path; all four boundary guards green.

---

## 7. Boundaries

- **Always:** loop `ResourceIds`; keep every number in the tuning file; `long` for magnitudes; seed
  through `BattleRuleset` so the power ladder stays single.
- **Ask first:** any change to what a resource *means* (`resource-hub-ssot.md` §2's "pays for" column
  is normative — a cost on a resource whose meaning is undecided is an authoring error).
- **Never:** a poise-only seed (§2.1); a private `f(level)` in the composer (§2.2); a hand-typed
  resource list anywhere; a hard cap on a pool maximum — `PS-8` makes a cap on a magnitude a
  progression ceiling until proven otherwise, so bounds are configurable soft caps and absolute
  bounds throw rather than clamp.
- **Never:** bump `RulesetVersion` in this module. If §6.6 finds real golden movement, that is
  `LAND1`/`LAND2`'s owner-gated landing, not this module's to shorten.

---

## 8. Success criteria

1. All six `resource.max.*` and `resource.regen.*` channels are seeded for every battle actor, proven
   by a test that derives the expected set from `ResourceIds` rather than listing it.
2. A counter commits `poise` and deals `Riposte` damage in a real battle — `RL2`'s acceptance line,
   observed true for the first time.
3. `RL3` becomes measurable: a spend range can be sized against a lane that actually fires.
4. `resource.max.hp` agrees with `setup.MaxHp`; no second HP number exists; no actor's HP ever rises
   during a battle.
5. **Regen is 0 for all six**, with the cliff falsifier (§6.5a) executed — proving the zero is a
   decision that would break things if reversed, not a value nobody set.
6. Every `max` coefficient lives in `data/tuning/battle-resources.v1.json` with
   `_meta.balanceStatus: unmeasured`; `M1 = 0`; a planted literal makes `M1` rise.
7. Golden movement **measured and reported** across `BattleGoldenTests`, `HybridAtbSweepTests` and
   `MeasProbe` — expected zero for the two reasons in §2.7, but stated from a run, not a prediction.
8. The resource scale appears in `ssot-power-scale.md` §10's inventory.
9. The four §10 follow-ups are recorded in `battle-tempo-todo.md` so they outlive this session.

---

## 9. Open questions

**One, and it is a balance question rather than a design one:** the placeholder `max` coefficients
decide how many counters a battle affords. They ship marked `unmeasured` and are sized by a later
balance pass against a lane that fires — which cannot happen until this module lands. That ordering
is deliberate, not an oversight.

⭐ **Regen is NOT on this list.** §2.5 settles it at 0 on a design argument (the SSOT's refill-at-rest
rule) plus a representability argument (the quantization cliff), not as a number awaiting tuning.

---

## 10. Named follow-ups — deliberately out of scope, recorded so they are not lost

| # | Follow-up | Why not here |
|---|---|---|
| 1 | **Sub-tick regen** — a per-mille-per-tick read, or accrual against a coarser clock, so in-battle regen has values between 0 and "three counters a round" (§2.5) | Changes `ResourceChannelReader`, which the lawn path also uses. Cross-cutting; its own module |
| 2 | **Per-species authored pool overrides** — a `BattleActorSetup` field so a tanky species can carry more `poise` than the ladder default | A real change to the setup contract and every caller that builds one. `tempo-content` earned its `AttackIntervalMs` field the same way, as its own task |
| 3 | **Poise exhaustion as a status** — `resource-hub-ssot.md` §10 says every non-`hp` pool debuffs derived stats at zero via `StatusRuntime` | Owned by `class-system`'s `spec-guard-economy.md`, not by seeding. This module makes the pool reach zero for the first time, which is what makes that spec testable |
| 4 | **Persisted pools across battles** — `ActorResourcePools.FromStored` exists and is unused here; v1 starts every battle full | `T18`'s job per `CreateFull`'s own doc comment. Starting full is the documented sane default, not an oversight |

⚠️ **Follow-up 3 is the one most likely to surprise someone**: this module makes it possible for a
battle actor's `poise` to hit zero, and today nothing happens when it does. That is correct for v1 —
the counter simply declines — but it means "guard breaks" is still unimplemented behaviour, and a
reader who assumes seeding delivered the whole guard economy would be wrong.
