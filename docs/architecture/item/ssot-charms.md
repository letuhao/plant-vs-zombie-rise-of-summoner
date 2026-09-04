# Lane I10 — charms and trinkets

**Status:** Lane I10 SSOT, drafted 2026-08-22. Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

---

## 1. Scope

### This lane owns

- **The carried-bonus mechanic**: what a charm is, and the rule that holding one — not wearing one —
  is what makes its atoms reach an actor.
- **Whose bonus it is.** The owner scope a charm binds at, and why (§3.1). This is the lane's crux.
- **Where a charm's carry state lives** — the *attunement* marking, distinct from where the item row
  is stored.
- **The charm carrying limit** — the attunement-point budget, its unit, and how it grows.
- **Charm classes** — rolled versus fixed-unique, and the tier discipline that keeps a charm weaker
  per actor than the equivalent equip affix.
- **Resonance** — the escalating bonus for carrying several charms of one axis, and the boundary that
  keeps it from being a set bonus by another name (§3.5).
- **Lifecycle** — when a charm's binding applies and withdraws, and what a mid-run pouch edit does.
- **The loadout gate** and its reason codes.

### This lane does NOT own

| Thing | Lane that owns it |
|---|---|
| Equip slots and roles. **A charm is never equipped in a role and has no `slot`** | **I2** |
| Set bonuses across equipped items | **I5** |
| Socket combos inside one item | **I4** |
| Bag capacity and storage in general — I own only the **charm carrying limit** | **I13** |
| The rarity ladder and its ordinals — I read rarity, I do not define it | **I1** |
| Affix tier bands and the affix pool | **I8** |
| Base types and implicits | **I3** |
| Post-drop mutation (enhance / reroll) of a charm instance | **I6** |
| What you spend to grow attunement capacity | **I9** |
| Turning a drop event into a charm instance | **I12** |

### One naming collision to clear first

`stub.hp_charm` already exists in the tree and is **not a charm in this lane's sense**. It is one of
three hardcoded equip stubs, allowlisted into the `trinket` **equip slot**
(`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:25`, slot allowlist at `:12`). The word "charm"
there is a trinket base-type name. When the real item system lands, that row becomes an **I3 base type
in the `jewel-minor` role**, not an I10 charm. Nothing in this document inherits from it.

---

## 2. The model

A **charm** is a container of atoms that grants its bonus **while the player holds it attuned**, and
grants nothing while it merely sits in the bag. It has no role, no equip slot, no frame requirement,
and it is never worn by a body.

Equipment answers *"what is this specimen?"* A charm answers *"what is this run?"* Those are different
questions asked at different moments — gearing a demon is sticky and per-actor; picking a pouch is a
plan you make when you dispatch. Keeping them separate is the entire reason both mechanics can exist
without one eating the other.

The mechanism, end to end:

```text
charm drops ──► charm instance in the bag        (I13 stores it; no bonus yet)
      │
      ├─ player ATTUNES it ──► row in `charm_pouch`   (durable intent; still no bonus)
      │                          gate: AP budget · axis cap · duplicates
      │
      └─ run starts ─────────► snapshot into `charm_run_hold`
                                 + one `effect_binding` per charm at `player:{id}`
                                 + one binding per satisfied resonance tier
                                        │
                                        └──► atoms land on every deployed actor's effect list
      run ends ──────────────► bindings withdraw; the snapshot stays for audit
```

Three properties fall out of that shape and are worth stating before any table:

1. **Attunement is a marking, not a container.** The charm's row lives wherever I13 puts every other
   item. `charm_pouch` says *which* of the player's charm instances are attuned. This is the cut that
   stops I10 and I13 both claiming to store the same object.
2. **Holding is not carrying.** Owning ten charms and attuning three is normal. The budget bites at
   attunement, never at pickup, so a drop is never a punishment.
3. **The bonus is a run-level commitment.** A charm committed to a live run is unavailable to another
   one. That is what makes the budget a real cost when several expeditions run in parallel.

### Where charms actually reach an actor today — read this before authoring anything

The lane's crux is a scope question, and the scope answer is constrained by two facts in shipped code
that must be said out loud.

**Fact one — `player:{id}` is a parsed scope with no real consumer.** `OwnerScope` accepts
`player:{id}` and validates it as a decimal id > 0
(`src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:124-129`). But in the overlay's stat layer, `player:`
is a **stub that degrades to match-wide**: `StatApplyScope.Matches` returns `true` for any `player:`
key with the comment `// stub → match-wide apply`
(`src/FusionRpg.Core/Stats/StatApplyScope.cs:81-82`), and `IsMatchWide` reports `player:` as match-wide
outright (`:88-92`). The effect owner matcher does the same —
`return true; // match-scoped for now; player filter is grant-time`
(`src/FusionRpg.Core/Effects/EffectProcAndOwner.cs:59-60`).

**Match scope matches both sides.** `StatApplyScope.Matches` returns `true` for `match` before it looks
at `side` at all (`src/FusionRpg.Core/Stats/StatApplyScope.cs:52-53`). So a `player:`-scoped `+atk`
charm on the lawn today would buff the **zombies** as well as the plants. That is not a balance
concern, it is a correctness one.

**Fact two — battle executes no stat atom.** `stat.modify` is `Full / None / PlanOnly`
(`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:88`) — *"Battle's sink ignores FA1, so battle is
not supported"* (`:92`). `stat.derived` is quarantined `None/None/None` (`:106`, defect D6).
`resource.delta` is `Full / None / PlanOnly` (`:128`). Every kind in the registry is `Battle = None`
except `status.apply`, which is `Partial` (`:159`).

Put together: **there is no runtime today in which a charm both binds correctly and executes.** The
lane's honest position is therefore:

> **Charms are authored against `player:{id}`, and their first real consumer is E12's
> `BattleStatComposer` seam — the same seam every per-actor stat is already waiting on.** Until E12
> lands, a charm bind in **battle** rejects `RuntimeUnsupported`, and a charm bind on the **lawn**
> rejects `ScopeUnsupported`, because `player:` there means "everyone in the match, both sides".
> **Do not author charm content rows before E12.**

That is the same discipline [atom-family-library.md](../effect-atom/atom-family-library.md) §3.2
already applies to the twelve `stat.derived` families, and the same warning the catalog gives about
`status.expose.*`: *a row no code consumes is not content; it is a lie in a table.* Rejecting is what
SC6 demands; the alternative is a charm system that silently does nothing and takes six months to
notice.

---

## 3. Options considered, and the recommendation

### 3.1 THE CRUX — whose bonus is a charm?

> ✅ **SETTLED 2026-09-04 by owner ruling D33(a) — the answer is B, not C.** Charm resonance binds per
> deployed actor at `unique-actor:{specimenId}`. Option C's `player:{id}` is **withdrawn**: it is a
> scope the grammar accepts and the resolver cannot express, so `StatApplyScope.Matches` returns `true`
> unconditionally (`:82`) and a `player:`-scoped `+atk` charm buffs the zombies. The underlying
> architecture defect — `StatApplyScope` has no atom dimension at all, so effects on that path never
> reach the atom scope model — is filed against `buff-debuff-scope`
> ([buff-debuff-scope-map.md](../buff-debuff-scope-map.md)). **The lane text below is kept as the
> reasoning that produced the four options; read the banner for the answer.**

Equipment binds to an actor; inventory belongs to the player. Four answers, four different games.

| # | Answer | Owner scope | What the game becomes |
|---|---|---|---|
| **A** | **Commander only** | a commander actor's `entity:` | Charms are a 14th–16th equip slot with a different name. The commander does not exist yet — `players` is `(id, name, created_utc)` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:85-89`) — so this invents an actor to hold a mechanic |
| **B** | **Every deployed actor, individually** | one `entity:` binding per deployed actor | Correct scope per actor, but N bindings created and withdrawn per run, and `entity:` is explicitly **session-scoped and never durable** (`src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:38`, definitions §6). A binder rebuilds them on every deploy |
| **C** | **The squad, as one run-scoped binding** | `player:{id}`, one binding per charm | Charms are the run's dial. One binding, one withdraw, one audit row. Inventory ownership and binding scope agree |
| **D** | **Per-actor charm pouches** | per-specimen | Charms become equipment without slots. The ideal already flags roster scale as the unsolved economic question — 20 demons × 12 slots = 240 items (item-ideal §8) — and this makes it 20 × (12 + 3) |

**Recommendation: C.** One binding per attuned charm, at `player:{id}`, created at run start and
withdrawn at run end.

Why C and not the others:

- **Against A:** it needs an actor that does not exist, and once it exists, A is just "the commander
  has more slots" — which is I2's business, not a distinct mechanic. If the commander later gets a
  `standard` role binding at `match` scope (item-ideal §5.6), that is I2's slot, and it is a
  *different* thing from a charm: a worn item in a role versus a carried item in no role.
- **Against B:** the binding count scales with the squad, every one of them is session-scoped, and the
  withdraw path has to match N rows instead of one. The *effect* of B and C is identical once
  `player:` resolves to the player's deployed side; C gets there with one durable row.
- **Against D:** it multiplies the roster problem the ideal says must be solved *before slot counts
  freeze*. It also destroys the mechanic's identity — a per-specimen pouch is a slot, and I2 owns
  slots.
- **For C, positively:** the inventory belongs to the player, so the scope that matches ownership is
  the player. Nothing has to be re-derived at deploy time, one row explains one bonus, and a run's
  charm loadout is a single readable list — which is what makes the bonus legible (§7.3).

**The scope's meaning, stated so nobody has to guess:** a `player:{id}` binding whose `source` is
`charm` means *"the atoms apply to every actor this player has deployed, and to nothing else."* It does
**not** mean match-wide, and it must never be resolved as match-wide. That is a change request against
the two stubs cited in §2, and it is item 10 in §8.

### 3.2 Where charms live, and what they cost

| # | Option | Prior art (recalled, **unverified**) | Verdict |
|---|---|---|---|
| **a** | **Main bag; bag space is the cost** | Diablo 2 charms occupy inventory grid cells — small 1×1, large 1×2, grand 1×3 | **Rejected.** A real tradeoff, and genuinely hated. It also cannot work here: this game's inventory is a web list, not a spatial grid, so there is no tetris to pay with — only a row count, which is a flat cap wearing a costume |
| **b** | **Dedicated pouch, own capacity, no other cost** | Diablo 3's Kanai's Cube: three account-wide powers extracted from stored items | **Rejected as-is.** Clean, and free stats. Three flat slots also converges hard — everyone runs the same three |
| **c** | **Dedicated pouch priced by a size budget, plus authored drawbacks on the strongest charms** | Monster Hunter decorations slot into level-1/2/3 sockets — a size budget, not a count | **Chosen** |

**Recommendation: (c).** A charm's carry state is a **marking on the player's charm instances**, not a
separate storage location. I13 stores the row; I10 marks it attuned. The cost is:

1. **A budget** — attunement points, §3.3. Opportunity cost, and it is real because charm sizes vary.
2. **Exclusivity** — a charm committed to one live run cannot serve another. With expedition
   parallelism gated at 2 → 5 slots ([standalone-rpg-map.md:20](../standalone-rpg-map.md)), running
   wide means splitting your charms thin. This is the cost that scales with how much you are doing.
3. **Drawbacks on the top class** — a 5-AP signet carries an authored negative atom (§6.1). Not every
   charm; only the ones large enough to distort a build.

The price is deliberately *not* bag space (that is I13's, and (a) is rejected on its merits) and *not*
an upkeep resource — there is no player-scoped per-run meter to spend. The five actor resources are
actor-scoped, and souls are a durable ledger (`rpg_soul_balances`,
`src/FusionRpg.Data/Sqlite/RpgStore.cs:455`), not a run meter.

### 3.3 The limit mechanism

| Option | Build diversity |
|---|---|
| Slots (`n` named positions) | Every charm competes to be the biggest thing that fits a slot — converges |
| Flat count cap | Same problem, no packing at all — converges hardest |
| **Total-size budget** | Charms declare a size; `3+3+2` and `5+2+1` are both legal and different — **creates packing** |
| Growing capacity stat alone | A progression axis, but without varying sizes it is still a count — half a mechanism |

**Recommendation: a total-size budget on a growing capacity.**

| | Decision |
|---|---|
| Unit | **attunement points (AP)**, integer. Not a resource you spend — a capacity you fill |
| Per-charm size | `ap_cost` ∈ **{1, 2, 3, 5}**, authored on the charm's **base type**, never rolled |
| Capacity | **6 AP at start, 20 AP at cap**, granted by progression |
| Axis cap | At most **3 charms of one axis** in one snapshot |
| Copy cap | At most **2 instances of the same `container_id`**; a `unique_carry` charm caps at **1** |

**`ap_cost` is not rolled.** If it were, the whole game becomes rerolling for a 1-AP copy of a 5-AP
charm, and every other decision in this lane collapses. It is a base-type property.

**Why {1, 2, 3, 5} and not {1, 2, 3, 4}:** the gap between 3 and 5 is the point. At capacity 8 you can
take one signet plus 3 AP of small charms, or four mid charms, and those are visibly different builds.
A smooth ladder makes every combination roughly equivalent, which is a budget that does not budget.

**Why the axis cap is a rejection and not a soft cap.** A fourth same-axis charm contributing nothing is
a silent no-op, which is exactly what this program exists to remove. It refuses, with a reason code the
UI can print.

### 3.4 Rolled, fixed, or both

**Both, split by AP class.**

| Class | AP | Rolled? | `pool_rolls` | Rarity | Notes |
|---|---|---|---|---|---|
| **Minor** | 1 | yes | 0–1 | reads I1's ladder | one small always-on effect |
| **Standard** | 2–3 | yes | 1–2 | reads I1's ladder | a fixed core plus a draw — the bread and butter |
| **Signet** | 5 | **no** | 0 | hand-authored, top of I1's ladder | named, `unique_carry = 1`, carries a drawback |

Charms **do** have rarity and their atoms **do** have tiers — nothing bespoke. Two disciplines apply on
top, and they are this lane's, not I1's or I8's:

- **One band below equipment.** At equal rarity, a charm's `max_tier` is **at most one below** the
  window I1 grants an equip container of the same rarity. A charm applies to the whole deployed side;
  it buys that breadth with per-actor depth. (I1/I8 own the actual bands — §8 item 1.)
- **Flat only.** Charms may carry `stat.modify` with `op = Flat`. They may **not** carry `Increased` or
  `More` — so no `fortitude`, `ferocity`, `bulwark`, `savagery`. Two reasons, both load-bearing: a
  multiplicative bonus applied squad-wide compounds with every other multiplier in the build, and a
  percentage of an unseen base is the single least legible thing you can put on a tooltip (§7.3).

### 3.5 Do charms combine? — resonance, and why it is not a set

**Yes.** OD5 makes combination bonuses first-class, and a charm system without one is six independent
small numbers. The mechanism is **resonance**: carrying several charms sharing an **axis** grants an
extra bonus at 2 and at 3.

Axes reuse the five power categories the family library already uses — Offense · Survivability ·
Control · Utility · Economy ([atom-family-library.md](../effect-atom/atom-family-library.md) §3) — so
this introduces no new vocabulary.

The boundary against **I5's set bonuses**, stated as a table so the two cannot quietly become one
mechanism:

| | **I5 — set bonus** | **I10 — resonance** |
|---|---|---|
| Combination lives in | several **equipped** items | the **unequipped** pouch |
| Membership | a **closed, named list** authored per set | **open** — any charm tagged with that axis |
| Keyed on | a set id | a category tag |
| Breakpoints | 2 / 4 / 6 pieces | 2 / 3 charms |
| Bonus reaches | the actor wearing the pieces | **every deployed actor** |
| Container kind | `set` | `charm` |
| Author intent | "these five things are a matching set" | "you leaned into defence this run" |

The one-sentence version: **a set is a club with a guest list; a resonance is a category you can fill
with anything.** You cannot *design* a resonance the way you design a set — there is no "Ember set" of
charms — you accumulate one. They share no rows, no tables and no key, and the enrichment contract's
own cut (§4: *the lane that owns where the combination lives*) puts them on opposite sides.

**Resonance is a discount, not a multiplier.** Its magnitudes are deliberately smaller than one more
charm of that axis would be: roughly **a quarter of a standard charm at 2**, and **a further third at
3**. Narrowing your pouch should be rewarded, not dominant. Concrete numbers in §6.2.

### 3.6 Charms versus the `jewel-minor` roles (rings, grafts)

Keep both. They answer different questions, and the split is by **family**, not by magnitude — a ring
that is a smaller charm is a worse charm, and would deserve cutting.

| | `jewel-minor` (`ring-1/2`, `graft-1/2` — **I2's roles**) | charm (**I10**) |
|---|---|---|
| Belongs to | one actor | the player |
| Decided when | you gear that specimen | you plan a run |
| Changes | rarely; gearing is sticky | every run; the pouch is the dial |
| Limit | 2 slots, flat | AP budget, packing |
| Magnitude | the full tier band for its rarity | one band lower, applied ×N deployed |
| **Family set** | **conditional, per-actor riders**: `searing_strike`, `lifesteal`, `retribution`, `keen_edge`, `cruelty`, on-hit `status.apply`, `warded` | **always-on flat side-wide**: `vitality`, `might`, `mending`, `regeneration`, `sunbloom`, `midas`, `cleansing` |
| Fiction | grafted onto the body | carried by the commander |

**The rule that keeps them apart:** an atom family may not appear on both a `jewel-minor` base type and
a charm. Not "at a different tier" — at all. Rings own the conditional layer; charms own the always-on
layer. That is a real identity difference a player can state in one sentence, which is the test.

**If one had to be cut, cut charms.** Rings are load-bearing for I2's slot table and for per-specimen
expression; charms are the newer, more speculative mechanic and the one currently blocked on a
consumer. Saying so is cheaper than defending a mechanic that does not earn its schema.

### 3.7 Frames

**A charm is frame-blind.** A plant-frame actor benefits from a charm the commander carries, exactly as
a humanoid one does, and frame restricts nothing at the charm level.

Frame exists to keep *base types* honest — a turnip has no hands, so it cannot wear gauntlets
(item-ideal §5.2). A charm is not worn on a body, occupies no role, and never touches a frame's
vocabulary, so there is nothing for frame to gate. Frame-locking charms would also punish exactly the
mixed rosters the base game is built on: the generated species list is 18 zombie-side and 6 plant-side
with several Fusion hybrids (item-ideal §4).

**The atoms inside are still filtered.** item-ideal §5.4 already establishes that an affix family
declares which frames it serves — `swiftness` writes `zombieSpeed` and does nothing for a plant. That
filtering is the family's, applied per actor at compose time, and it is not a charm rule.

But a charm whose atoms are *all* frame-restricted must say so, or the player carries a dead charm and
never learns why. So: **`charm_def.frame_hint` ∈ `any` | `humanoid` | `plant`, stored and validated
against the container's atoms** — a mismatch is a rejection, not a warning. It changes nothing
mechanically; it is what the pouch UI prints and filters on. Its consumer is named, so it satisfies
SC7.

### 3.8 Lifecycle — against the two-async-systems rule

The binding **applies at run start** and **withdraws at run end**. Not at pickup, not at attunement,
not at deploy.

| Moment | What happens | Why |
|---|---|---|
| **Pickup** | the instance enters the bag. **No binding, no gate** | A drop must never be a punishment. Owning is free |
| **Attune** | row in `charm_pouch`. Gate runs: budget, axis cap, copies, uniqueness, `level_req`. **Still no binding** | Attunement is durable intent, not a runtime fact |
| **Run start** (match start / expedition dispatch / battle build) | the attuned set is **snapshotted** into `charm_run_hold`; one `effect_binding` per charm at `player:{id}` with `source = 'charm'`, `slot = NULL`, `priority = -100`; one more per satisfied resonance tier | This is the only moment the bonus reaches an actor |
| **During the run** | the pouch stays **editable**. A charm the run holds is **locked** — un-attuning it, or attuning it into a second run, refuses `CharmInUse` | See below |
| **Run end** | bindings withdraw by `source`; `charm_run_hold` rows go inactive and stay for audit | One withdraw path, one key |

**Why a snapshot and not a live read.** The RPG and the game are two async systems: *"The RPG works
from past events and contributes a signed delta later; it never reads or guesses current game state"*
([definitions.md:551-553](../effect-atom/definitions.md)). A pouch edit that reached into a running
match would have to land as a delta at a moment neither side agreed on, against a state the overlay is
forbidden to read. It would also break the seal: an expedition's outcome is *"sealed at dispatch by
recorded seed"* ([standalone-rpg-map.md:20](../standalone-rpg-map.md)), and a loadout that changes
after the seal makes the seal a lie. The snapshot is what makes a run reproducible from its inputs.

**Why the pouch stays editable and only the held charms lock.** Freezing the whole pouch while any run
is live would be miserable once expeditions run 20 hours in parallel. The shipped precedent already
solves this shape: `rpg_expedition_members` marks membership per run with `active`, and a partial index
enforces one live membership per specimen (`src/FusionRpg.Data/Sqlite/RpgStore.cs:512-519`). Charms
copy it exactly.

**Why refuse rather than hold.** Equipment holds a mid-run change — *"Mid-run ActiveBound equip held"*
([unique-actor-runtime.md:243](../unique-actor-runtime.md)) — and that is right for equipment, which is
a sticky per-actor choice a player edits between runs anyway. A charm is a per-run dial, so a silently
held change is a player believing they made a decision that did nothing. Refusing with a code the UI
prints is the SC6 answer.

---

## 4. Data shape

### 4.1 What is reused, unchanged

| Existing | How charms use it |
|---|---|
| `effect_container` | `container_kind = 'charm'`, `container_id` prefixed `charm.`; `rarity` reads I1's ladder; `min_tier` / `max_tier` carry the one-band-below window; `pool_rolls` 0 for signets; `level_req` enforced at the **pouch gate** against the **player's** level; `enabled`, `revision`, `tags_json` as-is |
| `effect_container_atom` | the fixed core, `seq` authoring order |
| `effect_container_pool` | the rolled half; `group` keeps its `(family_id, variant)` default |
| `effect_instance` / `effect_instance_atom` | a dropped charm is an instance like any item — `roll_seed`, `catalog_revision`, frozen `OnInstantiate` values. **No new instance shape** |
| `effect_binding` | `owner_kind = 'player'`, `owner_key = '{id}'`, `slot = NULL`, `priority = -100`, `source = 'charm'` (`src/FusionRpg.Data/Sqlite/RpgStore.AtomInstances.cs:75-84`) |
| `BindGate` | unchanged — the charm gate runs **before** it, at attunement, and the bind gate still judges runtime and scope at run start |

**`priority = -100`** is a deliberate value, not a default: the actor effect list sorts
`priority DESC, container_id ASC, seq ASC` (definitions §5), so charm atoms resolve **after** equipment
(priority 0). For `Flat`-only atoms the arithmetic is order-independent; the ordering exists so any
listing of an actor's sources reads *its own gear first, the account layer second*.

### 4.2 New tables — five, each with a named consumer (SC7)

| Table | Columns | Consumer |
|---|---|---|
| `charm_def` | `container_id` PK · `axis` · `ap_cost` · `unique_carry` · `frame_hint` | the pouch gate (budget, axis cap, uniqueness) and the pouch UI. **A `charm.` container with no `charm_def` row is not attunable** — that is how resonance containers stay out of the pouch |
| `charm_pouch` | `player_id` · `instance_id` · `attuned_utc` · PK `(player_id, instance_id)` | the pouch gate and the run-start binder |
| `charm_run_hold` | `run_kind` (`match`\|`expedition`\|`battle`) · `run_id` · `player_id` · `instance_id` · `ap_cost` · `seq` · `active` · PK `(run_kind, run_id, instance_id)`, plus `CREATE UNIQUE INDEX … ON charm_run_hold(instance_id) WHERE active = 1` | the run-start binder, the `CharmInUse` check, and run replay/audit. The partial unique index **is** the exclusivity rule, mirroring `ix_rpg_expedition_members_active` |
| `charm_resonance` | `axis` · `count_req` · `container_id` · PK `(axis, count_req)` | the run-start binder: count the snapshot by axis, bind the highest satisfied tier |
| `charm_attunement` | `player_id` PK · `capacity` · `updated_utc` | the pouch gate. Growth is written by progression (§8 item 11) |

Notes on why these are tables and not columns on `effect_container`:

- `axis`, `ap_cost`, `unique_carry`, `frame_hint` are meaningful for exactly one `container_kind`.
  `effect_container` already carries `slot` and `rarity` that only items use, so precedent exists — but
  repeating that for a fifth kind is how a shared table becomes a union of every kind's private fields.
  A side table keyed on `container_id` costs one join and needs **no E5 column ask**.
- `charm_resonance` deliberately points at ordinary `charm.` containers. A resonance tier is a container
  of atoms like everything else (SC1 / OD6) — the only thing special about it is that no `charm_def` row
  exists for it, so it can never be attuned.

### 4.3 The one schema change this lane cannot avoid

`charm` is a reserved `container_kind` (SC3), but it is **not yet accepted by shipped code**. Four
sites, all small, all a reviewed change against E5:

| File | What changes |
|---|---|
| `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:7-15` | add `Charm` to the `ContainerKind` enum — **append only**, after `WorldBuff`, so no existing ordinal moves |
| `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:77-86` | add `ContainerKind.Charm => "charm"` to `PrefixOf` |
| `src/FusionRpg.Core/Effects/Atoms/ContainerValidator.cs:17` | extend the `container_id` regex to include `charm` |
| `src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs:281-283` | add `"charm" => ContainerKind.Charm` to the string→enum map |

Also required: `definitions.md` §1's `container_id` grammar row, which is the SSOT the regex mirrors.
That document *wins over any spec*, so the regex change is not legal until the row moves — an ask, not
an edit. §8 item 10.

---

## 5. Validation and reason codes

The gate runs at **attunement** and again over the **snapshot** at run start. Both reject; neither
ignores.

### 5.1 Reused codes

| Bad input | Reason code |
|---|---|
| Attuning an instance whose container id does not resolve | `UnknownContainer` |
| Attuning an instance whose atom is disabled or withdrawn | `StaleInstance` |
| `level_req` set and the **player's** level is lower | `LevelTooLow` |
| Two copies of a `unique_carry` charm in one snapshot | `DuplicateKey` |
| More than 2 instances of the same non-unique `container_id` in one snapshot | `DuplicateKey` |
| Binding a charm on the lawn while `player:` resolves match-wide (§2) | `ScopeUnsupported` |
| Binding a charm in battle before E12 opens `stat.modify` | `RuntimeUnsupported` |
| A charm container whose pool cannot satisfy `pool_rolls` | `PoolRollsExceedGroups` |
| A charm container with an all-zero-weight pool | `UnsatisfiablePool` |
| A charm pool atom outside `[min_tier, max_tier]` | `TierOutOfWindow` |
| `ap_cost` outside {1, 2, 3, 5}; `capacity < 0`; malformed `axis` | `BadParamValue` |
| Owner key malformed (`player:0`, `player:abc`) | `BadOwnerKey` |

### 5.2 New codes — five, proposed against a closed list of 33

Adding one is a reviewed change (definitions §10). Five is a large ask and is stated as such.

| # | Bad input | Proposed code | Why not an existing one |
|---|---|---|---|
| 1 | Snapshot AP total exceeds `charm_attunement.capacity` | **`CharmBudgetExceeded`** | No capacity code exists. This is the mechanic's primary refusal and the player sees it constantly — it needs its own name |
| 2 | More than 3 charms of one `axis` in a snapshot | **`CharmAxisOverflow`** | A different player mistake from #1, with a different fix — drop *this* charm, not any charm |
| 3 | Un-attuning, or re-attuning elsewhere, a charm a live run holds | **`CharmInUse`** | Closest existing is `StaleInstance`, which means content went away. Here the content is fine and the *player* is elsewhere |
| 4 | Attuning an instance whose container is not `charm`, or is a resonance container | **`CharmNotCarryable`** | The container resolves fine, so `UnknownContainer` would lie |
| 5 | A `charm.` container holding an atom charms may not carry — `op = Increased`/`More`, `stat.derived` while quarantined, or any `board.*` / `grid.*` / `box.*` / `spawn.*` kind | **`CharmAtomNotPermitted`** | An **authoring** error, caught at import. Shaped like `TriggerNotAllowed` but keyed on container kind, not trigger |

**Fold-to-three variant, if five is refused:** keep `CharmBudgetExceeded`, `CharmInUse`, and
`CharmAtomNotPermitted`; route #2 into `CharmBudgetExceeded` with a detail string (both are "the loadout
does not fit a rule") and #4 into `BadParamValue`. This loses the UI's ability to point at the offending
charm for #2, which is a real cost — recorded so the tradeoff is visible.

### 5.3 Where each check runs

| Check | Import | Attune | Run start |
|---|---|---|---|
| `CharmAtomNotPermitted`, `frame_hint` matches atoms, `ap_cost` in range | ✅ all-or-nothing | | |
| `CharmNotCarryable`, `LevelTooLow`, `StaleInstance` | | ✅ | ✅ re-checked |
| `CharmBudgetExceeded`, `CharmAxisOverflow`, `DuplicateKey` | | ✅ | ✅ re-checked |
| `CharmInUse` | | ✅ | ✅ (the partial unique index) |
| `ScopeUnsupported`, `RuntimeUnsupported` (BindGate) | | | ✅ |

Re-checking at run start is not redundancy for its own sake: capacity can *shrink* (a respec), a
container can be disabled between attunement and dispatch, and a snapshot that binds under a stale gate
is exactly the kind of drift that produces an un-reproducible run.

---

## 6. Worked examples

**Every number below is illustrative, not balanced.** Units are stated on every value, per SC4.

### 6.1 Three charms

**Charm 1 — Hardened Seedcase** · rolled · **2 AP** · axis Survivability · `frame_hint: any`

| Field | Value |
|---|---|
| `container_id` | `charm.hardened-seedcase` |
| `container_kind` | `charm` |
| `rarity` | reads I1's ladder — illustrative rung "uncommon" |
| `min_tier` / `max_tier` | 1 / 3 |
| `pool_rolls` | 1 |
| Fixed core | `atom.vitality.t2` — `stat.modify`, `channel: maxHp`, `op: Flat`, `{+18, +18}` **hit points** |
| Pool (1 draw, group `atom.mending` + `''`) | `atom.mending.t1` +8 · `.t2` +14 · `.t3` +22 **hit points** (current-HP top-up on grant) |
| Per deployed actor at a t2 draw | **+18 max hit points, +14 hit points** |
| Runtime | `stat.modify` — **binds at E12**; rejects `RuntimeUnsupported` in battle today |

For scale: a t2 `vitality` affix on a `stem` (core-protective) would be roughly **+40 hit points** to
*one* actor. The charm is ~45% of that per actor and reaches the whole deployed side. That ratio *is*
the one-band-below rule made concrete.

**Charm 2 — Sunwarden Bead** · rolled · **1 AP** · axis Economy · `frame_hint: any`

| Field | Value |
|---|---|
| `container_id` | `charm.sunwarden-bead` |
| Fixed core | `atom.sunbloom.t2` — `resource.economy`, on-kill sun, **+3 sun per zombie killed**, `capPerMatch = 400 sun` |
| `pool_rolls` | 0 |
| Runtime | `resource.economy` is `Full / None / PlanOnly`. **Lawn-only** — and charms cannot bind on the lawn until `player:` stops resolving match-wide (§2) |

This one is in the document on purpose. It is a **PvZ-enrichment charm**: it does something only when
the game is open, so it can never be the only source of anything (SC8 — the injector may enrich, never
gate). It is also, today, **blocked twice over**, and it is the cleanest illustration of why §8 item 10
exists. `capPerMatch` is what keeps sun economy from being the only axis anyone carries — and the cap is
*"implemented in the runner"* ([atom-family-library.md](../effect-atom/atom-family-library.md) §3.5), so
it is a real ceiling, not an authoring convention.

**Charm 3 — Signet of the Hollow Crown** · fixed unique · **5 AP** · axis Offense · `unique_carry = 1` ·
`frame_hint: any`

| Field | Value |
|---|---|
| `container_id` | `charm.signet-hollow-crown` |
| `pool_rolls` | **0** — hand-authored, no draw |
| Fixed core `seq 0` | `atom.might.t4` — `stat.modify`, `channel: atk`, `op: Flat`, `{+22, +22}` **attack points** |
| Fixed core `seq 1` | `atom.vitality.t3` at **negative** magnitude — `stat.modify`, `channel: maxHp`, `op: Flat`, `{−30, −30}` **hit points** |
| Per deployed actor | **+22 attack points, −30 max hit points** |

The negative is legal and meant: *"Sign carries meaning and is per-kind"* (definitions §2), and the HP
floor of 1 (`stat.modify` caps, family library §2) keeps it from killing anything. Five AP is 62% of a
starting player's entire capacity, so a signet is a build, not a stat stick — and it costs real
survivability to run. **This is the answer to "charms are strictly-better free stats": the biggest ones
are trades.**

### 6.2 Resonance tiers (illustrative)

Each tier is an ordinary `charm.` container with no `charm_def` row, bound at `player:{id}` when the
snapshot's axis count reaches `count_req`.

| Axis | 2 charms | 3 charms |
|---|---|---|
| Survivability | `atom.vitality` — **+6 max hit points** | **+16 max hit points** |
| Offense | `atom.might` — **+4 attack points** | **+11 attack points** |
| Economy | `atom.sunbloom` — **+1 sun per kill** | **+3 sun per kill** |

The 2-tier is roughly a quarter of a standard charm; the 3-tier adds roughly a third more. Narrowing
pays, but never as much as one more charm of that axis would — which is what stops resonance from being
the only thing anyone optimises.

### 6.3 One carry loadout — the packing decision

Player capacity **8 AP**. Six deployed actors. The three charms above plus two supporting rolls:

| Charm | AP | Axis | Per deployed actor |
|---|---|---|---|
| Hardened Seedcase | 2 | Survivability | +18 max hp, +14 hp |
| Rootbound Ward | 2 | Survivability | +14 max hp |
| Tallykeeper's Notch | 1 | Offense | +5 attack points |
| Sunwarden Bead | 1 | Economy | +3 sun/kill |
| Signet of the Hollow Crown | 5 | Offense | +22 atk, −30 max hp |

| Loadout | Contents | AP | Axis counts | Resonance | Net per deployed actor |
|---|---|---|---|---|---|
| **A — wide** | Seedcase, Rootbound Ward, Notch ×2, Bead ×2 | 2+2+1+1+1+1 = **8** | S 2 · O 2 · E 2 | S-2, O-2, E-2 | **+38 max hp, +14 hp, +14 atk, +7 sun/kill** |
| **B — tall** | Signet, Notch ×2, Bead | 5+1+1+1 = **8** | O 3 · E 1 | O-3 | **+43 atk, −30 max hp, +3 sun/kill** |
| **C — illegal** | Signet, Seedcase, Rootbound Ward | **9** | — | — | refused **`CharmBudgetExceeded`** (9 > 8) |
| **D — illegal** | Signet, Notch ×3 | 8 | O **4** | — | refused **`CharmAxisOverflow`**; the third Notch also trips `DuplicateKey` (copy cap 2) |

That is the packing decision, in one table. A and B cost the same 8 AP and produce genuinely different
squads: A is +38 hp / +14 atk across six actors; B is +43 atk / −30 hp. Neither dominates, and the 5-AP
signet is what forces the choice — take it and both survivability charms are gone.

**Scale check.** Loadout A contributes +38 max hit points and +14 attack points per actor. Against a
fully geared twelve-slot actor those are single-digit percentages of the total. **That is the intended
size** (§7.4): a full pouch should read as a meaningful tilt, not a second set of equipment.

---

## 7. Failure modes

### 7.1 Charms as strictly-better free stats

**How it happens:** a bonus with no cost. Every charm is a straight gain, so the only decision is
"collect more", and power creeps with inventory rather than with play.

**What prevents it here:** four things, and they stack.

1. **The AP budget.** Capacity is 6–20; charms cost 1–5. You always leave something behind.
2. **Cross-run exclusivity.** A charm a live run holds cannot serve another. Running five expeditions in
   parallel means splitting one pouch five ways — the cost scales with how much you are doing, which is
   the only cost that keeps scaling.
3. **Drawbacks on signets.** The 5-AP class carries an authored negative (§6.1, charm 3). The biggest
   charms are trades.
4. **Flat only, one band below.** No `Increased`, no `More`, and a tier window one band under equipment
   at equal rarity. A charm cannot compound with the rest of a build.

**What is honestly still open:** the 1-AP and 2-AP rolled charms *are* small straight gains. That is
fine — they are the filler that makes the budget interesting — but if capacity ever outruns the charm
pool, the budget stops binding and this failure mode returns. The guard is that capacity growth is
progression-granted and slow (§8 item 11), not purchasable at will.

### 7.2 Inventory tetris as the actual gameplay

**How it happens:** charms live in the main bag and the cost is grid space (Diablo 2's grand-charm
inventory — recalled, **unverified**). Players spend real time on spatial packing that is not the game
they came for, and every new charm is a chore.

**What prevents it here:** the pouch is **not storage**. It is a marking on rows I13 already holds (§2),
the budget is a **scalar** (integer AP, not an area), and there is no grid — the web control room lists
items, it does not arrange them. Option (a) in §3.2 is rejected explicitly and on the record so nobody
re-proposes it as "the classic way".

The residual risk is the *other* tetris: an AP budget with too many small charms becomes a knapsack
puzzle you re-solve every run. The copy cap (2) and axis cap (3) keep the search space small — with 8 AP
and a cap of three per axis, the number of legal, meaningfully different loadouts is in the dozens, not
the thousands.

### 7.3 A bonus so diffuse the player cannot tell it is working

**How it happens:** a squad-wide +3% to something the player cannot see, spread over six actors, with no
moment where it announces itself. The mechanic is technically working and functionally invisible.

**What prevents it here:**

1. **Flat, never percentage.** `+18 max hit points` is a number on a bar. `+4% increased vitality` is a
   percentage of a base the player does not know.
2. **A magnitude floor.** A charm's per-actor magnitude must be at least the **tier-1 band** of that
   family. No `+2 hp` charms; a charm too small to read is not shippable content. This is checkable
   against the atom table at import, not a guideline.
3. **The pouch UI shows the multiplication.** `+18 max hp × 6 deployed = +108`. The breadth is the
   charm's whole selling point, so the breadth is what the screen says.
4. **Resonance announces itself.** A named breakpoint firing at 2 and 3 charms is a legible event, and it
   is the reason to look at the pouch at all.

### 7.4 Charms that make equipment irrelevant

**How it happens:** the carried layer is cheaper to acquire and easier to change than the worn one, so it
becomes the real build and equipment becomes a stat floor.

**What prevents it here:**

1. **Disjoint families.** Charms carry always-on flat side-wide effects; `jewel-minor` and the rest of the
   gear carry conditional per-actor riders. A family may not be on both (§3.6). They are not the same
   currency at different prices.
2. **One band below at equal rarity**, so a charm never wins a head-to-head on magnitude.
3. **An authoring budget:** *a full pouch at capacity should contribute no more than ~15% of a fully
   geared actor's stat total.* §6.3 lands well inside that. This is the number a **power model** would
   verify, and E9 is build position 15 with `power_json` nullable (SC9) — so it ships as an authoring
   rule and a review question, not as an enforced check. Stated so it can be tested later rather than
   assumed now.
4. **Charms have no sockets** (§8 item 9) and are not set pieces (§8 item 3), so they cannot borrow the
   depth those systems give equipment.

### 7.5 Two more this lane creates for itself

| Failure | Prevention |
|---|---|
| **Pouch churn** — re-optimising the pouch before every single fight, which is admin, not play | The run snapshot commits at start and locks the held charms. The dial is per-run, and a run is the unit |
| **The invisible nerf** — a charm quietly not applying because a parallel run holds it | Never silent: `CharmInUse` refuses the action, and the pouch UI names the run holding each locked charm. The partial unique index makes the rule structural, not procedural |

---

## 8. What this lane needs from other lanes

1. **I1 (rarity).** Register that charms read the rarity ladder. I need one answer: does
   `rarity → (pool_rolls, min_tier, max_tier)` resolve **differently** for a `charm` container than for
   an `item` one? My one-band-below rule (§3.4) needs either a charm column in I1's budget lookup or
   permission to keep a separate charm band table. I1 decides which; I do not invent rarity rows.
2. **I13 (bags and storage).** Charm instances are stored by I13 exactly like any other item. I need
   three guarantees: (a) a stable `instance_id` I can mark; (b) **a charm that is attuned or held by a
   live run cannot be salvaged, sold, or destroyed** — I13's sinks must consult `charm_pouch` and
   `charm_run_hold`; (c) a charm item category so the pouch UI can list candidates. I own the marking;
   I13 owns the row.
3. **I5 (sets).** Confirm the §3.5 boundary in your own document, and confirm the one thing that would
   break it: **no set may name a charm as a member piece.** A set spanning worn and carried items would
   make both lanes own the same combination.
4. **I2 (equip slots).** Confirm in your document that **a charm has no role and no equip slot**, so
   nothing later tries to give it one. And agree the family split in §3.6 — `jewel-minor` keeps the
   conditional per-actor riders, charms take the always-on flat families. That split is asserted here and
   needs I2 and I8 to hold it, or rings become worse charms.
5. **I12 (loot → instance).** Charms drop as instances through your pipeline. Two requirements: the drop
   table must be able to produce `charm.` containers, and **`ap_cost` must come from the base type, never
   from the roll** (§3.3). If I12 rolls it, this lane's entire budget mechanic collapses.
6. **I11 (equip gating).** Charms deliberately **bypass** the equip gate — no frame check, no role check,
   no attribute requirement. But `level_req` still applies at the pouch gate against the **player's**
   level, and OD7 says primary actor attributes do not exist yet. I need to know whether a *player* level
   exists at all, or whether I must gate on something else (commander level, account progression tier).
   Today `players` is `(id, name, created_utc)` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:85-89`) — there is
   nothing to compare against.
7. **I6 (instance mutation).** May a charm be enhanced or rerolled? I adopt whatever model you land (SC5,
   cut #6), with one constraint I must keep: **mutation may change magnitudes and never `ap_cost`,
   `axis`, or `unique_carry`.** Those are base-type properties; if enhancement can move them, the budget
   and the axis cap both stop being stable.
8. **I9 (cost vocabulary).** Attunement capacity growth from 6 to 20 has to be *bought* or *granted* in
   your terms. I do not own the currency and have not picked one. If growth is purchasable rather than
   progression-granted, tell me — it changes §7.1's guard.
9. **I4 (sockets).** Confirm **charms have no sockets.** A socketable charm would put one lane's
   combination inside another's, and the enrichment contract's cut (§4) does not survive that.
10. **The effect-atom program (E5 / E6 / E12).** Four asks, in dependency order:
    - **Add `charm` to the container-kind enum, the `PrefixOf` map, the id regex, and the store's
      string→enum map** — four sites, cited at §4.3. Append-only on the enum. This is the reviewed E5
      change SC3 anticipates.
    - **Amend `definitions.md` §1's `container_id` grammar row** to include `charm`. That document *wins
      over any spec*, so the regex change is not legal until the row moves. Ask, not edit.
    - **Decide `player:{id}`.** Today it parses
      (`src/FusionRpg.Core/Effects/Atoms/OwnerScope.cs:124-129`) and then degrades to match-wide in two
      places (`src/FusionRpg.Core/Stats/StatApplyScope.cs:81-82, 88-92`;
      `src/FusionRpg.Core/Effects/EffectProcAndOwner.cs:59-60`), and `match` matches **both sides**
      (`src/FusionRpg.Core/Stats/StatApplyScope.cs:52-53`). Either `player:` gains a real meaning — *the
      player's deployed actors* — or this lane has no correct scope and option C in §3.1 has to be
      re-argued. **This is the single largest external dependency in the lane.**
    - **E12's `BattleStatComposer` seam is the charm layer's first real consumer.** `stat.modify` is
      `Battle = None` today (`src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:88`). Until E12, charm
      content is authored-and-dead, and this document says do not author it (§2).
11. **Progression / standalone-rpg.** Attunement capacity has to hang somewhere. Two questions: where does
    the 6 → 20 growth come from, and **does it compete with expedition slots (2 → 5)?** Sharing one
    progression currency between "how many runs at once" and "how strong each run is" would be a genuinely
    good tension, and it is not mine to decide.
12. **I3 (base types).** `stub.hp_charm` (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs:25`) should
    become an I3 `jewel-minor` base type, not an I10 charm, so the name stops meaning two things.

---

## 9. Open questions for the owner

1. **Is cross-run exclusivity too harsh?** Splitting one pouch across five parallel expeditions is the
   strongest cost in this design and the most likely to feel bad. The softer version is a pouch that is
   simply *copied* into every run, with the AP budget as the only limit. That is friendlier and removes
   the cost that scales.
2. **Capacity 6 → 20 and AP {1, 2, 3, 5}** — the shape, not the balance. These set how many charms a
   player carries at each stage of the game and are cheap to change now, expensive later.
3. **Does the commander, once it exists, change anything here?** My answer is no: a commander's `standard`
   slot (item-ideal §5.6) is a worn item in a role, which is I2's; charms stay account-level and carried.
   Confirm, because the alternative — charms as commander-only gear — is answer A in §3.1 and a different
   mechanic.
4. **Are charms tradeable or account-shared** if multiple commanders ever exist? The design assumes one
   pouch per `player_id`.
5. **Should lawn-only charms exist at all?** Sunwarden Bead (§6.1) works only with the game open. SC8
   permits it — the injector may enrich, never gate — but a charm that is inert in standalone play is
   half-dead content, and every one of them is a row somebody has to explain in a tooltip.
6. **Five new reason codes against a closed 33** (§5.2). The fold-to-three variant is written out; picking
   it costs the UI's ability to point at the offending charm for an axis overflow.
7. **Does resonance scale with deployed count?** I say flat — a resonance tier grants the same per-actor
   amount whether you deployed two actors or six. Scaling it would reward wide squads twice, since the
   charms themselves already do.
8. **Should the axis cap be 3, or should axes be dropped for a freer packing puzzle?** The cap buys
   legibility and build variety; dropping it buys freedom and risks six-of-one-axis convergence.

---

## 10. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — effect-atom (container / instance / binding /
    owner scope), overlay stat scoping, unique-actor lifecycle, expedition dispatch, and item
    lanes I1/I2/I3/I4/I5/I6/I9/I11/I12/I13.
[x] I read the required reading in the order the contract names, this session:
    enrichment-contract.md, item-ideal.md, definitions.md (§0–§6, §9, §10, §13-D6),
    spec-instance-and-binding.md, spec-container-schema.md, atom-family-library.md.
[x] I checked the contract's §6 owner decisions and decisions.md for a lock covering this —
    OD5 (combination bonuses first-class) and OD6 (every bonus is a container of atoms) bind
    this lane and are obeyed; SC3 reserves `charm`; nothing forbids the design.
[x] Every factual claim about the repo cites file:line or a doc.
[x] I verified claims against CODE, not comments — OwnerScope, StatApplyScope, EffectProcAndOwner,
    AtomKindRegistry, ContainerRow, ContainerValidator, BindGate, UniqueEquipmentCatalog and the
    RpgStore DDL were all opened. The `player:` match-wide stub and the `Battle = None` matrix are
    read from code, and they are the two findings that shaped the lane.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no test suite was run for this
    document.** The runtime-support matrix, the `player:` scoping, and the container-kind regex are
    read from shipped source, not executed. Run the suite before any of them justifies a build
    decision.
[x] Nothing contradicts a §2 invariant — SC1 (all bonuses are atom containers), SC2 (no new kinds,
    attach points or triggers), SC4 (units on every number), SC5 (determinism via instance seeds,
    unchanged), SC6 (reject, never ignore), SC7 (every table names its consumer), SC8
    (standalone-first; the one lawn-only charm is enrichment and is flagged), SC9 (the ~15% budget
    is stated as a want, and the design ships without a power number).
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no item capability map, plan, or task list exists yet** — the item program has not
    graduated past the ideal, and this lane writes exactly one file per the contract's §7.
```
