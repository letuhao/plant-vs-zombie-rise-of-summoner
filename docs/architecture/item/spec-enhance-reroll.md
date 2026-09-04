# Spec: `enhance-reroll`

**Module id:** `enhance-reroll` · **Program:** [item](../item-map.md) · **Build order:** 15 of 21
**Depends on:** `item-power-reads` (module 9 — **one read, R3; see §10**), `salvage-craft` (module 14),
`rarity-bands` (module 7 — seeds the `enhance_cap` asymptote this module consumes, §4a)
**Lanes:** [ssot-enhancement.md](ssot-enhancement.md) (I6) · [ssot-reroll.md](ssot-reroll.md) (I7) ·
[decision-d2-mutation-contract.md](decision-d2-mutation-contract.md)
**Rulings:** **D7**, D9, D23, **D26**, D29

## Objective

The two operations that change an item after its rolls are frozen — **enhancement** (`+X`, the same item
made stronger) and **reroll** (the same item made *different*) — built on **one** mutation contract, so
there is exactly one op log, one replay law, one idempotency story, and one place a later module can
break them.

The contract is not designed here. **[D2 §9](decision-d2-mutation-contract.md) already ruled it** —
fifteen numbered clauses, adopted verbatim (§1). What this module owns is the two operations, their
prices, their risk shape, and **D7's mandate: a perfect item is reachable by grinding, never blocked by
luck.**

## Design

### 1. The mutation contract is adopted, not re-derived

D2 §9's fifteen clauses are binding and replace `ssot-enhancement.md` §7.8 wherever the two disagree.
The four that shape every line below:

| D2 clause | What it forces here |
|---|---|
| **1 — the head is the SSOT** | `effect_instance_atom.values_json` always holds current numbers. No read path composes anything. Enhancement rewrites in place |
| **3 — the guarantee is 1′ + 2 + 3** | `replay(origin_values_json, ops[1..n]) == head`, byte-exact, **with no catalog involved**. `state_hash` is the check; a mismatch is `ReplayDivergence`, a defect |
| **4 — record the result, never the recipe** | `result_json` holds materialised deltas and the decided `outcome`. Replay never re-runs the formula and never re-rolls the dice. **This is what makes a rebalance structurally unable to touch an owned item**, and it must not be traded for log size |
| **14 — there is no `effect_instance_atom.overrides_json`** | ✅ **Verified.** The DDL is `instance_id · seq · atom_id · values_json · power_json` (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:73-80`) and `Instantiator.Freeze` leaves an `OnApply` spec **as authored** (`src/FusionRpg.Core/Effects/Atoms/Instantiator.cs:306-311`, `_ => raw`). Enhancing an `OnApply` affix rewrites `min`/`max` **inside the spec object in `values_json`** |

Columns this module adds — the only schema this module owns:

| Table | Column | Why |
|---|---|---|
| `effect_instance` | `enhance_level INT NOT NULL DEFAULT 0` | the `+X`. One writer |
| | `enhance_pity_counter INT NOT NULL DEFAULT 0` | §4's catalyst counter, reset on guarantee |
| | `mutation_seq INT NOT NULL DEFAULT 0` | `= max(op_seq)`. Structural cap 4096, and it says so in a comment |
| | `state_hash TEXT` | definitions §8's canonical form — SHA256, length-prefixed, sort-then-concatenate, **XOR-fold banned** |
| | `origin_values_json TEXT NULL` | D2 rung 1′. Written lazily at first mutation (D2 §11.3's lean) |
| `effect_instance_atom` | `suppressed INT NOT NULL DEFAULT 0` | D2 clause 9 — identity change is suppress-then-append, `seq` is never renumbered |
| `effect_instance_op` | *(new table, D2 §9 clause 2)* | the ledger. `UNIQUE(instance_id, correlation_id)` |

⚠ `origin_catalog_revision` is **not** a new column — it already exists as `effect_instance.catalog_revision`
(`RpgStore.AtomInstances.cs:66`), and D2 §7.1 granted it as a **semantic lock**: origin-only, no operation
rewrites it. I6 §5.1's request for a new column was refused.

### 2. ⛔ Platform correction — `pool_rolls` does not exist, and it breaks I7's algebra

I7 is built end to end on `pool_rolls`: `T` targets, `K = pool_rolls − T` anchors, `ANCHOR_MULT = 2^K`,
`T > pool_rolls` rejects, and a *"two sources of truth for `pool_rolls`"* hazard handed to I1 (§4.2).
**Verified: the column is gone on both tables.**

| I7 claim | Verified |
|---|---|
| `ContainerRow.PoolRolls` (`ContainerRow.cs:64`) | **`PrefixRolls` / `SuffixRolls`** (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:119-127`), DDL `RpgStore.Containers.cs:27-28`. The doc comment states it *"replaces the single `PoolRolls`"* |
| `RarityRow.PoolRolls` (`ContainerRow.cs:93`) — the second source of truth | **`RarityRow(RarityId, Ordinal, PrefixRolls, SuffixRolls, MinTier, MaxTier)`** (`ContainerRow.cs:158`), DDL `RpgStore.Containers.cs:54-61`. ⭐ **The hazard I7 handed to I1 no longer exists** — both tables split the same way, and the container's is still authoritative |
| `effect_container_pool(container_id, atom_id, weight, group)` | **`(container_id, affix_id, weight, group_key)`** (`RpgStore.Containers.cs:44-50`, `ContainerRow.cs:38`). A reroll re-draws **affixes**, not bare atoms |
| `Instantiator.Draw(container, lookupAtom, seed)` with one exclusion set | **`Draw(container, lookupAtom, lookupAffix, rollSeed)`**, which runs `DrawBudget` **twice** — once per budget, each with its own RNG stream `pool.{budgetName}.{containerId}` (`Instantiator.cs:180-203`) |

**Consequence — anchoring is per budget, and it is a better design than the one it replaces:**

```text
T_prefix + T_suffix  >= 1                      // at least one target, or the op is a paid no-op
K_prefix = container.PrefixRolls - T_prefix
K_suffix = container.SuffixRolls - T_suffix
ANCHOR_MULT = 2 ^ (K_prefix + K_suffix)        // superlinear, unchanged in shape
```

A partial redraw seeds each budget's exclusion set with the **groups of that budget's retained affixes**
before drawing — the one behavioural change this module needs from `Instantiator`, and it is now two
parameters on `DrawBudget` rather than a new signature on `Draw`. The existing call site passes the full
counts and an empty set, so **instantiation is byte-unchanged**.

⚠ **The reroll post-op invariant restates per budget:**

```text
count(drawn prefix affixes) == container.PrefixRolls
count(drawn suffix affixes) == container.SuffixRolls
distinct groups(drawn)      == count(drawn)                     // one-per-group holds
every drawn affix           in container.Pool
every drawn atom's tier     in [container.MinTier, container.MaxTier]
```

⚠ **A `Mixed` affix consumes one prefix roll AND one suffix roll simultaneously** (`ContainerRow.cs:41-47`).
Rerolling a `Mixed` affix therefore frees a slot in **both** budgets and must redraw into both, or the
invariant fails. `Instantiator.Draw`'s own comment calls today's two-independent-draws model *"an interim,
honestly-documented simplification"* (`Instantiator.cs:174-178`) — **this module must not build a second
simplification on top of it.** If module 2 `resolution-order` has not landed the real semantics, a reroll
targeting a `Mixed` affix is refused with `NotRerollable` until it has. Stated so it is a decision, not a
bug found later.

### 3. ⛔ D7 — crafting reaches t5, gated by COST, never by luck

> *"Looking for a perfect item (of course very op item) will be cost very much effort but dont make it
> impossible by chance, that is not fun."*

Three requirements, each answered by a named mechanism:

| D7 requirement | Mechanism | State |
|---|---|---|
| Material cost scaling steeply with affix tier and rarity | module 14's cost table, keyed on the **target's** rung and tier | ✅ built by 14 |
| A success chance on strong crafts | §4's three bands | this module |
| **Bad-luck protection — mandatory, not optional** | §5's guaranteed-tier counter | this module |

**And the top of the ladder is reachable.** `ssot-rarity` rule 7 was lifted by the owner 2026-09-03
([item-ideal.md](../item-ideal.md) §2f.2, D7 row): promotion reaches ordinal 100, **no drop-only band
exists on any axis**, so no affix family sits behind luck.

### 4. The risk shape, and why the cost curve is the only cap

Three bands (I6 §4 D3+D4, kept):

| Band | Levels | Success | Failure |
|---|---|---|---|
| **Safe** | +1 … +8 | 1000‰ | — |
| **Risk** | +9 … +14 | 950‰ → 600‰ | materials spent, level unchanged, pity counter +1 |
| **Peril** | +15 … up | 500‰ → 200‰ | as above; from **+17** a failure may drop **one** level unless a `ward.enhance` is loaded |

**There is no destroy outcome — not as an enum value, not as a reason code.** A code nothing emits is a
lie in a table, and reserving one invites a later session to wire it up.

#### ⛔ The cap rule — this is where AGENTS.md bites

`ssot-power-scale.md` §11 is explicit that *"a flat rate facing a scaling sink"* **is a cap**. A steep
enhancement cost curve facing an unbounded content ladder is exactly that shape.

| Thing | Standing | Where it lives |
|---|---|---|
| The cost curve | **configurable soft cap** — it makes the next level less worth it, it never refuses | `data/tuning/enhancement.v1.json` |
| The risk curve (falling success) | **configurable soft cap** — same shape, same file | same |
| `mutation_seq ≤ 4096` | **structural**, and the comment says so: a retry loop, not a design ceiling | `const` in code |
| `ilvl_cap(ilvl) = max(4, 4 + ilvl/4)` | **floor only, no ceiling** — ilvl 128 → +36, ilvl 500 → +129 | tuning |
| ⛔ `rarity_cap` per rung, topping out at +20 | **REMOVED as a level ceiling.** I6 §7.3 already reconciled it once; the ten-rung ladder and D29's unbounded content ladder finish the job. ⚠ **But `enhance_cap` returns as a *gain asymptote*** — §4a | `data/tuning/item-rarity.v1.json` |

D29: **tier saturates at t5 and that is correct** — growth past it is carried by `contentScale`, which is
built (`InstanceProducer.cs:47`, `ContentScale.Milli`). An ilvl-500 t5 affix is the same tier as an
ilvl-32 one and a far bigger number. **So an enhancement cap keyed on tier would be a ceiling on a system
that has none.**

⛔ **D26 applies here too:** the enhancement cost curve reads the **target's** rarity ordinal, item level
and current `+n`. It never reads the player's `Θ`, power index, item count or any per-day counter.

### 4a. ⛔ `enhance_cap` — the conflict with module 7, resolved as a **shrinking soft cap** (§2g #0c)

**This is a live blocker, not a difference of emphasis.** Module 7 seeds `enhance_cap` and registers
`enhance_cap_gain_never_exceeds_one_rung_step_at_any_rung` as **HARD**; this spec removes the mechanism
and asserts `no_enhancement_cap_is_a_hard_stop`. ⭐ **And module 7's SC7 rule makes a `rarity_budget`
key whose consumer has not shipped *reject*** — so if this module deletes the consumer, module 7's
**seed load fails**. Whichever ships second turns the other red.

**Both halves are right, and one curve satisfies both:**

| Half | Claim | Standing |
|---|---|---|
| This module | a hard `+X` ceiling is forbidden — AGENTS.md, D7 (*"cost, never luck"*), D29 | ✅ kept |
| Module 7 | at the top of the measured ladder a `2×`-at-cap gain is **3.46 rung steps** (`ln 2 / ln 1.222`), so a maxed `firstseed` clears a natural `almanac` — the ladder inverts | ✅ kept |

> **The resolution: enhancement gain *asymptotes below* one rung step instead of stopping at one.**
>
> ```text
> gainMilli(n, rung) = enhance_cap(rung) × n / (n + K)
> ```
>
> It approaches `enhance_cap(rung)` and never reaches it, so **no level is ever refused** — the curve
> is a soft cap in AGENTS.md's exact sense, and `+1` at any `n` still buys something. And because the
> asymptote sits below the local rung step, **the ladder cannot invert at any `n`**.

**`enhance_cap` is re-specified, not deleted.** It is no longer a maximum `+X`; it is the **per-mille
asymptote of total enhancement gain**, seeded per rung by module 7 from the measured `step(rung)` table
and consumed *here*. SC7 is satisfied because the key has a live reader.

| Rung | `enhance_cap` ‰ | | Rung | `enhance_cap` ‰ |
|---|--:|---|---|--:|
| `chaff` · `sprout` | 860 | | `heirloom` | 260 |
| `grafted` | 762 | | `firstseed` | 232 |
| `cultivated` | 627 | | `sunwoven` | 200 |
| `fused` | 619 | | `almanac` | 200 |
| `chimeric` | 552 | | | |

**Module 7 owns the column; this module owns `K`**, which lives in `data/tuning/enhancement.v1.json`
and is the only number here a balance pass touches. The derivation, the two-rung smoothing and the two
non-derived rows are recorded once, in
[spec-rarity-bands.md](spec-rarity-bands.md) §*Resolution — a shrinking soft cap*.

⚠ **It binds at v1's reach, so it is not a future-content guard.** `ilvl_cap(32) = 12`, and I6's linear
+20‰-per-level scalar reaches **240‰** at +12 — already past `firstseed`'s 232‰ and both 200‰ rows. The
asymptotic curve at the same `+12` with `K = 8` yields 120‰ on an `almanac`, which is the behaviour
change, and it is a change to the *shape* rather than a ceiling.

**The two tests are rewritten against the one curve:**

| Test | Owner | Asserts |
|---|---|---|
| `no_enhancement_gain_is_a_hard_stop` | **here** — replaces `no_enhancement_cap_is_a_hard_stop` | for every rung and every `n`, `gain(n+1) > gain(n)`; no level is refused; `ilvl_cap` still has a floor and no ceiling |
| `enhance_cap_asymptotes_below_one_rung_step_at_every_rung` | **module 7** — replaces `enhance_cap_gain_never_exceeds_one_rung_step_at_any_rung` | `lim gain < step(rung) − 1` on all ten, from the same seeded column |

**Cross-checked both ways:** the pair above is recorded identically in `spec-rarity-bands.md`. If either
moves, the other is wrong — which is the property the previous arrangement lacked.

### 4b. ⛔ The sinks have an expiry date, and it is **inside D26's scope** (§2h.3)

**Every spec in this program pushed this out as content pacing. It is not.** D26 draws the line at
*"the item system balances items against each other; it does not balance the game"* — and *"is a
crafted item still worth more than a fresh drop"* is an **item-versus-item** comparison. It is
squarely ours, and it decides whether §4's risk bands and §5's pity counter are apparatus for a
decision players will actually make.

**Computed against `PowerLadder`, not estimated.** Every input is a shipped constant:

| Input | Value | Source |
|---|---|---|
| `P(Θ) = c + A·Θ + B·Θ(Θ−1)/2` (milli) | `c = 80,000` · `B = 400` · `A = 26,200` (re-derived at load so `P(20) = 680`) | `data/tuning/power-scale.v2.json`; `Power/PowerLadder.cs:33-53` |
| `contentScale(Θc) = P(Θc) / pinValue` | `pinValue = 680` | `Power/ContentScale.cs:17-20` |
| **one realm = 25 Θ** | `WfMilli = WaMilli = 25,000`, and equality is enforced | `PowerIndexComposer.cs:46-51`, `power-scale.v2.json` |
| `ilvl_cap(ilvl) = max(4, 4 + ilvl/4)` | +12 at ilvl 32; +129 at ilvl 500 | `ssot-enhancement.md:437-438` |
| enhancement scalar | **+20‰ per level, never compounded** | I6 §3.3, §6 below |

#### The expiry, reproduced

At v1's shipped reach (`Θc = 20`, ilvl 32 per D4) a **perfected `almanac`** is
`770 × (1 + 0.020 × 12) = ` **954.8** hp-equivalent on [`ssot-rarity.md`](ssot-rarity.md) §7.3's measured ceiling-5 ladder (`almanac` = 770, `sprout` = 17). A **freshly dropped,
unenhanced `sprout`** at `Θc = 500` is `17 × contentScale(500) = 17 × 92.76 = ` **1,577**.

**The crossing is at `Θc = 376`** — solved, not bracketed: it is the Θc where
`contentScale = 954.8 / 17 = 56.16`, i.e. `P(Θc) = 38,192`. That lands inside §2h.3's stated 350–450
band and confirms it.

#### *"A crafting investment is worth N realms"*

`N(Θc) = (Θc′ − Θc) / 25`, where `P(Θc′) = gain(Θc) × P(Θc)` and `gain` is the full enhancement track
at that depth.

| Θc | `ilvl_cap` | crafting gain | Θc′ | ΔΘ | **N realms** |
|--:|--:|--:|--:|--:|--:|
| **20 — v1's shipped reach** (ilvl 32) | +12 | ×1.24 | 24.67 | 4.67 | **0.19** |
| 20 (ilvl = Θc) | +9 | ×1.18 | 23.53 | 3.53 | **0.14** |
| 50 | +16 | ×1.32 | 62.41 | 12.41 | 0.50 |
| 100 | +29 | ×1.58 | 136.98 | 36.98 | 1.48 |
| **123** | +34 | ×1.68 | 173.28 | 50.28 | **2.01** ← first depth where N reaches 2 |
| 200 | +54 | ×2.08 | 311.75 | 111.75 | 4.47 |
| 500 | +129 | ×3.58 | 999.40 | 499.40 | 19.98 |

> ⛔ **N ≈ 0.19 at everything the game currently ships.** A full crafting investment is worth about
> **one fifth of one realm**. §2h.3's threshold is 2, and N does not reach it until `Θc ≈ 123` — five
> realms deep into a content ladder that stops at level 10 today (**X5**).

**And §4a's soft cap makes it smaller, deliberately.** On an `almanac`, `enhance_cap = 200‰`, so the
gain **asymptotes at ×1.20 — N ≤ 0.16 at any `n`** — and at v1's reachable `+12` with `K = 8` it is
×1.12, **N = 0.09**. That is the correct direction; the alternative is a gain that inverts the rarity
ladder. But it means the honest statement is: **at v1 depth crafting is not competing with content
progression and cannot be made to.**

#### What follows, and what does not

| Consequence | Standing |
|---|---|
| ⛔ **Do not size §4's risk bands or §5's pity threshold as if they were a progression choice at v1 depth.** They are not; the player advances a realm instead | **decided here** |
| ⭐ **I6 §7.4 transfer stops being a nicety and becomes the mechanism that makes crafting survivable** — it is the only way an investment follows the player past a content step. **§6a below builds it** | **decided here** |
| **The investment is unrecoverable when the item is replaced**, which is what turns a small N into a *skipped* system rather than a merely weak one. ⚠ §2h.3 attributes this to *"R2's strict-loss rule"*; item-ideal's **R2 is the catalog-revision-equality defect** (§2e, closed by D9) and says nothing about loss on replacement. **Verified independently instead:** the only recovery path in I6 or I9 is **transfer at 700‰** (§6a) — salvage returns materials, never levels | recorded; the attribution is corrected, the finding stands |
| Raising N by making the enhancement track steeper | ⛔ **refused** — it re-inverts the ladder, which §4a exists to prevent |
| Raising N by flattening `contentScale` | **not ours.** That is the power ladder's `bMilli` dial (**PS-7**, `ssot-power-scale.md`), and D26 keeps content pacing out of this program |
| **Reporting N** | ⭐ **ours, and it ships.** `CraftingHorizonReport` computes the table above from the loaded `PowerTuning` and the seeded `enhance_cap` column, so the figure moves when the dials move instead of being a number in a doc |

⚠ **The comparison unit is module 9's, not a second one.** N is a ratio of two `P(Θ)` values, which
needs no pricer — but the *item-versus-item* half (a perfected item against a fresh drop of another
rung) is a cross-family comparison and reads **R3**, `PowerScalar` with its ±25% band
(`spec-item-power-reads.md` §R3). See §10.

### 5. ⛔ Bad-luck protection — and a genuine cross-document conflict, resolved

`rpg_summon_pity` is the in-tree precedent and it is **verified**:

| Claim | Verified |
|---|---|
| a persisted per-player pity table | `rpg_summon_pity(player_id PK, pulls_since_epic, pulls_since_legendary, updated_utc)` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:529-534`) |
| two counters, cross-banner, visible | `PityState(PullsSinceHeirloom, PullsSinceSunwoven)` (`src/FusionRpg.Core/Demons/SummonRoller.cs:12`) — the SQL column names deliberately kept their old labels (`SummonRoller.cs:6-11`) |
| read and written inside the pull transaction | `RpgStore.Summons.cs:200`, `:210` |
| hard pity at 25, soft ramp from 41, hard at 55, a 10-pull floor | stated in `SummonRoller.cs:23-30` |

#### The conflict

- **D7** requires *"a perfect item must be reachable by grinding… never impossible"*, which on a
  tier-hunting reroll means a **tier** guarantee.
- **`ssot-rarity.md` §3.8** forbids exactly that: *"Pity may key on **rung only** — never on roll quality,
  never on tier. A quality pity makes draws non-independent, and §3.5's invariant is measured on
  independent draws."*
- §3.5's overlap invariant is **measured, not asserted** — 2 × 10⁵ rolls per rung, seed `20260822`,
  `U(n,1)` 7.9–28.3 % against a required 5–30 % (`ssot-rarity.md` §3.5). **Adding a tier pity to the draw
  would invalidate the measurement**, not merely change a number.

#### The resolution — decided, and it is not a compromise

> **§3.8's rule is scoped to *drop* pity. Craft pity is a separate deterministic mechanism, and it
> touches no weight.**

Read §3.8's own heading row before quoting it as a universal law: every one of its rules is about
**counted drop sources** — *"expedition completion, boss kill, chest open"* — and every one of its levers
is a **weight shift** on a draw. §3.5's independence premise is a premise about *drops*. It says nothing
about an operation the player pays for on an item they already own.

| | Drop pity (`ssot-rarity` §3.8) | **Craft pity (this module)** |
|---|---|---|
| Triggered by | a drop event the player did not pay for | a **catalyst spend** the player chose |
| Keys on | rung only | **tier**, per `(instance, affix group)` |
| Mechanism | shifts the rung weights | ⭐ **a counter that, at N, makes the next draw's tier deterministic** |
| Touches the weight table | **yes** | **no — never** |
| Effect on §3.5's independence | would break it | **none.** The weighted draw is untouched; at the threshold it is *not run at all* |

**Concretely:** every failed reroll on a target group increments `enhance_pity_counter`. At the tuned
threshold the next reroll on that group **does not roll a tier** — it is placed at `max_tier` of the
container's window and the counter resets. Independent draws stay independent, because the guaranteed
draw is not a draw.

⭐ **This is I7's own Imprint, corrected.** I7 §3.4 already reached for a deterministic escape hatch and
placed it at the window **floor** (*"deliberately mediocre"*), then rejected a pity counter for needing
durable state. Two things changed: D7 makes the *ceiling* reachable by cost, not the floor; and the
durable state is one integer on a column this module is adding anyway.

**Decider if you disagree: the owner.** The alternative is to leave D7 unimplementable on the tier axis
and tell the player the top tier is a lottery — which D7 rules out by name. `ssot-rarity.md` §3.8 needs a
one-line scope edit (*"drop pity may key on rung only"*), which is module 7's to make.

### 6. The two operations

| Operation | Redraws | Targets | `op_kind` |
|---|---|---|---|
| **Enhance** | nothing — adds a scalar + milestone atoms | the whole item | `enhance` |
| **Temper** | the **value** of one affix, in its own range | exactly one drawn `seq` | `reroll-value` |
| **Reforge** | **identity, tier and value** of a chosen subset | `T ≥ 1` drawn `seq` values, per budget | `reroll-affix` |
| **Imprint** | nothing — **places** a chosen group deterministically | one drawn `seq` | `reroll-affix` |
| **Transfer** | nothing — **moves** `enhance_level` from a donor to a recipient, lossily | one donor + one recipient | `enhance-transfer-out` + `enhance-transfer-in` |
| **Restore** | administrative rollback to a recorded `op_seq` | — | `restore` |

The `op_kind` namespace is **this module's** (`ssot-enhancement.md` §5.3) and modules 14 and 16 draw
from it. Module 16 needs three that do not exist yet:

| `op_kind` | Owner | Note |
|---|---|---|
| `socket-add` · `socket-insert` · `socket-remove` | 16 | reserved in §5.3 |
| ⭐ **`socket-imbue`** | 16 | **new — D24's operation had no `op_kind`.** Added here, because inventing it in module 16 would fork the namespace |

**Enhancement's two components** (I6 §3.3, unchanged): a **+20‰-per-level scalar** applied to the origin
value and never compounded, and **milestone atoms** at +4/+8/+12/+16/+20 drawn from a reserved family
space no affix pool may draw from. Implicits are never scaled. At its cap the whole ladder is worth
roughly **one rarity rung** — enough that a maxed lower rung overlaps the next, never enough to clear it.

### 6a. Transfer — I6's release valve, and §4b is why it ships in v1

**It was missing.** `Restore` was covered; transfer was not, and it is a full I6 mechanism with two
`op_kind`s already reserved (`ssot-enhancement.md:253`), a reason code
(`TransferRoleMismatch`, `:346`) and a worked example (`:464-482`). Adopted, not redesigned:

| Rule | Value | Source |
|---|---|---|
| Recipient gains | `floor(donor_level × TransferRatioMilli / 1000)`, then clamped to its own cap | I6 §7.4, ratio **700‰** |
| Gate | recipient `role` **==** donor `role` (module 3's stable role id, never a display name) **and** item levels within **±8** | I6 §7.4 |
| Donor | drops to `+0`; its milestone rows are **suppressed** (D2 clause 9, never deleted); its scalar recomputes from origin | I6 §7.4 + D2 |
| Cost | one dedicated module-14 material | I6 §7.4 |
| Refusal | `ContentRuleViolated{enhance.transfer-role-mismatch}` — I6's `TransferRoleMismatch` is **not** a member of the closed 33-code list (`AtomRejection.cs`, verified 2026-09-04), so it lands as a namespaced rule id per §2b.1 | this spec |

**Why it is lossy, in I6's own words:** *"a lossless transfer turns `+X` into a portable currency, the
item becomes a disposable carrier, and the decision disappears."* And why it exists: *"without it,
enhancement punishes finding better loot — you keep the worse item because it is the one you paid
for."*

⭐ **§4b raises transfer from a nicety to the module's answer to its own worst number.** With N ≈ 0.19
realms, an investment locked to one item is an investment the player abandons at the next content step.
Transfer at 700‰ is the only mechanism here that lets it follow them — so **the 700‰ ratio is now a
load-bearing tunable**, not the *"pure feel number with no reasoning behind it beyond 'lossy but not punitive'"* I6 §10 Q4 admits it was, and it
lives in `data/tuning/enhancement.v1.json` beside `K`.

**Three things it inherits from this module rather than re-deriving:**

- **One transaction, two ops.** `enhance-transfer-out` on the donor and `enhance-transfer-in` on the
  recipient share one `correlation_id` and commit together. A half-applied transfer duplicates levels.
- **Replay is per instance, and both instances replay.** D2 clause 3 holds on each side independently:
  the donor's transcript ends at `+0`, the recipient's records the granted delta as a **result**, never
  as *"whatever the donor had"*. Recording the recipe would make a later ratio change rewrite an old
  transfer.
- **The ±8 window reads item level, never the player.** D26, same guard as the cost curve.

⚠ **Gated on module 3.** Transfer keys on role equality and I6 §9 #7 names the dependency: hybrid role
ids must be the same ids the pure frames use (OD3), *"or transfer across a hybrid is undefined."*
Module 3 `slot-roles` settles that; until it lands, a transfer whose donor or recipient is on a
`hybrid` frame is refused by name rather than guessed.

### 7. What can never be rerolled

The line is **drawn versus authored**, and it is what makes "an item the generator could never have
dropped" structurally impossible:

| Never | Why |
|---|---|
| Base type | it is `container_id`, the first term of the reproduction contract |
| Implicits and base stats | `effect_container_atom` rows — the fixed core, never in the pool |
| Affix **count** | `PrefixRolls`/`SuffixRolls` are rarity-selected container columns |
| Rarity | lives on the container (`ContainerRow.cs:109`), not the instance |
| Set membership | a container tag |
| Sockets and their inserts | module 16's; a reroll must leave every insert in place and must never reset socket count |

### 8. ⚠ D23 is a **pricing** ruling, and its framing was overstated

D23 reads as *"this resolves a blocking contradiction"* — that a Strain was structurally unbuildable
because low rarities grant zero sockets. **§2f.2 corrected it:** `ssot-sockets.md` §4.1 *already* layered
crafting top-up to `base_type.socket_max`; only the per-rarity **grant table** starts at zero.

So what D23 actually decides is this module's business and nothing more:

> **`socket.add` is available at every rarity. Rarity sets the price, not the possibility.** That is a
> **soft cap** — AGENTS.md's required shape — and it is D7's *"cost, never luck"* applied to a third
> mechanism. `base_type.socket_max` stays a **hard structural cap** (max 4, fixed per role): a legibility
> limit, not a progression ceiling, and it must say so in a comment.

Module 14 owns the price row; module 16 owns the operation. This module owns only the `op_kind` and the
guarantee that the op is logged, idempotent and atomic like every other.

### 9. ⚠ Two shipped defects this module cannot ship over

Both belong to **module 1 `durable-ownership`**, both verified today, both stated here because this
module is the one that dies on them:

| Defect | Verified at | Effect on this module |
|---|---|---|
| **Unequipping deletes the item.** `CollectOrphanInstancesUnlocked` deletes every `effect_instance` with no `effect_binding` row, and runs after every withdraw | `src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:611-622`, called at `:565` and `:583` | The natural workbench flow — take it off, improve it, put it back on — **destroys the item**. No reroll operation can ship first |
| **A content import refuses every instance.** `if (instance.CatalogRevision != current) … StaleInstance` | `RpgStore.AtomInstances.cs:437-441` | D9 removes it. ⚠ **D9's premise was corrected (§2f.2): the bind path never reads the frozen values** — `ResolveBindings` uses `instance.Atoms` as an id list and populates from the **live** catalog. **Sequencing: make frozen values authoritative at bind time FIRST, then drop the revision check** |

### 10. The one read this module takes from module 9

**The dependency on `item-power-reads` was declared in the header and never used in the body.** Named
rather than dropped, because §4b needs exactly one thing from it and inventing a second pricer here is
the failure `spec-item-power-reads.md` was written to prevent.

| Read | Used for | Not used |
|---|---|---|
| **R3 — `PowerScalar` with its ±25% band** (`spec-item-power-reads.md` §R3, `Power/PowerReads.cs:30`) | ⭐ the **before/after** figure on every mutation preview, and the item-versus-item half of §4b's `CraftingHorizonReport` — a perfected item of one rung against a fresh drop of another is a **cross-family** comparison, and E9's vector is the only unit that can express it | — |
| R1 implicit share · R2 granted-action price · R4 aptitude price | — | **not read here.** R1 and R4 are content lints; R2 is module 19's |

Three rules ride along with the read, all module 9's and none re-litigated here:

- **`unpriced` is never `0`** (`CoefficientTable.cs:71-74`). A mutation preview that cannot price the
  result says so; it does not show a `0` delta.
- **Two significant figures with the band** — `≈ 1,300 (±25%)`, never `1,284`. Rule P.
- ⛔ **Never a gate.** A power read may not refuse a mutation, price one, or decide an outcome. It is
  display and reporting only, which is `ContentValidation`'s own standing (`ContentValidation.cs:57-59`).

⚠ **`showPowerOnCard = false` must suppress the preview figure too**, or G3 §10 Q7's reversal is only
half a reversal. One tunable, two surfaces.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Enhance"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Reroll"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Replay"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~InstanceOp"
.\scripts\guard-dal.ps1
python scripts\audit-magic-numbers.py --targets M1   # odds, scalar, escalation all in tuning
python scripts\audit-overflow.py                     # long on every scaled magnitude
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/Instantiator.cs       SHIPPED - DrawBudget gains `count` and
                                                        `excludeGroups`; Draw's signature is unchanged
src/FusionRpg.Core/Items/MutationOp.cs                 new - the op record, op_kind enum, result deltas
src/FusionRpg.Core/Items/MutationReplay.cs             new - transcript replay + state_hash compare
src/FusionRpg.Core/Items/EnhancePolicy.cs              new - scalar, bands, odds, caps. Pure
src/FusionRpg.Core/Items/RerollPolicy.cs               new - temper/reforge/imprint, per-budget anchors
src/FusionRpg.Core/Items/CraftPityCounter.cs           new - the guaranteed-tier counter (section 5)
src/FusionRpg.Core/Items/TransferPolicy.cs             new - I6 section 7.4, role gate + 700 permille (6a)
src/FusionRpg.Core/Items/CraftingHorizonReport.cs      new - "N realms" from PowerTuning + enhance_cap (4b)
src/FusionRpg.Data/Sqlite/RpgStore.InstanceOps.cs      new - effect_instance_op DDL + append (guard-dal)
data/tuning/enhancement.v1.json                        new - scalar, odds, pity threshold, cost curve,
                                                        escalation cap, the asymptote's K (4a), and
                                                        TransferRatioMilli (6a). THE soft cap lives here
data/tuning/item-rarity.v1.json                        MODULE 7's - enhance_cap per rung, read here
tests/FusionRpg.Core.Tests/Items/MutationReplayTests.cs   new
tests/FusionRpg.Core.Tests/Items/EnhancePolicyTests.cs    new
tests/FusionRpg.Core.Tests/Items/RerollPolicyTests.cs     new
```

## Code style

```csharp
// Transcript replay, D2 clause 4: apply the RECORDED delta, never re-run the formula. This is what
// makes a rebalance structurally unable to reach backwards into an item a player already owns - a
// re-simulating replay would silently un-succeed an attempt they paid for. The rules table is not
// even reachable from here, which is the enforcement.
static InstanceHead Replay(InstanceHead origin, IReadOnlyList<MutationOp> ops)
{
    var head = origin;
    foreach (var op in ops)                       // dense, gapless, in order (D2 clause 7)
        head = ApplyRecordedDeltas(head, op.ResultJson);
    return head;
}

// Craft pity: at the threshold the tier is PLACED, not rolled. ssot-rarity section 3.8 forbids a
// tier pity that shifts DRAW WEIGHTS, because section 3.5's overlap invariant is measured on
// independent draws (2e5 rolls, seed 20260822). This touches no weight - at the threshold the
// weighted draw is not run at all - so the measurement stands. See spec section 5.
static int TierFor(RerollContext ctx, AtomRandom rng, EnhancementTuning t) =>
    ctx.PityCounter >= t.CraftPityThreshold
        ? ctx.Container.MaxTier!.Value                    // guaranteed, counter resets
        : rng.PickTier(ctx.Container.MinTier!.Value, ctx.Container.MaxTier!.Value);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `replay_of_origin_plus_ops_equals_the_head_for_every_mutated_instance` | D2 clause 3, over a **whole fixture database**, not a spot check |
| `a_rebalance_of_the_odds_table_changes_no_owned_item` | clause 4, the property this design most deliberately buys |
| `replay_never_reads_the_rules_table` | enforced by the type, then asserted |
| `a_replayed_correlation_returns_the_recorded_result` | clause 8, copied from `RpgStore.Souls.cs:189-213` |
| `a_reused_correlation_with_different_parameters_is_refused` | not silently applied |
| `op_seq_is_dense_and_an_out_of_order_arrival_is_OpSequenceGap` | clause 7 |
| `a_head_log_mismatch_raises_ReplayDivergence_loudly` | clause 12 — a defect, never a warning |
| `seq_is_never_renumbered_and_an_identity_change_suppresses_then_appends` | clause 9 |
| `an_OnApply_affix_is_enhanced_by_rewriting_min_max_inside_values_json` | clause 14 — **no `overrides_json`**, pinned against `Instantiator.cs:306-311` |
| **`anchoring_is_computed_per_budget_not_from_pool_rolls`** | §2 — the platform correction, asserted so the stale algebra cannot come back |
| `a_reforge_preserves_prefix_rolls_and_suffix_rolls_exactly` | the post-op invariant, per budget |
| `rerolling_a_mixed_affix_redraws_into_both_budgets_or_is_refused` | §2's `Mixed` hazard, decided rather than discovered |
| `a_partial_redraw_seeds_the_exclusion_set_with_retained_groups` | one-per-group survives a partial reroll |
| `a_rerolled_item_always_validates_as_freshly_instantiated` | the "impossible item" failure, structurally |
| `a_reroll_never_touches_a_socket_or_an_insert` | module 16's boundary |
| **`the_pity_counter_guarantees_max_tier_at_the_threshold`** | **D7** — the top tier is reachable by cost |
| **`craft_pity_shifts_no_draw_weight`** | §5's resolution — the guarantee **replaces** the draw, it does not bias it, so §3.5's measurement stands |
| `pity_resets_on_a_guaranteed_draw_and_persists_across_sessions` | the `rpg_summon_pity` shape, reused |
| `there_is_no_destroy_outcome_in_the_enum_or_the_reason_codes` | asserted directly — a code nothing emits is a lie in a table |
| **`no_enhancement_gain_is_a_hard_stop`** | ⭐ replaces `no_enhancement_cap_is_a_hard_stop`. For every rung and every `n`, `gain(n+1) > gain(n)` — the asymptote is never reached, so no level is refused; `ilvl_cap` still has a floor and no ceiling |
| `enhancement_gain_stays_below_its_rungs_asymptote_at_every_n` | §4a — the ladder cannot invert, from module 7's seeded `enhance_cap` column, not from a local constant |
| `the_crafting_horizon_is_computed_from_power_tuning_not_authored` | §4b — `CraftingHorizonReport` reproduces N = 0.19 at v1's reach and 2.01 at `Θc = 123`, and moves when `bMilli` or `enhance_cap` move |
| `a_transfer_is_one_transaction_with_one_correlation_id` | §6a — a forced failure mid-transfer leaves the donor at its original level and the recipient unchanged |
| `a_transfer_records_the_granted_delta_not_the_donors_level` | D2 clause 4 through §6a — a later `TransferRatioMilli` change rewrites no completed transfer |
| `a_transfer_across_unequal_roles_or_outside_the_ilvl_window_is_refused_by_name` | `ContentRuleViolated{enhance.transfer-role-mismatch}`; no new member of the closed code list |
| `a_donor_drops_to_plus_zero_with_milestones_suppressed_never_deleted` | D2 clause 9 holds on the donor side too |
| `a_transfer_touching_a_hybrid_frame_is_refused_until_module_3_lands` | I6 §9.7's undefined case, decided rather than discovered |
| `the_mutation_preview_reads_module_9_R3_and_nothing_else` | §10 — no pricer, no vector and no cost function is declared under `Items/` |
| `an_unpriced_preview_shows_unpriced_not_zero` | §10, `CoefficientTable.cs:71-74` |
| `the_cost_and_odds_curves_are_read_from_data_tuning` | AGENTS.md's balance-surface rule, mechanically |
| `no_cost_or_odds_input_reads_a_player_property` | **D26**, same guard shape as module 14's |
| `mutation_seq_is_capped_at_4096_and_the_comment_says_it_is_structural` | the one legal ceiling, and why |
| `every_scaled_magnitude_is_long_and_overflow_throws` | `+20` on an ilvl-500 t5 affix is not an `int` |

## Boundaries

**Always:** adopt D2 §9's fifteen clauses verbatim; record the result, never the recipe; append to
`effect_instance_op` on every mutation; carry a `correlation_id` with `UNIQUE(instance_id, correlation_id)`;
derive randomness via `SeededRng.DeriveStream(op_seed, "item.{op_kind}")`
(`src/FusionRpg.Core/Battle/SeededRng.cs:26`) — one named stream per op kind, recorded even when unused;
commit op row, material debit and head rewrite in one transaction; keep every odds, scalar and cost number
in `data/tuning/enhancement.v1.json`.

**Ask first:** ✅ ~~scoping §3.8's pity rule to drop pity~~ — **RULED as D31, and it lands *before* D7**;
module 7 owns the edit as **E1**. Adding an `op_kind`; a player-facing
un-enhance; whether enhancement extends to charms and inserts (scoped here to equipment); **the
transfer ratio (700‰)** — §4b makes it load-bearing rather than a feel number; **whether N ≈ 0.19 is
acceptable at v1 depth**, given that this module cannot raise it without inverting the ladder (§4b).

**Never:** delete `enhance_cap`'s consumer — module 7's SC7 rule makes an unconsumed key reject, and
this module is the reader (§4a). Never make the enhancement track steeper to raise N — it re-inverts
the rarity ladder (§4b). Never price a mutation, gate one, or show a `0` where module 9 returns
`unpriced` (§10). Never record a transfer's *recipe* — the granted delta is the result (§6a). Never
re-simulate replay — a nerf must not un-succeed a paid attempt. Never add
`effect_instance_atom.overrides_json` (D2 refused it, and the premise it rested on is refuted by a passing
test). Never a destroy outcome. Never a hard cap on `+X` — the risk and cost curves are the cap and they
live in tuning. Never a cost or odds term reading the player's `Θ`, level or a per-day counter (**D26**).
Never touch a socket, an insert or `item_socket` — module 16 owns them and D2 clause 13 exempts them from
clauses 3 and 4. Never renumber `seq`. Never delete an op row.

## Success criteria

- [ ] D2 §9's fifteen clauses are implemented and each has a named test.
- [ ] `replay(origin_values_json, ops) == head` byte-exact for **every** mutated instance in a fixture
      database, with no catalog involved.
- [ ] Anchoring, targeting and the post-op invariant are all expressed **per budget** — no `pool_rolls`
      anywhere in the module, proven by grep and by test.
- [ ] **D7 holds: `max_tier` is reachable by spending, on every affix group, with no luck floor** —
      proven by the pity test, and the guarantee shifts no draw weight.
- [ ] `ssot-rarity.md` §3.8 carries the drop-pity scope edit, or the owner has ruled otherwise.
- [ ] No hard cap on `+X`; the cost and risk curves live in `data/tuning/enhancement.v1.json` and
      `audit-magic-numbers.py` reports no M1 target in `EnhancePolicy` or `RerollPolicy`.
- [ ] ⛔ **`enhance_cap` has a live consumer here** and is read as a **‰ gain asymptote**, matching
      `spec-rarity-bands.md` row for row; the two previously-incompatible tests are replaced by the pair
      named in §4a, and neither spec can move without the other going red.
- [ ] **N is computed, not asserted**: `CraftingHorizonReport` reads `PowerTuning` and the seeded
      `enhance_cap` column and reproduces **N ≈ 0.19 at v1's shipped reach**, with `Θc ≈ 123` as the
      first depth reaching 2. The figure is in the report, not only in this document.
- [ ] **Transfer ships** — both `op_kind`s, one transaction, one `correlation_id`, the role + ±8 gate,
      the donor's milestones suppressed rather than deleted, and `TransferRatioMilli` in tuning.
- [ ] The module-9 dependency is **used**: exactly one read (R3), named in §10, with no pricer, vector
      or cost function declared anywhere under `Items/`.
- [ ] `socket-imbue` exists in the `op_kind` namespace before module 16 needs it.
- [ ] Module 1's two defects (orphan sweep, revision-equality) are closed before the first operation ships.
