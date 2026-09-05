# Spec: domain-catalog

Status: **DRAFTED 2026-09-05 (wave 4) — written against shipped code and the twelve approved specs; unbuilt.** Every
`file:line` below was opened this session; drift is reported in §Drift. Every number is a starting shape.

Module id `domain-catalog`, row 15 of the [party-dungeon map](../party-dungeon-map.md) (wave 4). Depends on
`dungeon-seed-contract` (anchor §1.1, provenance §6), `dungeon-registries` (`domain.*`, T5 loader), `difficulty-ladder`
(`RungOffer.For`, `OathUnlock`, `PermadeathGate`, `EffectiveBandName`), `delve-scope` (`CreateDelve`, `rpg_delves`, `CloseDelve`),
`delve-graph-roll` (`Roll` + validators), `encounter-generator` (`EncounterPreflight`, `EncounterCoverage`), `event-deck`
(`EventDeckPreflight`, `EventCoverage`), `supplies-and-objects` (`ObjectPreflight`), `dungeon-loot` (`dungeon-clear` key,
`DungeonLootTableGen`, `DelvePrices`), `loot-pack` (`PackProvisioning`, `PackFill`), `delve-attrition` (`won`, `Recovering`).
Wave-4 siblings `delve-quests` and `wild-room` are unspecced and enter as interfaces. **Consumed by** `delve-stage` (wave 5)
and the map-door ask on `world-stage-map.md` (R10). Gates **G4/G5** (`party-dungeon-map.md:160-161`). Ideal: §4.1, §4.6, §8
box 2, §11.1, §11.9 boxes 14/15, §11.10 R2/R4/R10/R12. Review: §1(c) (`audit:80-92`), S1-2, S2-13, P4/P7 (`:244`).
Format: [spec-expeditions.md](../standalone/spec-expeditions.md).

## Objective

Make the domain a **content object** with the same life as every other catalog row: imported once through the seed import
path, validated by every module's preflight before it can be offered, versioned against the registries and tuning it was
validated under, offered per player as band *names* and rungs, tracked per player as discoveries and clears, and entered
through one transaction that refuses before it writes. Success: `AtomImporter` over the six first-ship domains writes six
rows and refuses a seventh whose `roomPalette` cannot fill a cell, naming domain and rule; a rerun writes zero bytes; the
picker lists found domains as `Hard`, `Very hard`, … with no number anywhere; a clear at `abyss` opens `hopeless`; a start on
a sealed once domain returns `domain.sealed` and writes nothing; a tuning bump hides every domain until revalidated; every
golden is byte-identical.

## Locked anchors (quoted, not paraphrased)

- **Decision 15** (ideal §11.9 box, `:1711-1722`): *"a dungeon can be entered only once or entered many times, that depends on
  the dungeon … A one-run dungeon drops very strong items and has +7 difficulty."* → *"`many` domains are a standing sub-world
  re-rolled on entry (one row per domain); `once` domains are one row per delve, archived at extraction, sealed for that
  player afterwards … until [`world-generator`] lands, the Sanctum picker offers them."*
- **R2** (`:1746`): *"Keep the tunable, with conditions: the picker shows the effective band *name*, never the delta; the stack
  rule with rung deltas is written (it stacks); the 'very strong items' promise rests on `bossRarityFloor`; `entry: once|many`
  is **PLANNED** by the seedsmith budget, never a free model pick."*
- **R4** (`:1748`): *"A clear at `maxRungWithoutOath` itself opens the next rung. The Oath below the gate stays as opt-in
  permadeath and a first-clear key, not the unlock mechanism."*
- **R12** (`:1759`): *"A wipe seals the domain but the boss loot already earned is kept — Diablo 3's Greater-Rift shape.
  `onceEntry.failKeepsBossLoot: true` and `onceEntry.sealOnWipe: true`, both tunables per domain; the haul other than the
  boss grant is lost as everywhere."*
- **R8's offer rule** (`:1755`): *"a rung whose band would clamp on a domain is not offered."* Applied by the ladder as
  `RungOfferRefusal.BandBelowFloor` (`spec-difficulty-ladder.md` §6); never re-derived here.
- **R10** (`:1757`): *"No legion leaves the map: the door issues the same Sanctum delve request with a `domainId`."*
- **Seed-contract provenance rule** (`spec-dungeon-seed-contract.md` §6): *"`stale_ids()` compares recorded against current
  (never mtime …). The staleness key is `briefHash + promptVersions + registryVersions + motifSubsetHash`"*; *"A domain's
  `bossSpeciesRef` is re-validated at audit time … a re-band that drops it is a refusal at import."*
- **Audit P4/P7** (`:244`): *"Unlock proof is the expedition `FoundDomain` tick (map slots have no reader); otherwise game-closed."*

## Design

### 1. The domain content row

A domain enters the database as every other seed kind does — `SeedScanner` + `SeedImportRunner` → `RpgStore.ImportContent`
(`SeedImportRunner.cs:47-50`, *"exactly one implementation of 'how a seed tree becomes catalog rows'"*), one transaction,
errors collected before the first write (`RpgStore.Import.cs:126-130`), the revision bumped once and only when something
changed (`:188-190`). Tables follow the loot tables' catalog naming (`loot_source`, `drop_table`, `RpgStore.Loot.cs:42-58`):

```sql
CREATE TABLE IF NOT EXISTS dungeon_domain (
  domain_id TEXT PRIMARY KEY,                    -- domain.<climate>-<band>-<nnn>, PLANNED
  name TEXT NOT NULL, flavor TEXT NOT NULL,     -- AUTHORED; the only fields a player reads
  theme TEXT NOT NULL, climate TEXT NOT NULL, danger_band TEXT NOT NULL,   -- ORDINAL; the int is bands.dangerBand.* at read
  entry TEXT NOT NULL,                          -- 'once' | 'many' (PLANNED, R2)
  layout_template_id TEXT NOT NULL, boss_species_ref TEXT NOT NULL, retinue_family TEXT,
  entrance_hint TEXT NOT NULL,                  -- Lair | Tear | Vault | Anomaly (SlotTypeCatalog.cs:14-20)
  permadeath_from_rung TEXT,                    -- optional VALIDATED override (§Drift 5); NULL = difficulty default
  provenance_json TEXT NOT NULL, validated_json TEXT NOT NULL,             -- §3
  revision INTEGER NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS dungeon_domain_pool (   -- roomPalette / questPool / lootBinding as ids
  domain_id TEXT NOT NULL, pool TEXT NOT NULL,     -- 'room' | 'quest' | 'loot'
  seq INTEGER NOT NULL, key TEXT NOT NULL DEFAULT '', ref_id TEXT NOT NULL,   -- loot: key = room kind, ref_id = table id
  PRIMARY KEY (domain_id, pool, seq));
```

No magnitude on either table: the entrance band int, `Θ_content` per row, the boss band and every `onceEntry.*` value are
DERIVED at read from `dungeon.v1.json` (seed contract §1.1, last row).

**The import surface today.** `SeedScanner.OwnedFolders` (`SeedScanner.cs:29`) sweeps nine folders, none under `dungeon/`;
`SeedContent` (`AtomSeedFile.cs:59-80`) has no dungeon collection — a **wiring gap**. This module adds the seven corpus
folders `dungeon/{domains,rooms,layouts,events,quests,encounters,supplies}` to `OwnedFolders` (never the whole tree —
`_registry`, `_plan`, `_containers` are the hubs'; `Order` skips `_`-prefixed *files* only, `:55-57`) and a
`SeedContent.Dungeon` block whose row shapes each consumer owns (`EventCatalog.Load(rows, tuning)`, `spec-event-deck.md` §2).
`AtomImporter` (`Program.cs:56-57, :108`) and the server's boot import (`Program.cs:425-426`, gated on `catalog_revision == 0`,
`SeedImportRunner.cs:132-133`) reach the new kind through the same members.

### 2. Import and the preflight chain

`ImportContent` validates every kind against the real catalog before its first write (`RpgStore.Import.cs:100-130`). The
domain kind's validation **is the preflight chain**: a domain failing any row is a `SeedError` naming `domainId` and the
rule; the import is refused; nothing is written. Rows run in dependency order; every domain is checked.

| # | Preflight | Owner (call) | Refusal (names `domainId` + row) |
|---|---|---|---|
| 1 | Anchor shape, ids, vocabularies, `_provenance` present | seed contract's `contract --audit` rules, mirrored | `domain.schema` |
| 2 | Every pool ref exists; layout known; `bossSpeciesRef` `threatBand ≥ bossFloorRung` (re-validated, never regenerated) | this module | `domain.ref-missing` · `domain.boss-below-floor` |
| 3 | Every `(kind, climate)` cell the layout can place has ≥ 1 archetype in `roomPalette` | this module over `RoomKindCatalog` | `domain.palette-cell-empty` |
| 4 | `Roll(domain, layout, seed_i, mode, tuning)` for every `layout.raidModes` × `preflight.sampleSeeds`; any validator throw | `delve-graph-roll` | `domain.graph:{rule}` |
| 5 | Every gated door on every sampled graph has a reachable key room on that raid's walks or a `breakMode` | `supplies-and-objects` `ObjectPreflight.Run(graph, palette, raidMode, tuning)` | `domain.object:{rule}` |
| 6 | Every slot of every reachable encounter has candidates ignoring element **and** under the climate; none lacks `threatBand` | `encounter-generator` `EncounterPreflight.Run(corpus, [domain], tuning)` | `domain.encounter:{slot}` |
| 7 | Every archetype pool fits, compiles, ≥ 1 `good` and ≥ 1 `bad`/`mixed`, `> events.noRepeatRooms` cells, ≥ 1 ambush row per `rest`, every override tag supplied | `event-deck` `EventDeckPreflight.Run(corpus, [domain], supplies, tuning)` | `domain.event:{rule}` |
| 8 | ≥ 2 quest ids; every objective's `targetRef` kind occurs on every sampled graph; sink-avoidance templates `riskPaired` | `delve-quests` `QuestPreflight.Run(corpus, domains, layouts, tuning)` (`spec-delve-quests.md` §8 — it rolls its own 256-seed satisfiability sweep per `(domain, layout, raidMode, rung)`) | `domain.quest:{questId}` |
| 9 | `lootBinding[kind]` names a `drop_table` row for every bound kind (`fight · elite · boss · cache`) | this module over `drop_table` (`RpgStore.Loot.cs:52-58`); `dungeon-loot` generates them | `domain.loot-table-missing` |
| 10 | `RungOffer.For(domain, noClears)` offers ≥ 1 rung | `difficulty-ladder` | `domain.no-rung-offered` |

Rows 4–8 share the sampled graphs. `preflight.sampleSeeds` is validator depth, not a feel number: 32, the count the coverage
metrics already use (`spec-encounter-generator.md` §8), carried in `dungeon.v1.json` under `_meta.structural[]` (the
`raid.modes.*.pack` precedent, `spec-dungeon-registries.md:120`). Preflight is model-free and store-free. `--check`
(`Program.cs:24`; rollback `RpgStore.Import.cs:195`) runs the chain and writes nothing.

### 3. Provenance and staleness

`provenance_json` is the anchor's `_provenance` block verbatim (`{planHash, briefHash, promptVersions, registryVersions,
motifSubsetHash, …}`). `validated_json` is what **this import** proved the row against: `{registryVersions, dungeonTuningHash,
encounterTuningHash, catalogRevision, dropTableRevision, sampleSeeds}` — the inputs of every §2 row, nothing else.
**Staleness is a comparison, never a clock:** `DomainStaleness.Of(row, live)` compares `validated_json` with the registries,
tuning hashes and revisions the hubs hold; unequal ⇒ `Stale` — hidden from offers (§4), refused at entry (`domain.stale`)
until the next import revalidates it. Nothing is written at boot; the comparison is part of the offer function, and `/health`
gains `staleDomains: n` beside `ContentSource`/`ContentImportError` (`RpgStore.ContentBoot.cs:36-46`).

### 4. Offers

`DomainOffers.For(progress, catalog, delves, live)` is pure and returns the DTO `delve-stage` renders. Per player, per
**found** domain, in `domainId` ordinal order:

- **Hidden** when `Stale` or never found — absent, not "locked". **Sealed** (`once` only) when `rpg_delves` holds an `Archived`
  row for `(player, domain)` — delve-scope §7's marker, written on `Extracted`, on a boss kill that extracts, and on `Wiped`
  only under `onceEntry.sealOnWipe` (R12).
- **In progress** when an `Active` row exists: rungs replaced by `resume: {delveId}`. One in-flight delve per `(player, domain)`
  follows from delve-scope's world-id shapes; across domains the only bound is `rpg_delve_pack_lock` (`spec-loot-pack.md` §4)
  — as expeditions bound concurrency by membership rows, never a per-player count.
- **Rungs** = `RungOffer.For(domain, clears)`: the free band `1…domain.maxRungWithoutOath`, then each `r + 1` a clear at `r`
  exists for, the tail by the same rule (R4), each with `EffectiveBandName`; `BandBelowFloor` refusals are *omitted*, never
  greyed. Once-entry carries the `+7` **inside** the name (R2): a `shallow` once domain at `hard` shows the band-9 name.
- **Oath**: `oathOffered: true` below `PermadeathGate`; `permadeath: true` and no oath at or above it. **Raid modes** =
  `layout.raidModes` — exactly the modes preflight sampled. **Boss** = the almanac display name, never the species id.

```text
DomainOfferDto { domainId, name, flavor, climate, entranceLabel, entryKey: "standing" | "single-descent", sealed,
                 resume?: { delveId }, rungs: [{ rungId, label, bandName, oathOffered, permadeath }],
                 tailSteps: [{ n, label, bandName }], raidModes: [id], bossName, cleared: [rungId] }
```

`once`/`many`, `Θ`, `bandDelta`, `dangerBand`, `PartyIndex` never appear as values; the stage's `BANNED_WORDS` lint
(`party-dungeon-map.md:130`) runs over this DTO's JSON in a test here.

### 5. Progress and the unlock write

This module records only what no other row holds. First-clear-taken is `item_first_clear(player_id, 'dungeon-clear', domainId)`
(`RpgStore.Loot.cs:134-140`; `HasFirstClear` `:325-331`), written by the pipeline (`LootPipeline.cs:202-210`) when
`dungeon-loot`'s host keys the source by **domain**. Once-entry state and the in-flight delve are `rpg_delves(player_id,
domain_id, state)` (§4). That leaves discovery and clears, in **one table**:

```sql
CREATE TABLE IF NOT EXISTS rpg_domain_progress (
  player_id INTEGER NOT NULL, domain_id TEXT NOT NULL,
  found_via TEXT NOT NULL, found_ref TEXT NOT NULL,        -- 'expedition' | 'clear' | 'debug'; expedition/delve id or ''
  clears_json TEXT NOT NULL DEFAULT '[]',                  -- [{ rungId, oath, delveId }], one entry per rungId, first wins
  revision INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (player_id, domain_id));
```

The `rpg_demon_codex` shape (`RpgStore.cs:435-442`) without a timestamp, because no rule reads one; highest rung cleared is
derived from `clears_json`, never stored. `RpgStore.cs:734-769`'s reset list gains the table ahead of `rpg_delves`.

**5a. Unlock.** Inside `RpgStore.Delve.CloseDelve` — the writer every sibling names — on `Extracted` **and** attrition §9's
`won`: `RecordDomainClearUnlocked(db, tx, playerId, domainId, rungIdOrTailLabel, oath, delveId)` appends to `clears_json` iff
no entry carries that `rungId`; `OathUnlock.Opens(clear)` is then a read the next `DomainOffers.For` makes. A replayed
`CloseDelve` (correlation-idempotent, delve-scope §7) appends nothing — *exactly once per (player, domain, rung)*. An Oath
clear below the gate writes `oath: true` and opens nothing (R4). `Wiped` writes no clear.

**5b. Discovery** (ideal §4.1): **(i)** the expedition `FoundDomain` tick — the sibling `ExpeditionTickKinds`
(`ExpeditionResolver.cs:7-14`) does **not** carry today; `DomainDiscovery.Pick(unfoundShallow, tickSeed)` chooses by
`WeightedChoice` over unfound `shallow` domains in ordinal order and `ApplyExpeditionRewards` (`RpgStore.Expeditions.cs:276`)
writes the row — the tick kind and its ceiling are an **ask on expeditions**; **(ii)** a clear at any rung of a domain at band
ordinal *b* in climate *c* reveals the domains at *b + 1* in *c* — deterministic, in 5a's transaction (inert on the first-ship
corpus, which has no `mid`); **(iii)** SIM-only `POST /api/test/delve/found` (the `/api/test/world/create` shape,
`WorldEndpoints.cs:376-380`) — the game-closed proof G5's unlock row needs (P4/P7).

### 6. Entry orchestration

`POST /api/delve/start` → `DelveStart.Run(request)`, the one production path into `CreateDelve`; the map door (R10) sends the
**same body** with a `domainId` and `parentWorldId`. Refusals, in order, all **before the first write**:

1. `correlationId` present, ≤ 64 chars (`ExpeditionEndpoints.cs:281-284`); a replay returns the recorded delve (`ux_rpg_delves_corr`),
   `correlation.mismatch` if the body differs (`:41-45` shape). Row exists, not `Stale` → `domain.unknown` / `domain.stale`;
   found → `domain.not-found`; `once` and `Archived` → `domain.sealed`; an `Active` row → `delve.in-progress`.
2. Rung or tail step in `RungOffer.For(domain, clears)` → `rung.not-offered`; `oath: true` at or above the gate → `oath.implied`;
   `raidMode ∈ layout.raidModes` → `raid.mode-not-offered`; party count and sizes per `raid.modes[mode]` → `raid.party-shape`.
3. Every member owned, `Roster`/bound, not `Recovering` (attrition §7), not on an expedition (`HasActiveExpeditionMembership`,
   `RpgStore.Expeditions.cs:140`), not in `rpg_delve_pack_lock` → `member.unavailable:{id}`.
4. `PackProvisioning.Validate` per party (loot-pack §4); the price via `DelvePrices` at row-0 Θ from the **bank** (dungeon-loot
   §6) → `delve.souls-insufficient`. Nothing debited yet.
5. Parent terms `WorldTier`, `ZombossLevel`, `RealmsAdvanced` (`ContentContext.cs:16`) read once through the power program's
   content-side provider and **frozen** as `content_terms_json` on the delve header (a column filed on `delve-scope` via
   `EnsureColumn`, the `souls_unbanked` precedent) so every room composes against the same terms; Sanctum entry reads the
   player's terms, a map-door entry the parent world's. `RoomTheta.Compose` reads the record; this module composes nothing.
6. Seed sealed server-side (`ExpeditionEndpoints.cs:37`); `Roll(domain, layout, seed, raidMode, tuning)`;
   `ObjectPreflight.Run(graph, …)` on *this* graph (import sampled; entry proves).
7. **One transaction** — delve-scope's `CreateDelve` extended in the same `tx` with pack lock rows and the stock debit
   (loot-pack §4), the provisioning price, `content_terms_json`, and the first `decisions_json` entry `{seq: 0, kind: "enter",
   payload: {domainId, rungId, raidMode, oath}}`. `entered` for a `once` domain is the `Active` row itself.

Response: `{delveId, worldId}` — `delve-stage`'s bootstrap. No `Θ`, no seed.

### 7. The six first-ship domains and the G4 metrics

**Shape:** one `many` domain per climate — the six `ElementTypeId`s — at `dangerBand: shallow` (map Assumption 4; seed contract
§7's row *"6 (one per climate at `shallow`, `many`)"*). `shallow` resolves to band **2** (`spec-delve-graph-roll.md` §Tunables),
so on every first-ship domain `very-easy` composes band 0 and is **not offered** (ladder §6) — refuse-not-clamp visible on
day one, and a test row. Budget row: `domains | 24 cells | 48–72 | 2–3 per cell | first ship 6`.

| Module | Metric per domain | Pass (G4) |
|---|---|---|
| `dungeon-seed-contract` | schema audit; budget actual-vs-declared; `sha256` per file on rerun | green; in tolerance; identical |
| `delve-graph-roll` | every `(tier × raidMode)` roll over `sampleSeeds` validates | zero throws |
| `encounter-generator` | `EncounterCoverage.Report` distinct `(postureMultiset, spread, formation)` cells | ≥ budget row |
| `event-deck` | `EventCoverage.Report` distinct `(eventKind, outcomeOrdinal)` cells; repeated ids per delve | ≥ budget row; zero |
| `supplies-and-objects` | gated doors with a key or break path over sampled graphs | 100 % |
| `dungeon-loot` | `DungeonLootTableGen` validates; rerun identical; four bound kinds present | green |
| `loot-pack` | `PackFill.Estimate` at the identity rung | inside `pack.fillBand.identity` |
| `delve-quests` | every objective satisfiable on every sampled graph | 100 % (interface) |
| this module | six rows imported; rerun `RowsChanged == 0`; offers non-empty for a player who found all six | as stated |

`python -m seedsmith dungeon audit` runs the seedsmith half; `AtomImporter --check` the runtime half; G4 is both green.

### 8. Refusals — never a default

Import: every §2 row, plus `domain.duplicate` (two files, one id — the ordinal sort makes the pair stable, `SeedScanner.cs:55-57`).
Offers: none — a domain that cannot be offered is absent or `sealed`. Entry: §6's list. Nothing clamps, floors or substitutes.
A refusal at play for a reason §2 checks is a **bug in preflight**; its reproduction joins the red-fixture set, never a workaround.

### 9. Determinism and idempotency

**Import** is byte-identical on rerun: canonical anchor files (seed contract §6), `validated_json` a function of the live
catalogs, and `ImportContent` skipping identical content and bumping nothing when `changed == 0` (`RpgStore.Import.cs:188-190`;
container precedent `:100-101`); the test is `ComputeContentHash()` (`:205`) before and after. **Offers** are a pure function
of `(catalog, progress, rpg_delves states, live fingerprints, tuning)` — no clock, no `System.Random`, no store call inside,
ordinal order throughout. **Entry** is one transaction with a seed that never leaves the server (`ExpeditionEndpoints.cs:326-328`).
Guard: the `spec-turn-engine.md:138` scan over `Core/Delve/Domains/`.

## Tunables

All through `dungeon-registries`' T5 loader; **one new key**, entered via `publish.py`.

| Key | Unit | Owner | Read here as |
|---|---|---|---|
| `domain.maxRungWithoutOath` · `domain.permadeathFromRung` · `domain.onceEntry.{bandDelta,sealOnWipe,failKeepsBossLoot,bossRarityFloor}` | rung id · rung id · band int / bool / bool / rung id | registries `:148`; ladder §4–§5 | §4 via `RungOffer`; §4 sealing; §6 oath rule |
| `difficulty.rungs[]`, `difficulty.tail.*`, `difficulty.minOfferedBand`, `bands.dangerBand.*` | — | ladder | only through `RungOffer.For` |
| `raid.modes.*.{parties,squadSlots}` · `pack.provision.baseCells` · `difficulty.rungs[].provisionCellsDelta` | count · cells | registries `:119`; loot-pack | §6 steps 2, 4 |
| **new** `preflight.sampleSeeds` | seeds int | this module; `_meta.structural[]` (*validator depth, never a balance lever*) | §2 rows 4–8 · 32 |

Not keys: a per-player delve cap (a count cap on the player; the pack lock is the bound); a discovery ‰ (the expedition band
ceiling is that module's); a staleness grace period (a clock).

## Numeric types

Ids, rung ids, tail labels, table ids, correlation ids, hashes: `string` (ordinal-compared; hashes equality-only). Bands, rung
ordinals, `tailPlus`, parties, cells: `int`. Seed: `ulong` in Core, TEXT in the store (`RpgStore.World.cs:24`; `WorldState.cs:322`).
`catalog_revision`, `revision`, `delve_id`, `player_id`: `long` (`content_meta`, `RpgStore.Atoms.cs:70-74`). No magnitude here.

## Commands

```powershell
cd tools\seedsmith; python -m seedsmith dungeon audit ; python -m seedsmith dungeon emit   # seed contract §Commands — unbuilt today (§Drift 7)
dotnet run --project tools\AtomImporter -- --check ; dotnet run --project tools\AtomImporter -- --validate   # §2 chain rolled back; import + lint/drift
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Domains|FullyQualifiedName~Battle|FullyQualifiedName~Expedition|FullyQualifiedName~World|FullyQualifiedName~Items.Drops"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~Delve|FullyQualifiedName~Import"
.\scripts\guard-dal.ps1 ; .\scripts\guard-power.ps1 ; python scripts\audit-magic-numbers.py --domain dungeon
```

## Structure

```
src/FusionRpg.Core/Delve/Domains/    DomainRow.cs · DomainCatalog.cs (Load: ordinals → ints at read) · DomainPreflight.cs (§2) ·
                                     DomainStaleness.cs (§3) · DomainOffers.cs + DomainOfferDto.cs (§4) · DomainDiscovery.cs (§5b) ·
                                     DelveStart.cs (§6 ordered refusals → CreateDelve) · DomainRefusal.cs
src/FusionRpg.Data/Seed/SeedScanner.cs · src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs   seven dungeon folders; SeedContent.Dungeon
src/FusionRpg.Data/Sqlite/RpgStore.Import.cs       the domain kind's validate-then-write arm
src/FusionRpg.Data/Sqlite/RpgStore.Domains.cs      the three tables; reads; RecordDomainClearUnlocked / RecordFoundUnlocked (tx-scoped)
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs        CloseDelve calls the two Unlocked writers; CreateDelve gains content_terms_json
src/FusionRpg.Server/DelveEndpoints.cs             GET /api/delve/domains/{playerId} · POST /api/delve/start · test: POST /api/test/delve/found
tests/FusionRpg.Core.Tests/Delve/Domains/ · tests/FusionRpg.Data.Tests/Delve/
UNTOUCHED: LootPipeline.cs, RpgStore.Loot.cs, WorldEndpoints.cs, world.ts, every battle file
```

## Code style

Pure resolvers with catalogs and tuning as parameters (`DelveGraphRoll` voice); refusals name id and rule; no SQL outside
Data; no profile, Θ or seed on the wire.

```csharp
/// <summary>Import-time gate. Model-free, store-free. One refusal per (domain, rule); the caller writes nothing.</summary>
public static IReadOnlyList<DomainRefusal> Run(DungeonBatch batch, PreflightInputs live, DungeonTuning tuning)
{
    var refusals = new List<DomainRefusal>();
    foreach (var d in batch.Domains.OrderBy(x => x.DomainId, StringComparer.Ordinal))
    {
        if (!Refs.AllExist(d, batch, live, out var missing)) { refusals.Add(new(d.DomainId, "domain.ref-missing", missing)); continue; }
        var layout = batch.Layouts[d.LayoutTemplateId];
        var graphs = new List<(RaidMode Mode, DelveGraph Graph)>();
        foreach (var mode in layout.RaidModes)
            for (var i = 0; i < tuning.Preflight.SampleSeeds; i++)          // STRUCTURAL depth (_meta), never a lever
                try { graphs.Add((mode, DelveGraphRoll.Roll(d, layout, PreflightSeed(d.DomainId, mode, i), mode, tuning))); }
                catch (InvalidOperationException ex) { refusals.Add(new(d.DomainId, "domain.graph", ex.Message)); }
        foreach (var (mode, g) in graphs) refusals.AddRange(ObjectPreflight.Run(g, d.RoomPalette, mode, tuning).Select(r => r.For(d.DomainId)));
        refusals.AddRange(EncounterPreflight.Run(live.Species, new[] { d }, live.Encounter).Select(r => r.For(d.DomainId)));
        refusals.AddRange(EventDeckPreflight.Run(batch.Events, new[] { d }, batch.Supplies, tuning).Select(r => r.For(d.DomainId)));
        refusals.AddRange(QuestPreflight.Run(batch.Quests, new[] { d }, batch.Layouts, tuning));         // delve-quests §8
        foreach (var kind in RoomTableBinding.BoundKinds)
            if (!live.DropTables.Contains(d.LootBinding[kind])) refusals.Add(new(d.DomainId, "domain.loot-table-missing", kind));
        if (RungOffer.For(d, PlayerClears.None).Rungs.Count == 0) refusals.Add(new(d.DomainId, "domain.no-rung-offered", d.DangerBand));
    }
    return refusals;
}

/// <summary>Pure. Same (catalog, progress, delve states, live fingerprints) ⇒ same DTO, byte for byte.</summary>
public static IReadOnlyList<DomainOfferDto> For(IReadOnlyList<ProgressRow> progress, DomainCatalog catalog,
    IReadOnlyList<DelveStateRow> delves, LiveFingerprints live)
    => progress.OrderBy(p => p.DomainId, StringComparer.Ordinal)                     // found domains only
        .Select(p => (p, d: catalog.Get(p.DomainId)))
        .Where(x => DomainStaleness.Of(x.d, live) != Staleness.Stale)                // hidden until revalidated
        .Select(x =>
        {
            var sealedRow = x.d.Entry == DomainEntry.Once && delves.Any(s => s.DomainId == x.d.DomainId && s.State == "Archived");
            var active = delves.FirstOrDefault(s => s.DomainId == x.d.DomainId && s.State == "Active");
            var offer = RungOffer.For(x.d, PlayerClears.From(x.p.Clears));           // names, never deltas (R2)
            return DomainOfferDto.From(x.d, live, sealedRow, active,
                rungs: sealedRow || active is not null ? Array.Empty<RungOfferDto>() : offer.Rungs.Select(RungOfferDto.From).ToArray(),
                tail: offer.TailSteps.Select(TailStepDto.From).ToArray(), cleared: x.p.Clears.Select(c => c.RungId).ToArray());
        }).ToArray();
```

## Testing strategy (gates G4/G5)

- **Import refuses each red fixture naming the rule:** one fixture per §2 row (empty palette cell, refused layout, keyless gate
  with `breakMode: none`, unfillable slot, null `threatBand`, one-cell event pool, unsatisfiable quest, missing boss table, boss
  below floor, band-0 domain); `domainId` and rule asserted; `ComputeContentHash()` and `catalog_revision` unchanged.
  **Rerun byte-identical:** six imported twice; second `RowsChanged == 0`. **Staleness:** bump one live `registryVersion` ⇒ absent
  from offers, start → `domain.stale`; re-import ⇒ offered again. No `DateTime` anywhere (guard scan).
- **Offers:** a fresh player who found one `shallow` domain sees `easy…abyss` (band 2 refuses `very-easy`); a clear at `abyss`
  adds `hopeless`, at `impossible` adds `abyss +1`; an Oath clear at `easy` adds nothing and records `oath: true`; every
  `bandName` is a registry name; the DTO JSON contains none of `Θ`, `bandDelta`, `once`, `many`, `PartyIndex`, `dangerBand`.
  **Once-entry names:** a `once` fixture at `hard` shows the band-9 name where its `many` twin shows band 2's.
- **Unlock exactly once:** `CloseDelve(Extracted, won)` at rung 8 appends one clear; replayed appends nothing; `Wiped` nothing;
  `Extracted` without `won` nothing. **Sealing:** `Archived` ⇒ `sealed: true` and `domain.sealed` on start; `Wiped` with
  `sealOnWipe: true` ⇒ sealed, `false` ⇒ open; the `dungeon-clear` row survives the wipe (`HasFirstClear`).
- **Entry is one transaction:** a provisioning refusal, `member.unavailable`, `souls-insufficient` and a forced failure inside
  `CreateDelve` each leave `rpg_delves`, `rpg_worlds`, `rpg_item_stock`, the soul ledger and `rpg_delve_pack_lock` byte-identical;
  a success writes all of them and `content_terms_json` in one commit. **Discovery:** `Pick` over a fixed tick seed is stable;
  `RevealDeeper` reveals the `mid` twin in a widened fixture; the SIM route writes one progress row.
- **Goldens untouched:** the four battle hashes and 32-seed sweep, four expedition tier hashes, the world scenario hash and the
  item calibration run in the same command. **Guards:** `guard-dal` (SQL only in `RpgStore.Domains.cs` / `RpgStore.Delve.cs`),
  `guard-power` (no `location` under `Core/Delve/Domains`), magic-number audit zero M1.

## Boundaries

- **Always:** import through `ImportContent`, preflight before the first write, refusals naming domain and rule; provenance and
  the validated fingerprint on every row; staleness by comparison; offers through `RungOffer.For` with names only; the unlock
  inside `CloseDelve`'s transaction; entry refusals before any write; the seed sealed server-side; one import path for tool and boot.
- **Ask first:** a `mid`/`deep` first-ship domain; a fourth discovery route; a stored stale flag instead of the read-time
  comparison; a per-player concurrent-delve bound; a `permadeathFromRung` anchor field (§Drift 5).
- **Never:** offering an unvalidated or stale domain; a delta, a `Θ` or an engine word shown to the player; a wall clock in
  staleness or discovery; a private curve or any `f(level)`; entry without every preflight; a write before the last refusal;
  a magnitude in a content row; `TurnEngine.Step` from anything here; SQL outside `FusionRpg.Data`; a model call in the runtime.

## Success criteria

1. **G4:** six domains import green through §2, rerun `RowsChanged == 0`, every §7 coverage row at its pass over the same six
   files. 2. Every §2 red fixture refuses naming its rule and writes nothing. 3. **G5:** the Sanctum picker and the map-door
   request reach `POST /api/delve/start` with the same body (a test posts both); the DTO passes the stage lint; the unlock row
   *"Delve — first domain found (expedition)"* flips on the SIM route, and on the `FoundDomain` tick once that ask lands.
4. Clear-opens-next, Oath-opens-nothing, sealed-refuses-entry, once-per-(player, domain, rung) proven by the Data suite.
5. Battle, expedition, world and item goldens byte-identical; guards green.

## Interface exposed to dependents

| Member | Consumer |
|---|---|
| `GET /api/delve/domains/{playerId}` → `DomainOfferDto[]` (§4) | **`delve-stage`** — the Sanctum Delve layer's picker |
| `POST /api/delve/start {correlationId, playerId, domainId, rungId \| tailPlus, raidMode, oath, parties[][], provisioning[], parentWorldId?}` → `{delveId, worldId}` or a rule id | **`delve-stage`**; the **map door** (`world-stage-map.md` ask: `world-inspector` action + `world-commands` order issue this body with `domainId` and `parentWorldId`, R10) |
| `DomainCatalog.Get(domainId)` → row + pools, ordinals resolved | `delve-graph-roll`, `encounter-generator` (`bossSpeciesRef`, `retinueFamily`), `event-deck`, `dungeon-loot` (`lootBinding[kind]`), `delve-quests` (`questPool`) |
| `RecordDomainClearUnlocked` / `RecordFoundUnlocked` (Data, tx-scoped); `DomainPreflight.Run(batch, live, tuning)` | `RpgStore.Delve.CloseDelve` and `ApplyExpeditionRewards`; `RpgStore.ImportContent` and `python -m seedsmith dungeon audit` through `--check` |
| **Filed asks** | expeditions: `ExpeditionTickKinds.FoundDomain` + its ceiling; `delve-scope`: `content_terms_json`, `enter` decision kind; `dungeon-seed-contract`: optional VALIDATED `permadeathFromRung`, the `many ≥ mid` line (§Drift 4); `delve-quests`: `QuestPreflight.Run`; `dungeon-registries`: `preflight.sampleSeeds`; `world-stage-map.md`: the door request shape; `world-map-program.md`: `world-generator` places `entranceHint` slots from `dungeon_domain` |

## Drift found this session (report, not fixed here)

1. **No dungeon kind on the import path.** `SeedScanner.OwnedFolders` (`SeedScanner.cs:29`) has nine folders, none under
   `dungeon/`; `SeedContent` (`AtomSeedFile.cs:59-80`) has no dungeon collection — the map row's *"runtime catalog read from
   `data/seed/dungeon/domains/`"* is a wiring gap closed in §1. `DropTableValidator.KnownSourceKinds` (`DropTableValidator.cs:52-53`)
   lacks the `dungeon-*` kinds (`dungeon-loot`'s addition; §2 row 9 depends on them).
2. `ExpeditionTickKinds` (`ExpeditionResolver.cs:7-14`) has no `FoundDomain`; ideal §4.1 and audit P4/P7 assume one.
3. **First-ship band.** Seed contract §1.1 says *"`many` domains ≥ `mid`"*; the map and the same spec's §7 budget say six at
   `shallow`; graph-roll puts `shallow` at band 2 — satisfies the audit's *"≥ 2"* (`:154-155`) but not *"the whole ladder shows"*
   (`very-easy` refused). This spec follows the map and budget (§7); the `≥ mid` clause is the line to correct.
4. Ladder §4 says a seed *"may carry `permadeathFromRung`"*; the approved anchor table has no such field — nullable here, filed.
   `audit:86-88` (*"a wipe leaves the domain open"*) predates R12; R12's `sealOnWipe: true` default wins (§4).
5. **Nothing exists yet:** `data/seed/dungeon/` absent; `data/generated/` holds only `demons/`; `adapters/registry.py:13-15` registers
   `items`, `demons`, `actions` — no `dungeon`; no file under `tools/seedsmith/seedsmith/` mentions "dungeon". Every
   `python -m seedsmith dungeon …` command is the seed contract's promise, unbuilt.
6. `item_first_clear.player_id` is `TEXT` (`RpgStore.Loot.cs:135`) while `rpg_*` player ids are `INTEGER`; `HasFirstClear(string, …)`
   (`:325`) takes the string — the host converts once. The only world-creation endpoint is SIM-only (`test.MapPost("/world/create")`,
   `WorldEndpoints.cs:380`); §6's production precedent is expeditions' dispatch (`ExpeditionEndpoints.cs:277-290`; service `:35-45`).

## Design-gate checklist

```
[x] Subsystems: seed import path + content tables (DAL), power ladder (ContentContext pass-through only), world store (delve
    rows read), loot first-clear, expeditions (discovery seam), tunables, Game GUI bands, party dungeon.
[x] Read this session: party-dungeon-map.md (row 15, G4/G5, external deps, six first-ship domains); all TWELVE approved specs
    in full; ideal §4.1, §4.6, §8 + box, §11.1, §11.9 boxes, §11.10 table; audit §1(c), S1-2, S2-13, P4/P7, §7 rows;
    spec-expeditions.md (format, dispatch/list shape); decisions.md:113-116; DESIGN-GATE §5.
[x] Every code claim cites file:line opened this session (SeedImportRunner, SeedScanner, AtomImporter/Program, RpgStore.Import/
    .Atoms/.cs/.Loot/.World/.Expeditions/.ContentBoot, Server/Program, ExpeditionEndpoints, WorldEndpoints, ExpeditionResolver,
    ExpeditionTierCatalog, LootPipeline, DropTableValidator, AtomSeedFile, ContentContext, SectorTypeCatalog :24/:70/:98,
    SlotTypeCatalog :14-20, WorldState :318-323, WorldTemplateCatalog, WorldValidation, WorldCanonical :27, seedsmith
    adapters/registry.py). Verified against CODE, not comments; the surrounding section read for every quoted rule.
[ ] Constraints not tested — nothing was run; this spec changes no code. "Goldens untouched" is argued from the module
    emitting no hashed row; the suites are the first build task.
[ ] Gaps stated: delve-quests, wild-room, unique-pipeline are unspecced wave-4 siblings — QuestPreflight.Run is an interface
    named here for that spec to own; emit.py's stale_ids shape is cited through the seed contract §6, not opened; the
    content-side provider for WorldTier/ZombossLevel/RealmsAdvanced is the power program's read (ladder §1), file not opened.
[x] No §2 invariant contradicted: no injector, no private curve, no cap on a magnitude (the pack lock bounds concurrency;
    no per-player count), no clock, tunables in data, SQL in Data, one import path.
[x] Corrections propagated within this spec. Asks landed 2026-09-05 (verification pass): delve-scope carries
    `content_terms_json` and the `enter` decision kind (battle-profile §4a list); the seed contract carries the optional
    VALIDATED `permadeathFromRung` and the corrected first-ship band line; registries carries `preflight.sampleSeeds`;
    world-stage-map.md, world-map-program.md and standalone-rpg-map.md carry their filed rows.
```
