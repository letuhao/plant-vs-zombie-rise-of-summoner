# Spec: dungeon-loot

Status: **APPROVED by the owner 2026-09-05 (wave 3) — unbuilt.** Written against shipped code the
same day; every `file:line` below was opened this session; drift against the brief and earlier specs is
reported in the checklist. Every number is a starting shape, never a balance decision.

Module id `dungeon-loot`, row 10 of the [party-dungeon map](../party-dungeon-map.md) (`:120`; wave 3, beside
`event-deck`, `:139`). Depends on `delve-graph-roll` (`(row, col, kind, archetypeId, keyForLaneId)`, the reserved
`dungeon:loot:{r}:{c}` stream — `spec-delve-graph-roll.md:130-132, :339-342`), `difficulty-ladder`
(`RoomTheta.Compose`, `RungTable.Get`, the reward columns — §2, §8), `delve-scope` (`rpg_delves`, `CloseDelve`,
`RpgStore.Delve.cs` — §1, §7), `dungeon-registries` (`loot.*`, `merchant.*`, `altar.*`, `wild.offer.*`,
`risk.recoveryRitualSouls.*`, `difficulty.rungs[]`), `encounter-generator` (`BattleActorSetup.Level = θ_enemy`,
`spec-encounter-generator.md:446`), `delve-battle-profile` (`DelveBattleResult.Report`, the retreated reading —
§6, `:361-367`), `delve-attrition` (§7 ritual, §8 wipe, §9 victory rule). External: the contracts-on-Θ
follow-up (`party-dungeon-map.md:99`). Gate **G3** (`:159`). Ideal: §4.6, §4.9, §6, §11.3, §11.6, §11.7, §8 box,
§11.9 #12/#13/#15, §11.10 R2/R5/R7/R12. Review: §1(c), §1(e), §1(i), S1-1, S2-3, S2-7, S2-8, S2-10, N6.

## Objective

Turn a delve into value, priced on the one ladder, through the shipped funnels and nothing else. This is the
**first production host of `LootPipeline`**: `LootPipeline.Resolve` (`LootPipeline.cs:134`) and
`Instantiator.TryInstantiate` (`Instantiator.cs:98`) have no production caller, and the only callers of
`SoulEarnPolicy.KillEarn`/`MatchEndEarn` read the pin (`RpgStore.Souls.cs:57-58, :70`). Success: a `fight` room
on a `rich` domain at `hard` pays `KillEarn(83)` per raider into the at-risk ledger and drops item level `70 ± 1`
from the domain's fight table; the boss table drops with its floor raised and window shifted; extraction banks
the ledger plus `MatchEndEarn(true, 100)` once; a wipe banks nothing; two row-1 rooms then extract lose to a
clean run on souls per minute; every battle, expedition, world and item golden is byte-identical.

## Locked anchors

- **S1-1 (`audit-2026-09-05.md:195`), verbatim:** *"Victory souls fire **once per delve at extraction** on
  `Θ_run` = deepest room cleared, forfeited on a wipe; rooms pay `KillEarn` only. Add 'two row-1 rooms then
  extract' to the SSOT §11.7a stall-farm regression."*
- **The drop-count correction (S2-7 `:217`; §1(i) `:150`):** *"drop count reads `Θ_actor` (`LootPipeline.cs:192`,
  row 28, deliberate); depth raises item level and magnitude only."* Verified at `:192` and `:171-175`. *(The
  brief called this S1-2; S1-2 is the store-shape finding.)*
- **`ssot-rarity.md` §3.5:** overlap is *"the product of three variances that already live in shipped columns …
  no fourth mechanism is introduced."* **§3.6:** *"A multiplier on the rung makes rarity dominant and destroys
  the overlap."* A floor or shift moves which rung is drawn (`LootPity.cs:74-76, :101-103`); the rung buys
  `PrefixRolls/SuffixRolls/MinTier/MaxTier` (`RarityRow`, `ContainerRow.cs:163`; `LootPipeline.cs:328`). Never power.
- **`ssot-power-scale.md` §10.3:** *"Depth: more enemies or stronger? **Both, on separate owners.** Enemy level is
  `Θ_content` … enemy count is encounter design."* **PS-2 (`:120`):** *"A magnitude is scaled exactly once."*
  **PS-5 (`:698`):** faucet and sink on the same read. This module owns neither count nor curve.
- **R5 (ideal `:1752`), verbatim:** *"The offer floors at the altar pull price at the room's Θ via
  `SoulSinkPolicy`, paid from unbanked souls; spirit, supply and released-contract offers priced as equivalents.
  The bind stays free; teleport-home stands. Altar pulls are **at-risk haul on the delve ledger, delivered at
  extraction**."* **R12 (`:1759`):** *"A wipe seals the domain but the boss loot already earned is kept."*
  **Decision 13:** uniques *"rarity 8+"*, *"granted by id and never categorically"* — `unique-pipeline`'s.
- **N6 (`:236`):** the merchant *"reads the room's Θ."* **S2-10:** `altar.pullPriceSouls` and `provision.price*`
  do not exist — price *"is DERIVED from item class × grade × contentScale(Θ) on the item side … the Delve
  contributes only the markup"* (`spec-dungeon-registries.md`).

## Design

### 1. Inputs

Per room at **entry** (the roller reserved the stream and never drew it): `(delveId, row, col, kind,
archetypeId, keyForLaneId)`; `RoomTheta` (`Θ_room`); `RungDef`; the domain's `lootBinding`
(`spec-dungeon-seed-contract.md` §1.1, PLANNED: room kind → table id); the sealed seed; `Θ_actor` for the
**commander** through `IPowerIndexProvider.ActorIndex(ctx)` (`LootPipeline.cs:16-18`, `DropVolume.cs:44-53`)
— never a party mean, never a specimen level (N9). Per fight at close: `DelveBattleResult.Report`. At delve
close: `theta_run`, `souls_unbanked` (§7). Tuning: `dungeon.v1.json`, `item-drop-volume.v1.json`,
`power-scale.v2.json`, `souls.v1.json`, `summoning.v1.json` (`costPerPull`, `SummoningTuning.cs:5, :56`).

### 2. Soul earn — two reads, no new curve

```text
per kill       KillEarn(θ_enemy)         = KillDelta    × contentScale(θ_enemy)    SoulEarnPolicy.cs:77-78
at extraction  MatchEndEarn(true, Θ_run) = VictoryDelta × contentScale(Θ_run)      SoulEarnPolicy.cs:82-83
```

**Kills.** After each fight the host walks enemy-side `BattleActorResult`s (`BattleModels.cs:342-345`) with
`Survived == false && Retreated == false` and pays `KillEarn(setup.Level)` each — `Level` on an enemy setup **is**
`θ_enemy` (`spec-encounter-generator.md:446`). A **captured** enemy left through `Withdraw` (`Retreated`,
`spec-delve-battle-profile.md:184-185`) pays nothing (ideal §11.6); a party retreat (`:189`) still pays for
enemies that died first. Kills accrue to **`souls_unbanked`** (§7), never the bank. `PatronPolicy.
KillEarnWithPatron` (`PatronPolicy.cs:92`) is the lawn's counted-kill shape and is not called here.

**Victory — once.** At `CloseDelve(Extracted)` the host pays `MatchEndEarn(true, theta_run)` exactly once,
`theta_run` being the `Θ_room` of the deepest cleared room (§7). **The reading this spec adds, named as such:**
the term pays only when the delve is **won** by `delve-attrition` §9's rule — *"the raid extracted and (the
boss was killed or the party cleared at least half the rooms on its route)"* — the predicate loyalty already
reads, so faucet and loyalty cannot disagree. A shallow bail extracts kills and haul, no victory term; §Metrics
shows why the unconditioned reading fails S1-1's own regression row. `DefeatDelta` (`:40`) is **never read**:
a wipe pays nothing, and a consolation on a bail is a risk-free floor. Ledger rows at extraction:
`AwardSouls(playerId, kills, Reasons.Kill, "delve:{id}:kills")` and `AwardSouls(playerId, victory,
Reasons.Victory, "delve:{id}:victory")` (`RpgStore.Souls.cs:170`), dedupe-keyed; `event-deck` souls bank under
a new `Reasons.Delve = "delve"` (ideal §4.9; one constant on the closed list at `SoulEarnPolicy.cs:49-69`).
`GuardSoulAwardOrThrow` (`RpgStore.Souls.cs:161-166`) is the only bound and it throws.

**`VictoryFullPerDay`.** Removed (SSOT §11.7, audit F11; `SoulEarnPolicy.cs:9-13`): *"a wall-clock throttle …
three cap sweeps missed it because it names a threshold, not a ceiling."* Once-per-extraction is not its
successor — a **count per delve**, as a match pays its victory term once; it halves nothing, reads no clock,
and its guard is a **rate** test (§11.7a's souls per minute). A day/hour key is refused by the registries loader.

### 3. Drop tables and the two Θ reads

The host builds `LootContentView` (`LootPipeline.cs:72-86`) itself — nothing requires `Sources` to come from
`loot_source` rows. Per room:

```text
source  = new LootSourceRow("dungeon-room", $"{delveId}:{r}:{c}", tableId, ContentLevel: Θ_room,
                            FirstClearGrant: kind == boss ? bossGrantId : null)        DropTableModel.cs:52-60
seed    = SeededRng.DeriveStream(delveSeed, $"dungeon:loot:{r}:{c}").NextULong()       SeededRng.cs:26-29
request = new LootRequest(playerId, "dungeon-room", source.SourceId, seed, ThetaActor: Θ_commander, …)
```

`LootCorrelation.Derive` throws on an unknown kind (`:91-98`) and `DropTableValidator.KnownSourceKinds` (`:52-53`)
refuses one at import, so both gain **`dungeon-room`**, **`dungeon-clear`**, **`dungeon-quest`** with
`loot:delve:{delveId}:{r}:{c}`, `loot:delve:{delveId}:clear`, `loot:delve:{delveId}:quest:{questId}` —
server-derived, never client-supplied (`:10-14`; the ideal §11.3's `loot:quest:…` spelling is superseded).

**The two reads.** Step 3 reads **content**: `ItemLevel(source.ContentLevel, …)` at `:175`, `:363-371` — `Θ_room
± 1`, so a shallow domain cannot drop deep gear and the once-entry `+7` delivers *"very strong items"* through
`Θ_room` and the boss floor (audit §1(c)). Step 5a reads the **actor**: `VolumeScaleMilli(request.ThetaActor)` at
`:192`, linear, floored, uncapped (`DropVolume.cs:35-42`, §10 row 28). It holds in a delve as it holds anywhere: a
count is a rate, not a magnitude (PS-3), and a quadratic count floods the armoury (D5). S2-7's consequence is
accepted: **depth pays in item level and `contentScale`, not in count** — two row-1 rooms drop as *many* items
as two boss-adjacent rooms, and worse ones. The shallow throttle is hunger persisting across delves
(`spec-delve-attrition.md` §3) and provisioning priced at what the first room pays (§6), never a volume knob.

**Mint closes over the room Θ.** `view.Mint = grant => store.MintDrop(grant, Θ_room)` calls
`Instantiator.TryInstantiate(container, lookupAtom, lookupAffix, (long)grant.RollSeed, Θ_room, tuning, out
instance, InstanceOrigin.Drop, catalogRevision)` (`Instantiator.cs:98-107`); `thetaContent` is required —
*"absence is a rejection, not a default"* (`:88-96`); `contentScaleMilli` is computed once (`:116`) and applied in
`Freeze` (`:314-315`). `RollSeed` is step 9's (`:338`). **That is the one roll**; the `InstanceRow` records
`ThetaContent`/`ContentScaleMilli` (`:55-56`), so any drop audits back to its room.

**Four wiring gaps this module closes or files** (each a wiring fact at a named line, not a wall):

| Gap | Where | This module |
|---|---|---|
| First-clear grant appended **flat** — `RollSeed 0`, no `Mint`; persisted flat it ships a relic that ignores depth (ideal §11.7) | `LootPipeline.cs:202-210` | host instantiates it through `TryInstantiate` at `Θ_boss` on `DeriveStream(manifest.LootSeed, LootStreams.RollSeed(grant.Index))` (`LootStreams.cs:56`) — the pipeline's own stream at the grant's own index. A `RollSeed` inside the pipeline (`:208`) is ask-first: it changes every manifest with a `FirstClearGrant`. Once per `(player, kind, sourceId)` (`:204-205`); for `dungeon-clear` the source id is the **domain**, so a `many` domain's relic is per domain, not per delve |
| Step 6 reads `BaseTypesFor(frame, role)` and never `entry.RefId` for `Equipment` — no domain-only base-type subset | `:306` | files one optional delegate `LootContentView.BaseTypeSetFor`; step 6 reads `RefId is { Length: > 0 } ? BaseTypeSetFor(RefId) ∩ legal : legal`; null ⇒ today's path, byte-identical. Item program's file (`item-map.md` row); `DropContentLookups.BaseTypeSetExists` (`DropTableValidator.cs:12`) already knows the concept |
| Authored `dropBand` tables have no runtime shape — *"stage-1b infrastructure that does not exist yet"* | `LootCorpus.cs:341-352` | `DungeonLootTableGen`, scoped to dungeon tables: the planner's `dungeon-<climate>-<kind>.json` (`spec-dungeon-seed-contract.md` §4 step 4) → `DropTableRow` via the one `weightTable` (`bands.v1.json:463-470`: 1000/300/90/25/7) and `qtyCurve → Min/MaxCount`; output checked in under `data/generated/loot/dungeon.v{n}.json` (seed-contract §1) and validated by `DropTableValidator.Validate`. `d1–d4` stay the item program's |
| `affixChannel: boss` *"authored and inert — a WIRING GAP"* until X4 | `DropTableModel.cs:34-36` | boss entries author it; `loot.rooms.{kind}.affixChannel` is what X4 reads |

### 4. Rung reward columns — the floor and the shift

`difficulty.rungs[].rarityFloor` (rung id or `none`) and `.rarityShiftRungs` (signed int) are the ladder's
loot-owned columns (`spec-difficulty-ladder.md` §2); `loot.rooms.{elite,boss}.rarityFloor` and
`loot.rooms.boss.rarityShiftRungs` are the room-kind base. Composed **into the synthesized view**, never the pipeline:

- **floor** = the highest ordinal among the entry's authored `RarityFloor` (`DropTableModel.cs:97`), the room
  kind's, the rung's and — once-domain boss — `domain.onceEntry.bossRarityFloor`. `RarityDraw` applies it first
  (`LootPity.cs:74-76`); every rung below is removed, so the surviving rungs carry a higher `MinTier` — **the
  floor of the tier window rises** (`ssot-rarity.md` §3.3).
- **shift** = `RarityShift.ToWeightShift(ladder, n)` → the `RarityWeightShift` dictionary (`DropTableModel.cs:98`;
  `LootPity.cs:101-103`): `delta[o] = w(o − 10n) − w(o)` per ordinal, bottom `n` rungs zeroed, top absorbing —
  the default weight column **moved up `n` rungs**; the window shifts, nothing is multiplied. Negative `n`
  shifts down (the ladder's `very-easy`). Kind and rung shifts add; a zeroed rung is *"row kept, never drawn."*

Both are breadth and ceiling — which rung, hence how many affixes and which tiers. Neither touches
`contentScale`, `P(Θ)` or an atom range: a test rolls one `(seed, Θ_room)` with and without the boss floor and
asserts every frozen magnitude at equal `(atomId, tier)` is identical.

### 5. Room-kind table map

| Room kind | Source kind · table (`lootBinding[kind]`) | Floor / shift | Grant |
|---|---|---|---|
| `fight` | `dungeon-room` · `drop.dungeon.<climate>.fight` | rung columns | — |
| `elite` | `dungeon-room` · `….elite` | `loot.rooms.elite.rarityFloor` + rung | — |
| `boss` | `dungeon-room` for the fight; **`dungeon-clear`** (source id = domain id) for the relic | `loot.rooms.boss.*` + rung [+ `onceEntry.bossRarityFloor`] | `FirstClearGrant` at `Θ_boss`, banked at the clear (owned then, never in a pack); the boss **room's** `dungeon-room` drops are dealt round-robin by `PartyIndex` (`loot-pack` §5) |
| `cache` | `dungeon-room` · `….cache` | rung columns | — |
| a **secret** room | its kind's binding — a secret cache is a cache | as its kind | — |
| `curio · shrine · trap · wild · rest · merchant · unknown` | no table; a `cache` **outcome** of `event-deck` calls `dungeon-room` at the room's coordinates | — | — |
| quest | `dungeon-quest` · the domain's `cache` binding with the quest's `rewardBand` window (`quests.rewardBand.*`) | window as floor (composed) and ceiling (rungs above zeroed) | rolled once per completed quest inside `CloseDelve(Extracted)` at `Θ_run`, banked at the close like the relic — never through a pack (`spec-delve-quests.md` §4) |

A distinct `secret` table id is ask-first on `dungeon-seed-contract`. **The `unique` seam:** `DropEntryKind.Unique`
is refused until a concrete container exists (`DropTableModel.cs:162-165`), so `DungeonLootTableGen` **omits**
the boss table's `boss-unique` group in v1 and `unique-pipeline` fills it — by id, rung ≥ 80. **The key:** a room
with `keyForLaneId` adds one deterministic `DropResult` of kind `Key`, `RefId = laneId` — no roll, fires on
clear; `loot-pack` holds it as stock, `LaneGate` reads it (`spec-delve-scope.md` §5). `Consumable` is unavailable
in the draw until X7 (`:149-153`), which is why the key is a `DropResult` kind and not a table row.

### 6. Sinks and prices — one function, three callers

Every in-delve price is `DelvePrices.*`, and every one ends in `SoulSinkPolicy.Price(long basePriceSouls, int
thetaContent, PowerTuning)` (`SoulSinkPolicy.cs:40-41`; *"a sink reads the SAME Θ its faucet reads"*, `:23-25`).
Provisioning is `supplies-and-objects`' surface, altar and offer are `wild-room`'s, the ritual is
`delve-attrition`'s; the **price function** is this module's so no caller re-derives a scale.

| Sink | Base (long) | Θ read | Markup | Paid from |
|---|---|---|---|---|
| **merchant** | `basePriceSouls(item)` — class × grade, **DERIVED on the item side** | `Θ_room` of the merchant's room (N6) | `× (1000 + merchant.markupMilli) × rung.merchantMarkupMultMilli / 10⁶` — one widen, one divide | unbanked |
| **altar pull** | `banners[altar.bannerId].costPerPull` (`summoning.v1.json:10-11`) | `Θ_room` | — | unbanked; result is at-risk haul (R5) |
| **recruit offer floor** | `PullPrice(Θ_room) × wild.offer.soulsMilliOfPullPrice / 1000` (≥ 1000‰) | `Θ_room` | — | unbanked (R5) |
| **provisioning** (before entry) | the same derived item price | `Θ_room` at row 0 (`spec-difficulty-ladder.md` §8) | `merchant.markupMilli` | **banked** — spent at home |
| **recovery ritual** (after) | `risk.recoveryRitualSouls.{rung}` | `theta_run` of the wounding delve (attrition §7) | — | banked |

**Two horizons (P6).** In-delve sinks spend **unbanked souls only** and refuse on a shortfall (`delve.souls-
insufficient`, no overdraft) — spend the haul in the dark or carry it out. **The merchant's base price is a
wiring gap, named:** `seed-contract.md` §2.1 lists *"price · weight · durability · salvage yield — DERIVED —
§8: none exist yet."* Until the item program's derivation lands, `DelvePrices.Merchant` refuses
(`delve.price-undesigned`) and a merchant room opens as a sell-nothing rest; no default, no literal; G3 needs no
merchant. **The P2 upkeep gap (S2-8, ideal §11.6/§11.8):** `ContractPolicy.BaseUpkeepPerDay(rarity)` is a flat `int` per rarity
(`ContractPolicy.cs:124-126`) against a `P(Θ)` faucet; the fix is the contracts follow-up pricing slots, rituals
and upkeep on the player's highest cleared content Θ (map `:99`). This module does **nothing** beyond writing
`theta_run` on every extracted delve — `MAX(theta_run) WHERE state = 'Extracted'` is the seam that follow-up reads.

### 7. Haul and the at-risk ledger

**Items.** Every grant becomes a `DropResult(kind, refId, instanceId?, count, row, col, grantIndex)` row
**emitted to `loot-pack`**, which owns capacity, arrangement, the floor list and the D26 reconciliation. This
module never places, floors or reads a cell count. Instances are `InstanceOrigin.Drop` (`Instantiator.cs:9`),
**owned at placement** (`loot-pack` §5: `AcquireItem` with `origin_kind = "delve"` plus a `rpg_delve_pack_lock` row, so the
orphan sweep cannot collect a haul mid-delve and home surfaces cannot see it); on `Extracted` `loot-pack` drops the lock;
on `Wiped` the haul rows are `destroyed` and deleted with the delve. The once-domain boss relic under
`onceEntry.failKeepsBossLoot` (R12) never enters the pack: the `dungeon-clear` grant **banks at the clear itself**, owned
then, so a later wipe cannot take it. No `MutationOp` (`MutationOp.cs:126-136`) is written here — a drop is an instance; enhancement is later.

**Souls.** `rpg_delves` gains two columns through `delve-scope`'s `EnsureColumn` path (§1 there), no new table:
`souls_unbanked INTEGER NOT NULL DEFAULT 0` (long) and `theta_run INTEGER NOT NULL DEFAULT 0`. `RpgStore.Delve.cs`
gains `AccrueUnbanked(delveId, delta, roomKey)`, `SpendUnbanked(delveId, price, sinkKey)` (refuses on shortfall,
one transaction with the purchase), `RecordClear(delveId, r, c, thetaRoom)` (`theta_run = max(theta_run,
thetaRoom)`) and, inside `CloseDelve`: `Extracted` → §2's two `AwardSouls` rows, then `souls_unbanked := 0`;
`Wiped` → `souls_unbanked := 0`, no award. The ledger sits **beside** the bank: the watermark (`economy-
principles.md` P14) is untouched until extraction, a wipe writes no negative row, the dedupe keys make a
replayed close idempotent. **Rule: souls earned in a delve are at risk until extraction; a wipe forfeits them
with the haul; an extraction banks them once.**

### 8. Refusals — never a default

`drop.unknown-loot-source` (a kind with no `lootBinding` and not in §5's no-table row); `drop.entry-kind-
unavailable` (`DropTableModel.cs:134`); `delve.theta-missing` (no `RoomTheta` at entry); `delve.souls-
insufficient`; `delve.price-undesigned`; `delve.victory-twice` (a second `delve:{id}:victory` attempt —
`Inserted == false`, `RpgStore.Souls.cs:170`, a bug, not a replay); the pipeline's own rejections pass through
(`LootPipeline.cs:178-189`). Nothing clamps: `ContentScale.Apply` is `checked` (`ContentScale.cs:31-40`), draw
totals throw past `int` (`LootPity.cs:116-118`), `GuardSoulAwardOrThrow` throws at the int64 headroom.

### 9. Determinism

A room's manifest is pure over `(tableId, Θ_room, Θ_actor, rung, delveSeed, catalogRevision, dropTableRevision,
tuning, pityIn)`; pity rides the manifest (`PityIn/PityOut`, `:62-63`) and is **per player** (`LootPityState`,
`LootPity.cs:18`), distinct from `event-deck`'s per-party unknown-room pity. Every stream is a `LootStreams`
name off a seed derived from the reserved `dungeon:loot:{r}:{c}`; no `System.Random`, no clock. Souls are
`long` arithmetic over the report. Step 1's idempotency gate (`:148-156`) returns the recorded manifest on replay.

## Tunables

All read from `data/tuning/dungeon.v1.json` through `dungeon-registries`' loader (T5 rejection). **No new key,
no price literal anywhere** — every soul number is a tuning read, a `SoulEarnPolicy` constant × `contentScale`,
or a derived item price.

| Key | Unit | Owner | Read here as |
|---|---|---|---|
| `loot.rooms.{elite,boss}.rarityFloor` · `loot.rooms.boss.rarityShiftRungs` | rung id · rungs int | registries | §4 room-kind base |
| `difficulty.rungs[].{rarityFloor,rarityShiftRungs}` · `.merchantMarkupMultMilli` | rung id · int · ‰ long | ladder (written), read here | §4; §6 |
| `loot.rooms.{kind}.affixChannel` · `loot.bossGrantDistribution` · `loot.extendSlotChanceMicro` | id · rule id · per-million long | registries | X4 seam (inert); `loot-pack`'s; `unique-pipeline`'s |
| `merchant.markupMilli` · `altar.bannerId` · `wild.offer.soulsMilliOfPullPrice` | ‰ long · id · ‰ long | registries | §6 |
| `risk.recoveryRitualSouls.{rung}` · `domain.onceEntry.{bossRarityFloor,failKeepsBossLoot}` | souls long · rung id · bool | registries | §6; §4, §7 |
| `kill.killDelta` · `matchEnd.victoryDelta` (`souls.v1.json`) | souls int | soul economy | §2 through `SoulEarnPolicy` |
| `volume.*`, `pity.*`, `itemLevelJitter.*` (`item-drop-volume.v1.json`) | ‰, items | item module 11 | §3 through the pipeline |

## Numeric types

Souls, prices, item values, ‰ factors: **`long`** (`KillEarn`/`MatchEndEarn`/`Price` return `long`;
`ContentScale.Apply` widens then divides once, `ContentScale.cs:31-40`; the markup step is one `checked`
multiply of three `long`s then one `/ 1_000_000`). `Θ_room`, `Θ_actor`, `θ_enemy`, `theta_run`, ordinals, shift
counts, grant indices: **`int`**. `souls_unbanked` is a SQLite INTEGER read as `long`. Overflow throws
everywhere. No `float`/`double` under `Core/Delve/Loot/`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Loot|FullyQualifiedName~Items.Drops"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve|FullyQualifiedName~Souls"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Expedition"   # goldens
.\scripts\guard-power.ps1 ; .\scripts\guard-dal.ps1
python scripts\audit-magic-numbers.py --domain dungeon ; python scripts\audit-overflow.py
```

## Structure

```
src/FusionRpg.Core/Delve/Loot/   DelveLoot.cs · DelveSoulLedger.cs · DelvePrices.cs · RoomTableBinding.cs ·
                                 RarityShift.cs · DropResult.cs · DungeonLootTableGen.cs (→ data/generated/loot/dungeon.v1.json)
src/FusionRpg.Core/Items/Drops/  LootCorrelation.cs (+3 arms) · DropTableValidator.cs (+3 kinds) · LootPipeline.cs :306
                                 (RefId when BaseTypeSetFor supplied) — the item program's files, filed on item-map.md
src/FusionRpg.Core/Demons/SoulEarnPolicy.cs   Reasons.Delve
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs   souls_unbanked, theta_run; Accrue/Spend/RecordClear; CloseDelve earn
src/FusionRpg.Server/DelveEndpoints.cs        merchant buy; extraction summary rows (the altar is wild-room's call)
tests/FusionRpg.Core.Tests/Delve/Loot/ · tests/FusionRpg.Data.Tests/Delve/
```

## Code style

Pure over inputs, tuning injected, no I/O (tunables-ssot §7.2); no parameter named `level`/`lvl`/`index` on a
numeric method (`guard-power.ps1` G2); rejections name the rule.

```csharp
public static AtomRejection RollRoom(RoomLootInput room, LootContentView view, DropVolumeTuning drops,
    LootPityState pity, Func<LootGrant, int, LootMintResult> mintAt, out RoomLootResult? result)
{
    result = null;
    // Θ_room is content — the ONLY level the pipeline sees (:171-175); Θ_actor governs count alone (:192, row 28).
    var source = new LootSourceRow(room.SourceKind, room.SourceId, room.TableId, room.ThetaRoom, room.FirstClearGrant);
    var seed = SeededRng.DeriveStream(room.DelveSeed, $"dungeon:loot:{room.Row}:{room.Col}").NextULong();
    var request = new LootRequest(room.PlayerId, room.SourceKind, room.SourceId, seed, room.ThetaActor,
        room.CatalogRevision, room.DropTableRevision);
    var bound = view with
    {
        Sources = new Dictionary<string, LootSourceRow> { [source.Key] = source },
        Tables = RarityShift.Apply(view.Tables, room.TableId, room.Rung, room.Kind, room.OnceEntry), // floor ∪ shift, §4
        Mint = grant => mintAt(grant, room.ThetaRoom),                                        // TryInstantiate at Θ_room, once
    };
    var rejection = LootPipeline.Resolve(request, bound, drops, pity, out var manifest);
    if (!rejection.IsOk) return rejection;
    result = new RoomLootResult(manifest!, DropResult.From(manifest!, room),
        room.KeyForLaneId is { } lane ? DropResult.Key(lane, room) : null);
    return AtomRejection.Ok;
}

/// <summary>Once per delve, at CloseDelve(Extracted). Kills were accrued per room; the victory term reads Θ_run
/// and pays only when attrition §9's `won` holds. A wipe never reaches this method.</summary>
public static ExtractionEarn AtExtraction(long soulsUnbanked, int thetaRun, bool won, PowerTuning power) =>
    new(Kills: soulsUnbanked,
        Victory: won ? SoulEarnPolicy.MatchEndEarn(victory: true, thetaRun, power) : 0L,   // SoulEarnPolicy.cs:82-83
        ThetaRun: thetaRun);
```

## Testing strategy

- **Goldens per (table × rung):** four bound kinds × ten rungs, one seed each at a `rich` entrance — forty manifests
  hashed over `(TableId, ItemLevel, Grants[*].{RarityId, ItemLevel, MinTier, MaxTier, PrefixRolls, SuffixRolls,
  RollSeed})`, blessed once.
- **Victory once:** a counting fake store sees one `delve:{id}:victory` row per extracted delve; a wipe writes zero
  soul rows; a bail writes the kills row only; a replayed `CloseDelve` inserts nothing.
- **Kills exclude withdrawn:** one captured enemy pays nothing; the sum is `Σ KillEarn(setup.Level)` over
  `Survived == false && !Retreated`. **Prices monotone:** `Merchant(Θ)` non-decreasing in `Θ_room` and in rung;
  `PullPrice(Θ)` equals `SoulSinkPolicy.Price(costPerPull, Θ)` exactly; `OfferFloor ≥ PullPrice`.
- **The two reads:** `Θ_actor` doubled, `Θ_room` fixed ⇒ every `ItemLevel` and frozen magnitude unchanged, only
  `RollsEffective` moves; `Θ_room` raised, `Θ_actor` fixed ⇒ count unchanged, level and `ContentScaleMilli` move.
- **Floor and shift move rungs, not power:** boss floor on/off ⇒ identical `ValuesJson` at equal `(atomId, tier)`;
  `ToWeightShift(ladder, 1)` sums to zero and zeroes exactly the bottom rung; `n = 0` is empty.
- **§11.7a stall row (G3):** "two row-1 rooms then extract" vs a clean eleven-row run at `hard`, `rich`, autopilot,
  32 seeds — the clean run wins on souls per room and per minute (§Metrics).
- **Goldens untouched:** the four battle hashes, the 32-seed sweep, the four expedition tier hashes, the world
  goldens and the item suite's Correction 1 calibration byte-identical (`BaseTypeSetFor` null on every caller).
- **Guards:** `guard-power.ps1` green — no `location` under `Core/Delve/Loot` in `inventory.json`, and the two
  registered reads this module rests on are present: row 22 (`ContentScale.cs`), row 28 (`DropVolume.cs`);
  `guard-dal.ps1` green; `audit-magic-numbers --domain dungeon` adds zero M1 rows. **Generator:**
  `DungeonLootTableGen` over the six first-ship domains validates; rerun byte-identical; a `unique` row is omitted.

### Metrics — expected haul per room by rung (closed loop, 32 seeds)

`DropTableDraw.ExpectedEquipmentPerMille` (`DropTableModel.cs:235`) at the commander's volume scale plus
`Σ KillEarn` over the encounter's expected kills, per kind per rung; the assertion is **monotone in the rung's
reward columns**, never a target number. Worked at the ladder's row (`rich`, tier 1, `hard`: row 0 `Θ 70`,
`cs 4.235`; boss `Θ 100`, `cs 6.882`; a `pack` of five):

| Run | Kills × `KillEarn` | Victory | Souls | Rooms | Souls / room |
|---|---|---|---|---|---|
| two row-1 fights, extract (bail — no victory term) | 10 × 4.2 ≈ 42 | 0 | **≈ 42** | 2 | ≈ 21 |
| the same, victory paid unconditionally at `Θ_run = 70` | 42 | 100 × 4.235 = 424 | 466 | 2 | **233** |
| clean run, 8 fights, boss, extract | ≈ 40 × 5.5 ≈ 220 | 100 × 6.882 = 688 | **≈ 908** | 11 | ≈ 83 |

The middle row is why §2 binds the victory term to attrition's `won`: unconditioned, the bail out-earns the clean
run 2.8× per room and S1-1's own regression row fails. Bound, the clean run wins 4× per room before hunger and
provisioning count against the bail. The "no flat number" scan is the same test: every figure is `constant ×
contentScale`, recomputed from tuning.

## Boundaries

- **Always:** every item through `LootPipeline.Resolve` and `Instantiator.TryInstantiate` at `Θ_room`; every soul
  through `SoulEarnPolicy`, every price through `SoulSinkPolicy.Price`; `Θ_actor` for count, `Θ_room` for
  everything else; correlations server-derived; kills to the at-risk ledger, the bank touched only at `CloseDelve`;
  `DropResult` rows to `loot-pack`, never a placement; floor/shift composed in the view.
- **Ask first:** a `RollSeed` on the first-clear grant inside the pipeline (may move an item golden); a `secret`
  table id on `lootBinding`; a delve-scoped loot pity; the patron kill bonus in a delve; a consolation term on a
  bail; in-delve sinks drawing on the bank.
- **Never:** a private `f(level)` or `f(Θ)` — no `ssot-power-scale.md` §10 row is added; a rarity multiplier on a
  magnitude (`ssot-rarity.md` §3.6); a price literal or a `provision.price*`/`altar.pullPriceSouls` key; a second
  roll beside `Instantiator` (`Instantiator.cs:98` is the roll); a per-room `MatchEndEarn`; `DefeatDelta` on a wipe
  or a bail; a flat cap on haul, count or souls; a per-day key; a `float`/`double` magnitude; a specimen level as
  `Θ_actor`; SQL outside `FusionRpg.Data`.

## Success criteria

1. The forty goldens hold and a full solo delve on autopilot drops into the pack and extracts (G3).
2. Victory once per delve; wipe = 0; bail = kills only — proven by the counting store.
3. Captured enemies pay no `KillEarn`; every dead enemy pays `KillEarn(setup.Level)`.
4. Prices monotone in `Θ_room` and rung; `PullPrice` equals `SoulSinkPolicy.Price` bit for bit.
5. Drop count reads `Θ_actor`; level and magnitude read `Θ_room` — the two-read property holds.
6. Battle, expedition, world and item goldens byte-identical; `guard-power`, `guard-dal` green; `inventory.json` unchanged.
7. The §11.7a stall row loses to the clean run over 32 seeds. 8. No M1 under `Delve/Loot`.

## Interface exposed to dependents

| Member | Returns | Consumer |
|---|---|---|
| `DelveLoot.RollRoom(room, view, drops, pity, mintAt)` | `RoomLootResult { LootManifest; IReadOnlyList<DropResult>; DropResult? Key }` | `loot-pack` (places, floors, banks), `delve-stage` (band-4 reveal), `event-deck` (a `cache` outcome) |
| `DelveLoot.AtExtraction(soulsUnbanked, thetaRun, won, power)` · `DelveSoulLedger.KillsFrom(report, setups, power)` | `ExtractionEarn { Kills, Victory, ThetaRun }` · `long` per fight | `RpgStore.Delve.CloseDelve` / `AccrueUnbanked` (only writers), `delve-stage` (band-3 summary) |
| `DelvePrices.{Merchant(item, Θ, rung), PullPrice(Θ), OfferFloor(Θ), RecoveryRitual(rung, thetaRun)}` | `long`, or `delve.price-undesigned` | `supplies-and-objects` (provisioning), `wild-room` (altar, offers), `delve-attrition` (ritual), `delve-stage` (labels) |
| `RoomTableBinding.For(kind, lootBinding, keyForLaneId)` · `RarityShift.{ToWeightShift, ComposeFloor}` · `DungeonSourceKinds` | source kind + table id + grant; composed floor/shift; the three ids | `event-deck`, `delve-quests` (`dungeon-quest`), `domain-catalog` (first clear per domain), `unique-pipeline` (the `boss-unique` group key; the `dungeon-clear` grant seam) |
| `rpg_delves.{souls_unbanked, theta_run}` | columns via `delve-scope` | contracts-on-Θ follow-up (`MAX(theta_run)`), `delve-attrition` (`theta_run` on the recovery row) |

## Design-gate checklist

```
[x] Subsystems: item drops (pipeline, rarity, generation), soul economy (earn + sink), power ladder
    (PS-2/PS-3/PS-5, §10 rows 22/28, §11 caps), party dungeon, tunables, DAL boundary.
[x] Read this session: party-dungeon-map.md (row 10, gates, external deps); the eight approved specs; ideal
    §4.6/§4.9/§6/§8 box/§11.3/§11.6/§11.7/§11.8/§11.9/§11.10; audit §1(c)/(e)/(i), S1-1, S2-3, S2-7, S2-8,
    S2-10, N6, §5-§7; ssot-rarity §3.3/§3.5/§3.6/§8; item/seed-contract §1-§3; ssot-power-scale §8/§10/§11
    (incl. §11.7/§11.7a); spec-expeditions (format, collect); decisions.md:113-116; DESIGN-GATE §5.
[x] Code opened and cited by line: SoulEarnPolicy, SoulSinkPolicy, SoulEarnTuning, LootPipeline, DropTableModel,
    DropVolume, DropVolumeTuning, LootStreams, LootPity, LootCorpus, DropTableValidator, RarityBudgetKeys,
    MutationOp, Instantiator, ContainerRow, ContentScale, ContractPolicy, SummoningTuning, BattleModels,
    SeededRng, RpgStore.Souls, RpgStore.Contracts, WebMatchService, guard-power.ps1, inventory.json (27 rows),
    souls/power-scale/item-drop-volume/item-rarity tuning, bands.v1.json, loot/tables.v1.json, drop-tables/d1.json.
[x] Verified against CODE, not comments — the flat first-clear append (:202-210), the unread entry.RefId (:306),
    the unavailable-kind table (:134-166), the two Θ reads (:175, :192). Surrounding sections read for every
    quoted rule (§10.3 with §10; §11.7 with §11.7a; §3.6 with §3.5; S1-1 with §2's table; R5 with §1(e)).
[x] Drift reported, not fixed: KillEarn is SoulEarnPolicy.cs:77-78 and MatchEndEarn :82-83 (audit cites :79-80,
    the ladder spec :74-75); WebMatchService reads s.Actor.Level at :463, not :396-403; the ladder spec and
    map row spell the shift column `rarityShiftBand` while spec-dungeon-registries.md owns it as
    `rarityShiftRungs` (followed here); the brief's "S1-2 drop count" is S2-7 / §1(i); ContainerKind still ships
    six values (ContainerRow.cs:7-15) — `enemy` is encounter-generator's, unbuilt; MutationOp.cs is not a dependency.
[ ] Constraints not tested — nothing was run; this spec changes no code. "Goldens untouched" is argued from a null
    delegate and a delve-only host; the suites are the proof and the first build task. §Metrics is hand-computed
    from the ladder spec's worked numbers, not sampled.
[x] No §2 invariant contradicted. One reading added and named (§2): the victory term is bound to attrition §9's
    `won`, with the arithmetic that requires it. Two wiring gaps named as gaps: the item-side derived price
    (merchant refuses until it lands) and the authored→runtime table generator.
[x] Propagations landed 2026-09-05 (verification pass): spec-difficulty-ladder.md and party-dungeon-map.md now
    spell `rarityShiftRungs`; the ideal §11.3 quest correlation reads `loot:delve:{delveId}:quest:{questId}`;
    item-map.md §9 carries `LootContentView.BaseTypeSetFor`, the three source kinds and the first-clear
    `RollSeed` ask; spec-supplies-and-objects.md §7 states the merchant's refuse-until-priced behaviour.
```
