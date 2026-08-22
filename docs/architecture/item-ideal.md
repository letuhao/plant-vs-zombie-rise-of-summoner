# The item ideal — what an item is, and what wears it

**Status:** **Ideal captured 2026-08-22.** Discussion document, not a spec and not a plan. No build is
authorized from it. Program prefix: **`item`** (free — no `docs/architecture/item-*` and no
`tasks/item-*` existed when this was written). When it graduates, the capability map is
`docs/architecture/item-map.md`, module specs go in `docs/architecture/item/`, and tasks are
`tasks/item-plan.md` / `tasks/item-todo.md`, per the parallel-programs convention in AGENTS.md.

**Inspiration:** the Diablo / Path of Exile line of item systems — base types with implicit modifiers,
affixes rolled from tiered pools at drop, one modifier per family, rarity governing how many affixes
and how strong, hand-authored uniques standing beside procedurally rolled rares.

**Read before proposing against this:** [DESIGN-GATE.md](../DESIGN-GATE.md) §1 rows for the atom layer
and for match/actor lifecycle. This document was written after reading
[effect-atom/definitions.md](effect-atom/definitions.md) (which wins over any spec),
[atom-catalog-ssot.md](effect-atom/atom-catalog-ssot.md),
[atom-family-library.md](effect-atom/atom-family-library.md),
[spec-container-schema.md](effect-atom/spec-container-schema.md),
[spec-instance-and-binding.md](effect-atom/spec-instance-and-binding.md),
[effect-atom-map.md](effect-atom-map.md), [decisions.md](decisions.md),
[standalone-rpg-map.md](standalone-rpg-map.md), and [unique-actor-runtime.md](unique-actor-runtime.md).

---

## 1. The ideal, in one paragraph

An item is a **container of atoms** that an actor wears in a **slot**. It has a **base type** that decides
which slot it fits, which frame of body can wear it, and what it always carries (its implicit). It has
**rolled affixes** drawn from a tiered pool, frozen at the moment it drops. Three families of body wear
three different vocabularies of gear — **humanoid**, **plant**, and **hybrid** — and those vocabularies
are *parallel*, not duplicated: the same twelve slot **roles** exist in each, so one affix library serves
all of them and only the base types differ. Everything that is not equipment — materials, consumables,
quest items, currency — is a separate, simpler thing with its own storage, because the moment an item
carries rolled values it stops being stackable and needs a different table.

---

## 2. What the owner decided (2026-08-22)

These are inputs to the document, not proposals inside it.

1. **Zombies equip human-like equipment.**
2. **Plants equip a special type of equipment for plants.**
3. **The player/commander is human, plant, or zombie**, and therefore wears human-like or plant-like
   equipment accordingly.
4. **Three main equipment categories** follow from 1–3.
5. **Other item types exist:** material, quest items, consumables, and more.
6. **Slot sets should be rich** for both human-like and plant-like frames.
7. **Rarity, socket mechanism, and set-item combos are deferred** to the next discussion round. §11 holds
   what must be answered there; nothing in this document pre-decides them.

---

## 3. What already exists — the substrate this inherits

The effect-atom program built the machine an item system needs. **E1–E6 are built and green**
([tasks/effect-atom-todo.md](../../tasks/effect-atom-todo.md)), which means the following is not a
proposal — it is shipped code and schema.

| An item system needs | Already exists | Where |
|---|---|---|
| An item **template** | `effect_container` with `container_kind='item'`, `slot`, `rarity`, `min_tier`/`max_tier`, `level_req`, `pool_rolls` | [spec-container-schema.md](effect-atom/spec-container-schema.md) |
| A tiered **affix pool** with one-mod-per-family | `effect_container_pool` — `weight`, `group` defaulting to `(family_id, variant)` | same |
| A **specific dropped item** with frozen rolls | `effect_instance` + `effect_instance_atom`, `roll_seed`, reproducible from `(container_id, catalog_revision, roll_seed)` | [spec-instance-and-binding.md](effect-atom/spec-instance-and-binding.md) |
| **Equipping** | `effect_binding` — instance → owner scope, with `slot`, `priority`, bind-time rejection | same |
| A **rarity ladder** with stable ordinals | the `rarity` table, explicit append-only ordinals | E5 |
| The **affix library** | ~71 authored families × 5 tiers ≈ 355 atoms, plus ~420 generated element rows | [atom-family-library.md](effect-atom/atom-family-library.md) |
| A **player-scoped stackable inventory** | `rpg_demon_materials(player_id, material_id, qty)`, seeded by expeditions with `essence.{element}` and `shard.{rarity}` | `RpgStore.cs`, `DemonMaterialCatalog.cs` |

**The law that comes with it:** *items have no behaviour; actors do*
([definitions.md](effect-atom/definitions.md) §0). An item is a **source** that puts atoms on an actor's
effect list. It does not participate at runtime, it is not an execution unit, and `seq` inside a
container is authoring order, not execution order.

Two more inherited rules that shape everything below:

- **Rarity picks affix *count* and the *tier window*; tier carries strength. Rarity may never change a
  magnitude.** That split is already enforced by columns, not by convention.
- **`group` defaults to `(family_id, variant)`**, so an item can roll fire power *and* ice power but
  never two tiers of the same affix — the rule that stops a rolled item reading `+10 atk / +12 atk`.

### What exists for items today, honestly

A stub and nothing more:

- `rpg_unique_equipment(instance_id, slot, item_id)` — three columns, PK `(instance_id, slot)`
  (`src/FusionRpg.Data/Sqlite/RpgStore.cs:356`).
- `UniqueEquipmentCatalog` — **3 hardcoded items**, slot allowlist `weapon/armor/trinket`, each mapping
  to one grant template folded into `mods_json`; one of the three points at a placeholder effect id
  (`src/FusionRpg.Core/Match/UniqueEquipmentCatalog.cs`).
- Battle reads equipment **nowhere**: `ChannelMods` is documented as "trait stat mods, equipment later"
  (`src/FusionRpg.Core/Battle/BattleModels.cs:20`).

There is **no player/commander actor**. `players` is `(id, name)`; a player owns souls, materials, and a
patron designation (`RpgStore.cs:413`). A commander with equipment is a **new entity**, not an extension
of an existing one.

---

## 4. Frame — the key the whole system turns on

### Faction is not body

`DemonSpeciesDef.Side` is documented as *"linked capture side (plant | zombie) — portrait/body source"*
(`src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs:11`) — one field carrying faction **and** body. The
generated roster is 18 zombie-side and 6 plant-side species, and several zombie-side entries are Fusion
hybrids: `peashooterzombie`, `ironpeazombie`, `cherrynutzombie`, `bucketnutzombie`
(`DemonSpeciesCatalog.Generated.cs`). A peashooter-zombie is faction-zombie with a plant body.

**So the item system must not key on `side`.** It keys on an explicit **frame**:

| Frame | Who | Wears |
|---|---|---|
| `humanoid` | zombies, humans, the human commander | human-like gear |
| `plant` | plants, the plant commander | plant-like gear |
| `hybrid` | Fusion crossbreeds — a headline feature of the base game, not an edge case | see §5.3 |

Frame is a property of the **species / body**, declared once. Faction stays a separate field and keeps
doing what it does — element rings, capture side, portrait source. Deriving one from the other is the
mistake this section exists to prevent.

### Three different things are called "slot" — do not share a type

This word is already overloaded in the tree, and [definitions.md](effect-atom/definitions.md) §6 warns
about two of the three explicitly.

| Meaning | Where | Note |
|---|---|---|
| **Equip slot** — where an item goes on a body | `effect_container.slot`, `effect_binding.slot` | this document's subject |
| **World construction slot** — a buildable position in a sector | the `slot:{id}` owner scope | *"unrelated to an item's `slot` column. Two different concepts, one word — do not share a type"* |
| **Expedition slot** — a parallelism gate (2 → 5 via progression) | [standalone-rpg-map.md](standalone-rpg-map.md) | unrelated again |

---

## 5. Slots

### 5.1 One role table, two vocabularies

The design that keeps the item database from doubling: **twelve slot roles exist once**, and each frame
names them in its own fiction. Affix families are scoped to the **role**, so they are authored once and
serve every frame. Only **base types** are frame-specific.

| # | Role | Humanoid slot | Plant slot | What the role is for |
|---|---|---|---|---|
| 1 | head-protective | `head` | `crown` | the classic helm budget — defence with a small offensive rider |
| 2 | sense-utility | `face` | `bract` | accuracy, detection, crit-rate — the "how well it aims" slot |
| 3 | core-protective | `torso` | `stem` | the largest defensive budget on the body |
| 4 | mantle-utility | `back` | `canopy` | cloak / overhead shade — resistances, ambient mitigation |
| 5 | manipulator-offense | `hands` | `leaves` | attack speed, crit, on-hit riders |
| 6 | footing | `feet` | `roots` | humanoid = mobility; **plant = anchor** — same slot, different affix flavour (§5.4) |
| 7 | girdle-resource | `waist` | `soil` | the resource slot — pools, regen, economy, consumable capacity |
| 8 | jewel-major | `neck` | `pollen` | the amulet — the strongest single non-weapon affix source |
| 9 | jewel-minor A | `ring-1` | `graft-1` | |
| 10 | jewel-minor B | `ring-2` | `graft-2` | duplicated on purpose — see §5.5 |
| 11 | armament-primary | `main-hand` | `muzzle` | identity-defining: what this actor *does* |
| 12 | armament-secondary | `off-hand` | `thorn` | shield / focus / spines — the defensive or amplifying half |

Twelve per frame is deliberately rich — Diablo 2 shipped ten and Path of Exile nine plus flasks and
jewels, and the extra two here (`sense-utility`, `mantle-utility`) exist because this game already has
an accuracy/crit layer and a resistance layer deep enough to deserve dedicated homes.

### 5.2 The plant fiction, stated so it stays coherent

A plant is rooted, has no hands, and does not walk. The vocabulary must not be a humanoid one with the
words swapped, or it will read as a costume.

| Plant slot | The fiction | The mechanical consequence |
|---|---|---|
| `crown` | the bloom or canopy at the top | takes the helm's defensive budget |
| `bract` | the leaf collar around the bloom | targeting and perception affixes |
| `stem` | trunk, bark, husk plating | the core armour |
| `canopy` | overhead leaf spread | ambient and elemental mitigation |
| `leaves` | leaves as manipulators | fire rate, on-hit riders |
| `roots` | what it stands in, not what it walks on | **stability, regeneration, resource draw** — never movement speed |
| `soil` | the pot, the bed, the earth it occupies | sun / qi economy, resource pools |
| `pollen` | scent, spores, aura | the amulet-grade aura affixes |
| `graft-1/2` | grafted cuttings and scions | the ring-grade minor affixes |
| `muzzle` | the nozzle, seedpod, or barrel it fires from | the weapon |
| `thorn` | spines, burrs, the defensive husk | the off-hand |

### 5.3 Hybrid — flexibility paid for in slot count

A hybrid body can plausibly wear either vocabulary. Left unpriced, that is strictly better than both
pure frames — it sees twice the loot pool, so every drop is useful to it and hybrids become the only
frame worth playing.

**Proposal:** a hybrid gets **ten slots instead of twelve** — it loses `mantle-utility` and one
`jewel-minor` — and each remaining role accepts a base type from **either** frame. Breadth is bought
with depth. Alternative shapes worth arguing in review: a fixed per-role frame assignment per species
(no choice, but flavourful), or twelve slots with hybrids barred from set and unique items. Open.

### 5.4 The same role can mean different things per frame

`footing` is the honest example. On a humanoid it is movement, initiative, and evasion. On a plant,
movement is meaningless — a plant that "runs faster" is nonsense, and the affix would be dead content of
exactly the kind [atom-catalog-ssot.md](effect-atom/atom-catalog-ssot.md) §8a bans (*"a row no code
consumes is not content; it is a lie in a table"*).

**Rule: an affix family declares which frames its role serves.** The role is shared; the family list per
role is frame-filtered. That is one extra column, and it is what stops `+move speed` rolling on a
turnip.

### 5.5 Why two jewel-minor slots

Duplicated slots exist so a player can express *degree*: two rings means the choice is not "this affix
or that one" but "how much of this axis do I want". Diablo and Path of Exile both keep exactly two, and
both keep them the weakest per-slot budget precisely because doubling a strong slot doubles a strong
build. Keep the pair, keep the budget small.

### 5.6 Commander slots

The commander is a new actor (§3) and gets the full twelve of their chosen frame, plus a proposal:

**One commander-only role — `standard`** (banner, seal, sigil, or root-totem depending on frame) whose
atoms bind at **`match` scope rather than to the commander's own body**, so commander gear buffs the
whole squad. That scope already exists and needs no new mechanism, and it gives commander itemisation a
reason to be different from wearing thirteen copies of a demon's gear.

Also worth deciding early, because it is a progression lever rather than a content lever: **do slots
unlock with level?** Starting a commander at six slots and opening the rest over the campaign is a
cheap, legible progression axis, and it lets early loot matter without early builds being complete.

---

## 6. What an item is made of

### 6.1 Base type — the thing slots and implicits hang on

Every item has a **base type**: `plate-helm`, `iron-crown`, `pea-nozzle`, `bark-plating`. The base type
declares:

| Base type declares | Why |
|---|---|
| `frame` — humanoid / plant / either | which bodies may wear it |
| `role` — one of the twelve | which slot it occupies |
| `implicit` — zero or more fixed atoms | the modifier every copy carries, before any roll |
| `item_level` band | which tier window its affix pool may offer |
| `requirements` | level, and possibly faction or element |

**Implicits are the reason two items in the same slot feel different.** A crown that always carries
`+regeneration` and a crown that always carries `+fire power` are the same slot and the same affix pool,
but they are not the same choice. Without implicits, base types are cosmetic and every slot collapses
into "the one with the best roll".

### 6.2 The rolled part maps onto columns that already exist

The container schema expresses the Diablo rarity model without inventing anything:

| Item rarity | What it means | Expressed as |
|---|---|---|
| Normal | base type and implicit only | `pool_rolls = 0`, no pool rows |
| Magic | one or two affixes | `pool_rolls = 1..2` |
| Rare | three to six affixes | `pool_rolls = 3..6`, wider `min_tier`/`max_tier` |
| Unique | hand-authored, named, fixed identity | fixed core atoms; `pool_rolls = 0` or a small deliberate pool |
| Set | deferred — §11 | — |

`pool_rolls`, `min_tier`, `max_tier`, `rarity`, `weight`, and `group` are **already columns**. The
validation that makes them safe already exists too: a pool that cannot satisfy its own `pool_rolls`
rejects as `PoolRollsExceedGroups`, an all-zero-weight pool rejects as `UnsatisfiablePool`, and an atom
outside the tier window rejects as `TierOutOfWindow`. None of that has to be built for items — it has to
be *authored against*.

**The claim to test when this is built:** a new item base type should cost one container row plus its
pool rows, and a new affix should cost one atom row. If it costs code, the inheritance failed.

### 6.3 An instance is the item

A dropped item is one `effect_instance` plus one `effect_instance_atom` per atom, with the
`OnInstantiate` rolls resolved and frozen and the `roll_seed` recorded. Re-running instantiation with
the same `(container_id, catalog_revision, roll_seed)` reproduces it byte-identically.

**Open, and it matters for the schema:** is the instance *itself* the item, or is there an `item` row
above it? An item row earns its place only if items need things an effect instance should not carry —
a display name, an icon, bind-on-pickup, a stack of charges, a sell value, durability, a favourite flag.
Several of those are likely. The cheap answer is a thin `item` table keyed on `instance_id`; the wrong
answer is duplicating rolls into it.

### 6.4 Equipping is a binding

Equipping = create an `effect_binding` from the instance to the wearer's owner scope, with the item's
role in the binding's `slot` column. Unequipping = withdraw it. The bind gate already rejects for
runtime support, scope legality, `level_req`, and stale content, with named reason codes.

This retires the stub: `rpg_unique_equipment` and `UniqueEquipmentCatalog`'s `mods_json` grant-folding
go away, and E6 already migrates `mods_json` grants into `effect_binding` rows.

---

## 7. Items that are not equipment

The dividing line is not flavour, it is **whether the item carries rolled values**. A rolled item is
unique by construction and cannot stack; everything else can, and should live in a table that knows it.

| Category | Rolled? | Stacks? | Storage | Notes |
|---|---|---|---|---|
| **Equipment** | yes | never | gear inventory, one row per instance | §5–6 |
| **Material** | no | yes | **already exists** — `rpg_demon_materials`, generalise it beyond demons | crafting and upgrade inputs; expeditions already seed `essence.*` and `shard.*` |
| **Consumable** | no | yes, with charges | own store; possibly held in the `girdle-resource` slot | see below |
| **Quest / key** | no | usually no | own store, undroppable, unsellable | must never compete for gear space |
| **Currency** | no | yes | ledger, not inventory — souls already work this way | `rpg_soul_balances` / `rpg_soul_ledger` is the precedent |
| **Socket insert** (gem / rune) | maybe | yes until socketed | material-shaped | deferred to §11 |
| **Cosmetic** | no | no | wardrobe | out of scope for the first pass |

**Consumables are the one category that is not free.** A consumable does something *when used*, which is
an **action**, and actions are the [action program](action-map.md)'s — an action is an envelope (when) +
a container of atoms (what) + a target rule + a cost. A healing potion is therefore an item that carries
an action, not an item that carries atoms directly. Two consequences:

1. **Consumables should wait for the action layer**, or ship in a deliberately degenerate form
   (self-targeted, instant, no cost) that the action layer later absorbs without a migration.
2. The classic failure — consumable spam trivialising combat — is solved with charges refilled at rest
   and shared cooldowns, both of which are action-layer concepts, not item-layer ones.

---

## 8. Where items come from and where they go

**Sources**, in the order they already have machinery:

| Source | Status |
|---|---|
| Expedition rewards | server-resolved with a sealed seed today; rolling an item instance is the same shape as rolling a material |
| Battle / wave rewards | needs a drop table, which is a weighted pool — the same primitive the container pool already is |
| Crafting and upgrade | needs materials (exist) and a recipe table (`recipes` exists for fusion, different grain) |
| PvZ extension play | bounded by the standalone charter — see §9 |

**Sinks.** An item system without sinks becomes a museum. The standard three, in increasing order of
how much design they cost:

1. **Salvage** — unwanted gear becomes materials. Cheap, and it feeds crafting immediately.
2. **Reroll / upgrade** — spend materials to re-draw an affix or push a tier. This is where the tier
   window and `roll_seed` earn their keep.
3. **Sockets and sets** — deferred to §11.

**The open economic question:** items are per-actor, and this game has *rosters*. Twenty demons times
twelve slots is 240 equipped items before anything sits in a bag. Either gear is scarce and most
specimens go bare, or gear is plentiful and inventory management becomes the game. Games that solved
this went one of three ways — shared account-wide stat pools, per-specimen gear that is cheap and
disposable, or a small deployable squad so only a handful of actors ever need gear. **This must be
decided before slot counts are final**, because it is the difference between twelve slots being rich and
twelve slots being a chore.

---

## 9. Constraints this inherits and cannot argue with

| Constraint | Consequence for items |
|---|---|
| **Standalone-first** ([decisions.md](decisions.md)) | Every item must be earnable and usable **with the game closed**. PvZ may enrich the item economy, never gate it, and must never be the best source of anything web mode also provides |
| **No new atom kinds** | 12 kinds, 5 attach points, 7 triggers, closed. An item that needs a thirteenth kind is a design conversation, not a row |
| **`stat.derived` is quarantined `None/None/None`** (D6) | An item made of `+fire power` affixes **binds nowhere** until E12 ships the first consumer. First-wave items are realistically `stat.modify`, `resource.delta`, `status.apply`, `shield.grant`, and the board/economy families |
| **Power is open** (E9, build position 15) | `power_json` is nullable, so items can ship before power exists — but drop bands, authoring budgets, and "which of these two is better" have no number behind them until it does |
| **G8 — `warding` / `resilience` are match-scoped only** | A `+defense` affix on a single item bound to one actor **silently does nothing**. Per-actor mitigation must use `combat.defense.*`, which is `stat.derived`, which is quarantined. Worth saying out loud: **"+armour" is currently the hardest common affix to ship** |
| **Battle reads no equipment** | Until a consumer exists, an equipped item affects nothing in battle. `BattleStatComposer` at squad build is the natural first reader — the same seam E12 opens for traits |
| **One economy** | Web and PvZ write the same ledgers through the same ingest, source-tagged, never forked |

---

## 10. What this does *not* touch

Loot volume and filters, trading between players, durability, appearance/transmog, item level inflation
and squishes, and any change to the Foundation contract. Named here so nobody assumes silence means
approval.

---

## 11. Deliberately open — the next discussion round

The owner scoped these out of this pass. They are written as questions, not as leanings.

### Rarity
- What is the ladder, and how many rungs? The `rarity` table exists with append-only ordinals, so adding
  a rung later is cheap but reordering is not.
- Does rarity control affix **count**, affix **tier window**, or both? The schema supports both
  independently — that is a choice to make, not a constraint.
- Where do hand-authored **uniques** sit relative to rolled rares, and what keeps both relevant?
- Drop rates, and whether there is bad-luck protection. The summon pity counters are the in-tree
  precedent.

### Sockets
- Are sockets a **count rolled on the item**, a property of the base type, or added by crafting?
- Are inserts **typed** (a fire gem only fits a fire socket), or universal?
- Is a socket a real choice or a stat tax? The failure mode is well documented: a socket system where
  the correct insert is always obvious adds inventory work and no decision.
- Does socketing map onto the existing container pool, or does it need a new table? An insert is an atom
  arriving after instantiation, which the current model does not have a shape for — **this is the one
  deferred mechanic that probably needs new schema.**

### Sets
- **Discrete breakpoints** (2-piece / 4-piece / 6-piece) or **summed contribution** across pieces? These
  produce very different build spaces.
- How is set membership expressed — a tag on the container, or a container of containers?
- What prevents set-jail, where one set is so strong every build converges on it?
- Are set pieces rolled or fixed?

### Also still open from this document
- Hybrid slot rule (§5.3) — ten flexible slots, fixed per-species assignment, or twelve with
  restrictions.
- Whether the item entity is the `effect_instance` or a thin row above it (§6.3).
- Commander `standard` slot and squad-scoped bindings (§5.6).
- Slot unlocking as progression (§5.6).
- Roster-scale gear economy (§8) — the one that must be answered before slot counts freeze.
- Consumables: wait for the action layer, or ship degenerate (§7).

---

## 12. Prior art this draws on

Diablo 2 / D2R — base types with implicit modifiers, affix tiers, the ten-slot body, and rarity as affix
count. Path of Exile — modifier families with one mod per family, item level gating tier access, and
crafting as the primary sink. Diablo 4 — item power bands selecting affix ranges, so power is an input
rather than only an output. Last Epoch — a per-item spend budget and tier-plus-range-within-tier, the
closest published model to the value spec this repo already ships.

**Honest note on sourcing:** a web research sweep was launched for this document and did not return
before it was written. The prior-art claims above are from general knowledge and are stated at a level
that does not depend on specific numbers. **Any number that ends up in a spec must be re-verified
against a source.**

---

## 13. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — effect-atom (container/instance/binding),
    unique-actor lifecycle, demon species, standalone economy.
[x] I read every doc in the §1 row(s) for those subsystems, this session.
[x] I checked decisions.md for a lock covering this — standalone-first, resource model,
    action model, and the golden-ordering row all bear on it; none forbid this document.
[x] Every factual claim about the repo cites file:line or a doc.
[x] I verified claims against CODE, not comments — the equipment stub, the species roster,
    the materials table, and the battle ChannelMods comment were all opened.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. **Gap: no test suite was run for this
    document.** The constraints in §9 are read from shipped specs and code, not executed. Before
    any of them is used to justify a build decision, run the suite.
[x] Nothing contradicts a §2 invariant.
[ ] Corrections propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    **Gap: no map, plan, or task list exists yet** — this is an ideal, and those artifacts are
    written when the program graduates.
```
