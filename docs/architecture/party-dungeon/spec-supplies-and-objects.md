# Spec: supplies-and-objects

Status: **APPROVED by the owner 2026-09-05 (wave 3) — written against shipped code; unbuilt.** Every `file:line` below was
opened this session; where the brief, the map row or an earlier spec drifts from the code, the drift is named
in place. Every number is a starting shape so the system runs, never a balance decision.

Module id `supplies-and-objects`, row 12 of the [party-dungeon map](../party-dungeon-map.md) (wave 3, last of
the wave). Depends on `event-deck` (curio rows, outcome draw, `HoldsStock` override, four new leaves),
`loot-pack` (cells, `pack.drop`, the per-party stock a supply lives in), `delve-attrition` (`RestResolver`,
pools, `Downed`), `delve-graph-roll` (`gated` doors, `keyForLaneId`), `delve-scope` (`key_for_lane_id`,
`AppendDecision`, `LaneGate`), `dungeon-registries` (`interaction-verbs.v1.json`, `override-tags.v1.json`,
`rest.*`, `merchant.markupMilli`, `pack.*`), `dungeon-seed-contract` (§1.7). **External, gating:** the
`consumable` `ContainerKind` (D27 — `ConsumableDef.cs:201` `ConsumableContainerKindAvailable = false`) and the
item-cost row on actions (A3 — `ActionCostRow` is resource ids only, `ActionRow.cs:122-123`). **External, not
gating:** base-defense `structure-schema`'s 18th field `interaction`. Prices are `dungeon-loot`'s
`SoulSinkPolicy.Price` read; placement is `loot-pack`'s — neither spec existed when this was written, so both
are interfaces here. Format precedent: [standalone/spec-expeditions.md](../standalone/spec-expeditions.md).

## Objective

The runtime halves of two seed families. **Supplies:** a consumable anchor plus its §1.7 extension becomes one
concrete supply per player through `Instantiator.TryInstantiate` at the room's `Θ` when it drops or is bought
— never a second roll — and is used in exactly three delve contexts (`rest`, `curio`, `battle`), each use a
decision-log entry, each pool change a `resource.delta`. **Objects:** a room's interactive things — curio,
shut door, altar, merchant, rest fire, cage — are one read model (`RoomObject`) with registry verbs, a
closed-leaf requirement predicate, and an outcome that is always somebody else's number: a container roll, a
`dungeon-loot` price, a `loot-pack` placement, an `event-deck` draw, a `CombatantKind.Structure` fight, or
`RestResolver`. Success: a ration bought at the merchant and eaten at the rest row reproduces over
`(container, revision, seed, Θ)`; a gated door opens with its key or breaks, never a third way; a revive lifts
a `Downed` member and nobody else; battle, expedition and world goldens do not move.

## Locked anchors (quoted, not paraphrased)

- **Instantiator law** (ideal §10 laws 1–2, `party-dungeon-ideal.md:781-796`): *"Seedsmith emits seeds
  offline — enums, ordinals, registry ids, free text, never a magnitude, weight, probability or quantity …
  `Instantiator.TryInstantiate` … is the shared SDK for anything that is a container of atoms … Never a second
  roll implementation beside it."* *"A graph, a deck draw or a slot fill is not an atom container … every
  number inside the thing it picked still enters through `Instantiator`, `LootPipeline` or `BattleRuleset`."*
- **§11.5, supplies** (`:1289-1300`): *"Supplies are the consumable anchor plus three fields. No seventh
  vocabulary … ration → `restore` feeding `hunger`; bandage → `restore` on hp; antidote → `utility` (status
  clear); key → `utility` (override only, no atoms); charm → `draught`; ward → `ward`; bait → `utility`.
  Torch-analog: none."*
- **§11.5, objects** (`:1259-1262`, `:1269-1275`): *"An object is a STRUCTURE if it is still there after you
  interact with it … a CURIO if the interaction is the whole of it — one seeded draw, then it is spent."* The
  `interaction` axis is *"not a second obstacle-verb list … each mapped onto an action from the corpus with a
  usability predicate from E3's twelve leaves."*
- **R3** (`:1750`): *"the revive lets it finish the run, not escape the rule."* **R5** (`:1751`): *"the
  offer floors at the altar pull price at the room's Θ via `SoulSinkPolicy` … Altar pulls are at-risk haul on
  the delve ledger."* — `dungeon-loot`'s and `wild-room`'s numbers; this module owns only the `pray` verb.
- **S2-6** (`audit-2026-09-05.md:216`): *"`useContext: rest` on rations"*; `spec-delve-attrition.md:150-152`.
- **Seed contract §1.7** (`spec-dungeon-seed-contract.md:121`): extension = `consumableRef` · `overrideTags[]`
  · `useContextAdds[]` ⊆ `rest · curio`; *"`sizeBand`, `stackBand` and price are DERIVED and never in any
  file. Objects: v1 has none as seed — curios are `events/` rows."* Ask-first (`:314`): *"a fourth
  `useContext` value"*, *"a new anchor kind"*.
- **OwnerKinds** (`definitions.md` §6, corrected 2026-09-05; `OwnerScope.cs:20-30`): eight, closed. This
  document wins over any spec: a supply's effect binds on `unique-actor:{instanceId}`; a room object's on
  `sector:{sectorId}` (a delve room *is* a `WorldSector`, decision 5). No `delve:` owner kind is minted.

## Design

### 1. Supply anchors → concrete supplies

A **supply anchor** is the shipped consumable row (`data/seed/items/consumables/k1.json:24-39`: `id · nameKey ·
name · classId · useContext[] · family · powerBand · manifestCost · tags`; 20 entries in `k1`, more in
`k2`/`k3`) joined at import to its §1.7 extension (`supplies/<id>.json`). Class vocabulary is
`ConsumableClass` (`ConsumableDef.cs:12-35`: `restore · draught · ward · board · revive · utility`); the
class → executor mapping of §11.5 is the *only* mapping and lives in one table, `SupplyClassMap`, read by
the validator: `restore` → `resource.delta` on a pool in `ResourceIds` (`DerivedStatChannels.cs:521`, six:
`hp · stamina · hunger · spirit · qi · poise`); `ward` → `shield.grant`; `revive` → `resource.delta hp` gated
on `Downed` (§3); `utility` → `status.clear` or **no atoms** (a key or bait is an override tag with nothing
to fire); `draught` → delve-scoped `stat.derived`; `board` → refused in a delve (no lawn).

A **concrete supply** is `Instantiator.TryInstantiate(container, lookupAtom, lookupAffix, rollSeed,
thetaContent: Θ_room, tuning, out instance, origin, catalogRevision)` (`Instantiator.cs:98-107`), called
**once**, when the supply enters a pack — a `cache`/curio drop (`dungeon-loot` mints), a merchant purchase
(§7), or provisioning at entry (`Θ_entrance`). `rollSeed` is the first `NextULong()` of
`SeededRng.DeriveStream(delveSeed, "dungeon:supply:{r}:{c}:{n}")` (`SeededRng.cs:26-27`), `n` the supply's
ordinal in the room's floor list; entry uses `dungeon:supply:entry:{n}`. Reproducibility is
`InstanceRow.ContentFingerprint()` (`:59-68`), which includes `ThetaContent` on purpose. **Nuance the brief
does not state:** a consumable container has no pool — `consumable.rolls` refuses one that declares rolls
(`ConsumableDef.cs:225`) — so `Draw` returns empty and the roll is the fixed core's `OnInstantiate` freeze with
`contentScale(Θ_room)` applied exactly once (`Instantiator.cs:120`). Still the one SDK: a ration's refill at
`Θ = 100` is larger than at `Θ = 20` by the ratio every drop reads. Nothing here reads `P(Θ)` directly.

`sizeBand`/`stackBand` are **DERIVED** at import from `pack.footprint.consumableClass.{classId}` and
`pack.stack.consumableClass.{classId}` (`spec-dungeon-registries.md:141`) — cells and a count, `loot-pack`'s
to place. Price is DERIVED: `dungeon-loot` computes `SoulSinkPolicy.Price(base, Θ_room, tuning)`
(`SoulSinkPolicy.cs:40-41`) from grade × class; this module never sees a soul number.

### 2. Use contexts — widening `UseContext`, every reader listed

**Drift, reported.** The brief, the map row and `spec-delve-attrition.md:150` say `UseContext` is *"Menu ·
Dispatch · Battle"*. It has **four** members: `Menu · Dispatch · Battle · Lawn` (`ConsumableDef.cs:43-58`).
The brief names the new members `Rest` and `Room`; the approved seed contract (`:121`) and map row 12 spell
them `rest` and `curio`. **The approved spelling wins:** the enum gains `Rest` and `Curio` (wire `rest`,
`curio`), appended after `Lawn` so declaration order — which `UseContextWire` sorts by (`:379-380`) — keeps
every existing wire string byte-identical. "Room" in the brief *is* `curio`: a use between fights, at an
object. Widening is *"additive and never invalidates a row"* (`:38-41`), and every reader below is touched:

| Reader | Line | Change |
|---|---|---|
| `UseContexts.All` / `Wire` / `TryParse` | `ConsumableDef.cs:96-119` | two members, two spellings |
| `UseContexts.RuntimesFor` | `:147-154` | `Rest → []`, `Curio → []` — neither is a combat runtime; a supply used at rest or at a curio resolves through the battle runtime's `OnActivate` grant path *outside a fight*, exactly as `Dispatch` maps to `RuntimeId.Battle` for the same reason (`:126-146`). The `Menu → []` precedent (`:150`) is the shape |
| `ConsumableValidator` host check | `ConsumableValidator.cs:113-124` | reads `tuning.Authors(ctx)`; message text gains the two contexts |
| `ConsumableValidator.ValidateRuntimes` | `:215-227` | unchanged — iterates `RuntimesFor` |
| `ConsumableTuning.ContextsAuthored` / `Authors` | `ConsumableTuning.cs:53, :66, :115-120` | `data/tuning/consumables.v1.json:10` `contextsAuthored` gains `"rest"`, `"curio"` (and `"battle"` — the action layer exists, `spec-delve-battle-profile.md`) |
| `ConsumableCatalog.GateManifest(entries, belt, context)` | `ConsumableCatalog.cs:110, :142-145` | the delve host calls it with `Rest` / `Curio` / `Battle` per use; **`Menu` is refused before the gate** (§8) |
| `ConsumableCorpus` parse · `RpgStore.Consumables` write/read | `ConsumableCorpus.cs:119-123` · `RpgStore.Consumables.cs:112, :303-306` | `TryParse`/`Wire` handle it; no edit |

The §1.7 `useContextAdds[]` is unioned onto the consumable row's own `useContext[]` at import, so a shipped
`menu` ration becomes `menu, rest` without editing `k1.json` — the extension is the only place delve context
is authored, and the seedsmith adapter's field set (`adapters/items/kinds.py:92-93`) is untouched.

### 3. Rest and battle uses — the attrition and battle seams

**Rest.** A `rest` room grants `rest.activations` uses per member (`spec-delve-attrition.md:190-193`). A
supply use at rest is one activation: `SupplyUse.Use(member, instance, UseContext.Rest, facts)` checks the
context (§2), `HoldsStock(containerId, 1)` over the **pack** (not `rpg_item_stock`, `RpgStore.Items.cs:96` —
the armoury stays home), then fires the container's `OnActivate` atoms (`AtomKindRegistry.cs:46-48`
`AllTriggers` admits it on `resource.delta`, `status.apply`, `shield.grant`; `stat.modify` per `:488-497`).
Every pool change is `resource.delta` → `EffectActions.ApplyResourceDelta` (`AtomCompiler.cs:283`) against the
member's `ActorResourcePools` (`ActorResourcePools.cs:13-25`), never a field write. `loot-pack` decrements the
stack; `AppendDecision` records `{kind: "supply.use", partyIndex, payload: {memberId, containerId, context}}`
(`spec-delve-battle-profile.md:141-145`; `spec-delve-scope.md:272`). Camp *actions* are attrition §5's: corpus
actions with `useContext: rest`, paid through `CostLedger.TryPay(actorKey, actionId, OnCommit, rng)`
(`CostLedger.cs:106`), competing for `LoadoutSet.MaxSize = 5` (`LoadoutSet.cs:40`).

**Antidotes — a wiring gap, named.** `status.clear` is on `AtomTriggers.Events` only
(`AtomKindRegistry.cs:638-646`; `Events` = the four board events, `AtomKind.cs:104`), so a `utility` antidote
cannot fire on `OnActivate` today. The map row's *"`status.clear` allowed on `OnActivate`"* is one row in
`AtomKindRegistry` — an effect-atom file — **filed on `effect-atom-map.md`**, not edited here. Until it lands,
`SupplyClassMap` refuses such a row with `consumable.trigger-not-allowed` (`ConsumableDef.cs:254`) at import.

**Battle.** `useContext: battle` rides the action layer: the supply's `GrantsActionId` (`ConsumableDefRow`,
*"the seam to the action layer"*) names a corpus action whose `OnActivate` is raised at `BasicAttack.cs:132`
(the ideal's `:101` is the comment; the code is `:132`) and whose cost is the item — **A3's item-cost row,
external and gating for battle use only**. Rest and curio uses do not wait on A3: outside a fight the cost is
the pack decrement, which `loot-pack` owns as `pack.drop`.

**Revive.** The one legal `Downed → Charging` trigger outside a corpus action (`TurnState.cs:60`; attrition
§6, `:227-230`): a `revive` supply fires `resource.delta hp` for `attrition.revive.hpMilli` of max (registries
`:132`, starting shape 250) **only on a member whose machine is `Downed`** — the host's `IsDowned` fact —
refusing any other target with `supply.target-not-downed`. `downedOnce` is never cleared (R3). Usable at
`rest` and `battle`, never at `curio`.

### 4. Object anchors → room objects

**Reconciliation, stated once.** The brief asks for object anchors *"instantiated per room"*; the approved
seed contract says *"Objects: v1 has none as seed"* (ask-first: *"a new anchor kind"*), the map row says *"v1
curios only"*, and `obstacleVerbs` rows are base-defense's. So this module mints **no object seed kind**:
`RoomObject` is a **projection** over three specified sources, and the same resolver reads a fourth when
base-defense's 18th field lands:

| Source | Object kind | Reusable? | Owner of the row |
|---|---|---|---|
| `event-deck` curio row for the room (`spec-dungeon-seed-contract.md:89` `supplyOverride`) | `curio` | one-shot — spent on its draw | `event-deck` |
| `gated` door (`WorldLane.TypeId = gated`, `GateKeyId != null`, `WorldState.cs:247`) with `keyForLaneId` on some room (`spec-delve-scope.md:88`) | `obstacle` | until opened or broken | `delve-graph-roll` / `delve-scope` |
| room kind `shrine · merchant · rest · wild(altar/cage) · boss` | `building` | reusable within the visit, per its owner's rule | the room-kind owner (§7) |
| `structure-schema` row with `interaction ≠ none` (later) | `structure` | yes — *"still there after you interact"* | base-defense |

`RoomObject(sectorId, kind, sourceRef, verbs[], requirement: PredicateNode, oneShot, ownerKey:
"sector:{sectorId}")`. `verbs[]` ⊆ `interaction-verbs.v1.json`; `requirement` is a `PredicateNode`
(`PredicateNode.cs:60-77`) over the closed `LeafId` list (`:20-34`) plus `event-deck`'s four new leaves. Facts
are caller-supplied at evaluation setup (`FactReader.cs:16-20`, `Stock0Qty` etc.): the host loads the party's
pack into the stock slots; Core reads no store. Building the projection draws nothing.

### 5. Verbs and their resolution table

**Drift, reported.** The registry ships `open · disarm · pray · loot · destroy · garrison`
(`spec-dungeon-registries.md:78`), each with a base-defense `decision` column. The brief's `break` is
`destroy`, `search` is `loot`, `offer` is `pray`; its `trade` and `rest` are **room-kind interactions, not
object verbs** (no registry row needed); `ignite` is **dropped** — *"Torch-analog: none"* (§11.5). Every verb
resolves to a call into an owner; none computes a number here:

| Verb | Object kinds | Requirement (closed leaves) | Resolution — always someone else's number |
|---|---|---|---|
| `open` | `obstacle` (gated door), `curio` (chest) | `HoldsStock(key.{laneId}, 1)` for a door; the curio row's own predicate | door: `LaneGate` (`spec-delve-scope.md:183-187`, lifted from `MarchResolver.cs:57-60`) nulls `GateKeyId`, key consumed (`pack.drop`); curio: `event-deck` outcome draw with the `key` override tag |
| `destroy` | `obstacle`, `structure` | none, or `HpAboveMilli(Self, stamina floor)` | **either** a fight with one `CombatantKind.Structure` actor (`BattleModels.cs:127-136`, `Kind` at `:106`; `encounter-generator` seats it, `spec-encounter-generator.md:139`) through the `delve` profile, **or** a stamina cost through `CostLedger.TryPay` on the corpus action `delve.break` — the domain's rung picks which via `objects.breakMode` (§Tunables); on success the door is opened as above, no key consumed |
| `disarm` | `curio` (trap), `obstacle` (trapped door) | `HoldsStock(watch|key, 1)` or a status | `event-deck`: the trap row's outcome table with the override forcing the safe outcome |
| `loot` | `curio` | the row's predicate | `event-deck` outcome draw → `dungeon-loot` mints the drop → `loot-pack` places it |
| `pray` | `building` (shrine, altar) | shrine: none; altar: unbanked souls ≥ the pull price — a `dungeon-loot` at-risk-ledger check made by the host, **not** a predicate leaf (`HaulAtLeast` counts occupied pack cells, `spec-event-deck.md` §6) | shrine: `event-deck` row (spirit/nerve `resource.delta`, delve-scoped grants on `unique-actor`); altar: `wild-room`'s `SummonRoller` pull at `dungeon-loot`'s `SoulSinkPolicy.Price` (R5) — wave 4, an interface here |
| `garrison` | `structure` | none | base-defense decision 15 — refused in v1 (no structure rows; `GarrisonedBy` `BattleModels.cs:117-123` is consumed, not built) |

Room-kind interactions the resolver also dispatches, without a verb row: **trade** (merchant — `dungeon-loot`
prices the room's stock at `Θ_room` × `merchant.markupMilli`, `loot-pack` places the purchase, §1
instantiates), **rest** (`RestResolver.Resolve(members, rung, tuning)` → `RestOutcome`,
`spec-delve-attrition.md:422`), **cage** (wild-room's captured-demon seam, wave 4 — the `open` verb on a cage
object is reserved and refused until that spec lands). Every resolution appends one decision:
`{kind: "object.{verb}", partyIndex, payload: {sectorId, sourceRef, outcome}}`.

### 6. Obstacles and gated doors — the key rule made concrete

The graph guarantees the key: rule 12 (`spec-delve-graph-roll.md:171-174`) places exactly one `cache`/`elite`
room with `keyForLaneId = laneId` strictly above the gate on another route; `dungeon-loot` mints the key as a
`utility` supply (`overrideTags: [key]`, container id `key.{laneId}`) into that room's drop. This module adds
the second path — **break** — and the rule that keeps the door honest: **every gated door has a reachable key
or a break option**, checked at delve creation (§8). A `breakMode: none` domain whose key room sits on a route
this raid mode does not walk is a preflight throw, not a soft lock. Opening writes `WorldLane.GateKeyId = null`
through `RpgStore.Delve` — the one writer `delve-scope` names (`:185-187`) — and the `route` decision follows.

### 7. Buildings — altar, merchant, shrine, forge; the cage seam

A building is a multi-verb `RoomObject` whose owner is its room kind. **Merchant:** stock = the domain's
supply table filtered to `useContextAdds ∩ {rest, curio, battle} ≠ ∅`, drawn on `dungeon:merchant:{r}:{c}`
(a structural pick, law 2), priced by `dungeon-loot`, placed by `loot-pack`, instantiated by §1 at `Θ_room`
on purchase — the merchant sells anchors, the buyer receives a concrete. **Until the item-side derived price
exists** (`item/seed-contract.md` §2.1 lists price as DERIVED with none built), `dungeon-loot`'s `DelvePrices.Merchant`
refuses (`delve.price-undesigned`) and the merchant room opens as a sell-nothing rest — no default, no literal
(`spec-dungeon-loot.md` §6). **Shrine:** `pray` → an `event-deck`
row of kind `shrine`; nerve relief stays a `resource.delta`/`status.apply` in the row. **Altar:** `pray` →
wild-room's pull; at-risk haul (R5). **Rest fire:** the `rest` interaction above. **Cage:** wild-room's seam
(wave 4) — present in the projection so `delve-stage` can draw it; its verb refuses until then. **Forge:** the
brief names it; **no approved document does** — listed under *Ask first*, not designed: a forge is an
item-mutation sink (`Items/Mutation/MutationOp.cs` exists) and a fourth soul sink the economy table (ideal §6)
does not price.

### 8. Refusals and preflight

Refused at **import**: a verb not in `interaction-verbs.v1.json`; a `utility` supply with `overrideTags:
none` and no `restore` atom (§1.7); a `board` class in any delve context; `status.clear` on `OnActivate` until
the trigger row lands; a `revive` row with any context but `rest`/`battle`. Refused at **delve creation**
(`ObjectPreflight.Run(graph, palette, raidMode, tuning)`, throws): a gated door with neither a reachable key
room on this raid's walks nor a `breakMode`; an object whose `requirement` names a stock no supply in the
domain palette carries (the `OverrideTagUnsupplied` shape, `spec-dungeon-seed-contract.md:149`, run per domain
— *"a requirement that can never be met in its domain"*); an empty curio verb set. Refused at **use**:
`UseContext.Menu` anywhere in a delve (`supply.menu-in-delve`, before `GateManifest`); a supply the pack does
not hold; a `revive` on a member not `Downed`; a verb the object does not list; a spent one-shot. Every
refusal is a named rule id in a registered namespace (`ConsumableDef.cs:212-219` pattern: `supply.*`,
`object.*`) — never a new `AtomRejectionReason` member.

### 9. Determinism

Pure over `(anchor, room facts, party state, delveSeed, tuning)`: `SupplyUse.Use` and `VerbResolver.Resolve`
take every fact as a parameter and return an outcome record plus the decision entry; the host applies them in
one `RpgStore.Delve` transaction. Streams are `SeededRng.DeriveStream` names — `dungeon:supply:{r}:{c}:{n}`,
`dungeon:supply:entry:{n}`, `dungeon:merchant:{r}:{c}` — **filed on `delve-graph-roll`'s reserved list**
(`spec-delve-graph-roll.md:130-132`). No `System.Random`, `DateTime`, `Environment.TickCount`, store or I/O
under `Core/Delve/Supplies/` or `Core/Delve/Objects/`. A curio's draw is `event-deck`'s stream, a Structure
fight's is the battle seed, a key's minting is `dungeon-loot`'s — this module draws only the three above.

## Tunables

All in `data/tuning/dungeon.v1.json` through `dungeon-registries`' T5-rejecting loader; units in the key;
one owner each. **No price literal anywhere** — prices are `dungeon-loot`'s derived read.

| Key | Unit | Owner | Starting shape |
|---|---|---|---|
| `rest.activations` · `rest.ambushMilli` | uses int · ‰ long | registries `:140` (read) | 3 · 330 |
| `merchant.markupMilli` | ‰ long | registries `:140` (read) | 1150 |
| `attrition.revive.hpMilli` | ‰ of max hp, long | `delve-attrition` (read) | 250 |
| `pack.footprint.consumableClass.*` · `pack.stack.consumableClass.*` | cells int · count int | registries `:141` (read) | — |
| **new** `objects.breakMode` · `objects.breakStaminaMilli` | enum `none · stamina · structure · either` (per-domain override) · ‰ of max stamina, long (the `delve.break` cost row) | this module via `dungeon-registries` | `either` · 400 |
| **new** `objects.structureHpBand` · `merchant.stockCount` | `countBand` ordinal — the Structure's `MaxHp` is `encounter-generator`'s `BattleRuleset.BaseHp(θ)` read, this key only names the band · count int | this module via `dungeon-registries` | `some` · 4 |

## Numeric types

Amounts — a refill, a revive heal, a Structure's hp, a price from `dungeon-loot` — are `long`. Per-mille
tunables are `long`, applied by `ContentScale.Apply(rolledValue, milli)` (`ContentScale.cs:31`) inside the SDK
— widen before multiply, divide by 1000 last, never here. Counts — activations, stock, stack, cells — are
`int`. Seeds are `ulong`; `rollSeed` into `TryInstantiate` is `long` (`Instantiator.cs:102`). No `float`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Supplies|FullyQualifiedName~Delve.Objects|FullyQualifiedName~Consumable"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Expedition|FullyQualifiedName~World"   # goldens untouched
python scripts\audit-magic-numbers.py --summary ; .\scripts\guard-dal.ps1      # zero new M1 rows under Delve/
```

## Structure

```
src/FusionRpg.Core/Items/Consumables/ConsumableDef.cs  → UseContext.Rest/.Curio; UseContexts.*; RuntimesFor rows
src/FusionRpg.Core/Delve/Supplies/   SupplyClassMap.cs (§1, §8) · SupplyInstantiation.cs (§1, §9) · SupplyUse.cs (§3)
src/FusionRpg.Core/Delve/Objects/    RoomObject.cs + RoomObjectBuilder (§4) · VerbResolver.cs (§5) · ObjectPreflight.cs (§8)
data/tuning/consumables.v1.json      → contextsAuthored + rest, curio, battle
tests/FusionRpg.Core.Tests/Delve/Supplies/, Delve/Objects/
UNTOUCHED: AtomKindRegistry.cs (status.clear row filed), interaction-verbs.v1.json, RpgStore.Delve.cs (delve-scope's)
```

## Code style

Pure resolvers with injected facts, the `LoadoutSet.Validate` shape (`LoadoutSet.cs:46-50`): delegates in,
record out, first broken rule wins, nothing applied on refusal.

```csharp
/// <summary>Pure. Same (object, verb, facts, tuning) ⇒ same outcome and decision entry.</summary>
public static VerbOutcome Resolve(RoomObject obj, string verb, PartyFacts facts, DungeonTuning tuning)
{
    if (!InteractionVerbCatalog.IsKnown(verb)) throw new InvalidOperationException($"verb '{verb}' is not in interaction-verbs.v1.json");
    if (!obj.Verbs.Contains(verb)) return VerbOutcome.Refuse("object.verb-not-offered", verb);
    if (obj.OneShot && facts.Spent(obj)) return VerbOutcome.Refuse("object.spent", obj.SourceRef);
    if (!PredicateEvaluator.Holds(obj.Requirement, facts)) return VerbOutcome.Refuse("object.requirement-unmet", verb);
    return verb switch
    {
        "open"    when obj.Kind == ObjectKind.Obstacle => VerbOutcome.OpenGate(obj.LaneId!, consumeStock: $"key.{obj.LaneId}"),
        "destroy" when tuning.Objects.BreakMode is BreakMode.Stamina or BreakMode.Either => VerbOutcome.PayThenOpen("delve.break", obj.LaneId!),
        "destroy" => VerbOutcome.StructureFight(obj.SectorId, tuning.Objects.StructureHpBand),
        "loot" or "disarm" or "pray" when obj.Kind is ObjectKind.Curio => VerbOutcome.DeckDraw(obj.SourceRef, verb),
        _ => VerbOutcome.Refuse("object.verb-unresolved", verb),
    };
}

/// <summary>A supply use: context gate, pack stock, then the container's OnActivate atoms — every pool change a resource.delta.</summary>
public static SupplyUseOutcome Use(MemberState m, InstanceRow supply, UseContext ctx, PartyFacts facts)
{
    if (ctx == UseContext.Menu) return SupplyUseOutcome.Refuse("supply.menu-in-delve");
    if (!facts.HoldsStock(supply.ContainerId, 1)) return SupplyUseOutcome.Refuse("supply.not-held");
    if (SupplyClassMap.ClassOf(supply) == ConsumableClass.Revive && !m.Downed) return SupplyUseOutcome.Refuse("supply.target-not-downed");
    return SupplyUseOutcome.Fire(supply.Atoms, decrement: supply.ContainerId,
        decision: new DelveDecision("supply.use", m.PartyIndex, new { m.MemberId, supply.ContainerId, ctx }));
}
```

## Testing strategy

- **Goldens per (object kind × verb):** `obstacle×open`, `obstacle×destroy(stamina)`, `obstacle×destroy(structure)`,
  `curio×loot`, `curio×disarm`, `curio×open`, `building×pray(shrine)` — seven, hashed over outcome + decision entry.
- **Property, 256 rolled graphs per layout tier × raid mode:** every `gated` door has a reachable key room on
  this raid's walks *or* `breakMode ≠ none`; `ObjectPreflight` throws when a key room is moved below its gate.
- **Concrete supply reproduces:** `TryInstantiate` twice over `(container, revision, seed, Θ_room)` ⇒ equal
  `ContentFingerprint()`; a different `Θ_room` ⇒ a different fingerprint and a larger `hunger` delta.
- **Contexts:** `UseContext.Menu` refused in a delve before `GateManifest`; `rest`/`curio` accepted only when
  the unioned contexts name them; `battle` refused until A3 lands (`Actions/CrossProgramLandedFlags.cs` shape).
- **Revive:** only on a `Downed` member (`Downed → Charging`); `downedOnce` untouched; refused at `curio`.
- **Import refusals:** unknown verb; `utility` with `none` tags and no `restore`; `board` in a delve;
  `status.clear` on `OnActivate` until the registry row lands — each a named rule id.
- **Wire stability:** `UseContextWire` for every shipped `k1`–`k3` row byte-identical after the widening.
- **Goldens untouched:** four battle hashes, the 32-seed sweep, four expedition tier hashes, the world
  scenario hash — same command. **Guard:** no `System.Random`/`DateTime`/`Environment.TickCount` under
  `Core/Delve/Supplies/` or `Core/Delve/Objects/` (`spec-turn-engine.md:138` pattern).

## Boundaries

- **Always:** one `TryInstantiate` per supply as it enters a pack, at `Θ_room`; `HoldsStock` over the pack;
  every pool change through `resource.delta`; every price through `dungeon-loot`; every placement through
  `loot-pack`; every use and verb a decision entry; registry verbs; the closed eight owner keys; preflight
  before the first room.
- **Ask first:** a forge (§7); a delve-owned object seed kind; widening `interaction-verbs.v1.json` (a
  base-defense `decision` per verb); a third new `UseContext`; a supply targeting another party; `garrison`
  before base-defense ships a structure row.
- **Never:** a second roll (`Instantiator` is the SDK — no private freeze); a price literal; a verb outside
  the registry; an object that writes a pool without `resource.delta`; a supply with a model-picked
  magnitude; `System.Random` or `DateTime`; `UseContext.Menu` inside a delve; serializing `Kind` on
  `BattleActorSetup` (`BattleModels.cs:105` `[JsonIgnore]` stays); reading `rpg_item_stock` for a delve
  fact; a torch, light meter or `ignite` verb.

## Success criteria

1. Seven verb goldens blessed; 256-graph preflight property green per tier. 2. Every §8 refusal has a named
test. 3. Concrete-supply reproducibility over `(container, revision, seed, Θ)` proven. 4. Battle, expedition
and world goldens byte-identical (run, not argued). 5. `audit-magic-numbers.py` adds zero rows under `Delve/`.
6. `consumables.v1.json` widened, every `k1`–`k3` wire string unchanged. 7. G3's *"hunger binds between
rests"* holds with rations usable only at `rest`.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `SupplyInstantiation.Concrete(anchor, ext, delveSeed, r, c, n, Θ_room, tuning) → InstanceRow` | `dungeon-loot` (drops, merchant sales), `loot-pack` (the cells it occupies via derived `sizeBand`) |
| `SupplyUse.Use(member, instance, context, facts) → SupplyUseOutcome` | `delve-stage` (the use button), `event-deck` (a `supplyOverride` spend is a `curio` use), `delve-attrition` (revive → `Downed → Charging`) |
| `RoomObjectBuilder.For(room, curioRow?, doors, kind) → IReadOnlyList<RoomObject>` | `delve-stage` (interaction prompts, `Glimpse` shows kind only), `event-deck` (curio verbs), `wild-room` (altar/cage objects), `delve-quests` (`interact-with` objective facts) |
| `VerbResolver.Resolve(object, verb, facts, tuning) → VerbOutcome` | the delve host (`RpgStore.Delve` applies), `dungeon-loot` (`Trade`, `DeckDraw` → mint), `loot-pack` (`Place`), `delve-attrition` (`Rest`), `wild-room` (`Pray` on an altar) |
| `ObjectPreflight.Run(graph, palette, raidMode, tuning)` | `delve-scope.CreateDelve` (before the first write), `delve-graph-roll` (the metric *"every gated door has a key or break path"*) |
| Filed rows | `effect-atom-map.md`: `status.clear` on `OnActivate`; `delve-graph-roll`: three reserved stream names; `dungeon-registries`: four `objects.*`/`merchant.stockCount` keys; `action-map.md`: `delve.break` corpus action with a stamina cost row |

## Design-gate checklist

```
[x] Subsystems: consumables, effect atoms (Instantiator, triggers, predicates, owner scopes), actions
    (costs, loadout), battle (CombatantKind, TurnState), world movement (gates), party dungeon.
[x] Read this session: party-dungeon-map (row 12, gates, external deps); the eight approved specs; ideal
    §4.5, §4.7, §8 box, §10 laws, §11.5, §11.9 box, §11.10 R3/R5; audit §1(f), S2-4, S2-6, §7 rows;
    effect-atom/definitions.md §6; spec-expeditions.md. dungeon-loot / loot-pack: absent, treated as interfaces.
[x] Every claim cites file:line opened against CODE. Drift reported: UseContext has FOUR members incl. Lawn
    (brief/attrition say three); BasicAttack raises OnActivate at :132 (ideal :101 is a comment);
    WorldLane.GateKeyId is WorldState.cs:247 (graph-roll spec says :228); registry six verbs vs the brief's
    eight (break=destroy, search=loot, offer=pray; trade/rest are room kinds; ignite dropped); "Room" is the
    approved `curio`; a consumable container has no pool (consumable.rolls) so the roll is the fixed-core
    freeze at Θ; the consumable ContainerKind is still X7/D27-external (:201 false).
[ ] Constraints tested — nothing was run; unbuilt. "Goldens untouched" is argued from the hash inputs
    (Kind/GarrisonedBy JsonIgnore; no BattleActorSetup field added) and is the first build task.
[x] No §2 invariant contradicted: no magnitude computed, no private f(level), no cap, no price literal, one
    roll SDK. Reconciled with siblings: no object seed kind minted; forge is Ask first, not designed.
```
