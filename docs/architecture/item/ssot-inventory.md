# Lane I13 — inventory, storage, and item lifecycle

**Lane I13 SSOT, drafted 2026-08-22.** Enriches [item-ideal.md](../item-ideal.md); bound by
[enrichment-contract.md](enrichment-contract.md).

---

## 1. Scope

### This lane owns

- Where an item lives when it is not being worn: the armoury, the stock counters, the material counters.
- **Stacking** — which things collapse into a counter and which need a row, and the law that decides.
- **Assignment** — the durable record of what a specimen is wearing, and how it is changed atomically.
- **Loadouts** — named presets, applying them, and what happens when two of them want the same item.
- **Salvage, destroy, lock, and bulk actions**, including the guards that stop a bulk action eating
  something precious, and the undo window.
- **Comparison data** — what the FE needs to answer *"is this better than what I have on?"* across ~15
  roles and a roster, before a power number exists.
- **Filters and sorting**, and whether a drop-time loot filter is required.
- **The whole item lifecycle**: drop → armoury → assigned → released → salvaged → purged, plus every
  stale state along the way.
- The **volume this design can absorb**, stated as a budget so I12 can tune against it.

### This lane does NOT own

| Thing | Lane |
|---|---|
| The category taxonomy (what an "equipment" or "consumable" *is*) | **I3** — I13 stores what they define |
| Material taxonomy and the salvage-yield vocabulary | **I9** |
| Drop volume and drop tables | **I12** — but §3.8 states what volume this absorbs |
| Equip slots, roles, and how many there are | **I2** — I13 stores role ids and reports the answer they need |
| Charm carrying limits | **I10** — but the pouch is one owner scope inside this storage model |
| Rarity ladder and ordinals | **I1** — I13 sorts and filters by the ordinal |
| Post-drop mutation (enhance, reroll, socket) | **I6** / **I4** — I13 only defines what mutation does to *stacking* |
| The equip gate (who may wear this) | **I11** — I13 names the refusal sites |

---

## 2. The model

### 2.1 The problem that defines this lane, and its answer

OD2 gives every pure frame **~15 equip slots**. The roster is not small: contract binding slots start at
**12 and buy up to a hard maximum of 48** (`docs/architecture/demons/spec-demon-contracts.md:54`). At the
ceiling that is **48 × 15 = 720 equipped items** before one spare sits anywhere. Twenty specimens is 300.

There are only two honest ways out, and both of the obvious ones are wrong:

- Make gear scarce, and 42 of your 48 demons stand bare. The roster becomes decorative — you own 48 and
  play 5. That kills the thing the owner actually asked for, which is *commanding a roster*.
- Make gear plentiful, and you are hand-placing 720 rolled items across 48 dossiers in a browser. That is
  a spreadsheet with a fantasy skin, and it is the single most reliable way to make 15 slots a chore.

**The answer: split equipment into two storage grades, and make the high-volume grade a counter.**

> **An item that rolls nothing is fungible, and a fungible thing is a number, not a row.**

| Grade | What it is | Stored as | Volume |
|---|---|---|---|
| **Stock** ("standard issue") | Normal-rarity base types: base stats + implicit, `pool_rolls = 0`, no rolled affix | **a counter** — `(player_id, container_id) → qty` | unbounded; hundreds or thousands is free |
| **Rolled** | Magic / Rare / Unique / Set — anything carrying a rolled affix, a socket, or an enhancement | **one row per instance** | tens to low hundreds; every row is a real decision |

Filling a specimen's 15 roles with stock is **one action and fifteen counter decrements**. Nobody is
bare, the 21st specimen costs almost nothing to outfit, and yet gear still matters — because a stock kit
is deliberately worse than a rolled one, roughly the way a common set is worse than a rare in any ARPG.
Rolled gear is what you *think* about, and there are few enough of those that thinking is possible.

**This is the answer lane I2 asked for: 15 slots is rich, not a chore** — because slot-filling is bulk by
default and deliberate only where it pays. Fifteen roles across a roster of 48 is 720 *cells*, but it is
never 720 *decisions*: it is 48 "issue kit" clicks plus however many rolled items the player actually
owns.

The law behind the split is already in the contract, not invented here. SC5:

> same `(container_id, catalog_revision, roll_seed)` ⇒ byte-identical instance.

A container with `pool_rolls = 0` and only `Fixed` value specs does not read `roll_seed` at all. So every
instance of it, at one catalog revision, is **indistinguishable by construction**. Two indistinguishable
things do not need two rows.

### 2.2 Storage grade is derived, never authored

`stock_eligible` is computed from the container and **validated against it**, the same way `atom_id` is
derived and validated in [definitions.md](../effect-atom/definitions.md) §1:

```text
stock_eligible(container) =
      pool_rolls == 0
  AND every fixed-core atom's effective value spec (after overrides_json) is Fixed
  AND the container declares no sockets                       (I4)
```

An author cannot tick a "stackable" box and be wrong. If they add a rolled affix to a stock base type, it
stops being stock, and the importer says so.

**Promotion is one-way.** The moment a stock item is socketed, enhanced, or rerolled (I4 / I6), it stops
being fungible: the counter decrements by one and a real `rpg_item` row is created carrying the mutation
log. It never re-stacks, even if the mutation is later reverted — because SC5's reproducibility now
depends on an operation list that other copies do not have.

### 2.3 The armoury, not bags

There is **one player-scoped armoury**. There is no per-specimen bag, no bank, no stash tab, no
warehouse. A specimen does not "hold" items; an **assignment** points from a `(specimen, role)` cell at an
item in the armoury.

This matters more than it sounds. Per-specimen bags force a move operation between two containers, and
move operations are where inventory games grow tetris. With one armoury, "swap this helm onto that demon"
is a single row update, and "which of my 48 demons could use this?" is one query rather than 48.

### 2.4 Equipped is an assignment; a binding is its runtime shadow

[item-ideal.md](../item-ideal.md) §6.4 says *"Equipping = create an `effect_binding`."* That is half
right and the missing half is load-bearing.

[definitions.md](../effect-atom/definitions.md) §6 is explicit: **`entity:` bindings are session-scoped
and never durable** — a pointer can be recycled, and a durable row aimed at a recycled address silently
retargets. The 7 owner scopes are `match` · `plant:N` · `zombie:N` · `entity:` · `player:` · `sector:` ·
`slot:`. **None of them is a durable specimen.** A demon is a `rpg_unique_actors.instance_id` GUID; the
shipped code already knows this and works around it — `UniqueOwnerBinder` translates `instance:{guid}` →
`entity:{ptr}` at bind time, and unique-actor-runtime §3 states the rule outright: *"Durable `instance:`
must not appear in hot Resolve."*

So there are **two acts, not one**:

| Act | Durable? | Owner | Lives in |
|---|---|---|---|
| **Assign** — this demon wears this item | yes, forever | I13 | `rpg_item_assignment` |
| **Bind** — this item's atoms are on this actor's effect list right now | no, session-scoped | E6 | `effect_binding` |

The assignment is the **only** durable writer of equip state. Bindings are **derived**: at deploy (or at
squad build for a battle / expedition) the runtime projects the assignment set into `effect_binding` rows
at `entity:{ptr}`, and withdraws them at recover. Nothing patches a binding incrementally; the projection
is always a full rebuild from assignments. That is what makes unequipping atomic (§5.4) and it is the same
shape as the loadout → Intent → grants pipeline already shipped in W5.

This retires the stub. `rpg_unique_equipment(instance_id, slot, item_id)` — three columns, slot allowlist
`weapon | armor | trinket` (`src/FusionRpg.Data/Sqlite/RpgStore.cs:356`, mirrored in the FE at
`web/fusion-rpg-web/src/features/roster/RosterPage.tsx:33`) — is superseded by `rpg_item_assignment`.

### 2.5 No bag limit, and the pressure that replaces it

**Unlimited rows. No capacity, no expansion currency, no tabs.**

Limited inventory is a real design tool in exactly two situations: when it forces a decision *in the
field* under time pressure (Resident Evil, Tarkov, Diablo 1's dungeon trips — recalled, unverified), or
when the expansion is a monetization hook. Neither applies here. There is no in-run inventory at all —
gear is set between runs and expeditions auto-resolve — and this project has no monetization, the same
argument [standalone-rpg-map.md](../standalone-rpg-map.md) already used to kill a stamina gate: *"with no
monetization a stamina gate has no honest job."* A bag cap here would be pure friction in a browser tab.

The honest cost of that choice is that an unlimited stash becomes a museum nobody sorts. The counter is
not capacity — capacity does not make people sort, it makes them delete at random. The counters are:

1. **The high-volume grade never becomes rows at all** (stock and materials are counters).
2. **An inbox.** Every rolled item arrives `seen = 0`. The armoury header shows an unreviewed count, and
   an inbox can be emptied; a stash cannot.
3. **The gap board** (§3.6) — the screen tells you *where acting would help*, so the stash has a purpose
   other than existing.
4. **Auto-salvage rules** so junk is credited as materials at the drop boundary and never becomes a row.
5. **A structural ceiling, not a game rule** — creating a rolled row beyond 20 000 per player rejects
   `InventoryCeiling`. That is an abuse and runaway-drop guard, and it needs a reason code anyway.

---

## 3. Options considered, and the recommendation

Six decisions, each with the alternatives that were actually plausible.

### 3.1 The roster-scale gear economy

| Option | Tradeoff |
|---|---|
| **A — Per-actor rolled gear, plentiful** | The ARPG default. 720 rolled rows to place by hand. Inventory management becomes the game; the browser FE becomes a spreadsheet. |
| **B — Account-wide stat pools** | Equip once, everyone benefits (the gacha "resonance"/collection-power line — recalled, unverified). Kills the per-specimen fantasy outright and makes I2's 15 slots decorative. |
| **C — Small deployable squad; only 5 actors ever need gear** | Honest and cheap, but it means 43 of 48 contract slots exist to be looked at. Buying a slot for 300 × k Souls to own a demon that can never be equipped is a bad purchase. |
| **D — Two storage grades: stock counters + rolled rows** | Everyone is kitted; only the interesting half needs attention. Costs a derived discriminator and a promotion rule. |

**Recommendation: D.** It is the only one that keeps all three of "everybody is geared", "15 slots is
rich", and "nobody manages 720 rows". C survives as a *balance* statement rather than a storage one —
squads are 2–5 (`docs/architecture/standalone/spec-expeditions.md:15`), so a rolled item can only ever
contribute to one squad at a time regardless.

### 3.2 Bag capacity

| Option | Tradeoff |
|---|---|
| Limited with paid expansion | Standard, and standard because it sells tabs. Nothing to sell here. |
| Limited and that is the point | Works when the limit forces a *field* decision. There is no field; there is a browser. |
| **Unlimited** | Removes the friction entirely, and pushes the museum problem onto attention design. |

**Recommendation: unlimited**, with the five pressures in §2.5 and a structural ceiling that is a bug
guard, not a rule.

### 3.3 Binding — bind-on-pickup / bind-on-equip

| Option | Tradeoff |
|---|---|
| **BoP** | Exists to protect a trade economy. [item-ideal.md](../item-ideal.md) §10 puts trading out of scope. Protecting an economy that does not exist. |
| **BoE** | Makes swapping costly, which is precisely the tax the roster fantasy cannot pay — the whole point of one armoury is that gear moves. |
| **Neither** | Gear moves freely between specimens, from `Roster` phase, at zero cost. |

**Recommendation: neither.** The one thing binding usually also buys — stopping one god item from
serving the whole roster by rotation — is already bought: an item is assigned to at most one cell, and
expeditions run concurrently, so rotation cannot double its contribution.

A future `no_reassign` content flag for quest-reward uniques is **reserved and deliberately not added**:
SC7 forbids a column with no consumer, and there is no quest system in the tree.

### 3.4 Comparison, with no power number

SC9 forbids depending on E9's power model, and `power_json` is nullable.

| Option | Tradeoff |
|---|---|
| **Invent a score** — weighted sum of magnitudes | Reinvents power, badly. SC4 says `+10 hp` and `+10 fire power` differ by an order of magnitude; a naive sum would be wrong *and* look authoritative, which is worse than no number. |
| **Show raw stats, let the player work it out** | Truthful. Useless across 15 roles and 48 specimens. |
| **Delta + dominance + roll quality** | Three unitless-or-honest signals that need nothing E9 has not shipped. |

**Recommendation: the third.** Detail in §3.6.

### 3.5 Loadouts

| Option | Tradeoff |
|---|---|
| Per-specimen saved sets | Matches the mental model, but 48 specimens × N sets is a lot of rows for a feature used on maybe five of them. |
| **Player-level named loadouts with an optional frame constraint** | One library; apply to whichever specimen is going out. Conflicts become explicit. |
| No loadouts at all | Fine for v1, but re-kitting a rotated squad is 15 clicks per specimen. |

**Recommendation: player-level library.** Apply **refuses by default** when an entry's item is held
elsewhere (`LoadoutConflict`, listing exactly which cells hold what); `force = true` steals and **reports
what it stripped**. Never silently strip — the "why is my other demon naked" bug is one silent strip away.

### 3.6 Salvage safety

| Option | Tradeoff |
|---|---|
| Lock flag only | The industry default. Fails on exactly the item the player has not looked at yet — which is always the one they lose. |
| Lock + confirmation dialog | Better. Still asks the player to spot one line in a list of 300. |
| **Standing guards + preview + undo window** | Costs a soft-delete tombstone and a purge job. Removes the failure rather than warning about it. |

**Recommendation: the third**, in full — §5.7 and §6.

---

## 4. Data shape

All of this is SQLite inside `FusionRpg.Data`. **No SQL leaves that project** — `scripts/guard-dal.ps1`
scans all of `src/` outside Data with an empty allowlist, and it runs in CI, `FusionRpg.Guard.Tests`, and
`deploy-play.ps1` ([data-architecture.md](../data-architecture.md) §6). Every store below lands in a new
partial `RpgStore.Items.cs`, matching the existing domain partitioning.

### 4.1 Store list

| Store | Shape | Holds | New? |
|---|---|---|---|
| `rpg_item` | one row per rolled instance | rolled / unique / socketed / enhanced equipment and charms | **new** |
| `rpg_item_stock` | counter | unrolled equipment, unrolled consumables, uncut inserts | **new** |
| `rpg_demon_materials` | counter | materials, essences, shards | **exists**, `RpgStore.cs:520` — reuse, widen the id vocabulary (I9) |
| `rpg_soul_balances` / `rpg_soul_ledger` | ledger | currency | **exists**, `RpgStore.cs:455` — reuse, never inventory |
| `rpg_item_assignment` | one row per filled cell | what is worn / carried | **new** — replaces `rpg_unique_equipment` |
| `rpg_item_loadout` / `rpg_item_loadout_entry` | preset | named gear sets | **new** |
| `rpg_item_rule` | per-player filter | auto-salvage and hide rules | **new** |
| `rpg_item_event` | append-only log | acquisition, assignment, salvage, undo, stale flags | **new** |

**Deliberately not created:** a quest/key-item table. There is no quest system in the tree, and SC7 is
blunt — *a row no code consumes is not content; it is a lie in a table.* When a quest lane exists it gets
`rpg_item_key(player_id, key_id, qty)`, a set that never appears in a gear list and is never salvageable.
Reserved, not built.

### 4.2 `rpg_item` — the thin row above the instance

Answers [item-ideal.md](../item-ideal.md) §6.3. **PK is `instance_id`**, one-to-one with
`effect_instance` — no second identity, and no rolls duplicated.

| Column | Type | Notes |
|---|---|---|
| `instance_id` | TEXT PK | FK → `effect_instance.instance_id`, 32 lowercase hex (definitions §1) |
| `player_id` | INT NOT NULL | ownership; every item-lane query is player-scoped |
| `acquired_utc` | TEXT NOT NULL | sort default |
| `origin_kind` / `origin_ref` | TEXT | `expedition` \| `battle` \| `craft` \| `grant` \| `migration` + the id. `effect_instance.origin` already carries the coarse word; this carries *which one* |
| `locked` | INT NOT NULL DEFAULT 0 | favourite / protect |
| `seen` | INT NOT NULL DEFAULT 0 | 0 = in the inbox |
| `stale` | INT NOT NULL DEFAULT 0 | set by the importer when an atom beneath it is disabled (§5.6) |
| `disposition` | TEXT NOT NULL DEFAULT `'held'` | `held` \| `salvaged` \| `destroyed` — soft-delete tombstone |
| `disposed_utc` | TEXT | when the tombstone was written; drives the undo window |
| `note` | TEXT | player note — cheap, and it is the "why did I keep this" memory |
| `revision` | INT NOT NULL DEFAULT 0 | monotonic, same convention as every other durable row |

Indices: `(player_id, disposition, acquired_utc)`, `(player_id, disposition, locked)`,
`(player_id, seen) WHERE seen = 0`.

**Why not put these on `effect_instance`?** Because `player_id`, `locked`, and `seen` are item *policy*,
and `effect_instance` belongs to the atom program, whose contract is content-derived reproducibility.
Ownership is not content. Adding player columns there would put I13's policy inside E6's table and break
the byte-identity comparison's meaning.

### 4.3 `rpg_item_stock` — the counter

| Column | Type | Notes |
|---|---|---|
| `player_id` | INT | PK part |
| `container_id` | TEXT | PK part — FK → `effect_container.container_id`, must be `stock_eligible` |
| `qty` | INT NOT NULL DEFAULT 0 | `≥ 0`, enforced |
| `updated_utc` | TEXT NOT NULL | |

PK `(player_id, container_id)`. **`catalog_revision` is deliberately not in the key.** Stock is fungible
*because* it is standard issue — two revisions of "iron plate helm" are still iron plate helms. What
changes across a revision is the **canonical stock instance**: exactly one `effect_instance` per
`(container_id, catalog_revision)`, `origin = 'stock'`, that every stock assignment binds through. The
importer refreshes those in the same transaction that bumps `catalog_revision` — see the **stock refresh**
step in §5.6. Many bindings may point at one instance; nothing in E6 forbids it, and it keeps 720 stock
cells from becoming 720 instance rows.

### 4.4 `rpg_item_assignment` — the durable equip record

| Column | Type | Notes |
|---|---|---|
| `player_id` | INT NOT NULL | |
| `owner_kind` | TEXT NOT NULL | `specimen` \| `player` — reuses the atom program's grammar shape, **not** its scope enum |
| `owner_key` | TEXT NOT NULL | specimen GUID, or `''` for `player` |
| `role` | TEXT NOT NULL | I2's role id (`head-protective`, `armament-primary`, …) or a pouch role (`charm-1`…) |
| `ref_kind` | TEXT NOT NULL | `rolled` \| `stock` |
| `ref_id` | TEXT NOT NULL | `instance_id` when rolled, `container_id` when stock |
| `assigned_utc` | TEXT NOT NULL | |
| `revision` | INT NOT NULL DEFAULT 0 | |

PK `(player_id, owner_kind, owner_key, role)` — **one item per cell, by the primary key**, so double-equip
is impossible rather than merely checked. Partial unique index on `ref_id WHERE ref_kind = 'rolled'` — a
rolled item is in at most one cell, also by constraint. Stock has no such index: five demons may all wear
`item.iron-plate-helm`, and the counter is what limits it.

`owner_kind = 'player'` is how I10's **charm pouch** lives here: the pouch is a set of assignment rows
whose owner is the player rather than a specimen, with roles `charm-1 … charm-N` where N is I10's cap.
The refusal site is mine (`CharmPouchFull`); the number is theirs.

### 4.5 Loadouts

```text
rpg_item_loadout(loadout_id PK, player_id, name, frame?, created_utc, revision)
rpg_item_loadout_entry(loadout_id, role, ref_kind, ref_id)     PK (loadout_id, role)
```

Entries are **validated on read**, never silently dropped: an entry whose item has been salvaged returns
with a `missing` marker so the player sees the hole instead of a quietly shorter loadout.

### 4.6 `rpg_item_rule` — auto-salvage and hide

```text
rpg_item_rule(player_id, rule_id, seq, action, predicate_json, enabled, revision)
```

`action ∈ auto-salvage | hide`. `predicate_json` reuses the **canonical predicate encoding** from
definitions §3 — internal nodes carry `op` + `children`, leaves carry `leaf` + `subject` + `value`, depth
≤ 4, ≤ 16 nodes, and the same rejection codes. Inventing a second filter grammar for the same job would
be exactly the second-mechanism defect SC1 exists to catch. Leaves this lane needs: `rarityBelow`,
`roleIs`, `frameIs`, `hasFamily`, `rollQualityBelowMilli`, `isStale`. Subject is always `item`.

### 4.7 `rpg_item_event` — the log that makes salvage answerable

```text
rpg_item_event(id PK AUTOINCREMENT, player_id, t, kind, ref_kind, ref_id, detail_json)
```

`kind ∈ acquired | assigned | released | locked | unlocked | salvaged | undone | purged | promoted |
stale-flagged | stock-converted`. This is what makes *"where did my item go"* answerable, and it is the
only place the auto-salvage path leaves a trace. Consumer: the armoury history panel and the salvage undo
path — named, per SC7.

### 4.8 What maps onto the existing atom schema

| Reused unchanged | How |
|---|---|
| `effect_container` | item templates; `pool_rolls`, `min_tier`/`max_tier`, `rarity`, `level_req`, `slot` all already exist |
| `effect_container_pool` | the affix pool; `group` defaulting to `(family_id, variant)` |
| `effect_instance` / `effect_instance_atom` | a rolled item **is** the instance; `rpg_item` hangs off it |
| `effect_binding` | the **runtime shadow** of an assignment, built at deploy, withdrawn at recover |
| `rarity` table + ordinals | sort, salvage-by-rarity, the auto-salvage floor — all key on the **ordinal**, never the label |
| `rpg_demon_materials` | salvage yield lands here; no second material store |
| `rpg_soul_*` | currency stays a ledger |

| Genuinely new | Why it could not be reused |
|---|---|
| `stock_eligible` (derived column on `effect_container`) | nothing today distinguishes a fungible template from a rolling one |
| the eight `rpg_item_*` stores | ownership, lock, seen, disposition, presets, filters, and history are player policy, not content |

---

## 5. How each operation works

### 5.1 Acquire

I12 turns a loot event into an instance. I13 then:

1. If the container is `stock_eligible` → `INSERT … ON CONFLICT DO UPDATE qty = qty + n`. **No instance
   is created and no row enters `rpg_item`.**
2. Otherwise → create the `effect_instance` (E6), then one `rpg_item` row, `seen = 0`.
3. Evaluate the player's `auto-salvage` rules in `seq` order. First match wins; the item is salvaged
   immediately at full yield and logged. **Auto-salvage never fires on a locked item, never on an item
   above the player's rarity floor, and never on one that would be best-in-role (§5.8).**

All three paths are one transaction with the drop's own write, so a crash cannot credit a reward twice.

### 5.2 Issue a standard kit

One request: `POST /api/items/issue-kit { specimenId }`. The server resolves the specimen's frame and
role list (I2), picks the highest-`level_req`-satisfying stock container per role that the player holds,
decrements each counter, and writes the assignment rows — **one transaction, all or nothing**. A role with
no stock in hand is skipped and reported; the call never half-fills silently.

### 5.3 Assign / swap

`PUT /api/items/assignment { owner, role, ref }`. Refusals in §6. A swap into an occupied cell requires
`replace: true` and returns the displaced item's id; without it, `RoleOccupied`.

### 5.4 Unassign, and why it is atomic

Unassign is `DELETE` of one assignment row plus one `rpg_item_event` row, in one transaction. That is the
whole operation, because **there is no second place that records equip state**:

- The item row has no "equipped" column to fall out of sync. Asking whether an item is equipped is a
  lookup by `ref_id` in the assignment table.
- Bindings are never patched. The runtime projection is rebuilt in full from the assignment set at deploy
  and torn down at recover, so a half-applied unequip cannot exist — there is no delta to lose.
- Stock unassign returns the counter (`qty + 1`) in the same transaction.

Assignment changes are refused unless the specimen is in `Roster` phase — `SpecimenNotIdle`. That matches
the shipped rule (unique-actor-runtime §11: *"Mid-run ActiveBound equip held"*) and it is why the full
rebuild is affordable: nothing rebuilds mid-match.

### 5.5 The comparison payload

For one candidate against one incumbent, the server returns three things and **no invented scalar**:

| Signal | Shape | Why it is honest |
|---|---|---|
| **Per-channel delta** | `[{ channel, unit, incumbent, candidate, delta }]` where `unit ∈ game-units \| resolver-points \| per-mille \| ms` | SC4 says magnitudes across channel families are not comparable. So do not compare them — show them, labelled. |
| **Dominance verdict** | `strictly-better` \| `strictly-worse` \| `sidegrade` \| `incomparable` | A partial order is mathematically defensible and answers the question outright in the cases where an answer exists. `incomparable` = the two touch disjoint channels. |
| **Roll quality** | integer ‰ per atom, plus the mean | Where the rolled value sits inside the atom's own authored `[Min, Max]` **after** curve scaling (definitions §2: the curve scales before the roll). Unit-free, needs nothing from E9. |

When I9/E9 lands, `power_json` becomes a **fourth column**, not a replacement. The delta table stays,
because a single number cannot say *what* got better.

### 5.6 Stale state — extending definitions §6

[definitions.md](../effect-atom/definitions.md) §6's stale-owner table covers instances and bindings.
Items add owners, presets, and counters. This is the extension:

| Case | Behaviour |
|---|---|
| **Specimen dies in a match** | Nothing. Death is `ActiveBound → Recovering → Roster`, not removal. Assignments survive untouched. *Gear is never lost on death* — stated because "lost on death" is a design others might assume. |
| **Specimen retired** | Retire **releases every assignment to the armoury in the same transaction**, writes one `released` event per cell, and reports the count. Gear must never be soft-deleted along with a tombstone; that is how a museum starts. |
| **Specimen consumed by fusion** | Identical to retire, and it is the fusion path's job to call the release helper. Backstop: assignments carry `ON DELETE CASCADE` on the specimen, so a hard delete can never orphan a cell — but cascade alone is not enough, because a cascade leaves no record of where the gear went. The helper writes the events. |
| **Atom disabled beneath a held item** | The item keeps its frozen values (definitions §6). I13 sets `stale = 1`, shows it flagged, **excludes it from the gap board and from best-in-role protection** (it cannot be equipped anyway), and keeps it **salvageable at full yield** — never punish a player for a content change, and never delete it for them. |
| **Atom disabled beneath an *assigned* item** | The assignment survives. The **next deploy builds bindings best-effort**: 14 of 15 succeed, the stale one is skipped, and the deploy response names the role and the reason. This is a deliberate, narrow deviation from SC6's "reject, never ignore" — refusing an entire deploy over a patch-disabled trinket is the worse failure. The rejection is **surfaced**, not swallowed: the item stays flagged and the deploy result lists it. |
| **Catalog revision bumps with items in the armoury** | Rolled items are frozen and unaffected. **Stock refresh** runs in the import transaction: for every stock container, materialise the canonical instance at the new revision and re-point stock bindings at it. Bounded — one instance per stock container, not per stock item. |
| **A stock container is disabled** | Its counter is **converted to materials at the standard salvage yield** during stock refresh and logged as `stock-converted`. A counter pointing at a disabled container is a lie in a table (SC7). |
| **A role id is retired by I2** | Role ids are **append-only and never renamed** — an ask on I2 (§9). If a role must go, its assignments are released to the armoury at import, one event each. |
| **Loadout entry whose item was salvaged** | Returned with a `missing` marker. Never silently dropped. |
| **Save slot reset** | Every `rpg_item_*` table joins `RpgStore.Reset()`'s delete list (`RpgStore.cs:606`ff). |
| **Archive / compaction** | Item rows are **not** capture data and are never archived or trimmed. The hot→cold lifecycle in [data-architecture.md](../data-architecture.md) §5 does not touch them, and `#/storage` purge must never reach them. |

### 5.7 Salvage — the four guards, the preview, and the window

**Standing guards.** These make bulk salvage safe by construction, not by warning:

| # | Guard | Rationale |
|---|---|---|
| **G-A** | An **assigned** item is never salvageable. Unassign first. | The single most effective mitigation any ARPG shipped: you cannot destroy what you are wearing. |
| **G-B** | A **locked** item is never salvageable, through any path including auto-salvage. | Lock must be absolute or it is decoration. |
| **G-C** | **Loadout membership implies lock.** | A preset that quietly loses a piece is worse than a refused salvage. |
| **G-D** | **Best-in-role** items are excluded from bulk selections by default, and *listed* as excluded. | This is the one that prevents the actual disaster. Players do not lock what they have not looked at, and the item they lose is always the one they have not looked at. |

**Preview then commit.** `POST /api/items/salvage/preview` returns the exact id list, the yield, and a
guard report: how many matched, and how many were excluded by each of G-A…G-D **with the excluded items
named**. Commit takes the preview's id list, so a race that adds an item between the two calls cannot
widen the selection.

**Undo window.** Commit sets `disposition = 'salvaged'` and credits materials immediately — escrowing the
yield would defeat the point of bulk salvage. Rows stay for **24 hours or 200 subsequent salvage
operations, whichever comes first**. Undo restores the rows and debits the yield; if the player has
already spent below what the debit needs, undo refuses `SalvageUndoInsufficientMaterials`, and the preview
says so up front: *"undo is available while you still hold the yield."* A purge job hard-deletes
tombstones past the window; hard delete cascades the `effect_instance` (definitions §6).

**Unseen items** (`seen = 0`) are excluded from manual bulk salvage unless the player explicitly ticks
*include new*. You cannot destroy in bulk what you have never looked at. Auto-salvage-on-drop is a
separate, opt-in path with its own rules and its own log lines.

### 5.8 Best-in-role ranking — a protection heuristic, and labelled as one

G-D needs a total order, and dominance only gives a partial one. So for **protection only**:

```text
rank = (count of channels where this item is the player's maximum for that role,
        then rarity ordinal,
        then mean roll quality in ‰)
```

This is crude on purpose and it is not a balance number. It is tuned to be **over-protective** — the
correct failure direction is refusing to salvage something worthless, never destroying something good.
When I9's power model lands, this ranking gets replaced by it; nothing else depends on the heuristic.

### 5.9 Filters, sorting, and the gap board

**Sort keys:** acquired (default, newest first) · rarity ordinal · role · mean roll quality ‰ ·
assigned-to · locked · unseen.

**Filters:** role · frame · rarity range · assigned / unassigned · locked · unseen · stale · *fits
specimen X* · *improves any specimen* · affix family present · socket count (I4) · set (I5).

**The gap board** is the screen that makes 15 roles across 48 specimens tractable. For each
`(specimen, role)` cell it reports one of: `locked` (role not unlocked yet — I2), `empty`, `stock`, or
`rolled`, plus whether an **unassigned strict improvement exists in the armoury**. It defaults to showing
*only* cells with an available strict improvement, which is a short list, and it collapses "issue stock"
into one action per specimen.

Cost: 48 × 15 = 720 cells, each comparing ≤ 8 atoms against a candidate set. Computed server-side,
memoised per `(player_id, armoury revision, catalog_revision)`, invalidated on any assignment or
acquisition. No precomputed table — this is a query, not a store.

**Do loot filters need to exist?** That depends on I12, and here is the budget to tune against (§3.8).

### 5.10 Volume this design absorbs

Design budgets, not measurements. Stated so I12 has a target.

| Stream | Absorbed | Binding constraint |
|---|---|---|
| **Stock and material drops** | effectively unbounded | counters; a thousand helms is one row |
| **Rolled rows in storage** | ~2 000 per player before the FE needs list virtualisation | rendering, not SQLite |
| **Rolled rows reviewed** | **~60 per session** before players stop reading them | human attention — this is the real ceiling |
| **Rolled drop rate** | ≤ ~30 per hour of active play | above that, a drop-time filter stops being optional |

**Conclusion for I12: a drop-time loot filter is required if rolled drops exceed roughly one per two
minutes of active play.** Below that, sorting plus the gap board plus the inbox is enough. Above it, route
the surplus into stock and materials rather than into rows — which is the whole point of the two-grade
split.

---

## 6. Validation and reason codes

### Reused from the closed 33 (definitions §10)

| Bad input | Code |
|---|---|
| Assign an item whose atom was disabled | `StaleInstance` |
| Assign below the container's `level_req` | `LevelTooLow` |
| Malformed owner key on the runtime projection | `BadOwnerKey` |
| Bind a stock/rolled item whose kind the runtime cannot execute | `RuntimeUnsupported` |
| `stat.modify` on `defense` projected anywhere but `match` (G8) | `ScopeUnsupported` |
| Reference a container that does not exist | `UnknownContainer` |
| Author a stock counter for a container that rolls | `BadParamValue` |
| Two assignments into one cell | `DuplicateKey` |

### Proposed new codes

Twelve, which is a large ask against a closed list of 33. Each is a **distinct remedy** for the player —
that is the bar I held them to, and three that failed it were merged away.

| Bad input | Code | Remedy it names |
|---|---|---|
| Act on an item another player owns | `ItemNotOwned` | wrong save slot |
| Salvage or destroy a locked item | `ItemLocked` | unlock it first |
| Salvage or destroy an assigned item (G-A) | `ItemAssigned` | unassign it first |
| Assign into a filled cell without `replace` | `RoleOccupied` | pass `replace`, or pick another cell |
| Assign to a role this frame does not have (I2) | `RoleNotOnFrame` | wrong specimen |
| Base type's frame ≠ specimen's frame (I3 / I11) | `FrameMismatch` | wrong item |
| Change assignments while phase ≠ `Roster` | `SpecimenNotIdle` | wait for recover |
| Change assignments on a `Retired` specimen | `SpecimenRetired` | terminal — nothing to wait for |
| Issue more stock than the counter holds | `StockDepleted` | craft or buy more |
| Apply a loadout whose items are held elsewhere | `LoadoutConflict` | pass `force`, or free the items |
| Undo a salvage past the window | `SalvageWindowExpired` | gone |
| Undo when the yield has been spent | `SalvageUndoInsufficientMaterials` | re-earn the materials, then undo |
| Create a rolled row past the structural ceiling | `InventoryCeiling` | salvage; this is a bug guard |
| Exceed I10's pouch cap | `CharmPouchFull` | remove a charm |

That is fourteen. If the owner wants a smaller surface, three collapse cleanly: `ItemNotOwned` and
`RoleNotOnFrame` and `FrameMismatch` could all become one `AssignmentRejected` with a detail field — at the
cost of a vaguer error. I recommend keeping them separate, because these are the errors a player sees.

**Every one of these rejects. None degrades into a partial operation.** The one deliberate exception is
the best-effort deploy projection in §5.6, which is a *reported* skip, not a silent one.

---

## 7. Worked examples

Numbers are illustrative, not balanced. Units are stated because SC4 requires it.

### 7.1 Kitting the twenty-first specimen

The player has bought 9 contract slots beyond the base 12 (`spec-demon-contracts.md:54` — the k-th costs
300 × k Souls) and owns 21 bound demons. #21 is a plant-frame Sunflower, phase `Roster`, wearing nothing.

`POST /api/items/issue-kit` → 15 roles resolved, 15 stock counters decremented, 15 assignment rows
written, one transaction.

| | Cost |
|---|---|
| Rows in `rpg_item` | **0** |
| Rows in `rpg_item_assignment` | 15 |
| Counter decrements | 15 |
| `effect_instance` rows created | **0** — the canonical stock instances already exist |

Grant: each stock piece carries one tier-1 implicit averaging **+8 hp (game units)**, so the kit is
**+120 hp** spread across the body. For scale, one rolled rare chest at tier 4 might carry **+95 hp** by
itself. Standard issue is worth roughly one and a third good chest pieces — enough that a bare specimen
is clearly worse, cheap enough that nobody is ever bare. **That is the whole roster-scale answer in one
row of arithmetic.**

### 7.2 A bulk salvage that would have eaten a good item

Player selects *salvage all Magic and below, unassigned*. 312 rows match.

Preview returns:

```text
matched   312
excluded   41   — locked 6 · assigned 12 · in a loadout 5 · best-in-role 18
salvages  271   yield 542 shard.magic   (2 per item)
undo      24 h or 200 operations, while you still hold 542 shard.magic
```

Named in the excluded list: **Verdant Bract of the Kiln** — Magic rarity, so it was inside the selection,
but it rolled **940‰** of its band on `atom.elemental-power.fire.t3` and it is the only fire-power bract
the player owns. G-D caught it. Under a lock-only design it would be gone, because the player had never
opened it — it was still `seen = 0`.

Commit salvages 271 and credits 542 `shard.magic` into `rpg_demon_materials`.

### 7.3 A content patch disables an atom

Import bumps `catalog_revision` 118 → 119 and sets `atom.thorn-riposte.t2` to `enabled = 0`.

| Effect | Where |
|---|---|
| 3 rolled items flagged `stale = 1` | excluded from the gap board and from G-D; still salvageable at full yield; not deleted |
| 1 of the 3 is assigned to specimen `d3f8…` in `armament-secondary` | the assignment **survives**; the next deploy builds **14 of 15** bindings and reports `armament-secondary skipped: StaleInstance` |
| The stock container `item.plain-thorn-guard` was also disabled | its counter of **6** converts to **6 × 1 = 6 `shard.normal`**, logged as `stock-converted` |
| Canonical stock instances | re-materialised at revision 119; stock bindings re-pointed in the same transaction |

Nothing is silently lost, and no deploy is refused over a patched trinket.

### 7.4 Comparison with no power number

Candidate helm vs the incumbent in `head-protective`:

| Channel | Unit | Incumbent | Candidate | Delta |
|---|---|---|---|---|
| `hp` | game units | 71 | 62 | **−9** |
| `accuracy` | resolver points | 9 | 14 | **+5** |

Verdict: **`sidegrade`** — neither dominates. Roll quality: candidate mean **610‰**, incumbent **540‰**.

The FE shows exactly this and **offers no single number**, with one line of copy explaining why: 9 hit
points and 5 accuracy points are not the same currency, and pretending otherwise would be a guess wearing
a decimal point. When E9 lands, a power column joins the table; the delta rows stay.

---

## 8. Failure modes

Unsentimental, and each names what in this design prevents it.

| # | Failure | Where it shipped | What prevents it here |
|---|---|---|---|
| 1 | **Inventory tetris as accidental gameplay** | Diablo 2's grid; Resident Evil by design (recalled, unverified) | No capacity at all, no grid, no per-specimen bags. The high-volume grade is a counter. The unit of action is the *assignment*, not a move between two containers. |
| 2 | **The stash becomes a museum nobody sorts** | Every ARPG with a big enough stash | Not fully solved — converted from a *storage* problem to an *attention* problem, which is the honest framing. The counters are the inbox (`seen`), the gap board (tells you where acting pays), auto-salvage rules, and a tidy advisory that never refuses. |
| 3 | **Bulk salvage destroys something precious** | Destiny 2's "dismantled a god roll" is the canonical case (recalled, unverified); Diablo 3 fixed most of it with lock + never-salvage-equipped | Four standing guards, a preview that names every exclusion, an undo window, and the rule that unseen items are excluded from bulk by default. G-D is the load-bearing one: lock only protects what you already looked at. |
| 4 | **Comparison UI cannot answer the only question players ask** | Any game that shows a raw stat block and calls it a tooltip | Delta table + dominance verdict + roll quality ‰, and a hard refusal to synthesize a scalar before E9 exists. A wrong number that looks authoritative is worse than no number. |
| 5 | **Per-actor gear makes the twenty-first specimen unaffordable** | Gacha rosters where only the meta five are geared | §7.1 — one click, 15 counter decrements, zero rows. Contract slots stay worth buying because the demon you buy can actually be fielded. |
| 6 | **Loadout rot** — presets silently referencing destroyed items | Common in games where presets are id lists | Loadout membership implies lock (G-C), and entries are validated on read with a `missing` marker rather than dropped. |
| 7 | **The gap board becomes a to-do list of 720 chores** | The failure mode a "helpful" completion screen creates | It defaults to showing only cells where an unassigned **strict** improvement exists — usually a handful — and collapses "issue stock" into one action per specimen. |
| 8 | **Bag pressure sold back as a feature** | Diablo 3 stash tabs (recalled, unverified) | There is nothing to sell, and no capacity to sell it against. The pressure is deliberately never built. |
| 9 | **Silent strip** — applying a loadout quietly undresses another demon | Any "equip best" button | Apply refuses by default with `LoadoutConflict` naming the conflicts; `force` steals and **reports every cell it emptied**. |
| 10 | **A patch bricks a save** — a disabled atom refuses a deploy | The exact shape of an atom-layer regression | Best-effort binding projection with a reported skip (§5.6). One dead trinket costs one role, never the deploy. |

---

## 9. What this lane needs from other lanes

Numbered, each naming the lane. This is where the insufficiency shows.

1. **I2 — the role id list, and confirmation on 15.** I need role ids as **stable, append-only,
   kebab-case** strings and the per-frame membership table, because `rpg_item_assignment.role` is a
   primary-key component and a rename would orphan every cell. **In return, I13's answer to the question
   I2 blocked on: 15 slots stands.** The two-grade split (§2.1) makes filling them bulk by default, so
   richness costs the player one click per specimen, not fifteen decisions. I also need to know whether
   roles **unlock with level** ([item-ideal.md](../item-ideal.md) §5.6) — the gap board must render a
   locked cell differently from an empty one, or it will nag about roles that do not exist yet.

2. **I3 — confirm the standard-issue grade exists.** `stock_eligible` requires base types that declare
   `pool_rolls = 0` and `Fixed`-only implicits. If I3's taxonomy has no such grade, the entire
   roster-scale answer collapses and I13 needs to know immediately. Name the grade; I will store it.

3. **I1 — rarity ordinals and salvage yield per rung.** Sorting, salvage-by-rarity, the auto-salvage
   floor, and G-D's tiebreak all key on the **ordinal**, never on the label. I also need the yield
   schedule (how many of which material per rung) — see 7.

4. **I4 — sockets and the promotion rule.** I am asserting that socketing **promotes** a stock item to a
   rolled row and that it never re-stacks. Confirm that, and tell me an uncut insert's storage grade — I
   have it as stackable until socketed, which puts it in `rpg_item_stock`.

5. **I5 — set membership must be cheap to read.** Set is a list filter and a salvage guard, so I need it
   readable per item at list time. **A tag on the container works; a container-of-containers does not** —
   the second shape makes "show me every set piece I own" a recursive query on every page render.

6. **I6 — the mutation operation log.** SC5 says an item's state is derivable from origin seed plus an
   ordered recorded operation list. I need (a) the table that holds that list, so I do not invent a second
   one, and (b) a flag or a derivable answer to *"has this instance been mutated"*, because that is what
   permanently blocks re-stacking.

7. **I9 — the material id namespace and the yield vocabulary.** I am reusing `rpg_demon_materials`
   (`RpgStore.cs:520`) rather than creating a second material store, so I9 must widen that id namespace
   beyond `essence.*` / `shard.*` rather than starting fresh. Also: when the power model lands, it becomes
   a **fourth column** in the comparison payload and the replacement for G-D's crude ranking — not a
   replacement for the per-channel delta.

8. **I10 — the pouch cap, and whether a charm is live from the pouch.** The number is theirs, the refusal
   site (`CharmPouchFull`) is mine. The bigger question: if a charm grants **while unequipped**, the pouch
   is a bindable owner and needs its own runtime projection at `player:{id}` scope, exactly as a specimen
   needs one at `entity:{ptr}`. Confirm, because that is a second projection path and I have only designed
   one.

9. **I11 — the equip gate's inputs.** `RoleNotOnFrame`, `FrameMismatch`, and `LevelTooLow` must all be
   evaluated in **one** place, and the gap board needs to call that same evaluator to pre-filter 720 cells
   without duplicating the rules. Give me a single `canEquip(item, specimen) → ok | reason` entry point.

10. **I12 — the rolled-drop rate.** §5.10 states the budget: ≤ ~30 rolled drops per hour of active play,
    ~60 reviewable per session. If I12's tables exceed that, a **drop-time loot filter becomes mandatory**
    rather than optional, and the surplus must be routed into stock and materials instead of into rows.

11. **The atom program (E6 / E14) — two asks, and one is a boundary correction.**
    - **(a) There is no durable owner scope for a specimen.** The 7 scopes in definitions §6 do not
      include one, `entity:` is explicitly session-scoped and never durable, and `player:` is the wrong
      grain. I13's design works around this with the assignment-plus-projection split (§2.4), which I
      believe is the *right* answer and not merely a workaround — but it needs E6's explicit blessing,
      because [item-ideal.md](../item-ideal.md) §6.4 currently says equipping *is* a binding, and that
      sentence needs correcting in the reconciliation pass. E6's own boundary says *"Ask first: adding an
      owner scope"*; I am asking whether to add `specimen:{guid}` or to bless the projection.
    - **(b) Stock refresh is a write into `effect_binding` from the importer.** Re-pointing canonical
      stock instances at a new `catalog_revision` touches E6's table during E14's import transaction.
      That crosses a program boundary and needs sign-off on which side owns the step.

12. **The unique-actor lane — three concrete changes.** Retire and fusion must call the assignment-release
    helper before tombstoning; `rpg_unique_equipment` (`RpgStore.cs:356`) and its `weapon|armor|trinket`
    allowlist are **retired** by this design, along with the FE's stub item ids at
    `RosterPage.tsx:33-34`; and `RebuildUniqueModsFromEquipment` (the W8-A `mods_json` grant-folding path)
    is superseded by the assignment → binding projection.

---

## 10. Open questions for the owner

1. **Is undo wanted at all?** The four standing guards plus a naming preview may be sufficient, and the
   undo window costs a tombstone column, a purge job, and the awkward "you spent the yield" refusal. I
   built it because bulk destruction is irreversible and this is a database, not a console — but it is the
   most droppable thing in this document.

2. **Should issuing a standard kit be free?** Free removes all friction from the 21st specimen, which is
   the point. Charging a small material amount gives the economy a permanent floor sink and makes stock
   feel earned. I lean **free at first, priced later if the material economy needs the sink** — but that
   is a product call, not a storage one.

3. **What is the structural row ceiling?** I used 20 000 rolled rows per player as an abuse guard. It
   should be set from I12's real drop rate once that exists.

4. **Does the commander share the armoury?** I assumed yes — one player, one armoury, and the commander is
   just another owner in the assignment table. If commander gear should be a separate pool (so a
   commander item can never be "wasted" on a demon), that is a different design.

5. **Retire while geared: auto-release, or refuse?** I chose auto-release with a reported count. Refusing
   ("unequip first") is more explicit and less magical, at the cost of an extra step on a routine action.

6. **Player-level loadout library, or per-specimen wardrobes?** I chose one player-level library with an
   optional frame constraint. Per-specimen wardrobes match the mental model better and cost more rows for
   a feature most players will use on five specimens.

7. **Fourteen new reason codes is a lot** against a closed list of 33. Three could collapse into one
   `AssignmentRejected` with a detail field. I recommend keeping them separate because these are the
   errors a *player* reads, not an author — but the surface size is the owner's call.
</content>
</invoke>
