# Lane I3 SSOT — item categories, base types, and equipment base stats

**Status:** Lane I3 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

---

## 2. Scope

### This lane owns

- The **top-level item category taxonomy** — what categories exist, whether each rolls values, whether it
  stacks, what it binds to, and which store it belongs in.
- The **base-type model** — what an equipment template declares, and its row shape.
- **Implicits** — the fixed modifier every copy of a base type carries.
- **Equipment base stats** — a weapon's base damage and a piece of armour's base guard: where the number
  lives, whether it rolls, and how it reaches an actor.
- **Base-type families and their ladders** — the humanoid and plant material rungs, weapon classes, and
  the v1 authoring count.
- **Base-type identity grammar** and the validation that enforces it.

### This lane does NOT own

| Thing | Lane |
|---|---|
| Rolled affixes, the affix pool, tier bands | **I8** — I supply the pool key, they own the contents |
| The equip-slot role list and its ids | **I2** — I consume it; see §9.1 |
| The rarity ladder and its ordinals | **I1** |
| Sockets, inserts, socket combos | **I4** — I store a per-base-type ceiling only |
| Set bonuses | **I5** |
| Post-drop mutation (enhancement, reroll) | **I6** |
| Cost vocabulary and material taxonomy detail | **I9** |
| Charm mechanics | **I10** |
| Equip gating beyond `level_req` | **I11** |
| Turning a loot event into an instance | **I12** |
| Bags, stacking implementation, salvage, comparison | **I13** — I declare stacking *intent* per category |

---

## 3. The model

An item is a **container of atoms** (SC1). Nothing in this lane changes that, and nothing in this lane
adds a second modifier path.

A **base type** is one `effect_container` row with `container_kind = 'item'`, plus one row in a new
`item_base_type` side table holding the four things the container schema has no column for: frame, class,
item-level band, and socket ceiling. The container's **fixed core** (`effect_container_atom`) holds
exactly two things, in this order:

```text
seq 0   base-stat atom      atom.base-guard.plate.t3     the "Armour 501" number
seq 1   implicit atom       atom.fortitude.t3            the reason this base type is a choice
seq 2+  drawn affixes       appended at instantiate, in draw order   (definitions §5)
```

**A base stat is an atom.** A weapon's base damage is `atom.base-damage.{class}.t{band}` — kind
`stat.modify`, channel `atk`, op `Flat`, unit *attack points*. A piece of armour's base guard is
`atom.base-guard.{class}.t{band}` — `stat.modify`, channel `maxHp`, op `Flat`, unit *hit points*. There is
no `base_damage` column anywhere, because there is nowhere for one to go: the eight primary channels are
`hp · maxHp · atk · defense · arm1 · arm1Max · arm2 · arm2Max`
([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §4.1) and none of them is "weapon damage". A
column would need a composer, a pricer, a display path, and a writer route that the atom path already has.

Base stats live in a **reserved family namespace, `atom.base-*`**, which is what separates them from
implicits and affixes with no new column. A family in that namespace may never appear in
`effect_container_pool`.

**The base stat rolls a narrow range at drop** — ±10% around the base-type midpoint, as an
`OnInstantiate` value spec written into `effect_container_atom.overrides_json`. `roll_seed` freezes it, so
SC5 holds with no new mechanism.

**Exactly one implicit per base type, and it does not roll.** Two crowns in the same role with the same
guard are different items because one carries `atom.regeneration` and the other carries `atom.warded`.
Fixed, not rolled, so the implicit is a choice you make at the vendor rather than a lottery you farm.

Everything that is **not** equipment is a category row, not a table. There are **three stores** for the
whole item system — one for rolled instances, one generalised stack table, one ledger — and adding a
category is adding a row to `item_category`, never adding a table.

### The scale, up front

| Thing | v1 count |
|---|---|
| Categories | **10** (`equipment` `material` `consumable` `quest` `currency` `insert` `charm` `cosmetic` `blueprint` `cache`) |
| Categories actually **authored** at v1 | **4** — `equipment`, `material`, `currency`, `quest`. The rest have a named owner and no consumer yet (SC7) |
| Base-stat families | **2 shipping** (`atom.base-damage`, `atom.base-guard`) + **2 scheduled** (`atom.base-cadence`, `atom.base-ward`) |
| Base-stat atom rows | **64** = (10 guard classes × 4 bands) + (6 weapon classes × 4 bands) |
| Base-type identities per frame | **43** |
| Base-type containers at v1 | **344** = 43 × 2 frames × 4 bands |
| New tables | **2** — `item_base_type`, `item_category`. Plus **1** recommended to I13 |
| New reason codes | **6** |

---

## 4. Options considered, and the recommendation

### A. Where does a weapon's base damage live?

The hardest question in this lane, so it gets the most room.

| Option | What it is | Cost |
|---|---|---|
| **(a) an implicit-style atom** | `stat.modify` / `atk` / `Flat`, in the container's fixed core | Base damage lands on the actor's effect list next to affixes; a weapon's `+96 atk` base and a rolled `+20 atk` `might` affix compose on the same channel |
| **(b) a numeric column** | `item_base_type.base_damage INT` | Needs a composer that reads it, a power pricer that reads it, a display path, and a write route. Four second paths |
| **(c) both** | column is the SSOT, an atom is generated from it | Two sources of truth for one number, kept in sync by code that will drift |

**Recommendation: (a), and it is not close.**

Three reasons, in order of weight.

1. **There is no channel for (b) to write.** The primary channel list is closed at 8 (growing to 11 with
   `attackInterval` / `produceInterval` / `zombieSpeed`), and there is no `weaponDamage` among them. A
   base-damage column therefore has to *become* `atk` somewhere. The only sanctioned route from a value to
   `atk` is compose → `EntityStatWriter`, which is guarded (`scripts/guard-single-writer.ps1`). So (b) is
   not "a column instead of an atom" — it is "a column **plus** a hand-written bridge to the same place the
   atom already goes". That is the failure mode *"a base-stat path that bypasses the single-writer and
   compose rules"*, arrived at by accident.
2. **A column also needs a second power path and a second display path.** SC9 says power is open, but when
   E9 lands it prices atoms. A column would be invisible to it, so every item's power would understate by
   roughly half. Same for the UI: one renderer for atoms, one for the column.
3. **The stated cost of (a) is smaller than it looks.** "Base damage competes for the same list as affixes"
   is true only of the *actor's effect list*, which is a display and iteration order — it is **not** the
   roll budget. The fixed core (`effect_container_atom`) and the pool (`effect_container_pool`) are
   different tables, and `pool_rolls` draws only from the pool
   ([spec-container-schema.md](../effect-atom/spec-container-schema.md)). A base stat consumes zero affix
   slots. The `atom.base-*` prefix separates it for display, sort, and budget accounting.

Rejected (c) outright: two writable representations of one number is the defect, not the compromise.

**The one thing (a) gives up:** an item can no longer be summarised by reading one column. Every "what is
this weapon's damage" query joins `effect_instance_atom` and filters on the family prefix. That is a real
cost and it is paid once, in one query helper.

### B. Do base stats roll at drop, or are they fixed per base type?

| Option | Tradeoff |
|---|---|
| **Fixed per base type** | Trivial comparison, no variance, and two copies of the same base are literally identical before affixes. Removes a whole axis of "is this one better" |
| **Roll a range at drop** | Costs nothing — `overrides_json` already carries value specs with `roll: onInstantiate`, and `roll_seed` freezes them. Adds a rarity-independent variance axis |
| **Fixed value scaled by a curve** | The `level` curve input reads the *owning actor's* level (definitions §2), which would make gear scale with its wearer — wrong. `rarity` input would make rarity change a magnitude, which the schema forbids. `tier` input is what bands already are. So this option is either wrong or redundant |

**Recommendation: roll, but narrowly — ±10% of the base-type midpoint, integer game units,
`roll: onInstantiate`.**

Why ±10% and not D2's much wider armour spread (recalled: Full Plate Mail 60–105 base defence, roughly
±27% — **unverified**): a wide base roll makes re-farming the *base* the dominant activity, and it lets a
lucky low-class roll cross into the next class rung, which erases the class ladder. At ±10% the ladder
survives — plate band 3 rolls 451–551 hit points and scale band 3 rolls 353–431, so they never meet — while
two copies of the same base are still distinguishable.

Base-stat variance is **independent of rarity**, which is a direct contribution to OD4's overlap
requirement: see §7.4.

### C. How many implicits, and do they roll?

| Option | Tradeoff |
|---|---|
| **0–2, rolled** (PoE-shaped) | Maximum variety; also maximum grind — PoE's corrupt-for-a-better-implicit loop is the direct consequence, and it makes the base type's identity fuzzy |
| **Exactly 1, fixed** | The base type *is* its implicit. Comparison is legible. No implicit lottery |
| **Exactly 1, rolled narrowly** | Splits the difference and inherits a small version of both problems |

**Recommendation: exactly one, fixed, mandatory.** `item_base_type.implicit_family` is NOT NULL in
practice (nullable only for the handful of pure-stat bases where the base stat *is* the identity).

The dominance guard that works **today, without a power number** (SC9): every implicit on a role's slate is
authored at the **same tier**, so they are the same strength band by construction. What I would *want* from
power once E9 lands is a hard cap — implicit `atom.power` ≤ 15% of the base type's total item budget — but
the design does not depend on it.

Second guard: when a base type's `implicit_family` also exists in its affix pool, I8 must exclude it with an
explicit `group` on the pool rows. That stops an item reading `+8% life (implicit) / +8% life`.

**What makes two items in the same role feel different, concretely:** the implicit is a different *family*,
not a different number of the same family. A `plate` crown carrying `atom.fortitude` and a `cloth` crown
carrying `atom.midas` are not two points on one axis — one is a survivability pick and the other is an
economy pick, and no amount of rolling turns one into the other. That is the whole mechanism, and it is why
the implicit must never be a bigger version of the base stat.

### D. Does one base type span item-level bands, or is there one per band?

Forced by the schema, and worth stating so nobody re-opens it: `effect_container_atom` references a concrete
`atom_id`, and `atom_id` embeds the tier (definitions §1). **A container is therefore band-fixed.** A base
type that spanned bands would have to select its base-stat atom at instantiate, and the only
instantiate-time selection mechanism is the weighted pool — which is random, not level-driven.

**Recommendation: one container per band, four bands.** D2's normal / exceptional / elite ladder is the same
shape (recalled, three rungs — **unverified**). Four bands × 43 identities × 2 frames = 344 containers, all
**generated**, not hand-authored.

### E. Storage — a table per category, or a small fixed set?

**Recommendation: three stores, forever.** See §5.6. A new category is a row in `item_category`; it is never
a table. The category column is the entire anti-proliferation mechanism.

---

## 5. Data shape

### 5.1 Categories

Ten categories. `unique` and `set` are **rarities** (I1) and `legendary` is a rarity ordinal — none of them
is a category.

| Category | Rolls values? | Stacks? | Binds to | Store | v1 |
|---|---|---|---|---|---|
| `equipment` | **yes** | never | owned by a **player**; the instance binds to an **actor** at equip | instance | **author** |
| `material` | no | by qty | player | stack | **author** |
| `consumable` | no | by qty, with charges | player; its effect targets an actor at use | stack | declare only |
| `quest` | no | by qty, usually capped at 1 | player | stack | **author** |
| `currency` | no | ledger, never a bag row | player | ledger | **exists** |
| `insert` | maybe (I4) | by qty until socketed | player; binds into an **item**, not an actor | stack → instance if I4 rolls them | declare only |
| `charm` | maybe (I10) | never if rolled | player — the bonus binds at `player:{id}` scope | instance if rolled, stack if not | declare only |
| `cosmetic` | no | never (an unlock row) | player | stack | declare only |
| `blueprint` | no | never (qty 1 = known) | player | stack | declare only |
| `cache` | no | by qty | player | stack | declare only |

Two categories beyond the brief's eight: **`blueprint`** (crafting knowledge, which is an unlock and not a
material — it must not be spendable) and **`cache`** (an unopened loot bundle, which is a *deferred* drop
and therefore has to survive a catalog revision without re-rolling).

#### SC7 — every category names its consumer, and four of them have none

| Category | Consumer today | Verdict |
|---|---|---|
| `equipment` | `BindGate` + compose → `EntityStatWriter` on the lawn | ships |
| `material` | **none.** `DemonMaterialCatalog` is documented as *"inventory rows with validated ids"* and *"demon-fusion consumes these later"* (`src/FusionRpg.Core/Demons/DemonMaterialCatalog.cs:7`) | ships as inventory; **I7/I9 must name the consumer** |
| `currency` | `rpg_soul_ledger` / `rpg_soul_balances` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:440`, `:455`) | ships |
| `quest` | world / expedition gates | ships |
| `consumable` | the **action layer**, unbuilt | do not author |
| `insert` | I4, unbuilt | do not author |
| `charm` | I10, unbuilt | do not author |
| `cosmetic` | none, and none planned | **do not author** |
| `blueprint` | I7, unbuilt | do not author |
| `cache` | I12, unbuilt | do not author |

That is the `status.expose.*` lesson applied before the fact: *a row no code consumes is not content; it is
a lie in a table.*

#### `item_category`

| Column | Type | Notes |
|---|---|---|
| `category_id` | TEXT PK | one of the ten above |
| `rolls_values` | INT | 0/1 |
| `stack_intent` | TEXT | `never` \| `qty` \| `charges` \| `ledger` — **I13 builds it; this column is the declaration** |
| `owner_scope` | TEXT | `player` \| `player-then-actor` \| `player-then-item` |
| `store` | TEXT | `instance` \| `stack` \| `ledger` |
| `consumer` | TEXT **NOT NULL, non-empty** | the code that reads this category. Empty is `CategoryHasNoConsumer` |
| `enabled`, `revision` | INT | joins the E8 content hash |

**Is this data or code?** A category whose `store` is `stack` and whose behaviour is "sit in a bag" is data
— adding the row changes what the bag holds with no new code. A category that *does* something is code, and
the `consumer` column is what forces that distinction to be written down.

### 5.2 Base type — the row shape

A base type is **two rows**. Reused columns first.

**Reused from `effect_container` — no schema change:**

| Column | What a base type puts in it |
|---|---|
| `container_id` | derived id, §5.5 |
| `container_kind` | `item` (already reserved for I3, SC3) |
| `slot` | **the frame-neutral `role` id** — `core-protective`, `armament-primary`. Not the frame-specific slot name (`torso` / `stem`), so one string compare serves the bind check. See §9.2 |
| `rarity` | I1's ordinal key |
| `min_tier` / `max_tier` | the affix tier window (I1 picks, I8 fills) |
| `level_req` | enforced at bind, `LevelTooLow` |
| `pool_rolls` | I1 picks the count |
| `tags_json` | `{"frame":…,"class":…,"band":…}` mirrored for read convenience |
| `enabled`, `revision` | content hash |

**New — `item_base_type`, keyed 1:1 on the container:**

| Column | Type | Notes |
|---|---|---|
| `container_id` | TEXT PK, FK → `effect_container(container_id)` | must have `container_kind = 'item'` |
| `frame` | TEXT NOT NULL | `humanoid` \| `plant` \| `either`. `either` exists for OD3 hybrids; see §9.4 |
| `class_id` | TEXT NOT NULL | armour, weapon, jewel, or off-hand class — §5.3 |
| `band` | INT NOT NULL | item-level band 1–4. **Must equal the tier of every `atom.base-*` atom in the core** |
| `socket_capacity` | INT NOT NULL DEFAULT 0 | the **ceiling** this base may ever carry. I4 decides how many are actually cut |
| `implicit_family` | TEXT NULL | the one implicit's `family_id`. NULL only for pure-stat bases |
| `affix_pool_tag` | TEXT NOT NULL | the key I8's pool generator filters on — `armour-plate`, `weapon-nozzle` |
| `req_json` | TEXT NULL | I11's requirement expression. **Opaque to I3** |
| `display_json` | TEXT NULL | name parts, icon key |
| `enabled`, `revision` | INT | joins the E8 content hash |

**What the base type declares, against the brief's list:**

| Asked for | Where it lives |
|---|---|
| frame | `item_base_type.frame` |
| role | `effect_container.slot` |
| implicit atoms | `effect_container_atom` at `seq 1`, family named in `implicit_family` |
| item-level band | `item_base_type.band` |
| requirement hooks | `effect_container.level_req` (enforced) + `item_base_type.req_json` (I11) |
| socket capacity | `item_base_type.socket_capacity` |
| **base stats** | `effect_container_atom` at `seq 0`, family `atom.base-*`, value in `overrides_json` |

### 5.3 The base-stat families and the class ladders

Four families. Two ship now; two are scheduled and **must not be authored before their consumer exists.**

| Family | Kind | Channel · op | Unit | State |
|---|---|---|---|---|
| `atom.base-damage` | `stat.modify` | `atk` Flat | attack points (game units) | **ships** — lawn ✅, battle ✖ until E12 |
| `atom.base-guard` | `stat.modify` | `maxHp` Flat | hit points (game units) | **ships** — lawn ✅, battle ✖ until E12 |
| `atom.base-cadence` | `stat.modify` | `attackInterval` Flat | ms | **held** — `attackInterval` is not a channel until the channel-extension spec lands |
| `atom.base-ward` | `stat.derived` | `combat.defense.omni` Flat | resolver points | **held** — `stat.derived` is quarantined `None/None/None` (D6); binds nowhere until E12 |

**Why armour's base stat is `maxHp` and not `defense`.** `stat.modify` on `defense` is legal only at `match`
scope: the `TakeDamage` prefix reads one side-wide cached value, so `plant:N`, `zombie:N` and `entity:` all
reject with `ScopeUnsupported` (definitions §6, gap G8). An armour piece whose base stat was `defense` would
be **rejected at bind**. So v1 armour is effective HP, which composes per actor and works on the lawn today.
When E12 re-opens `stat.derived`, `atom.base-ward` is added alongside and the `maxHp` band values are
rebalanced down. That is a scheduled migration, not a surprise.

**The same rule bans `warding` and `resilience` as item implicits.** Any base type carrying either would
reject at bind. This is worth repeating loudly because "+armour" is the most obvious affix in the genre and
it is the one this codebase cannot currently ship per-item.

**Class ladders** — 24 classes, 8 of which are the two armour ladders:

| Group | Humanoid | Plant |
|---|---|---|
| Armour (4 rungs, ascending) | `cloth` · `leather` · `scale` · `plate` | `fibre` · `husk` · `bark` · `heartwood` |
| Weapon | `blade` · `blunt` · `launcher` | `nozzle` · `seedpod` · `lash` |
| Jewel | `signet` · `torc` · `seal` | `graft` · `bulb` · `spore` |
| Off-hand | `bulwark` (guards) · `focus` (does not) | `thornguard` (guards) · `censer` (does not) |

Base-stat atom rows: `atom.base-guard` × 10 guarding classes (8 armour + 2 shield-shaped off-hands) × 4
bands = **40**. `atom.base-damage` × 6 weapon classes × 4 bands = **24**. **64 rows**, generated.

### 5.4 The guard budget — one number split across the body

The `atom.base-guard.{class}.t{band}` value is the **whole-body reference**: what a full set of that class at
that band grants. A base type claims a per-mille share of it by role, written into `overrides_json` by the
generator.

| Role (item-ideal §5.1) | Guard share | Damage share |
|---|---|---|
| `core-protective` | 280‰ | — |
| `head-protective` | 170‰ | — |
| `mantle-utility` | 120‰ | — |
| `footing` | 100‰ | — |
| `armament-secondary` | 100‰ (guarding classes only) | — |
| `manipulator-offense` | 90‰ | — |
| `girdle-resource` | 90‰ | — |
| `sense-utility` | 50‰ | — |
| `jewel-major` · `jewel-minor` | **0** | 0 |
| `armament-primary` | 0 | **1000‰** |

The eight guarding roles sum to exactly **1000‰**, which makes the class band value legible: *plate band 3 is
1790 hit points across a full set.* The share is a **ceiling**, not a mandate — a `focus` off-hand claims 0
and pays for it with a stronger implicit slate.

A role with share 0 that carries a base-stat atom is `BaseStatRoleForbidden`.

**This table survives I2 changing the role count.** OD2 says ~15 slots, item-ideal §5.1 lists 12 roles. The
shares are per-mille of one budget, so adding roles re-splits the same 1000 rather than inflating it.

### 5.5 Identity grammar

`container_id` grammar is `^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$` (definitions
§1) — **one dot, then kebab-case only.** `item.humanoid.plate.warplate.b3` is illegal. The form is:

```text
item.{frame}-{class_id}-{identity}-b{band}

  item.humanoid-plate-warplate-b3
  item.plant-nozzle-emberjet-b2
  item.humanoid-seal-tollring-b4
```

The role is deliberately **not** in the id — it is already `effect_container.slot`, and multi-word role ids
(`core-protective`) would make positional parsing ambiguous.

**The id is derived from its columns and validated against them**, exactly as `atom_id` is (definitions §1).
A mismatch is `IdMismatch` — an existing code, reused. `class_id` values are therefore constrained to single
kebab tokens with no internal hyphen, so the middle segment is unambiguous.

Base-stat atoms follow the ordinary atom grammar: family `atom.base-guard` (matches
`^atom\.[a-z0-9]+(-[a-z0-9]+)*$`), variant `plate`, tier = band, id `atom.base-guard.plate.t3` (matches
`^atom\.[a-z0-9-]+(\.[a-z0-9-]+)?\.t[1-9][0-9]*$`). Unique key `(family_id, tier, variant)` holds.

Implicits use ordinary affix families with no special namespace — `atom.fortitude.t3`,
`atom.searing-strike.fire.t2`. The `atom.base-` prefix is the only thing distinguishing core rows, and it is
sufficient: the core is base stats plus exactly one implicit.

### 5.6 Storage — the recommendation to I13

Three stores. Named here because §9.11 asks I13 to build them.

**1. `rpg_item_instance`** — the thin row above `effect_instance` for every rolled, non-stacking thing.
item-ideal §6.3 left this open; this lane closes it for equipment: **yes, a thin row.** `effect_instance`
carries `instance_id · container_id · roll_seed · created_utc · origin`
([spec-instance-and-binding.md](../effect-atom/spec-instance-and-binding.md)) and has no player, no category,
no bind state, no lock flag — and adding them would make the atom program know about items.

| Column | Notes |
|---|---|
| `instance_id` | PK, FK → `effect_instance` |
| `player_id` | owner |
| `category_id` | FK → `item_category` |
| `base_type_id` | **denormalised** FK → `item_base_type` so a bag listing does not join `effect_instance` |
| `bound_owner_kind` / `bound_owner_key` | NULL when unequipped; mirrors the live `effect_binding` |
| `bind_state` | `free` \| `bound-on-pickup` \| `bound-on-equip` |
| `locked`, `favourite` | player flags |
| `acquired_utc`, `origin` | provenance |

**Never** duplicate rolled values into this row. The rolls live in `effect_instance_atom` and nowhere else.

**2. `rpg_player_stack`** — one generalised stackable table, the direct generalisation of
`rpg_demon_materials(player_id, material_id, qty, updated_utc)`
(`src/FusionRpg.Data/Sqlite/RpgStore.cs:520`):

```sql
rpg_player_stack(player_id, category_id, item_id, qty, charges, updated_utc,
                 PRIMARY KEY (player_id, category_id, item_id))
```

Serves `material`, `consumable`, `quest`, `insert` (unrolled), `cosmetic`, `blueprint`, `cache`.
`rpg_demon_materials` migrates into it with `category_id = 'material'`.

**3. The ledger** — currency never enters an inventory table. `rpg_soul_ledger` / `rpg_soul_balances` already
work this way and are the precedent to copy, not to replace.

**Which categories share a store, and why:** everything that does not roll shares store 2, because the only
thing that forces a separate table is *rolled values*, and rolled values force it for a schema reason (a
per-instance atom set cannot be a `qty` column) rather than a flavour reason. Undroppable / unsellable
(`quest`) is a **flag**, not a table. A wardrobe is a filter, not a table.

---

## 6. Validation and reason codes

### Reused, unchanged

| Bad input | Code |
|---|---|
| `container_id` does not equal its derived form; `atom_id` does not equal `{family}[.{variant}].t{tier}` | `IdMismatch` |
| `frame`, `class_id`, `store`, `stack_intent`, `owner_scope` outside its enum; `band` outside 1–4; negative `socket_capacity` | `BadParamValue` |
| `overrides_json` malformed, or `Fixed` with `Min != Max`, or `Min > Max` | `BadValueSpec` |
| Base-stat range overflows `int` after scaling | `MagnitudeOverflow` |
| `implicit_family` names a family with no rows at the base type's band | `UnknownAtom` |
| `item_base_type` row with no matching `effect_container` | `UnknownContainer` |
| Same atom in the fixed core and the pool | `DuplicateAtomInContainer` |
| Two core rows sharing a `seq` | `DuplicateSeq` |
| Pool atom outside `[min_tier, max_tier]` | `TierOutOfWindow` |
| `pool_rolls` above the drawable group count | `PoolRollsExceedGroups` |
| Every pool row at `weight = 0` | `UnsatisfiablePool` |
| A base type carrying `warding` / `resilience` (implicit or base stat) bound to any non-`match` scope | `ScopeUnsupported` — G8 |
| A base type carrying a `stat.derived` atom bound anywhere today | `RuntimeUnsupported` — D6 |
| Wearer below `level_req` at equip | `LevelTooLow` |
| Instance references a disabled atom | `StaleInstance` |

### Proposed new — six, each a reviewed addition to the 33

| # | Bad input | Code |
|---|---|---|
| 1 | An `atom.base-*` family appears in `effect_container_pool` | `BaseStatInPool` |
| 2 | More than one non-`atom.base-*` atom in a base type's fixed core | `ImplicitCountExceeded` |
| 3 | A base-stat override range outside `roleShare‰ × classBand ± 10%`, rounded half away from zero | `BaseStatOutOfBudget` |
| 4 | A base-stat atom on a role whose share is 0 (`jewel-*`, a `focus` off-hand) | `BaseStatRoleForbidden` |
| 5 | `item_base_type.band` ≠ the tier of an `atom.base-*` atom in the core | `BandTierMismatch` |
| 6 | An `item_category` row with an empty `consumer` | `CategoryHasNoConsumer` |

All six are **import-time** (all-or-nothing, E14). None is a bind-time check, because all of them are
authoring mistakes a generator can make and a player cannot.

**Two lints, not rejections** (they report, they do not block):

- A class and band whose per-role guard shares do not sum to 1000‰ across the authored base types.
- A base type whose `affix_pool_tag` resolves to a pool that also contains its `implicit_family` with no
  explicit `group` — I8's to fix, mine to notice.

---

## 7. Worked examples

**Every number below is illustrative, not balanced.** Bands rise ×2.1, class rungs rise ×1.4 — both picked
for legibility, neither tuned.

Reference guard bands (whole-body hit points):

| Class rung | b1 | b2 | b3 | b4 |
|---|---|---|---|---|
| `cloth` / `fibre` | 200 | 420 | 780 | 1300 |
| `leather` / `husk` | 280 | 590 | 1090 | 1820 |
| `scale` / `bark` | 360 | 760 | 1400 | 2340 |
| `plate` / `heartwood` | 460 | 970 | 1790 | 2990 |

Reference damage bands (whole-weapon attack points):

| Class | b1 | b2 | b3 | b4 |
|---|---|---|---|---|
| `blade` | 24 | 52 | 96 | 160 |
| `blunt` | 30 | 65 | 120 | 200 |
| `launcher` | 20 | 43 | 80 | 133 |
| `nozzle` | 22 | 47 | 88 | 146 |
| `seedpod` | 27 | 58 | 108 | 180 |
| `lash` | 25 | 54 | 100 | 167 |

### 7.1 A humanoid plate torso, band 3, Rare

```text
effect_container
  container_id  item.humanoid-plate-warplate-b3
  kind          item
  slot          core-protective          (the role, not "torso")
  rarity        rare
  min_tier 2    max_tier 4
  level_req     34
  pool_rolls    4

item_base_type
  frame humanoid   class_id plate   band 3
  socket_capacity 2
  implicit_family  atom.fortitude
  affix_pool_tag   armour-plate

effect_container_atom
  seq 0  atom.base-guard.plate.t3
         overrides_json {"amount":{"min":451,"max":551,"roll":"onInstantiate"}}
  seq 1  atom.fortitude.t3
         (maxHp Increased, +80 per-mille, Fixed)
```

The override arithmetic: `280‰ × 1790 hp = 501 hp`, ±10% → **451–551 hit points**. A value outside that
window is `BaseStatOutOfBudget`.

One drop, `roll_seed = 0x5F2A1C`, resolves the base stat to **517 hp** and draws four affixes at tiers 2–4.
Player-facing:

```text
Warplate  (Rare, item level band 3)
  Armour        517            [base]
  +8% maximum life             [implicit]
  ... four rolled affixes ...
  Sockets 0/2
  Requires level 34
```

On the lawn this composes to +517 `maxHp` and +8% `maxHp` through the ordinary Flat → Increased → More path
and reaches Unity through `EntityStatWriter`. **In battle it does nothing** — the battle sink ignores FA1
([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md) §2) until E12 wires `BattleStatComposer`.

### 7.2 A plant nozzle, band 2, Magic

```text
effect_container
  container_id  item.plant-nozzle-emberjet-b2
  slot armament-primary   rarity magic
  min_tier 1  max_tier 3   level_req 18   pool_rolls 2

item_base_type
  frame plant   class_id nozzle   band 2
  socket_capacity 1
  implicit_family atom.searing-strike
  affix_pool_tag  weapon-nozzle

effect_container_atom
  seq 0  atom.base-damage.nozzle.t2
         {"amount":{"min":42,"max":52,"roll":"onInstantiate"}}     42–52 attack points
  seq 1  atom.searing-strike.fire.t2
         resource.delta, when OnDamageDealt, icd_ms 250,
         {"element":"fire","amount":{"min":30,"max":45,"roll":"onApply"}}
```

`1000‰ × 47 = 47 attack points`, ±10% → 42–52. One drop resolves to **51 atk**.

Note the two different roll policies in one core: the base stat is `onInstantiate` and freezes at drop; the
implicit's fire damage is `onApply` and rolls per hit, so it is deliberately **not** frozen
(spec-instance-and-binding.md: *"`OnApply` values are left unresolved — they belong to the hit, not the
item"*). Same table, same mechanism, different moment.

### 7.3 A humanoid signet ring, band 4, Normal — no base stat at all

```text
  container_id  item.humanoid-seal-tollring-b4
  slot jewel-minor   rarity normal   pool_rolls 0

item_base_type
  frame humanoid   class_id seal   band 4
  socket_capacity 0
  implicit_family atom.midas
  affix_pool_tag  jewel-seal

effect_container_atom
  seq 0  atom.midas.t4        resource.economy, on kill, +14 money
```

`jewel-minor` has share 0, so there is no `atom.base-*` row and adding one is `BaseStatRoleForbidden`. A
Normal rarity item with `pool_rolls = 0` is base type plus implicit only — item-ideal §6.2's Normal row,
expressed with no new columns. The whole item is one container row, one base-type row, one core row.

### 7.4 What the base-stat roll does and does not do — the OD4 contribution

Two band-3 `core-protective` plates:

| | Guard | Affixes |
|---|---|---|
| A — Normal, high base roll | **551** hp | none |
| B — Rare, low base roll | **451** hp | four, tiers 2–4 |

A's base edge is +100 hit points, real but small; B's four affixes dominate. So **the base-stat roll is a
tiebreaker within a rarity, not across rarities** — it is not the OD4 overlap mechanism by itself.

What it *does* contribute to OD4 is structural: the base stat is **rarity-independent**, and at the ~45–55%
budget share this lane targets, roughly half of an equipment piece's contribution does not move with rarity
at all. That is what makes a high-roll Magic competitive with a low-roll Rare — the rarity-dependent half has
to swing 2× to double the item. I1 and I8 get that overlap floor for free and should design the rest of it
knowing it is already there.

The class comparison is the opposite story, and it is a **risk to name**: band-3 cloth `core-protective` is
`280‰ × 780 = 218 hp` (196–240) against plate's 501. Plate wins guard by 2.3× and never overlaps. Cloth's
entire compensation therefore has to come from its class-tagged affix pool and its implicit slate. **If I8
does not differentiate `armour-cloth` from `armour-plate`, cloth is strictly worse and the ladder collapses
into "wear the heaviest thing you can."** That is §9.6.

### 7.5 Weapon classes — mechanical, not flavour, and no new kind

The brief asks whether `blade` / `blunt` / `launcher` / `nozzle` / `seedpod` matter mechanically. They do,
through three mechanisms that all use columns that already exist:

| Mechanism | How | Example |
|---|---|---|
| **A different base-damage band** | `class_id` is the `variant` on `atom.base-damage`, so each class has its own 4-band curve | `blunt` b3 = 120 atk against `launcher` b3 = 80 |
| **A different implicit slate** | `implicit_family` is chosen from a per-class slate | `blade` → `atom.keen-edge`-shaped offence; `nozzle` → `atom.searing-strike`; `seedpod` → `atom.gardener`; `launcher` → `atom.volley`; `lash` → `atom.retribution` |
| **A different affix pool** | `affix_pool_tag` = `weapon-{class}` | I8 owns the contents |

Nothing here needs a thirteenth atom kind (SC2). Class is `variant` + family selection + a pool tag.

Two honest gaps. **Cadence is missing:** `blunt` is *supposed* to trade attack speed for damage, and until
the channel-extension spec promotes `attackInterval` there is no way to say so, so today `blunt` is simply
better than `blade` at the same band. That is §9.18, and it is the single biggest hole in the class design.
**Range is not mine:** how far a `launcher` or a `nozzle` reaches is the action layer's target spec; I3
declares the class tag it keys on and nothing more.

### 7.6 Sizing, per frame per role

| Role group | Base types per frame per band | How |
|---|---|---|
| 7 armour-bearing body roles | 4 each = **28** | one per class rung |
| `armament-primary` | **6** | 3 weapon classes × 2 identities |
| `armament-secondary` | **3** | 2 guarding + 1 focus class |
| `jewel-major` | **3** | 3 jewel classes |
| `jewel-minor` | **3** | 3 jewel classes |
| **Per frame per band** | **43** | |

**43 × 2 frames × 4 bands = 344 base-type containers at v1.** At 1 container row + 2 core rows + ~12 pool
rows each, that is roughly **5,200 rows**, all emitted by one generator from 43 identity definitions × 2
class ladders × 4 bands.

If content time is short, ship **bands 1 and 3 only** — 172 containers — and add 2 and 4 later. Bands are
append-only in exactly the way rarity ordinals are not, so this is a safe cut.

Hybrid frames (OD3) author **no new base types**: they set `frame = 'either'` on the roles they keep and draw
from both ladders. Breadth costs slots, not content.

---

## 8. Failure modes

**1. Base types that are pure flavour, so only affixes matter.** The D3-launch shape (recalled —
**unverified**). Three defences, none of which is a slogan: the base stat is ~45–55% of the piece's
contribution and does not move with rarity; the implicit is a **different family**, not a bigger number of
the same one; and `affix_pool_tag` means class filters *which* affixes can roll. If any one of those is
dropped, base types go cosmetic. The one that is not in my hands is the third — §9.6.

**2. A base-stat path that bypasses single-writer and compose.** Prevented by construction: there is no
base-stat column, so there is nothing to bypass with. Base stats are `stat.modify` atoms and take the same
route every other modifier takes, which `scripts/guard-single-writer.ps1` already guards. This is the single
strongest reason for option 4.A(a).

**3. Category proliferation — a table per item type.** Prevented by the three-store rule and by
`item_category` being a table of rows. The pressure to add a table always arrives as "but quest items are
special": they are, by one boolean, and a boolean is a column.

**4. Implicits so strong that one base type dominates its role.** The D2 "everything is a Shako" shape
(recalled — **unverified**). Defended by: one implicit; fixed, so it cannot high-roll; **tier-equal across a
role's slate**, so they are the same band by construction; and a uniform role share, so no base type gets
both the best implicit and a guard advantage. The remaining hole is a *qualitatively* better family at the
same tier — economy implicits are notoriously over-valued in ARPGs — and that is a content-review problem the
power model will eventually measure (SC9).

**5. Base-stat re-farming becomes the game.** PoE's corrupt-and-pray loop, imported through the back door.
±10% is deliberately below the threshold where re-farming a base beats improving an affix. If I6's
enhancement can push the base stat, this changes — §9.13.

**6. Band inflation.** b1 → b4 is ~15× on guard and ~6.7× on damage. That is intentional and it is a *content*
ladder, not a level curve: band is a property of the drop, not of the wearer. The classic failure is bolting a
level scalar on top, which the curve `input` rules already prevent (definitions §2: `level` reads the owning
actor).

**7. Two different things called "base stats".** definitions §7 says *"base stats contribute nothing"* to
`actorPower`. That sentence is about the **actor's level curve**, not about item base stats — item base stats
are granted atoms and they price normally. Anyone reading §7 out of context will get this backwards and
under-price every weapon in the game by roughly half.

**8. Shipping content nothing consumes.** Six of ten categories, and two of four base-stat families, are
declared and deliberately unauthored. The temptation is to seed them "so they are ready". `status.expose.*` is
the counter-example already in the tree.

**9. Equipment that does nothing where players expect it to.** Base damage is `stat.modify` on `atk`, which the
**battle sink ignores**. An equipped weapon changes lawn behaviour and changes nothing in battle until E12.
This is not a design choice, it is the current state of the runtime, and it needs to be visible in the UI
rather than discovered.

**10. The `warding` trap.** Every ARPG designer's first armour affix is `+defense`. In this codebase that affix
is bind-rejected at every per-actor scope (G8), so the *first* base type anyone authors by instinct is the one
that cannot ship. §5.3 exists to catch that before it is written, not after.

---

## 9. What this lane needs from other lanes

Numbered, each naming the lane. This is where I3's insufficiency shows.

1. **I2 — the role list, as a table with stable ids.** Everything in §5.4 keys on role. OD2 says ~15 slots per
   frame; item-ideal §5.1 lists 12 roles. I used the 12 as an interim and made the shares per-mille so the
   table re-splits rather than inflates, but I need the final ids to generate 344 containers.
2. **I2 — a ruling that `effect_container.slot` and `effect_binding.slot` both hold the frame-neutral
   *role***, never the frame-specific slot name. If I2 puts `torso` / `stem` in that column, the equip check
   becomes a two-way lookup and base types need a per-frame slot column.
3. **I2 — is `jewel-minor` one role filling two equip slots, or two roles?** item-ideal §5.1 lists them as rows
   9 and 10. I authored one role; if it is two, my 43-per-frame count becomes 46.
4. **I2 / OD3 — the hybrid rule.** `frame = 'either'` only works if a hybrid's roles accept a base type from
   either ladder. If I2 picks fixed per-species assignment instead, `either` is wrong and base types need a
   species filter column.
5. **I1 — a written guarantee that rarity never touches base stats.** Rarity selects `pool_rolls` and the tier
   window, full stop. If rarity scales base stats, the ~50% rarity-independent floor disappears and the OD4
   overlap argument in §7.4 inverts.
6. **I8 — the affix pool must be filtered by `affix_pool_tag`, and cloth must not be strictly worse than
   plate.** §7.4 is the live risk: the class ladder is a guard ladder, and only I8 can pay the lighter classes
   back. I supply the key; they own the contents.
7. **I8 — an affix budget share of roughly 30–45% of a piece's contribution.** If affixes are much stronger,
   base types are cosmetic (failure mode 1). If much weaker, base types are the whole game.
8. **I8 — exclude a base type's `implicit_family` from its own pool** with an explicit `group`, so nothing
   reads `+8% life (implicit) / +8% life`.
9. **I4 — the socket ceiling per class and band.** I store `socket_capacity` and nothing else. I also need to
   know whether sockets consume base-stat budget (in D2 they effectively did, through socketed-base scarcity —
   recalled, **unverified**); if yes, §5.4's shares need a socket term.
10. **I9 — the material class ladder must match my `class_id` values.** I named `cloth · leather · scale ·
    plate` and `fibre · husk · bark · heartwood`. If I9's taxonomy uses different rungs, crafting a plate helm
    has no plate material and the two ladders silently disagree.
11. **I13 — build the three stores in §5.6**, and put `base_type_id` on `rpg_item_instance` as a denormalised
    column so a bag listing does not join `effect_instance`. Also: migrate `rpg_demon_materials` into
    `rpg_player_stack` with `category_id = 'material'` rather than leaving two stack tables.
12. **I12 — drop must choose base type and rarity as two independent picks** and pass both into instantiation.
    If a drop table names a single fused "item id", band/rarity orthogonality dies and §7.4's overlap floor
    goes with it.
13. **I6 — does enhancement touch base stats?** Mine freeze at drop inside a ±10% window. If enhancement can
    push them, the window becomes a floor and failure mode 5 re-opens. I would prefer enhancement operate on
    affixes and sockets only, but I6 owns the mutation model and I adopt whatever they pick.
14. **I10 — `atom.base-*` is a reserved namespace.** Charms must not author into it, or the pool exclusion in
    reason code 1 stops meaning anything.
15. **I11 — the `req_json` grammar.** It is opaque to me and I need the expression shape, plus confirmation
    that `effect_container.level_req` stays the level gate (already enforced at bind, `LevelTooLow`) rather
    than being duplicated inside `req_json`.
16. **The effect-atom program (E5 / E8 / E14) — three asks.** Register `item_base_type` and `item_category` in
    E8's covered-table set (they are content and they must move the content hash). Accept the six reason codes
    in §6. Confirm that reusing `effect_container.slot` for a role id is a reviewed reuse and not a schema
    change.
17. **E12 / combat unification — the schedule for battle reading equipment.** Base damage and base guard are
    `stat.modify`, ignored by the battle sink today. Until E12 wires `BattleStatComposer`, a fully geared demon
    fights identically to a naked one. I need to know whether item content lands before or after that, because
    it decides whether v1 items are a lawn feature or a battle feature.
18. **The channel-extension spec — `attackInterval`.** `atom.base-cadence` is held on it, and without cadence
    the weapon classes are half-designed: `blunt` is supposed to trade speed for damage and currently just has
    more damage (§7.5).

---

## 10. Open questions for the owner

1. **Four bands, or three?** Four gives a ~15× guard span from b1 to b4 and 344 containers. Three (D2's
   normal / exceptional / elite shape) gives ~7× and 258. I picked four; three is defensible and cheaper.
2. **Should the base stat roll at all?** I chose ±10%. Zero variance is simpler, comparison-friendly, and
   removes a whole class of "reroll the base" pressure. This is a feel question, not a technical one.
3. **Should `armament-secondary` guard at all?** I gave guarding off-hand classes 100‰. The alternative is that
   off-hands are pure implicit-and-affix items, which makes the off-hand a build choice rather than a second
   armour slot.
4. **`cosmetic` — cut the row now?** It has no consumer and none planned (item-ideal §7 calls it out of scope).
   I declared it for completeness. Deleting the row is cheaper than carrying a category nobody reads.
5. **Do jewels really get no base stat?** D2 and PoE both say yes (recalled — **unverified**). It does mean a
   band-4 ring is mechanically identical to a band-1 ring except for its implicit tier and affix window, which
   may read as flat.
6. **Is a `focus` off-hand with 0 guard and a stronger implicit acceptable**, or should every base type in a
   role claim the same share so comparison stays one-dimensional?
7. **The implicit budget cap I want from power (≤15% of item budget) has no number behind it** until E9 lands
   (SC9). Ship on tier-equality alone, or hold the implicit slates until power can check them?
8. **Should equipment be bind-on-pickup at all?** `rpg_item_instance.bind_state` carries the column because it
   is nearly free, but item-ideal §10 lists trading as out of scope, and without trading the flag has no
   purpose yet. Keep the column, or strike it until there is a reason.
