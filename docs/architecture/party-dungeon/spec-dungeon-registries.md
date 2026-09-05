# Spec: dungeon-registries

Status: **APPROVED by the owner 2026-09-05 (wave 1) — gate G0/G1, not built.** First module of wave 1; nothing in this
program is named for build until this lands, because every later module's inputs are ids and
numbers this module owns.

Module id `dungeon-registries` in the [party-dungeon map](../party-dungeon-map.md) (row 1; build
order `party-dungeon-map.md` §Build order, wave 1). Depends on nothing in this program. Implements
the ideal's principle 5 ([party-dungeon-ideal.md](../party-dungeon-ideal.md) §0: *"a missing tunable
is a load rejection, never a default"*), the review's S1-6, S2-10, S2-12, N13, N15 and G20
([audit-2026-09-05.md](audit-2026-09-05.md) §2–§4), and [tunables-ssot.md](../tunables-ssot.md)
T1–T7 as written.

## Objective

Give the Delve its closed vocabularies and its two balance surfaces before any generator exists,
each owned exactly once. Eight closed lists — room kinds, door kinds, override tags, objective
templates, difficulty rung ids, disposition, interaction verbs, raid mode ids — plus the ordinal
band vocabularies the anchors write, become **one JSON registry each** under
`data/seed/dungeon/_registry/`, read by a C# catalog (rules, bans, adjacency) *and* by the seedsmith
validator (legal ids), so review finding G20 (*"room-kind list owned twice"*, audit §4) cannot recur.
`data/tuning/dungeon.v1.json` and `data/tuning/encounter.v1.json` get a schema, a pure parser that
rejects any missing key by name, and a hub with no default — the shape `ExpeditionTuning.cs` already
ships.

Success looks like: `DungeonTuningLoader.Parse(json)` with one key deleted rejects naming that key;
`RoomKindCatalog.All` and the seedsmith `dungeon` adapter report eleven room kinds from the same file
and a test asserts they agree; `wildJoinMilli`, `costPerPull` and boss-lair's `6` appear in neither
new tuning file, because they are read from their owners or replaced by a delta; and
`audit-magic-numbers.py --domain dungeon` reports M1 = 0.

## Locked anchors

Owner decisions this module implements, quoted from the ideal's boxes and the review record:

- **§11.9 #7** — *"'Medium' sits one band below the entrance band. Rung 3 has `bandDelta −1`; `hard`
  (rung 4) is the authored band and becomes the identity row for modifiers … very-easy −2 (floors at
  0), easy −1, medium −1, hard 0, very-hard 0, nightmare +1, hell +1, abyss +2, hopeless +2,
  impossible +3; the tail starts from +3."* → the starting `difficulty.rungs[].bandDelta` column.
- **§11.10 R8** — *"Every rule rung carries a reward-bearing column; the delta column is unchanged
  … validator: neighbouring rungs differ in `bandDelta` or a reward column, never only a penalty …
  `depth.bossBand` becomes `depth.bossBandDelta` on the last corridor's band; a rung whose band would
  clamp on a domain is not offered."*
- **§11.10 R6** — *"Virtual time only — a downed demon sits out a tunable number of delves
  (`risk.downedRecoveryDelves`). The real-time clock is removed."* No key in either file may carry a
  day, hour or minute unit.
- **§11.10 R4** — *"A clear at `maxRungWithoutOath` itself opens the next rung."* §5's
  `risk.oath.bandUnlock` is retired; `domain.maxRungWithoutOath` is a rung id, never a band count.
- **§11.9 #15 / §11.10 R2, R12** — once-entry domains carry *"an entrance `bandDelta` of +7 and a
  rarity floor on their boss table — both tunables (`domain.onceEntry.bandDelta`,
  `domain.onceEntry.bossRarityFloor`), never literals"*; *"`onceEntry.failKeepsBossLoot: true` and
  `onceEntry.sealOnWipe: true`, both tunables."*
- **§11.9 #16 / §11.10 R7** — *"Keep the 0.01% as `loot.extendSlotChanceMicro: 100` on a per-million
  stream (`CombatProbability.cs:15` precedent)."*
- **§5 (owner clarification)** — `events.provisionSlots` retired: provisions occupy pack cells.
- **Review S2-10 (N13, N15)** — *"Three knobs authored three times (retinue, `bossW`, boss band); four
  copied numbers (`wild.joinMilli`, `bossBand`, `altar.pullPriceSouls`, `provision.price*`) … One owner
  each, the rest read; `graph.minDeadEnds` and `summon.capPerBoss` relabelled tunable; units in key
  names."*
- **Review S2-12** — *"`weightBand` is a second frequency vocabulary with a deny-listed stem …
  Rename to `dropBand`; stem check `*weight*`/`*chance*`; spelled-number list."*

## Design

### Registry files

`data/seed/dungeon/_registry/<name>.v1.json`, one vocabulary per file, the envelope
`data/seed/demons/_registry/families.v1.json:2-3` already uses (`schemaVersion`, `registryVersion`):

| File | Members (starting shape) | Row fields beyond `id` |
|---|---|---|
| `room-kinds.v1.json` | `fight · elite · cache · curio · wild · shrine · rest · merchant · trap · unknown · boss` (ideal §11.1) | `climateNeutral` (true for `rest · merchant · boss · unknown`), `secretEligible` (true for `cache · shrine · merchant`), `bossRowAllowed` (true only for `boss`), `neverAdjacentTo[]`, `unknownResolvesTo[]` (on `unknown`: `cache · merchant · fight`) |
| `door-kinds.v1.json` | `passage · gated · one-way · secret` | `gated`, `oneWay`, `hidden` — the same flags `LaneTypeDef` carries (`LaneTypeCatalog.cs:23-26`), so `DoorTypeCatalog` rows are `LaneTypeDef`s as row P2 requires |
| `override-tags.v1.json` | `herbs · key · holy · bait · watch` (ideal §11.5) | — |
| `objective-templates.v1.json` | the nine of ideal §11.3 | `targetKind` ∈ `room-kind · curio-kind · item-kind · boss · none`, `sinkAvoidance` (true for `finish-under-hunger · survive-no-downed · spend-no-provision`, audit §4 D14) |
| `difficulty-rungs.v1.json` | `very-easy · easy · medium · hard · very-hard · nightmare · hell · abyss · hopeless · impossible` | `ordinal` 1–10. **No number of any other kind** — every column lives in `dungeon.v1.json` |
| `disposition.v1.json` | `eager · open · wary · hostile` (ideal §11.6) | — |
| `interaction-verbs.v1.json` | `open · disarm · pray · loot · destroy · garrison` (ideal §11.5) | `decision` (the base-defense decision number that admits the verb) |
| `raid-modes.v1.json` | `solo · pair · quad` | — |
| `bands.v1.json` | the ordinal vocabularies the anchors write: `dangerBand`, `depthBand`, `widthBand`, `branchiness`, `density`, `hazardBand`, `sightBand`, `countBand`, `elementSpread`, `formation`, `eventKind`, `outcomeOrdinal`, `repeatScope`, `entry`, `phasing`, `questScope`, `rewardBand`, `deltaBand`, `hpBand`, `nerveStage` (`unsettled · shaken · afflicted`, added by `delve-attrition`) | one `enum[]` per band, the shape of `data/seed/items/_registry/bands.v1.json:451-460` |

`bands.v1.json` is the one file beyond the map row's list: every `bands.<band>.<member>` tuning key
needs a required-member list, and a C# loader hardcoding `shallow · mid · deep · abyssal` beside a
seedsmith schema listing them too is G20 with a different noun. Two rules every registry enforces on
itself: **`none` is admitted by every closed enum by contract and is never a registry row** (ideal §10
law 6; audit §4 G8), and **no member may be a spelled number** — `countBand` is `lone · few · several ·
many`, `phasing` is `none · breakpoint · escalating`, never `one · two · three` (S2-12). Which anchor
*field* uses which registry is `dungeon-seed-contract`'s table; this module owns only the vocabularies.

### C# catalogs

`src/FusionRpg.Core/Dungeon/Registry/` — one `*Catalog` per registry, the `SlotTypeCatalog` shape
(`SlotTypeCatalog.cs:45-117`) with one change: rows come from the loaded registry
(`DungeonRegistryHub.Configure(DungeonRegistries)`), not a compiled `Seed`. Numbers a row needs — a
room kind's `weightMilli`, `earliestRowMilli` — are **joined from `DungeonTuningHub.Tuning`** at first
read, exactly as `LaneTypeCatalog.Seed` reads `WorldTuningHub.Tuning.LaneCostMultiplierMilli`
(`LaneTypeCatalog.cs:49-58`). A registry row never carries a magnitude.

`Validate` throws (`InvalidOperationException`, the catalogs' own type) on: a non-kebab id
(`WorldIds.RequireKebab`, `WorldIds.cs:6-14`); a duplicate id (`SlotTypeCatalog.cs:101-102`); a
`neverAdjacentTo`, `unknownResolvesTo` or `targetKind` naming an unknown id (the cross-catalog check
at `SectorTypeCatalog.cs:134-137`); exactly one `bossRowAllowed` kind; a `secretEligible` kind that is
also `bossRowAllowed`; a rung registry whose ordinals are not `1..n` contiguous; a tuning file whose
`difficulty.rungs[]` ids, `nodes.*` keys, `raid.modes.*` keys or `bands.*.*` keys are not **exactly**
the registry's member set (missing → rejected naming the key; extra → rejected naming the key).
`Get(unknownId)` throws `ArgumentException` (`SlotTypeCatalog.cs:59-62`); there is no `TryGet` that
returns a stand-in row.

### Tuning schema — `data/tuning/dungeon.v1.json`

Key naming follows T6; the `_meta` block copies `expeditions.v1.json:4-8` with `owner` set to this
spec. Every key below is **required** (T5). Class: **T** tunable · **S** structural (carries the
exemption comment in `_meta.structural[]` and in the loader). All values are starting shapes, not a
balance decision. Types: `int` for bands, counts, rows, rung ordinals, Θ deltas; `long` for every
per-mille and every soul or item magnitude (see §Numeric types).

| Block · keys | Unit / type | Class | Owner note |
|---|---|---|---|
| `raid.modes.{solo,pair,quad}.parties` · `.squadSlots` · `.walksDelta` · `.bossW` | count int · count int · count int · concurrency int | T | `bossW` is the boss room's width per raid; `tempo` never overrides it (see encounter). `walksDelta` adds to `bands.branchiness.*.pathWalks` — the layout owns the base, the raid adds (ideal §11.1: raid changes walks, never rows). **Retired:** `raid.modes.*.bossRetinuePerParty` — retinue is the encounter anchor's `countBand` + the rung delta (S2-10). **Added by `encounter-generator` (wave 2, decision 3's own name):** `raid.modes.{pair,quad}.bossShieldPerPartyMilli` — ‰ of `P(Θ_room)` per extra party, long, starting shape 300; absent on `solo` by schema, not by default |
|`preflight.sampleSeeds` (added by `domain-catalog` §2, wave 4)|seeds int|S — `_meta.structural[]`: *validator depth, never a balance lever*|How many seeds per (layout tier × raid mode) the import preflight rolls before a domain may be offered; 32, the count the coverage metrics use|
| `raid.modes.*.pack.{rows,cols}` | cells int | T | Per-delve carry grid. `_meta` note: *structural per-run limit, not a progression ceiling; the armoury is uncapped* (ideal §11.7 (2), caps register exemption) — tunable in location so `loot-pack` may scale or pin it (S2-6) |
| `bands.dangerBand.{shallow,mid,deep,abyssal}` | `DangerBand` int (`ContentContext.cs:16`) | T | The entrance band an authored ordinal resolves to; feeds `PowerIndexComposer.cs:72` |
| `bands.depth.{short,medium,long}.rows.{min,max}` · `bands.width.*.cols.{min,max}` · `bands.branchiness.{linear,forked,webbed}.pathWalks` | rows int · cols int · walks int | T | Replaces §5's `graph.tiers.*` (ideal §11.1 supersedes) |
| `bands.{gate,secret,oneWay}Density.{none,sparse,dense}.perRoomMilli` | ‰ long | T | |
| `bands.sightBand.{dim,lit,scouting}.extraLanes` · `sight.lanes` · `sight.scoutLanes` | lanes int | T | Read here, never `Visibility`'s const (ideal §11.1) |
| `bands.hazardBand.{none,light,heavy}.hungerPerMille` | ‰ of max hunger, long | T | **The one owner of hunger per room.** §5's `attrition.hunger.perRoom.{kind}` is retired — hazard is a room property, kind is not (duplicate owner) |
| `graph.fixedRows.midCacheRowMilli` | ‰ of rows, long | T | §5's `graph.cacheRow`. `restRowBeforeBoss` is **not a key**: §4.2 calls the guaranteed row structural; it is a validator rule in `delve-graph-roll` |
| `graph.secretAppearMilli` · `graph.minDeadEnds` | ‰ long · count int | T | `minDeadEnds` relabelled tunable per S2-10. **`graph.secretAttachAnyMilli` does not exist** — it was the same number as `bands.secretDensity.*.perRoomMilli` under a second owner (N13; filed by `delve-graph-roll`) |
| `nodes.{kind}.{weightMilli,earliestRowMilli,latestRowMilli}` — all eleven | ‰ long · ‰ of rows long | T | Keys = `room-kinds.v1.json` members, checked both ways. `boss.weightMilli` is 0 (the boss row is fixed); still required, never implied |
| `nodes.unknown.pity.{cache,merchant,fight}.{baseMilli,stepMilli}` | ‰ long | T | Keys = the `unknownResolvesTo` row; **consumer is `event-deck` §4**, never the roller (per-party counters, R11) |
| `depth.rowsPerBandStep` · `depth.bossBandDelta` | rows int · band int | T | S2-2: the prose rate becomes a key; **`bossBandDelta` replaces `depth.bossBand`** — no copy of `SectorTypeCatalog.cs:98`'s 6 (N13) |
| `attrition.spirit.{perEliteMilli,bossPresenceMilli,retreatMilli}` · `attrition.restHealMilli` | ‰ of max spirit / max pool, long | T | `risk.retreatSpiritCost` (§5) is retired as a duplicate of `retreatMilli` |
| **Added by `delve-attrition` (wave 2):** `attrition.nerve.stageThresholds[]` · `attrition.nerve.stackPer{Elite,Boss,Retreat,Curio}` · `attrition.nerve.restRelief` · `attrition.revive.hpMilli` · `attrition.persistAcrossDelves[]` · `rest.healsPools[]` | stacks int (strictly increasing, length = `nerveStage` count) · stacks int · stacks int · ‰ of max hp long · resource ids · resource ids | T | The two id lists are ⊆ `DerivedStatChannels.ResourceIds`, checked at load; the loops still visit all six. Starting shapes `[1,3,5]` · 1/2/1/1 · 2 · 250 · `["hunger"]` · `["hp","hunger","spirit"]` |
| `risk.downedRecoveryDelves` · `risk.recoveryRitualSouls.{ten rung ids}` | delves int · souls long | T | R6: no day unit anywhere in the file (loader rejects any key matching `Day|Hour|Minute|Ms`). Ritual base price is scaled at use through `SoulSinkPolicy.Price(long, int, PowerTuning)` (`SoulSinkPolicy.cs:40`). Rung ids = `core.v1.json:26-180` |
| `events.noRepeatRooms` · `events.offeredPerRoom` · `quests.offeredAtEntry` | count int | T | **No `events.dropBand.*` keys:** outcome weights resolve through `data/seed/items/_registry/bands.v1.json:463-470`'s `weightTable` (S2-12's rename makes the items registry the owner) |
|**Added by `delve-quests` (wave 4):** `quests.autopilotCompletionBand.{min,max}Milli`|‰ long|T|A regression band on autopilot completion per template over 32 seeds — never a target; starting shape 300 / 900. A balance pass moves completion through `countBand` ordinals on anchors, never through code|
| **Added by `event-deck` (wave 3):** `events.climateAffinity.{match,none,off}Milli` · `bands.hpBand.{low,half,high}.milli` | ‰ relative weight, long · ‰ of max hp, long | T | Affinity weights, never gates (seed contract §1.4); `hpBand` maps an anchor ordinal to the `HpBelowMilli`/`HpAboveMilli` leaf argument. Starting shapes 1000/1000/500 · 250/500/750 |
| `quests.countBand.{few,some,most,all}Milli` · `quests.rewardBand.{modest,fair,rich}.{floorRung,ceilRung}` | ‰ of rooms long · rung id | T | Rung ids from the ten-rung ladder (`item-rarity.v1.json:7-18`) |
| `wild.outcome.{eager,open,wary,hostile}.{joins,takesLeaves,flees,attacks}Milli` | ‰ long, rows sum to 1000 (loader-checked) | T | **`wild.joinMilli` does not exist.** The talk's disposition table replaces expeditions' one coin (`ExpeditionTuning.cs:59`); nothing here reads or copies `expeditions.v1.json:19` |
| `wild.deltaBands[]` · `wild.deltaShiftRungs[]` · `wild.tide.{enabled,shiftRungs[]}` · `wild.offerPreference.{personality}.{souls,spirit,item,demon}` · `wild.offer.spiritPerSoulMilli` · `wild.offer.soulsMilliOfPullPrice` · `wild.provisionOverrideTag` | Θ int · rungs int · bool + rungs int · ordinal `craves·accepts·scorns` · ‰ long · ‰ long (≥ 1000, loader-checked) · override-tag id | T | Personality keys = `contracts.v1.json` `personalityRates` members, checked at load. **`soulsMilliOfPullPrice` is how R5's floor is expressed:** the price itself is `summoning.v1.json` `banners[altar.bannerId].costPerPull` (`SummoningTuning.cs:56`) through `SoulSinkPolicy.Price` at Θ_room — never a soul number here |
| `capture.usableBelowMilli` · `capture.chanceMilli[hpBand][deltaBand]` · `capture.statusBonusMilli[countBand]` · `capture.sealTierShiftBands[]` · `capture.failStepBands` | ‰ long · ‰ long table · ‰ long · ‰ long · bands int | T | Table axes = `bands.v1.json` members, dimension-checked |
|**Added / corrected by `wild-room` (wave 4):** `wild.talk.maxSteps` · `wild.talk.flatterMilli` · `wild.autopilot.rule` · `wild.cageMilli`; `wild.offer.spiritPerSoulMilli` (was `spiritMilli` — a ‰-of-max with no soul equivalence); `capture.sealTierShiftBands[]` (was `…ShiftMilli[]` — a seal shifts a band index, not a ‰)|steps int · ‰ long · id ∈ {`fight`, `leave-hostile`} · ‰ long; spirit units per soul (‰ long); bands int per seal tier|T|Starting shapes 2 · 500 · `fight` · 150; 2000; `[0, −1, −2]`. Every wild-room price still ends in `SoulSinkPolicy.Price` through `dungeon-loot`'s `DelvePrices`; no soul literal here|
| `altar.bannerId` · `altar.poolFromDomain` | banner id · bool | T | **`altar.pullPriceSouls` does not exist** (S2-10). `sharedPity` is not a key: a boolean that must be `true` is *"a literal with extra ceremony"* (tunables-ssot §6) — it is a rule in `wild-room` |
| `merchant.markupMilli` · `rest.activations` · `rest.ambushMilli` | ‰ long · uses int · ‰ long | T | **No `provision.price*` or `merchant.priceSouls.*`:** price is DERIVED from item class × grade × `contentScale(Θ)` on the item side (ideal §11.5, `seed-contract.md` §2.1); the Delve contributes only the markup |
| **Added by `supplies-and-objects` (wave 3):** `objects.breakMode` · `objects.breakStaminaMilli` · `objects.structureHpBand` · `merchant.stockCount` | enum `none · stamina · structure · either` (per-domain override) · ‰ of max stamina, long · `countBand` ordinal · count int | T | `breakMode` picks how a gated door breaks; the Structure's `MaxHp` is `BattleRuleset.BaseHp(θ)` through `encounter-generator`, the band only names the count. Starting shapes `either` · 400 · `some` · 4 |
| `pack.footprint.role.{fifteen item roles}` · `pack.footprint.massStep.{five mass classes}` · `pack.footprint.consumableClass.{restore,draught,ward,board,revive,utility}` · `pack.stack.consumableClass.*` · `pack.stack.materialClass.*` | cells int (a `ShapeLadder` member `{1,2,3,4,6,8}`) · **ladder steps** int (unit corrected 2026-09-05, `loot-pack` §2) · cells int · count int · count int | T | Keys are item-registry vocabularies (`tags.v1.json:64` `mass-class`; `consumables/k1.json:28` `classId`), read at load and checked — never restated in a dungeon registry |
|**Added by `loot-pack` (wave 3):** `pack.provision.baseCells` · `pack.autopilot.floorRule` · `pack.autopilot.swapMarginMilli` · `pack.fillBand.identity.{min,max}Milli`|cells int (≤ rows×cols) · rule id ∈ {`value-per-cell`, `leave`} · ‰ long · ‰ long|T|Provisioning allowance base (DD's sixteen) plus the rung's `provisionCellsDelta`; the autopilot floor rule is a closed id list; the fill band is a regression band on `PackFill.Estimate`, tuned through loot-table `rolls`, never grid size. Starting shapes 16 · `value-per-cell` · 250 · 700/1000|
| `loot.rooms.{elite,boss}.rarityFloor` · `loot.rooms.boss.rarityShiftRungs` · `loot.rooms.{kind}.affixChannel` · `loot.bossGrantDistribution` · `loot.extendSlotChanceMicro` | rung id · rungs int · `drop·boss` · rule id (`round-robin`) · per-million long | T | `extendSlotChanceMicro: 100` per R7; drawn on a per-million stream (`CombatProbability.cs:15`) |
| `difficulty.rungs[]` — one object per registry id: `bandDelta` · `{eliteWeight,restWeight,restHeal,hunger,spiritDrain,merchantMarkup}MultMilli` · `wildDispositionShiftRungs` · `enemyCountDelta.{fight,elite}` · `bossRetinuePerPartyDelta` · `bossWDelta` · `provisionCellsDelta` · `{restEveryOtherRow,restRowsOnlyBeforeBoss,doubleBoss}` · `{eventSeverityTier,eliteKitTier,bossKitTier}` · `unknownPityStepMultMilli.{cache,merchant,fight}` · **reward columns** `rarityFloor` (rung id or `none`) · `rarityShiftRungs` | band int · ‰ long (1000 = identity) · rungs int · counts int · count int · width int · cells int · bool · tier int · ‰ long · rung id · rungs int | T | `hard` is the identity row (#7): every `*MultMilli` 1000, every delta 0. Validator: neighbours differ in `bandDelta` **or** `enemyCountDelta`/`rarityFloor`/`rarityShiftRungs` (R8); no rung key may name an actor axis (ideal §11.2); a rung whose band would floor below 0 on a domain is refused, not clamped |
| `difficulty.tail.{enabled,startsAfterRung,bandStepPerPlus,rulesFrozenAtRung}` | bool · rung id · band int · rung id | T | No upper bound key exists (PS-8); the only bound is `PowerLadder.MaxIndex`, which throws. The `"abyss +{n}"` label is the stage's copy, not tuning |
| `domain.maxRungWithoutOath` · `domain.permadeathFromRung` · `domain.onceEntry.{bandDelta,bossRarityFloor,sealOnWipe,failKeepsBossLoot}` | rung id · rung id · band int · rung id · bool · bool | T | Program-wide rows (R4, R3, #15, R12). A per-domain tightening, if `dungeon-seed-contract` adds one, is a VALIDATED rung id on the anchor — never a second number |

`budget.roomsPerCell.*` (seedsmith's skew guard) is not a key here — Core would load it and never
read it; it lives with the `dungeon` adapter.

### Tuning schema — `data/tuning/encounter.v1.json`

| Block · keys | Unit / type | Class | Owner note |
|---|---|---|---|
| `slot.countBand.{lone,few,several,many}.{min,max}` | count int | T | Also resolves `retinueRule.perParty` — one vocabulary, one table (S2-10) |
| `threatWindow.bossFloorRung` | threat rung id (`demon-threat.v1.json:3-14`) | T | **No `threatWindow.defaultRungs`:** a default window is a default (T5); `threatWindow` is required on every encounter anchor. `DemonThreatTuning.OffsetFor`'s fallback (`DemonThreatTuning.cs:19-29`) is the wiring gap `threat-audit` closes, not something this file papers over |
| `spread.{mono,dual,rainbow}.offClimateMilli` | ‰ long | T | Climate = `ElementTypeId` or `none` (`ActorElementTypes.cs:3-11`) |
| `formation.{pack,party}.w` · `formation.party.slots.{min,max}` · `formation.party.maxRepeatedPosture` · `formation.boss.rankSpan` (added by `encounter-generator`: ranks int, starting shape 2) | width int · count int · count int | T | **The one owner of `W` outside the boss room.** §11.4's `tempo.*.wOverride` is retired: it would key on `attackTempo`, a vocabulary owned by seedsmith's schema (`anchor/schema.py:143`) that Core cannot read — `tempo` stays a species filter |
| `boss.fightLengthTargetRounds.{min,max}` | rounds int | T | The calibration target for the shield share; the share's derivation from the `W` ratio is `encounter-generator`'s and adds a key here only through its own spec |
| `affix.{elite,boss}KitTier.{t1,t2,t3}.{affixCount,floorRung,ceilRung}` | count int · rung id · rung id | T | *"A count band, a tier floor, a tier ceiling. Nothing else"* (ssot-rarity §3.6). `affix.exclusiveTags` is the affix library's row, not this file's |
| `phase.{breakpoint,escalating}.hpThresholdMilli[]` | ‰ long | T | Length 1 and 2 respectively, loader-checked |
| `summon.capPerBoss` · `pack.sameSpeciesMaxMilli` | count int · ‰ long | T | `capPerBoss` relabelled tunable (S2-10). `rank.spanMax` is **not a key**: it is derived from `raid.modes.*.squadSlots` |

### Loader and hub

`DungeonTuning.cs` / `EncounterTuning.cs` in `src/FusionRpg.Core/Dungeon/Tuning/`: a sealed record
per block, a `*TuningRejection : Exception` (`ExpeditionTuning.cs:20-23`), a pure `Parse(string json)`
with the `Obj`/`Int`/`Long` helpers of `ExpeditionTuning.cs:68-80` and `WorldTuning.cs:210-215`, each
rejection naming the full dotted path (`"dungeon tuning: missing or non-integer 'depth.bossBandDelta'"`),
and a hub whose getter throws when `Configure` has not run (`ExpeditionTuningHub`,
`ExpeditionTuning.cs:85-95`). Hosts load: `Program.cs:71-73` and `RpgHost.cs:99-100` gain the two
files beside `expeditions.v1.json`; tests construct inline (`ContractTuningTestBootstrap.cs:64`).
Registries load the same way (`DungeonRegistryLoader.Parse(name, json)`); Core never touches a path
(tunables-ssot §7.2).

### The seedsmith side

The `dungeon` adapter reads `data/seed/dungeon/_registry/*.json` **fresh on every call**, the
`adapters/items/registries.py:1-6` discipline (*"read, never transcribed"*), and never the
`adapters/demons/registries.py:3-9` shape (a mirror of C# enums, pinned by count). Every brief carries
the literal legal list generated from the registry at emit time (`spec-pipeline.md` §3.3), and every
anchor's `_provenance.registryVersions` records the `registryVersion` of each file it validated
against, so a registry bump identifies exactly the anchors to re-run.

## Numeric types

Per the overflow thresholds computed from the shipped curve (`B = 0.4`): `float` stops being
integer-exact at `Θ = 232` and `int` per-mille at `Θ = 3,213`, so **`long` for every per-mille and
every soul, price or item magnitude**
(`hungerPerMille`, `*Milli`, `*MultMilli`, `recoveryRitualSouls.*`, `extendSlotChanceMicro`) — a ‰ is
a factor the runtime multiplies into a `P(Θ)` magnitude, and widening before the multiply means the
rate side is already `long`. **`int` for bands, counts, rows, cells, rungs, Θ deltas** — bounded by
the graph or the ladder (`ContentContext.DangerBand` is `int`, `ContentContext.cs:16`). Parsers use
`TryGetInt64`/`TryGetInt32` exactly; a fractional value is a rejection, never a truncation. No `double`
anywhere in either schema.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Dungeon"   # loaders, catalogs, agreement
python tools/tuning/publish.py dungeon depth.bossBandDelta=3                   # writes dungeon.v2.json (T4)
python scripts\audit-magic-numbers.py --domain dungeon                        # M1 must be 0
cd tools\seedsmith; python -m pytest tests\test_dungeon_registries.py          # ids agree with the JSON
```

`publish.py` refuses to invent a key (`publish.py:128-131`): a new tunable is a schema change here
first. **Correction (verified 2026-09-05):** `domain_of` (`audit-magic-numbers.py:173-180`) matches a
fixed tuple of folder names and otherwise falls back to the *immediate* parent directory, so
`src/FusionRpg.Core/Dungeon/Registry/X.cs` would report as `registry` and `…/Dungeon/Tuning/X.cs` as
`tuning`. The module adds `"Dungeon"` to that tuple — a one-line script edit, filed in this module's
task list — so `--domain dungeon` covers both subfolders.

## Structure

```
data/seed/dungeon/_registry/            → room-kinds, door-kinds, override-tags, objective-templates,
                                          difficulty-rungs, disposition, interaction-verbs, raid-modes,
                                          bands  (all *.v1.json)
data/tuning/dungeon.v1.json · encounter.v1.json
src/FusionRpg.Core/Dungeon/Registry/    → DungeonRegistries.cs (records + loader + hub) and one
                                          <Vocabulary>Catalog.cs per registry file (nine)
src/FusionRpg.Core/Dungeon/Tuning/      → DungeonTuning.cs, EncounterTuning.cs
src/FusionRpg.Server/Program.cs · src/FusionRpg.Injector/Host/RpgHost.cs   → the Configure lines
tools/seedsmith/seedsmith/adapters/dungeon/registries.py
tests/FusionRpg.Core.Tests/Dungeon/     → see Testing strategy
tools/seedsmith/tests/test_dungeon_registries.py
```

## Code style

Catalog rows are init-only records with no magnitude; numbers arrive through the hub, as
`LaneTypeCatalog` does:

```csharp
public sealed record RoomKindDef
{
    public string RoomKindId { get; init; } = "";
    public bool ClimateNeutral { get; init; }
    public bool SecretEligible { get; init; }
    public bool BossRowAllowed { get; init; }
    public IReadOnlyList<string> NeverAdjacentTo { get; init; } = Array.Empty<string>();
    // Joined at first read from DungeonTuningHub.Tuning.Nodes[RoomKindId] — never authored here.
    public long WeightMilli { get; init; }
}
```

```json
{ "id": "unknown", "climateNeutral": true, "secretEligible": false, "bossRowAllowed": false,
  "neverAdjacentTo": [], "unknownResolvesTo": ["cache", "merchant", "fight"] }
```

Voice: pure parsers, no logging, rejections name the dotted key, comments say *why* a constant is structural (T2).

## Testing strategy

- **Every-key-required, per file.** A generator walks the shipped `dungeon.v1.json` and
  `encounter.v1.json`, deletes one leaf at a time, and asserts `Parse` throws a rejection whose message
  contains that leaf's dotted path. One test, N cases, no hand-maintained list. Same harness: a
  `1.5` or a string digit where an integer is expected rejects.
- **Duplicate id rejection** per registry; **unknown cross-reference rejection** (`neverAdjacentTo`,
  `unknownResolvesTo`, `targetKind`); **exactly one `bossRowAllowed`**; **contiguous rung ordinals**.
- **Registry ↔ tuning agreement.** `difficulty.rungs[].id`, `nodes.*`, `raid.modes.*`, `bands.*.*`,
  `wild.outcome.*`, `nodes.unknown.pity.*` equal the registry member sets — both directions.
- **Registry ↔ seedsmith agreement.** `test_dungeon_registries.py` loads the same JSON and asserts the
  adapter's vocabularies equal it; a C# test asserts `RoomKindCatalog.All.Count == 11` **as a canary
  beside** the formula (`== registry.RoomKinds.Count`), the `DerivedStatRegistryTests` pattern.
- **Rung validator.** The shipped table passes R8; a fixture with two rungs differing only in
  `hungerMultMilli` is rejected; a rung with `bandDelta` that floors a `shallow` domain below 0 is
  reported as *not offered*, never clamped.
- **No copied number.** A test reads every other `data/tuning/*.json` and asserts the two new files
  contain no key named `wildJoinMilli`, `costPerPull`, `pullPriceSouls`, `bossBand`, `price*`, and no
  key whose name matches `Day|Hour|Minute|Ms` (R6).
- **Stem check.** No registry member matches `*weight*` or `*chance*`; in tuning only `nodes.*.weightMilli`
  may (S2-12's stem rule is for anchors — the tuning key is the legal home of the number).
- **Hub without Configure throws** with the message naming the file (`ExpeditionTuning.cs:92-94`).
- **Audit gate.** `audit-magic-numbers.py --domain dungeon` M1 = 0 and M4 = 0 for the new files.

## Boundaries

- **Always:** one registry per vocabulary, read by both consumers; every tuning key required; units
  in every key name; `long` for anything a magnitude touches; a number two domains need is read from
  the domain that owns the concept; new keys enter through this spec and `publish.py`, never a
  hand edit.
- **Ask first:** adding a registry member (a room kind carries catalog rules, tunables and a HUD
  affordance — ideal §11.1 calls kind *"the expensive axis"*); adding a tuning key another module's
  spec did not derive; changing a starting value (T7: never in the same change as code).
- **Never:** a default for a missing key; a second registry for the same vocabulary (including a C#
  enum that mirrors a registry file); a number copied from another domain's tuning file; a `float` or
  `double`; a day/hour/minute unit; a spelled number as an ordinal; a magnitude on a registry row.

## Success criteria

1. Nine registry files load through `DungeonRegistryLoader`; every catalog's `Validate` passes on the
   shipped files and every rejection case in §Testing is red-then-green.
2. `dungeon.v1.json` and `encounter.v1.json` parse; deleting any leaf produces a rejection naming it.
3. The registry ↔ tuning and registry ↔ seedsmith agreement tests pass from the same JSON.
4. `grep -rn "wildJoinMilli\|costPerPull\|pullPriceSouls\|bossBand\b" data/tuning/dungeon.v1.json
   data/tuning/encounter.v1.json` returns nothing; `depth.bossBandDelta` exists.
5. `audit-magic-numbers.py --domain dungeon` reports M1 = 0, M4 = 0.
6. Both hosts `Configure` both hubs at startup; a grep for `File.` under `Core/Dungeon/` is empty.
7. `publish.py dungeon <key>=<value>` writes `dungeon.v2.json` and refuses an unknown key.

## Interface exposed to dependents

| Dependent | Reads |
|---|---|
| `dungeon-seed-contract` | every registry's member set (legal ids per anchor field), `registryVersion` per file for `_provenance`, the `none`-admitted and no-spelled-number rules |
| `delve-graph-roll` | `RoomKindCatalog` (`SecretEligible`, `BossRowAllowed`, `NeverAdjacentTo`, `UnknownResolvesTo`, joined `WeightMilli`/`EarliestRowMilli`/`LatestRowMilli`), `DoorKindCatalog` flags, `raid.modes.*.walksDelta`, `bands.{depth,width,branchiness,*Density,sightBand}.*`, `graph.*`, `sight.*` |
| `delve-scope` | projects `RoomKindCatalog`/`DoorKindCatalog` into the `SectorTypeDef`/`LaneTypeDef` lists it names `RoomTypeCatalog`/`DoorTypeCatalog` for `WorldValidation` rules 1 and 6 — one registry, two views (`decisions.md:114`) |
| `difficulty-ladder` | `DifficultyRungCatalog` ordinals, `difficulty.rungs[]`, `difficulty.tail.*`, `depth.*`, `bands.dangerBand.*`, `domain.*` |
| `encounter-generator` | `encounter.v1.json` whole, `raid.modes.*.{squadSlots,bossW,parties}`, `difficulty.rungs[].{enemyCountDelta,bossRetinuePerPartyDelta,bossWDelta,eliteKitTier,bossKitTier}` |
| `delve-attrition` · `event-deck` · `dungeon-loot` · `loot-pack` · `wild-room` · `delve-quests` | `attrition.*` / `risk.*` · `events.*` + `nodes.unknown.pity.*` (per party, R11 — the deck draws it, not the roller) + `DispositionCatalog` · `loot.*` · `raid.modes.*.pack.*` + `pack.*` · `wild.*` + `capture.*` + `altar.*` · `quests.*` + `ObjectiveTemplateCatalog.SinkAvoidance` |

## Design-gate checklist

```
[x] I identified the subsystem(s) this touches — tunables, world catalogs (shape reuse), seedsmith registries, power ladder (DangerBand int only).
[x] I read every doc in the §1 row(s) for those subsystems, this session — tunables-ssot.md, spec-pipeline.md §3, the ideal §0/§5/§10/§11.1–§11.7/§11.9/§11.10, the audit §2–§4, the map, spec-expeditions.md, DESIGN-GATE §5.
[x] I checked decisions.md for a lock covering this — :52 (magic numbers), :113-116 (the four party-dungeon rows); none contradicted.
[x] Every factual claim cites file:line — code claims do; ideal/audit claims cite section and finding id.
[x] I verified claims against CODE, not comments — ExpeditionTuning.cs, WorldTuning.cs, the three World catalogs, SummoningTuning.cs, SoulSinkPolicy.cs, CombatProbability.cs, ContentContext.cs, PredicateNode.cs, DemonThreatTuning.cs, publish.py, audit-magic-numbers.py, both registries.py files, all opened.
[x] I read the surrounding section of every rule I quoted — tunables-ssot §1/§2/§3/§6/§7.2 read whole; spec-pipeline §3 read whole.
[ ] I tested (not assumed) any constraint I am reporting — nothing here claims a golden moves; the one runtime claim (`--domain dungeon` needs no script edit) is read from `domain_of` at :173-180, not run. Honest gap: not executed.
[x] Nothing contradicts a §2 invariant, or I named the contradiction explicitly — `raid.modes.*.pack.{rows,cols}` sits in a tuning file while the ideal calls it structural; resolved above (tunable in location, caps-register exemption in `_meta`) so `loot-pack` can decide S2-6 without a schema change.
[x] Corrections are propagated to prose, Structure, Testing, Boundaries, map, and tasks — within this file; the map row already names this module's scope; tasks are the plan phase's.
```
