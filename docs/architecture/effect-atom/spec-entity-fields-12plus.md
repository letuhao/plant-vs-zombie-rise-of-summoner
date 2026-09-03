# Spec: entity-fields-12plus (E38)

Module **E38** in the [atom effect map](../effect-atom-map.md) §13 (Wave 8). Depends on **E30**
(`channel-pool`). Ideal: [effect-atom-ideal.md](../effect-atom-ideal.md) §W8.4 row 4.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit.
> Where this spec and the definitions disagree, **the definitions win**. E38 authors magnitudes from
> its §2 units. ⛔ **CORRECTED 2026-09-03**: the item program *proposed* the units fix on 2026-08-22, but
> `definitions.md` itself carried the wrong row until **E42** applied it on 2026-09-03. E38 depends on
> E42, not merely on the 2026-08-22 handoff.

> **⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — the coefficient data path.**
> Coefficients do **not** live in `data/tuning/effects.v1.json`: that file has no coefficient section
> (its keys are `matchupRead` and `damageFxFloater`), and `CoefficientTable` never reads `data/tuning/`
> at all — the only reader is `RpgStore.GetPowerTables` over the **content-hashed** `power_coefficient`
> table (`RpgStore.Power.cs:61-72`; hash registry V3 at `ContentHashRegistry.cs:148-160`). **The path is
> a seed kind, `data/seed/power/coefficients.v1.json`**, decided in full at
> [`spec-power-sweep.md`](spec-power-sweep.md) §4.1. `CoefficientTable.Authored()` stays the
> no-database fallback and this module does not edit it. **This spec's coefficient row lands in that
> seed file.**


## Objective

**This is [`channel-extension` (E16)](spec-channel-extension.md) run a second time.** E16 promoted
three cheat-document keys to real composed channels and took the primary list from 8 to 11. Twelve
more Unity fields are written today from cheat keys only, straight past the modifier bag, so no effect
can reach them — including `takeDmgMultiplier`, the "takes +X% damage" knob every debuff design wants.
E38 promotes those twelve. Primary channels go **11 → 23**.

Read E16's spec before this one. Its locked decisions — channel direction, the interval floor, cheat
keys becoming `Override` modifiers, the extras path stopping — are the shape E38 repeats, not
re-decides.

## 1. What exists today

### Built

| Fact | Where |
|---|---|
| Eleven primary channels, in declaration order | `src/FusionRpg.Core/Stats/ModifierOp.cs:26-50` |
| `AtomKindRegistry.PrimaryChannels` **reads** `StatChannels.All` rather than copying it | `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:40` |
| Rule G6 refuses an unknown primary channel on `stat.modify` | `AtomKindRegistry.cs:64-73` |
| Channel direction is declared once and read downstream; an imported `effect_channel_policy` row can override it (E22) | `ModifierOp.cs:20-24`, `ModifierOp.cs:59-72` |
| Compose cases and the interval floor | `src/FusionRpg.Core/Stats/StatComposer.cs:91-108` |
| `EntityBaseline` / `EntityFinal` carry a field per channel | `src/FusionRpg.Core/Stats/EntityBaseline.cs:4-47` |
| E16's three keys arrive as `Override` modifiers instead of direct writes | `src/FusionRpg.Injector/CheatState.cs:572-591` |

### Wiring gap — the fields are written today, from cheat state only

All twelve are already injector-writable. Every one of them bypasses the bag.

| Field | Side | Cheat key | Writer |
|---|---|---|---|
| `theShieldHealth` (`int`) | plant | `P-SHIELD` | `src/FusionRpg.Injector/Stats/EntityStatWriter.cs:102-103` |
| `thePlantAttackCountDown` (`float`) | plant | `P-ATK-CD` | `EntityStatWriter.cs:111-112` |
| `attackSpeedAdder` (`float`) | plant | `P-ATK-ADD` | `EntityStatWriter.cs:113-114` |
| `thePlantProduceCountDown` (`float`) | plant | `P-PROD-CD` | `EntityStatWriter.cs:115-116` |
| `thePlantSpeed` (`float`) | plant | `P-SPEED` | `EntityStatWriter.cs:117-118` |
| `moveSpeed` (`float`) | plant | `P-MOVE` | `EntityStatWriter.cs:119-120` |
| `theLevel` (`int`) | plant | `P-LEVEL` | `EntityStatWriter.cs:121-122` |
| `shootingLevel` (`int`) | plant | `P-SHOOTLVL` | `EntityStatWriter.cs:123-124` |
| `theArmor` (`float`) | zombie | `Z-ARMOR-F` | `EntityStatWriter.cs:139-140` |
| `takeDmgMultiplier` (`float`) | zombie | `Z-TAKEMULT` | `EntityStatWriter.cs:141-142` |
| `theSpeed` (`float`) | zombie | `Z-SPD` | `EntityStatWriter.cs:145-146` |
| `theOriginSpeed` (`float`) | zombie | `Z-SPD-O` | `EntityStatWriter.cs:147-148` |

### Real gap

| Gap | Note |
|---|---|
| G6's own message text is stale | `AtomKindRegistry.cs:70-72` still tells an author that `attackInterval` / `produceInterval` / `zombieSpeed` "bypass the modifier bag; E16 promotes them" — E16 shipped, and they are in `StatChannels.All:46-50` |
| No coefficient row for any of the twelve | `CoefficientTable.Authored()` (`CoefficientTable.cs:120-147`) lists eight primary channels. Everything else falls back to `("stat.modify", "", 1000, 10)` at `CoefficientTable.cs:130` — which prices a plant level like ten hit points |
| E16's own boundary says **"Never: promote `Z-TAKEMULT`"** and marks it **LIVE-inconclusive** | `spec-channel-extension.md` §"Scope discipline". Map §13 supersedes that scope cut by naming the field; the **LIVE-inconclusive** status is not superseded and is an acceptance criterion below, not a footnote |

## 2. The contract

### 2a. The twelve channels

| Channel | Unity field | Side | Direction | Storage |
|---|---|---|---|---|
| `plantShield` | `theShieldHealth` | plant | higher | `long`, clamped to `int` at write |
| `attackCountdown` | `thePlantAttackCountDown` | plant | **lower** | `double` |
| `attackSpeedAdder` | `attackSpeedAdder` | plant | higher | `double` |
| `produceCountdown` | `thePlantProduceCountDown` | plant | **lower** | `double` |
| `plantSpeed` | `thePlantSpeed` | plant | higher | `double` |
| `plantMoveSpeed` | `moveSpeed` | plant | higher | `double` |
| `plantLevel` | `theLevel` | plant | higher | `long`, clamped to `int` |
| `shootingLevel` | `shootingLevel` | plant | higher | `long`, clamped to `int` |
| `armorFlat` | `theArmor` | zombie | higher | `double` |
| `takeDmgMultiplier` | `takeDmgMultiplier` | zombie | **lower** | `double` |
| `zombieSpeedCurrent` | `theSpeed` | zombie | higher | `double` |
| `zombieOriginSpeed` | `theOriginSpeed` | zombie | higher | `double` |

Each gets exactly what E16's three got: a `StatChannels` constant, a `DirectionOf` arm where the
default is not `HigherIsBetter`, a `StatComposer` case, an `EntityBaseline`/`EntityFinal` field, and an
`EntityStatWriter` case.

**Direction is per channel, not per side** — `zombieSpeed` is already `HigherIsBetter` even though a
fast zombie is bad for the player (`ModifierOp.cs:67-71`). E38 keeps that convention and does not
introduce side-relative direction; `takeDmgMultiplier` is `LowerIsBetter` because lower is better *for
the bearer*, which is the same frame `attackInterval` uses.

### 2b. The extras path stops, and the cheat keys become `Override`

E16's lock, repeated: two write paths to one field fight the composer, last-write-wins and
spawn-order-dependent, so the same board can settle differently twice. Every promoted key routes
through `CheatAbsoluteStatPlugin` (`src/FusionRpg.Core/Stats/Plugins/CheatAbsoluteStatPlugin.cs`), and
`WritePlantExtras` / `WriteZombieExtras` stop writing the twelve.

> ⚠ **`BuildPlantAbsolute`'s int map filters `if (v > 0)`** (`CheatState.cs:546-559`, the filter at
> `:553`; `BuildZombieAbsolute` at `:594-608` does the same at `:601`), while the extras
> path guards these keys with `>= 0` (`EntityStatWriter.cs:121-124`). Moving `P-LEVEL` or
> `P-SHOOTLVL` into that map as-is **silently drops a legitimate zero**. Use the `>= 0` guard, or the
> operator loses the ability to set level 0 and nothing reports it.

#### The twelve do not share one guard shape — and one of them has no guard at all

> **⛔ CORRECTED 2026-09-03 — §2b covered `>= 0` vs `> 0` for two keys and left four wrong and one
> missing.** The warning above generalised from `P-LEVEL`/`P-SHOOTLVL`. Reading all twelve write sites
> shows **three** shapes, not one, and the promotion has to preserve each.

| Shape | Keys | Cite |
|---|---|---|
| `IsUserSet && value >= 0` — a zero is legal and must survive | `P-SHIELD` · `P-ATK-CD` · `P-PROD-CD` · `P-LEVEL` · `P-SHOOTLVL` · `Z-ARMOR-F` · `Z-TAKEMULT` | `EntityStatWriter.cs:102`, `:111`, `:115`, `:121`, `:123`, `:139`, `:141` |
| `IsUserSet && value > 0` — **a zero is already refused today** | `P-SPEED` · `P-MOVE` · `Z-SPD` · `Z-SPD-O` | `EntityStatWriter.cs:117`, `:119`, `:145`, `:147` |
| **`IsUserSet` only — no value guard whatsoever** | `P-ATK-ADD` | `EntityStatWriter.cs:113-114` |

**Why each shape matters to the promotion:**

- **The seven `>= 0` keys** are the case §2b's warning already covers: both absolute maps filter
  `v > 0` (`CheatState.cs:553`, `:579`, `:601`), so moving any of them across unchanged loses the
  zero. `Z-TAKEMULT` at zero is *immune to damage* and `P-SHIELD` at zero is *no shield* — both are
  settings an operator means, not the absence of one.
- **The four `> 0` keys** are the opposite trap: they already refuse zero, so routing them through a
  `>= 0` path would make `P-SPEED 0` reach `thePlantSpeed` for the first time and freeze a plant.
  **Preserve the refusal**, and say in a comment that it is a structural floor (a zero speed is not a
  balance value; it is a stuck entity), not a progression cap.
- **`P-ATK-ADD` has no value guard at all** — `if (CheatState.IsUserSet("P-ATK-ADD")) p.attackSpeedAdder = CheatState.FVal("P-ATK-ADD");`
  and nothing else. A negative attack-speed adder reaches the Unity field today.

  > **⛔ DECIDED 2026-09-03 (owner removed themselves as a gate): negative is meaningful. `P-ATK-ADD`
  > stays unguarded, and the comment says why.**
  >
  > **An adder is a signed delta by construction** — that is what distinguishes it from
  > `P-ATK-CD`/`P-PROD-CD` (countdowns, `>= 0`, a duration cannot be negative) and from
  > `P-SPEED`/`P-MOVE`/`Z-SPD` (speeds, `> 0`, a zero freezes the entity). A delta with a sign is the
  > one shape in the twelve for which "negative" is an ordinary value, and the repo already treats a
  > deliberate drawback as legitimate content — E28's `backwards-interval` lint **warns and does not
  > block** on exactly this kind of authoring.
  >
  > **The tie-break is that keeping it unguarded is the reversible choice.** Adding the guard is a
  > **behaviour change to a shipped operator key** — an operator who has been setting a negative adder
  > loses it silently. Keeping it is the status quo, and adding a guard later costs one line if a
  > reason appears.
  >
  > **Where the floor belongs if one is ever needed:** on the **composed** attack rate, not on the
  > adder. That is where `StatChannels.MinimumInterval` already lives (`ModifierOp.cs:57`) and it is
  > the only place that can see whether the sum went non-positive — an adder cannot know what it is
  > being added to. **A per-key clamp would be the wrong layer even if the value were wrong.**
  >
  > **What would overturn it, and it is a live check this decision does not block on:** if a
  > sufficiently negative `attackSpeedAdder` makes a plant stop firing entirely rather than fire
  > slower, the value is structurally invalid, not merely a drawback, and the floor above becomes
  > required. **See §2b.1 — it does not block any other work in this module.**

#### 2b.1 Criteria-stated task (needs a live lawn, blocks nothing)

**What to check:** with a plant on the board, set `P-ATK-ADD` to a large negative value (start at
`-100`, then `-1000`) and watch whether the plant's attack rate degrades smoothly or the plant stops
attacking / behaves erratically.

**Pass:** the plant fires more slowly and keeps firing. `P-ATK-ADD` stays unguarded per §2b, and the
task closes with the observation recorded.

**Fail:** the plant stops firing, fires infinitely fast, or throws. Then the composed attack rate gets
a **structural** floor at `StatChannels.MinimumInterval`'s call site, with the comment `AGENTS.md`
requires — still not a guard on the key.

**Why it blocks nothing:** the decision above is *"keep today's behaviour"*, so every other promotion
in this module proceeds on the eleven guarded keys and on `P-ATK-ADD` unchanged. The task can run
before, during, or after.

**None of the three is a decision this spec gets to skip.** Twelve promotions with one assumed guard
shape is how a key changes meaning during a refactor nobody reviewed as a behaviour change.

`guard-single-writer.ps1` reads `EntityStatWriter.cs` **as text** and cannot tell code from a comment
(`EntityStatWriter.cs:109-110`). Do not quote a removed field assignment in a comment.

### 2c. Pricing

Twelve rows in `data/seed/power/coefficients.v1.json` (see the decision below), which import into
`power_coefficient`. **`CoefficientTable.Authored()` is not edited** — it is the no-database fallback,
and a coefficient added there would move every golden with no content-hash change. A missing row is **not** zero — `CoefficientTable.Find` falls back to
the kind's channel-less row (`CoefficientTable.cs:75-82`), so an unpriced level-up quietly prices as
ten hit points rather than reporting `unpriced`. That fallback is why every one of the twelve needs an
explicit row even where the number is a guess.

The two `LowerIsBetter` countdowns already price correctly through E16's sign flip
(`CostFunction.cs:71-75`) once `DirectionOf` knows them.

#### `takeDmgMultiplier` is the third `LowerIsBetter` channel, and the sign flip inverts its headline use

> **⛔ ADDED 2026-09-03 — this section said "the two countdowns" while §2a marks **three** channels
> `LowerIsBetter` and criterion 6 says "the three". The third is the module's own headline channel,
> and it is the one the sign flip mishandles.**

The flip reads:

```csharp
// CostFunction.cs:74-75 (its rationale comment at :71-73)
if (StatChannels.IsLowerBetter(StringOf(pars, "channel")) && SignOf(atom, kind, pars) > 0)
    points = -points;
```

For `attackCountdown` and `produceCountdown` that is exactly right: raising a countdown is a penalty,
so it prices negative. **For `takeDmgMultiplier` it inverts the knob E38 exists to deliver.** §2a marks
the channel `LowerIsBetter` *"because lower is better for the bearer"*, which is true — and it means a
**raise**, the *"this target takes +X% damage"* debuff that every design wants, prices as **negative
power**. A container whose defining atom is a bigger debuff would be **cheaper** the stronger it gets.

That is the same failure the comment four lines above the flip records already happening once:
*"Pricing the signed value made every damage atom worth a negative amount — so a budget over a damage
item RELAXED as the item got deadlier"* (`CostFunction.cs:60-63`).

**The two frames E38 had to choose between** — it is a choice, not a bug with one fix. **Decided 2026-09-03, immediately after this list:**

1. **Bearer frame, kept.** `takeDmgMultiplier` stays `LowerIsBetter`, and a raise on *oneself* is
   correctly a penalty. A debuff **applied to an enemy** is then not a `stat.modify` on the caster's
   own channel at all — it is a `status.apply` whose payload carries the modifier, and the pricing of
   *that* is the status's, not the channel's. **This is the likely answer**, and it means E38 must say
   plainly that `takeDmgMultiplier` is not the authoring surface for *"enemies take more damage"*.
2. **Target frame.** The channel is declared `HigherIsBetter` on the ground that content only ever
   raises it on someone else. That makes a self-buff *reducing* it price as a penalty, which is worse.

#### ⛔ DECIDED 2026-09-03 (owner removed themselves as a gate) — **option 1, the bearer frame.** `takeDmgMultiplier` stays `LowerIsBetter`

The spec called option 1 *"the likely answer"*; it is now the decision. Three reasons, in the order
they carry weight:

1. **Option 2 is worse in the same way, on a case that is more common.** Declaring the channel
   `HigherIsBetter` makes a **self-buff** — *"I take 20% less damage"*, the ordinary defensive
   affix — price as a **penalty**. That is the same sign inversion moved onto the more frequently
   authored direction, so it trades a rare mispricing for a common one.
2. **The channel is a bearer's own stat, and everything that writes it writes a bearer's.**
   `Z-TAKEMULT` reaches `EntityStatWriter.cs:141` on the entity being written, and §2a's own
   justification is *"lower is better **for the bearer**"*. Nothing in the write surface expresses
   "someone else's `takeDmgMultiplier`" — the frame is not a choice the code left open, it is the
   frame the code already has.
3. **The debuff has a correct home already.** *"This target takes +X% damage"* is a `status.apply`
   whose payload carries the modifier, and the status is priced as a status — by its own coefficient,
   its trigger frequency and its uptime, which is a better model of a debuff's worth than a flat
   channel magnitude anyway.

**So E38 must say plainly, in §2a and in the coefficient row's comment: `takeDmgMultiplier` is not the
authoring surface for *"enemies take more damage"*.** That sentence is the deliverable of this
decision — without it, the first author to want the debuff reaches for the channel, gets a negative
price, and files a bug against the cost function.

**What would overturn it:** a target-framed write surface appearing — an atom that applies a
`stat.modify` to an entity other than its bearer. That is a **new attach point**, not a channel
direction, and it would make the frame question a real fork rather than a naming one.

Under the bearer frame, §2c's coefficient row carries the reason in its comment, and §4 gets a test
for the direction that is *not* obvious — a **raise**, not only the reduction the table tests today.

## 3. What it must NOT do

- **`long` for every magnitude, never `float`.** `plantShield`, `plantLevel` and `shootingLevel` are
  magnitudes: `long` end to end, clamped to `int` only at the Unity write boundary, exactly as
  `EntityStatWriter.cs:43-50` already does. A `float` magnitude stops being integer-exact at index 232.
- **`double` only where the field is a ratio or a timer**, matching the interval precedent
  (`EntityBaseline.cs:19-21`) — and **never in a hashed or persisted path**, which is non-deterministic
  across runtimes.
- **Widen before multiplying**, and **divide by 1000 exactly once, last.** **Overflow throws.**
- **No hard progression ceiling.** No cap on `plantShield` or the levels. The interval floor E16
  enforces is **structural** (a zero interval is a divide-by-zero or an infinite fire rate) and must
  say so in a comment; a countdown gets the same floor for the same reason and the same comment.
  `Math.Max(1L, …)` on max HP is structural too. Anything else is a soft cap in
  `data/tuning/stats.v1.json`, never a `const`.
- **No number a balance pass would change, in code.** Coefficients live in
  `data/seed/power/coefficients.v1.json`; floors and soft caps live in `data/tuning/`.
  `CoefficientTable.Authored()` is the no-database fallback, not a tuning file, and this module does
  not edit it.
- **Do not promote `Z-SLOW-FREEZE` / `Z-SLOW-COLD` / `Z-SLOW-BUTTER`** (`EntityStatWriter.cs:149-155`).
  Those floats are owned by the status runtime; a channel over them would give one slow two owners.
  Scope discipline, exactly as E16 held the line at three.
- **Do not copy `StatChannels.All`.** `AtomKindRegistry.cs:40` reads it so the two lists cannot drift;
  keep it that way.
- **Do not touch the `TakeDamage` prefix's side-wide defense cache** (G8). Per-entity primary defense
  still waits on perf **O5**; `takeDmgMultiplier` is a Unity field write, not a per-hit resolve, which
  is exactly why it is safe here and `defense` is not.

## 4. Testing strategy

| Case | Expect |
|---|---|
| `Flat +1` on `plantLevel` | `theLevel` rises; a planted removal of the writer case fails the test |
| **Planted violation:** leave one of the twelve out of `StatChannels.All` | the channel-count guard fails, and a `stat.modify` on it is refused `BadParamValue` by G6 rather than composing into nothing |
| **Planted violation:** restore the extras-path write for `Z-ARMOR-F` alongside the composed one | the extras guard test fails — two writers to one field |
| `More -100%` on `attackCountdown` | clamps at the structural floor, never 0 or negative |
| `Increased +50%` on `takeDmgMultiplier` | value **rises** — the bearer takes more damage — and the lower-is-better lint (E14b) warned |
| Cost function on a `takeDmgMultiplier` **reduction** | prices as a **benefit**, not a penalty |
| Cost function on a `takeDmgMultiplier` **raise** | matches whichever frame §2c chose, and the test names it. Under the bearer frame it prices **negative**, and a second test asserts that *"enemies take more damage"* is authored as a `status.apply` payload instead — the sign trap must be pinned in the direction that is not obvious |
| `P-SPEED 0` / `P-MOVE 0` / `Z-SPD 0` / `Z-SPD-O 0` set by an operator | still **refused**, as `EntityStatWriter.cs:117`, `:119`, `:145`, `:147` refuse them today. A promotion that starts accepting zero here freezes the entity |
| `P-ATK-ADD` negative | reaches the field unguarded, per §2b's decision, and a test pins the absence of a guard so a later promotion cannot add one silently |
| Each of the twelve | has a coefficient row; a removed row makes the atom report `unpriced`, never the generic fallback |
| `P-LEVEL 0` set by an operator | still reaches `theLevel`; asserts the `>= 0` guard, not `> 0` |
| The existing eleven channels | unchanged, goldens unmoved |
| `guard-single-writer.ps1` | passes |

**The injector is not built by CI** (`.github/workflows/ci.yml:75-103` — ten test projects, no injector
build). Compose, direction, pricing and validation all assert in `FusionRpg.Core.Tests`; the writer
half is covered by `guard-single-writer.ps1` plus the extras text guard, and confirmed live by the owner.

## 5. Acceptance criteria

1. `StatChannels.All` has 23 entries and `AtomKindRegistry.PrimaryChannels.Length` reports 23 without
   a second list existing anywhere.
2. G6 refuses an unknown channel and its message no longer names the three E16 already promoted.
3. Each of the twelve composes, writes its Unity field, and has an `EntityFinal` field.
4. `WritePlantExtras` / `WriteZombieExtras` no longer write any of the twelve; `guard-single-writer.ps1`
   is green.
5. Every promoted cheat key still works from the operator menu, now as an `Override`, and **each of the
   three guard shapes in §2b is preserved** — `P-LEVEL 0` and the other six `>= 0` keys still accept
   zero, the four `> 0` keys still refuse it, and **`P-ATK-ADD` stays unguarded** with the reason
   recorded (§2b, decided 2026-09-03) and a test pinning the absence of the guard.
6. The three `LowerIsBetter` channels price as benefits when reduced, and `takeDmgMultiplier`'s
   **raise** prices as **negative power under the bearer frame** (§2c, decided 2026-09-03), with the
   reason in the coefficient row's comment and a test on that non-obvious direction — plus a second
   test asserting *"enemies take more damage"* is authored as a `status.apply` payload.
6b. §2a and the coefficient row both state that `takeDmgMultiplier` is **not** the authoring surface
   for *"enemies take more damage"*.
7. All twelve have coefficient rows in `data/seed/power/coefficients.v1.json`; none falls through to
   the channel-less row.
8. **`Z-TAKEMULT` is confirmed live before it ships as a channel.** E16 recorded it LIVE-inconclusive;
   an owner-run lawn proof that a `takeDmgMultiplier` change alters incoming damage is a gate on this
   channel, not on the module. If the proof fails, the channel ships refused with the reason recorded
   here — never shipped silently inert.

## 6. Dependencies and cross-program hazards

| Item | Detail |
|---|---|
| **E30 `channel-pool`** | The map's stated dependency. E30 is needed to **author a pooled** atom over the new families; the compose half is independent of it, so E38's channels can land first if E30 slips — say which was done |
| **E16 `channel-extension`** | Same shape, already run. Its "never promote `Z-TAKEMULT`" boundary is superseded by map §13; its LIVE-inconclusive finding is not |
| **effect-pipeline overlap** | Map §16: E30 owns the pool contract, effect-pipeline owns the slot declaration and resolver. E38 adds channels; it must not add a resolver |
| **battle-timeline B25/B26** | B26 freezes shield and DoT behaviour while this edits the shared compose path, and the injector is not built by CI. Sequence, do not straddle |
| **E42 `units-correction`** | ✅ **CLOSED 2026-09-03.** `definitions.md` §2 now correctly states `combat.power.*`/`combat.defense.*`/`combat.shield.*` as flat game units. E38's magnitudes may be authored |
| **Stale instances** | A `catalog_revision` bump makes every rolled `effect_instance` unbindable (`StaleInstance`) |
| **Goldens** | E16 moved none. Twelve new channels with no content using them should move none either — if a golden moves, that is a finding, not a re-bless |
