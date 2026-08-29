# Action system — the ideal (re-design)

**Status: ✅ SEALED 2026-08-27 by the owner.** 26 decisions, 0 open questions, 6 retractions. §0.1 is
**binding — do not reopen**; a later session that disagrees with a row there has found either a real
architectural change (say so explicitly and expect a decision) or its own misunderstanding.

This is a **design record**, not authority over shipped code: where it disagrees with a shipped mechanism
the code wins, and §9 is the list of places the older module specs disagree with *this*.

**One dependency carries into spec phase** — the atom-engine linkage extension (§8.5, §0.2). It is build
work, not an open question.

> **⛔ The ten module specs in [action/](action/) predate this re-design and are stale in the places
> §9 lists.** They were written 2026-08-22 against a five-resource model, an 84-channel catalog and a
> different reading of guard. Read this document first; §8 is the reconciliation list.

**Reads (all in the session that wrote this):**
[battle-turn-ideal.md](battle-turn-ideal.md) §2–4 (the FSM, `W`, the virtual clock) ·
[power/ssot-power-scale.md](power/ssot-power-scale.md) §4 (the ladder), §10 (the closed inventory),
§11 (the caps register) · [resource-hub-ssot.md](resource-hub-ssot.md) ·
[effect-atom/spec-container-schema.md](effect-atom/spec-container-schema.md) **(built)** ·
[effect-atom/spec-power-vector.md](effect-atom/spec-power-vector.md) (E9) ·
[seedsmith-map.md](seedsmith-map.md) · [seedsmith/spec-numerics.md](seedsmith/spec-numerics.md) ·
[class-system/spec-guard-economy.md](class-system/spec-guard-economy.md) ·
[class-system-ideal.md](class-system-ideal.md) §5b, §8.1 · [status-ssot.md](status-ssot.md) ·
[action/spec-action-model.md](action/spec-action-model.md) ·
[action/spec-defence-actions.md](action/spec-defence-actions.md) ·
[action/spec-movement-actions.md](action/spec-movement-actions.md) ·
[design/spec-action-layer.md](../design/spec-action-layer.md) ·
`DerivedStatChannels.cs` · `BattleEngine.cs` · `data/tuning/battle.v1.json`.

---

## 0. State of the design

### 0.1 Decided — do not reopen

| | Decision | Where |
|---|---|---|
| **1** | **Three kinds of action.** Basic (3, free) · innate (1, free, per demon type) · earned skills (**5 equipped slots**) | §1 |
| **2** | **Basic actions cost no loadout capacity** — they are *intrinsic*, so there is nothing for a cap to count | §1.2 |
| **3** | **Guard is a STANCE, not a reaction.** Continuous while enabled; every other action, movement included, is refused while it holds | §2.2 |
| **4** | **Actions are seeded, never handcrafted.** An action is an `effect_container` with a weighted pool — shipped machinery, not new | §3.0 |
| **5** | **One counter drives the unlock ladder: earn history.** Monotonic, never decrements, never resets | §3.1 |
| **6** | **Discard, never reroll.** Discard is available, flat-taxed, uncapped — and it does **not** rewind the chance | §3.3 |
| **7** | **Rung cap defaults to 10**, tunable. Held actions ≤ cap; accepting one more forces a discard | §3.2 |
| **8** | **Only the levelling faucet is capped.** Actions from items, passives, variants and future mechanisms are **uncapped, because they were paid for** | §3.4 |
| **9** | **Two axes, multiplied once.** `Θ` makes everything bigger; the rung makes *this* action better than your last | §4.2 |
| **10** | **Duration rides the ladder**, with a bound that is **relative, never absolute** | §5 |
| **11** | **Cost rides rung and `Θ`; cooldown rides rung only.** Cooldown is ticks, not a magnitude | §6 |
| **12** | **Cost span > power span.** This is the mechanism that makes FOCUS a build rather than flavour | §6.1 |
| **13** | **A demon type is a weight vector over the five shipped action-categories** — not a third vocabulary | §7.2 |
| **14** | **PvZ samples the frame clock into integer ticks.** Real time drives; wall-clock is never stored | §5.3 |
| **15** | **Complexity = referencing state you did not create**, never atom count | §8.1 |
| **16** | **A rung buys STRUCTURE as well as numbers** — a rung-10 action plays differently, not just harder | §8.3 |
| **17** | **Seedsmith rolls the target spec**, not only the atom list | §8.4 |
| **18** | **Predicates ARE priced**, by a tunable apply-frequency table — a conditional atom costs less than the same atom unconditional | §8.6 |
| **19** | **A predicate's price is a CHAIN** — reachability × susceptibility × coincidence × uptime; only reachability is buildable | §8.6 |
| **24** | **The discard tax is paid in `soul`** (owner, 2026-08-27) — summoner-scoped currency for a summoner-scoped decision | §3.3 |
| **25** | **Action kinds close at three** — basic · innate · earned skill. Owner, 2026-08-27 | §1 |
| **26** | **PvZ cadence and cost span are DEFERRED MEASUREMENTS, not open design** — both wait on a built system, neither blocks a spec | §5.3 · §6.1 |
| **23** | **Restriction is a self-debuff** — `status.apply` scoped `caster`; axis I needed nothing new | §8.7 |
| **22** | **The basic attack costs `stamina`** (owner, 2026-08-27). *"stamina is free"* is a **resource-economy tuning defect, not an action-design one** — diagnosed at `recovery.scaleMilli`, owned by the class system | §2.1 |
| **21** | **The discount is FLOORED at 2.5×** (`predicateDiscountFloorMilli = 400`, band 400–500). The chain measures the average case; the price must hold against the best case | §8.6 |
| **20** | **A generated pool must offer the enabler for every conditional payoff it offers** — pricing cannot fix a combo the generator never assembles | §7.3 |

### 0.2 Open — none. This ideal is ready to seal

**Every question raised in the re-design session is closed**, and the two that are not *decisions* are
**deferred measurements** rather than open design (§26): the PvZ cadence reference waits on a real lawn,
and the cost span waits on the simulator. Neither blocks a spec.

**One item carries into the spec phase**, by owner instruction 2026-08-27:

| | Work | Owner |
|---|---|---|
| **Linkage** (§8.5) | Extend the atom effect engine so a magnitude can read a field the event already carries (`ev.Damage`) — GAS's `SetByCaller` shape, then optionally its `AttributeBased` shape | The **atom program**, in **spec phase**, *after* this ideal is sealed |

> *"That means we need to extend our atom effect engine, and we should do that after we seal the ideal here
> — do it in spec phase."* It is a build dependency, not an unanswered design question, so it does not hold
> the seal.

### 0.3 Retracted this session — do not act on these

Recorded rather than deleted, because each was argued from evidence and the correction is the useful
part.

| Retracted | Why it was wrong |
|---|---|
| *"Duration must not ride the ladder"* | Owner: it should, with a tunable cap. The item corpus's 1.4-vs-1.75 warning is about **span**, not about participation |
| **An absolute duration bound of 50,000 ticks**, derived from `maxRounds × roundDurationMs` | `MaxRounds` has exactly one reader — `BattleEngine.cs:306` — and the injector never references `BattleRuleset`. It is a **battle-mode loop guard**, and deriving a universal bound from it pushes a web-engine constant into the lawn |
| **A rung-scaled discard tax**, to close a top-end reroll farm | The farm does not exist: chance keys on **earn history**, which never rewinds. The scaled tax priced against a closed problem and would have double-charged the same behaviour. A **flat** tax is correct |
| **"Do uncapped sources share the levelling pool, or a separate one?"** — asked as an open question | **A malformed question, not a decision.** Owner, 2026-08-27: *"it won't use 10 slots — how do you bind an uncapped set to a cap of 10? Wrong question."* The 10 is the **levelling unlock count**; paid sources are uncapped **by definition**, so they were never candidates to share a cap. Offering "shared or separate" implied a shared reading that cannot exist |
| **"The slot remembers its rung"** | It freezes an unlucky early roll permanently — a progression ceiling wearing a different hat, which PS-8 refuses. Rung keys on earn history, so everything eventually climbs |

---

## 1. Three kinds of action

| Kind | Count | Costs loadout capacity? | Where it comes from |
|---|---|---|---|
| **Basic** — attack · guard · move | 3 | **no** | intrinsic on every species row |
| **Innate** | 1 | **no** — a free sixth | the actor's demon type |
| **Earned skill** | **5 equipped** | yes — this is the scarcity | the unlock ladder (§3) and any paid source (§3.4) |

### 1.1 ⚠️ "Slot" is already taken, and it means something else

[battle-turn-ideal.md](battle-turn-ideal.md) §2 knob 2: *"concurrency width `W` — how many actors may
hold an **action slot** simultaneously"*, and `rpg_action.slot_consuming` is that flag. That is a
**scheduling** concept.

> **The equipped-skill count is `loadout capacity`. `slot` stays the kernel's.**

Two meanings of one word inside one subsystem is how `block`/`guard` and `primary`/`aptitude` each cost
this repo a rename. This one is caught before it ships.

### 1.2 Why the basic three cost nothing — by construction, not by exemption

[spec-action-model.md](action/spec-action-model.md) §5 already splits the sources:

| Source | Rule |
|---|---|
| **Intrinsic** | `species.action_ids`. *"A default must never depend on authored data"* |
| **Granted** | `rpg_actor_action(owner_kind, owner_key, action_id, source)` |

**Loadout capacity caps the granted set only.** Intrinsic actions are never bound, so there is nothing
for a cap to count. That is stronger than a rule saying "basic actions are free", because there is no
rule to forget.

Two additions keep it true:

- A species row omitting any of the three is **rejected at load, naming the species**.
- A granted action colliding with a basic `action_id` is **rejected**, never double-counted.

### 1.3 The innate action climbs

**Recommended, not yet ratified.** The innate action's rung tracks earn history exactly as an earned
action's would — same `rung(n)`, no second curve.

The alternative is a fixed rung 1, which turns the type signature into dead weight the moment the
ladder passes it. **An action that becomes dead weight is worse than one that never existed**, and it
still occupies the sixth slot. A lagging climb (`rung − 3`) is rejected: a third curve for a small gain
is the private-`f(x)` defect the power SSOT exists to end.

---

## 2. The three basic actions

| | attack | guard | move |
|---|---|---|---|
| `tags_json` | `offensive` | `defensive` | `movement` |
| `slot_consuming` (`W`) | true | **false while held** — §2.2 | **false** |
| pool cost | **open** — §2.1 | `poise`: flat commit **+** absorb drain **+** per-tick hold | time only; `stamina` optional for a dash |
| `time_cost_ticks` | non-zero | commit only | non-zero — *the time cost is the economy* |
| cooldown | `skill.cooldown.attack` | `skill.cooldown.defense` | typically none |
| range | `min_range` / `max_range` / `range_channel` | self | `range_channel = move.range` |

**The attack needs no new machinery to be type-flavoured.** A5's basic-attack adoption already takes
element components from `attacker.AttackComponents`, so *"becomes element damage"* is a **stat and
trait change on the actor**, not a different action row. One row; the demon type and its passives
change what it does. Range parameters are authored now and inert until the board exists — and with no
board **every range check passes**, which is the line the byte-identity proof rests on.

**Movement is already specced correctly** in [spec-movement-actions.md](action/spec-movement-actions.md)
and needs nothing from this re-design. Its two load-bearing points survive: `slot_consuming = false`
(else at `W = 1` only one actor on the board could ever move), and `move.range` is *how far* while
`turn.speed` is *how often* — a single stat cannot express both.

### 2.1 The basic attack costs `stamina` — and "stamina is free" is not this layer's bug

**Owner, 2026-08-27:** *"that means the tuning table has a problem in resource economy, not the action
itself."* Correct, and it retracts an earlier framing in this document that offered three action-design
options (charge stamina / charge nothing / charge a token amount) for what is a **single wrong coefficient
one layer down**.

The measurement, from [class-system-ideal.md](class-system-ideal.md) §8.1b:

```text
strike   cost 1,544 stamina/round   vs   regen 3,784/round   ->  NEVER runs dry
```

#### The address, and why the number is wrong

[`tools/CombatSim/tuning/aptitudes.v1.json`](../../tools/CombatSim/tuning/aptitudes.v1.json):

```json
"recovery": {
  "scaleMilli": 374,
  "families": ["resource.regen", "combat.shield.regen"],
  "_note": "Recovery is sized against PEER DAMAGE, never against the pool it refills"
}
```

**One dial multiplies every regen family, and `374` was solved against the wrong opponent for stamina.**
It was derived for the **termination invariant** — `r = recovery / peerDamage`, measured at 1.33 (an
unkillable pair) and solved to 0.670 over three measured passes. That is the correct opposition for
`resource.regen.hp`, which incoming damage genuinely opposes.

> **`resource.regen.stamina` is opposed by ACTION COSTS, not by peer damage.** It is swept up by the same
> prefix match and inherits a coefficient solved against a force that does not act on it. **Stamina regen
> was never sized against anything.**

That is this repo's recurring rule turned on the *dial* rather than the coefficient — *a number is only
meaningful relative to the thing that opposes it* — and the dial's own `_note` states the rule while
applying it to the wrong pool.

#### Whose fix it is

**Not this program's.** [class-system-map.md](class-system-map.md) already schedules it as `residual-fit`'s
**second fixed step**: *"make `stamina` bind — it is free today and is the top reservation for 9 of 12
aptitudes."* And §8.1d says why it outranks every per-aptitude adjustment: *"the single largest reservation
is not per-aptitude at all… **fixing that one number does more for the distribution than any per-aptitude
adjustment could.**"*

**The likely shape of the fix, for whoever takes it:** the dial goes **per-family**, because `hp` regen and
action-pool regen are sized against different opponents. Solving one number against both is what produced
the defect.

#### What this program owes instead

One rule, and it is an authoring rule rather than a number:

> **An action cost is authored against the pool's REGEN, never against its MAX.** Sized against the pool a
> cost looks meaningful and is not.

That holds whatever `recovery.scaleMilli` becomes, which is exactly why it belongs here and the coefficient
does not.

### 2.2 Guard is a stance, and that unblocks it

[spec-defence-actions.md](action/spec-defence-actions.md) §1 splits defence into **stance** (own turn,
ordinary `W` slot) and **reaction** (someone else's action, a separate `WReact` pool), and filed *guard*
under reaction. **Guard as designed here is a stance.** That matters for scheduling, not taste:

> A8's own words: *"A stance is an ordinary action and needs nothing new; the whole reason A8 waits on
> B6 is the second row."*

**So guard is not blocked on timeline B6** and can ship with the basic attack. The reaction lane stays a
separate, later feature.

Three properties, and two of them are findings rather than choices:

**It needs no new FSM state.** The shipped machine is `Charging → Ready → Committed → Resolving →
Recovering`. Guard is a **self-granted status** plus the usability layer's condition gate: every other
action carries a refusal while the stance holds. Same claim movement makes — *"if this module grows a
runtime of its own, something is wrong."*

**It must NOT hold a `W` slot while enabled.** A8 scoped a stance to *"until your next turn"*; this one
is indefinite, and at `W = 1` an indefinite hold **freezes the board** — the exact failure the movement
spec names for itself. Guard consumes a slot to *raise*, then releases it. **The status persists, not
the slot.**

**It needs a per-tick hold cost, or it trips the HARD criterion.** Two actors both guarding forever deal
and take nothing — `netAttrition ≤ 0` on both sides, which is the **termination invariant**, and
`decisions.md` makes that blocking: *"no later layer can repair a pool that refills faster than it
drains."* `when = perTick` already exists and *"failing to pay ends the action through the interrupt
path"* is shipped semantics, so a mutual guard resolves **arithmetically**, with no special case.

That is a third cost component beyond [spec-guard-economy.md](class-system/spec-guard-economy.md)'s
flat-commit-plus-absorb-drain, which was decided for guard-as-a-proc. **A stance needs the third.**

---

## 3. The unlock ladder

### 3.0 The generator already exists

[spec-container-schema.md](effect-atom/spec-container-schema.md) — **built**:

> *"A container is a named, ordered bundle of atom references, optionally with a **weighted pool it
> rolls from**."* `pool_rolls` = how many atoms to draw · `min_tier`/`max_tier` = the tier window ·
> `group` = PoE's mod-family rule, so one action never rolls `+10 atk / +12 atk / +14 atk`.
> **"Rarity (on the container) selects the `pool_rolls` count and the `min_tier`/`max_tier` window…
> No third mechanism."**

**An unlocked action is a container roll, and the rung is its rarity.** Nothing to build.

### 3.1 One counter

```text
earnCount        monotonic. Never decrements, never resets.
chance(n)        = max(floor, p1 * delta^(n-1))
rung(n)          = min(earnCount, cap)
holding          <= cap; accepting one more requires a discard
```

**Your chance only ever falls, and your rung only ever rises.** Neither can be gamed, because discard
moves neither.

Starting values — all four are tuning rows:

| `p1` | `delta` | `floor` | `cap` |
|---|---|---|---|
| 50% | 0.88 | 0.1% | **10** |

| earn | 1 | 10 | **11** | 20 | 25 | 40 | 50 |
|---|---|---|---|---|---|---|---|
| chance | 50% | 15.8% | **13.9%** | 4.4% | 2.3% | 0.34% | 0.1% ← floor |

> **A single geometric cannot independently pin "still meaningful at 40" and "floored at 50."**
> Tightening one loosens the other. `0.88` is the closest single value; pinning both needs two segments.

### 3.2 Why the cap is 10

At **cap 25** the first forced discard is earn #26, where chance is ~2% — a decision made twice a year.
At **cap 10** it is earn #11, where chance is still **~14%**. The keep-or-discard tension becomes part of
normal levelling instead of an endgame footnote. That is the whole reason for the smaller number.

It also settles a question the larger cap raised: at 10 held against 5 equipped, the pool is a **bench**,
not a warehouse, and slots 6–10 need no separate justification.

**The cap is the rung table's row count** (§4.1), so changing it is deleting rows and re-authoring the
survivors to span the same range — steeper per step, which is the point.

### 3.3 Discard

A discard is structurally a respec of one unlock, so it reuses `RespecPolicy`'s shape rather than
authoring a second pricing mechanism: ***always available, always priced, never on a cooldown, never
capped.*** Flat tax — see §0.3 for why a scaled one was retracted.

**Discard is not a reroll**, in mechanism rather than in wording: it frees a slot and costs a payment,
and the chance ratchet means the next attempt is strictly more expensive than the one before it.

**Three compounding brakes, two of which already ship:**

1. **The chance ratchet** — never rewinds. New, and it does the real work.
2. **The discard tax** — flat, always available.
3. **The levelup cadence** — `XpToNext(L) = first + (L−1)·step`, arithmetic and rising. Power SSOT
   §10.1 row 6 keeps it explicitly as *"the **cost** ladder, not a power ladder."* Needs nothing.

None is a wall. Each is a price — §11.1a's *"a cap is a cliff; the continuous instrument is the real
control"* holding across all three.

### 3.4 Only the free faucet is capped

**Owner, 2026-08-27:** actions from passive skills, variants, items and future mechanisms have **no cap,
because they already pay.**

That is [power/ssot-power-scale.md](power/ssot-power-scale.md) §11.1a verbatim, on removing `MaxSlots`:

> *"The hard cap was redundant. Scarcity came from the **escalating price**, not from the ceiling."*

So the uncapped sources need no defence. The **cap** needs one, and it has three, in order of strength:

1. **It caps a count, not a magnitude.** PS-8 governs magnitudes; a limit on how many things one faucet
   grants is structural, like `pool_rolls <= distinct drawable groups`.
2. **The total action pool is uncapped**, because paid sources are. Same shape as the register's
   world-size row: *"world size stops; world count does not, and that is the axis."*
3. **Power per fight is bounded by the 5 equipped slots regardless of pool size.** This is what makes
   uncapped paid sources genuinely safe rather than merely permitted — **an uncapped pool grows the
   choice, never the power.**

### 3.5 One rung table, many faucets

Every source grants actions, so every source needs a rung — and that must not become three private
curves.

| Source | Rung from | Capped? |
|---|---|---|
| Demon-type levelling | `min(earnCount, cap)` | **cap**, tunable |
| Item grant | the item's rarity / tier ladder | no |
| Passive skill, variant | that system's own tier | no |
| Future mechanisms | **declare a mapping at registration** | no |

**One ladder, many readers, no private `f(source)`.** A new mechanism that wants to grant actions
declares its mapping; it does not invent a rung scale.

### 3.6 What the endgame looks like

Earns 1…cap fill the pool at rungs 1…cap. Every earn after that arrives at the **top rung** and forces a
discard — so the pool converges upward and **the floor rises rather than the ceiling**. At the 0.1% tail
against a rising XP cost, that is on the order of a thousand levels per upgrade: endless grind behaving
exactly as the SSOT asks, always advancing and never finished.

Discarding a low rung *early* is profitable — dump a rung-3 at earn 10 and the refill is rung 10. That is
the intended upgrade loop, not an exploit, and it is self-limiting because every attempt burns an earn
the chance never returns.

**Consequence worth confirming:** once the best five are all top-rung, further earns only improve
*atoms*. Past roughly earn `cap + 5` the ladder stops being about rungs and becomes about which atoms
rolled inside a top-rung container. The atom pool weights carry the long tail.

---

## 4. Atom power scaling — two axes, multiplied once

### 4.1 The ladder needs no new power scale

The power SSOT's §10 inventory is closed: *"a power-shaped number that is not in this table does not
have permission to exist."* One row already covers this shape — **row 7**:

> Affix tier ladder `m_t = m1 × 1.75^(t−1)`, geometric, 5 rungs. *"Bounded at t5 (9.4× total). A
> **within-item quality ladder in relative space** — it never sees a level. §2's theorem does not
> apply."*

The unlock ladder is the same shape one level up: a **within-demon-type quality ladder in relative
space**. So it is built from two shipped mechanisms — the `1.75^(t−1)` magnitude ladder and `pool_rolls`
breadth — rather than a third.

**The ladder is an authored table, not a formula:**

| rung | `min_tier` | `max_tier` | `pool_rolls` | `costMulti‰` | `cdMulti‰` |
|---|---|---|---|---|---|
| 1 | 1 | 1 | 1 | 1000 | 1000 |
| … | | | | | |
| cap | 5 | 5 | 5 | … | … |

Authored, because the ordering of `(tier, rolls)` pairs is a balance decision. **Machine-checked**,
because it must be monotonic: a test prices every rung through E9's `PowerVector` and fails if rung
*u+1* is not worth more than rung *u*. A designer picks the sequence; arithmetic proves it climbs.

### 4.2 The two axes

```text
value(rung, Theta)  =  anchor(Theta)  x  q(rung)
                       ------------      -------
                       the ONE Theta     relative rung ladder
                       read; P(Theta),   bounded, level-free
                       grows forever

anchor(Theta) = sharePermille * P(Theta) / 1000
```

Same pin the item corpus uses (`m1 = share × B_family(20)`). **`Θ` makes everything bigger; the rung
makes this action better than your last one.** They multiply once and never again.

> **PS-4 applies directly: the rung ladder must NEVER be multiplied by `contentScale`.** The anchor
> already did it. This is the mistake that rule exists to catch, and a rung ladder is a bigger blast
> radius than an affix one.

### 4.3 Two traps

**Multiplicative pairs will underprice, and random generation guarantees you hit them.** E9 documents
itself as knowingly ~12.5% wrong on crit-rate × crit-damage and on the element ring. Across a pooled
ladder that combination *will* roll. The rung budget check needs either E10's marginal read or a `group`
exclusion so one container cannot roll both halves.

**Demon-type level is not a `Θ` axis.** `Θ_actor` has exactly five axes (power SSOT §5) and this is not
one. The unlock ladder stays **local** — row 7's precedent — or it is a proposal for a sixth axis, which
is a reviewed change to that document.

---

## 5. Duration

**Duration rides the ladder** (owner, 2026-08-27), with a bound that is **relative, never absolute**.

### 5.1 The reference is the victim, not the fight

An earlier draft anchored duration to fight length, which exists in one mode only (§0.3). The anchor
that exists in every mode is the **victim's own action cadence**.

> **Control duration is authored in victim turns and resolved to ticks at apply time.**

"Stun for 2 of your turns" is meaningful in a 12-round battle, in a 40-minute lawn run, at `Θ`=10 and at
`Θ`=5,000, without a single tick constant.

| | |
|---|---|
| **Mode-free** | no fight-length reference, so nothing leaks between the battle engine and the lawn |
| **`Θ`-free by construction** | both sides scale, so it is contest-shaped and PS-3 is satisfied without trying |
| **Still rides the rung** | a top-rung control steals more turns than rung 1 |
| **Safe without an absolute bound** | *"you lose at most N of your actions"* is a **bounded ratio** — PS-8 exempt, **and must say so in a comment** |

Only control needs the relative form, because only control removes agency:

| family | bound | expressed in |
|---|---|---|
| **control** — stun, freeze, root | small, tunable | **victim turns** |
| **DoT / debuff** | none needed — it kills or expires | ticks |
| **buff / stance** | none | ticks |

### 5.2 Clamp-and-convert, and the leak it must close

When duration hits its bound, **the rung's remaining growth converts into intensity.** A top-rung
control is then not *"the same stun, longer"* but *"the same stun, far harder to resist."* Nothing is
lost, it is redirected — which is what makes it a **soft** cap rather than a ceiling.

No new machinery: [status-ssot.md](status-ssot.md) already splits `status.duration.*` from
`status.intensity.*` with identical omni/category/perId shape.

> **⚠️ The clamp must be the LAST step of Phase 2, after `durationNetFactor`.** That chain
> (`status.duration.{omni,category,id}` minus its `Reduction` siblings) is **uncapped today**. A clamp
> applied at authoring time is one a duration-stacking build walks straight through — the difference
> between a cap that holds and a cap that reads correct in the catalog and never fires in a fight.

### 5.3 The PvZ clock — real time drives, wall-clock is never stored

[battle-turn-ideal.md](battle-turn-ideal.md) §4 already specifies the `pvz-realtime` profile:
*"Inverted: the game's frame clock is the source and we **sample** it into ticks."*

And, in the same section, the invariant: *"Replay is virtual-time replay… **Wall-clock never enters the
recording.**"* A duration stored in real milliseconds breaks byte-identical replay, which is what the
content-hash and golden apparatus rest on.

> **Sample the frame clock into integer ticks at the boundary; store ticks.** The lawn gets real-time
> pacing at no determinism cost.

**⛔ Open (§0.2 A): a lawn actor has no turn**, so control duration has nothing to resolve against there.
This is empirical and is deferred by owner decision — *"needs a real test after we build the action
system and play."*

**What to build now so deferring is free:** make the duration unit a **per-mode resolver behind one
interface**. Battle resolves victim-turns → ticks. PvZ resolves whatever measurement says → ticks.
Authored content never changes; the day a real lawn is measured, that is one implementation, not a
re-authoring of every control action. Same shape as `Relation` compiling to `TargetSpec[2]`: **author
once, resolve per mode.**

---

## 6. Cost and cooldown

```text
cost(rung, Theta)  = anchorCost(Theta) * qCost(rung)      // ValueSpec, so it scales
cooldown(rung)     = baseCd * qCd(rung)                   // ticks. NEVER Theta.
```

**Cooldown rides the rung alone.** It is time, not a magnitude — a level-1000 actor waiting 1000× longer
is nonsense, and PS-3 does not cover it because a cooldown is neither contest nor magnitude. It is an
envelope field in ticks.

### 6.1 Cost span > power span — and this is where FOCUS lives

| If | Then |
|---|---|
| cost span **=** power span | a top rung is a bigger rung 1 at identical efficiency — you always equip your five highest, and the loadout is a **sort**, not a decision |
| cost span **<** power span | high rungs strictly dominate. Worst case |
| **cost span > power span** | high rungs are **burst you pay for**; low rungs stay sustain — five slots become a real mix |

**Take the third.** Then FOCUS is not flavour: it is the build that pays the escalation tax better,
because `skill.cooldown.*` and `resource.efficiency` are exactly the two multipliers this ladder taxes.

That also cashes a debt the class system logged — Focus measures **36% reserved**, and three of its
largest coefficients are unmeasurable *because neither engine has cooldowns* (ideal §8.1a). This
mechanism makes them measurable.

**The value is a tunable, settled by measurement — but the metric is declared now.** Seedsmith P2:
***"a metric without a declared target is an opinion."***

> **Metric: the share of equipped loadouts that mix rungs.** All five at top rung → the tax is too low.
> Nobody equipping top rung → too high. A healthy mix is the target.

Starting value **1.5× power span**. It doubles as the direct measure of how much room FOCUS has.

---

## 7. Seeding

### 7.1 The split is forced by P1

[seedsmith-map.md](seedsmith-map.md) P1: ***"The LLM writes identity; deterministic code writes
magnitude."*** *"A model has no calibrated sense of scale, so a number it picks is a plausible-looking
guess that survives review because nothing looks wrong with it."*

| Authored by hand — the balance surface | Generated |
|---|---|
| `sharePermille` per channel — [spec-numerics.md](seedsmith/spec-numerics.md) §2 calls this *the entire tunable surface*, and **refuses to guess one** | identity: name, flavour, the concept embodied (LLM) |
| the rung table (§4.1) | structure: which atoms, via pool + weights + `group` (deterministic, shipped) |
| `p1`, `delta`, `floor`, `cap` | every magnitude (deterministic, `numerics`) |
| per-demon-type category weights | |

### 7.2 The categories already exist — twice — and a third is the defect

| Vocabulary | Members | Consumed by |
|---|---|---|
| `action-category` | `attack · defense · support · movement · status` | `skill.cooldown.{category}`, `skill.effectiveness.{category}` |
| `tags_json` | `offensive · defensive · heal · buff · debuff · movement · summon · utility` | action selection — *"AI reads tags, never internals"* |

> **A demon type is a weight vector over the five shipped action-categories, plus its element/aspect
> bias.** One small authored row per type. A fire type weights `attack`; a warden type weights
> `defense`. Each type's unlocks *feel* like that type with zero handcrafting, and no third vocabulary
> is created.

### 7.3 ⛔ A pool that offers a conditional payoff must also offer its enabler

**Owner, 2026-08-27, on why a rot-conditional action is hard to actually use:**

> *"rot is one of 21 statuses, it needs 3 conditions to apply so it should be cheaper 3 or 4 times. A
> defence demon can be rotted (low rot resistance). An attack demon carries a rot status action or passive.
> And that attack demon can attack the target defence demon."*

Pricing handles the *discount* — that is §8.6 and E9's four-factor chain. **It does not handle whether the
combination exists at all**, and that is a generation problem:

> **`rot` is 1 of 21 statuses.** Weight the pool's statuses independently and a rot-conditional payoff will
> almost never share a ten-action pool with a rot applier, let alone a five-slot loadout. The discount is
> then **paid for a combination the generator never assembles** — a real price cut for an unreal
> capability, which is worse than not discounting at all.

**So the type weight vector carries a second thing: enabler/payoff pairing.** A type whose pool can roll
*"double damage against Chilled"* must also weight *"applies Chill"*. The pair is the unit, not the action.

This is what makes the five-slot loadout a **combo** rather than five independent picks, and it is the
generated counterpart of §8.1's definition — an action that references foreign state is only interesting if
something in reach can create that state.

**Consequence for seedsmith:** a coverage metric with a declared target, in `budget`'s own terms —
*every conditional payoff in a pool has at least one enabler in the same pool.* Closed-loop and machine-
checkable, which is P3's requirement for a metric that can verify its own fix.

---

## 8. Complexity — what makes an action interesting, and how it is generated

**Added 2026-08-27 after a comparison pass against PoE 2, Last Epoch and Diablo 4.** The first concrete
roster was thirty variations of one shape — `damage + one status` — and the owner's verdict was correct:
boring. This section is why, and the fix.

### 8.1 The definition, and it is not "more atoms"

The design literature is blunt that effect count is not depth. Jesse Schell's **"shallow complexity"** —
*"rules that burden players without enriching the experience"* — and the working test that a mechanic must
*"multiply strategic possibilities rather than merely adding to the rule count."*

> **An action is complex when it references state it did not create.**

| | |
|---|---|
| `damage + apply Chill` | creates its own state, self-contained. **Shallow** — however many atoms are stacked on it |
| `damage, doubled if the target is Chilled` | references state, so a *previous* action matters and the loadout acquires an order. **Deep** |

That is PoE 2's *Biting Frost* (*"Consume Freeze on enemies to deal 50% more Damage"*) and Diablo 4's
*"enemies affected by your Trap Skills"*, and it maps onto shipped vocabulary with no translation:
**every E3 predicate leaf is "read state you did not create"** — `hasStatus`, `hpBelowMilli`,
`resourceAboveMilli`, `elementIs`, `isMindControlled`, `actorIsKiller`.

**So complexity is predicate usage, not atom count** — countable, and therefore a generation budget rather
than a matter of taste.

### 8.2 Nine axes, and seven already ship

Derived from the PoE 2 support-gem catalogue, which is the clearest published list of *kinds* of effect:

| # | Axis | Example from another game | Us |
|---|---|---|---|
| A | **Condition** | *Biting Frost* — "Consume Freeze to deal 50% more" | ✅ E3 predicate tree — 10 leaves, AND/OR/NOT, depth 4 |
| B | **Sequence** | *Unleash* — "effects Reoccur for each Seal lost" | ✅ `resolve_offsets_json` — shipped in `ActionEnvelope.ResolveOffsets` |
| C | **Consumption** | *Biting Frost* again — consumes what it checks | ✅ `hasStatus` + `status.clear` |
| D | **Scope split** | strike-and-heal-self | ✅ `rpg_action_effect_scope` — caster / primaryTarget / eachTarget / casterAllies |
| E | **Conversion** | Last Epoch — "converts poison chance to chill chance" | ✅ `element` param |
| F | **Reaction** | *Behead* — "Killing Blows grant one of their Modifiers" | ✅ 7 triggers, incl. `OnDeath` + `actorIsKiller` |
| G | **Geometry / targeting** | *Astral Projection* — "Cast at the targeted location instead" | ⚠️ exists on the **action row**, but §8.4 |
| H | **Linkage** | *Corrosion* — "Breaks Armour **equal to 80% of Poison Damage dealt**" | ❌ **gap** — §8.5 |
| I | **Restriction** | *Brutality* — "35% more Physical" + "deal **no** Chaos" | ✅ **closed 2026-08-27** — a restriction is a **self-debuff**: `status.apply` scoped to `caster`. Nothing new. §8.7 |

**Seven of nine were already available and the first roster used none of them.** The shallowness was the
authoring, not the vocabulary.

### 8.2a A single atom already carries seven dials

**Owner, 2026-08-27:** *"my effect atom is plenty — like trigger condition, trigger change."* Correct, and
worth writing out, because the first roster used **one** of these seven. From `effect_atom`'s columns:

| Dial | What it holds | Range |
|---|---|---|
| `kind_id` | what it does | **12** kinds |
| `when_json` → trigger | when it fires | **7** — `OnSpawn` `OnDamageDealt` `OnDamageTaken` `OnDeath` `OnGranted` `OnRemoved` `OnTimer` |
| `when_json` → `chance` | how often | ‰, so a rider can be rare |
| `when_json` → `icd_ms` | internal cooldown | throttles a cheap trigger without changing it |
| `when_json` → **predicate tree** | *under what conditions* | **E3** — 10 leaves, AND/OR/NOT, depth 4, 16 nodes |
| `params_json` → **roll policy** | when the number resolves | `Fixed` · `OnInstantiate` (frozen at drop) · `OnApply` (**rolled per hit**) |
| `params_json` → `curveId` | how it scales | `effect_curve`, integer-interpolated |

Two consequences the roster missed entirely:

**`OnApply` is where "500–1,000" actually lives.** A damage range is not decoration — PoE and D2 roll
damage *per hit*, and `OnApply` is that exact policy. The roster wrote bands and then treated them as flat
numbers.

**`icd_key` composes atoms into one grant.** *"Atoms sharing a key compile into one grant whose `Triggers`
is the **union** of theirs."* So *"fires on hit **or** on death, but at most once every 5s"* is two atoms
plus a shared key — no new mechanism, and no way to express it with one atom.

> **The combinatorial surface was never the constraint.** Twelve kinds × seven triggers × a chance × an
> ICD × a predicate tree × three roll policies × a curve, and a container holds many of them. **Thirty
> actions that each used one kind, one trigger and no predicate was an authoring failure**, and naming it
> that way is what stops the next roster repeating it.

### 8.3 ⛔ The rung must add STRUCTURE, not only numbers

This is the change that matters most, and it is a correction to §4.2 rather than an addition.

As written, `value = anchor(Θ) × q(rung)` means a rung-10 action **plays identically** to a rung-2 one and
merely hits harder. That is the definition of shallow complexity, built into the ladder.

> **A rung buys numbers *and* a structure budget.** The rung table (§4.1) gains a column.

| rung band | structure the rung unlocks |
|---|---|
| 1–2 | one atom, no condition — the plain verb |
| 3–4 | + a rider status, **or** a scope split (D) |
| 5–6 | + a **condition** (A) — the first rung that references foreign state |
| 7–8 | + a **sequence** (B) **or** a **consumption** (C) |
| 9–10 | + a **reaction** (F) **or** a **restriction** (I) |

A rung-10 action is then a **different kind of thing** from a rung-2, which is what Last Epoch's deep nodes
do — *"Investing Skill Points empowers your Skills and can even **transform them entirely**."* It also gives
the keep-or-discard decision (§3.2) something to weigh beyond a number.

### 8.4 Seedsmith rolls the target spec too, not only the atom list

Axis G is not a vocabulary gap — targeting is an `rpg_action` column (`target_spec_json`, `min_range`,
`max_range`, `anchor_source`). It was simply **left out of the generation surface**.

Adding it costs nothing and is a whole variation dimension: the same atom list at single-target, at
`eachTarget`, and at a `Square` area is three genuinely different actions. **The demon-type weight vector
(§7.2) should weight target shapes alongside categories.**

### 8.5 Linkage is a TRIGGER CHAIN, and the event already carries the number

**Owner, 2026-08-27, correcting an earlier draft of this section:** *"wrong — use atom effect trigger
condition. `Heal for 50% of the damage this attack dealt` is a **2-step trigger**, that should be a chain of
effects; and if the atom does not ship it yet, it means missing and we extend the effect runtime."*

Correct. The earlier draft proposed a new `ValueSpec` **source family** for reading event outputs. That was
the wrong shape — it invented a parallel mechanism for something the trigger vocabulary already models:

```text
step 1   the attack resolves            -> resource.delta{hp, -}
step 2   an atom fires on that event    -> resource.delta{hp, +}   when: trigger OnDamageDealt
```

**Three of the four pieces already ship**, verified in code:

| Piece | Where |
|---|---|
| The trigger | `OnDamageDealt`, one of the closed 7 |
| **The damage amount on the event** | **`EffectEventDto.Damage`** — `src/FusionRpg.Contracts/EffectDtos.cs:64` |
| A chain-depth guard | `EffectEventDto.ChainDepth`, alongside `ProcDepthLimit` |
| **A magnitude that reads it** | ❌ **the only missing link** |

> **The gap is one link, not a family:** an atom's magnitude cannot reference a field the event is already
> carrying. Everything else in the chain is shipped.

**And the special case ships while the general case does not.** `leech` is a status — *"OverTime dual
pulse: hurt target, heal ActorPtr"* — which is lifesteal, hardcoded. The mechanism exists exactly once, as
content, with no way to author a second instance of the same shape. That is the strongest argument for
generalising it rather than adding a third bespoke status.

**Owner: *"seem like every game have this mechanism?"*** — yes. Life steal in D2/D3/D4, leech in PoE and as
a secondary stat in WoW, and PoE's *Corrosion* (*"Breaks Armour equal to 80% of Poison Damage dealt"*) is
the same link on a different channel. It is table stakes rather than a flourish.

**Still the atom program's call**, because it touches param resolution in a sealed contract — but it is now
*"let a magnitude read `ev.Damage`"* rather than *"add a value-source family"*, which is a much smaller ask
and one the runtime is already half-way to.

#### How other engines do it — researched 2026-08-27

**Unreal's Gameplay Ability System has four magnitude types. We adopted one.**

| GAS magnitude type | Us |
|---|---|
| **ScalableFloat** — a number, optionally curve-driven | ✅ `ValueSpec` `Fixed`/`Range` + `CurveId` |
| **AttributeBased** — *"CurrentValue or BaseValue of a backing Attribute on the **Source or Target**, further modified by a coefficient"* | ❌ — this is *"10% of the target's max HP"* |
| **SetByCaller** — *"allow the Spec to carry float values associated with a tag around"* | ❌ — this is *"ferry the damage dealt into the triggered effect"* |
| CustomCalculationClass | ❌ and **not wanted** — it is code, and the balance surface is config |

> **We already borrowed GAS's snapshot rule and stopped there.**
> [spec-value-spec-and-curve.md](effect-atom/spec-value-spec-and-curve.md) cites it by name: *"GAS makes
> exactly this a per-value flag — snapshotted attributes captured at spec creation, non-snapshotted at
> apply."* That is our `OnInstantiate` vs `OnApply`. **The two types we skipped are precisely the two that
> express linkage**, which is why linkage is the one thing missing rather than one of many.

**Two asks, not one — and they are separable:**

1. **`SetByCaller` shape** — a magnitude that reads a field the event already carries (`ev.Damage`). Covers
   lifesteal, *Corrosion*, and the Bamblock caster-side half. **The small one; take it first.**
2. **`AttributeBased` shape** — a magnitude that reads a Source or Target attribute with a coefficient.
   Covers *"10% of the target's max HP"*. A separate, larger ask.

#### And the balance half is already right

**Path of Exile does not apply leech instantly**, which is what stops *"one huge hit fully heals you"*:

| PoE leech | |
|---|---|
| each hit creates an **instance** | unlimited instance count |
| per instance | `damage × leech%`, capped at **10% of max life** |
| rate | **2% of max life per second** |
| **total rate cap** | **20% of max life per second** — excess is ignored |

**The degenerate case is bounded by a RATE CAP, not by the per-hit number.** Our shipped `leech` status is
already *"OverTime dual pulse"* — the same shape — so the content half of this was got right the first
time. **Only the authoring half is missing:** the pattern exists once, hardcoded, with no way to write a
second instance of it.

**So the rule for whatever lands:** a linked payout is a **rate over time with a cap**, never an instant
lump. That is a content rule this program owns, independent of which magnitude type the atom program adds.

### 8.6 ✅ Predicates ARE priced — decided 2026-08-27

E9 used to say *"predicates are deliberately not priced"*, which meant *"damage, doubled if Chilled"* priced
as though the doubling always happened: rare conditions **overpriced** and refused by the rung budget,
guaranteed conditions **underpriced** — and a build that guarantees its own condition is precisely what a
five-slot loadout is for.

**Owner:** *"should calculate price by apply chance, should set it as tunable value — like `deal x2 damage
on rotted zombie` versus `deal x2 damage`; the second statement is higher price."*

**It needs no new mechanism.** A predicate is priced exactly the way a trigger already is — E9 gains a
`predicateFrequency` factor backed by a `power_predicate_frequency` table, parallel in every respect to the
`power_trigger_frequency` table that ships:

```text
conditionality = (chance/1000) x triggerFrequency x icdFactor x targetCountFactor x predicateFrequency
```

`1000‰` when there is no predicate. The tree composes in per-mille — `And` multiplies, `Or` is the
complement of the product of complements, `Not` inverts. Written up in
[effect-atom/spec-power-vector.md](effect-atom/spec-power-vector.md).

**Why this matters to the ladder specifically.** §8.3 asks a rung-6 action to buy a *condition* on top of
its numbers. Under flat pricing the condition would cost full price and the rung budget would refuse it, so
**the structure ladder was unaffordable by construction.** Discounting the condition by how often it holds
is what makes conditional actions authorable at all.

**The chain is FLOORED, and the floor is the balance guard.** Owner, 2026-08-27: *"3 or 4 cheaper is
too high — maybe 2 or 2.5 as default, else we can ship some imbalanced build if the player focuses combo
play; that can be unfair for Zomboss."*

```text
predicateFrequency = max(400, reachability x susceptibility x coincidence x uptime)   // permille
```

> **The chain measures the AVERAGE case; the price must hold against the BEST case.** A combo build does
> not experience the average. Unfloored at `250‰`, a build landing its condition 80% of the time pays 25%
> and receives 80% — **3.2× value**. At the `400‰` floor it pays 40% for the same 80% — **1.6×**: still a
> real payoff for building around a condition, no longer a dominant one.

**The Zomboss asymmetry is the argument.** Both sides can carry combos; **only the player picks theirs
after seeing the opponent.** A Zomboss pattern is an authored allocation, fixed at design time. So any
mechanic rewarding *adaptive* assembly favours the player structurally — and the class system's dominance
matrix cannot catch it, because that matrix compares **allocations, not loadouts**. A combo-driven
dominant build is invisible to the guard built to find dominant builds.

That also inverts this layer's job: `class-system-ideal.md` §8.8b makes the dominant corner **the
action/passive/skill layer's to fix**. A discount generous enough to create a new dominant build would
have this layer manufacturing the defect it was brought in to remove.

### 8.7 Restriction is a self-debuff — closed, and it needed nothing

**Owner, 2026-08-27:** *"negative effect — atom effect needs to extend itself, like getting a debuff status
on the attacker after taking a burst action."*

That closes axis **I**, and it retracts three options an earlier draft offered (a new `Flag` channel per
restriction, a closed set of restriction channels, or `Replace`-to-zero). All three went looking for a
*disable-a-capability* mechanism. **The genre answer is simply: the action debuffs you.**

```text
scope: primaryTarget   ->  resource.delta{hp, -}          the burst
scope: caster          ->  status.apply{<debuff>}         the price
```

Everything is shipped: `rpg_action_effect_scope` already carries `caster` as a scope, and a status that
lowers derived channels is the same machinery as `rally` (*"Buff ModifyStat — Timed ATK More"*) with the
sign flipped — E17 shipped the `ModifyStat` consumer.

**Two shapes of restriction, both now available**, and they are complementary rather than rivals:

| Shape | Mechanism | Example |
|---|---|---|
| **Tempo** — you cannot act | `recovery_ticks` on the envelope | *Devour*: 3× damage, then 10s unable to act |
| **Capability** — you act worse | **self-debuff**, `status.apply` scoped `caster` | burst now, −40% accuracy for 8s |

What this *does* need is **debuff statuses to exist as content** — the catalog is 21 declared and ~13
functional. That is authoring, not architecture, and it is the same pool the seeded ladder draws from.

---

## 9. What this supersedes in the shipped action specs

The ten specs in [action/](action/) were written 2026-08-22. These lines are stale:

| Where | Says | Should say |
|---|---|---|
| [spec-action-costs.md](action/spec-action-costs.md) §1 | "the five resources" | **six** — `poise` shipped 2026-08-26 (`DerivedStatChannels.cs:510`) |
| [tasks/action-todo.md](../../tasks/action-todo.md) T12 | "Five ids code-first" | six, **and already built** — T12 shrinks to wiring |
| [tasks/action-todo.md](../../tasks/action-todo.md) T12 | "`AllCombatChannelIds` is **still exactly 84**" | 259+. This is an *acceptance criterion*, so it becomes a red test rather than a confusing sentence |
| [action-map.md](action-map.md) §2 | "84 combat + … **Zero resource channels**" | resource channels are registered (`DerivedStatRegistry.cs:165-171`) with no reader |
| [action-map.md](action-map.md) B2 | open — "Lock the five ids as a `decisions.md` row" | **done**, six, 2026-08-26 |
| [action-map.md](action-map.md) A3 row | "Five resources" | six; **guard pays `poise`, not `stamina`** |
| [spec-defence-actions.md](action/spec-defence-actions.md) §1 | *guard* filed as a **reaction** | guard is a **stance** (§2.2), and is therefore **not blocked on B6** |
| [spec-action-costs.md](action/spec-action-costs.md) §3 | *"committing is what costs… one rule with **no exceptions**"* | still true, but `poise` is a **documented split** — flat commit (the action) + absorb drain (the mitigation) + per-tick hold. Needs a sentence, not a rewrite |
| [design/spec-action-layer.md](../design/spec-action-layer.md) §2 | five-resource cost cluster | six |

**Gate 0 has also cleared.** [tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md) is 27/27 and
committed at `842907f`, so the action program's own blocking gate is satisfied — with that program's own
caveat that its completeness audit found most of the layer does not yet reach the running game.

---

## 10. Related

- [action-map.md](action-map.md) — the capability map, and §8's reconciliation target
- [class-system-ideal.md](class-system-ideal.md) §5b (action costs), §8.1 (`stamina` is free, FOCUS)
- [class-system/spec-guard-economy.md](class-system/spec-guard-economy.md) — `poise`'s cost shape
- [power/ssot-power-scale.md](power/ssot-power-scale.md) §4 (the ladder), §10.2 row 7 (the precedent), §11.1a (the price is the cap)
- [effect-atom/spec-container-schema.md](effect-atom/spec-container-schema.md) — the generator, built
- [effect-atom/spec-power-vector.md](effect-atom/spec-power-vector.md) — E9, the monotonicity check
- [seedsmith-map.md](seedsmith-map.md) P1 · [seedsmith/spec-numerics.md](seedsmith/spec-numerics.md) §1–2
- [battle-turn-ideal.md](battle-turn-ideal.md) §2–4 — the FSM, `W`, the virtual clock
