# Scoped inventory hierarchy — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.

**Update (2026-09-13, same day, owner decisions folded in):** four of the five open questions below
are now resolved by explicit owner decision — sequencing (#5), legion cargo's capacity shape (#1),
sector storage's capacity shape (#2), and cross-faction transfer (#3). Only #4 (which loop unlocks
sector cargo) remains open. See each resolved question for the owner's verbatim framing and this
session's fresh verification against source. **One correction to this doc's own prior reasoning:**
open question #2's recommendation (generalize `CapacityBonus` as a second field) is now known to be
wrong — the owner chose the opposite. Flagged in place below, not silently dropped.

**Correction (strengthen pass 2026-09-13, finding F1) — the count above overclaims.** Q3's own
"RESOLVED... verified buildable" framing is corrected below: the **schema choice** (key ownership on
the string `WorldFaction`/`FactionId` model, never the closed `CommanderId` enum) is genuinely
resolved and still right. **Cross-faction item transfer as a buildable mechanism is not** — verified
this session, `rpg_world_factions` has no `player_id` column at all (one `rpg_worlds` row carries
exactly one `player_id` for the whole world) and `WorldFactionKind`'s members
(`Player`/`Zomboss`/`Clan`/`Rival`/`Wild`) are AI sub-actors inside that one player's own world, not
separate players each with their own item-owning `player_id`. So the honest count is: **three**
resolved outright (#1, #2, #5), Q3's schema half resolved and its mechanism half downgraded from
"resolved" to a **real gap**, and resolving Q3 at all spawned a **new**, equally-open Q6
(consent/offer flow) — see Q3 and Q6 below, not silently folded into the "four resolved" count.

**Program id:** `scoped-inventory`. Triggered while designing the Wonder/relic feature
([loam-relics-and-wonders-ideal.md](loam-relics-and-wonders-ideal.md), a sibling doc — not touched by
this session, cross-referenced only): a legion carrying a relic to a sector needs somewhere to put it
down, and today only one inventory scope exists end to end (empire). This doc is the owner's own
request to enrich the whole hierarchy before any one scope (sector) gets built in isolation.

**Owner, verbatim (2026-09-13):** *"the thing new is when legion carry the relic to the sector, it
need sector inventory to store, this is whole new feature because we only have empire scope
invetory that share all, this relic is special show we will enrich idea for new sector scope
inventory and legion scope inventory so we have empire scope inventory access everywhere, legion
scope inventory that a legion manage, and trade between 2 legion, sector scope inventory need
storage building to extend slots and share between sector by trading route (will add later for
trading program) and legion can depoit/withdraw from sector scope when they come to sector unique
actor inventory and delve party inventory this is whole new hierachy scoped inventory deploy agent
to enrich it."* And, binding the scope of this session specifically: **"we cannot design everything,
just focus on what we already have first."**

---

## Which loop this extends

**Added as its own heading (strengthen pass 2026-09-13, finding F12) — was folded into Step 0 prose;
content unchanged, only promoted so it survives a long session per the idea-phase skill's required
structure.**

Rise of Summoner is an RPG plus empire-building game (`the-game.md:11`). This doc extends
**Place 4 (World map — adventure: legions)**, **Place 5 (World stage — empire building: storage
buildings, sector slots)**, and **spine C (Item collection and progression)** — `the-loops.md:83-121`
names buildings/wardens and stage buildings as **WIP** explicitly, and item collection's own status
line is `the-loops.md:49-58`. **Place 6 (the Delve)** is not extended here — its party inventory
(`loot-pack`) is read only as the nearest existing precedent for a capacity-bounded, non-empire scope,
per this doc's own comparison requirement.

## Step 0 — principles, restated

- **Every RPG feature lives in the RPG layer; it is never built by changing what PvZ is.** Every
  scope discussed here — empire, legion, sector, unique actor, delve party — is `FusionRpg.Core`
  state plus `FusionRpg.Data`/`RpgStore` rows. None of it touches a Unity field or asks PvZ to
  represent an inventory of any kind; the PvZ write surface is not a constraint on this feature at
  all.
- **Two async systems, deltas not absolutes, record-then-drain.** Not directly load-bearing here —
  no lawn write is involved — but any future deposit/withdraw resolver that runs inside a world
  turn must still be a pure `(state, seed) -> state` phase, the same shape `LoamPhases.Production`
  already uses (`LoamPhases.cs:18-50`), not a live read of a store mid-`Step`.
- **One power ladder.** Nothing proposed here is a magnitude derived from a level. Capacity numbers
  (grid cells, `CapacityBonus`) are structural counts, not `P(Θ)`-shaped — the same ruling
  `spec-loot-pack.md` §8 already made for the delve pack's own grid (*"the grid does not read
  Θ"*), and it applies to a sector's or legion's item capacity for the identical reason: a container
  size is a structural/UX bound, not a combat magnitude.
- **The balance surface is data.** Any capacity number, cost, or transfer rate this hierarchy
  eventually introduces belongs in `data/tuning/<domain>.v{n}.json`, never a `const` — named in
  §Tunables below, none of it decided yet.
- **No hard progression ceilings.** The empire scope is already unbounded by design
  (`ssot-inventory.md:670`, quoted below) and only carries an **abuse guard**, never a progression
  cap (`InventoryCeiling = 20_000`, `RpgStore.Items.cs:276`, doc comment: *"a structural bug guard,
  never a progression ceiling"*). Any new scope's capacity (legion cargo, sector cargo) must follow
  the same discipline: a structural/UX bound tied to something in-fiction (a building, a footprint),
  never a silent progression clamp.
- **Gameless-first.** Fully satisfied by construction — every scope discussed is world-map/empire
  state, already playable with Fusion closed today (the world map is Shipped per `the-loops.md:105`).

DESIGN-GATE §1 rows read this session: Product vision (`the-game.md`, `the-loops.md`), Data/SQL
(`data-architecture.md` — not re-opened line-by-line; no schema is proposed, only shape options),
World map (`world-map-program.md` referenced via code; not re-opened in full — `WorldState.cs` was
read directly instead, the primary source it summarizes), and the `docs/architecture/item/` lane for
the armoury (`ssot-inventory.md` — read this session, cited below). `decisions.md` and
`empire-economy-ssot.md` were grepped for an existing "inventory hierarchy" or "legion/sector
inventory" row this session; **neither has one** — this is confirmed greenfield at the doc level, not
an oversight of an existing lock.

---

## What this is

**The player sentence:** *My empire's stash is mine everywhere — home, delve provisioning, lawn
deploy, all draw from the same pool. A legion marching the rift carries its own cargo, separate from
the stash, and can trade cargo with another legion it meets. A sector I hold can store items too, but
only once I've built something there to hold them — and a legion standing in that sector can put
things into that store or take things out. Someday, sectors will be able to share stock with each
other over a trade route, without a legion physically walking it there — but that's a later program.*

Today, exactly **one** of those five scopes is real: the empire stash. Everything else — legion cargo,
sector cargo, the storage building that unlocks it, legion-to-legion trade, and sector-to-sector trade
routes — is **either a real gap or explicitly deferred**. The unique-actor scope (equipped gear) and
the delve-party scope (the in-run carry limit) already exist as their own, narrower things and are not
being redesigned here; they are read as **precedent** for how a non-empire scope should be shaped.

---

## What already exists

### Empire scope — BUILT

| Finding | Evidence |
|---|---|
| `rpg_item` — one row per rolled instance, `instance_id` PK, `player_id` column is the **reachability root** independent of any assignment | `RpgStore.Items.cs:46-98` (schema `:85-98`; doc comment `:36-38`: *"the second reachability root... an instance is now collected only when it has neither a binding nor an owner"*) |
| `rpg_item_stock` — fungible per-player counts, `(player_id, container_id)` PK | `RpgStore.Items.cs:7` (`RpgItemStockRow`), schema `:101-107` |
| **Reachable from every context** — lawn deploy, delve provisioning, expeditions all read the same `player_id`-keyed rows; there is no scope column to filter on at all | `RpgStore.Items.cs:46-107` (no `context`/`scope` field anywhere on either table) |
| **Explicitly, deliberately uncapped** — "no capacity at all, no grid, no per-specimen bags" is a named genre-failure-mode countermeasure, not an oversight | `docs/architecture/item/ssot-inventory.md:670` |
| The one guard that does exist is an **abuse guard**, not a progression ceiling, and is scoped to prevent a runaway *bug*, never a player | `RpgStore.Items.cs:268-276` (`InventoryCeiling = 20_000`, doc comment quoted above); a sibling test `NoCapacityCapExistsOutsideTheNamedAbuseGuard` (`ArmouryTests.cs`, per the same comment) enforces that no second cap is ever added |

This is exactly the owner's "share all" — the empire scope needs no new work.

### Unique actor scope — BUILT (precedent, not being redesigned)

| Finding | Evidence |
|---|---|
| `rpg_item_assignment` — one row per `(specimen_id, role)`, pointing at either an `rpg_item.instance_id` or a stock `container_id` | `RpgStore.Items.cs:17-20` (doc comment), schema cited at `RpgStore.Items.cs:152-159` (per `spec-corpse-cache.md`'s own citation, confirmed this session) |
| Commander pouch is a **sibling table**, not the same rows reused — `rpg_player_item_assignment`, keyed by `player_id` instead of `specimen_id`, because Dave is not a persistent unique specimen | `RpgStore.Items.cs:17-20` doc comment: *"Commander equipment uses this scope instead of pretending that the commander is a persistent unique specimen"*; schema `RpgStore.Items.cs:164-171` |
| Capacity here is not a cell grid — it is the **closed 15-role enumeration** (`ArmamentPrimary` … `JewelMinorB`, plus a reserved `Standard` for the commander) | `src/FusionRpg.Core/Items/ItemRole.cs:11-31` |

**Why this matters for the shape question below:** this scope proves capacity does not have to mean
"a grid" — a fixed, closed set of named slots is a legitimate second capacity shape already shipping.

### Delve party inventory — Real gap (unusually well-specified)

**Corrected taxonomy (strengthen pass 2026-09-13, finding F17).** The idea-phase skill's bucket
vocabulary is closed at three words — Built / Wiring gap / Real gap — and a first draft invented a
fourth label ("a fourth state, named precisely") outside it. Zero code runs for this scope, so by the
skill's own definitions it is a **Real gap**, full stop. What makes it worth a prose note rather than a
plain real-gap entry: unlike a typical real gap, this one already has a **fully designed and
owner-approved spec** sitting behind it (`loot-pack`) — nothing is undesigned, only unbuilt. That
distinction belongs in prose, not in a new bucket word.

| Finding | Evidence |
|---|---|
| Status line: **"APPROVED by the owner 2026-09-05 (wave 3) — unbuilt"** | `docs/architecture/party-dungeon/spec-loot-pack.md:3` |
| `PackGrid(rows, cols)` per `(delveId, partyIndex)` — capacity is a **structural grid derived from role/mass-class footprint**, never authored on an item, never Θ-scaled | `spec-loot-pack.md` §1-2, §8 ("the grid does not read Θ") |
| Ownership is **never duplicated** — a haul instance is owned (`rpg_item.player_id`) at the moment it is placed in the pack; the pack is a **capacity + reachability overlay** on top of the same empire-scope ownership row, plus a lock table (`rpg_delve_pack_lock`) so the armoury can't touch a carried instance | `spec-loot-pack.md` §5 ("A placed haul instance is acquired at placement"), §4 ("a lock table... the reason `rpg_expedition_members` is a table") |
| Extraction **moves** the same rows back to plain empire-scope reachability (unlocks + stock upsert) — it never copies data between two independent stores | `spec-loot-pack.md` §7 |
| Explicit design ruling that this scope's cap is **structural, not a progression ceiling** — the exact discipline any new scope's cap must also satisfy | `spec-loot-pack.md:26` ("a structural per-run limit, not a progression ceiling; the stash is uncapped") |

**Why this matters for the shape question below:** `loot-pack` already proves the "move, never copy;
capacity is a derived overlay on the same flat ownership root" pattern for a *third* scope beyond
empire and unique-actor. Legion and sector cargo are proposed below as the same pattern's fourth and
fifth instances, not a new invention.

### Legion scope — REAL GAP

| Finding | Evidence |
|---|---|
| `WorldEntity` (the legion record) carries **exactly one** carried-resource field, and it is a bare scalar, not an item container | `WorldState.cs:286-326` — the entire record; the only carried-anything field is `public long CarriedLoam { get; init; }` (`:325`) |
| Confirmed explicitly, in-repo, that legions carry **no items today** | `deployment-hierarchy-ideal.md:231`: *"a legion stack uses HoMM3 top-unit math... No tiers, no recovery rows, **no inventory** — the dead stay dead."* |
| `CarriedLoam` is spent by move/build resolvers exactly like a wallet, never referenced by anything item-shaped | `SustainResolver.cs:58,71`; `BuildResolver.cs:101,115`; `LegionSupply.cs:49,56,71` — every call site treats it as a fungible scalar, none as a container |
| **No faucet or sink for legion-carried items exists anywhere** — no code path lets a legion pick up, hold, or hand off an item-shaped thing | Confirmed by the same grep that found zero item-shaped `WorldEntity` fields; there is nothing to cite because nothing exists |
| Legion-to-legion trade: **no mechanism exists** | Same as above — trade requires two parties each holding something to trade, and neither side of that exists yet |

This is the cleanest "real gap" of the five: no wiring gap to correct, because there is no inert item
field to wake up — the field itself does not exist.

**New real gap, named by the owner's own capacity-shape decision below and verified fresh this
session:** `WorldEntityMember` (`WorldState.cs:275-284`) — the record for one HoMM-style headcount
stack inside a legion (`InstanceId?`, `SpeciesId`, `Level`, `Hp`, `Wounds`, `Role`) — carries **no**
weight, slot, or carry-capacity field of any kind, confirmed by reading the whole record this session.
There is no seedsmith "unit" adapter either: `tools/seedsmith/seedsmith/adapters/` today holds `items`,
`demons`, `creatures`, `actions`, `effects`, and `_stub` — no `units`/`troops` directory — and the
`creatures` adapter's one `CREATURE` `KindSpec` (`tools/seedsmith/seedsmith/adapters/creatures/kinds.py:17-27`)
required/optional fields (`id,nameKey,name,side,gameTypeId` required;
`flavorInfo,flavorIntroduce,sunCost,cooldownSec,hp,attack,armor,armorMax,coverage,lineage` optional,
verified verbatim against the file this session) are PvZ lawn-card flavor/combat-stat fields — `side`
and `gameTypeId` are the lawn deploy shape, `hp`/`attack`/`armor`/`coverage` mirror vanilla plant/zombie
stats — not empire troop logistics. A repo-wide grep for `carryWeight`/`backslot` (any casing) returns
zero hits anywhere in the repo. **This confirms the owner's own claim precisely: no per-unit carry-weight
or per-unit slot-count stat exists on any troop record today, and no generator pipeline produces one.**
Building legion cargo per the owner's decision below therefore requires two new, sequenced pieces of
work, named here as their own gap rather than assumed solvable inside this program: (1) a new per-unit
stat pair (carry weight, "backslot" count) on the troop-stat side, source TBD (a tuning table keyed by
species/role, or a new seedsmith-authored field) and (2) a **new seedsmith unit/troop-stat adapter or
an extension of the existing `creatures` adapter** to author it, once a brief exists — today's
`creatures` adapter has no `item`/`action`-shaped kind by design (`kinds.py:4-7`: *"a creature 'item'
here would be a different thing from a real item"*), so it does not obviously extend to a logistics
stat and this is not a five-minute wiring fix. Named here as a real gap and a future dependency; not
designed further, per the owner's own "we cannot design everything" scoping.

**Added (strengthen pass 2026-09-13, finding F5) — no named fate for legion-carried cargo when the
legion is destroyed.** Legion cargo (relics included) has nowhere to go if the carrying `WorldEntity`
is wiped on the world map or in a siege. This contradicts the sibling `deployment-hierarchy` program's
own just-established principle that every dead end resolves to a named fate, never limbo — see its
"Dead ends with named fates" finding (`deployment-hierarchy-ideal.md:243`: *"a cache whose place
changes state gets a fate, never limbo"*). See Open question #7 below.

### Sector scope — split by bucket (strengthen pass 2026-09-13, finding F17)

**Corrected taxonomy.** A first draft blended this scope's two halves under one invented "PARTIAL"
label, outside the idea-phase skill's closed three-word bucket vocabulary (Built / Wiring gap / Real
gap). The two halves are cleanly separable — loam capacity is fully **Built**; item capacity is a
plain **Real gap** — so they are split explicitly below instead of blended into a fourth label.

**Built — loam capacity, fully working:**

| Finding | Evidence |
|---|---|
| A structure of `Kind = Storage` raises a sector's **loam** capacity via `CapacityBonus`, read by `LoamPhases.EffectiveCapacity` | `StructureCatalog.cs:14-15` (`Storage` kind doc comment: *"Raises capacity — a real third thing a structure can do"*); `StructureCatalog.cs:67` (`CapacityBonus` field, doc comment: *"Read only by `Storage`-kind structures — how much a granary raises a sector's cap by"*); `LoamPhases.cs:66-79` (`EffectiveCapacity` sums `CapacityBonus` from every active `Storage`-kind slot) |
| `WorldSector` carries **three** scalar stock fields today (`LoamStock`, `RubbleStock`, `IronworkStock`) — an established "a new resource is a new scalar field" pattern, but **zero** of them are item-shaped, and there is no generic item-capable field at all | `WorldState.cs:148-244` — the entire record; grep for anything item/instance-shaped on `WorldSector` returns nothing |
| **`CapacityBonus` is read by exactly one consumer (`LoamPhases.EffectiveCapacity`) and nothing else** — it is not a generic "this structure raises some capacity" field; it is hard-wired to loam specifically | `LoamPhases.cs:76`: `if (structure.Kind == StructureKind.Storage) bonus += structure.CapacityBonus;` inside a function whose only caller path feeds `LoamPhases.Production`'s loam math |
| **This is a wiring-shaped fact, not a real-gap-shaped one, about the *mechanism* of "a building extends a scope's capacity"** — the mechanism (Storage-kind structure → capacity bonus → `EffectiveCapacity`) is proven and shipping; it is simply denominated in loam only. Generalizing it to items is new plumbing (a second capacity axis, or a second `CapacityBonus`-shaped field), not a new architectural idea | `LoamPhases.cs:66-79` read in full this session |

**Real gap — item capacity, does not exist:**

| Finding | Evidence |
|---|---|
| **No item-shaped store exists at sector scope at all** — no table, no field, no lock/overlay row naming a sector as a holder of anything but loam/rubble/ironwork/recruits | Confirmed by the same `WorldState.cs` read; this is the actual real gap |
| **A related but distinct real gap already named elsewhere, checked and confirmed NOT the same gap:** `corpse-cache`'s own map explicitly defers "world-map and siege" death-loot caching as out of scope, tracked as its own Wave-3 note | `deployment-hierarchy-map.md:94-97`: *"World-map and siege are explicitly not external dependencies of this map. Their death paths do not exist yet (real gap)... tracked in the ideal doc's Wave 3 note."* This is a **death/loot-cache** gap (an item ends up on the ground when something dies on the world map), not a **standing storage building** gap (a sector holds items on purpose because a player built somewhere to put them). Related — both are "world map has no item container" — but not the same feature and not the same trigger. |
| Sector-to-sector item movement via trade routes: **no mechanism, and no spec exists** | Grepped `docs/` for "trade route"/"trading route": the only hit is `base-defense-ideal.md:2126-2129`, which is **prior-art research about Stellaris**, not a Rise of Summoner spec. There is no `trading-program` doc, map, or ideal anywhere in the repo today. |

---

## Prior art

| Source | What transfers | Failure mode to avoid |
|---|---|---|
| **Anno 1800 — warehouse tiers and cargo depots** ([Warehouse wiki](https://anno1800.fandom.com/wiki/Warehouse)) | A settlement's storage is **not** the empire pool — it is local, and a dedicated building (the warehouse, upgradeable up to three times in the Old World) is what grants it capacity at all, with **separate cargo depots adding a further +20/+40/+60 tons** on top. This is the closest real-game analog to "a sector needs a storage building to have slots at all, and a bigger/second building extends it further" — the exact shape the owner described, with concrete numbers confirming tiered, additive capacity growth is a proven convention, not a novel ask. | Not directly documented as a failure in the sources found this session; flagged as unverified beyond the cited wiki page, not fabricated. |
| **Total War — baggage train vs. settlement supply warehouse** ([Supply Warehouse wiki](https://totalwar.fandom.com/wiki/Supply_Warehouse)) | Two **structurally distinct** logistics scopes ship side by side in the same game: a **baggage train attaches to an army** (the legion-carries-its-own-cargo shape — it grants supplies for a limited duration and exists specifically so a force *away* from home ground is not helpless) while a **supply warehouse is a settlement building** that boosts replenishment for forces *at home* only. Neither substitutes for the other. This is a real, shipped precedent for exactly the owner's legion-scope/sector-scope split being two different mechanisms serving two different situations, not one generalized "storage" concept wearing two skins. | Not directly documented as a failure in the sources found this session for this specific split; the two systems are described as complementary, not competing, which is itself the useful data point — this repo's legion cargo and sector cargo should be equally non-competing, not a single shared limit split two ways. |
| **Stellaris — routed trade removed in patch 4.0** (already researched in-repo, `base-defense-ideal.md:2126-2129`, re-confirmed this session, not re-searched) | Paradox shipped a full routed-trade-with-piracy system for **six years** and then deleted it, replacing it with flat distance-and-position-priced upkeep and **no routing UI at all**. This is a real, sourced, six-year-lived failure mode directly on point for the owner's own explicitly-deferred "trade route" mechanic — strong evidence the owner is right to defer it rather than design it now, and a specific warning for whoever eventually *does* design it: a routing/convoy UI is a maintenance and complexity sink even for a studio with vastly more resources than this project, and the fallback that survived is a flat rate, not a fancier router. | **This is the failure mode itself** — a UI-visible trade-route/convoy system is the thing that got cut. Directly actionable: when the trading program is eventually designed, price sector-to-sector movement as a flat, distance-priced upkeep/rate first, and treat a visible routing UI as something to earn only if the flat version proves insufficient — never the starting design. |
| **This repo's own `loot-pack` spec** (in-repo, not external, listed here because it is genre-adjacent prior art for "a capacity-bounded scope that is not the empire stash") | Diablo-2-style fixed grid, footprint-derived, never authored on the item — already the chosen shape for the delve-party scope. Directly reusable as a **candidate** shape for legion cargo capacity if the owner ever wants legion capacity to be structural/grid-like rather than a flat scalar limit; not decided which one legion should use (§Open questions). | `spec-loot-pack.md` §8 already names and rejects the alternative of scaling a grid with `Θ` — the same warning applies to any capacity number this hierarchy ever introduces. |

Three real, sourced external precedents (Anno 1800, Total War, Stellaris) all point the same direction
the owner's own framing already took: legion-carries and sector-storage are two distinct, non-competing
mechanisms (Total War), a storage building is what grants sector capacity at all and further buildings
extend it (Anno 1800), and a routed trade-network UI is a proven, six-year-lived failure mode to defer
rather than design prematurely (Stellaris) — exactly what the owner already did by pushing "trading
route" to a future program.

---

## The shape

**One proposal, not several competing ones**, because the codebase already answers this question
twice (empire↔unique-actor via assignment, empire↔delve-party via the pack's lock overlay) and both
answers agree:

> **Ownership of a rolled item or a fungible stack has exactly one root, always: `rpg_item.player_id`
> for a unique instance, `rpg_item_stock` for a fungible stack — both already player-keyed, already
> empire-wide, already built. A "scope" is never a second ownership table. A scope is a reachability +
> capacity overlay that points at the same root row and is layered on top of it — the same "move,
> never copy" discipline `corpse-cache` proves for death and `loot-pack` proves for a delve run.**

Concretely, every scope in the owner's hierarchy decomposes into the same two questions, answered
differently per scope:

| Scope | **Reachability** — who can act on the item | **Capacity** — what bounds how much |
|---|---|---|
| Empire | Anyone with the matching `player_id`, from any context | None — by design (`ssot-inventory.md:670`); abuse-guarded only |
| Unique actor | The one specimen (or Dave) named by the assignment row's key | The closed 15-role enumeration (`ItemRole.cs`) — a fixed *set of slots*, not a grid |
| Delve party | The party at that `(delveId, partyIndex)`, only for the run's duration | A structural grid, footprint-derived (`spec-loot-pack.md` §1-2) |
| **Legion (RESOLVED)** | The legion named by `EntityId`, wherever it stands or marches | **List of item stacks/slots, gated by both a slot count AND a total weight limit** — owner's exact framing, quoted in full under Open question #1 below. Neither a pure scalar (`CarriedLoam`) nor a full footprint grid (`loot-pack`'s `PackGrid`) — a fourth capacity shape, distinct from all three candidates this doc originally offered. The legion's own totals are an **aggregate of its troops** (Σ `carryWeight × count`, Σ `backslot × count` across `Members`), never a per-unit-visible value — see the new real gap named above for why per-unit values cannot exist yet. **Typed (strengthen pass 2026-09-13, finding F10):** `carryWeight`, `backslot`, and the legion's own aggregate slot-count/weight totals are all `long`, per `CLAUDE.md`'s "Numeric overflow" table (`long` is the default for any magnitude) — never `int`/`float`. |
| **Sector (RESOLVED)** | Any legion physically at that sector (`AtSectorId` match), while a Storage-capable structure exists there | **Zero until a storage building exists**, then a **new, distinct structure-kind/field for item storage — NOT a generalization of `CapacityBonus`/`EffectiveCapacity`.** This doc's own prior recommendation (a second additive field mirroring the loam mechanism) is now known to be **wrong**; the owner chose separation instead. See Open question #2 below for the corrected reasoning. **Typed (strengthen pass 2026-09-13, finding F10):** sector item-storage capacity is `long`, matching `CapacityBonus`'s own type and the same overflow-table rule. |

**CORRECTED (2026-09-13) — this doc's own prior recommendation below was overturned by the owner.**
The paragraph that follows was this session's original proposal (generalize `CapacityBonus` as a second
field). The owner's decision for Open question #2 is the opposite: sector item storage is **"a new,
distinct structure concept for items,"** not a reuse of the loam-capacity precedent. Kept here, struck
through in spirit rather than deleted, so the reasoning trail shows what was proposed and why it did not
survive contact with the owner's actual intent — see Open question #2 for the corrected shape and this
session's own inference about *why* the owner likely wanted separation.

~~**Why a second capacity axis on `StructureDef`, not a new `StructureKind`.** `Storage` already means
"raises a capacity"; the question is *which* capacity. The loam precedent (`CapacityBonus`) is
consumed by exactly one reader (`LoamPhases.EffectiveCapacity`). The narrower, more consistent
extension is a second field (e.g. an `ItemCapacityBonus`-shaped counterpart) read by a *new* sector-cargo
capacity function that mirrors `EffectiveCapacity`'s shape exactly — not a second `StructureKind`, and
not overloading `CapacityBonus` itself to mean two different units. This mirrors how the loam-relics
sibling doc treats `StructureDef` extension for Wonders: additive new fields on the same catalog row,
never a parallel structure system.~~

**Why legion-to-legion trade and legion deposit/withdraw need no new ownership concept.** Both are
just a move of the same overlay pointer from one scope's holder key to another's — legion A's cargo
pointer to legion B's cargo pointer (trade), or a legion's cargo pointer to a sector's cargo pointer
and back (deposit/withdraw). The underlying `rpg_item`/`rpg_item_stock` row's `player_id` need not
even change if both legions belong to the same player, which is the common case — the only case that
resolves end-to-end today. **Correction (strengthen pass 2026-09-13, finding F1): it does not "also
change player entirely" yet.** The owner's intent to allow cross-faction transfer someday is real and
the schema (string `FactionId`, not the closed `CommanderId` enum) is chosen so it doesn't block that
future — but verified this session, `rpg_world_factions` carries no `player_id` at all and
`WorldFactionKind`'s AI members live inside the single player's own world, so there is no second real
`player_id` to move ownership to today. A `player_id` change across two genuinely different players'
rows stays hypothetical until a multi-empire program gives a second faction its own item-owning
player row — see Open question #3 (corrected) and #6 below.

**Alternative considered and rejected: a second, generic "container" table used by every scope
uniformly (empire, legion, sector, party) instead of scope-specific overlay tables.** Rejected because
it does not match how this codebase already treats "capacity": the unique-actor scope's capacity is a
*closed enumeration*, not a grid, and the delve-party scope's capacity is *derived from role/mass-class
footprint*, not authored per container. A single generic container abstraction would force one of
those two already-shipped shapes to bend toward the other, or invent a third shape neither currently
uses. The overlay-on-a-shared-root pattern already generalizes cleanly without forcing this; a uniform
container table would not.

---

## Tunables

None of these are decided by this doc — named so a future spec knows where they land, not what they
equal:

| Tunable | Home |
|---|---|
| Per-unit `carryWeight` and `backslot` (slot count), by species/role — new, per the legion-cargo decision (Open question #1, RESOLVED); both **`long`** (strengthen pass 2026-09-13, finding F10 — per `CLAUDE.md`'s overflow table, never `int`/`float`) | Not yet named — this is the new per-unit troop-stat gap called out under §Legion scope; home file TBD once the stat itself and its authoring path (tuning table vs. seedsmith-authored field) are decided |
| Legion cargo's slot-count cap and weight-limit cap (the two gates on the aggregate), both **`long`** (F10) | `data/tuning/world.v{n}.json` or a `loam-legions` sibling file, beside `CarriedLoam`'s own tuning — shape now RESOLVED (Open question #1), exact numbers still undecided |
| Sector item-capacity numbers for the new, distinct item-storage structure concept (Open question #2, RESOLVED as its own structure, NOT `CapacityBonus`), typed **`long`** (F10) | A new section/key, sibling to but separate from `structures.*.capacityBonus` (loam's own tuning) — exact shape (slot/weight vs. something else) still undecided |
| Any deposit/withdraw transfer rate or turn cost | Not yet named — depends entirely on the deposit/withdraw mechanics this doc explicitly declines to design |
| Legion-to-legion or legion-to-sector trade cost/tax, if any | Same — a future spec's decision |
| Any capacity number's soft-cap/overflow behavior | Must follow `LoamPhases.Production`'s own `loam.overflow:`-report pattern (`LoamPhases.cs:43-44`) or the pack's own refusal pattern (`spec-loot-pack.md` §9) — never a silent clamp, per `AGENTS.md`'s no-hard-ceilings rule |

---

## What this deliberately does not decide

Per the owner's own scoping instruction ("we cannot design everything, just focus on what we already
have first"), this doc is deliberately silent on:

- **Deposit/withdraw UX** — what the player clicks, whether it costs a turn, whether it is instant or
  queued. Not designed here at all, even though ship-together (Open question #5, RESOLVED) means it
  ships in the same program as legion cargo and sector storage — sequencing in one program is not the
  same as designing the mechanic, and this doc still does neither.
- **Trade mechanics between two legions (same empire)** — barter, gift, a market, or a flat transfer.
  Not designed; only the underlying "move the overlay pointer" primitive is named as sufficient
  plumbing for whichever mechanic is eventually chosen.
- **Cross-faction trade's fairness/consent mechanism** — the schema is chosen so a future multi-empire
  program isn't blocked (Open question #3, corrected), but the transfer mechanism itself is a real gap
  today (no second `player_id` exists to trade with) and the consent/offer flow is explicitly not
  designed here regardless — see Open question #6 (narrowed).
- **Cross-sector trade routes** — explicitly deferred by the owner to a future, separate trading
  program. This doc names the *eventual consumer* of sector-to-sector item movement (a sector cargo
  overlay that a trade route can move rows between) but does not design the route, the UI, the cost, or
  the failure-mode mitigations Stellaris's own removal (§Prior art) suggests are worth reading before
  that program starts.
- **Legion cargo's and sector storage's exact numbers** — the *shapes* are now RESOLVED (Open questions
  #1 and #2: slot+weight aggregate for legion, a new distinct item-storage structure concept for
  sector), but no slot count, weight limit, or building tier number is decided here.
- **The exact new field(s)/table(s)** for legion cargo, the new sector item-storage structure, and the
  per-unit troop-stat gap named under §Legion scope — their SQL shape, or which file in `RpgStore`
  gains the new tables. §The shape names the pattern (overlay on the same root, mirroring
  `rpg_delve_pack_lock`); it does not write the schema.
- **Whether relics specifically are items (`rpg_item`-shaped) or a scalar world-stock resource** — that
  is the sibling `loam-relics-and-wonders-ideal.md` doc's own open question (§Open questions #2 there);
  this doc's hierarchy supports either answer without needing to resolve it.
- **The seedsmith unit/troop-stat adapter itself** — named as a real gap and future dependency under
  §Legion scope, not designed or scoped further here.

---

## Open questions — owner decisions only

1. **Legion cargo's capacity shape — RESOLVED 2026-09-13.** Owner, verbatim: *"legion use list/slot/
   and weight, like empire scope but it have weight and slot limit. every unit have carry weight and
   backslot that cannot view because troop stack is not unique demon. we will add to seedsmith unit
   pipeline later (current we don't have seedsmith unit generator right?)"*
   None of the three original candidates (flat scalar, structural grid, uncapped) — a **fourth shape**:
   a list of item stacks/slots, gated by both a **slot count** and a **total weight** limit
   simultaneously. The numbers are an **aggregate over the legion's own troops**: every individual unit
   has its own carry-weight and "backslot" (per-unit slot count), but because troops are HoMM-style
   headcount (`WorldEntityMember`, not a `UniqueActor` row), no per-unit value can ever be displayed or
   assigned — only the legion's **total** (Σ `carryWeight × count`, Σ `backslot × count` across
   `Members`) is real. **Verified fresh this session, confirming the owner's own claim:** no such
   per-unit field exists on `WorldEntityMember` today (`WorldState.cs:275-284` — full record read, only
   `InstanceId`, `SpeciesId`, `Level`, `Hp`, `Wounds`, `Role`), and no seedsmith "unit" pipeline exists —
   `tools/seedsmith/seedsmith/adapters/` has `items`, `demons`, `creatures`, `actions`, `effects`,
   `_stub`, nothing named `units`/`troops`; the `creatures` adapter's one `CREATURE` `KindSpec`
   (`tools/seedsmith/seedsmith/adapters/creatures/kinds.py:17-27`) required/optional fields
   (`id,nameKey,name,side,gameTypeId` / `flavorInfo,flavorIntroduce,sunCost,cooldownSec,hp,attack,
   armor,armorMax,coverage,lineage`) are PvZ lawn-card flavor/combat-stat fields, not empire troop
   logistics. **The owner's own answer to their own parenthetical question is correct: there is
   genuinely no seedsmith unit generator today.** This is real, deferred work — a new per-unit
   troop-stat pair plus a new/extended seedsmith adapter — named as its own real gap under §Legion
   scope, not solved by this doc or by the sibling Wonder doc.
2. **Sector storage capacity shape — RESOLVED 2026-09-13.** Owner, verbatim: *"A new, distinct
   structure concept for items."* **Not** a generalization of the existing `CapacityBonus`/
   `LoamPhases.EffectiveCapacity` mechanism — this doc's own §The shape originally recommended exactly
   that reuse (struck through in place above, not deleted); the owner chose separation instead.
   **This session's own inference for why (explicitly inference, not stated by the owner):** loam
   capacity is a single scalar the whole sector shares uniformly — `EffectiveCapacity` sums one number
   and `LoamStock` is one field (`WorldState.cs:148-244`). Item storage, per Open question #1's answer
   above, may need slot **and** weight semantics much closer to the legion-cargo shape — a bare
   `CapacityBonus: long` field cannot express two independent gates (slots, weight) the way a single
   scalar capacity number can for loam. A second structure concept leaves room for that richer shape
   without overloading a field designed for one unit (loam) into meaning two.
3. **Cross-faction transfer — CORRECTED (strengthen pass 2026-09-13, finding F1): the schema choice
   is resolved, "buildable" was an overclaim.** Owner, verbatim (paraphrased from the framing
   decision): *"Allow cross-faction transfer now."* An initial read of this flagged a real concern —
   `CommanderId` is closed to exactly `{Dave, Zomboss}` (`CommanderId.cs:20-24`), and this is a
   single-player, no-account game (`the-game.md:67`), so "trade with a different player" looked like
   it might not describe anything real. **The owner corrected this in conversation, and the schema
   reading checks out against code:** the world-map's actual faction concept is
   **`WorldFaction`/`OwnerFactionId`/`FactionId`**, plain `string` fields throughout
   (`FactionIntel.cs:12,78,124`, `RaiseResolver.cs:62,124,128`, `IWorldView.cs:17,95-103`) — never tied
   to the closed `CommanderId` enum — with `world.Factions` already an **iterable collection**, not a
   hardcoded pair, and `ZoneOfControl.IsHostile(factionA, factionB)` already modeling faction
   relationships generically. `CommanderId`'s own doc comment even says *"for now only have 2 of them
   **for lawn run**"* — a battle/resource-pool-scoped identity, narrower than the world-map's own
   faction model. The owner's stated intent: *"my architect already allow new empire, we just don't
   really build them because they need serious program for multiple empire gameplay mechanism"* — i.e.
   a third+ empire is a real, unbuilt, but architecturally unblocked future addition, and this doc's
   schema should key ownership on the **string `FactionId`/`player_id` model**, never on `CommanderId`,
   so it does not close a door the rest of the world-map deliberately left open. **That much still
   holds and is genuinely resolved.**

   **Correction — "verified buildable" overclaimed, struck.** Verified fresh this session:
   `rpg_world_factions`'s schema (`RpgStore.World.cs:37-44`) has **no `player_id` column at all** —
   its primary key is `(world_id, faction_id)` with `kind`/`name`/`policy_id`, nothing that resolves a
   faction to an item-owning player. `rpg_worlds` (`RpgStore.World.cs:20-22`) carries exactly **one**
   `player_id` for the entire world. And `WorldFactionKind`'s members (`Player`, `Zomboss`, `Clan`,
   `Rival`, `Wild`) are AI-controlled sub-actors living *inside* that one player's own `WorldState` —
   not separate players who each own an `rpg_item.player_id`-keyed stash. So there is no real, existing
   path today from a `FactionId` string to a distinct `player_id` that a cross-faction item transfer
   could actually move ownership to. The *relationship* model (hostility, iteration over
   `world.Factions`) genuinely does generalize to a future multi-empire program; "cross-faction item
   transfer" as a mechanism a player could use today does not — it is a **real gap**, not a resolved
   decision. **The only sensible near-term shape, given this:** same-empire legion↔legion and
   legion↔sector movement (within the single `Player`-faction's own holdings). Trading with the
   AI-controlled `Zomboss`/`Clan`/`Rival` factions does not make sense as a player-facing feature
   regardless of schema readiness, since they are not separate players with their own stash to trade
   from. A genuine multi-empire program would first need to give each empire/faction its own
   `player_id`-rooted item store before cross-faction trade could mean anything real — that is the
   actual prerequisite, not a consent/offer flow layered on top of what exists today.

   The two sub-requirements originally named below still apply **once a real second player/empire
   exists to trade with** — they are not wrong, only premature relative to the gap just named:
   - **An atomicity/fairness guarantee** — both sides' state changes together, or neither does. The
     real, verified precedent for this discipline in this repo is **`corpse-cache`'s** own "move never
     copy" rule: *"A real death moves, never copies: after `RetireUniqueActorUnlocked` commits, the
     specimen's `rpg_item_assignment` rows are gone and an equal number of `rpg_corpse_cache_item` rows
     exist"* (`spec-corpse-cache.md:240-241`) — a single-transaction move of the same underlying row,
     never a copy that could leave two owners believing they hold the same item. **Correction to this
     task's own initial framing:** `PartyPoolsCarry` (`src/FusionRpg.Core/Delve/Attrition/
     PartyPoolsCarry.cs`) was suggested as a second precedent alongside corpse-cache; read in full this
     session, it does **not** apply — it is a pure split/rebuild of a battle actor's six resource pools
     (hp + five others) into and out of one room's battle, with no item ownership, no cross-owner row,
     and no "move never copy" claim anywhere in it. Cross-faction item-trade atomicity should cite
     `corpse-cache` (and `loot-pack`'s own "acquired at placement" / extraction-moves-rows pattern,
     already cited above in §What already exists) as its precedents, not `PartyPoolsCarry`.
   - **A consent/offer notion** — a trade needs both parties' agreement, not a unilateral push. Nothing
     in the codebase today models a multi-party offer/accept exchange for items; this is new.
   Per the owner's own "we cannot design everything" instruction, **this doc does not design either
   mechanism** — see the new Open question below, which names this explicitly as follow-up design work
   still owed before cross-faction trade can be built, distinct from "the scopes can exist same-empire-
   only today."
4. **Which loop unlocks sector cargo — still open, unaffected by this pass.** Item collection is gated
   behind a Dave-level chapter today (`the-loops.md:49-53`: *"Drops before that chapter either do not
   roll or bank until the armoury opens"*). Does sector cargo/legion cargo exist before that chapter
   (world-map buildings and legions are Shipped/WIP independent of the armoury gate), or does it wait
   for the same unlock? Not decided anywhere today; none of the four resolutions above bear on it.
5. **Whether this hierarchy is one program or several — RESOLVED 2026-09-13.** Owner, verbatim: *"Ship
   the whole hierarchy together."* Legion cargo, sector storage, and deposit/withdraw all ship in
   **one program** — not sector-storage-first, which this doc had originally recommended (it would have
   unblocked the relic/Wonder feature soonest). The owner's call overrides that recommendation; nothing
   in §The shape required either sequencing, so this is a pure scoping decision, not a correction of an
   architectural claim.
6. **NEW — cross-faction trade's fairness/consent mechanism (narrowed, strengthen pass 2026-09-13,
   finding F18).** Open question #3 (corrected above) resolves the *schema* — key ownership on
   `FactionId`, not `CommanderId` — but not a buildable transfer mechanism, since no second real
   `player_id` exists to trade with today. This question is therefore scoped to **"once a genuine
   second empire/player exists"**, not to anything buildable in this program's first wave. Splitting
   the two sub-questions the first draft bundled together:
   - **(a) The atomicity primitive — effectively decided, not actually open.** A single DB transaction
     across both scopes' overlay tables, following `corpse-cache`'s own "move never copy" discipline
     (*"after `RetireUniqueActorUnlocked` commits, the specimen's `rpg_item_assignment` rows are gone
     and an equal number of `rpg_corpse_cache_item` rows exist"*, `spec-corpse-cache.md:240-241`) is
     the clearly correct shape for any ownership-changing move this hierarchy makes, same-empire or
     cross-faction alike — there is no real design question here, only an implementation task once a
     second player row exists.
   - **(b) The consent/offer flow — the actual open question.** Does a cross-faction trade require an
     explicit accept from the receiving player, an escrow step, a time-limited offer, or something
     else? Nothing in the codebase today models a multi-party offer/accept exchange for items. This
     stays genuinely open, and — per the correction above — is not answerable in isolation from the
     multi-empire program itself, since "the receiving player" does not exist as a distinct row yet.
7. **Destroyed-legion cargo's fate — RESOLVED 2026-09-13 (/spec kickoff).** Owner: cargo drops as a
   **revisit-lootable cache**, mirroring `corpse-cache`'s own pattern (a cache pinned to the legion's
   last sector, decaying on the world-stage turn clock, `deployment-hierarchy-ideal.md` §Corpse-drop;
   shape reused from `spec-corpse-cache.md`). This is the same mechanism this session already built for
   dying specimens, extended to a dying legion's cargo — not a new mechanism, an additional trigger onto
   an existing one. `/spec` should model this as `corpse-cache`'s `place_kind`/`place_ref` keying
   extended to accept a legion's last-known sector as a valid place, not a parallel cache table.
8. **Sector storage on capture — RESOLVED 2026-09-13 (/spec kickoff).** Owner: stored items **transfer
   to the new owner** on capture. This is the opposite of the nearest existing precedent
   (`ClaimResolver.cs:107`'s Warden-binding capture handling, which clears outright with no transfer) —
   `/spec` must build new ownership-transfer logic for sector storage specifically, not reuse the Warden
   capture path. Note this transfer changes `rpg_item.player_id` (or the stock equivalent) across a
   faction boundary at the moment of capture — the same "move never copy," single-transaction discipline
   `corpse-cache` already established applies here too, even though this is not the cross-faction
   *trade* mechanism (question #6b below) — capture-transfer is involuntary and doesn't need a
   consent/offer flow, only an atomic ownership move.
9. **Sector item storage slot-bound or sector-wide — RESOLVED 2026-09-13 (/spec kickoff).** Owner:
   **slot-bound** — the new structure competes with Well/Extractor/Granary for the sector's limited
   `WorldSlot`s, same as every other structure (`WorldState.cs:119`). Capacity **sums additively across
   multiple storage structures**, per this doc's own recommendation and precedent (`CapacityBonus`/
   `EffectiveCapacity`'s existing additive discipline, `LoamPhases.cs:66-79`; Anno 1800's warehouse +
   cargo-depot prior art, §Prior art above).

## Handoff

**Added (strengthen pass 2026-09-13, finding F16).** Written: `docs/architecture/scoped-inventory-hierarchy-ideal.md`
(this file). Next step: `/spec` for a capability map + module specs, once the remaining open questions
above (#4, #6, #7, #8, #9) are answered by the owner — `loam-relics-wonders`' own Wonder-building flow
is blocked on this program's build, per `empire-development-map.md`'s dependency edge. Do not write
specs, plans, or code from this doc alone.

## Corrections log — strengthen pass 2026-09-13

Four independent lenses (mechanism soundness · cross-program/economy · hard-rule compliance ·
spec-quality-vs-bar) ran this session. See the four lens reports this session for the full
checked-and-held list; refuted probes are not repeated here since they were not handed to this pass.

| # | Lens | Finding | Disposition |
|---|---|---|---|
| F1 | cross-program/economy | Q3's "RESOLVED... verified buildable" overclaimed: `rpg_world_factions` has no `player_id`, `rpg_worlds` carries exactly one `player_id` per world, and `WorldFactionKind` members are AI sub-actors inside that one player's world, not separate item-owning players | Corrected in place: schema choice (`FactionId`, not `CommanderId`) stays resolved; cross-faction transfer as a buildable mechanism downgraded to a real gap; near-term shape is same-empire legion↔legion/legion↔sector only |
| F5 | mechanism soundness | Legion-carried cargo has no named fate if the carrying legion is destroyed — contradicts `deployment-hierarchy`'s own "named fates, never limbo" principle | Added as new real gap + Open question #7, three candidates named, none picked |
| F6 | mechanism soundness | Sector-storage reachability never gates on the withdrawing legion's faction matching the sector's current owner; sectors can be captured (`SectorPhase.Contested`/`Besieged`/`Lost`) | Added as Open question #8; `ClaimResolver.cs`'s `WardenBindingId = null` on capture cited as the nearest analog, not a decision |
| F7/F8 | spec-quality-vs-bar | Sector item-storage's "new, distinct structure concept" was never reconciled against `WorldSlot.StructureId`'s one-structure-per-slot model | Added as Open question #9; additive-capacity-if-slot-bound recommended (mirrors loam's `CapacityBonus` and the doc's own Anno 1800 prior art), flagged as owed at spec time |
| F10 | hard-rule compliance | New magnitudes (`carryWeight`/`backslot`, legion cargo caps, sector storage capacity) had no explicit numeric type | Typed `long` throughout, per `CLAUDE.md`'s overflow table, at every first-introduction point in §The shape and §Tunables |
| F12 | spec-quality-vs-bar | No `## Which loop this extends` heading — folded into Step 0 prose, the idea-phase skill's own named red flag | Promoted to its own top-level heading; content unchanged |
| F14 | spec-quality-vs-bar | Update banner claimed "four of five resolved," but resolving Q3 spawned a new, equally-open Q6 | Corrected banner to state the honest count and name Q6 |
| F16 | spec-quality-vs-bar | No `## Handoff` section per the idea-phase skill's Step 5 | Added, naming the written path and the `/spec` next step |
| F17 | spec-quality-vs-bar | Section headers invented labels outside the skill's closed Built/Wiring gap/Real gap vocabulary ("a fourth state, named precisely"; "PARTIAL") | Delve-party inventory reframed as Real gap with a prose nuance note; Sector scope split explicitly into its Built (loam) and Real gap (items) halves |
| F18 | spec-quality-vs-bar | Open question #6 bundled an already-obvious atomicity primitive with a genuinely open consent/offer question | Split: atomicity (single-transaction move-never-copy, `corpse-cache` precedent) marked effectively decided; question narrowed to consent/offer flow only |
