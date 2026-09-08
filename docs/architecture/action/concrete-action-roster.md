# Concrete action roster — 30 seeded from the almanac

**Status: REFERENCE PROSE — owner decision 2026-08-27. Never imported.**

> **This roster is a design record, not future seed data.** It shows what an action looks like when written
> out, and it is what the seeded corpus (`A13`) is *compared against by a reader*, never by a loader. The
> shipped corpus is generated; nothing here becomes a row.
>
> **Do not build an importer for this file.** If a fixture is needed, `A5` and `A13` author their own.
 Companion to
[action-ideal.md](../action-ideal.md). These are **concrete** actions: a **fixed atom list, already
resolved** — a container with `pool_rolls = 0` and no pool rows, which
[spec-container-schema.md](../effect-atom/spec-container-schema.md) calls *"a plain fixed list. Traits,
skills, and species passives use the core alone; item templates roll the pool."*

They are the hand-authored floor the seeded ladder is measured against, not the ladder itself.

**Source corpus:** [`data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json`](../../../data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json)
— 781 entries, **574 with a description**, 617 plant / 164 zombie. Every action cites the entry it was
seeded from, verbatim.

---

## 1. How every number below was derived

**Nothing here is picked.** Four constants, all read from shipped data or code:

| Constant | Value | Source |
|---|---|---|
| `P(20)` — the calibration point | **680** | `BattleRuleset.BaseHp(20)`, `BattleModels.cs` |
| band floor / ceiling | **670‰ / 1330‰** (±33%) | `bands.v1.json` `bandFloor` / `bandCeiling` |
| tier ratio | **1.75** | `bands.v1.json` `magnitudeRatioPerMille` |
| rung = **half a tier** | `qPower(r) = 1.75^((r−1)/2)` | derived — 10 rungs over the shipped 5 tiers |

```text
mid(share, rung)  = share/1000 * P(20) * qPower(rung)
low = 670 * mid / 1000            high = 1330 * mid / 1000

cost(share, rung) = share/1000 * P(20) * 1.38^(rung-1)
cd(base, rung)    = base * 1.15^(rung-1)
```

**The cost ratio (1.38) is deliberately larger than the power ratio (`1.75^0.5` = 1.32).** Across rungs
2→10 that gives **power ×9.38, cost ×13.15 — a 1.40× escalation tax**, which is
[action-ideal.md](../action-ideal.md) §6.1's rule turned into a number. A rung-10 action is burst you pay
for, not a strictly better rung-2 — and **FOCUS is the build that pays that tax better**, because
`resource.efficiency` and `skill.cooldown.*` are exactly the two multipliers being taxed.

> **These are BASE values at `Θ = 20`.** Actor stats then scale them — `combat.amplification`, crit, the
> element matrix, `resource.efficiency` on the cost, `skill.cooldown.{category}` on the cooldown. At any
> other `Θ` every magnitude moves by `P(Θ)/P(20)`; the **cooldown does not** (§6 of the ideal: cooldown
> rides the rung alone, never `Θ`).

**Checked against the owner's own example** — *"deal 500-1000 fire damage to 1 target, cooldown 5s, cost
50 qi"*: share 830‰ at rung 2 resolves to **500–993**, and cost share 52‰ at rung 2 resolves to **49 qi**.
The band width was never chosen — **500–1000 *is* ±33% around 750**, which is `bands.v1.json` exactly.

---

## 2. ⭐ Eight showcase actions — one per complexity axis

**Added 2026-08-27.** The thirty rows in §3–§8 are all one shape — `damage + one status` — and the owner's
verdict was correct: boring. **The almanac was not the problem.** Every line below is verbatim from the
same corpus, and each one carries structure the first pass flattened away.

These eight replace their §3–§8 counterparts where they overlap. Complexity axes are
[action-ideal.md](../action-ideal.md) §8.2; **seven of the eight need no new vocabulary.**

### A — Condition · **Icicle Lance** (rung 6)

> *Icicle-shroom — "Inflicts Chill. Pierce 2 times. **Deals 4x Damage to frozen zombies.**"*

**Deal 738–1,466 ice damage to 1 target and apply Chill 6s. Against a target that is already Frozen, deal
4× instead and consume the Freeze.**

```
resource.delta{hp,−,ice}
resource.delta{hp,−,ice}   when: hasStatus(freeze, subject:target)
status.clear{freeze}
status.apply{cold}
```

This is PoE 2's *Biting Frost* — *"Consume Freeze on enemies to deal 50% more Damage"* — and the almanac
had it first. **The first pass reduced it to "applies Chill."**

### B — Sequence · **Starch Detonation** (rung 8)

> *Starch-shroom — "Every 10th attack, triggers a Potato Mine explosion that deals 300 damage **at the
> position of each zombie hit**."*

**Deal 1,922–3,815 fire damage to every enemy in the area. Three seconds later it detonates again for 40%
at every position it hit.**

`resolve_offsets_json: [0, 3000]` — **shipped**, in `ActionEnvelope.ResolveOffsets`. No new mechanism, and
it turns one action into a set-up-and-punish decision.

### C — Consumption · **Aloe Chunk** (rung 7)

> *Aloe Aqua — "Gains 1 ice charge from snowballs and tridents. Gains 15 ice charges from Ice-shroom.
> **Consumes 1 ice charge** to thrown an ice chunk, with 200 damage, Deep Chill, and 40 Cryo points."*

**Requires 1 Command stack. Consume it to deal ice damage and apply Deep Chill.**

**The charge mechanic already ships**: `command` is *"Meter — **Stacks when you apply statuses**"*. So
another action that applies a status *is* the charge generator, and `conditions_json` gates on the stack
while `status.clear` spends it. Two actions in a five-slot loadout now depend on each other.

### D — Scope split + linkage · **Bamblock Slam** (rung 9) ⛔

> *Bamblock — "On contact with a zombie, **deals damage equal to the zombies health, at the cost of its
> toughness**."*

**Deal damage equal to 60% of the target's current HP. You lose shield equal to the damage dealt.**

```
resource.delta{hp,−}          scope: primaryTarget     amount = 60% of target hp   ← LINKAGE
shield.grant{−}               scope: caster            amount = damage dealt        ← LINKAGE
```

> **⛔ This is the fixture for the one real vocabulary gap** ([action-ideal.md](../action-ideal.md) §8.5).
> `ValueSpec` reads channels and curves, never a target fact or another atom's output. **Both halves of
> this action are unauthorable today**, and the almanac asks for the same shape three separate times
> (*Bamblock*, *Doubleblast Passionfruit* — *"an additional hit equal to 10% of the zombie's max
> toughness"*, *Hearty Apple* — *"consuming energy equal to damage blocked"*).

### E — Reaction · **Solar Comet** (rung 9)

> *Comet-shroom — "**When bitten**, hypnotizes the attacker and summons a 'Solar Comet.' The Solar Comet
> seeks out zombies (**prioritizing hypnotized**) and bounces between them, up to 10 times."*

**For 8s, when you are hit: Charm the attacker 2s and launch a comet that bounces to 3 enemies for light
damage.**

```
status.apply{charm_pulse}     when: trigger OnDamageTaken     icd_key: solar-comet
resource.delta{hp,−,light}    when: trigger OnDamageTaken     icd_key: solar-comet
```

**The shared `icd_key` is the mechanism**: *"atoms sharing a key compile into one grant whose `Triggers` is
the **union** of theirs."* Both halves fire as one grant on one clock — impossible to express with a single
atom, trivial with two.

### F — Restriction · **Devour** (rung 10)

> *Chomper — "Deals 40 damage each bite to zombies immune to devouring."* · *Hypno Chomper — "**then
> digests for 10s**"*

**Deal 3× damage to 1 target. You cannot act for 10 seconds afterwards.**

`recovery_ticks: 10000` — **shipped on the envelope.** This is PoE 2's *Brutality* shape (more power, give
something up) and it is the cleanest way to make a top-rung action a *decision* rather than an upgrade:
the cost is not resource, it is **tempo**.

### G — On-kill chain · **Cherry Chomp** (rung 8)

> *Cherry Chomper — "**Creates a Cherry explosion on the tile in front after killing a zombie.**"*

**Deal damage to 1 target. If this kills it, explode its cell.**

```
resource.delta{hp,−}
resource.delta{hp,−,fire}     when: trigger OnDeath AND actorIsKiller(true)
```

Both the trigger and the leaf ship. This is *Behead*'s shape — *"Killing Blows… grant one of their
Modifiers"* — and it rewards target selection rather than raw output.

### H — Self-condition · **Cower Brace** (rung 6)

> *Sturdy-shroom — "After cowering for 10s, immediately stands up and knocks back nearby zombies…
> **While cowering, takes 50% reduced Damage.**"*

**Guard: +42% defence while held. Below 40% HP, the guard also reflects 30% of what it stops.**

```
stat.derived{combat.defense.omni, Increased}
stat.derived{combat.reflect.rate.omni, Flat}   when: hpBelowMilli(400, subject:caster)
```

The berserker shape the E3 leaf list names as its own reason for existing — *"berserker / coward shapes"*.

---

### What the eight change about the ladder

| axis | rungs it belongs to | needs new vocabulary? |
|---|---|---|
| A condition · H self-condition | 5–6 | no |
| C consumption · B sequence | 7–8 | no |
| G on-kill · E reaction | 8–9 | no |
| F restriction | 9–10 | no |
| **D linkage** | 9–10 | **yes** — §8.5 |

That is [action-ideal.md](../action-ideal.md) §8.3's structure ladder, populated: **a rung-10 action is a
different kind of thing from a rung-2, not a bigger one.**

---

## 3. `attack` — 5

| rung | action | what it does | cooldown | cost |
|---|---|---|---|---|
| 2 | **Fume Vent** | Deal **500–993** damage to 1 target and apply **Expose** 4s — it takes more damage. | 4.6s | 49 qi |
| 4 | **Icicle Lance** | Deal **738–1,466** ice damage to 1 target and apply **Chill** 6s. | 6.1s | 93 qi |
| 6 | **Sword Fan** | Deal **701–1,392** damage to each of 3 targets and apply **Bond** 8s. | 8.0s | 150 stamina |
| 8 | **Comet Chain** | Deal **969–1,924** light damage, chaining to 3 targets, and apply **Spark** 6s. | 10.6s | 337 qi |
| 10 | **Starch Detonation** | Deal **1,922–3,815** fire damage to every enemy in the area, then **Wither** 6s and **Shatter** 3s. | 14.1s | 642 qi |

<sub>ids: `atk.fume-vent` · `atk.icicle-lance` · `atk.sword-fan` · `atk.comet-chain` · `atk.starch-detonation`</sub>

| seeded from | atoms |
|---|---|
| *Fume-shroom — "Ignores handheld armor."* | `resource.delta{hp,−}` · `status.apply{expose}` |
| *Icicle-shroom — "Inflicts Chill. Pierce 2 times. Deals 4x Damage to frozen zombies."* | `resource.delta{hp,−,ice}` · `status.apply{cold}` |
| *Swordmaster Starfruit — "Each attack unleashes 5 swords that pierce infinitely…"* | `resource.delta{hp,−,eachTarget}` · `status.apply{bond}` |
| *Comet-shroom — "…bounces between them, up to 10 times."* | `resource.delta{hp,−,light}` · `status.apply{spark}` |
| *Starch-shroom — "Every 10th attack, triggers a Potato Mine explosion…"* | `resource.delta{hp,−,fire}` · `resource.delta{hp,−,statusId:wither,periodMs}` · `status.apply{shatter}` |

**Why `expose` for "ignores armour".** Absolute bypass is impossible by construction — mitigation is
`offense × K/(K + defense)`, asymptotic, *"removes a fraction, never all."* `expose` is the shipped way to
say *their armour matters less*. **`spark` is the bounce** — already `Area Square` contagion, so the Solar
Comet needs no new mechanic.

---

## 4. `defense` — 5

| rung | action | what it does | cooldown | cost |
|---|---|---|---|---|
| 2 | **Umbrella Ward** | Grant self and adjacent allies a **338–670** shield. | 6.9s | 38 poise |
| 4 | **Sawtooth Regrowth** | Grant self a **475–942** shield that refills over time. | 9.1s | 71 poise |
| 6 | **Cower Brace** | Raise defence **+42%** while held; on release apply **Rally** 5s. | 12.1s | 136 poise |
| 8 | **Chrysanthemum Sanctum** | Grant self and allies a **2,907–5,771** shield for 6s. | 16.0s | 298 poise |
| 10 | **Hearty Bulwark** | Grant self and allies a **4,296–8,528** shield that drains poise in proportion to what it absorbs. | 21.1s | 568 poise |

<sub>ids: `def.umbrella-ward` · `def.sawtooth-regrowth` · `def.cower-brace` · `def.chrysanctum` · `def.hearty-bulwark`</sub>

| seeded from | atoms |
|---|---|
| *Umbrella Leaf — "Protects plants… in a 3x3 area."* | `shield.grant{sourceClass:aura, casterAllies}` |
| *Saw-me-not — "Gains 5 Shield hitpoints every 0.5s, capped at 300."* | `shield.grant{refillOnMerge}` · `stat.derived{combat.shield.regen.omni, Flat}` |
| *Sturdy-shroom — "While cowering, takes 50% reduced Damage… knocks back nearby zombies."* | `stat.derived{combat.defense.omni, Increased}` · `status.apply{rally}` |
| *Chrysanctum — "6 seconds of immunity to all damage and crushing."* | `shield.grant{durationTicks:6000, casterAllies}` |
| *Hearty Apple — "Blocks damage… by consuming energy equal to damage blocked."* | `shield.grant{casterAllies}` · `resource.delta{poise,−,perTick}` |

**"Immunity" is a large short shield, never a flag** — an immunity flag is an absolute, and absolutes are
refused by the mitigation model.

**Hearty Bulwark is the guard economy, and PvZ already shipped it.** *"Consuming energy equal to damage
blocked"* is precisely [spec-guard-economy.md](../class-system/spec-guard-economy.md)'s absorb-drain.
Good evidence the guard design is genre-native rather than invented here.

---

## 5. `support` — 5

| rung | action | what it does | cooldown | cost |
|---|---|---|---|---|
| 2 | **Sun Tithe** | Gain **157–311** sun. | 9.2s | 28 qi |
| 4 | **Lotus Mend** | Heal self and adjacent allies for **454–900**. | 12.2s | 79 qi |
| 6 | **Odyssey Resonance** | Raise allies' damage **+33%** and apply **Rally** 8s. | 16.1s | 150 qi |
| 8 | **Digest and Restore** | Heal self **2,067–4,104**, gain sun, and apply **Command** 10s. | 21.3s | 285 qi |
| 10 | **Giftbox Bloom** | Summon a plant body with **3,957–7,855** HP to 1 cell. | 28.1s | 543 qi |

<sub>ids: `sup.sun-tithe` · `sup.lotus-mend` · `sup.odyssey-resonance` · `sup.digest-restore` · `sup.giftbox-bloom`</sub>

| seeded from | atoms |
|---|---|
| *Endoflame — "Each projectile… produces 25 Sun."* | `resource.economy{sun, add}` |
| *Snow Lotus — "Consumes 5 ice charges to heal plants in a 3x3 area for 50 HP."* | `resource.delta{hp,+,casterAllies}` |
| *Queen Endoflame — "Increases base damage by 25 for each unique Odyssey Plant on the field."* | `stat.derived{combat.amplification.omni, Increased, casterAllies}` · `status.apply{rally}` |
| *Chomp-nut — "Restores 1000 Toughness after digestion."* · *Solar Chomper — "Grants 100 Sun after digestion."* | `resource.delta{hp,+,caster}` · `resource.economy{sun,add}` · `status.apply{command}` |
| *Plant Giftbox — "When placed, produces a random plant."* | `spawn.entity{kind:plant, count:1}` · `resource.economy{sun,add}` |

> **⚠️ Giftbox Bloom is the one row that must not become random.** *"Produces a **random** plant"* is a
> pool roll, and this roster is the fixed-list half. The concrete version summons a **named** body; the
> random version is what the seeded ladder authors with `pool_rolls > 0`. Keeping them distinct is the
> entire point of having a concrete floor.

**E9 prices `spawn.entity` as `chance × count × power(body)` at depth 1, memoized** — so the summoned
body's HP carries real cost, and a spawn with `count` omitted would price at **zero**.

---

## 6. `movement` — 5

Movement produces no magnitude — *"its output is position… an action with no magnitude is priced in time
alone."* **So an earned movement action must be a move PLUS something**, or it is strictly worse than the
free basic move and would never take a slot. The rider is what it is paying for.

| rung | action | what it does | cooldown | cost |
|---|---|---|---|---|
| 2 | **Sidestep** | Reposition 1 cell and apply **Rally** 3s. | 3.4s | 28 stamina |
| 4 | **Homing Leap** | Leap to the nearest enemy and apply **Bond** 6s. | 4.6s | 54 stamina |
| 6 | **Gale Displace** | Move, dealing **406–806** air damage along the path and knocking back (**Butter** 1s). | 6.0s | 116 stamina |
| 8 | **Comet Bounce** | Bounce between up to 3 enemies for **1,098–2,180** light damage each, applying **Spark** 4s. | 8.0s | 220 stamina |
| 10 | **Bamboom Dive** | Dive forward for **3,279–6,508** earth damage to everything on the path, with **Butter** 1s and **Shatter** 3s. | 10.6s | 469 stamina |

<sub>ids: `mov.sidestep` · `mov.homing-leap` · `mov.gale-displace` · `mov.comet-bounce` · `mov.bamboom-dive`</sub>

| seeded from | atoms |
|---|---|
| *Spikeweed — "Does not block regular zombies."* | `status.apply{rally}` |
| *Cattail — "Shoots thorns that home in zombies closest to your house."* | `status.apply{bond}` |
| *Blover — "Blows away all Balloon Zombies on screen. Pushes back Zomppelins."* | `resource.delta{hp,−,air}` · `status.apply{butter}` |
| *Comet-shroom — "bounces between them, up to 10 times."* | `resource.delta{hp,−,light}` · `status.apply{spark}` |
| *Bamboom — "flies forward in its lane, knocking back zombies and causing 300 area damage on each impact."* | `resource.delta{hp,−,earth,eachTarget}` · `status.apply{butter}` · `status.apply{shatter}` |

**`butter` is the knockback.** There is no displacement atom, and inventing one would be a thirteenth kind
in a closed vocabulary of twelve. Denying an actor its next action is the mechanical half of a knockback,
and it is what the engine can express today.

**All five keep `slot_consuming = false`** — *"if movement took a slot, at `W = 1` only one actor on the
board could ever move."*

---

## 7. `status` — 5

| rung | action | what it does | cooldown | cost |
|---|---|---|---|---|
| 2 | **Chill Touch** | Apply **Chill** 6s to 1 target. | 8.0s | 32 qi |
| 4 | **Deep Freeze** | Deal **211–419** ice damage to all enemies, **Freeze** 4s and **Chill** 10s. | 10.6s | 82 qi |
| 6 | **Hypnotic Spore** | Apply **Charm** 3s to 1 target. | 14.1s | 157 qi |
| 8 | **Blight Bloom** | Apply **Wither** to 1 target for **775–1,539** over 6s, spreading to neighbours. | 18.6s | 298 qi |
| 10 | **Catalyst Conversion** | Deal **3,505–6,957** dark damage to 1 target and **Hypno** 8s — it fights for you. | 24.6s | 617 qi |

<sub>ids: `sta.chill-touch` · `sta.deep-freeze` · `sta.hypnotic-spore` · `sta.blight-bloom` · `sta.catalyst-conversion`</sub>

| seeded from | atoms |
|---|---|
| *Icicle-shroom — "Inflicts Chill."* | `status.apply{cold}` |
| *Ice-shroom — "Freezes all zombies for 4s, Chills all zombies for 10s, and deals 20 damage."* | `status.apply{freeze}` · `status.apply{cold}` · `resource.delta{hp,−,ice}` |
| *Hypno-nut — "4% chance on each bite to hypnotize the zombie."* | `status.apply{charm_pulse}` |
| *(overlay DoT + lane contagion)* | `resource.delta{hp,−,statusId:wither,periodMs,spread}` · `status.apply{blight}` |
| *Catalyst-shroom — "turns the zombie into a Hypnotized Explod-o-shooter Zombie and disappears."* | `status.apply{hypno}` · `spawn.entity{kind:zombie, count:1}` |

> **⛔ Deep Freeze and Catalyst Conversion are the roster's control-ceiling test.** Both are hard control,
> and [action-ideal.md](../action-ideal.md) §5.1 requires control duration authored in **victim turns**,
> not seconds. The seconds above are the *almanac's* numbers, kept so the seed stays traceable — **they
> are not the authored values.** Converting them is the first job of the duration resolver, and **Deep
> Freeze is the fixture that proves it**: 4s Freeze plus 10s Chill against a ~1s turn is a **14-turn
> lock**, which is exactly the permanent-lock failure the bound exists to stop.

---

## 8. Innate — 5, one per demon-type archetype

Free, outside the 5 equipped, and per [action-ideal.md](../action-ideal.md) §1.3 the innate **climbs** with
earn history rather than sitting at a fixed rung. Shown here at rung 2.

| element | action | what it does | cooldown | cost |
|---|---|---|---|---|
| `fire` | **Ember Brand** | Deal **241–479** fire damage to 1 target and apply **Ember** 4s. | 5.8s | 38 qi |
| `ice` | **Frostbind** | Deal **229–455** ice damage to 1 target and apply **Chill** 5s. | 5.8s | 38 qi |
| `air` | **Galewrack** | Deal **217–431** air damage to 1 target and knock back (**Butter** 1s). | 5.8s | 38 qi |
| `earth` | **Stoneward** | Grant self a **265–526** earth shield that counters the next hit. | 5.8s | 38 qi |
| `dark` | **Hypno Seed** | Summon a hypnotized body with **301–598** HP that fights for 6s. | 5.8s | 38 qi |

<sub>ids: `inn.ember-brand` · `inn.frostbind` · `inn.galewrack` · `inn.stoneward` · `inn.hypno-seed`</sub>

Seeded from *Endoflame*, *Snow Lotus*, *Blover*, *Bamblock*, and *Hypno Pea* — the last verbatim:
*"Summons a Hypnotized Hypno Pea Zombie upon killing a zombie… 270HP, fires a 20 damage Hypno Pea every
1.5s."*

**`light` is deliberately unassigned.** Five archetypes against six elements leaves one open, and an
element with no innate is a better state than a sixth invented to fill a table.

---

## 9. The vocabulary these are built from

### 9.1 Twelve atom kinds, and there is no damage kind

Verified in `AtomKindRegistry.cs`:

| Attach | Kinds |
|---|---|
| Stat | `stat.modify` · `stat.derived` |
| Resource | `resource.delta` · `resource.economy` |
| Status | `status.apply` · `status.clear` |
| Shield | `shield.grant` |
| Board | `spawn.entity` · `board.action` · `grid.spawn` · `grid.clear` · `box.set` |

> **`resource.delta` on `hp` IS damage.** The registry says so itself: *"FA10, hp add-only. The only
> opcode battle consumes. **Dealing damage is this plus a trigger — there is no separate damage attach
> point.**"*

Two param traps, both re-derived from executors rather than docs:

- **`status.apply` takes `status`, not `statusId`, and `duration` is in SECONDS as a float** — *"FA2
  predates the integer-ms rule and was not changed for it."* Everything else in this repo is integer ms.
- **The DoT payload lives on `resource.delta`** — `statusId` / `periodMs` / `durationMs` / `tickBudget` /
  `spread` are FA10 keys. A DoT authored on `status.apply` compiles to an opcode that never carries it.

**Derived ops are `Flat | Increased | Replace | Flag`** — *"there is no More on the derived side."*

### 9.2 Closed id sets

**Statuses** — Unity CC: `butter` `freeze` `cold` `poison` `hypno` `ember` `jala` `kelp` · overlay:
`wither` `bond` `rally` `leech` `expose` `command` `shatter` `charm_pulse` · contagion: `blight` `rot`
`spark` `pact_mark`.

**Elements** — `fire` `ice` `air` `earth` `light` `dark`. **Base is omni**; an element on the basic attack
is a stat and trait change on the actor, never a different row.

**Costs** — ⚠️ **corrected 2026-08-30: all six resources are legal action costs.** `stamina` physical ·
`qi` channelled · `poise` guard · `hp` sacrifice (floors at 1 unless the action opts into lethality) ·
`hunger` metabolic (**Sun** on the plant side) · `spirit` essence. The previous line — *"`hp`, `hunger`
and `spirit` are never action costs"* — was a design defect: it made HP-sacrifice and sun-priced plant
actions unbuildable and left `spirit` with no sink at all. See `resource-hub-ssot.md` §"pays for".

---

## 10. ⛔ Honesty: most of this cannot run in battle today

The runtime support matrix is per-kind and per-runtime, and it is **not** uniform:

| kind | Injector | **Battle** | Sim |
|---|---|---|---|
| `stat.derived` | None | **Full** | None |
| `resource.delta` | Full | **None** | PlanOnly |
| `resource.economy` | Full | **None** | PlanOnly |
| `status.apply` | Full | **Partial** | PlanOnly |
| `status.clear` | Full | None | PlanOnly |
| `shield.grant` | Full | **None** | None |

**So every damaging row above is a no-op in battle mode right now.** Publishing this roster without saying
so would create exactly the defect the class system's reconcile sweep found nine of — *declared,
registered, documented and inert, each with a green test beside it.*

### 10.1 …and the action program is what fixes it

The registry's own comments name the missing piece:

> `resource.delta`, D6: *"Battle's sink **does** handle FA10, but no ATOM can reach it — `BattleEngine`
> never grants and never calls `OnEvent`, so a bound `resource.delta` is a silent no-op. **Full again
> when battle grows a grant path.**"*
>
> `shield.grant`, D6: *"…the grant skips with `shield-runtime-missing`. Sim is one line of wiring away;
> **battle also needs a grant path.**"*

And [spec-action-model.md](spec-action-model.md) §4 already resolved that an action's atoms **do not go
through the effect list**: *"an action applies its own atoms directly at its resolve tick."* **That direct
application is the grant path both comments are waiting for.**

> **`A5` is the acceptance test for that claim, not this roster** (owner: reference prose only). If an
> action's atoms resolve in battle, `resource.delta` and `shield.grant` go Full there, and two of the nine
> inert registrations close as a side effect. The shapes below are what such a test would exercise.

---

## 11. What the shipped corpus needs — which this file does not

**This roster is never imported** (see the status banner), so nothing below is owed *by it*. The list is
kept because it is the same list `A13`'s generated corpus needs, and it was easier to see written against
concrete rows than against a generator.

| | Owed | Blocked on |
|---|---|---|
| 1 | The `sharePermille` per channel behind each number — these were derived from *relative* shares, still owed a calibration pass | An authored tier-bands artifact ([spec-numerics.md](../seedsmith/spec-numerics.md) §2: *"must reject at import, not guess one"*) |
| 2 | Control durations converted from **seconds → victim turns** | The per-mode duration resolver ([action-ideal.md](../action-ideal.md) §5.3) |
| 3 | `resource.max.qi` / `.stamina` / `.poise` at `Θ=20` — costs above assume the pool anchors at `P(20)` | `resource.max` is one of the families that carried `unitClass: null` |
| 4 | The `rpg_action` / `rpg_action_cost` schema | A1, unbuilt |
| 5 | An E9 monotonicity pass over all 30 | `PowerVector` pricing wired to actions |
| 6 | ~~`board.action`'s `op` vocabulary~~ **closed 2026-08-27** | Ops are a closed four — `freeze` \| `doom` \| `fireline` \| `cherry` — and the kind is **Injector-Full, Battle-None**. Lawn-only, so no battle action may use it |

**Rows 3 and 6 are declared gaps, not oversights.** The cost column assumes a base `qi` pool of ~680 at
`Θ=20`, which makes Starch Detonation (642 qi) very nearly a full-pool cast — strong but usable, and
gated behind qi investment, which is the intended shape. If the real pool differs, **every cost moves
together** and the 1.40× tax is unaffected, because it is a ratio.

---

## 12. Related

- [action-ideal.md](../action-ideal.md) — the re-design record; §4 the rung ladder, §5 duration, §6 the tax
- [effect-atom/spec-container-schema.md](../effect-atom/spec-container-schema.md) — fixed core vs rolled pool
- [effect-atom/spec-power-vector.md](../effect-atom/spec-power-vector.md) — E9, and the spawn depth-1 rule
- [status-ssot.md](../status-ssot.md) — the status ids and their stacking families
- [seedsmith/spec-numerics.md](../seedsmith/spec-numerics.md) §1–2 — the band model and the refuse-to-guess rule
- [`pvz-fusion-almanac-3.6.1.json`](../../../data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json) — the corpus
