# Spec: unique-pipeline

Status: **DRAFTED 2026-09-05 (wave 4) — unbuilt, not yet approved.** Written against shipped code the same day; every
`file:line` was opened this session; drift against the brief, map and earlier specs is reported in the checklist, never
silently fixed. Every number is a starting shape, never a balance decision.

Module id `unique-pipeline`, row 16 of the [party-dungeon map](../party-dungeon-map.md) (`:126`; wave 4, `:140`). Depends
on `dungeon-loot` (`boss-unique` group key, `dungeon-clear` grant seam, `RarityShift.ComposeFloor` — `spec-dungeon-loot.md`
§3–§5), `dungeon-seed-contract` (ownership levels, §2 audit, `dungeonBinding` §1.7, planner §4, provenance §6),
`dungeon-registries` (`loot.extendSlotChanceMicro`, `loot.rooms.boss.*`, `difficulty.rungs[].rarityFloor`). **Prerequisite
row:** `decisions.md:116` (P4). **External:** item module 17 `uniques` amended (`item-map.md:153`; map `:98`); **X4**
`affix-channel-weights` (`item-map.md:67`, specced, unbuilt — the `boss` channel stays inert until it lands); effect-pipeline
affix authoring (`data/seed/effects/affixes/all.json` holds two entries; the variance pools draw from that library). Gate
**G4** (`:160`). Ideal: §11.7 (`:1495-1530`), §11.9 #13/#16 (`:1699-1737`), §11.10 R7 (`:1753`). Review: S2-9, S2-13
(`audit-2026-09-05.md:219, :223`), §7 (`:371`).

## Objective

Make uniques drop in delves, the Diablo way, without adding a roll, a curve or a set. Two halves: **the seedsmith uniques
extension** (identity written offline by the model — name, lore, base type, fixed atom kinds and bands, grants — every
number written by code) and **the runtime** that turns an anchor into a concrete item through the one roll every item takes
(`Instantiator.TryInstantiate`, `Instantiator.cs:98-107`) at the boss room's `Θ_room`. It also lands the **`loadout.slots`
derived channel**, decides the **95 shipped uniques below rung 80**, and flips `DropTableDraw.UnavailableKinds[Unique]`
(`DropTableModel.cs:158-166`) so `DungeonLootTableGen` may write the boss table's `boss-unique` group by id.

Success: a `rich` domain's boss at `hard` lists its climate's rung-80+ uniques by id; a clear rolls one through
`TryInstantiate` at `Θ_room = 100`, fixed atoms frozen with `contentScale` once, variance slot drawn once; a `sunwoven` copy
always carries the extend-slot atom, a `firstseed` copy 100 per million on its own stream; the wearer reads `5 + 1` slots; two
copies differ in the variance slot and nowhere else at equal `Θ`; every golden is byte-identical.

## Locked anchors

- **Decision 13 (ideal `:1699-1708`), owner verbatim:** *"bring unique items into the game, like Diablo … unique items are
  very strong, rarity 8+ and cannot be a set item; it will have unique affix, fixed atom effect + random atom effect; bring
  new passive skills grant and unique action grant to the game."* Recorded: *"rung ≥ 80 (`firstseed` and up); never
  `set_eligible`; a fixed core plus a rolled variance (the shipped unique shape already has both); passive and action
  grants as atoms on the unique's container."* Loot (`:1503-1504`): *"a unique is granted by id and never categorically."*
- **Decision 16 (`:1726-1737`), owner verbatim:** *"add a new extend-action-slot atom effect, very very rare on normal
  drops (0.01% chance) and can be higher in unique items; some rarity 9+ unique items can have a fixed extend-action-slot
  atom effect."* **R7 (`:1753`):** *"Keep the 0.01% as `loot.extendSlotChanceMicro: 100` on a per-million stream
  (`CombatProbability.cs:15` precedent); one extend-slot unique counts at a time (`affix.exclusiveTags`); the slot count stays
  structural with the exemption comment; the rung-9+ fixed core stands."*
- **`decisions.md:116`, verbatim:** *"**`LoadoutSet.MaxSize = 5` is the structural base of a `loadout.slots` derived
  channel**, registered in the Actor Hub like every other derived channel; the three readers (`LoadoutSet.cs:60`,
  `AutoEquip.cs:55`, `CapPolicy.cs:39`) read `base + channel`. The channel is fed by an **extend-action-slot** atom effect
  — `stat.derived` on `loadout.slots` if the closed 16-kind vocabulary admits it, else a reviewed seventeenth kind —
  carried by rung ≥ 90 uniques as a fixed core and by normal drops at `loot.extendSlotChanceMicro` (100 per million, drawn
  per-million on a named stream). One extend-slot item counts at a time (`affix.exclusiveTags`). Unique **passives** sit
  outside the five slots; unique **actions** occupy one."*
- **`ssot-rarity.md` §3.3 (`:109-131`):** ordinals spaced by 10 — **"8+" is 80 `firstseed`; "9+" is 90 `sunwoven` and 100
  `almanac`** (`data/tuning/item-rarity.v1.json:7-18`). **§3.5:** overlap is *"the product of three variances … no fourth
  mechanism."* **§3.6:** *"Unique should be a rung — No. A unique is a container with a fixed core and `pool_rolls = 0`; it
  carries a rung like anything else."* **§8.5:** a multiplier on the rung *"dominates count and tier, and the overlap dies."*
- **`ssot-uniques.md` §3.6 (`:177-207`):** identity atoms 1–3 roll **value only** (spread ≤ ±15%); the variance slot is
  **0 or 1** draw from a pool *"authored for this unique"*; *"`pool_rolls` may never exceed 1 on a unique"*; *"`min_tier ==
  max_tier`."* Shipped as `UniqueLimits.MaxTotalRolls = 1` (`UniqueRow.cs`, structural, exemption comment). **§3.8:** *"A
  unique may not be a set member. Hard no."* **§4.5:** ordinal ≥ 90 is never plain `drop` (`UniqueValidator.cs:135-139`); no pity.
- **Seed law** (`item/seed-contract.md` via seed-contract §1): **Law 1** — a field with no declared level is a defect;
  **Law 2** — no seed file carries a number; the model writes identity, code writes every magnitude.

## Design

### 1. The unique anchor — identity only

The corpus exists: **144 anchors in 18 files** under `data/seed/items/uniques/` (8 per file, `theme × rung-band`), read by
`UniqueCorpus.Parse` (`UniqueCorpus.cs:101-133`; the band is the partition key, `:125-131`). Shipped shape
(`charnel-bloom-90-001`): `id · nameKey · name · frame · baseType · rarity · powerAxis · fixedAtoms[{family, powerBand}] ·
varianceSlot{family, variance} · counterPressure{kind, note} · tags · iconKey · flavorKey · flavor · acquisition`. No field is
a number. This module **amends the `unique` `KindSpec`** (`adapters/items/kinds.py:56-60`) with one level per field:

| Field | Level | Vocabulary · `none` | Is not |
|---|---|---|---|
| `id`, `nameKey`, `iconKey`, `flavorKey` | PLANNED | `unique.<theme>-<band>-<nnn>` plus three keys minted from the slug; container id DERIVED (`UniqueContainerIds.FromSeedId`, `UniqueRow.cs:142-151`: `unique.x → item.x`) | never authored (seedsmith-map Appendix A row 6) |
| `name`, `flavor`, `counterPressure.note`, `reason` | AUTHORED | free text | flavour never states a number |
| `frame`, `powerAxis` | PLANNED | the cell: frame (2) × `core.v1.json` axis (5) × band (3); the *"one unique per (role, rung band, power axis)"* grid (`UniqueRules.AxisCollision`) is why the planner owns the axis | not a stat |
| `rarity` | **PLANNED** | `firstseed · sunwoven · almanac` (seed-contract §1.7) | not a strength — on a unique the rung buys only the gate (§5) |
| `baseType` | VALIDATED ref | a base-type id; **role and mass class DERIVED from it** (`tags.v1.json:67-75`, `mass-class` `appliesTo` `unique`; `loot-pack` §2 derives footprint the same way) | not a role noun |
| `fixedAtoms[]` (1–3, `maxIdentityAtoms`) | `family` VALIDATED; `powerBand` AUTHORED, **voted** (seed-contract §4) | atom-family ids; the list is never empty (`UniqueCorpus.cs:162`) | **never a magnitude** |
| `varianceSlot` | family VALIDATED; `variance` AUTHORED (`narrow · normal · wide`) | `none` legal (a 0-roll unique) | not a second core |
| `counterPressure` | `kind` VALIDATED (`drawback · conditional · narrow`); its arguments VALIDATED | closed `core.v1.json` lists | not the note — the check is `UniqueValidator` |
| `acquisition` | **PLANNED** | `drop` (rung 80 only) · `source-locked` · `deterministic` | not a weight |
| `actionGrantRef` | VALIDATED | an `rpg_action` corpus id · `none` legal and the common reading | not an atom (§3) |
| `dungeonBinding {encounterRef, eventRef}` | VALIDATED (seed-contract §1.7) | dungeon corpus ids · each `none` legal; **both `none` refused on `source-locked`** | not a table |
| `tags` | VALIDATED | item tag registry; exactly one `mass-class` | — |
| **DERIVED, never in the file** | | `climateAffinity` (the theme's `elementAffinity[]`, `themes.v1.json:11`), `budget_ae` (`UniqueValidator.Price`, `:178-188`), the container, extend-slot carriage (§4), footprint | |

**`neverSet` is a schema invariant, not a field.** `set` is its own `KindSpec` (`kinds.py:61-64`: `themeKey · members ·
thresholds`); the §2 audit gains a **set-stem check** — `set*`, `*setId*`, `*setBonus*`, `members`, `thresholds` on a unique
anchor are refused. Import already refuses from the other side (`IsUniqueSetMember`, `RpgStore.ItemUniques.cs`;
`unique.set-membership`, `UniqueValidator.cs:148-151`). A passive is not a field either: decision 13 puts passives *"as atoms
on the unique's container"* — that is `fixedAtoms[]`, and the brief's `passiveGrantRef` collapses into it.

**The 95 below rung 80, decided (map `:175`; S2-13).** From disk: `grafted 20 · cultivated 20 · fused 20 · chimeric 20 ·
heirloom 15` = **95** below 80; `firstseed 9 · sunwoven 23 · almanac 17` = 49 at or above. **Ruling: `enabled: false`, not
re-rung.** Every rung-relative check — `narrowCeilingPerMille`, `budget_ae` against `UniqueBudget.RungBaselineAeHundredths(rung)`
(`UniqueValidator.cs:154-164`), the partition key of the anti-convergence grid — was authored for the band the anchor sits
in; re-runging 95 three bands up fails those checks en masse or passes with a note reading *"well under a cultivated rare's
baseline"* on a sunwoven item. Disabled, never deleted: `item_unique.enabled = 0` (`RpgStore.ItemUniques.cs:47`); the 95
`entryKind: unique` rows in `d1/d2/d4` (`d1.json:588-606`) flip `enabled: false` — *"row kept, never drawn"*; re-enabling is a
one-flag edit. **`uniques.v1.json rungFloorOrdinal` moves 30 → 80** in its own change (T7), re-seeding `unique_eligible`
through `RpgStore.SeedUniqueEligible` (`RarityBudgetKeys.cs`; `UniqueTuning.IsRungEligible`) with no code change.

### 2. Fixed + random atoms through the one roll

`UniqueContainerBuild.From(anchor, rollSeed, tuning, lookups)` is a pure function producing the `ContainerRow`
(`ContainerRow.cs:99-140`); the instance is `TryInstantiate` on it — nothing else.

- **Fixed core = `fixedAtoms[]`**, one `ContainerAtomRow(seq, atomId, overridesJson)` each, atom id from `family × powerBand`,
  the `OnInstantiate` band from `identitySpreadPerMille`; frozen in seq order (`Instantiator.cs:120-129`) through
  `ContentScale.Apply` (`:313-315`) with `contentScaleMilli` computed **once** (`:116`) from `thetaContent = Θ_room`.
- **Pool = the variance slot**: `PrefixRolls + SuffixRolls = 1` (or 0), `MinTier == MaxTier` at the authored tier, 3–6
  `ContainerPoolRow`s from the affix library (`ContainerRow.cs:33-38`); `Draw` picks one, `Freeze` scales it the same way.
- **`Rarity` = the anchor's rung id** — a description and the drop gate (§5); it buys no count band here, because
  **`MaxTotalRolls = 1` is the class definition.** The brief's "random atoms by the rarity row's count band" is the
  *equipment* reading of `RarityRow` (`ContainerRow.cs:163`); on a unique the shipped const and ssot-uniques §3.6 refuse it.

Reproducible over `(container, catalogRevision, rollSeed, thetaContent)` (`Instantiator.cs:83-84`). One roll, never a
second: the drop path (§5) supplies `rollSeed` from `LootStreams.RollSeed(i)` (`LootStreams.cs:56`), the first-clear path
from the grant's own index (dungeon-loot §3). `Instantiator` gains no unique branch (`RpgStore.ItemUniques.cs:23-26`).

### 3. Passive and action grants

**Passives** are the fixed core, bound at equip through the existing projection (`ReconcileUniqueEquipmentAtomBindingsUnlocked`
produces and withdraws by source, ideal `:1091-1092`) — on the actor's effect list, **outside the five slots**. **Actions** occupy a slot through the shipped seam, not an atom: `item_granted_action(container_id, seq, action_id, role,
enabled, revision)` (`RpgStore.ItemGrants.cs:40-51`; `ItemGrantedActionRow.cs:227-236`) → `EquippedGrantProjection.GrantsFor`
(`EquippedGrantProjection.cs:36-59`) → `ActionGrantRow(OwnerKind.Entity, specimenId, actionId, Source: containerId, GrantRole)`
(`ActionRow.cs:135-136`) → `WebMatchService.EquippedActionIdsFor`. **None of the 16 kinds is an action grant**
(`AtomKindRegistry.cs:476-869`: `stat.modify · stat.derived · resource.delta · resource.economy · status.apply · status.clear ·
shield.grant · spawn.entity · board.action · grid.spawn · grid.clear · box.set · bullet.modify · match.modify · wave.control ·
ui.present`), and `spec-granted-actions.md`'s Never list forbids one. So `actionGrantRef` emits one `item_granted_action` row,
`role: granted`, on the unique's container — decision 13's *"as atoms"* read as *"on the container"*. **Wiring gap:**
`ItemGrantedActionRow.ContainerId` is *"the base type's container id"* and `ItemGrantValidator` checks it against base-type
facts; a unique's `item.<slug>` must be admitted too — one validator arm, filed on `item-map.md` module 19. The consumable
arm (`ConsumableDef.GrantsActionId`, `ConsumableDef.cs:312, :322`) is untouched.

### 4. The extend-action-slot grant

**What it is in code.** `stat.derived` admits any channel `DerivedStatRegistry.CreateDefault().AllRegistered` knows
(`AtomKindRegistry.cs:511`; ops `Flat|Increased|Replace|Flag`, `:537`). `loadout.slots` is **not registered** (grep under
`Core/Stats`: zero) and matches no `status.*` prefix family (`DerivedStatRegistry.cs:313-360`). So the row's first branch
holds: **one `Register(new DerivedStatDef("loadout.slots", DerivedComposeKind.FlatSum, 0, Class: StatClass.Pool, Unit:
<count unit>))`** in `RegisterDefaults` (`:64-75` shape), **no seventeenth kind**; the atom is `{kind: stat.derived, channel:
loadout.slots, op: flat, amount: {min: 1, max: 1, roll: fixed}}`.

**Three readers** (`LoadoutSet.cs:40, :60`; `AutoEquip.cs:55`; `CapPolicy.cs:39` — verified three, audit §1(i)) read
`base + extraSlots`: `MaxSize` stays `const 5` **with the exemption comment** (*structural base of `loadout.slots`; the
channel is the progression*), `extraSlots` arriving through the injected-delegate seam `LoadoutSet.Validate` already uses
(`:47-51`). **One at a time (R7):** `extraSlots = channel > 0 ? 1 : 0` — the owner's rule at the read, structural, commented.
`affix.exclusiveTags` names that rule in the row; **no such tag exists in code or data** (grep zero across `Core/`,
`data/seed/effects`, `docs/architecture/{effect-atom,item}`), and encounter §6's pool-`Group` covers one container, not two
worn items — so the reader owns the rule; a cross-item tag is an ask on effect-pipeline if ever wanted.

**Carriage by rung.** Rung ≥ 90: the atom is **appended to the fixed core** — always, never rolled. Rung 80:
`ExtendSlotRoll.Hit(rollSeed, micro)` = `SeededRng.DeriveStream(rollSeed, "unique:extend-slot").NextUInt(1_000_000) < micro`
(`SeededRng.cs:26-27, :44`; `CombatProbability.cs:15`'s `1_000_000`; `IAtomRandom.NextPerMille` cannot express 100 per
million, `AtomRandom.cs:15`, S2-9), evaluated before `TryInstantiate` on the instance's own `rollSeed`; a hit appends the same
atom, so the build stays pure over `(anchor, rollSeed)`. The normal-drop arm is item module 11's step 9, calling the same `Hit`.

**Hazard, named.** `Freeze` applies `ContentScale.Apply` to every `Fixed`/`OnInstantiate` value (`Instantiator.cs:313-315`):
`Apply(1, 4235)` at `Θ_room = 70` is **4**, and a slot count must never scale. Rule: **a `stat.derived` value on a count-unit
channel is frozen unscaled** — one guard in `Freeze` reading the channel's `UnitClass`, tested by *"frozen at Θ 100, still
1."* Filed on the effect-atom program; until it lands the build refuses (`unique.slot-scaled`) rather than ship five slots.

### 5. The `boss-unique` drop group and first clear by id

`DropEntryKind.Unique` (`DropTableModel.cs:22`) is refused because *"no CONCRETE unique container exists yet"* (`:158-166`).
§2 makes the containers; this section closes the arm that would ship them flat: the non-equipment arm (`LootPipeline.cs:274-281`)
emits `LootGrant(index, kind, refId, count, channel)` with **no `RollSeed` and no `Mint`** — dungeon-loot §3's first-clear defect
again. **`MintUnique`** (beside `MintEquipment`, `:292`): `rollSeed = DeriveStream(lootSeed, LootStreams.RollSeed(i)).NextULong()`,
`ItemLevel` from step 3, rung = **the container's own `Rarity`** (authored, never drawn), then `view.Mint`. `UnavailableKinds[Unique]`
goes in the same change. Item-program file; filed on `item-map.md` module 11.

**The group.** `DungeonLootTableGen` writes `boss-unique` per domain boss table once containers exist: one `{entryKind:
unique, ref: item.<slug>, dropBand: exceptional}` per eligible unique beside `{nothing, staple}` — weights via the one
`weightTable` (`bands.v1.json:463-470`: 1000/300/90/25/7). **Eligible** = rung ≥ 80, `enabled`, `acquisition ∈ {drop,
source-locked}` (a `deterministic` unique never sits in a table, §4.5), and the theme's `elementAffinity[]` contains the
domain's climate or is empty (`themes.v1.json:11, :76`) — **a filter, never a weight**. `affixChannel: boss` is authored and
inert until X4 (`DropTableModel.cs:34-36`). **Two structural gates at the draw, no multiplier:** (1) rung ordinal ≥ the room's
composed floor (`RarityShift.ComposeFloor`, dungeon-loot §4) — under `bossRarityFloor: sunwoven` the rung-80 rows are kept,
never drawn; (2) authored tier ≤ the room's tier ceiling from `ItemLevel` (`DropEnvelope.Resolve`, `DropEnvelope.cs:39-41`;
ssot-uniques §3.6). Depth gates uniques; rarity columns only remove the low ones. An emptied group refuses, `unique.group-starved`.

**Source lock.** *"The lock lives in which table references the id"* (ideal `:1521-1524`): a `source-locked` unique is
listed by **exactly one** domain table, chosen by the planner from `dungeonBinding.encounterRef`'s domain; the seedsmith
`acquisition` module (`adapters/items/acquisition.py:1-15`, ported from `seed_graph`) asserts the count is one.
**First clear by id.** The `dungeon-clear` grant (`LootSourceRow.FirstClearGrant`, `DropTableModel.cs:52-60`;
`item_first_clear`, `RpgStore.Loot.cs:134-139`) **may name a unique container id** (decision 13). The domain anchor gains
`firstClearRef` (VALIDATED against rung-80+ `deterministic` unique ids · `none` legal) — **a one-field ask on
`dungeon-seed-contract` §1.1**, which has no such field today. Instantiated at `Θ_boss` on its own stream, banked at the clear.

### 6. Identity, not scarcity

Diablo 2's uniques drop repeatedly, and so do these. A unique is a container; many instances may exist; each differs in its
variance slot and identity values (±15%) and is identical in what it *is*. Scarcity is the rung's — `exceptional` weight, floor
columns, `Θ_room` on the tier ceiling — never a per-player counter: **no unique pity** (§4.5 rule 2, ideal `:1507-1508`). The
deterministic answer to "I never see it" is the first-clear grant and the blueprint path, both by id.

### 7. Refusals — never a default

Seed side (`contract --audit`, seed-contract §2 plus): a magnitude or `*Milli` on an anchor (`numeric_audit`,
`audit.py:26-32`); a set stem; `rarity` outside the three; `acquisition: drop` at ≥ 90; `actionGrantRef` not in the action
corpus; a fixed atom family not in the corpus or a kind not in `AtomKindRegistry`; a `source-locked` unique with both bindings
`none`, or referenced by zero or two tables. Import, unchanged: the nine `unique.*` rules (`UniqueCorpus.cs:71-83`). Runtime:
`unique.slot-scaled` (§4); `unique.group-starved` (§5); **an empty variance pool** → the instance carries its fixed core only
**with a warning row** (encounter §6: *"never a fake affix, never a flat stat bump"*). Nothing clamps (`ContentScale.cs:31-40`).

### 8. Determinism

Anchor → container is pure over `(anchor, registries, tuning, rollSeed)`; container → instance is `TryInstantiate`'s contract;
the boss group is pure over `(domain, themes, corpus, tuning)`, rerun byte-identical (seed-contract §6). No `System.Random`, clock or I/O.

## Tunables

| Key | Unit | Owner | Read here as |
|---|---|---|---|
| `loot.extendSlotChanceMicro` (100) | per-million **long** | `dungeon-registries` (`spec-dungeon-registries.md:146`) | §4 gate on rung 80; the same key the normal-drop arm reads |
| `loot.rooms.boss.{rarityFloor,rarityShiftRungs}` · `difficulty.rungs[].rarityFloor` · `domain.onceEntry.bossRarityFloor` | rung id · rungs int | registries / ladder | §5 gate 1, composed by `dungeon-loot` |
| `uniques.v1.json`: `rungFloorOrdinal` (30 → **80**, own change) · `maxIdentityAtoms` · `identitySpreadPerMille` · `budgetPremiumAeHundredths` · `narrowCeilingPerMille` · `maxRolesPerFrame` · `forbiddenRoles` · parity bounds · `outOfBandMagnitudeCapPerMille` | ordinal · count · ‰ · AE×100 long · ‰ · count · ids · ‰ · ‰ | item module 17 | §1 validation, unchanged |
| `item-rarity.v1.json` rungs · `bands.v1.json dropBand.weightTable` | per-100k · plain int | item modules 7 / registry | ordinals; §5 weights — never a multiplier |

**No new tuning key** — a second "unique" chance key would be a copied number (S2-10); seedsmith budget rows live in
`data/seed/items/_plan/`, never in a runtime tuning file.

## Numeric types

Magnitudes (frozen values, AE×100, `contentScaleMilli`): **`long`** (`ContentScale.Apply` is `long → long`,
`ContentScale.cs:31-40`; `BudgetAeHundredths` is `long`). The gate compares a `long` `micro` against
`(long)NextUInt(1_000_000)` — widened, never cast down. Rung ordinals, tiers, seq, slot counts: **`int`**. No `float`/`double`.

## Commands

```powershell
cd tools\seedsmith
python -m seedsmith items uniques contract --audit      # §1 levels, set-stem and rung checks; exit 1 on a finding
python -m seedsmith items uniques plan --dry-run         # cells (frame × axis × band), briefs, call ledger; calls nothing
python -m seedsmith items uniques run start --all | resume | status
python -m seedsmith items uniques audit                  # stale_ids, budget actual-vs-declared, acquisition = exactly one table
python -m pytest tests/test_unique_*.py -q ; cd ..\..   # transport stubbed to RAISE
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Items.Uniques|FullyQualifiedName~Delve.Loot|FullyQualifiedName~Stats.Derived"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Expedition|FullyQualifiedName~Items.Drops"
.\scripts\guard-power.ps1 ; .\scripts\guard-dal.ps1 ; python scripts\audit-overflow.py ; python scripts\audit-magic-numbers.py --domain items
```
`--dry-run` as default follows `report/cli.py:285` and seed-contract §Commands.

## Structure

```
src/FusionRpg.Core/Items/Uniques/   UniqueContainerBuild.cs · ExtendSlotRoll.cs — beside UniqueCorpus/UniqueRow/UniqueValidator:
                                    the unique is the item program's class and RpgStore.ItemUniques.cs keys item_unique 1:1 on
                                    the container; Delve/Uniques would put the class's runtime in a consumer's namespace
src/FusionRpg.Core/Items/Drops/     LootPipeline.cs MintUnique arm (:274-281 → the :292 shape) · DropTableModel.cs row removed
src/FusionRpg.Core/Stats/Derived/   DerivedStatRegistry.cs loadout.slots · Actions/ LoadoutSet · AutoEquip · CapPolicy — base + extraSlots
src/FusionRpg.Core/Effects/Atoms/   Instantiator.cs Freeze count-unit guard (filed) · Items/Grants/ItemGrantValidator.cs unique ids (filed)
src/FusionRpg.Core/Delve/Loot/      DungeonLootTableGen.cs boss-unique group (dungeon-loot's file, extended)
data/tuning/uniques.v1.json rungFloorOrdinal 80 (own change) · data/seed/items/uniques/*-{30,50,70}.json enabled: false
tools/seedsmith/seedsmith/adapters/items/  kinds.py (unique levels) · uniques/{planner,briefs,pipelines,audit}.py
tests/FusionRpg.Core.Tests/Items/Uniques/ · tools/seedsmith/tests/test_unique_*.py
```

## Code style

Pure over inputs, tuning injected, no I/O; rejections name the rule; no parameter named `level`/`lvl` on a numeric method.

```csharp
/// <summary>Anchor → container, pure over (anchor, rollSeed). Rung ≥ 90 carries the slot atom always (R7).</summary>
public static AtomRejection Build(UniqueSeed anchor, ulong rollSeed, long extendSlotChanceMicro,
    UniqueBuildLookups lookups, out ContainerRow? container)
{
    container = null;
    var core = new List<ContainerAtomRow>();
    foreach (var (fixedAtom, seq) in anchor.FixedAtoms.Select((a, i) => (a, i + 1)))
        core.Add(new ContainerAtomRow(seq, lookups.AtomIdFor(fixedAtom.Family, fixedAtom.PowerBand),
            lookups.IdentityOverrides(fixedAtom)));                  // a band → an OnInstantiate spec, never a number

    var ordinal = lookups.RarityOrdinal(anchor.RarityId);
    if (ordinal >= 90 || (ordinal >= 80 && ExtendSlotRoll.Hit(rollSeed, extendSlotChanceMicro)))
        core.Add(new ContainerAtomRow(core.Count + 1, ExtendSlotAtom.Id));   // stat.derived → loadout.slots, flat 1, unscaled

    var pool = anchor.VarianceSlot is { } v ? lookups.VariancePool(v.Family, v.Variance) : Array.Empty<ContainerPoolRow>();
    var tier = lookups.VarianceTier(anchor);
    container = new ContainerRow { ContainerId = UniqueContainerIds.FromSeedId(anchor.SeedId), Kind = ContainerKind.Item,
        Rarity = anchor.RarityId, MinTier = tier, MaxTier = tier, Atoms = core, Pool = pool,
        PrefixRolls = 0, SuffixRolls = pool.Count > 0 ? 1 : 0 };    // UniqueLimits.MaxTotalRolls — the class definition
    return AtomRejection.Ok;
}

public static bool Hit(ulong rollSeed, long chanceMicro) =>
    (long)SeededRng.DeriveStream(rollSeed, "unique:extend-slot").NextUInt(1_000_000) < chanceMicro;
```

## Testing strategy

- **Goldens per (unique × Θ):** every rung-80+ anchor at `Θ ∈ {20, 70, 100, 150}`, one seed, hashed over frozen `ValuesJson`
  and atom ids; blessed once. **Property:** across 64 seeds at equal Θ the fixed atoms' ids are identical and values sit
  inside ±15%; the variance slot varies; nothing else does.
- **Slot carriage:** every rung ≥ 90 build carries `ExtendSlotAtom`; rung 80 over 1,000,000 consecutive `rollSeed`s hits
  within `100 ± 30` (binomial 3σ); `micro = 0` never; the frozen amount is `1` at `Θ 100`; `LoadoutSet.Validate` accepts six
  ids with `extraSlots = 1` and rejects seven; two worn extend-slot items still read `+1`.
- **Refusals red/green:** a `setId` stem; `rarity: heirloom`; `acquisition: drop` at `sunwoven`; an unknown `actionGrantRef`;
  an unregistered atom kind; a `source-locked` anchor with both bindings `none`; a starved group.
- **Table gen and pipeline arm:** the boss table omits `boss-unique` while `UnavailableKinds` holds `Unique` and includes it
  once the arm lands; a climate lists only affinity-matching themes; every `source-locked` id in exactly one table; rerun
  byte-identical; a `unique` draw yields a non-zero `RollSeed` and a minted `InstanceId` with `RarityDraw` never called.
- **Goldens untouched:** the four battle hashes, the 32-seed sweep, the four expedition tier hashes, the world goldens, the
  item suite's Correction 1 calibration and every `d1–d4` manifest byte-identical — `MintUnique` is reached only by a
  `unique` draw, and no shipped table draws one while its rows are refused. **Guards:** `guard-power.ps1` (no `location`
  under `Items/Uniques` in `inventory.json`), `guard-dal.ps1`, `audit-overflow` 0 critical, `audit-magic-numbers` zero M1.

### Metrics — closed loops

**Uniques per cell** (`frame 2 × axis 5 × band 3 = 30` cells): target 2–3 per cell → 60–90 at full, first ship 30 beside the
49 already at rung 80+; actual-vs-declared per cell is a pass condition; `AxisCollision` bounds a cell at one per role.
**Drop rate at the boss table per rung** (`ExpectedEquipmentPerMille`'s method, `DropTableModel.cs:235`, on the group):
`E[uniques per clear] = rolls × Σ w_u / (w_nothing + Σ w_u)` — `n` eligible at `exceptional` beside one `staple` is
`7n / (1000 + 7n)`: `n = 6 → 40‰`, `n = 12 → 77‰`. Printed per rung with the mean ordinal of the drawable set; the assertion
is **mean ordinal non-decreasing in the rung's floor column and count > 0 on every offered rung** — never a target number.

## Boundaries

- **Always:** identity from the anchor, every number from code; one `TryInstantiate` at `Θ_room`; the slot atom through
  `stat.derived` on the registered `loadout.slots`; uniques listed by id; `enabled: false` over deletion; the seed-contract
  levels and audit on every field; the per-million gate on a named stream off the instance seed.
- **Ask first:** widening `UniqueLimits.MaxTotalRolls`; re-runging any of the 95; a second chance key; a cross-item
  exclusivity tag on the affix schema; `firstClearRef` on the domain anchor (filed); a `unique` group on `cache`/`elite`
  tables; a `deterministic` unique in any table.
- **Never:** a set item or any set stem on a unique; a magnitude, weight or chance from a model; a second roll beside
  `Instantiator`; a categorical unique grant (`role`/`frame` on a `unique` entry); a `float`/`double`; a private `f(Θ)` or a
  rarity multiplier on a magnitude; a unique below ordinal 80; a unique pity counter; more than `+1` slot per wearer; a
  seventeenth atom kind while `stat.derived` serves.

## Success criteria (G4)

1. Six domains' boss tables carry `boss-unique` groups from the pipelines; audit, budget and byte-identical rerun green;
   every `source-locked` unique in exactly one table.
2. A rolled unique reproduces over `(container, revision, rollSeed, Θ)`; fixed atoms identical across seeds at equal Θ.
3. Rung ≥ 90 always carries the slot atom; rung 80 hits at the micro rate over 1e6 seeds; the wearer reads `5 + 1`, two
   items still `+1`; the frozen amount is unscaled.
4. `UnavailableKinds` no longer lists `Unique`; a `unique` draw mints through `TryInstantiate` with its own `RollSeed`.
5. The 95 are `enabled: false`; `rungFloorOrdinal = 80` re-seeds `unique_eligible` with no code change.
6. Item, battle, expedition and world goldens byte-identical; guards green; no new tuning key.

## Interface exposed to dependents

| Member | Returns | Consumer |
|---|---|---|
| `UniqueContainerBuild.Build(anchor, rollSeed, micro, lookups, out container)` | the `item.` `ContainerRow` | `dungeon-loot` (`Mint` for `unique` grants and the `dungeon-clear` relic); the item import |
| `ExtendSlotRoll.Hit(rollSeed, micro)` | `bool` | the unique arm here; item module 11's normal-drop arm |
| `UniqueDomainListing.For(climate, corpus, themes)` | eligible unique ids for a domain (a filter, ordered) | `DungeonLootTableGen`; `domain-catalog` (picker shows boss drops by name) |
| `DerivedStatChannels.LoadoutSlots = "loadout.slots"` | the registered channel | `LoadoutSet`/`AutoEquip`/`CapPolicy`; the action program's slot read; `delve-stage` (a sixth slot rendered, never the channel id). Footprint stays `loot-pack`'s, from the base type |

## Design-gate checklist

```
[x] Subsystems: item uniques (17), item drops (11), granted actions (19), effect atoms (kinds, Instantiator, derived
    registry), action loadout, seedsmith items adapter, party dungeon loot, tunables, power ladder (PS-2).
[x] Read this session: party-dungeon-map (row 16, G4, external deps, open items); the approved specs (loot, seed-contract,
    registries in full; encounter §6, loot-pack §2, ladder §2/§8); ideal §11.7 :1488-1535, §11.9 #13/#16, §11.10; audit
    S2-9, S2-13, §1(i), §5 #7, §7; decisions.md:113-116; ssot-rarity §3.3-§3.6, §8; ssot-uniques §3.6, §3.8, §4.5, §5.4;
    spec-uniques, spec-granted-actions and spec-affix-channel-weights headers; spec-expeditions; DESIGN-GATE §5.
[x] Code opened and cited by line: every C#, Python and JSON file this spec names by line — 29 C# files across
    Items/{Uniques,Drops,Grants}, Effects/Atoms, Stats/Derived, Actions, Battle, Combat, Power and Data/Sqlite;
    kinds.py, acquisition.py, audit.py; the tags/themes/bands registries, the four tuning and seed JSONs; the 18 unique
    files and the three drop tables (counted from disk).
[x] Verified against CODE, not comments: the 16 kinds by their `new(...)` rows, not the header's "12"; `loadout.slots`
    absent by grep; the flat arm at LootPipeline.cs:274-281; the Freeze arms :313-315; MaxTotalRolls in UniqueRow.cs;
    the 144/95/49 rung split and 64/40/40 acquisition split from disk.
[x] Drift reported, not fixed: (1) map :126 "64 `unique` table rows" — 64 is the source-locked COUNT; the tables carry
    144 unique rows. (2) The brief's "random atoms by the rarity row's count band" contradicts UniqueLimits.MaxTotalRolls
    = 1 and ssot-uniques §3.6 — code followed; widening is ask-first. (3) Decision 13's "action grants as atoms" — no
    atom kind exists; the seam is item_granted_action. (4) The brief's `passiveGrantRef` collapses into fixedAtoms[].
    (5) `affix.exclusiveTags` (decisions.md:116, R7) exists nowhere in code or data. (6) uniques.v1.json rungFloorOrdinal
    is 30 against decision 13's 80. (7) AtomKindRegistry.cs's summary says "12 kinds"; KindCount = 16.
[ ] Constraints not tested — nothing was run; this spec changes no code. "Goldens untouched" is argued from the
    refused-kind path and a delve-only host; the suites are the first build task. The 1e6 band is binomial arithmetic.
[x] No §2 invariant contradicted. Wiring gaps named with the line: the flat Unique arm, the unregistered channel with
    three const readers, the Freeze count-scaling hazard, the ItemGrant container-id arm. One reading added and named:
    rarity columns gate a unique's rung from below only; depth gates the tier.
[x] Propagations landed 2026-09-05 (verification pass): item-map.md §9 (module 17 amendment, module 11 `MintUnique` arm,
    module 19 validator arm); seedsmith-map.md filed section; dungeon-seed-contract §1.1 `firstClearRef`;
    effect-atom-map.md §19 (Freeze count-unit guard); action-map.md §13 (`loadout.slots` read). The 95 flag flips and
    the `rungFloorOrdinal` edit become rows in tasks/party-dungeon-todo.md at the plan phase.
```
