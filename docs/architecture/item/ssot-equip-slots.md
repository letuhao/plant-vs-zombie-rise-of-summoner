# Lane I2 SSOT — equip slots and frames

**Status:** Lane I2 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Owner decisions **OD1** (three frames), **OD2** (~15 roles per pure frame, main-hand and off-hand
mandatory, parallel naming), and **OD3** (hybrid gets 12–13, each role accepting either frame's base
type) are inputs. This document designs inside them and does not re-litigate them.

Numbers in this document are **illustrative, not balanced**. Every one states its unit.

---

## 1. Scope

### This lane owns

- **The frame concept** — `humanoid` · `plant` · `hybrid`, and the rule that frame is not faction.
- **The role list** — the frame-neutral names that go in `effect_container.slot` and
  `effect_binding.slot`.
- **The per-frame slot vocabularies** — the fiction words a player reads (`head` / `crown`).
- **Hybrid slot rules** — which roles a hybrid loses, and the either-frame acceptance rule.
- **Per-role affix budget weighting** — the relative weight table that stops slots being interchangeable.
- **The role–family legality table** — which affix families may land on which role on which frame. This
  is a *proposal I8 consumes*, not a claim on the affix library.
- **Slot unlocking by level** — which roles start open and when the rest arrive.
- **The commander's slot set**, including the `standard` role and its match-scoped binding.

### This lane does NOT own

| Thing | Lane |
|---|---|
| Sockets inside an item, and inserts | **I4** — never called a slot here |
| Which affix families exist, their tier bands, and the pool | **I8** — I2 says only *where they may land* |
| Base types, implicits, base stats | **I3** |
| Requirements to equip, and the gate that enforces them | **I11** — I2 publishes the tables the gate reads |
| The rarity ladder and its ordinals | **I1** |
| Post-drop mutation | **I6** |
| Turning a drop into an instance | **I12** |
| Bags, stacking, comparison | **I13** |
| What attack an actor makes | the [action program](../action-map.md) — see §3.6 |

---

## 2. The model

### 2.1 Three keys, and only the first is mine

An item finds its way onto a body through three independent keys:

```text
frame   — what kind of body this is        (humanoid | plant | hybrid)   ← I2
role    — which position on that body      (armament-primary, core-guard, …)  ← I2
faction — plant or zombie allegiance       (element rings, capture side)  ← not I2, not the item system
```

`DemonSpeciesDef.Side` carries faction **and** body in one field —
`src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs:11` documents it as *"linked capture side (plant |
zombie) — portrait/body source"*, and `Validate` at `:76` rejects anything but `plant|zombie`. The
generated roster contains zombie-side entries with plant bodies (`peashooterzombie`, `cherrynutzombie`).
`rpg_unique_actors.side` repeats the conflation at row level
(`src/FusionRpg.Data/Sqlite/RpgStore.cs:340`).

**Frame is therefore a new declared field, never a function of `Side`.** §5.5 states what has to be
added and who reads it.

### 2.2 The role is the id; the vocabulary is display

One value goes in `effect_container.slot` and `effect_binding.slot`, and it is the **frame-neutral
`role_id`**. `head` and `crown` are display strings looked up from `(role_id, frame)`.

This is the single most load-bearing decision in the lane. If the display word went in the column, an
affix pool authored for helmets would have to be authored twice, and `effect_container_pool`'s
one-per-group rule would be evaluated against two disjoint universes. One role id keeps the affix
library authored once, exactly as [item-ideal.md](../item-ideal.md) §5.1 intends.

### 2.3 The fifteen roles

Expanded from the ideal's twelve. The three additions and one redefinition are justified in §2.4.

| # | `role_id` | Humanoid | Plant | Weight ‰ | What the role is for |
|---:|---|---|---|---:|---|
| 1 | `armament-primary` | `main-hand` | `muzzle` | **160** | The identity slot — raw attack, elemental power, shield penetration. What this actor hits with. |
| 2 | `core-guard` | `torso` | `stem` | **120** | The hit-point pool itself, and the only role that may roll a `More` multiplier on `maxHp`. |
| 3 | `ward-array` | `shoulders` | `sheath` | **90** | The depleting outer layer — shield capacity, toughness, regeneration, and the two vanilla armour layers. Everything that is spent *before* HP. |
| 4 | `armament-secondary` | `off-hand` | `thorn` | **80** | The answering half — reflect, block, parry, or a second armament. Defensive or amplifying, never a duplicate of the primary. |
| 5 | `jewel-major` | `neck` | `pollen` | **80** | The strongest single non-weapon affix source; the one place a build's signature stat can be maxed outside the weapon. |
| 6 | `manipulator` | `hands` | `leaves` | **70** | Rate and follow-through — attack interval, crit damage, on-hit damage riders. How often and how hard, as opposed to how accurately. |
| 7 | `mantle` | `back` | `canopy` | **60** | Elemental mitigation — the seven `combat.defense.*` wards and status cleansing. The resistance home. |
| 8 | `head-guard` | `head` | `crown` | **60** | Resistance to being *disabled* — crit resist, crit-damage padding, status resist and immunity. Not a small torso. |
| 9 | `girdle` | `waist` | `soil` | **60** | The resource role — the five actor resource pools, their regeneration, and the economy families (sun, money). |
| 10 | `sense` | `face` | `bract` | **50** | Finding the opening — accuracy and crit *rate*. Paired with `manipulator`, deliberately split. |
| 11 | `footing` | `feet` | `roots` | **50** | Standing — humanoid: evasion, movement, initiative. Plant: stability, regeneration, resource draw. Frame-split by design (§2.6). |
| 12 | `infusion` | `bandolier` | `glands` | **50** | What your hits *inflict* — the 21 `status.apply` families and status power. Coating a blade, or secreting a toxin. |
| 13 | `retinue` | `horn` | `runner` | **40** | What else is on the board because of you — spawns, board actions, grid and terrain operations. |
| 14 | `jewel-minor-a` | `ring-1` | `graft-1` | **15** | Degree, not direction. Small, tier-capped, deliberately weak (§2.5). |
| 15 | `jewel-minor-b` | `ring-2` | `graft-2` | **15** | Identical twin of 14, and that is the point and the cost (§2.5). |

Weights are **integer per-mille of one fully-geared pure frame's total gear budget**, per SC4. They sum
to 1000. They are ratios, not points — the design does not require the power model to exist (SC9).

Commander-only, on top of the fifteen: **`standard`** (humanoid `banner`, plant `root-totem`) — §2.8.

### 2.4 Why fifteen, and what changed

Three roles were added because three large clusters in
[atom-family-library.md](../effect-atom/atom-family-library.md) had no home, and one role was
redefined because it duplicated another.

| Change | Cluster it houses | Families | Why it is not a filler slot |
|---|---|---|---|
| **+ `ward-array`** | the depleting layer | `shield_capacity` · `shield_toughness` · `shield_regen` · `warded` · `plating` · `carapace` | Shields are a shipped per-actor system with their own capacity, drain order, and element matrix. They are not hit points and they are not mitigation percentage; they are a separate resource that is consumed first. Folding them into `core-guard` would put two unrelated survivability currencies in one budget. `plating`/`carapace` (the vanilla `arm1`/`arm2` fields) belong here for the same reason: they deplete. |
| **+ `infusion`** | status offense | 21 `status.apply` families + `affliction` | The single biggest cluster in the library, and the ideal's twelve had nowhere for it. "What my hits inflict" is a different build axis from "how hard my hits land"; a build that stacks freeze is not a build that stacks crit. |
| **+ `retinue`** | the board | `summoner` · `gardener` · `volley` · `cherry_bloom` · `dooming` · `firelining` · `flash_freeze` · `gravemaking` · `gravedigging` · `terraforming` | Summoning is this game's core verb ([action-map.md](../action-map.md) §10.6). Board and grid operations are the PvZ-native content that makes this not a generic ARPG, and they were homeless. **This is the weakest of the three** — see the honest risk in §8.4. |
| **~ `head-guard` redefined** | disable resistance | `stoicism` · `padding` · `stalwart` · `immunity` · `cleansing` | The ideal called it *"the classic helm budget — defence with a small offensive rider"*, which is a smaller `core-guard` taking the same affixes. Two roles with one family list is the merge case the brief names. Head now owns *not being taken out of the fight*: crit resist, crit-damage padding, status resist and immunity. Distinct list, distinct decision. |

The suffixed ids from the ideal (`head-protective`, `sense-utility`, `mantle-utility`,
`manipulator-offense`, `girdle-resource`) are shortened, because the suffix encoded a budget category
into the id and `head-protective` would now be a lie. `footing`, `jewel-major`, `jewel-minor-*`, and
`armament-*` keep their names.

### 2.5 The twin minor jewels — the duplicate, priced

Two identical roles is the "symmetric duplicate doubles the strongest affix" failure by construction.
It is kept anyway, because expressing *degree* ("how much of this axis do I want") is worth a slot, and
it is priced with three shipped mechanisms rather than a special rule:

1. **Budget.** 15‰ each — 3% of the body between them, the smallest pair on the frame.
2. **Tier cap.** `item_role_family.max_tier = 3` for every family on both minor jewels, against `5` on
   `jewel-major`. Doubling a T3 affix does not reach one T5 affix.
3. **Legality.** The six strongest families — `bulwark`, `savagery`, and the four shield families — are
   **absent from the minor-jewel family list on every frame**. You may double a mid affix; you may not
   double a top one.

They are otherwise identical, and no attempt is made to differentiate them by flavour. An asymmetric
pair ("ring-1 is offense, ring-2 is defense") was considered and rejected: it destroys the degree
expression that is the pair's only justification, and then two roles exist for no reason at all.

### 2.6 The plant vocabulary — five categories, not swapped words

A plant is rooted, handless, and does not walk. The test applied to every plant word was:

> **Can a rooted, handless thing *possess* this, or would it have to *put it on*?**

A plant does not wear. It **grows**, is **potted in**, **secretes**, is **grafted with**, and carries
**apparatus** it grew. Every plant slot is one of those five, and I3's base-type naming should inherit
the same rule.

| Category | Plant slots | The fiction |
|---|---|---|
| **Growth** | `crown` · `bract` · `stem` · `canopy` · `leaves` · `sheath` · `runner` | Parts the plant produced. `sheath` is the bud sheath — the outer layer that takes damage and is shed, which is precisely what a shield does. `runner` is the stolon, the horizontal stem that propagates daughter plants — an exact botanical word for a summon slot. |
| **Substrate** | `roots` · `soil` | What it stands *in*, not what it walks *on*. `soil` is the pot, the bed, the earth it occupies; it is the resource slot because that is where a plant draws from. |
| **Secretion** | `pollen` · `glands` | What it emits. `pollen` is the aura-grade amulet; `glands` (nectaries, resin ducts) is what it puts into a wound. |
| **Graft** | `graft-1` · `graft-2` | Grafted cuttings and scions — the botanically correct way a plant acquires a foreign part, which is exactly what a ring is. |
| **Apparatus** | `muzzle` · `thorn` | The nozzle, seedpod, or barrel it fires from, and the spines it answers with. |

**What breaks immersion, and what was rejected:**

| Rejected | Why it reads as a costume |
|---|---|
| `leaf-gloves`, `vine-gauntlets` | A plant has no hands. Gloves imply hands under them. `leaves` *are* the manipulators. |
| `root-boots`, `bark-greaves` | Footwear implies walking. `roots` is not where a plant puts shoes; it is what a plant *is* at the bottom. |
| `petal-cape`, `leaf-cloak` | A cape is worn over a back. A plant's overhead spread is a `canopy` it grew. |
| `thorn-sword`, `bramble-axe` | A sword is gripped. A plant's weapon is the aperture it fires through — `muzzle`. |
| `sun-belt`, `vine-sash` | A belt cinches a waist. A plant's economy comes from below: `soil`. |
| `flower-hat`, `leaf-helm` | Headwear implies a head to protect. The `crown` is the bloom itself; armouring it means the bloom is tougher, not that it wears a hat. |
| `pollen-mask` for `sense` | A mask covers a face. `bract` — the leaf collar around a bloom — is a real structure that surrounds the sensing part. |

The one place the fiction had to bend: `bandolier` and `horn` on the humanoid side are objects at a
position rather than body parts. That is acceptable because a humanoid *does* carry things; the
equivalent bend on the plant side would not be.

### 2.7 Main-hand and off-hand

**Two-handed items: yes.** A base type declares `hands ∈ {1, 2}` on I3's record. A two-handed item is
**one** `effect_binding` with `slot = 'armament-primary'` and a **reservation** on
`armament-secondary` — never two binding rows, which would double-count its atoms onto the actor's
effect list. Attempting to bind an off-hand while a 2H is equipped rejects `SlotOccupied`.

Its budget is the sum of the two roles it consumes: **240‰**. It is not given a fudge discount, because
the mechanism already prices it: a 2H draws from **one pool**, and
`effect_container_pool.group` defaults to `(family_id, variant)`, so it can never roll the same family
twice. A 1H + off-hand pair is two pools and *can* — two independent instances of `might`, at two
independent rolls. The 2H trades that redundancy for one large, coherent item. Its legality list is the
**union** of the two roles' lists.

**Dual-wield: no, and not later without a frame answer.** The plant frame has no dual-wield fiction — a
rooted tower does not hold two nozzles — so dual-wield would be a humanoid-only rule, and OD2 requires
parallel roles across the two pure frames. What is allowed instead costs nothing: **`armament-secondary`
accepts an armament base type.** A second blade or a second nozzle sits in the off-hand, rolls the
off-hand's family list at the off-hand's 80‰ budget, and needs no new rule. It looks like dual-wield and
is priced like an off-hand.

### 2.8 The seam: what a weapon decides

> **A weapon supplies numbers and legality. It never supplies activation.**

Items have no behaviour; actors do ([definitions.md](../effect-atom/definitions.md) §0). The main-hand
item's atoms are `stat.modify`, `stat.derived`, and on-hit riders — magnitudes and conditions. Whether
an actor's basic attack becomes a cleave, a projectile, or a three-target volley is an **action**, and
by the action layer's membership rule (*anything an actor does that interacts with the environment or
itself, costs resource or time, and needs a cooldown*) that is theirs, not mine.

The seam, declared and not designed: I3's base-type record may carry a nullable **`grants_action_id`**.
I2 asserts only that the column is legal on `armament-primary` and `armament-secondary` and on no other
role. Everything about what it means — activation, cost, targeting, cooldown, whether unarmed has a
default action — is the action layer's, and until it ships **weapons are numbers only**. That is a
shippable state, not a stub: a main-hand at 160‰ is already the most valuable item on the body without
changing what the actor does.

### 2.9 The commander

The commander gets the **full fifteen of their chosen frame** (they are human, plant, or zombie, per
OD1, so they take `humanoid` or `plant` — never `hybrid`), **plus one** commander-only role.

**`standard` — humanoid `banner`, plant `root-totem`. One slot, not several.**

Its atoms bind at **`match` owner scope** rather than to the commander's body, so they reach the whole
squad. That scope exists today (`match`, `owner_key = ''`) and needs no new mechanism.

Three reasons it is exactly one:

1. **It is already the strongest slot in the game and cannot be made stronger.** G8 makes `warding` and
   `resilience` — flat and increased `defense` — legal at **`match` scope only**; any other scope
   rejects `ScopeUnsupported`. So `standard` is the *only* position in the entire fifteen-plus-one where
   a `+defense` affix does anything at all (§5.4). One such slot is a signature. Two is a stat tax.
2. **Squad size multiplies it.** A match-scoped atom worth *X* on one actor is worth roughly *X × squad
   size*. At a five-actor squad, `standard`'s nominal 100‰ is ~500‰ effective — half a body again.
   Several such slots stack side-wide multipliers, which is the symmetric-duplicate failure at the worst
   possible scale.
3. **One slot makes commander itemisation a choice.** Fifteen body slots plus one decision about what
   the *squad* gets is legible. Four squad slots is just another body.

Priced accordingly: **`pool_rolls ≤ 3`**, **`max_tier = 3`**, nominal budget **100‰ of a separate
commander budget**, not drawn from the 1000‰ body total. Its family list is short and hand-picked, not
inherited from any body role.

### 2.10 Slot unlocking as progression

Roles open on **actor level**, read from `rpg_unique_actors.level`
(`src/FusionRpg.Data/Sqlite/RpgStore.cs:343`) — the column already exists and already advances.

A new specimen starts with **four** open roles, not fifteen. This directly answers the ideal's §8
worry that twenty demons × twelve slots is a gearing chore: a bench specimen at level 1 needs four
items, and only the actors you actually level reach fifteen.

| Level | Role opened | Frame note |
|---:|---|---|
| **1** | `armament-primary` · `core-guard` · `head-guard` · `jewel-minor-a` | 355‰ of the budget at level 1 — early loot matters immediately |
| 3 | `armament-secondary` | |
| 5 | `manipulator` | |
| 8 | `footing` | |
| 11 | `girdle` | |
| 14 | `sense` | |
| 17 | `mantle` | |
| 20 | `jewel-major` | the first "chase" unlock |
| 24 | `ward-array` | **hybrid: never** |
| 28 | `infusion` | |
| 32 | `retinue` | hybrid's last unlock |
| 36 | `jewel-minor-b` | **hybrid: never** |

**It stops at 36.** Nothing unlocks after that. There is no level cap in the tree today — the XP curve
in `src/FusionRpg.Core/Progression/RpgProgression.cs:32` is unbounded arithmetic — so this is a request
to whoever sets one: **the cap must sit meaningfully above 36** (≥ 50 is the shape I would pick), so the
last third of levelling is driven by stats and content rather than by slot count. If the cap lands at
40, the ladder must be compressed; the table is data (§5.2) so that is a row edit, not a code change.

Hybrids finish their ladder at 32 instead of 36 — a small consolation for the budget cut in §4.2.

---

## 3. Options considered, and the recommendation

### 3.1 How many roles

| Option | Tradeoff |
|---|---|
| **A. Keep the ideal's twelve.** | Cheapest. But three large family clusters (shields, status offense, board) have no home, and `head-protective` duplicates `core-protective`'s list. Dead families are the `status.expose.*` scar repeated at the role layer. |
| **B. Fifteen — twelve plus three cluster homes, with `head-guard` redefined.** ← **picked** | Every role has a family list nothing else takes. Satisfies OD2's "approximately 15" honestly rather than by padding. Costs one more role than the ideal to gear, mitigated by the unlock ladder (§2.10). |
| **C. Twenty-plus, with paired symmetric slots (two rings, two bracers, two boots).** | Reaches the D&D/MMO slot density some players expect, but every symmetric pair is a duplicate affix list, and the pair problem in §2.5 is hard enough with one pair. Rejected outright. |

**B.** The test applied was the brief's own: *if two roles would take the same affixes, merge them and
add a better one.* That is exactly what happened to `head-protective`.

### 3.2 How hybrid is priced

| Option | Tradeoff |
|---|---|
| **A. Fifteen roles, a global budget scalar** (hybrid rolls at ×0.9). | Precise and tunable. But "hybrid budget multiplier" is a second, invisible mechanism the player never sees, and it makes every hybrid item strictly worse than the identical pure-frame item — which reads as a bug, not a cost. |
| **B. Thirteen roles, each accepting either frame's base type.** ← **picked** | The cost is visible on the character sheet: two empty positions a pure frame has. Every item a hybrid *can* wear is exactly as good as a pure frame's. Uses OD3's own shape. |
| **C. Fifteen roles, hybrids barred from set and unique items.** | Punishes the wrong axis — it makes hybrids worse at the *content* rather than at the *body*, and set legality is I5's to enforce, not mine. Also creates a per-frame loot filter, which is a UI problem for every player. |

**B**, with the two dropped roles and the arithmetic in §4.2.

### 3.3 How "+movement speed" is stopped from rolling on a turnip

| Option | Tradeoff |
|---|---|
| **A. A `frames` column on the affix family.** | One column, cheap. But legality is genuinely per *(role, frame)*, not per family: `regeneration` is legal on plant `roots` and on humanoid `girdle` but not on humanoid `feet`. A family-level column cannot express that, and the family table is I8's anyway. |
| **B. A role–family legality table, `item_role_family(role_id, frame, family_id, max_tier)`.** ← **picked** | Expresses the real shape, carries the tier cap the minor jewels need, and is pure data with two named consumers. Costs one table and a load-time check. |
| **C. A predicate on the atom (`frameIs` leaf).** | Would work at runtime, but it is the wrong phase entirely: an illegal affix should never *roll*, not roll and then evaluate false. It would also add a leaf to the predicate vocabulary for an authoring problem. |

**B.** Its consumers, named per SC7: **I8's pool author and validator** (a container whose pool row
names a family not legal for its `slot` + frame is rejected `RoleFamilyIllegal` at load) and **I12's
drop generator** (which picks only from legal rows). **I11's equip gate** is defence in depth, not the
primary enforcement.

### 3.4 Where the role list lives

Covered in full in §5.6, because SC7 deserves a substantive answer rather than a one-line pick. The
short form: **frame is code, role is data, and role is only honestly data once three consumers stop
hardcoding.**

---

## 4. Budget weighting, and the hybrid proof

### 4.1 Weight becomes shipped columns

A per-mille weight is not a schema concept. It becomes one through a band table that I1 and I8 consume
to set `pool_rolls` and the `min_tier`/`max_tier` window — all four already columns on
`effect_container`.

| Weight ‰ | `pool_rolls` at Rare | `max_tier` | Roles in band |
|---:|---:|---:|---|
| ≥ 200 | 8 | 5 | two-handed `armament-primary` (240‰) |
| 120–199 | 6 | 5 | `armament-primary` · `core-guard` |
| 70–119 | 5 | 5 | `ward-array` · `armament-secondary` · `jewel-major` · `manipulator` |
| 45–69 | 4 | 4 | `mantle` · `head-guard` · `girdle` · `sense` · `footing` · `infusion` |
| 30–44 | 3 | 4 | `retinue` |
| < 30 | 2 | 3 | `jewel-minor-a` · `jewel-minor-b` |

Rarity still picks count and tier window and never touches magnitude — the band table only says *which
window this role is allowed to reach*. I1 owns the rarity ladder; this table is the role-side input to
it.

### 4.2 Hybrid: what is dropped, and the arithmetic

**A hybrid has 13 roles. It loses `ward-array` (90‰) and `jewel-minor-b` (15‰).**

**Either-frame acceptance rule:** for a hybrid actor, a base type is legal in role *R* if
`base.role_id == R` **and** `base.frame ∈ {humanoid, plant, either}`. No per-species assignment, no
per-role frame lock. One rule, checkable in one line.

Why those two:

- **`ward-array`** because a chimera has no coherent outer layer — a body that is half bark and half
  bone does not shed a single sheath. It is also the most expensive drop available that is not an
  armament, which is what makes the price land.
- **`jewel-minor-b`** because a second graft onto a body that is already two things does not take.

**The shield families are not lost, only the slot is.** `shield_capacity`, `shield_toughness`,
`shield_regen`, and `warded` become legal on **`core-guard` for the hybrid frame only**, at
`max_tier = 3` against `ward-array`'s `5`. That adds no budget — `core-guard` is still 120‰ — it adds
*competition inside* a fixed budget. A hybrid may have shields or hit points, not both at full strength.
This is the legality table (§3.3 option B) doing the job it was proposed for.

**Proving the pricing.**

| | Pure frame | Hybrid |
|---|---:|---:|
| Roles | 15 | 13 |
| Budget ‰ | 1000 | **895** |
| Affixes on a full Rare set | **63** | **56** |
| Base types visible per role | *N* | *2N* |

Hybrid roll count, from §4.1's bands: 6 + 6 + 5 + 5 + 5 + 4 + 4 + 4 + 4 + 4 + 4 + 3 + 2 = **56**.
56 / 63 = **88.9%**, matching the 895‰ budget ratio — the two derivations agree, which is the check.

**Is a 10.5% cut fair for double the loot pool?** The breadth gain is a *selection* effect, not a power
grant. For roughly uniform item quality, the expected best of *N* candidates is *N/(N+1)* of the range;
at *N* = 10 that is 0.909 and at *N* = 20 it is 0.952 — a **~4.7%** lift in expected item quality, and
the lift shrinks as *N* grows. So doubling the pool is worth roughly **5%** of power and a large,
unmeasurable amount of *convenience* (every drop is useful to this actor; the bag fills slower).

A 10.5% cut therefore over-prices the power and correctly prices the convenience. That asymmetry is
deliberate: convenience at roster scale is the thing that actually makes a frame the only one worth
playing, and it is the failure the ideal §5.3 flagged. Overshooting by ~5% is the cheaper error — a
hybrid that is 89% of a pure frame is a real choice, while a hybrid at 100% is the only choice.

*(The 4.7% figure is order-statistics arithmetic on a uniform assumption, not a measurement. Real drop
quality is not uniform. Recheck it against I12's actual drop distribution before it is used to tune.)*

---

## 5. Data shape

### 5.1 What is reused, unchanged

| Existing column | I2 writes | Note |
|---|---|---|
| `effect_container.slot` | the **`role_id`**, for `container_kind = 'item'` | never the display word |
| `effect_binding.slot` | the same `role_id` | equipping is a binding, per item-ideal §6.4 |
| `effect_container.min_tier` / `max_tier` | the window from §4.1's band table | rarity × role, both inputs |
| `effect_container.pool_rolls` | the count from §4.1's band table | |
| `effect_container.level_req` | I3's, but I2's unlock ladder is a *separate* gate — see §5.3 | |
| `effect_binding.owner_kind` / `owner_key` | `match` for `standard`; **unresolved for body slots** — §7.1 in the asks | |

**No change is requested to any effect-atom table.** Everything below is new tables in the item
program's own space.

### 5.2 New tables

```sql
-- The role list. One row per role, frame-neutral.
item_role(
  role_id      TEXT PRIMARY KEY,   -- kebab-case, stable, referenced by every downstream lane
  ord          INT NOT NULL,       -- display order, explicit and append-safe (the elements rule)
  commander_only INT NOT NULL DEFAULT 0,
  enabled      INT NOT NULL DEFAULT 1,
  revision     INT NOT NULL DEFAULT 0
);

-- Per-frame presence, vocabulary, budget, and unlock level. This one table carries the
-- hybrid drop (present = 0), the hybrid budget, and the whole unlock ladder.
item_role_frame(
  role_id         TEXT NOT NULL,
  frame           TEXT NOT NULL,   -- humanoid | plant | hybrid
  present         INT  NOT NULL,   -- 0 = this frame does not have this role
  display_slot    TEXT NOT NULL,   -- 'head' | 'crown' | ...
  fiction_note    TEXT,            -- the §2.6 category, for authors and tooltips
  budget_permille INT  NOT NULL,
  unlock_level    INT  NOT NULL DEFAULT 1,
  PRIMARY KEY (role_id, frame)
);

-- Which affix families may land on which role on which frame, and how high they may tier.
-- This is a PROPOSAL I8 consumes; I8 owns the families themselves.
item_role_family(
  role_id   TEXT NOT NULL,
  frame     TEXT NOT NULL,
  family_id TEXT NOT NULL,         -- atom.<kebab>, per definitions §1
  max_tier  INT  NOT NULL,
  PRIMARY KEY (role_id, frame, family_id)
);
```

**Consumers, named per SC7 — no table ships without one:**

| Table | Consumers |
|---|---|
| `item_role` | the equip gate (I11); the roster slot grid; I12's drop-role picker |
| `item_role_frame` | the equip gate (`SlotLocked`); the slot allowlist replacing `UniqueEquipmentCatalog.NormalizeSlot`; I1's rarity → (`pool_rolls`, tier window) mapping; the web slot grid's labels |
| `item_role_family` | I8's pool validator (`RoleFamilyIllegal` at load); I12's drop generator |

These three tables should join the content-hash registry (definitions §8) when they land, which is an
explicit `contentHashSchemaVersion` bump, not a silent addition.

### 5.3 Two level gates, and they are different

`effect_container.level_req` asks *"is this actor high enough to use this item?"* — I3's and I11's.
`item_role_frame.unlock_level` asks *"does this actor have this position yet?"* — mine. A level-2 actor
holding a level-1 off-hand fails the second and not the first. Both reject; the reason codes differ
(`LevelTooLow` vs `SlotLocked`) so the player is told the truth.

### 5.4 One consequence worth stating loudly

**`warding` and `resilience` appear in no role's family list, on any frame.** G8 (definitions §6,
atom-family-library §4.1a) makes `stat.modify` on `defense` legal **only at `match` scope**; the
`TakeDamage` prefix reads one side-wide cached value, so `plant:N`, `zombie:N`, and `entity:` all
reject. A `+armour` affix on a body slot is not weak — it is *silently dead*.

So the two flat-defence families are **commander `standard` only**, and every other mitigation on a body
goes through `elemental_defense` (`mantle`) or the shield stack (`ward-array`). This is not a design
preference; it is what the shipped prefix does. It changes if and when perf **O5** lands a per-ptr
resolve cache, and not before.

### 5.5 Frame needs a home, and it does not have one

| Where | Today | Needed |
|---|---|---|
| `DemonSpeciesDef` | `Side` only, `plant\|zombie`, documented as body *and* faction (`DemonSpeciesCatalog.cs:11`, validated `:76`) | a new `Frame` field, its own validation, set at generation — never computed from `Side` |
| `rpg_unique_actors` | `side TEXT` (`RpgStore.cs:340`) | a `frame TEXT` column, backfilled from the species record once |
| the commander | does not exist as an entity at all (item-ideal §3) | frame declared at creation |

The backfill is one-way and mechanical for the pure cases; the Fusion crossbreeds
(`peashooterzombie`, `ironpeazombie`, `cherrynutzombie`, `bucketnutzombie`) are the rows that need a
human decision, and they are the reason the field cannot be derived.

### 5.6 Data or code — applying SC7 honestly

> SC7: *a thing can be data if adding a row changes behaviour without new code.*

The answer splits three ways, and the split is the point.

**Frame is CODE — a closed enum of three.** Adding a fourth frame row changes nothing on its own: it
would need a vocabulary (15 display words), a budget column set, an unlock ladder, a legality list per
role, a hybrid-style pricing decision, base types from I3, and an art pass. Every one of those is new
authoring or new code, so by SC7's own test frame fails to be data. Making it a table would be the
`status.expose.*` mistake in a new place — a legal, registered, fully-valid frame with nothing behind
it. Three values, an enum, and adding a fourth is a design conversation.

**The role list is DATA — but not yet.** Adding an `item_role` row *should* change behaviour with no
new code. Today it would not, because three consumers hardcode the set:

| Consumer | Hardcode | Must become |
|---|---|---|
| `UniqueEquipmentCatalog.DefaultSlots` = `{ "weapon", "armor", "trinket" }` (`UniqueEquipmentCatalog.cs:12`), enforced by `NormalizeSlot` throwing at `:50` | a static array | a lookup against `item_role_frame` for the actor's frame and level |
| `RosterPage.tsx:33` — `const EQUIP_SLOTS = ["weapon", "armor", "trinket"] as const` | a TS literal | the role list, served with the equipment payload |
| the affix pool author | there is no author yet | reads `item_role_family` |

So the honest SC7 answer is: **role is data, conditional on those three becoming table-driven, and
until they are it is data-shaped code.** Saying "it's data" while a TypeScript literal decides what the
player sees would be exactly the lie in a table that SC7 exists to prevent. The migration in §5.7 makes
the first two true; the third is I8's.

**The guard that keeps it honest:** an `item_role` row with **no `item_role_family` rows on any frame**
is rejected at load as `UnsatisfiablePool` — the existing code, reused, because the failure is
identical: a position that can never be filled with anything. That single check is what stops the
"slots that exist only to be filled" failure mode from ever reaching a player.

**Budget weights, vocabulary, and unlock levels are DATA**, unconditionally — each is a number or a
string read by a generic consumer, and changing one is a balance edit.

### 5.7 Retiring the three-slot allowlist without breaking the equip flow

What exists today, verified:

- `rpg_unique_equipment(instance_id, slot, item_id)`, PK `(instance_id, slot)`
  (`src/FusionRpg.Data/Sqlite/RpgStore.cs:356-361`).
- `UpsertUniqueEquipment` validates through `UniqueEquipmentCatalog.NormalizeSlot` and `IsKnownItem`
  (`src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs:626,629`), then calls
  `RebuildUniqueModsFromEquipmentUnlocked` (`:658`) which folds grants into `mods_json` via
  `BuildModsJson` (`:693`).
- REST is `GET /actors/{id}/equipment`, `PUT|DELETE /actors/{id}/equipment/{slot}`
  (`src/FusionRpg.Server/UniqueActorEndpoints.cs:79,85,100`).
- Three stub items exist, one pointing at a placeholder effect id
  (`UniqueEquipmentCatalog.cs:23-26`).

Three steps, and no step breaks the flow:

**Step 1 — alias, do not rename.** Seed `item_role` and `item_role_frame`, and add a nullable
`legacy_alias` column to `item_role` carrying the three old strings:

| Legacy slot | `role_id` |
|---|---|
| `weapon` | `armament-primary` |
| `armor` | `core-guard` |
| `trinket` | `jewel-minor-a` |

Existing `rpg_unique_equipment` rows keep their old `slot` values and keep resolving. The PK
`(instance_id, slot)` already generalises — fifteen roles is fifteen rows, and the table needs **no DDL
change**. The REST contract does not change. Nothing observable moves.

**Step 2 — widen the allowlist from the table.** `NormalizeSlot` stops consulting a static array and
consults `item_role_frame` filtered by the actor's frame and level, accepting the `legacy_alias` values
as well as the canonical `role_id`s. New rejections are `RoleUnknown` and `SlotLocked`; the endpoint
shape is unchanged, so the existing 400/409 handling at `UniqueActorEndpoints.cs:95,108` still applies.
The web page's `EQUIP_SLOTS` literal is replaced by the role list **returned inside the existing
equipment GET payload** — no new endpoint, no second query, and the page stays one fetch.

**Step 3 — swap the effect path, not the equip path.** `RebuildUniqueModsFromEquipment` stops folding
`grants` into `mods_json` and starts creating and withdrawing `effect_binding` rows. E6 already
specifies this migration for the `grants` half, and `absolutes` stay where they are by contract.
`UniqueEquipmentCatalog.Items` — the three stubs — is retired by I3's base types and I12's instances,
and `rpg_unique_equipment.item_id` is joined by a new nullable `instance_id` column, backfilled one-way.
That new column is the **only real schema change** in the whole retirement.

**One blocker on step 3, and it is not mine to fix.** The stub grants
`OwnerKind = "instance"`, `OwnerKey = "instance:pending"` (`UniqueEquipmentCatalog.cs:124-125`).
`instance` is **not one of the seven owner scopes** — the seven are `match`, `plant:N`, `zombie:N`,
`entity:hex`, `player:N`, `sector:id`, `slot:id`. And `entity:` is explicitly *session-scoped and never
durable*. There is therefore **no owner scope a durably equipped item can bind to today.** See §7.1.

---

## 6. Validation and reason codes

Reused from the closed 33 wherever one fits, per SC6.

| Bad input | Reason code | Phase | New? |
|---|---|---|---|
| Base type names a `role_id` not in `item_role` | `RoleUnknown` | load | **new** |
| Base type's frame not accepted by the wearer's frame | `FrameMismatch` | equip | **new** |
| Pool row names a family not in `item_role_family` for this role + frame | `RoleFamilyIllegal` | load | **new** |
| Equipping into a role whose `unlock_level` exceeds the actor's level | `SlotLocked` | equip | **new** |
| Equipping into a role the frame does not have (`present = 0`), or commander-only on a non-commander | `SlotLocked` | equip | reused above |
| Equipping into an occupied role, or the off-hand while a two-handed item is bound | `SlotOccupied` | equip | **new** |
| `item_role` row with no `item_role_family` row on any frame | `UnsatisfiablePool` | load | reused — identical failure |
| Pool atom above the role's `item_role_family.max_tier` | `TierOutOfWindow` | load | reused |
| Item's `level_req` above the actor's level | `LevelTooLow` | equip | reused |
| A body-slot binding at `entity:` scope expected to survive a restart | `ScopeUnsupported` | bind | reused — and see §7.1 |
| `warding` / `resilience` bound anywhere but `match` | `ScopeUnsupported` | bind | reused — G8 |
| `item_role_frame` row for an unknown frame string | `BadParamValue` | load | reused |

**Five new codes** against a closed list of 33: `RoleUnknown`, `FrameMismatch`, `RoleFamilyIllegal`,
`SlotLocked`, `SlotOccupied`. The three equip-phase codes (`FrameMismatch`, `SlotLocked`,
`SlotOccupied`) are enforced by **I11's gate**, so I11 registers them; I2 proposes them. `RoleUnknown`
and `RoleFamilyIllegal` are load-phase and belong with the importer.

`RoleHasNoFamilies` was drafted and dropped — `UnsatisfiablePool` already means exactly "this cannot
ever be filled", and adding a sixth code to say the same thing is how a closed list stops being closed.

---

## 7. Worked examples

All numbers illustrative, not balanced. Units stated on every line.

### 7.1 A level-40 pure humanoid, fully geared in Rare

| Role | Weight ‰ | `pool_rolls` | `max_tier` | A plausible roll |
|---|---:|---:|---:|---|
| `armament-primary` (1H) | 160 | 6 | 5 | `might` T5 (+120 atk, game units) · `ferocity` T4 · `elemental_power.fire` T5 (+50 resolver points) · `searing_strike.fire` T4 (300–450 hp damage on hit) · `shield_pen` T3 · `keen_edge` T3 |
| `core-guard` | 120 | 6 | 5 | `vitality` T5 (+400 hp) · `fortitude` T4 · `bulwark` T3 · `mending` T2 · `elemental_defense.earth` T3 · `evasion` T2 |
| `ward-array` | 90 | 5 | 5 | `shield_capacity.omni` T5 · `shield_toughness` T4 · `shield_regen` T3 · `plating` T3 · `warded.ice` T2 |
| `armament-secondary` | 80 | 5 | 5 | `retribution` T4 · `shield_capacity.fire` T3 · `stoicism` T3 · `might` T2 · `elemental_defense.fire` T3 |
| `jewel-major` | 80 | 5 | 5 | `elemental_power.fire` T5 · `cruelty` T4 · `vitality` T3 · `precision` T3 · `lifesteal` T2 |
| `manipulator` | 70 | 5 | 5 | `quickening` T5 · `cruelty` T4 · `searing_strike.ice` T3 · `volley` T2 · `might` T3 |
| `mantle` | 60 | 4 | 4 | `elemental_defense.fire/ice/dark` T4/T3/T3 · `cleansing` T2 |
| `head-guard` | 60 | 4 | 4 | `stoicism` T4 · `padding` T3 · `stalwart` T3 · `immunity.freeze` T2 |
| `girdle` | 60 | 4 | 4 | `resource.max.stamina` T4 · `resource.max.qi` T3 · `regeneration` T3 · `midas` T2 |
| `sense` | 50 | 4 | 4 | `precision` T4 · `keen_edge` T4 · `precision.fire` T2 · `evasion` T2 |
| `footing` | 50 | 4 | 4 | `evasion` T4 · `swiftness` T3 · `resource.max.stamina` T2 · `precision` T2 |
| `infusion` | 50 | 4 | 4 | `freezing` T4 · `venomous` T3 · `affliction` T3 · `withering` T2 |
| `retinue` | 40 | 3 | 4 | `summoner` T3 · `gravemaking` T2 · `terraforming` T2 |
| `jewel-minor-a` | 15 | 2 | 3 | `might` T3 · `elemental_power.fire` T2 |
| `jewel-minor-b` | 15 | 2 | 3 | `might` T3 · `precision` T2 |
| **Total** | **1000** | **63** | | |

63 affixes across 15 items is in the same range as Diablo 2's ten slots at roughly six affixes each
*(recalled, not verified)*. The distribution is what matters: the weapon alone carries 16% of the body,
and the two rings together carry 3%.

### 7.2 The same character as a hybrid

Thirteen roles: everything above except `ward-array` and `jewel-minor-b`.

| | Pure | Hybrid | Ratio |
|---|---:|---:|---:|
| Budget ‰ | 1000 | 895 | 0.895 |
| Affixes | 63 | 56 | 0.889 |
| `max_tier` reachable | 5 | 5 | equal |
| Base types visible per role | *N* | 2*N* | 2.0 |

The hybrid's `core-guard` may now roll `shield_capacity`, `shield_toughness`, `shield_regen`, and
`warded` at `max_tier = 3` — so a hybrid can have *some* shield, at T3, competing with `vitality` T5 for
the same six pool rolls. It cannot have both a 400-hp pool and a T5 shield stack. That is the whole
trade, in one row of one table.

### 7.3 Two-handed against one-hand plus off-hand

| | 2H | 1H + off-hand |
|---|---|---|
| Budget ‰ | 240 (160 + 80) | 240 (160 + 80) |
| `pool_rolls` at Rare | 8, from **one** pool | 6 + 5 = **11**, from two pools |
| `max_tier` | 5 | 5 |
| Can roll `might` twice? | **No** — `group` defaults to `(family_id, variant)` | **Yes** — two independent instances |
| Family list | union of both roles | each role's own list |
| Items to find | 1 | 2 |

Eight big rolls on one item against eleven smaller rolls across two. The 2H is easier to complete and
more coherent; the pair has three more rolls and can stack a family. Neither is strictly better, and no
balance fudge factor was needed to get there — the one-per-group rule did it.

### 7.4 The commander's `standard`, priced

A `banner` at Rare: `pool_rolls = 3`, `max_tier = 3`, all atoms bound at `match` scope.

| Roll | Magnitude | Reaches |
|---|---|---|
| `warding` T3 | +40 defense, game units | the **whole side** — the only place this affix is not silently dead (§5.4) |
| `resilience` T2 | +80‰ defense increased | the whole side |
| `elemental_defense.fire` T3 | +30 resolver points | the whole side |

Nominal budget 100‰. With a five-actor squad the effective value is roughly **500‰** — half a body
again — which is why `pool_rolls` is 3 and not 5, why `max_tier` is 3 and not 5, and why there is one
`standard` and not four.

**A request to E9 falls out of this:** the power model prices an atom's magnitude, not how many actors
it reaches. A match-scoped atom must be priced with an expected-squad-size factor or the commander's
gear will read as the cheapest thing on the roster while being the most valuable. SC9 says I may not
depend on power, and I do not — the caps above hold without it — but the mispricing should be recorded
before E9 is fitted.

---

## 8. Failure modes

### 8.1 Slots that exist only to be filled

**The failure:** a game ships eighteen slots, four of them roll from a pool of three boring affixes, and
the player fills them once and never thinks about them again. Every one is inventory work and no
decision.

**What prevents it here:** the load-time check in §5.6 — an `item_role` row with no `item_role_family`
row on any frame is rejected `UnsatisfiablePool`. A role cannot exist before the affixes that make it
interesting exist. The §2.4 table is the receipt: every one of the fifteen has a named family cluster
nothing else takes.

**Where it can still bite:** `retinue` — §8.4.

### 8.2 Gearing a new specimen is a chore

**The failure:** the ideal names it in §8 — twenty demons × fifteen slots is 300 equipped items before
anything sits in a bag. Either most specimens go bare or inventory management becomes the game.

**What prevents it here:** the unlock ladder (§2.10). A level-1 specimen has **four** slots and 355‰ of
its budget available; the fifteenth arrives at level 36. Gearing scales with investment, so a bench
demon is a four-item job and only your actual squad is a fifteen-item job.

**Honest limit:** this softens the problem, it does not decide it. The roster-scale gear economy — shared
pools, disposable gear, or a small deployable squad — is still open and still belongs to whoever owns
the economy. What I2 contributes is that the answer no longer has to be "cut the slot count."

### 8.3 Symmetric duplicates double the strongest affix

**The failure:** two rings, both able to roll the game's best affix, so the correct build is that affix
twice and every other combination is wrong.

**What prevents it here:** three stacked limits in §2.5 — 15‰ each, `max_tier = 3`, and the six
strongest families excluded from the minor-jewel list on every frame. The pair can double a mid affix,
which is the expressiveness it exists for, and cannot double a top one.

**The same failure at commander scale** is why `standard` is one slot (§2.9), and it is the more
dangerous instance because match-scoped atoms multiply by squad size.

### 8.4 The plant vocabulary reads as a costume

**The failure:** `leaf-gloves` and `root-boots`. A humanoid slot list with botanical adjectives, which
tells the player the plant frame was an afterthought.

**What prevents it here:** the possession test and the five categories in §2.6, applied to every word,
with the rejected list written down so the next author can see the standard. `sheath` and `runner` are
the proof the method works — both are real botanical structures that happen to mean exactly what the
mechanic means, and neither has a humanoid word behind it.

### 8.5 `retinue` is the weak one, and this is the honest note

Ten families is a healthy cluster on paper. In practice most of them are lawn-facing:
`board.action`, `grid.spawn`, `grid.clear`, and `box.set` are PvZ-mode kinds, and
[atom-family-library.md](../effect-atom/atom-family-library.md) §4.2 records the demon/battle domain as
carrying only derived-channel, HP-delta, and shield families **today**. SC8 requires every mechanic to
be fully usable with the game closed, and `retinue` currently leans on families that are not.

What holds it up standalone is `summoner`, `gardener`, and `volley` — three `spawn.entity` families —
and summoning is the game's core verb, so that is not nothing. But three is thin.

**Stated plainly: `retinue` is the role I would cut first if the owner wants fourteen.** It unlocks last
(level 32) precisely so that decision stays cheap — no early-game content depends on it. The gate it
needs is a battle consumer for spawn or board families, which is E12's and the action layer's, and it is
listed as ask 7 in §9.

### 8.6 Frame silently derived from faction

**The failure:** somebody writes `frame = side == "plant" ? plant : humanoid`, ships it, and every
Fusion crossbreed in the roster gets the wrong body. Four species are wrong on day one and nobody
notices until a plant-bodied zombie is offered boots.

**What prevents it here:** §5.5 makes `Frame` a separate declared field with its own validation, and
this document states the rule in its second section. The generated roster is where the four hard cases
get their answer, once, by a human.

---

## 9. What this lane needs from other lanes

1. **E6 / the atom program — a durable per-actor owner scope.** This is the hardest blocker in the
   lane. Equipping is a binding, and a binding needs an owner key. The seven scopes are `match`,
   `plant:N`, `zombie:N`, `entity:hex`, `player:N`, `sector:id`, `slot:id`. A roster demon is none of
   them: `entity:` is *session-scoped and never durable* by contract, `plant:N`/`zombie:N` are type-wide
   (equipping one demon would equip every copy), and `player:N` is the whole account. The shipped stub
   already invented an eighth — `OwnerKind = "instance"`, `OwnerKey = "instance:pending"`
   (`UniqueEquipmentCatalog.cs:124-125`) — which parses as nothing. **An `actor:{instanceId}` scope is
   needed, and adding a scope is "ask first" under E6's boundaries.** Nothing in §5.7 step 3 can land
   without it.

2. **I3 — three fields on the base-type record.** `frame ∈ {humanoid, plant, either}`, `role_id`
   (FK to `item_role`), and `hands ∈ {1, 2}`. I2 supplies the role vocabulary and the two-handed
   reservation rule; I3 supplies the record they hang on. Also: I3's base-type naming should inherit
   §2.6's five plant categories, or the vocabulary discipline stops at the slot list.

3. **I8 — consume `item_role_family` as the pool's legality filter.** I2 proposes which families may
   land on which role on which frame and how high they may tier; I8 owns the families and their tier
   bands and is the enforcement point at load (`RoleFamilyIllegal`). If I8 declines the table, §2.6's
   "no `+move speed` on a turnip" rule has no mechanism and the whole frame-filter argument collapses to
   a convention.

4. **I1 — consume §4.1's band table.** Rarity picks `pool_rolls` and the tier window; role weight picks
   which window a role may *reach*. Those two inputs multiply, and I1 owns the resulting matrix. I2 will
   not author rarity rows.

5. **I11 — own three of the five new reason codes.** `FrameMismatch`, `SlotLocked`, and `SlotOccupied`
   fire at equip time, which is I11's gate. I2 defines when each is correct; I11 registers them against
   the closed 33 and enforces them. I11 also needs `item_role_frame` for the level check, which is a
   *different* gate from `level_req` (§5.3).

6. **I12 — pick roles from `item_role_frame`, not from a constant.** A drop generator that rolls a slot
   from a hardcoded list is the `EQUIP_SLOTS` literal in a new place. I12 also owns the drop-quality
   distribution that §4.2's 4.7% breadth estimate assumes is uniform; that estimate should be rechecked
   against the real one before it is used to tune the hybrid price.

7. **E12 and the action layer — a battle consumer for `retinue`'s families, or an owner decision to
   cut the role.** §8.5. This does not block anything before level 32.

8. **The demon / species stream — a `Frame` field on `DemonSpeciesDef` and a `frame` column on
   `rpg_unique_actors`.** Four Fusion crossbreeds need a human answer at generation time (§5.5). Frame
   must never be derived from `Side`.

9. **Whoever sets the level cap — put it meaningfully above 36.** The unlock ladder ends at 36 and the
   XP curve is unbounded today (`RpgProgression.cs:32`). A cap at 40 makes the last four levels a
   formality; the ladder is data and can be compressed, but the compression should be a decision, not a
   surprise.

10. **E9 / the power model — price match scope by squad size.** §7.4. Not a blocker; SC9 means I2 ships
    without power. It should be recorded before the coefficients are fitted, or commander gear prices as
    the cheapest and best thing in the game.

11. **The web stream — serve the role list with the equipment payload.** `RosterPage.tsx:33` hardcodes
    three slots. §5.7 step 2 proposes putting the role list inside the existing
    `GET /actors/{id}/equipment` response so the page keeps one query and no endpoint is added.

---

## 10. Open questions for the owner

1. **Fifteen or fourteen?** `retinue` is the marginal role (§8.5). Fifteen matches OD2 exactly and gives
   the board and spawn families a home; fourteen is cleaner and loses nothing that has a battle consumer
   today. I committed to fifteen and named the cut candidate rather than hedging.

2. **Which two roles does hybrid lose?** I picked `ward-array` and `jewel-minor-b` for a 10.5% budget
   cut. The alternative worth a sentence of your time is `mantle` and `jewel-minor-b` (7.5%), which is
   gentler and leaves the shield stack intact but takes elemental resistance from the frame most likely
   to face mixed damage.

3. **Is 89% of a pure frame the right hybrid price?** §4.2 argues it over-prices power (~5%) and
   correctly prices convenience. If hybrids should be *aspirational* rather than *balanced*, the number
   moves up; if they should be a starter option, it moves down. This is a product call.

4. **Does the commander wear `hybrid`?** OD1 says the commander is human, plant, or zombie and
   therefore wears one of the two pure vocabularies. I read that as *never hybrid* and designed for it.
   If a Fusion commander is on the roadmap, `standard` plus 13 roles is the shape and the budget maths
   in §4.2 still holds.

5. **Where does the level cap land?** §2.10 and ask 9. The ladder ends at 36 and I picked ≥ 50 as the
   shape; the actual number is yours and the progression stream's.

6. **Should the unlock ladder be per-actor level or per-account progression?** I chose per-actor level
   because the column exists and because it makes a fresh specimen cheap to gear. Account-wide unlocking
   would mean a level-1 demon inherits all fifteen slots — better for a roster game, worse for the chore
   problem in §8.2. I did not decide this one lightly and it is reversible: the gate reads one number,
   and which number it reads is one line.

---

## 11. Design-gate checklist

```
[x] Subsystems identified — effect-atom container/instance/binding, unique-actor equip flow,
    demon species, the action layer seam.
[x] Read this session: item-ideal.md, enrichment-contract.md, definitions.md (§1, §2, §4, §5,
    §6, §8, §9, §10), spec-container-schema.md, spec-instance-and-binding.md,
    atom-family-library.md, action-map.md §10.6.
[x] Every repo claim cites file:line — UniqueEquipmentCatalog.cs:12/50/23-26/124-125,
    RpgStore.cs:340/343/356-361, RpgStore.UniqueActors.cs:626/629/658/693,
    UniqueActorEndpoints.cs:79/85/95/100/108, RosterPage.tsx:33,
    DemonSpeciesCatalog.cs:11/76, RpgProgression.cs:32.
[x] Verified against CODE, not comments — the allowlist, the equip path, the owner-scope list,
    the species Side validation, and the absence of a level cap were all opened.
[x] Read the surrounding section of every rule quoted — G8 in both definitions §6 and
    atom-family-library §4.1a; SC7's consumer rule; the seven owner scopes in E6.
[ ] Tested (not assumed) any constraint reported. **Gap: no test suite was run.** The G8 scope
    rule, the seven-scope list, and the `group` default are read from shipped specs and code,
    not executed. Run the suite before any of them justifies a build decision.
[x] Nothing contradicts an invariant — no new atom kind, no second modifier mechanism, no
    change requested to any effect-atom table, standalone-first respected (and its one weak
    spot named in §8.5).
[x] Five new reason codes proposed, not assumed; one draft code dropped in favour of reuse.
[ ] Corrections propagated to map, plan, and tasks. **Gap: the item program has no map, plan,
    or task list yet** — reconciliation into item-ideal.md happens in one pass after all lanes
    land, per the enrichment contract.
```
