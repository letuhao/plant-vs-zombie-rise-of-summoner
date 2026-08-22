# Lane I8 SSOT — the prefix / suffix affix system

**Status:** Lane I8 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Read this session, in the contract's §5 order: [item-ideal.md](../item-ideal.md),
[enrichment-contract.md](enrichment-contract.md), [definitions.md](../effect-atom/definitions.md),
[spec-container-schema.md](../effect-atom/spec-container-schema.md),
[spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md),
[atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md),
[atom-family-library.md](../effect-atom/atom-family-library.md).

---

## 1. Scope

### This lane owns

- The **prefix / suffix split** — the principle, and which of the ~71 authored families lands on which side.
- The **role × family legality** method and matrix — which affix families may roll on which equip role,
  filtered by frame and side.
- **Tier bands** — the banding method, and the real per-family numbers with units.
- **Item-level gating** of tier access.
- **Pool weighting** — how a weight is assigned to a pool row so good affixes are rare but findable.
- **Item naming from affixes** — the grammar, the word tables, and the plant-frame variant.
- Whether the one-per-group rule needs a second layer, and what that layer is.
- Hybrid affixes and crafted-only affixes, as far as the affix pool is concerned.

### This lane does NOT own

| Thing | Lane |
|---|---|
| Implicits and base stats — those are **fixed**, mine are **rolled** | **I3** |
| How many affixes a rarity grants, and which tier window it opens | **I1** (I own what the tiers *are*) |
| The equip-role list and the frame vocabulary | **I2** (I consume it) |
| Rerolling, and any post-drop change to a rolled value | **I7**, under **I6**'s mutation model |
| Sockets and inserts | **I4** |
| Set bonuses | **I5** |
| Turning a loot event into an instance, and stamping `item_level` | **I12** |
| The equip gate | **I11** |
| What crafting costs | **I9** |
| The `stat.derived` quarantine lift | **E12**, in the effect-atom program |

---

## 2. The model

An affix is one **atom** drawn from a container's weighted pool at instantiate time and frozen there
([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)). Everything I8 designs is
*which atoms are in which pool, at what weight, and what the numbers on them are.* No new runtime
mechanism, and one new pair of columns.

Three layers, in the order the machine reads them.

**Layer 1 — affix class.** Every atom is a **prefix** or a **suffix**, and the class is **derived from
`kind_id`, never authored**:

> An atom is a **prefix** if its kind is `stat.modify` or `stat.derived`. Every other kind is a
> **suffix**.

That is not an aesthetic split. [definitions.md](../effect-atom/definitions.md) §14.2 makes
`stat.modify` and `stat.derived` **permanent modifiers that declare no trigger at all** — the runtime
owns apply and revert, and authoring a trigger on either is `TriggerNotAllowed`. The other ten kinds all
carry one of the five authorable triggers. So the split is exactly:

| Class | Is | Costs at runtime | Families |
|---|---|---|---|
| **Prefix** | a permanent modifier — composes once at bind, then it is a number | one compose pass per bind | **30** |
| **Suffix** | a behaviour — an atom on the actor's effect list that the event loop walks | **per-event work, forever** | **40** |

The count comes out at 30 + 40 = 70, against the library's stated 71 — see §4.2 for the missing one.

**Layer 2 — legality.** A family may only roll on a role where the **role × affix-group matrix** gives it
a non-zero weight, and only where the item's **frame** and the family's **side** allow it. That is what
stops every slot being interchangeable, and it is the single biggest content artefact in the item system.

**Layer 3 — magnitude.** Tier carries strength, and tier bands are authored **per channel family** from a
share of a reference base, never copied across. `+10 hp` and `+10 fire power` differ by roughly an order
of magnitude (SC4), and the banding method exists to make that impossible to get wrong by accident.

Sitting over all three: **one atom per group** (`group` defaults to `(family_id, variant)`) and, new
here, **a class quota** — at most 3 prefixes and at most 3 suffixes on one item.

---

## 3. Options considered, and the recommendation

### 3.1 The prefix / suffix split — four candidates

| Option | Principle | Why it fails, or holds |
|---|---|---|
| **A — offense / defense** | prefixes attack, suffixes defend | Fails on ~30 of 70 families. `regeneration` is defensive and triggered; `terraforming` is neither; `sunbloom` is economy. Ends as two bags with a third bag stapled on |
| **B — additive / multiplicative** | `Flat` prefixes, `Increased`/`More` suffixes | Fails by construction. `vitality`/`fortitude`/`bulwark` are the same concept at three ops, so one affix identity would straddle the split, and the one-per-group rule would not stop `+40 hp` prefix + `+8% hp` suffix reading as a duplicate |
| **C — magnitude / conditional** | prefixes are unconditional numbers, suffixes are things that happen | Holds on every family. **And it is already a column** |
| **D — hand-tagged** | an `affix_class` column an author sets | Holds by fiat, drifts by Tuesday. Two authors will disagree about `regeneration` and nothing will catch it |

**Recommended: C, derived from `kind_id`.**

Three reasons, in order of how much they matter.

1. **It is machine-derivable, so it cannot drift.** `kind_id` is a closed set of 12 that only grows by
   reviewed code change (SC2). The class function is a 12-row lookup in code, not a data column, so
   there is nothing to author wrong, nothing to migrate, and nothing to lint.

2. **It holds for all 70 families with no exceptions and no judgement calls.** Every `stat.modify` and
   `stat.derived` family is a prefix; every `resource.delta`, `resource.economy`, `status.apply`,
   `status.clear`, `shield.grant`, `spawn.entity`, `board.action`, `grid.spawn`, `grid.clear`, and
   `box.set` family is a suffix. No family sits on the line.

3. **The cap becomes a frame-time budget, not a taste preference.** This is the argument that decides it.
   A prefix is composed once at bind and is thereafter a number in the modifier bag. A suffix is an atom
   the event loop walks on every matching event, for every actor carrying it. The 2026-08 perf audit
   concluded that combat lag in this game is **per-hit work on the Unity main thread**
   ([docs/runbook/perf-probe-plan.md](../../runbook/perf-probe-plan.md)). With ~15 equip slots per actor,
   an uncapped affix system that happened to roll suffix-heavy would put **90 triggered atoms on one
   actor's effect list**. Capping suffixes at 3 per item caps it at 45 — still a lot, and §9 asks I1 and
   I2 for a per-actor ceiling on top. A split whose cap is measured in milliseconds is a better split
   than one whose cap is measured in feel.

**What C costs, stated plainly:** it puts elemental *resistance* on the prefix side, where Path of Exile
puts it on suffixes. Recalled, unverified: PoE's resistances are suffixes and its life/damage rolls are
prefixes, which is a near-inversion of this rule for that one group. I am taking the divergence. PoE's
split is historical, ours is mechanical, and a player reading "prefixes are what the item *is*, suffixes
are what it *does*" will not miss the difference.

### 3.2 How many, and how it meets rarity

**Recommended: at most 3 prefixes and at most 3 suffixes, 6 affixes maximum.** That is the PoE shape
(recalled: 3 + 3 on a rare, unverified) and it is the right size here for the runtime reason above.

The split of a rarity's affix count into the two classes is **I1's to allocate**; I define only the shape
and the ceiling:

```text
prefix_rolls ≤ 3
suffix_rolls ≤ 3
prefix_rolls + suffix_rolls = pool_rolls
```

A suggested allocation for I1 to accept, amend, or reject, given the ideal's §6.2 ladder:

| Rarity | `pool_rolls` | Suggested split | Note |
|---|---|---|---|
| Normal | 0 | 0 + 0 | base type and implicit only |
| Magic | 1–2 | 1 + 0, 0 + 1, or 1 + 1 | the classic "one prefix, one suffix" |
| Rare | 3–6 | 2+1 … 3+3 | at least one of each — see below |
| Unique | fixed core | n/a | fixed atoms, no pool draw; the class quota does not apply |

**One rule I do assert, because it is the anti-stat-stick guard:** any item with `pool_rolls ≥ 3` must
have **`prefix_rolls ≥ 1` and `suffix_rolls ≥ 1`**. A six-prefix item is a stat stick with no identity;
a six-suffix item is a proc storm with no numbers. Forcing one of each is what makes a rare item read as
an item rather than a spreadsheet row.

### 3.3 Where the class quota lives — three ways to build it

| Option | Shape | Verdict |
|---|---|---|
| **A — a container per split** | one `effect_container` per `(base type, prefix count, suffix count)` | **Reject.** 16 containers per base type; ~100 base types is 1 600 containers, and a weight change touches all of them |
| **B — `affix_class` column on `effect_container_pool`** | author the class on every pool row | **Reject.** It is derivable from the atom, so a stored copy is a second source of truth that can disagree with the first |
| **C — two roll counts on the container, class derived from the atom** | `prefix_rolls` / `suffix_rolls` nullable on `effect_container`; the draw runs twice over two derived sub-pools | **Recommended** |

**C is two nullable INT columns and no new tables.** When both are NULL, `pool_rolls` behaves exactly as
it does today — traits, skills, species passives, patrons, and world buffs are untouched, which matters
because [spec-container-schema.md](../effect-atom/spec-container-schema.md) publishes this contract at
**Checkpoint B** and the action program A1 consumes it. Adding a column is *ask-first* under E5's
boundaries; this is that ask, and §9 raises it formally.

### 3.4 Tier banding — three methods

| Option | Method | Verdict |
|---|---|---|
| **A — hand-author 5 numbers per family** | a designer types 350 min/max pairs | **Reject.** This is exactly how a band gets copied across channel families and lands 10× wrong. It is also 700 numbers nobody will re-check |
| **B — a global multiplier ladder** | `m_t = m_1 × r^(t-1)`, one `m_1` per family | Half right. It fixes the ladder but not the anchor, so `m_1` is still a free-hand number per family and the cross-family units trap survives |
| **C — share of a reference base, then a ladder** | `m_1 = share × B_family(L_ref)`, then `m_t = m_1 × r^(t-1)` | **Recommended** |

**C** anchors every family to something real. `B_family` is the game's own base curve for that channel —
`BattleRuleset.BaseHp(level) = 80 + 30 * level`, `BaseAtk(level) = 12 + 4 * level`,
`BaseCritRate(level) = 10 * level` (`src/FusionRpg.Core/Battle/BattleModels.cs:60`, `:61`, `:73`) — and
`share` is one authored number per family meaning *"a tier-1 roll of this affix is worth this fraction of
what an actor already has."* The units trap dies because `share` is dimensionless and `B` carries the
unit.

Constants, committed:

```text
L_ref = 20                       calibration level
r     = 1.75                     tier-to-tier midpoint ratio  (t1 → t5 spans 9.4x)
lo_t  = round(0.67 × m_t)        band floor
hi_t  = round(1.33 × m_t)        band ceiling
```

`hi/lo = 1.985 > r = 1.75`, so **adjacent tier bands overlap by construction** — a maximum-roll tier-3
beats a minimum-roll tier-4. That is the tier-level half of **OD4**, and it is a consequence of two
constants rather than an assertion. §9 tells I1 how much of OD4 it does *not* cover.

### 3.5 Item-level gating — open window or sliding window

| Option | Behaviour | Verdict |
|---|---|---|
| **A — open window** | ilvl unlocks a tier; lower tiers never leave the pool | **Reject.** At ilvl 60 the pool offers t1–t5 and four fifths of it is junk. This is failure mode 1 — *an affix pool so wide every item is mediocre* — and it is the most common way this system rots |
| **B — sliding window** | the pool offers the highest unlocked tier and the two below it | **Recommended** |
| **C — exact band** | only the highest unlocked tier | **Reject.** Kills the within-item variance that makes two drops of the same base type different |

**Can a low-level item roll a top tier? No — never.** Tier availability is a pure function of
`item_level`, with no lottery, no lucky roll, and no bad-luck-protection exception. The alternative turns
every drop into a slot-machine pull and makes the level curve decorative. What replaces the excitement is
the ±33% within-band roll and the adjacent-tier overlap: a maximum-roll item from your own bracket is
genuinely better than a poor drop from the next one, and that is a satisfaction the player can reason
about instead of pray for.

### 3.6 The role × family artefact — cells or groups

| Option | Size | Verdict |
|---|---|---|
| **A — 70 families × 13 roles as booleans** | 910 hand-set cells | **Reject.** Nobody audits 910 booleans, and it gives legality without weight, so a second artefact of the same size follows |
| **B — 15 affix groups × 13 roles as weights** | **195 cells**, `0` meaning illegal | **Recommended** |

**B** collapses legality and weighting into one artefact: weight `0` is illegal, weight `n > 0` is both
legal and the role's appetite for that flavour. Groups are how a designer actually thinks — *"a helm
should be mostly life and armour, a little crit, no economy"* is one row of six numbers.

---

## 4. The design, committed

### 4.1 The 15 affix groups

Every one of the 70 authored families lands in exactly one group. The group is what the role matrix
weights; the family is what the one-per-group rule keys on.

**Prefix groups — 30 families, all `stat.modify` or `stat.derived`, all permanent, all triggerless.**

| Group | Families | Kind | Live today? |
|---|---|---|---|
| `g.life` | `vitality` · `fortitude` · `bulwark` · `mending` | `stat.modify` | ✅ lawn |
| `g.attack` | `might` · `ferocity` · `savagery` | `stat.modify` | ✅ lawn |
| `g.tempo` | `quickening` · `flourishing` · `swiftness` | `stat.modify` | ⛔ pending the channel-extension spec |
| `g.armour` | `warding` · `resilience` · `plating` · `carapace` | `stat.modify` | partial — see §4.7 |
| `g.ward` | `elemental_defense` · `stoicism` · `padding` · `stalwart` · `immunity` · `susceptibility` | `stat.derived` | ⛔ quarantined |
| `g.elem-power` | `elemental_power` · `affliction` | `stat.derived` | ⛔ quarantined |
| `g.precision` | `precision` · `keen_edge` · `cruelty` | `stat.derived` | ⛔ quarantined |
| `g.evade` | `evasion` | `stat.derived` | ⛔ quarantined |
| `g.shield-stat` | `shield_capacity` · `shield_toughness` · `shield_regen` · `shield_pen` | `stat.derived` | ⛔ quarantined |

**Suffix groups — 40 families, everything else, all triggered.**

| Group | Families | Kinds | Live today? |
|---|---|---|---|
| `g.on-hit` | `searing_strike` · `lifesteal` · `retribution` · `volley` | `resource.delta`, `spawn.entity` | ✅ lawn |
| `g.on-death` | `deathblast` · `martyrdom` · `summoner` · `gardener` | `resource.delta`, `spawn.entity` | ✅ lawn |
| `g.sustain` | `regeneration` · `cleansing` · `warded` | `resource.delta`, `status.clear`, `shield.grant` | ✅ lawn |
| `g.affliction` | the 20 `status.apply` families | `status.apply` | 13 functional, 7 pending payloads |
| `g.board` | `cherry_bloom` · `dooming` · `firelining` · `flash_freeze` · `gravemaking` · `gravedigging` · `terraforming` | `board.action`, `grid.*`, `box.set` | ✅ lawn |
| `g.economy` | `sunbloom` · `midas` | `resource.economy` | ✅ lawn |

### 4.2 The count discrepancy, reported not papered over

[atom-family-library.md](../effect-atom/atom-family-library.md) §6 states **71 authored families**, and
its §3.4 heading says **21 `status.apply` families**. Grouping every row of §3.1–3.5 into exactly one
group yields **70**, because §3.4's table lists **20** family rows, not 21. The missing one is
`charm_pulse`, which the same document calls *"a def error to correct, not a branch to write"* —
[atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §5 confirms no vanilla method exists for it.
So the 21st **status** is real and the 21st **family** is not.

I8 authors against **70**. The completeness lint in §6.3 is what caught this and is what will catch the
next one.

### 4.3 The role × affix-group matrix

Roles are I2's. This matrix is written against the 12 published in [item-ideal.md](../item-ideal.md)
§5.1 plus the commander-only `standard` from §5.6. OD2 says ~15 slots per frame, so I2 will land more
roles than this; the method takes any role list, and §9 asks I2 for the final ids.

Weights are relative pull within a role, `0` = illegal. `jewel-minor` A and B share one profile.

| Role | life | attack | tempo | armour | ward | elem-pw | precis | evade | shield | on-hit | on-death | sustain | afflict | board | econ |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| `head-protective` | 5 | 0 | 0 | 4 | 3 | 0 | 1 | 1 | 2 | 0 | 2 | 1 | 0 | 0 | 0 |
| `sense-utility` | 1 | 0 | 0 | 0 | 1 | 0 | **5** | 4 | 0 | 2 | 0 | 0 | 2 | 0 | 0 |
| `core-protective` | **6** | 0 | 0 | 5 | 4 | 0 | 0 | 1 | 3 | 0 | 2 | 2 | 0 | 0 | 0 |
| `mantle-utility` | 2 | 0 | 0 | 2 | **5** | 0 | 0 | 3 | 4 | 0 | 0 | 2 | 0 | 0 | 0 |
| `manipulator-offense` | 0 | 4 | **5** | 0 | 0 | 3 | 3 | 0 | 0 | 5 | 0 | 0 | 3 | 0 | 0 |
| `footing` | 2 | 0 | 3 | 1 | 2 | 0 | 0 | **4** | 0 | 0 | 0 | 3 | 0 | 2 | 0 |
| `girdle-resource` | 3 | 0 | 2 | 0 | 0 | 0 | 0 | 0 | 2 | 0 | 0 | **5** | 0 | 0 | **5** |
| `jewel-major` | 3 | 3 | 1 | 1 | 3 | 4 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 1 | 2 |
| `jewel-minor` | 2 | 2 | 1 | 1 | 2 | 2 | 2 | 2 | 1 | 1 | 1 | 1 | 1 | 1 | 1 |
| `armament-primary` | 0 | **6** | 4 | 0 | 0 | 5 | 3 | 0 | 0 | **6** | 2 | 0 | 4 | 2 | 0 |
| `armament-secondary` | 2 | 2 | 0 | 3 | 3 | 2 | 0 | 2 | **5** | 2 | 0 | 1 | 2 | 2 | 0 |
| `standard` (commander) | 2 | 2 | 2 | **4** | 2 | 2 | 2 | 0 | 2 | 0 | 0 | 2 | 0 | 3 | 4 |

Read it in rows, not cells. `core-protective` is the biggest life budget on the body and has no offence
at all. `armament-primary` is the only role that can roll a top-weight attack *and* a top-weight on-hit
rider, which is why the weapon is the identity slot. `jewel-major` and `jewel-minor` are the only roles
legal for **everything** — that is what makes rings the flexible slot, and their flat low weights are why
they are also the weakest per-slot budget ([item-ideal.md](../item-ideal.md) §5.5).

`standard` is the odd row, and deliberately so — see §4.7.

### 4.4 The three filters that run after the matrix

The matrix gives `(role, group) → weight`. Three filters then remove individual families from that group
before any pool row is emitted.

| Filter | Rule | Source |
|---|---|---|
| **Frame** | A family declares which frames its role serves. `swiftness` never rolls on a plant item; `flourishing` and `quickening` never roll on a humanoid one | [item-ideal.md](../item-ideal.md) §5.4 — *"one extra column, and it is what stops `+move speed` rolling on a turnip"* |
| **Side** | `plating` and `carapace` write `arm1`/`arm2`, Unity fields that exist only on zombies | [atom-family-library.md](../effect-atom/atom-family-library.md) §4.1 |
| **Runtime** | A family whose kind has runtime support `None` for the item's target runtime may not enter an item pool **at all** | §4.9 |

**A group can be emptied by a filter, and that must not pass silently.** `g.armour` on a plant-frame
`core-protective` loses `plating` and `carapace` to the side filter and `warding`/`resilience` to §4.7 —
leaving weight 5 pointing at nothing. The generator must **redistribute the weight across the role's
remaining groups, proportionally**, and emit an authoring lint naming the `(role, frame, group)` triple.
Silently dropping the weight would shrink the plant helm pool by a third with no signal at all.

### 4.5 Tier bands — the method and the real numbers

```text
m_1  = round_legible( share × B_family(20) )
m_t  = round_legible( m_1 × 1.75^(t-1) )        t = 1..5
lo_t = round_legible( 0.67 × m_t )
hi_t = round_legible( 1.33 × m_t )
```

`round_legible` snaps to a human number (1 / 2 / 5 significance) without breaking the overlap invariant
`hi_t ≥ lo_(t+1)`; a snap that breaks it is rejected by the lint in §6.3.

**All numbers below are illustrative, not balanced.** They demonstrate the method and the units; the real
values need the power model (SC9) or a simulation sweep before anyone should trust them for balance.

#### `vitality` — `stat.modify`, `maxHp` Flat, **game units (hit points)**

`B_hp(20) = 680` (`src/FusionRpg.Core/Battle/BattleModels.cs:60`). `share = 3%` → `m_1 = 20`.

| Tier | Midpoint | Band | Reads as |
|---|--:|--:|---|
| 1 | 20 | **14 – 26** hp | `+18 hp` |
| 2 | 35 | **24 – 46** hp | `+31 hp` |
| 3 | 60 | **40 – 80** hp | `+67 hp` |
| 4 | 105 | **70 – 140** hp | `+118 hp` |
| 5 | 185 | **125 – 245** hp | `+201 hp` |

Sanity check against the curve it is anchored to: `B_hp(60) = 1880`, so one maximum-roll tier-5 is 13% of
an end-game actor's base HP, and a full set of gear carrying it on ~8 legal roles roughly triples base
HP. That is the ARPG shape.

#### `might` — `stat.modify`, `atk` Flat, **game units (attack points)**

`B_atk(20) = 92` (`BattleModels.cs:61`). `share = 4.5%` → `m_1 = 4`.

| Tier | Midpoint | Band |
|---|--:|--:|
| 1 | 4 | **3 – 5** atk |
| 2 | 7 | **5 – 9** atk |
| 3 | 12 | **8 – 16** atk |
| 4 | 21 | **14 – 28** atk |
| 5 | 37 | **25 – 49** atk |

**This family exposes a real limit of the method, and it is worth naming rather than hiding.** The base
attack curve is small, so `m_1 = 4` and the tier-1 band is three integers wide. `hi_1 = 5` and `lo_2 = 5`
are *equal*, not overlapping — the overlap invariant holds only as a tie. Integer arithmetic (SC4: no
floats in content) sets a floor on band resolution at roughly `m_1 ≥ 5`. **The fix is not to invent
decimals; it is to let the `Increased` sibling carry the fine grain** — `ferocity` below has 20× the
resolution of `might` at every tier and covers exactly the same fantasy. The lint in §6.3 flags any family
with `m_1 < 5`; `might` trips it knowingly.

#### `ferocity` — `stat.modify`, `atk` Increased, **integer per-mille**

A ratio family has no base curve; its reference is the identity, 1000‰. `share = 1.6%` → `m_1 = 16`,
legibilised to 15.

| Tier | Midpoint | Band | Reads as |
|---|--:|--:|---|
| 1 | 15‰ | **10 – 20‰** | `+1.4% attack` |
| 2 | 26‰ | **17 – 35‰** | |
| 3 | 46‰ | **31 – 61‰** | `+4.7% attack` |
| 4 | 80‰ | **54 – 106‰** | |
| 5 | 140‰ | **94 – 186‰** | `+15.1% attack` |

#### `elemental_power` — `stat.derived`, `combat.power.{element}`, **resolver points**

**This is the family the units rule exists for.** The sigmoid scale is 100.0 for accuracy, crit rate, and
crit damage alike (`src/FusionRpg.Core/Stats/Derived/CombatPolicies.cs:10-12`), so 100 points is one
sigmoid unit. Calibration from [definitions.md](../effect-atom/definitions.md) §2: `critical-hunter`
grants **+150 points**. An affix must not outclass a named trait, so tier 5 tops out near a third of it.

| Tier | Midpoint | Band |
|---|--:|--:|
| 1 | 6 | **4 – 8** points |
| 2 | 10 | **7 – 13** points |
| 3 | 18 | **12 – 24** points |
| 4 | 32 | **21 – 43** points |
| 5 | 56 | **38 – 74** points |

> **Put the two side by side and the trap is obvious.** A tier-5 `vitality` maximum roll is **245**. A
> tier-5 `elemental_power` maximum roll is **74**. Same tier column, same value-spec shape, same
> `{min, max, onInstantiate}` encoding — and copying either band onto the other family would be wrong by
> 3.3× in one direction and produce an affix worth an entire sigmoid unit in the other. The band is
> **never** a property of the tier. It is a property of the family.

Seven element variants share one band table. `atom.elemental-power.fire.t3` and
`atom.elemental-power.ice.t3` carry identical `{12, 24}` value specs and differ only in `variant` and the
`channel` param — which is precisely the generation rule from
[atom-family-library.md](../effect-atom/atom-family-library.md) §2.

#### `searing_strike` — `resource.delta`, element damage `OnDamageDealt`, **game units of damage**

Anchored to the library's own worked example: tier 3 is `{100, 200, onApply}`
([atom-family-library.md](../effect-atom/atom-family-library.md) §2a). The ladder is fitted to that point
rather than to a base curve, because the reference for a damage rider is *the hit it rides on*.

| Tier | Midpoint | Band | Chance |
|---|--:|--:|--:|
| 1 | 50 | **35 – 65** | 200‰ |
| 2 | 85 | **55 – 115** | 250‰ |
| 3 | **150** | **100 – 200** | 300‰ |
| 4 | 260 | **175 – 345** | 350‰ |
| 5 | 460 | **310 – 610** | 400‰ |

Two things this family shows that the prefixes do not.

1. **Its value spec is `onApply`, not `onInstantiate`.** The roll happens on the hit, not at the drop, so
   the *item* freezes nothing — the tooltip shows the band, and two copies of the same tier are
   identical. Suffix families in `g.on-hit` and `g.affliction` are mostly like this. It matters for I7:
   **rerolling an `onApply` affix changes its tier, never its number**, because there is no number.
2. **It has two ladders.** Magnitude runs at `r = 1.75`; chance runs far flatter, 200‰ → 400‰ over five
   tiers. That is deliberate and is the rule for every triggered family: *magnitude scales, frequency
   barely does.* A tier-5 rider that fires 800‰ of the time is not a stronger affix, it is a different
   and much worse game.

#### `freezing` — `status.apply`, **‰ chance + integer ms duration**

The clearest case of the two-ladder rule, because control compounds and damage does not.

| Tier | Chance | Duration | Ratio applied |
|---|--:|--:|---|
| 1 | 25‰ | 800 ms | — |
| 2 | 45‰ | 1 100 ms | chance ×1.75 · duration **×1.38** |
| 3 | 75‰ | 1 500 ms | |
| 4 | 135‰ | 2 100 ms | |
| 5 | 235‰ | 2 900 ms | chance ×9.4 overall · duration **×3.6** overall |

**Rule, committed: control families ladder duration at `r = 1.4`, never at 1.75.** Duration stacks across
sources and across the roster; chance does not. A five-tier duration ladder at 1.75 puts tier-5 freeze at
7.5 seconds and turns three items into a permanent lock. This is the affix design that most reliably
breaks a shipped ARPG, and the flatter ladder is the cheapest possible guard.

### 4.6 Item-level gating

`item_level` is stamped on the instance at drop (**I12**) from the base type's band (**I3**). I8 owns only
the function from it to a tier set.

| Tier | Unlocks at `item_level` |
|---|--:|
| 1 | 1 |
| 2 | 12 |
| 3 | 25 |
| 4 | 40 |
| 5 | 60 |

**Sliding window: the pool offers the highest unlocked tier and the two below it.**

| `item_level` | Highest unlocked | Tiers actually offered |
|---|---|---|
| 1 – 11 | t1 | t1 |
| 12 – 24 | t2 | t1, t2 |
| 25 – 39 | t3 | t1, t2, t3 |
| 40 – 59 | t4 | **t2, t3, t4** — t1 falls out |
| 60+ | t5 | **t3, t4, t5** |

The container's own `min_tier` / `max_tier` (I1's rarity window) intersects with this, and **the
intersection is what the pool must satisfy**:

```text
effective_window = [ max(min_tier, ilvlFloor) , min(max_tier, ilvlCeil) ]
```

An empty intersection means no pool row is drawable, which the existing schema already rejects as
`UnsatisfiablePool` ([spec-container-schema.md](../effect-atom/spec-container-schema.md)). A pool row
outside it is the existing `TierOutOfWindow`. **No new validation is needed for item-level gating** — it
narrows a window the schema already enforces.

### 4.7 The one-per-group rule, and whether it needs a second layer

`group` defaults to `(family_id, variant)`, so an item may roll fire power and ice power but never two
tiers of the same one ([definitions.md](../effect-atom/definitions.md) §4). **That layer stays exactly as
it is.** The question is what sits on top.

**Yes, one second layer is needed: the class quota.** And it is *not* a group, because the algebra is
different — a group says *at most one*; the quota says *at most three, across many groups*. The schema
has no way to express a quota, which is why §3.3 adds two columns rather than reusing `group`.

**What breaks without it**, concretely and in this repo:

| Without the quota | The failure |
|---|---|
| An item with `pool_rolls = 6` drawing from a pool where 40 of 70 families are suffixes averages **3.4 triggered atoms**, with real variance. Some items roll 6 procs, some roll 0 | Variance in *kind* is far worse than variance in *magnitude*. Two rares of the same base type become **incomparable**, and the compare-two-items view (I13) has nothing to compare |
| 6 suffixes × 15 slots = **90 triggered atoms** walked per matching event, per actor | The 2026-08 perf audit's exact failure shape: per-event work on the main thread. It is not a hypothetical; it is the thing that already lagged this game |
| 6 prefixes on one item | A pure stat stick. It has no identity, no name worth reading, and no reason to prefer it over the next one with bigger numbers |

**A third layer is *not* needed**, and the reason is that the schema already has the escape hatch. The
one case that worried me is element breadth: nothing stops a weapon rolling fire, ice, and air power as
three prefixes, which under the element ring is strictly better than three tiers of one element. The fix
uses the existing **explicit `group` override**: on roles where multi-element is not wanted, every
`elemental_power` pool row is emitted with `group = 'g.elem-power'` instead of the default, collapsing
seven variants into one draw. On `jewel-major`, where breadth *is* the fantasy, the default is left alone.
**One column, already there, already validated. No new mechanism.**

#### The `standard` slot, G8, and what "+armour" means on an item

Gap **G8** says a `stat.modify` on `defense` bound anywhere but `match` scope silently does nothing,
because the `TakeDamage` prefix reads one side-wide cached value
([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §7; `GameHooks.cs:578` plant, `:683` zombie,
per [atom-family-library.md](../effect-atom/atom-family-library.md) §4.1a). The bind gate rejects it with
`ScopeUnsupported` at `plant:N`, `zombie:N`, and `entity:` alike
([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)).

So **"+armour" is not one affix. It is four different answers, and one of them is not a prefix at all.**

| Frame / role | What "+armour" is today | Class | Status |
|---|---|---|---|
| Humanoid or zombie-bodied item | `plating` / `carapace` — real `arm1Max` / `arm2Max` Unity fields, written per entity | prefix | ✅ **works now** |
| Plant-frame item | **nothing.** No per-actor primary mitigation path exists | — | ⛔ |
| Plant-frame item, the honest substitute | `vitality` (effective HP) and **`warded`** — `shield.grant`, and the shield stack is shipped **for both sides** ([atom-family-library.md](../effect-atom/atom-family-library.md) §4.1) | prefix + **suffix** | ✅ works now |
| Commander `standard` slot | **`warding` / `resilience`** | prefix | ✅ **works now, and only here** |

That last row is the design payoff of the constraint. [item-ideal.md](../item-ideal.md) §5.6 proposes that
the commander's `standard` binds at **`match` scope** so its atoms buff the whole squad. `match` is
precisely the one scope where G8 permits primary `defense`. So:

> **`warding` and `resilience` are banned from every item pool in the game except the commander's
> `standard` slot, where they are the signature affix.** "+8% armour for your entire army" is a banner,
> not a helmet — and that is a better item than the helmet would have been.

Enforcement is a static family blacklist on `container_kind = 'item'` where `slot != 'standard'`,
rejecting at **import** with `ScopeUnsupported`. Promoting that check from bind time to import time is
the point: the player must never equip an item and see nothing happen.

When E12 lifts the quarantine, `elemental_defense` (`combat.defense.*`, per-actor through the overlay
resolver) becomes the real per-actor armour prefix on **both** frames, and the plant row above stops being
empty. `warding` stays banned outside `standard` until perf **O5** lands, and that is not this lane's
call to make.

### 4.8 Pool weighting

**A pool row's weight is a product of three factors, and only one of them is allowed to be rare.**

```text
weight(family, variant, tier, role, frame)
      =  W_role(group)      ·  the matrix cell in §4.3, 0..6
       × W_tier(tier)       ·  the rarity curve, below
       × W_family           ·  1 for almost everything
```

**Tier weight — the only rarity axis:**

| Tier | Weight |
|---|--:|
| 1 | 1000 |
| 2 | 600 |
| 3 | 300 |
| 4 | 120 |
| 5 | 35 |

Roughly 29 : 1 from tier 1 to tier 5, and the sliding window means a tier-5 at ilvl 60 competes only
against tier-3 and tier-4 — an effective ~13 : 1 within its own bracket, not 29 : 1. That is the whole
answer to *"rare without being unfindable"*: **the window does half the work the weight would otherwise
have to do.**

**`W_family = 1`, and this is a rule, not a default.** The trap is multiplicative rarity — if a good
family is rare *and* its top tier is rare, finding it is rare², and the drop that finally produces it is
indistinguishable from a bug. So within a group, families are equal-weight, and "this affix should be
rare" is expressed **as a tier restriction, never as a low weight**:

> `bulwark` and `savagery` are described as *"rare tier band only"* in the family library. That is
> `min_tier = 4` on the family, which removes them from every pool below ilvl 40 and leaves their
> weight at the ordinary tier-4 and tier-5 values inside it. A player at ilvl 40 who has never seen a
> `bulwark` is unlucky; a player at ilvl 40 facing a 1-in-400 roll is being lied to.

Deviations from `W_family = 1` are allowed but must be listed explicitly in the generator input with a
reason, and the lint in §6.3 flags any weight below one fortieth of its group's maximum as **unfindable**.

#### Is a tier-5 roll a separate pool entry or a window? — **A separate pool entry.**

`atom_id` derives as `{family_id}[.{variant}].t{tier}` and the unique key is `(family_id, tier, variant)`
([definitions.md](../effect-atom/definitions.md) §1), so **every tier of every variant is its own atom
row and therefore its own `effect_container_pool` row with its own weight.** The `min_tier`/`max_tier`
window filters which rows are *eligible*; the weight decides among the eligible. Both mechanisms are
already built and neither needs changing.

The consequence is a content-volume problem, and it is the reason **pool rows are generated, never hand
authored**:

```text
rows(container) = Σ over eligible groups   families(group, frame, side)
                    × variants(family)
                    × tiers in the effective window
```

Worked for a plant-frame `core-protective` base type at ilvl 45, wave 1 (§4.9):

| Group | Weight | Live families after filters | Variants | Tiers | Rows |
|---|--:|---|--:|--:|--:|
| `g.life` | 6 | `vitality`, `fortitude`, `mending`, `bulwark` (t4+ only, in window) → 4 | 1 | 3 | 12 |
| `g.armour` | 5 | **none** on a plant frame → weight redistributed (§4.4) | — | — | 0 |
| `g.on-death` | 2 | `deathblast`, `martyrdom`, `gardener` | 1 | 3 | 9 |
| `g.sustain` | 2 | `regeneration`, `cleansing`, `warded` | 1, 1, 7 | 3 | 27 |
| `g.evade`, `g.ward`, `g.shield-stat` | 1+4+3 | quarantined → redistributed | — | — | 0 |
| **Total** | | | | | **48 rows** |

48 pool rows over 5 drawable groups, 1 of them prefix-class and 2 suffix-class (plus the two emptied).
Against `prefix_rolls = 1` and `suffix_rolls = 2`, both classes clear `PoolRollsExceedGroups`. Forty-eight
rows is a generated artefact; it is not something a person types.

### 4.9 What wave 1 can actually ship, and what it cannot

**This is the `stat.derived` problem, stated at full strength.** All 12 element/crit/shield families plus
the 4 status-channel families are `stat.derived`, and `stat.derived` is quarantined `None/None/None`
today (**D6**). No opcode, no bag branch, no sink arm; battle reads `ChannelMods` only from
`TraitBattleCatalog` ([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §2). The first consumer
ships in **E12**.

**Exactly which of my affixes are affected: 16 of my 30 prefix families — 53% of the prefix layer, and
every single generated element variant.** In atom rows it is far worse than 53%, because the element
families are the ones that expand ×7:

| | Families | Atom rows at 5 tiers |
|---|--:|--:|
| Prefix, quarantined (`stat.derived`) | 16 | ~420 generated + 20 status-channel |
| Prefix, live (`stat.modify`) | 14 | 70 |
| Suffix, live | 40 | ~200 |

Cut the live prefix pool further by the constraints that are already known:

| Removed from wave 1 | Why | Families |
|---|---|--:|
| `warding`, `resilience` | G8 — banned outside `standard` (§4.7) | −2 |
| `quickening`, `flourishing`, `swiftness` | the channel-extension spec has not shipped; they are cheat-document keys today, not channels ([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §4.1) | −3 |
| `plating`, `carapace` | zombie-bodied items only — available, but on one frame | frame-limited |

**Wave 1's prefix pool is 7 families on a plant frame and 9 on a humanoid one.** `vitality`, `fortitude`,
`bulwark`, `might`, `ferocity`, `savagery`, `mending`, plus `plating` and `carapace` where the body has
the fields. That is a stat stick, and no amount of naming makes it feel like an ARPG.

**So wave 1 ships suffix-first.** The recommendation:

| | Wave 1 (today) | Wave 2 (E12 lands) |
|---|---|---|
| Class shape | **1 prefix + up to 3 suffixes** on a rare | 3 + 3, the designed shape |
| Prefix pool | 7–9 `stat.modify` families | +16 families, +~440 atom rows, in one import |
| What carries the item's identity | the **suffix** — a proc, a status rider, a board effect | rebalances toward the prefix, as intended |
| Naming | the suffix word does the work ("… of Embers") | the prefix word becomes meaningful |

**The design does not change between the waves — only `prefix_rolls` does.** That is the whole reason the
class quota is two integer columns on the container rather than a shape baked into the pool: lifting the
quarantine is an import plus one number per rarity rung, not a redesign. Everything in §4.1–§4.8 is
authored now, for 70 families, and 16 of them sit at weight 0 until E12.

**One enforcement rule makes this safe.** The bind gate rejects a `stat.derived` atom with
`RuntimeUnsupported` ([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)) — but
by then the item has already dropped, and the player clicks equip and gets an error. **For
`container_kind = 'item'`, the runtime-support check is promoted from bind time to import time**: an item
container carrying a pool row whose kind is `None` in the item's target runtime is rejected at import with
`RuntimeUnsupported`. A drop that cannot be equipped is worse than no drop.

### 4.10 Hybrid affixes

A hybrid affix touches two channels at once — the classic `+X life and +Y armour`, `+X attack and +Y
attack speed`. Three ways to get one, and only one of them works today.

| Route | Verdict |
|---|---|
| A new kind that carries two channels | **Forbidden.** SC2 closes the kind list at 12 |
| Two atoms drawn as a unit from the pool | **Not expressible.** `effect_container_pool` draws atoms independently; nothing says *"these two come together and count once against the quota"* |
| Two atoms in the **fixed core** (`effect_container_atom`) | **Works today, with no change at all.** The fixed core has no one-per-group constraint and no quota |

**Committed: hybrid affixes are a fixed-core mechanism, not a rolled one.** Uniques, set pieces, and
crafted items express hybrids by putting both atoms in `effect_container_atom`. The rolled pool does not
offer hybrids in wave 1, and the loss is small — a hybrid roll is mostly a *name* ("Vicious" reading as
one affix), and the naming grammar in §4.12 already gets that from a single high-tier family word.

Where a true rolled hybrid is eventually wanted, the minimal ask is **one nullable `bundle_id` column on
`effect_container_pool`**: rows sharing a `bundle_id` are drawn together, occupy one group, and count
once against the class quota. That is a request to E5 in §9, not an assumption, and nothing in wave 1
depends on it.

### 4.11 Crafted-only affixes

An affix that exists in the pool but never drops. **The schema already does this exactly, with no
change:**

> *"`weight = 0` — row kept, never drawn."* — [spec-container-schema.md](../effect-atom/spec-container-schema.md)

So a crafted-only affix is a pool row at `weight = 0`. The drop path never sees it; the craft path (I6 /
I7) *selects* the row by `atom_id` instead of drawing it. Three properties fall out for free:

1. `PoolRollsExceedGroups` counts only **drawable** groups (`HAVING max(weight) > 0`), so a crafted-only
   group does not inflate the count and cannot cause a silent under-fill.
2. `UnsatisfiablePool` still fires if *every* row is zero, so an all-crafted pool is caught.
3. The crafted affix is a normal atom — same tier, same band, same one-per-group behaviour, same naming.

**One rule other lanes must honour:** a reroll (I7) draws from `weight > 0` only. If reroll draws from the
full pool, crafted-only affixes become findable by spamming reroll and the whole category evaporates.
That is item 7 in §9.

Crafted-only affixes are also where the **hybrid** and the **above-window** content live: a craft can
place a fixed-core hybrid pair, and a craft can place an atom whose tier is outside the item's
`item_level` window, because the craft is not the pool draw. Both are I6/I7's to price.

### 4.12 Item naming from affixes

```text
[prefix word]  [base type name]  of [suffix word]
   Sturdy         Bark Helm         of Embers
```

**The name is a pure function of `(base_type, affix set)` and is never stored.** No `name` column on the
instance, nothing to migrate when I7 rerolls an affix, and nothing that can disagree with the item's
actual contents. Recomputing it is a lookup, and SC5's determinism contract carries it for free.

**Selection.** The name uses the **highest-tier prefix** and the **highest-tier suffix** on the item. Ties
break on `(tier DESC, seq ASC)` — content-derived, ordinal, and deliberately *not* on `instance_id` or
`binding_id`, which are generated and would give two byte-identical items two different names.
[definitions.md](../effect-atom/definitions.md) §5 makes exactly this mistake explicit for the effect
list; the same trap applies to naming.

**Name bands, not tiers.** Five tiers map onto **three** name bands, so the word vocabulary is 3 per
family, not 5:

| Band | Tiers |
|---|---|
| A | t1 – t2 |
| B | t3 |
| C | t4 – t5 |

70 families × 3 bands is 210 words, and the element families reuse one word set across all seven variants
by substituting the element's flavour name — which
[atom-family-library.md](../effect-atom/atom-family-library.md) §3.2 already authored: *Ember / Frost /
Gale / Stone / Radiant / Umbral*.

**Sample word table** (illustrative):

| Family | Class | Band A | Band B | Band C |
|---|---|---|---|---|
| `vitality` | prefix | Hale | Robust | Vital |
| `fortitude` | prefix | Sound | **Sturdy** | Enduring |
| `bulwark` | prefix | — | — | Adamant |
| `might` | prefix | Keen | Strong | Mighty |
| `ferocity` | prefix | Angry | Fierce | Ferocious |
| `plating` | prefix | Tin-clad | Iron-clad | Adamantine |
| `elemental_power.fire` | prefix | Warm | Smoldering | Ember |
| `elemental_defense.ice` | prefix | Cool | Chilled | Frostward |
| `searing_strike.fire` | suffix | of Sparks | of Cinders | **of Embers** |
| `lifesteal` | suffix | of Sipping | of Draining | of the Leech |
| `freezing` | suffix | of Chill | of Rime | of the Glacier |
| `regeneration` | suffix | of Knitting | of Renewal | of the Wellspring |
| `sunbloom` | suffix | of Gleaning | of Harvest | of Abundance |
| `gravemaking` | suffix | of Barrows | of Cairns | of the Boneyard |
| `terraforming` | suffix | of Tilling | of Terraces | of Genesis |

**Plant-frame items get overrides, but only where the humanoid word reads wrong.** One nullable column,
`word_plant`, on the name row; when it is NULL the humanoid word is used. Overriding *every* row would
double the vocabulary for no gain, and most words are frame-neutral already — "Sturdy", "Ember", "of the
Wellspring" all read fine on a turnip.

| Family | Humanoid | Plant override |
|---|---|---|
| `quickening` (band C) | Quickening | **Blooming** |
| `mending` (band C) | Restorative | **Verdant** |
| `regeneration` (band C) | of the Wellspring | *(none — it works)* |
| `sunbloom` (band C) | of Abundance | **of Photosynthesis** |
| `evasion` (band B) | Shifting | **Deep-rooted** |
| `swiftness` | Swift | **n/a — never rolls on a plant** (§4.4 frame filter) |

**Rare items do not use this grammar.** With up to 6 affixes there is no honest way to compress the item
into `prefix + noun + suffix`, and picking two of six to name it by is a lie about what the item does. So:

| Rarity | Name |
|---|---|
| Normal (0 affixes) | the base type name — `Bark Helm` |
| Magic (1–2 affixes) | **the affix grammar** — `Sturdy Bark Helm of Embers` |
| Rare (3–6 affixes) | a **generated two-word name** from a head/tail word table, seeded by `roll_seed` — `Bramble Bite`, `Havoc Root`. The base type and the affix list live in the tooltip |
| Unique | hand-authored. The grammar is bypassed entirely |

The rare name is seeded from `roll_seed`, so it satisfies SC5 the same way the rolls do: same
`(container_id, catalog_revision, roll_seed)`, same name, byte for byte. It also survives I7's reroll
unchanged, which is correct — rerolling one affix should not rename your item.

---

## 5. Data shape

### 5.1 Columns reused, unchanged

Everything below is already in the schema and I8 adds nothing to it.

| Column | Table | What I8 uses it for |
|---|---|---|
| `pool_rolls` | `effect_container` | total affix count; stays the sum |
| `min_tier` / `max_tier` | `effect_container` | the rarity tier window (I1's), intersected with the ilvl window |
| `rarity` | `effect_container` | the key I1's budgets are looked up by |
| `slot` | `effect_container` | the equip role (I2's ids) |
| `level_req` | `effect_container` | I11's gate; I8 does not read it |
| `atom_id`, `weight`, `group` | `effect_container_pool` | one row per (family, variant, tier); the generated weight; the explicit group override for element collapsing (§4.7) |
| `family_id`, `variant`, `tier` | `effect_atom` | identity; `atom_id` derives from them |
| `kind_id` | `effect_atom` | **the affix class**, derived not stored |
| `values_json` | `effect_atom` | the tier band `{min, max, policy}` |
| `seq` | `effect_container_atom` | the naming tiebreak, and the fixed-core hybrid slot |

### 5.2 New columns — two, and both nullable

| Table | Column | Type | Meaning |
|---|---|---|---|
| `effect_container` | `prefix_rolls` | INT NULL | how many prefix-class atoms to draw |
| `effect_container` | `suffix_rolls` | INT NULL | how many suffix-class atoms to draw |

Semantics:

- **Both NULL** ⇒ today's behaviour exactly. One pool, `pool_rolls` draws, no class awareness. Traits,
  skills, species passives, patrons, and world buffs are unaffected, and no existing golden moves.
- **Both set** ⇒ the pool is partitioned by derived class and the draw runs twice, `prefix_rolls` over
  the prefix partition and `suffix_rolls` over the suffix partition. `pool_rolls` must equal their sum.
- **One set, one NULL** ⇒ rejected.

Draw order for `seq`: fixed core first (authored `seq`, unchanged), then **prefixes in draw order, then
suffixes in draw order**, continuing the numbering. That preserves
[definitions.md](../effect-atom/definitions.md) §5's rule that the deterministic part comes first, and it
makes the name tiebreak (`tier DESC, seq ASC`) stable.

**This changes the Checkpoint B contract and is therefore ask-first under E5's boundaries.** §9 raises it.

### 5.3 New table — one

| Table | Columns | Consumer (SC7) |
|---|---|---|
| `item_affix_name` | `family_id`, `variant`, `band` (A/B/C), `class`, `word`, `word_plant` NULL | the item naming function in Core, called by the tooltip, the inventory list (I13), and the loot toast |

That is a genuine runtime table: a word must be readable at display time. It is the only one I8 adds.

### 5.4 Generator input — not tables

Three artefacts that a naive design would make tables and that should **not** be, because nothing at
runtime reads them. Under SC7, *a row no code consumes is not content; it is a lie in a table* — and a
table whose only consumer is the importer is just the importer's input file.

| Artefact | What it holds | Consumed by |
|---|---|---|
| `affix-groups` | family → group, and the family's frame/side filters | the content generator |
| `role-affix-weights` | the 13 × 15 matrix in §4.3 | the content generator |
| `tier-bands` | one `share` per family, plus per-family ladder overrides (`r`, the two-ladder cases) | the content generator |

The generator emits `effect_atom` rows (the bands, as `values_json`) and `effect_container_pool` rows (the
weights). **Those generated rows are the SSOT**; the input files are how they are produced, and they live
beside the other content sources, versioned in the repo.

**Content-hash consequence:** the generated rows are already covered — `effect_atom` and
`effect_container_pool` are both in the E8 registry ([definitions.md](../effect-atom/definitions.md) §8)
— so a weight or band change moves the hash correctly with **no `contentHashSchemaVersion` bump and no
new covered table**. That is a direct argument for generator-input-over-table: the table route would need
registering, and an unregistered balance table is exactly the `effect_channel_policy` defect the catalog
SSOT already flags.

---

## 6. Validation and reason codes

### 6.1 Rejections that reuse an existing code

| Bad input | Code | Phase |
|---|---|---|
| A pool row whose atom's tier is outside `[min_tier, max_tier]` | `TierOutOfWindow` | import |
| The ilvl window and the rarity window do not intersect, so no row is drawable | `UnsatisfiablePool` | import |
| `prefix_rolls` exceeds the distinct drawable **prefix** groups | `PoolRollsExceedGroups` | import |
| `suffix_rolls` exceeds the distinct drawable **suffix** groups | `PoolRollsExceedGroups` | import |
| `warding` or `resilience` in an item pool for any slot but `standard` (§4.7, G8) | `ScopeUnsupported` | **import** — promoted from bind |
| A pool row whose kind has runtime support `None` for the item's target runtime — every `stat.derived` family in wave 1 (§4.9) | `RuntimeUnsupported` | **import** — promoted from bind |
| `susceptibility` authored at all (`status.expose.*` has zero readers) | `RuntimeUnsupported` | import |
| Tier band `values_json` with `min > max`, or `Fixed` with `min != max` | `BadValueSpec` | import |
| A chance above 1000‰ from a mis-scaled chance ladder | `BadParamValue` | import |
| A tier band whose magnitude overflows `int` after curve scaling | `MagnitudeOverflow` | import |
| The same atom in an item's fixed core and its pool (a crafted hybrid duplicated into the pool) | `DuplicateAtomInContainer` | import |

### 6.2 New reason codes — two

Adding to a closed 33-code list is a reviewed change ([definitions.md](../effect-atom/definitions.md)
§10), so each is justified on its own.

| Code | Fires when | Why an existing code will not do |
|---|---|---|
| **`AffixNotLegalHere`** | A pool row's family has weight 0 for the container's role, or is filtered out by frame or side — `swiftness` on a plant item, `plating` on a plant item, `g.economy` on `core-protective` | This is the single most common authoring mistake I8 makes possible, and it is the one that quietly makes every slot interchangeable. There is no existing code for *"legal atom, wrong place"* — `ScopeUnsupported` is about owner scope, `TierOutOfWindow` is about strength. Folding it into either would put the wrong word in the operator's log for the failure that will happen most often |
| **`AffixClassRollsMismatch`** | `prefix_rolls + suffix_rolls != pool_rolls`, or exactly one of the two is NULL, or either exceeds 3, or `pool_rolls ≥ 3` with either class at 0 | Guards the new columns. Reporting it as `BadParamValue` would name the value and not the invariant, and the invariant is the whole reason the columns exist |

### 6.3 Authoring lints — warnings, not rejections

These are content-quality checks on I8's own generator input. They are not player- or author-facing
rejections and do not belong on the reason-code list.

| Lint | Fires when | Action |
|---|---|---|
| `family-ungrouped` | A family in the atom library maps to no affix group | **Fails the generator.** This is what caught the 70-vs-71 discrepancy in §4.2 |
| `group-empty-for-frame` | A `(role, group)` cell has weight > 0 but every member family is filtered out for that frame or side | Redistribute the weight proportionally across the role's remaining groups, and log the triple (§4.4) |
| `band-overlap-broken` | `hi_t < lo_(t+1)` after `round_legible` | **Fails the generator.** The overlap is OD4's tier-level mechanism; a rounding snap must never silently remove it |
| `band-resolution-low` | `m_1 < 5`, so integer bands are too coarse to be distinct | Warn, and name the `Increased` sibling that should carry the fine grain. `might` trips this knowingly (§4.5) |
| `weight-unfindable` | A pool row's weight is below one fortieth of its group's maximum | Warn. Below that the affix reads as broken rather than rare (§4.8) |
| `band-copied-across-families` | Two families in different channel-unit classes share an identical band table | Warn loudly. This is the 10× units bug (SC4), and identical numbers across a *game units* family and a *resolver points* family is its fingerprint |

---

## 7. Worked examples

**All numbers illustrative, not balanced.** They exist to show the method, the units, and the failure the
validation catches.

### 7.1 A plant-frame rare helm at ilvl 45 — wave 1

**Container:** `item.bark-crown-rare`, `slot = head-protective`, `frame = plant`, `rarity = rare`,
`item_level = 45`.

I1 sets `pool_rolls = 4`. Wave 1 shape (§4.9) gives `prefix_rolls = 1`, `suffix_rolls = 3`.

**Window.** ilvl 45 → highest unlocked t4, sliding window offers **t2, t3, t4**. I1's rarity window for
rare is `min_tier = 2`, `max_tier = 5`. Intersection: **[2, 4]**.

**Pool after the matrix and the three filters.** `head-protective` weights: life 5, armour 4, ward 3,
shield 2, precision 1, evade 1, on-death 2, sustain 1.

| Group | Weight | Survives the filters? |
|---|--:|---|
| `g.life` | 5 | ✅ `vitality`, `fortitude`, `mending`, `bulwark` (t4 only) |
| `g.armour` | 4 | ✖ plant frame — `plating`/`carapace` zombie-only, `warding`/`resilience` banned. **Weight redistributed**, lint fires |
| `g.ward`, `g.precision`, `g.evade`, `g.shield-stat` | 3+1+1+2 | ✖ quarantined (`RuntimeUnsupported` at import). **Redistributed** |
| `g.on-death` | 2 | ✅ `deathblast`, `martyrdom`, `gardener` |
| `g.sustain` | 1 | ✅ `regeneration`, `cleansing`, `warded` (×7 element variants) |

After redistribution the prefix side is `g.life` alone at 100% of the prefix weight, and the suffix side is
`g.on-death` 2 : `g.sustain` 1. **The lint output on this one container is the honest picture of wave 1**:
five of eight groups on the game's most iconic slot are empty.

**Draw** (seed fixed, illustrative):

| # | Class | Atom | Group | Rolled value | Unit |
|---|---|---|---|---|---|
| 1 | prefix | `atom.fortitude.t3` | `(atom.fortitude, '')` | **+52‰ maxHp** | per-mille |
| 2 | suffix | `atom.regeneration.t4` | `(atom.regeneration, '')` | 34 hp / 2 000 ms | game units + ms |
| 3 | suffix | `atom.warded.fire.t2` | `(atom.warded, fire)` | 120 fire shield `OnSpawn` | game units |
| 4 | suffix | `atom.gardener.t2` | `(atom.gardener, '')` | spawn 1 plant `OnDeath`, 250‰ | count + ‰ |

**Name.** 4 affixes ⇒ rare ⇒ generated two-word name from `roll_seed`: **"Thornward Bloom"**. The affix
words are not used; they appear in the tooltip. Had this been a magic item with only #1 and #3, the name
would be **"Enduring Bark Crown of Cinders"**.

**What the example proves:** the item is entirely playable today — every one of its four atoms binds and
executes on the lawn — and it is *also* visibly thin, because the prefix half of the design is behind E12.

### 7.2 A humanoid magic gauntlet at ilvl 8

**Container:** `item.plate-gauntlet-magic`, `slot = manipulator-offense`, `frame = humanoid`,
`item_level = 8`, `pool_rolls = 2`, `prefix_rolls = 1`, `suffix_rolls = 1`.

**Window.** ilvl 8 → **t1 only**. I1's magic window is `min_tier = 1, max_tier = 3`. Intersection:
**[1, 1]**. A level-8 item cannot roll t2, and there is no lucky exception.

| # | Class | Atom | Value | Unit |
|---|---|---|---|---|
| 1 | prefix | `atom.might.t1` | **+4 atk** | game units |
| 2 | suffix | `atom.searing-strike.fire.t1` | 35–65 fire on hit, 200‰, `onApply` | game units + ‰ |

**Name:** magic, 1 prefix + 1 suffix ⇒ the grammar applies. `might` t1 = band A = "Keen";
`searing_strike.fire` t1 = band A = "of Sparks". → **"Keen Plate Gauntlet of Sparks"**.

Note affix #2's value is **not frozen**. `{35, 65, onApply}` rolls on every hit
([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md): *"`OnApply` values are left
unresolved — they belong to the hit, not the item"*). Two copies of this gauntlet are byte-identical, and
that is correct.

### 7.3 A commander banner at ilvl 55 — the G8 exception

**Container:** `item.war-standard-rare`, `slot = standard`, `frame = humanoid`, `item_level = 55`,
`pool_rolls = 3`, `prefix_rolls = 2`, `suffix_rolls = 1`. Binds at **`match`** scope
([item-ideal.md](../item-ideal.md) §5.6).

**Window.** ilvl 55 → t2, t3, t4. `standard` weights: armour 4, economy 4, board 3, and 2 across life,
attack, tempo, ward, elem-pw, precision, shield, sustain.

| # | Class | Atom | Value | Unit | Note |
|---|---|---|---|---|---|
| 1 | prefix | `atom.resilience.t3` | **+64‰ defense** | per-mille | **Legal only here.** Bound at `match`, this is the one scope G8 permits |
| 2 | prefix | `atom.vitality.t4` | +118 hp | game units | applies side-wide at `match` scope |
| 3 | suffix | `atom.midas.t3` | 9 money per kill, `capPerMatch` | game units | |

**Name:** rare ⇒ generated. **"Ironcall Vigil"**.

**What the example proves:** the constraint that made "+armour" the hardest common affix to ship
([item-ideal.md](../item-ideal.md) §9) turns into the commander slot's signature. *+6.4% defense for the
entire army* is a better item than a helmet's *+6.4% defense for one plant* would ever have been, and it
required no new mechanism — only reading which scope G8 leaves open.

### 7.4 A crafted-only affix, and the reroll that must not find it

**Container:** `item.pea-nozzle-rare`, with one crafted-only row.

| `atom_id` | `group` | `weight` | Drawable? |
|---|---|--:|---|
| `atom.searing-strike.fire.t4` | `(atom.searing-strike, fire)` | 120 | ✅ |
| `atom.volley.t3` | `(atom.volley, '')` | 300 | ✅ |
| `atom.lifesteal.t3` | `(atom.lifesteal, '')` | 300 | ✅ |
| **`atom.deathblast.omni.t5`** | `(atom.deathblast, omni)` | **0** | ✖ **crafted only** |

`suffix_rolls = 2`; drawable groups = 3, so `PoolRollsExceedGroups` passes — the zero-weight group is not
counted, per the schema's `HAVING max(weight) > 0` rule. `UnsatisfiablePool` does not fire, because three
rows are non-zero.

The craft operation (I6's model, I7's operation, I9's cost) *selects* `atom.deathblast.omni.t5` by id. A
reroll must draw only from `weight > 0` — otherwise the crafted-only affix becomes findable by grinding
rerolls and the category stops existing. **Zero schema change; one rule two other lanes must honour.**

---

## 8. Failure modes

Unsentimental, with what in this design prevents each — and where it does not.

### 8.1 An affix pool so wide every item is mediocre

**How it happens.** The open ilvl window: tier 1 never leaves the pool, so at max level four fifths of
every roll is junk, and the player's response is to ignore 95% of drops. D2 shipped close to this and
survived on volume; PoE manages it with ilvl-gated tier removal *and* an item filter, which is a UI
solving a content problem. (Both recalled, unverified.)

**Prevented by:** the sliding window (§4.6) removes tiers more than two below the highest unlocked, and
the role matrix (§4.3) narrows a slot to 5–8 legal groups out of 15. A plant `core-protective` at ilvl 45
draws from 48 rows, not from the ~2 000 the full library could offer. **The pool is narrow by
construction, not by a filter the player has to configure.**

**Residual risk:** the sliding window makes tier-1 and tier-2 content dead above ilvl 40. Those atom rows
still exist and still cost import time and hash surface. Accepted — they are cheap, and they are live for
every low-level actor.

### 8.2 Obvious best-affixes make all others filler

**How it happens.** One affix is strictly better than its siblings, so every build takes it and the other
n−1 are decoration. D4's "critical strike damage with a two-handed weapon" class of affix is the archetype
(recalled, unverified).

**Partly prevented by** three things:

1. **Rarity lives only on tier.** `W_family = 1` (§4.8), so finding the good family is one roll, not
   two. An affix that is rare *and* strong is a slot machine.
2. **The class quota forces diversity.** A rare must carry at least one prefix and at least one suffix
   (§3.2), so the best-stat-only item is not constructible.
3. **The role matrix removes the comparison.** The best offensive affix is simply not in a
   `core-protective` pool, so the question "is this the best affix" is asked per role, over 5–8 groups,
   not globally over 15.

**Not prevented, and I am not going to pretend otherwise.** Whether `+185 hp` and `+140‰ attack` are
*comparable* is a balance question, and the instrument that answers it is the power model — which is
**E9, build position 15, with an unsolved cost function for multiplicative pairs, and `power_json`
nullable** (SC9). I8 ships without it, as required. The consequence is real: the tier bands in §4.5 are
internally consistent per family and **unvalidated across families**, and the first thing a sweep will do
is move them. §10 asks the owner whether that is acceptable for a first wave; my recommendation is yes,
because the *method* is what has to be right now and the numbers are one generator run away from being
re-fitted.

### 8.3 Tier bands copied across channel families and wrong by 10×

**How it happens.** Someone writes five numbers for `vitality`, copies the row for `elemental_power`, and
ships `+185 fire power` — 1.85 sigmoid units, more than twelve times `critical-hunter`'s entire grant on
one affix. Nothing in a JSON file objects.

**Prevented by:** bands are **generated from a per-family `share` against a per-family base curve**
(§3.4), so there is no row to copy — the input is one dimensionless number and the unit comes from `B`.
On top of that, `band-copied-across-families` (§6.3) fires when two families in different unit classes
carry identical band tables, which is what a copy-paste looks like from the outside.

**And a test, not just a lint.** The §4.5 side-by-side — t5 `vitality` max 245 game units vs t5
`elemental_power` max 74 resolver points — should be a golden assertion, so a regeneration that
accidentally unifies them fails the suite rather than reaching a player.

### 8.4 Affixes that no runtime can execute

**How it happens.** This one has already happened in this repo, twice, and both scars are on record:
`status.expose.*` is a legal registered derived channel with **zero readers**, and eight of 21 statuses
are declared with no consumer ([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §5, §8a). *A
row no code consumes is not content; it is a lie in a table.*

**Prevented by:** promoting `RuntimeUnsupported` from bind time to **import time** for
`container_kind = 'item'` (§4.9). An item that could drop and not work never enters the catalog. This is
the single most valuable rule in the document, because the alternative failure — the player clicks equip
and gets an error dialog — is worse than any amount of missing content.

**Residual:** the check is only as good as the runtime-support matrix, which
[definitions.md](../effect-atom/definitions.md) §9 explicitly calls a living audited table. If the
matrix says `Full` and the consumer is half-built, items ship broken. That is E1's audit to keep honest,
not mine to duplicate.

### 8.5 The suffix proc storm

**How it happens.** Not in the brief's list, and it is the one I would bet on. Six triggered atoms per
item × 15 slots = **90 atoms on one actor's effect list**, every one of them walked on every matching
event. The 2026-08 perf audit already found that this game's combat lag is per-hit main-thread work.
An affix system is the fastest way to multiply that by fifteen.

**Partly prevented by:** the class quota caps it at 3 per item, so 45 rather than 90; and the prefix half
of the item costs nothing per event by construction, which is the whole reason for the split (§3.1).

**Not sufficient.** 45 is still 45. §9 asks I1 and I2 for a **per-actor suffix ceiling** on top of the
per-item one, and asks the perf stream for a probe scenario with a fully geared roster before the first
wave of items reaches a live lawn. This is the number I most want measured and least want to guess.

### 8.6 The affix pool becomes unauthorable

**How it happens.** 70 families × up to 7 variants × 5 tiers × 13 roles × 3 frames is a number no team
hand-authors, so someone hand-authors a subset, and the subset is what ships — inconsistent, partial, and
impossible to retune.

**Prevented by:** pool rows are **generated** from three small input artefacts (§5.4). The 13 × 15 matrix
is 195 numbers a person can read in one sitting; the tier bands are one `share` per family. Retuning a
role means editing one row of fifteen numbers and regenerating.

---

## 9. What this lane needs from other lanes

Numbered, each naming the lane, each a thing I8 cannot decide alone.

1. **I1 — the class allocation per rarity rung.** I define the shape (`prefix_rolls + suffix_rolls =
   pool_rolls`, each ≤ 3, both ≥ 1 when `pool_rolls ≥ 3`); I1 picks the numbers per rung. I1 should also
   know that **wave 1 needs `prefix_rolls` low** (§4.9) and that raising it after E12 is a data change,
   not a redesign.

2. **I1 — the rest of OD4's overlap, because my half is small.** Measured against my own constants: with
   bands `[0.67m, 1.33m]` and `r = 1.75`, the probability that a tier-3 roll beats a tier-4 roll of the
   same family is roughly **2%**. That is a real overlap and it is *not enough on its own* to make "a
   high-roll low rarity beats a low-roll high rarity" a felt property. The rest has to come from **affix
   count** and the **rarity window**, which are I1's: a 4-affix rare with four high rolls beating a
   6-affix rare with six low rolls is where the visible overlap lives. I8 supplies the per-affix
   variance; I1 must supply the count variance.

3. **I2 — the final role id list and its stable kebab ids.** OD2 says ~15 roles per frame; §4.3 is written
   against the 12 published in the ideal plus `standard`. Every added role is one row of 15 numbers, so
   the cost is bounded — but the ids must be frozen before the matrix is generated, because they key the
   generator input.

4. **I2 or I11 — the `frame` field, as one field, read the same way by both of us.** §4.4's frame filter
   and I11's equip gate must read the *same* column. If they diverge, an item will roll a plant-only
   affix and then be equippable by a zombie, which is silent dead content of exactly the kind SC7 bans.

5. **I3 — three things from the base type.** (a) `item_level`, or the band I12 stamps it from, because
   §4.6 is a pure function of it. (b) The base-type **noun** for the naming grammar, per frame. (c) A
   guarantee that an **implicit does not duplicate a rollable affix family on the same item** — an
   implicit `+hp` above a rolled `+hp` reads as a bug even though it is legal, and the one-per-group rule
   does not span the fixed core and the pool.

6. **I12 — `item_level` at drop, and the seed.** I12 owns turning a loot event into an instance; it must
   supply the instantiator with both the `roll_seed` and the `item_level`, because the tier window is a
   function of the second and the draw is a function of the first.

7. **I7 — two rules for reroll.** (a) A reroll draws from **`weight > 0` only**, or crafted-only affixes
   (§4.11) stop existing. (b) A reroll must respect the **class quota** — rerolling a prefix produces a
   prefix. Rerolling across the class boundary would let a player convert a stat stick into a proc storm
   one currency at a time, which defeats the quota entirely.

8. **I6 — the mutation model, for two I8-specific cases.** An affix whose value spec is `onApply`
   (§4.5, `searing_strike`) has **no frozen number**, so "upgrade this affix's roll" is meaningless for
   it — only its tier can change. And because the item **name is derived, never stored** (§4.12), a
   tier upgrade silently renames a magic item. Both are fine; both need to be in I6's recorded-operation
   model rather than discovered later.

9. **I4 — sockets must not be a way around the class quota.** A gem that grants a triggered atom is a
   suffix by every definition in §3.1, and if inserts bypass the quota then a 3-socket item with 3 proc
   gems is a 6-suffix item. Either inserts count against the item's suffix budget, or sockets need their
   own ceiling. This is the single biggest interaction risk between our two lanes.

10. **I5 — set bonuses must not draw from the affix pool.** If a set tier grants a rolled affix, the
    one-per-group rule and the class quota both stop being item-local, and "how many suffixes does this
    character have" becomes a cross-item question with no owner. Set atoms should be fixed-core.

11. **I9 — a cost vocabulary for crafted-only affixes.** §4.11 makes them free to express; it says nothing
    about what placing one costs, and it should not.

12. **E5, in the effect-atom program — the two columns, and this is ask-first.**
    `effect_container.prefix_rolls` and `.suffix_rolls`, both nullable, NULL preserving today's behaviour
    exactly. E5's boundaries list *"adding a column"* as ask-first, and
    [spec-container-schema.md](../effect-atom/spec-container-schema.md) publishes its contract at
    **Checkpoint B**, which the action program's A1 consumes. This is the formal ask. Nothing else in I8
    touches that contract.

13. **E5 — one optional column, for later, not now: `effect_container_pool.bundle_id`.** Rows sharing a
    `bundle_id` draw together, occupy one group, and count once against the class quota. It is the only
    way to get rolled hybrid affixes (§4.10). **Wave 1 does not need it** and I am not asking for it yet;
    it is written down so the shape is on record if the owner wants hybrids later.

14. **E12 — the quarantine lift is the single largest input to this lane.** 16 of 30 prefix families and
    every generated element variant are `stat.derived` and bind nowhere until `BattleStatComposer` reads
    bound atoms at squad build. Until then the prefix half of the affix system does not exist. Nothing in
    I8's design blocks on it, and everything in I8's *feel* does.

15. **The perf stream — a probe scenario for a fully geared roster.** §8.5 estimates 45 triggered atoms
    per actor at the designed cap and cannot validate it. The perf pipeline
    ([docs/runbook/perf-probe-plan.md](../../runbook/perf-probe-plan.md)) already has the machinery; this
    needs a scenario, not a new tool. I would rather move the cap from 3 to 2 on measured evidence than
    ship 3 on a guess.

---

## 10. Open questions for the owner

1. **Is 3 + 3 the right ceiling?** PoE's 3 + 3 (recalled, unverified) is for a game with hundreds of
   hours of crafting. With ~15 slots and a *roster* of actors, 2 + 2 would halve both the authoring
   surface and the per-event cost, at the price of shallower individual items. §8.5 says I would move to
   2 + 2 on measured evidence; I have not made that call without it.

2. **Suffix-first wave 1, or wait for E12?** §4.9 recommends shipping items now with a 7–9 family prefix
   pool and letting suffixes carry the identity. The alternative is to prioritise E12 and ship the item
   system whole. This is a sequencing decision with a real cost either way, and it is not mine.

3. **The four constants.** `r = 1.75`, band `[0.67m, 1.33m]`, tier-weight ladder `1000/600/300/120/35`,
   ilvl unlocks `1/12/25/40/60`. Each is defensible and none is verified. They are the numbers a sweep
   will move first.

4. **Do rare items get generated names or affix names?** §4.12 commits to generated two-word names for
   3+ affixes, because naming a six-affix item after two of its affixes is a lie. D2 does this and PoE
   does this (recalled, unverified). But it costs the player the at-a-glance read that magic items keep,
   and some players prefer a long descriptive name to "Bramble Bite".

5. **Should `warding`/`resilience` really be the commander banner's signature (§4.7)?** It is elegant and
   it falls straight out of G8, but it makes one equip slot mechanically unlike every other, and it means
   the fantasy of "+armour on a helmet" never ships for plants until E12. The alternative is to ban the
   two families outright and accept that the commander banner is an economy and board slot instead.

6. **Two ladders for control families (§4.5) — is `r = 1.4` on duration right?** It is the guard against
   permanent-lock stacking, and it is a number I picked from the shape of the problem rather than from
   any measurement of this game's CC.

7. **Does the roster-scale gear question (item-ideal §8) change the affix design?** The ideal says it must
   be answered before slot counts freeze. If the answer is "a small deployable squad", 3 + 3 and 15 slots
   are fine. If it is "twenty demons all geared", the per-actor suffix budget in §8.5 becomes the binding
   constraint on the whole item system and I8 should be re-read with that in mind.

---

## Design-gate checklist

```
[x] I identified the subsystem(s) this touches — effect-atom (atom/container/instance/binding),
    the item enrichment program, battle base curves, the perf hot path.
[x] I read every document named in enrichment-contract.md §5, this session, in order.
[x] I checked decisions.md's binding inputs via the contract's §6 owner decisions — OD1–OD7 —
    and designed within OD2 (roles), OD4 (overlap), OD6 (containers of atoms).
[x] Every factual claim about the repo cites file:line or a document section.
[x] I verified claims against CODE where the number mattered: BattleModels.cs:60-73 for the base
    curves, CombatPolicies.cs:10-12 for the sigmoid scales.
[x] I read the surrounding section of every rule I quoted — G8 in three places (catalog §7,
    library §4.1a, definitions §6), the weight-0 rule in the container spec, §14.2 for triggers.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no test suite was run.** The
    quarantine (D6), the G8 scope rule, and the weight-0 behaviour are read from shipped specs and
    code, not executed. Before any of them justifies a build decision, run the suite.
[x] Nothing contradicts a §2 invariant of the enrichment contract — no new atom kind, no second
    modifier mechanism, no float in content, no silently ignored input.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no item map, plan, or task list exists yet.** This is a lane SSOT; those artefacts are
    written when the program graduates. The 70-vs-71 family-count correction in §4.2 is a finding
    against atom-family-library.md that I may not edit — it needs propagating by that document's
    owner.
```
