# Lane I5 SSOT — set items and set bonuses

**Status:** Lane I5 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

Owner decision **OD5** is an input, not a proposal: set combos are first-class, and several items in a
set grant escalating bonuses.

---

## 1. Scope

### This lane owns

- What a **set** is, and how membership is expressed in data.
- **Thresholds** — how many equipped members turn which bonus on.
- The **bind / unbind lifecycle** of a set bonus, and its owner scope.
- How set pieces are **rolled** at the item level, and how that keeps rares relevant.
- The **anti-set-jail** rules: piece budget, bonus-shape rules, and the two-partial-sets guarantee.
- Set validation and reason codes; where set atoms land in the actor effect list's ordering.

### This lane does NOT own

| Thing | Lane |
|---|---|
| Combinations of inserts **inside one item** | **I4** — sockets |
| Bonuses from **unequipped** inventory | **I10** — charms |
| The **affix pool** and tier bands set pieces roll from | **I8** |
| The **rarity ladder** and its ordinals | **I1** — this lane registers a request in §8 |
| The **equip slot / role** vocabulary and how many slots each frame has | **I2** |
| **Base types**, implicits, and which frame may wear one | **I3** |
| **Post-drop mutation** of a frozen instance | **I6** |
| **Equip gating** (who may wear this) | **I11** |
| Turning a loot event into an instance, and drop bias | **I12** |
| Bags, salvage, comparison UI | **I13** |

---

## 2. The model

A **set** is a named group of item base types. Wearing several of them at once turns on bonuses that a
single item cannot carry.

The whole mechanic is three facts:

1. **Membership is declared, not inferred.** A row says *this base type is the head-protective member of
   Ember Legion*. Nothing is derived from a name, a tag string, or an icon.
2. **A set bonus is an ordinary container of atoms.** It is an `effect_container` with
   `container_kind = 'set'` — the value SC3 reserved for exactly this. Its atoms reach the actor the same
   way an item's do: instance → binding → the actor's effect list. **This lane invents no second effect
   mechanism** (SC1).
3. **The count decides which containers are bound.** An evaluator counts how many distinct member
   **roles** the wearer currently has equipped, looks up every threshold at or below that count, and
   makes the wearer's set bindings match. Equipping the fourth piece binds the 4-piece container.
   Unequipping it withdraws that container and leaves the 2-piece one alone.

Nothing is stored twice. The durable truth is the item bindings; the set bindings are **derived state**
the evaluator keeps in sync, tagged `effect_binding.source = 'set:{set_id}'` so they can be withdrawn as
a group. That column and its index already exist
(`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:82`, `:89`).

A set bonus is **cumulative**: a wearer at 4 pieces holds both the 2-piece and the 4-piece containers.
That is what "escalating" means in OD5, and it is what makes a partial set a real, playable thing rather
than a consolation prize.

The evaluator is a pure function in Core — bound member containers in, tier container ids out — so it
runs with the PvZ game closed (SC8). It runs on equip, not per frame.

---

## 3. Options considered, and the recommendation

### 3.1 The core choice — discrete breakpoints vs summed contribution

**Option A — discrete breakpoints.** 2-piece / 4-piece / 6-piece step bonuses, keyed on a set id. Diablo
2, WoW tier sets, Elder Scrolls Online. *(Prior-art claims throughout this document are recalled from
play and general knowledge, not verified against a source.)*

**Option B — summed contribution.** Every piece contributes points to named skills; a skill fires at
accumulated thresholds. Monster Hunter's armour skills. Any piece can carry any skill, so "sets" stop
existing as a thing and the build becomes a point-allocation puzzle.

**Option C — the hybrid.** Points accumulate against a *set* id, and pieces may be worth more or less
than one point.

| | A — breakpoints | B — summed points |
|---|---|---|
| **Build diversity** | Medium. You are at a threshold or you are not. Diversity comes from *which* sets you combine, and from how many slots the set leaves free | High. Every piece is a dial, and near-arbitrary skill combinations are reachable |
| **Legibility** | High. "2: this. 4: that." A tooltip states the whole rule | Low. The player sums numbers across pieces and cross-references a skill-level table to know what they actually have |
| **Content cost, first set** | Low. One header, N member rows, T tier containers, atoms reused from the existing family library | High. A skill-level table is a second content axis that must exist before the first item does |
| **Content cost, at scale** | Grows linearly per set | Cheaper — hundreds of pieces reuse one skill table. This is why Monster Hunter chose it: it ships armour by the hundred |
| **Does it satisfy OD5?** | Yes, directly | **No.** Pure point-summing dissolves set identity. Monster Hunter had to add "series bonuses" back later precisely because the fantasy of *wearing a set* had been engineered out |

**Recommendation: Option A, discrete breakpoints.**

Three reasons, in order of weight:

1. **OD5 asks for sets, and Option B is a system that has no sets in it.** It is a good system; it is
   not the one that was decided.
2. **Legibility is the scarce resource here.** This game already asks a player to hold ~15 equip slots,
   a rarity ladder with deliberate overlap (OD4), sockets, and charms in their head at once. A model
   whose rule fits in one tooltip line is worth a lot in that context.
3. **We are not at Monster Hunter's content scale**, and Option B's cost is paid up front.

**What is kept from Option B.** The mechanism is written as a *counter with thresholds*, not as a
hardcoded 2/4/6 ladder. Thresholds are rows: a set may use 2/4, or 2/3/4, or 2/4/6. That is where the
"escalating" of OD5 lives, and it is data. Option C's weighted pieces are **not shipped** — nothing would
consume a weight column in wave 1, and SC7 is explicit that a row no code consumes is a lie in a table.
The hybrid-frame problem it was invented for is solved better in §3.7 by a role rule.

### 3.2 Where the interesting bonus sits — an inversion, stated loudly

Genre convention puts the capability at the *top* threshold: the 6-piece is the payoff, and the lower
tiers are stat filler. Diablo 3 is the cautionary case — 6-piece bonuses were multiplicative damage
multipliers in the thousands of percent, every build was "the set", and Diablo 4 abandoned sets almost
entirely in favour of Aspects.

**We invert it.** The **capability sits at the lowest threshold**, and the higher thresholds grant plain
numbers:

> **Every set grants exactly one capability atom** — an atom of a non-`stat.*` kind: `status.apply`,
> `status.clear`, `shield.grant`, `resource.delta`, `resource.economy`, `spawn.entity`, `board.action`,
> `grid.spawn`, `grid.clear`, `box.set`. **It sits at the set's lowest threshold.** Every higher
> threshold grants `stat.modify` / `stat.derived` atoms only.

So a 2-piece splash gives you the thing rares cannot roll, and full commitment gives you numbers. Two
half-sets is then a real build — two capabilities and no big numbers — sitting beside one full set — one
capability and big numbers. Those are two different, roughly equal answers, which is the definition of a
build space rather than a checklist.

The cost is real: it removes some of the "chase the last piece" feeling. It is paid deliberately, because
it is the single rule that makes §3.6 true. Flagged to the owner in §9.

### 3.3 How membership is expressed

| Option | Verdict |
|---|---|
| A tag in `tags_json` (`set: ember-legion`) | **Rejected.** Free text cannot be validated, an author typo makes a silent orphan, and you cannot enumerate a set's members to render "3 / 4" |
| A `set_id` column on `effect_container` | **Rejected.** Adding a column is ask-first under E5's boundaries, and it still cannot express a threshold or a member's role |
| A `set` container that lists members | **Rejected as written.** `effect_container_atom.atom_id` is an FK to `effect_atom`, not to another container — listing member containers needs a new table anyway |
| **A membership table plus a `set`-kind container per threshold** | **Picked.** The membership table is enumerable and validatable; the threshold bonus reuses the container machinery whole |

The picked shape is in §4. It adds three tables and zero columns to existing atom tables.

### 3.4 Piece count

**Typical set: 4 members. Grand set: 6 members, and rare.**

OD2 puts roughly 15 equip slots on a pure frame. The denominator matters more than the numerator:

| Set size | Share of ~15 slots | Free slots left | Two of them |
|---|---|---|---|
| 4 | 27% | 11 | 8 used, 7 free |
| 6 | 40% | 9 | 12 used, 3 free |
| 8 | 53% | 7 | impossible |

Diablo 2 shipped 3–6 piece sets over ten slots, and its worst set-jail cases were the 6-of-10 ones.
Elder Scrolls Online runs 5-piece sets over twelve-ish slots and is explicitly built so you wear two
5-piece sets plus a 2-piece — which is why it is the best-regarded build space in the genre. Four of
fifteen is deliberately closer to ESO's ratio than to D2's.

Hard rules:

- **Every set has a threshold at 2.** No exceptions. A set whose first bonus is at 3 has an invisible
  first step and cannot be splashed.
- **The top threshold must be ≤ the member count** (`SetThresholdUnreachable`).
- **A set may claim at most 6 roles**, so at least 9 slots on a pure frame are always rare or unique
  territory.
- **Grand sets are the exception, not the pattern.** A 6-piece set must also carry thresholds at 2 and 4,
  so a partial grand set is playable and the last two pieces are a chase rather than a cliff.

### 3.5 Set jail — what actually prevents it

Set jail is a set so strong every build converges on it, after which gear outside the set is unwearable.
Five mechanisms, in descending order of how much work they do.

**1. The capability sits at the lowest threshold** (§3.2). The load-bearing one.

**2. Set tiers may not grant a `More`-op modifier.** `More` is the multiplicative op on `stat.modify`
(families `bulwark`, `savagery`); derived channels have no `More` at all (atom-catalog-ssot §4.1, §4.2).
A set restricted to `Flat` and `Increased` is **self-diminishing by construction**: `Increased` sums with
every other `Increased` on the actor, so the set's share of total power falls as the rest of the build
grows. That is the diminishing-returns requirement met by compose rules that already exist, not by a
bolt-on curve. Violation is `SetTierForbiddenAtom`. This rule makes the Diablo 3 failure literally
unauthorable.

**3. A hard budget on total set value.** Denominated in **affix-equivalents (AE)** — one AE is one rolled
affix at the middle of the set's tier window, a unit I8 owns:

> Sum of all a set's tier atoms ≤ **1.5 AE per member piece**.

A 4-piece set is capped at 6.0 AE. The piece-level deficit it must pay for is 4–6 AE (§3.9). A completed
set is therefore roughly break-even in raw stats and ahead by one capability — desirable, not mandatory.
When E9 lands, this cap converts to a power-vector budget; until then it is an authoring rule stated in
AE, per SC9.

**4. No set owns both weapons.** A set may claim at most one of `armament-primary` /
`armament-secondary`. Weapons are where build identity lives; a set that owns both owns the build.
Violation is `SetRoleForbidden`.

**5. Two partial sets are legal, budgeted for, and expected** (§3.6).

### 3.6 Can a player run two partial sets at once?

**Yes. Explicitly, and it is the design target.**

Nothing in the model forbids it: the counter is per set id, thresholds are per set, and the tier
bindings are independent rows. Two 4-piece sets at 2 pieces each costs 4 of ~15 slots and leaves 11 for
rares.

This is the rule that decides between builds and a checklist, and it is why the capability was moved to
the 2-piece threshold in §3.2. If the payoff sat at the top, two partial sets would give two lots of
stat filler and nothing else, and the "choice" would be fake.

There is **no cap on the number of sets** a wearer may be partially in. The slot budget is the cap: with
4-piece sets and a threshold at 2, the ceiling is seven partial sets on a pure frame, and a build that
does that has seven capabilities, no set numbers at all, and one free slot. That is a legal, weird, and
probably bad build — which is what a build space is supposed to contain.

### 3.7 Frames — can a set span them, and can a hybrid complete one?

**A set is frame-neutral. Its members are frame-specific base types, at most one per (role, frame).**

Ember Legion can declare a humanoid `item.ember-helm` **and** a plant `item.ember-crown`, both on the
head-protective role. They are two rows, one role, one point. A pure humanoid completes the set with
humanoid bases; a pure plant with plant bases; nobody sees a slot they cannot fill.

A set may also be authored for one frame only. That is legal, and it is how flavour-locked sets are
expressed — a plant-only bloom set simply has no humanoid member rows.

**Hybrids can complete every set.** This is guaranteed, not hoped for:

> **A set's member roles must all be in the hybrid role core** — the roles that exist on every frame.
> Violation is `SetRoleNotUniversal`, at load.

OD3 gives hybrids 12–13 slots instead of ~15, and each role accepts a base type from either frame. So a
hybrid pays for breadth in slot count — and only in slot count. It completes any set, and it completes
sets with *more* freedom than a pure frame, because it can mix: a humanoid `ember-helm` with a plant
`ember-bark` completes two roles of Ember Legion on one body. Since membership is keyed on the member
container and not on the wearer's frame, that works with no special case at all.

The alternatives were considered and rejected. Lowering thresholds for hybrids makes the tooltip lie —
"4-piece" would mean different things on different bodies. Weighting hybrid pieces above 1 has the same
problem inverted. Barring hybrids from sets is the named failure mode "sets make hybrids unplayable",
and hybrids are a headline feature of the base game (item-ideal §4), not an edge case.

### 3.8 Legacy sets when new slots unlock

Two directions, and only one is dangerous.

**A role is added.** Nothing breaks. Membership is declared per role and thresholds are absolute counts,
so an existing 4-piece set is still a 4-piece set on a 16-slot frame. It is a slightly smaller share of
the body, which is the correct drift.

**A role is removed, or leaves the hybrid core.** Every set using it becomes invalid — and it fails
**loudly**, at import. `SetRoleNotUniversal` fires at load, and import policy is **all-or-nothing**
(definitions §10): one bad row and nothing imports. So an I2 change that would strand a legacy set stops
the import rather than shipping a set that can never be completed. That is the protection, and it is
already policy rather than something this lane has to build.

**Roles are append-only**, on the same reasoning as rarity ordinals and the element roster. This lane
asks I2 to state that as a rule.

**Slot unlocking as progression** (item-ideal §5.6) is the one real hazard: a player with 6 unlocked
slots cannot wear a 6-piece set no matter what they own. Rule, pending I2's schedule:

> A set's top threshold must be ≤ the number of slots unlocked at that set's `level_req`.

### 3.9 Are set pieces rolled, fixed, or fixed with a rolled range?

**Fixed core plus a small, narrow rolled pool.** Concretely, using columns that already exist:

| Piece property | Value | Column |
|---|---|---|
| Fixed identity atoms | 2, at a fixed tier | `effect_container_atom` |
| Rolled affixes | 2 | `pool_rolls = 2` |
| Tier window | 2 tiers wide, from a set-specific pool | `min_tier` / `max_tier` |

Compare against a rare of the same rung: 4 rolled affixes from the general pool, in a wider window.

The deficit is therefore **a flexibility deficit, not a raw-power one**. A set piece has about as many
modifier lines as a rare, but two of them are fixed, so on any given build roughly 40% of the piece is
off-plan. Expected cost ≈ **1.0–1.5 AE per piece**, which is what §3.5's budget is sized to buy back.

Why the two alternatives lose:

- **Fixed like a unique.** Once you own the set, every drop in those roles is dead. The loop that makes
  an ARPG work stops for a quarter of the body.
- **Rolled like a rare.** A set piece is then a rare *plus* a set bonus, so it is strictly better in
  isolation and the correct build is always "the set". That is set jail arriving through the item layer,
  where none of §3.5's rules can reach it.

**Is a set piece ever an upgrade?** Yes — its two rolled affixes vary, so there is a real chase for a
good copy, with a bounded tail because the window is narrow. And this is the direct application of OD4's
overlap: a well-rolled rare beats a badly-rolled set piece *as an item*, and the set bonus pays the
difference back. Both remain worth picking up.

### 3.10 Do socket inserts or charms count toward set completion?

**No. Neither. Hard no in wave 1.**

The count is `COUNT(DISTINCT role)` over **equip-slot bindings**. An insert lives in a socket inside an
item, not in an equip slot. A charm is not equipped at all — that is I10's whole premise.

Three reasons this is not merely a definitional preference:

1. **The budget denominator breaks.** §3.5's cap is 1.5 AE per member *piece*, and a piece costs an
   equip slot. If a charm counted, a set could be completed without spending the slots the bonus was
   priced against.
2. **The one-point-per-slot invariant breaks.** An insert would let one item contribute two or more
   points, so a 4-piece set could be worn on two items.
3. **"Equipped" would stop meaning anything**, and every cut in the contract's §4 table depends on it
   meaning something.

Flagged to those lanes in §8. A "counts as the final piece" insert is the most-requested and
most-jail-producing feature in this space; if I4 wants it, it comes back as an owner decision.

A charm that **reads** set state ("+X while a 4-piece is active") is a different thing and is fine in
principle — reading is not counting. It needs a `setTierActive` predicate leaf, and the leaf list is
closed (atom-catalog-ssot §8), so that is a reviewed change, not an assumption.

---

## 4. Data shape

### 4.1 What is reused unchanged

| Existing thing | Used as |
|---|---|
| `effect_container` (`container_kind`, `slot`, `rarity`, `min_tier`/`max_tier`, `pool_rolls`) | both the **member base types** (kind `item`) and the **tier bonuses** (kind `set`) |
| `effect_container_atom` | the tier bonus's atoms — fixed core, `pool_rolls = 0` |
| `effect_container_pool` | the member piece's small rolled pool (§3.9) |
| `effect_instance` / `effect_instance_atom` | one canonical instance per tier container (§4.5) |
| `effect_binding` — `owner_kind`/`owner_key`, `slot`, `priority`, `source` | how a tier reaches the actor, and how it is withdrawn |
| The bind gate (`src/FusionRpg.Core/Effects/Atoms/BindGate.cs`) | unchanged; a tier binding goes through it like any other |

**This lane adds zero columns to any existing atom table.** It adds three tables and asks for one enum
value.

### 4.2 Three new tables

```sql
CREATE TABLE item_set (
  set_id       TEXT NOT NULL PRIMARY KEY,   -- ^[a-z0-9]+(-[a-z0-9]+)*$, must not end in -NN
  display_name TEXT NOT NULL,
  level_req    INTEGER,                     -- informational; the real gate is I11's, per member
  enabled      INTEGER NOT NULL DEFAULT 1,
  revision     INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE item_set_member (
  set_id       TEXT NOT NULL,
  container_id TEXT NOT NULL,               -- FK effect_container, kind 'item'
  role         TEXT NOT NULL,               -- I2's frame-neutral role name
  frame        TEXT NOT NULL,               -- humanoid | plant; validated against I3's base type
  PRIMARY KEY (set_id, container_id),
  UNIQUE (set_id, role, frame)
);

CREATE TABLE item_set_tier (
  set_id          TEXT NOT NULL,
  pieces_required INTEGER NOT NULL,
  container_id    TEXT NOT NULL UNIQUE,     -- FK effect_container, kind 'set'
  PRIMARY KEY (set_id, pieces_required)
);
```

**Named consumers, per SC7.** All three are read by one new Core component,
`src/FusionRpg.Core/Items/Sets/SetEvaluator.cs` — a pure function from *bound member containers* to
*tier container ids that should be bound*. `item_set_member` is additionally read by the tooltip to
render "3 / 4". No row in these tables is inert.

`role` and `frame` are **denormalized copies** of what I3 declares on the base type, kept locally so the
`UNIQUE (set_id, role, frame)` constraint can exist in SQL. They are validated against I3's declaration
at load and a mismatch is `IdMismatch` — the same derive-and-check pattern `atom_id` already uses
(definitions §1). If I3 ends up exposing role and frame in a shape this table can join, drop the copies.

SQL lives in `src/FusionRpg.Data` — `RpgStore.ItemSets.cs` — or `guard-dal.ps1` goes red.

### 4.3 The tier container id, and why it has a single dot

A tier bonus is a container, so it needs a `container_id`. The grammar in definitions §1 is:

```
^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$
```

Two things follow, and only one of them is a change.

- `set` must join the alternation, and `ContainerKind` must gain the value
  (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:77-84`). SC3 reserved the string; the code does not
  have it yet. This is the E5 ask-first change SC3 anticipated.
- The body is `[a-z0-9-]+` with **no dot**, so `set.ember-legion.04` would not parse even after the
  alternation is widened. **The id is therefore `set.{set_id}-{pieces:D2}`** — `set.ember-legion-04`.
  One dot, kebab body, matches the existing structure exactly. No regex restructuring.

The **two-digit zero pad is load-bearing**, not cosmetic. The actor effect list sorts by `container_id`
**ordinal** (definitions §5). Unpadded, `set.x-10` sorts before `set.x-2` and a ten-piece set would
resolve its tiers out of order. Padded, ordinal order equals numeric order, and the lower tier always
resolves first for free.

A `set_id` ending in `-NN` would collide with a tier id, so the grammar forbids it (`IdMismatch`).

### 4.4 Where set atoms go, and at which owner scope

**A set tier binds to exactly the owner scope its member pieces are bound to, and never to `match`.**

The wearer wears the set; the squad does not. Binding a tier at `match` would silently turn one demon's
gear into a team buff, and it would make the §3.5 budget cap unenforceable because the denominator — one
actor's slots — would no longer be the thing being paid. A tier binding aimed anywhere but the wearer's
own scope is `ScopeUnsupported`.

The commander's `standard` slot (item-ideal §5.6) proposes `match`-scoped **item** atoms. That proposal
does not extend to set tiers in wave 1; squad-scoped set bonuses are §9's question, not a decision made
here.

`priority` is **0**, identical to an item binding. Raising it would let a set bonus pre-empt an item proc
for no design reason, and the tiebreak we want — `container_id ASC` — is content-derived and stable,
which is what definitions §5 requires.

All three tables join the E8 covered-table registry, which is an explicit `contentHashSchemaVersion` bump
(definitions §8). Adding them silently would leave every stamp made before them looking valid.

### 4.5 The lifecycle — which rows change, and when

This is the answer to *"equipping a fourth piece must turn the 4-piece bonus on."* It is a binding
operation and the bind gate has real rejection codes, so the order of operations matters.

**Equipping a piece** is one transaction:

1. **Bind the item.** Insert the `effect_binding` row with the item's **role** in the `slot` column
   (item-ideal §6.4). The bind gate runs. If it rejects — `LevelTooLow`, `RuntimeUnsupported`,
   `StaleInstance` — nothing else happens and the set count is untouched.
2. **Recount.** For every set with a member among the wearer's bindings, count the **distinct roles**
   filled:

   ```sql
   SELECT m.set_id, COUNT(DISTINCT m.role)
     FROM effect_binding b
     JOIN effect_instance  i ON i.instance_id  = b.instance_id
     JOIN item_set_member  m ON m.container_id = i.container_id AND m.role = b.slot
    WHERE b.owner_kind = $k AND b.owner_key = $v AND b.source = 'equip'
    GROUP BY m.set_id;
   ```

   One indexed lookup on `ix_effect_binding_owner`. Not a hot path, and never per frame.
3. **Resolve the target tier set.** Every `item_set_tier` row for that set with
   `pieces_required <= count`. Cumulative, so 4 pieces yields `{02, 04}`.
4. **Diff against what is bound.** Withdraw the tier bindings no longer in the target set; bind the ones
   newly in it. Withdrawal is by `source = 'set:{set_id}'` plus the tier's `container_id`.
5. **Bind the new tiers.** A tier container has `pool_rolls = 0` and every value spec `Fixed`, so
   instantiating it rolls nothing and produces the same rows every time. **One canonical instance per
   `(tier container_id, catalog_revision)`** is created on first need and reused by every wearer. That
   is a determinism property, not just an optimisation: the tier's `values_json` cannot drift between
   two actors wearing the same set.
6. **Commit.**

**Unequipping** is the same steps with the item binding deleted instead of inserted. Dropping from 4 to
3 pieces withdraws `set.ember-legion-04` and leaves `set.ember-legion-02` bound.

Three rules the lifecycle needs stated:

**Counting is per role, not per item.** Two copies of the same set ring worn in `jewel-minor A` and
`jewel-minor B` count as **one**, because the member row declares one role and the join requires
`m.role = b.slot`. This closes the obvious cheese — buy four copies of the cheapest member, wear them
everywhere — without a special case. It is a **disclosure requirement** for I13's tooltip, not a
rejection: equipping a duplicate is legal, so the UI must show "3 / 4" and say why the fourth did not
count. A silent non-count is the kind of surprise SC6 exists to remove, even where a rejection would be
the wrong answer.

**A failed tier bind does not roll back the equip.** Set state is derived, and a tier that cannot bind
in *this* runtime is exactly the case E6 already describes: "the same container may bind on the lawn and
be rejected in battle." The durable equip stands; the tier is simply not on the effect list in that
runtime, and the rejection is surfaced with its code. Rolling the equip back would mean a lawn-legal
tier blocks equipping a piece in battle, which is worse than the disease. What *is* forbidden is a
partial tier bind — an instance binds whole or not at all, which `BindGate.Check` already enforces by
checking every atom before accepting any.

**Recount triggers — the complete list.** Equip · unequip · item instance destroyed or salvaged (I13) ·
member container disabled by a content update (`enabled = 0`) · `item_set.enabled` flipped to 0 ·
catalog revision bump · equip slot locked or unlocked, if I2 ships slot unlocking. Anything that changes
which member containers are bound to an owner must run step 2. A recount that is skipped leaves a stale
tier binding — a live effect with no visible source, which is the worst failure this lane can produce.

---

## 5. Validation and reason codes

**Every code below except the bind-time rows is load-time content validation.** An author sees them; a
player never does. The player-facing surface reuses existing codes only.

### 5.1 Reusing existing codes

| Bad input | Code |
|---|---|
| `item_set_member.container_id` not in `effect_container` | `UnknownContainer` |
| Member container's kind is not `item` | `UnknownContainer` |
| Tier container id ≠ `set.{set_id}-{pieces:D2}` | `IdMismatch` |
| Tier container's kind is not `set` | `IdMismatch` |
| `set_id` ends in `-NN` (collides with a tier id) | `IdMismatch` |
| `member.role` / `member.frame` disagree with I3's base type | `IdMismatch` |
| Two `item_set_tier` rows with the same `pieces_required` | `DuplicateKey` |
| Tier container has `pool_rolls > 0` | `UnsatisfiablePool` — a set bonus rolls nothing |
| Same atom twice in a tier's core | `DuplicateAtomInContainer` |
| Tier atom outside the container's tier window | `TierOutOfWindow` |
| **Bind:** tier binding at `match`, or at a scope other than the pieces' owner | `ScopeUnsupported` |
| **Bind:** tier instance references a disabled atom, or `item_set.enabled = 0` | `StaleInstance` |
| **Bind:** tier atom has no consumer in this runtime | `RuntimeUnsupported` |
| **Bind:** member's `level_req` above the owner's level | `LevelTooLow` |

### 5.2 New codes proposed

Six. Each is a distinct authoring mistake, in the spirit of `UnknownTrigger` versus `TriggerNotAllowed` —
one shared code with a message would make the import log unreadable.

| # | Bad input | Proposed code |
|---|---|---|
| 1 | `pieces_required` exceeds the set's member count; or a set has no tier rows at all; or no threshold at 2 | `SetThresholdUnreachable` |
| 2 | Two members declared on the same `(role, frame)` | `SetRoleCollision` |
| 3 | A member role is not in the hybrid role core (§3.7) | `SetRoleNotUniversal` |
| 4 | A set claims both `armament-primary` and `armament-secondary`, or more than 6 roles | `SetRoleForbidden` |
| 5 | A tier grants a `More`-op modifier, or a match-scope-only family (`warding`, `resilience` — G8), or `stat.modify` on `defense` | `SetTierForbiddenAtom` |
| 6 | A set has zero or more than one capability atom, or its capability is not at the lowest threshold | `SetCapabilityMisplaced` |

Code 5 deserves a note. `warding` and `resilience` bind **only** at `match` scope (G8, definitions §6),
and a set tier binds at the wearer's scope by §4.4. So a set carrying `+defense` would be rejected at
bind for every wearer, forever. Catching it at load instead of at every player's equip is the whole point
of having a content-validation phase.

### 5.3 How set atoms interact with the actor effect list's ordering

The list iterates `(priority DESC, container_id ASC, seq ASC)`, compared **ordinal** (definitions §5).

- **Set tiers take `priority = 0`**, identical to items. The tiebreak is then `container_id`, which is
  content-derived and stable — exactly what §5 of definitions requires, and never the generated
  `binding_id`.
- Ordinally, `item.` < `patron.` < `set.` < `skill.` < `species-passive.` < `trait.` < `world-buff.`
  So **set atoms resolve after item atoms** at equal priority. That is a stated consequence, not an
  accident.
- Within a set, `set.x-02` sorts before `set.x-04`, so lower tiers resolve first — the zero pad in §4.3
  is what keeps that true for a hypothetical ten-piece set.
- **Most of it does not matter, and that is deliberate.** Higher tiers grant only `stat.modify` /
  `stat.derived`, which are permanent modifiers carrying no trigger at all (definitions §14.2). They
  compose Flat → Increased → More regardless of list order. Order is observable only for the **one**
  capability atom per set, and only against other procs on the same trigger consuming the same RNG
  stream. Confining a set to one capability atom therefore also confines its ordering surface to one
  atom.
- A capability atom sharing an `icd_key` with an item atom shares one cooldown clock (definitions §14.1).
  That is a legitimate authoring tool — a set's proc and an item's proc on one clock — and it is the
  *only* cross-source coupling this lane permits.

---

## 6. Worked examples

**All numbers are illustrative, not balanced.** Units are stated on every one.

### 6.1 Ember Legion — a 4-piece set

Header: `item_set('ember-legion', 'Ember Legion', level_req 20)`.

| Role | Humanoid member | Plant member |
|---|---|---|
| head-protective | `item.ember-helm` | `item.ember-crown` |
| core-protective | `item.ember-plate` | `item.ember-bark` |
| manipulator-offense | `item.ember-gauntlets` | `item.ember-fronds` |
| jewel-major | `item.ember-torc` | `item.ember-pollen` |

Eight member rows, four roles, all four in the hybrid role core. Two tiers:

**`set.ember-legion-02`** — the capability, one atom:

| Atom | Kind | Value | Unit |
|---|---|---|---|
| `atom.warded.fire.t3` | `shield.grant`, `OnSpawn`, `icd_key: ember-legion` | 120 shield | game units, shield runtime scale |

**`set.ember-legion-04`** — the numbers, three atoms:

| Atom | Kind | Value | Unit |
|---|---|---|---|
| `atom.might.t3` | `stat.modify`, `atk` Flat | +35 | game units |
| `atom.vitality.t3` | `stat.modify`, `maxHp` Flat | +45 | game units |
| `atom.elemental-power.fire.t3` | `stat.derived`, `combat.power.fire` Flat | +30 | **resolver points** |

**Two honest notes.**

The third atom **binds nowhere today.** `stat.derived` is quarantined `None/None/None` (defect D6) until
E12 wires `BattleStatComposer`. Wave-1 Ember Legion either ships without it or ships knowing it is inert.
This is SC2's "say so where it bites your lane", and it bites hard: element power is the most natural
thing a themed set would grant, and it is the one thing a themed set cannot grant yet.

`+35 atk` and `+30 fire power` are **not comparable numbers**. One is 35 attack points; the other is 30
resolver points on a sigmoid where `CritRateScale = 100.0`. Tier bands are authored per channel family
and never copied across (SC4).

**Budget check.** Cap is 4 members × 1.5 AE = **6.0 AE**. Shield capability ≈ 2.0 AE; three t3 stat atoms
≈ 1.0 AE each = 3.0. Total **5.0 ≤ 6.0**. Passes. The pieces' forgone flexibility is 4 × ~1.2 = ~4.8 AE,
so a completed Ember Legion is worth roughly a third of an affix more than four comparable rares — plus a
fire shield rares cannot roll.

### 6.2 A hybrid running two partial sets

A hybrid demon with 13 slots (OD3), wearing:

- `item.ember-helm` (humanoid base) in head-protective
- `item.ember-bark` (plant base) in core-protective
- `item.tithe-soil` (plant base) in girdle-resource
- `item.tithe-roots` (plant base) in footing

Counts: Ember Legion **2**, Root Tithe **2**. Bound tiers: `set.ember-legion-02` and `set.root-tithe-02`.
Neither 4-piece.

What the wearer has: a 120-point fire shield on spawn, and Root Tithe's capability `atom.sunbloom.t2`
(`resource.economy`, +5 sun per kill, `capPerMatch` honoured in the runner). Four slots spent of
thirteen; **nine free for rares**.

Arithmetic: 4 pieces × ~1.2 AE forgone = **4.8 AE** paid. Two 2-piece capabilities ≈ 2.0 + 1.8 =
**3.8 AE** returned in stat terms, plus two capabilities the rolled pool cannot produce at all. Roughly
break-even on paper, ahead on options — which is the intended relationship to committing fully to one
set.

Note the mixed frames: a humanoid helm and a plant stem-piece on one body, both counting. That is OD3's
breadth doing real work, and it needed no special case.

### 6.3 The equip transaction, step by step

The wearer has 3 Ember Legion pieces and equips `item.ember-torc` into `jewel-major`.

| Step | What happens |
|---|---|
| 1 | `effect_binding` insert: instance `a3f1…`, owner `entity:1f4c`, `slot = 'jewel-major'`, `priority = 0`, `source = 'equip'`. Bind gate accepts |
| 2 | Recount returns `ember-legion → 4` |
| 3 | Target tiers = rows with `pieces_required ≤ 4` = `{set.ember-legion-02, set.ember-legion-04}` |
| 4 | Currently bound with `source = 'set:ember-legion'`: `{set.ember-legion-02}`. To bind: `set.ember-legion-04`. To withdraw: none |
| 5 | Fetch the canonical instance of `set.ember-legion-04` for this `catalog_revision` — `pool_rolls = 0`, all values `Fixed`, so no roll and no seed variation. Bind it: owner `entity:1f4c`, `slot = NULL`, `priority = 0`, `source = 'set:ember-legion'` |
| 6 | Bind gate: `stat.modify` is lawn-Full; the scope check passes because no atom is `warding`/`resilience` — and it cannot be, because `SetTierForbiddenAtom` refused that at load |
| 7 | Commit. The actor's effect list now iterates the four `item.ember-*` containers, then `set.ember-legion-02`, then `set.ember-legion-04`, all at priority 0, `container_id` ordinal |
| 8 | The player unequips the torc: count → 3, target tiers → `{02}`, so `set.ember-legion-04` is withdrawn by `(source, container_id)` and `02` stays bound |

---

## 7. Failure modes

Six, each with what in this design stops it.

**Set jail — one set so strong every build converges on it.** Five layered rules, §3.5: the capability
sits at the 2-piece threshold so splashing is always viable; `More` ops are banned so the set's
contribution self-dilutes as the build grows; total set value is capped at 1.5 AE per member against a
piece-level deficit of 1.0–1.5 AE; no set owns both weapon roles; and typical sets are 4 of ~15 slots so
two fit.

**Sets obsolete all rolled loot.** Set pieces roll **fewer** affixes from a **narrower** window than a
rare of the same rung (§3.9), so the general pool remains the only source of top-end lines. A set build
still farms rares for its 9–11 free slots, and by §3.4 no set may claim more than 6 roles, so that free
space is guaranteed rather than incidental.

**A 6-piece bonus nobody ever sees.** Typical sets are 4-piece. Grand 6-piece sets must also carry
thresholds at 2 and 4, so the ramp is felt three times. Every set has a threshold at 2
(`SetThresholdUnreachable`), and the capability lives there, so the first step is the most *interesting*
one rather than the least. The remaining half of this failure is a drop problem, not a design problem —
see the ask to I12 in §8.

**Sets make hybrids unplayable.** Member roles must all be in the hybrid role core
(`SetRoleNotUniversal`), so a hybrid completes every set. Because membership is keyed on the member
container and not on the wearer's frame, a hybrid may also mix frames within one set, which makes sets
*easier* for hybrids to complete than for pure frames — the correct pricing of OD3, which already charged
them 2–3 slots.

**Legacy sets break when new slots unlock.** Adding a role is inert. Removing one, or moving it out of
the hybrid core, fails `SetRoleNotUniversal` at load, and import is all-or-nothing (definitions §10), so
a broken legacy set stops an import instead of shipping. Slot unlocking as progression needs one more
rule — top threshold ≤ slots unlocked at the set's `level_req` — which is pending I2's schedule.

**A stale tier binding nobody can trace.** The one failure this lane can produce on its own: a recount
skipped after a salvage or a content disable leaves a set bonus live with no visible source. Prevented by
the complete recount-trigger list in §4.5, and detectable because every tier binding is stamped
`source = 'set:{set_id}'` — a reconciliation pass can recompute the expected set and diff it against
`effect_binding` at any time, offline, with the game closed.

---

## 8. What this lane needs from other lanes

1. **I2 — equip slots.** The final role list; **which roles are in the hybrid role core** (present on
   humanoid, plant, and every hybrid variant); whether roles are **append-only**; and the slot-unlock
   schedule if slots unlock with level. `SetRoleNotUniversal`, `SetRoleForbidden`, and the
   top-threshold rule all compile against these. Without the hybrid core list this lane cannot validate
   a single set.
2. **I3 — base types.** A member is a base type. I3 must expose **`frame`** and **`role`** on the item
   container in a shape this lane can validate against, so `item_set_member.frame` / `.role` stay checked
   copies rather than a second source of truth. If I3 makes them joinable columns, drop the copies from
   `item_set_member` and keep only what the `UNIQUE` constraint needs.
3. **I1 — rarity. A formal registration request.** Sets need their own rung on the ladder, because the
   `rarity` table is what supplies `pool_rolls`, `min_tier`, and `max_tier`
   (`src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:52-60`; `RarityRow` in
   `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:93`). Requested: a rung `set` with
   **`pool_rolls = 2`** and a **2-tier-wide window**, positioned so a well-rolled rare of the adjacent
   rung can beat a poorly-rolled set piece — OD4's overlap applied to this lane. Ordinals are
   append-only, so this must be registered before the ladder is frozen, not after.
4. **I8 — the affix pool.** Two things. (a) Set pieces roll from a **narrow, set-scoped pool** rather
   than the general pool; `effect_container_pool` is per container so the mechanism exists, but I8 must
   say whether a private pool may draw families the general pool also offers. (b) **The
   affix-equivalent unit** that §3.5's budget cap is denominated in is I8's to define. Until it does, the
   cap is a direction, not a number.
5. **I4 — sockets.** Position stated and flagged: **an insert never counts toward set completion**
   (§3.10). Two open questions for I4: do set pieces have sockets at all, and if they do, how does the
   socket-combo budget reconcile with the set budget? Two combination bonuses stacking on one item have
   no combined ceiling today and no lane owns that reconciliation.
6. **I10 — charms.** Position stated and flagged: **a charm never counts toward set completion**. A charm
   that **reads** set state is acceptable in principle but needs a `setTierActive` predicate leaf, and the
   leaf list is closed (atom-catalog-ssot §8) — a reviewed change, not an assumption.
7. **I11 — equip gating.** Set pieces use the same gate. Needed: a rule for a set whose members have
   different `level_req`, since the set is effectively gated at the maximum and the tooltip should say so
   rather than letting a player collect three pieces and stall.
8. **I12 — loot.** **Completion bias / duplicate protection on set drops.** Without it, the top threshold
   is statistically unreachable and "nobody sees the 6-piece" comes back regardless of anything designed
   here. The summon pity counters already in the tree are the precedent.
9. **I13 — inventory.** Salvaging or destroying an **equipped** set piece must route through the recount
   in §4.5. Separately, the comparison UI must show the **set delta**, not just the item delta, or every
   set piece reads as a downgrade against a rare and players never assemble one.
10. **I6 — instance mutation.** Can a set piece be enhanced or rerolled? This lane's position: **the
    fixed core is immutable, the rolled pool is rerollable.** If reroll can touch the fixed core, set
    identity becomes mutable and the §3.5 budget cap is unenforceable. I6 owns the model; this lane
    adopts it, but needs that one boundary honoured.
11. **The atom program (E5) — two changes, both anticipated by SC3 but neither in the code.**
    (a) `container_kind` gains `set`, and `ContainerKind` gains the enum value plus its prefix
    (`src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:77-84`). (b) definitions §1's `container_id` regex
    gains `set` in its alternation. The single-dot id form in §4.3 was chosen specifically so the regex's
    *structure* does not have to change. Both are ask-first under E5's boundaries.
12. **Whoever names the durable wearer scope — the largest external dependency.** A set tier binds at its
    pieces' owner scope. The seven scopes are `match` · `plant:N` · `zombie:N` · `entity:HEX` ·
    `player:N` · `sector:N` · `slot:N`, and **none is a durable per-actor scope for a demon or a
    commander**: `entity:` bindings are session-scoped and never durable (E6, Boundaries), and there is
    no commander actor at all (item-ideal §3). Every lane that binds equipment is blocked on this, not
    only this one — but this lane is blocked twice, once for the pieces and once for the tiers.

---

## 9. Open questions for the owner

Decisions deliberately not made here.

1. **Does a commander's set bonus buff the squad?** Item-ideal §5.6 proposes a `standard` slot whose
   atoms bind at `match` scope. §4.4 confines set tiers to the wearer's scope in wave 1. Extending that
   to `match` for the commander is a real option with a real cost — it reintroduces the budget problem
   the wearer-scope rule was written to close.
2. **Does the capability-at-the-lowest-threshold inversion stand?** §3.2 overturns genre convention. It
   buys the two-partial-sets build space; it costs some of the "chase the last piece" feeling. Stated
   loudly because it is the one place this lane deliberately departs from what shipped games do.
3. **Do grand 6-piece sets ship in wave 1, or wait?** They are where set jail historically lived, and
   4-piece sets alone would prove the system with less risk.
4. **Can sets be faction-locked?** Frame is body, faction is allegiance, and they are different axes
   (item-ideal §4). A plant-only set is a frame restriction and works today. A *zombie-faction* set —
   which a plant-bodied `peashooterzombie` could wear — is a second restriction axis nothing in this
   design supports or forbids.
5. **Roster scale.** Twenty demons × 4-piece sets is 80 set pieces before anyone is fully geared. The
   ideal's §8 open economic question — scarce gear, disposable gear, or a small deployable squad — hits
   this lane harder than most, because a set is only satisfying if it can actually be completed on the
   actors that matter.
6. **Set transmutation.** "Convert a rare into a set piece" is the standard crafting sink for this
   mechanic and is deliberately not designed here. It is an I6/I9 shape if it ever exists.
