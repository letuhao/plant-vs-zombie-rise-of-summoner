# Spec: almanac-seed

Module in the [almanac map](../almanac-map.md). Depends on `almanac-capture-fix`,
`almanac-spawn-coverage`. BE only — no FE this round (owner has a separate FE refactor task).

Read first: [data-architecture.md](../data-architecture.md) §3 SSOT map, §6 DAL boundary.

## Objective

Turn the two existing raw capture streams — `type_almanac_dump` (prose, media DB) and
`spawn_stats`/`types` (numeric, hot DB, SSOT per data-architecture.md §3) — into **one clean,
typed, per-type table** other gameplay systems can query without re-parsing rich text or guessing
which numeric source is trustworthy. This is the generator the Demon program's species catalog
already commits to needing ([decisions.md:90](../decisions.md) — *"generated deterministically from
captured game data (types/almanac/icons/spawn_stats)"*).

Explicitly **not** a live-computed join — measured 2026-08-23: only 89 of 677 plant almanac entries
(13%) even carry a `cost` field (the rest are fusion-fragment/structural entries with no seed-card
cost), and only 66/677 plants + 18/227 zombies have a `spawn_stats` sample at all before
`almanac-spawn-coverage` runs. Re-parsing and re-joining that on every read is both slow and hides
"we don't have this yet" behind a request-time null. A rebuilt, persisted table makes both the
coverage and the confidence explicit and queryable.

Done means: one REST call returns a fully-typed record for a type — name, flavor text, parsed sun
cost/cooldown where they exist, and real hp/attack/armor where observed — with no raw JSON, no
color-tag markup, and an explicit flag for "not observed yet," backed by a table the owner can query
directly for sanity.

## Design (locked on approval)

### Storage: new hot-DB table, not a join

```sql
CREATE TABLE IF NOT EXISTS almanac_seed (
  side               TEXT NOT NULL,             -- 'plant' | 'zombie'
  type_id            INTEGER NOT NULL,
  type_name          TEXT,                       -- enum identifier, e.g. 'Peashooter'
  display_name       TEXT,                       -- localized, e.g. '豌豆射手'
  flavor_info        TEXT,                       -- PlantInfo.info / ZombieInfo.info, raw (markup stripped)
  flavor_introduce   TEXT,                       -- ZombieInfo.introduce, raw (markup stripped); null for plants
  sun_cost           INTEGER,                    -- parsed from PlantInfo.cost; null unless cost_status='parsed'
  cooldown_sec       REAL,                       -- parsed from PlantInfo.cost; null unless cost_status='parsed'
  cost_status        TEXT NOT NULL DEFAULT 'absent', -- 'absent' | 'parsed' | 'unparsed' — see Cost/cooldown parsing
  hp                 INTEGER,                    -- from spawn_stats SSOT baseline sample; null if unobserved
  attack             INTEGER,
  armor              INTEGER,                    -- zombie-only; our capture never emits armor for plants, see below
  armor_max          INTEGER,                    -- zombie-only, same reason
  stats_observed     INTEGER NOT NULL DEFAULT 0, -- 1 iff a spawn_stats baseline sample was found
  stats_sample_utc   TEXT,                       -- captured_utc of the spawn_stats row used
  almanac_captured_utc TEXT,                     -- captured_utc of the type_almanac_dump row used
  contract_version   INTEGER NOT NULL,
  rebuilt_utc        TEXT NOT NULL,
  PRIMARY KEY (side, type_id)
);
```

Lives in **`rpg-hot.sqlite`**, not media — it's structured/queryable, not a BLOB
([data-architecture.md §1](../data-architecture.md): media is BLOB-only —
`type_icons`/`type_icon_layers`/`type_almanac_dump`). New partial-class file
`src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs`, following the existing `RpgStore.Almanac.cs`
shape (DTO defined in the same file/namespace, `lock (_gate)`, `OpenUnlocked()` for hot). Recipes are
**not** a column here — separate lookup, owner call.

**`type_name`/`display_name` are a rebuild-time naming snapshot, not the naming SSOT.**
`data-architecture.md §3` names `types` as the naming SSOT (fill-if-empty from spawn; almanac dump
may prefer Chinese titles). The existing almanac-dump read path already treats its own copy as a
snapshot and falls back to live `types` values when its own fields are empty
([RpgStore.Almanac.cs:167-179](../../../src/FusionRpg.Data/Sqlite/RpgStore.Almanac.cs)) — this
table's read path (`GetAlmanacSeed`/`ListAlmanacSeed`) does the same, so a name correction landing in
`types` between rebuilds is visible immediately, not stuck until the next rebuild.

### Combat stats: `spawn_stats`, never `types.hp_base`

The SSOT is `events.payload` **+** `spawn_stats.stats_json` — "per-spawn dumps, same keys"
([data-architecture.md §3](../data-architecture.md)). `types.hp_base`/`sample_json` are explicitly
**not SSOT** (*"RPG features read dumps..., never types.hp_base. Missing hook = missing fact — never
invent HP from the catalog."*) even though `types` already mirrors the first-seen sample via a
`COALESCE`-once write ([RpgStore.cs:2696-2708](../../../src/FusionRpg.Data/Sqlite/RpgStore.cs)). The
rebuild reads `spawn_stats` only, **not** `events.payload` — a type whose only record is an
`events.payload` row with no corresponding `spawn_stats` row (per §4 invariant 3, extra
`spawn_stats` rows arrive only via `entity.stats` recapture) will be marked `stats_observed = 0` even
though the SSOT technically has data. Accepted for v1: `spawn_stats` is where `almanac-spawn-coverage`
writes, and re-deriving from the raw `events` log is a heavier query this module doesn't need yet —
flagged as a known gap, not solved here.

**Split by side — the source values are not the same list for plants and zombies**
([game-types-381.md](../../research/game-types-381.md)): plant capture sources are `start` /
`setPlantAttributes`; zombie capture sources are `start` / `initHealth` / `setHealthInTravel` /
`setZombieHealth` / `reinforce`. `initHealth` never appears on a plant row (plants only have one
spawn-time hook, `Plant.Start`) so it is meaningless on that side. Two queries:

```sql
-- plant
SELECT stats_json FROM spawn_stats WHERE side='plant' AND type=$t AND source='start'
ORDER BY captured_utc ASC LIMIT 1;
-- zombie
SELECT stats_json FROM spawn_stats WHERE side='zombie' AND type=$t AND source IN ('start','initHealth')
ORDER BY captured_utc ASC LIMIT 1;
```

**Corrected 2026-08-23, live evidence during implementation:** an earlier draft of this spec
restricted the zombie query to `source='initHealth'` only, reasoning that `Zombie.InitHealth()`
always wins the race against `Zombie.Start()` for the `EntityApply.RunZombie` `Applied.Add(ptr)`
first-write gate ([GameHooks.cs:678-701](../../../src/FusionRpg.Injector/GameHooks.cs) — both
`Zombie.Start` and `Zombie.InitHealth` are separately Harmony-patched and both call `ApplyZombie`,
but only the one that fires first for a given `ptr` actually writes). Live sampling of 14 real runs'
`spawn_stats` (via `GET /api/runs/{id}/spawns`) found this assumption false in a small minority of
cases: **zombie type 0 (base `Zombie`, run 15, 2026-08-16) has a row with `source='start'`** — 1 hit
out of ~4300 sampled zombie spawn rows (~0.02%), but on a mainstream type, not an obscure one. A
query restricted to `initHealth` alone would silently report `stats_observed=false` for any zombie
type whose only spawn-time capture happened to win that race the other way, even though a legitimate
baseline row exists under `source='start'`. Both `start` and `initHealth` are genuine
first-sight-of-the-entity captures (as opposed to `reinforce`/`cheat.reapply`/`debug.spawn`, which
reflect mid-match or debug-injected state) — the fix is to accept either for zombies, exactly as an
even earlier draft of this spec already had it before a prior correction pass narrowed it down for
the wrong reason (that pass correctly identified plants never populate `initHealth`, but
over-applied the fix by dropping `start` from the zombie side too).

`hp`/`attack` are read from `stats_json`'s `hpBase`/`attackBase` keys — present in both plant and
zombie captures ([GameDumps.cs:58-62](../../../src/FusionRpg.Injector/GameDumps.cs) (plant),
[GameDumps.cs:116-124](../../../src/FusionRpg.Injector/GameDumps.cs) (zombie)). **`armor`/`armor_max`
are zombie-only** — `GameDumps.Plant(...)` has no `armorBase`/`armorMaxBase` parameters at all
([GameDumps.cs:40](../../../src/FusionRpg.Injector/GameDumps.cs)); every plant row will have
`armor`/`armor_max = NULL` even when `stats_observed = 1`, and that is the correct, permanent state
for plants — not a missing-data case to chase. No sample found for a side/type ⇒
`stats_observed = 0`, numeric columns `NULL` — never fall back to `types.hp_base`.

### Cost/cooldown parsing: scoped to the one reliable template

Sampled 40 random plant almanac entries live (2026-08-23): 5 had a non-null `cost` field in that
sample, and all 5 matched the same template exactly — a small sample backing the regex, not a large
one; the design accepts that and treats any non-match as data to inspect, not silently drop (see
below). Separately measured across the full corpus: 89 of 677 plant entries (13%) have a `cost`
field at all — the rest are fusion-fragment/structural entries with no seed-card cost, a real "not
applicable" case, distinct from "present but unparseable":

```
花费：<color=red>NUM</color>
冷却时间：<color=red>NUM秒</color>
```

(`NUM` integer for cost, decimal-capable for cooldown — e.g. `7.5秒`.) Two independent single-line
regexes, applied to the `cost` field text (order/whitespace-tolerant, not full-string anchored):

```csharp
static readonly Regex SunCostRx = new(@"花费[:：]\s*<color=(?:red|#[0-9A-Fa-f]{6,8})>(\d+)</color>");
static readonly Regex CooldownRx = new(@"冷却时间[:：]\s*<color=(?:red|#[0-9A-Fa-f]{6,8})>(\d+(?:\.\d+)?)秒</color>");
```

`<color=...>` accepts both the literal `red` seen in every sample so far and a `#RRGGBB`/`#RRGGBBAA`
hex form, since nothing in the captured corpus rules out other almanac text using hex — the regexes
should not silently stop matching the day one does. `(\d+(?:\.\d+)?)` (not `[\d.]+`) rejects
malformed multi-dot input like `7.5.5` rather than partially matching it.
`double.Parse(..., CultureInfo.InvariantCulture)` for `cooldown_sec` — the source text always uses
`.` regardless of the machine's locale, and a comma-decimal locale must not be allowed to misparse it.

`cost_status` (`TEXT`, not a bare boolean) distinguishes three states a single `cost_parsed` flag
cannot: `'absent'` (no `cost` field — the 588-row majority, expected and fine), `'parsed'` (both
regexes matched), `'unparsed'` (a `cost` field exists but didn't match — the actual defect signal,
counted in the rebuild summary and worth a human look, never silently coerced into either of the
other two states). `sun_cost`/`cooldown_sec` are `NULL` unless `cost_status = 'parsed'`.

**Not attempting** to parse damage/range/traits out of `info`/`introduce` — those fields are far less
uniform (support plants have no damage line, structural entries show `韧性` instead, ability plants
mix damage+range+traits in varying order) and are stored **raw** (markup-stripped) for the consumer
to read as text. If the owner wants specific numeric fields mined from `info` later (e.g. `韧性` for
structural plants), that's a follow-up with its own format catalog — not assumed here.

### Rebuild: server-side batch job, no injector/game session required

Pure DAL operation over already-ingested tables (`type_almanac_dump`, `spawn_stats`) — does not need
the game running. `RpgStore.RebuildAlmanacSeed()`:

1. **Wrapped in one transaction per connection** (hot write; media is read-only for this step —
   never shared with the hot write, per the existing convention `// Media write first — never share
   a txn with hot.` [RpgStore.Almanac.cs:54](../../../src/FusionRpg.Data/Sqlite/RpgStore.Almanac.cs)).
   Combat baselines are loaded **once, in exactly two queries** (one per side) before the row loop —
   **not** one query per type. **Corrected 2026-08-23, live evidence:** the original design (a
   per-type `SELECT ... WHERE side=... AND type=... ORDER BY captured_utc LIMIT 1` called once per
   row) is an N+1 pattern — confirmed live against a real 40,459-row `spawn_stats` table (523MB
   `rpg-hot.sqlite`, 70+ runs) to take 30s+ and never complete under a 30s client timeout, invisible
   to every automated test because test databases only ever seed a handful of rows. Replaced with
   `LoadCombatBaselinesUnlocked`: one query per side using
   `ROW_NUMBER() OVER (PARTITION BY type ORDER BY captured_utc ASC)` to get the earliest sample per
   type in a single pass, loaded into an in-memory dictionary the row loop reads from (no further
   SQL per row). Proven independent of indexing, not just index-assisted: timed directly against a
   copy of the real database with `ix_spawn_stats_side_type_source` dropped entirely — 0.111s;
   0.069s with the index present. The index stays as a minor assist, not a load-bearing requirement.
   For each row in `type_almanac_dump` (media DB): parse `fields_json`, strip `<color=...>` /
   `</color>` markup from `info`/`introduce`, run the cost regexes, look up the baseline from the
   preloaded dictionary, upsert one `almanac_seed` row. A failure partway through rolls back the
   whole rebuild rather than leaving a table with mixed `contract_version`/`rebuilt_utc` stamps.
2. **Stale-row handling:** after upserting, delete any `almanac_seed` row whose `(side, type_id)` no
   longer has a matching `type_almanac_dump` row — a rebuild reflects the current capture state, not
   an ever-growing superset of every type ever seen.
3. Returns a summary: `{ built, plantsBuilt, zombiesBuilt, costAbsent, costParsed, costUnparsed,
   statsObserved, statsUnobserved, staleRemoved }` — surfaced by the REST trigger so a rebuild's
   quality is visible without a manual query.

### Contract version

`AlmanacSeedContractVersion = 1` — a small integer constant, the same *idea* `FoundationContractVersion`/
`MatchRuntimeContractVersion` use ([software-architecture.md §11](../software-architecture.md)) for
versioning a shape independently of code deploys. Note: those two live in `FusionRpg.Contracts`
because they cross the Injector↔Server boundary as wire types; `AlmanacSeedContractVersion` does
**not** need to (nothing in the Injector reads it — the rebuild is a pure server-side DAL operation,
see above), so `AlmanacSeedDto` staying in `FusionRpg.Data` next to `AlmanacTextDumpDto` is a
different, still-correct precedent, not the same one restated. Any *other* consumer (e.g. the Demon
species-catalog generator this whole module exists to feed, per `decisions.md:90`) is expected to
read it over HTTP like any other REST client, not via a C# type reference — so "lives in
`FusionRpg.Data`" and "other systems will read this" are not in tension.

Stamped into every row's `contract_version` column and every REST response. Bump it when the row
shape changes; old rows keep their stamped version until the next rebuild.

### REST (Server, no SignalR — this is pull-only reference data)

```
GET  /api/almanac/seed?side=plant|zombie      → { contractVersion, items: AlmanacSeedDto[] }
GET  /api/almanac/seed/{side}/{typeId}        → AlmanacSeedDto | 404
POST /api/almanac/seed/rebuild                → rebuild summary (see above)
POST /api/almanac/seed/enrich                 → { matched, unmatched: string[] } (see External enrichment)
```

`AlmanacSeedDto` (camelCase JSON, `FusionRpg.Data` namespace next to `AlmanacTextDumpDto`):

```csharp
public sealed class AlmanacSeedDto
{
    public string Side { get; set; } = "";
    public int TypeId { get; set; }
    public string? TypeName { get; set; }         // naming reference only — types is naming SSOT, see below
    public string? DisplayName { get; set; }       // same
    public string? FlavorInfo { get; set; }
    public string? FlavorIntroduce { get; set; }
    public int? SunCost { get; set; }
    public double? CooldownSec { get; set; }
    public string CostStatus { get; set; } = "absent";  // 'absent' | 'parsed' | 'unparsed'
    public int? Hp { get; set; }
    public int? Attack { get; set; }
    public int? Armor { get; set; }     // always null for plants — see Combat stats above
    public int? ArmorMax { get; set; }  // same
    public bool StatsObserved { get; set; }
    public int ContractVersion { get; set; }
    public string RebuiltUtc { get; set; } = "";
}
```

### External enrichment (optional, separate table — never SSOT)

A third-party fan tool ("Almanac for PvZ Fusion 3.6.1", a Scratch project packaged as a standalone
HTML app, inspected live 2026-08-23) has fields our own capture cannot produce at all: plant `Qualities`
tags (`Defensive`/`Short`/`Grounded`/…), `Unlock` condition text, a `Type` classification
("Basic Plant" etc.), and per-zombie damage-vs-tag breakdowns + explicit weakness text (e.g. Giga
Mecha Gargantuar: "Deals 500 Damage to anti-crush plants... Succeptible to magnet shrooms").
Genuinely useful, but it is **fan-compiled, from an older game version (3.6.1 vs our 3.8.1), and
matched by name, not by our `type_id`** — three separate reasons it can never join the core
`almanac_seed` table's trusted columns or be mistaken for something the game itself confirmed.

Separate table, so the join is explicit and the provenance can never leak into the trusted columns:

```sql
CREATE TABLE IF NOT EXISTS almanac_seed_enrichment (
  side              TEXT NOT NULL,
  type_id           INTEGER NOT NULL,
  qualities_json    TEXT,   -- raw tag array, e.g. ["Defensive","Short"]; plants only
  unlock_condition  TEXT,
  type_class        TEXT,   -- e.g. "Basic Plant"; plants only
  weaknesses_text   TEXT,   -- raw prose; zombies only
  damage_vs_text    TEXT,   -- raw prose, damage-vs-tag breakdown; zombies only (unpopulated for now
                             -- — see note below, the source tool folds this into weaknesses_text)
  description_text  TEXT,   -- raw prose behavior text, e.g. "Ignores handheld armor"; plants only
                             -- (added 2026-08-23 — found on Plant-id-info alongside Type/Unlock,
                             -- covers 574/617 plants, not in the original design)
  source            TEXT NOT NULL,   -- e.g. 'pvz-fusion-almanac-3.6.1'
  matched_by        TEXT NOT NULL,   -- 'name' — never 'type_id', see above
  imported_utc      TEXT NOT NULL,
  PRIMARY KEY (side, type_id)
);
```

**Import, not live capture.** There is no automated path from the fan tool into our DB — it isn't
our game, isn't the injector, and matching happens by display/internal name (fuzzy, human-checked),
not a live hook. One-time (repeatable) import:

1. A reference export, checked into the repo:
   `data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json` (kept under
   `data/seed/` to match the repo's one existing checked-in-data convention, in an `external-reference/`
   subfolder since it isn't authored game content like the rest of `data/seed/`) — an array of `{name, side,
   qualities?, unlock?, typeClass?, weaknesses?, damageVs?, description?}`, produced once by querying
   the fan tool's Scratch VM state (as done manually this session) and hand-reviewed before
   committing — not regenerated automatically, since the source is a static download, not a live
   feed. **Owner-reviewed and approved 2026-08-23** (781 rows: 617 plants, 164 zombies) via a
   generated review page, before being treated as commit-ready. Two extraction bugs were found and
   fixed during this session before that approval: `qualities` initially came back empty for every
   row due to a Scratch-VM object wrapper the unwrap logic didn't yet handle (fixed — 375/617 plants
   now carry real tags); `description` wasn't extracted at all in the first pass (added — 574/617
   plants). `Zombie-id-modifiers` (a separate buff/upgrade-gacha catalog in the source tool, 15
   entries) was deliberately left uncollected — a different data domain from descriptive type text.
2. `RpgStore.ImportAlmanacEnrichment(IEnumerable<...> rows)` matches each row to an
   `almanac_seed` row by normalized `display_name`/`type_name`, upserts the enrichment table, and
   **reports unmatched rows** (name-matching a 3.6.1 export against 3.8.1 content will always miss
   some — new/renamed/rebalanced plants) rather than silently dropping them.
3. `POST /api/almanac/seed/enrich` triggers the import from the checked-in file and returns
   `{ matched, unmatched: [names] }` so gaps are visible, not assumed away.

`AlmanacSeedDto` gains an optional nested block, never merged into the trusted top-level fields:

```csharp
public sealed class AlmanacSeedEnrichmentDto
{
    public string[]? Qualities { get; set; }
    public string? UnlockCondition { get; set; }
    public string? TypeClass { get; set; }
    public string? WeaknessesText { get; set; }
    public string? DamageVsText { get; set; }
    public string? Description { get; set; }   // added 2026-08-23, see schema note above
    public string Source { get; set; } = "";
}
// on AlmanacSeedDto:
public AlmanacSeedEnrichmentDto? Enrichment { get; set; }   // null when unmatched/not imported
```

`GET /api/almanac/seed/...` left-joins this table automatically; its absence (`Enrichment: null`)
is the normal case for anything added since 3.6.1 or not yet imported — never treated as an error.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AlmanacSeed"
dotnet test tests\FusionRpg.E2E.Tests --filter "FullyQualifiedName~AlmanacSeed"
.\scripts\guard-dal.ps1   # new table/queries must stay inside FusionRpg.Data
```

## Structure

```
src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs   (schema, DTO, RebuildAlmanacSeed, Get/List)
src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeedEnrichment.cs  (enrichment table + import + join)
src/FusionRpg.Data/Sqlite/RpgStore.cs               (EnsureColumn/CREATE TABLE registration only)
src/FusionRpg.Server/Program.cs                     (4 routes: get/list/rebuild + enrich)
data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json  (checked-in export)
tests/FusionRpg.Data.Tests/AlmanacSeedTests.cs       (parsing, upsert, baseline-sample selection)
tests/FusionRpg.Data.Tests/AlmanacSeedEnrichmentTests.cs  (name-matching, unmatched reporting)
tests/FusionRpg.E2E.Tests/AlmanacSeedE2ETests.cs     (REST round-trip)
```

## Testing strategy

Unlike the other three modules, this one is pure C#/SQLite with no Unity/IL2CPP surface — full
automated coverage is expected, following `RpgStoreSmokeTests.cs` / `AlmanacTextE2ETests.cs`
conventions:

- **Cost regex, table-driven, all three `cost_status` outcomes:** `'absent'` (no `cost` field —
  most rows), `'parsed'` (the real samples: Peashooter 100/7.5, Jalapeno 125/50, plus a hex-color
  variant `<color=#FF0000>`), `'unparsed'` (a deliberately malformed string, e.g. `7.5.5秒`) — asserts
  `sun_cost`/`cooldown_sec` stay `NULL` in every case except `'parsed'`, never a partial/guessed
  value, and that `'unparsed'` is distinguishable from `'absent'` in the rebuild summary.
- **Cooldown parsing is culture-invariant:** run the same test under a comma-decimal
  (`CultureInfo`) thread culture and confirm `7.5秒` still parses to `7.5`, not `75`.
- **Baseline sample selection, per side:** given multiple `spawn_stats` rows for one plant with
  `source` values `start` and `setPlantAttributes` at different `captured_utc`, the `start` row wins
  regardless of ordering; same test shape for zombie with `initHealth` vs `reinforce`/
  `setHealthInTravel`. (Not asserting `reinforce` differs numerically from `start` — only that the
  selection logic prefers `start`/`initHealth` when both exist, independent of whether their values
  happen to match in a given fixture.)
- **Unobserved type:** no `spawn_stats` row ⇒ `stats_observed = false`, `hp`/`attack`/`armor`/
  `armor_max` all null — never falls back to `types.hp_base` (regression test: seed a `types` row
  with `hp_base` set and no `spawn_stats` row, assert the rebuilt `almanac_seed` row still has
  `hp = null`).
- **Plant armor is always null:** given a plant `spawn_stats` sample (which never carries armor
  keys), assert `armor`/`armor_max` are `NULL` even with `stats_observed = true` — a permanent state,
  not a bug to keep re-flagging.
- **Rebuild transaction rollback:** inject a failure partway through a rebuild (e.g. a malformed row
  after N good ones) and assert the table is unchanged from before the rebuild — not partially
  updated with mixed `contract_version`/`rebuilt_utc` stamps.
- **Stale-row removal:** seed an `almanac_seed` row for a `(side, type_id)` with no matching
  `type_almanac_dump` row, run a rebuild, assert the row is gone.
- **Rebuild idempotency:** running the rebuild twice with unchanged source data produces byte-identical
  rows (except `rebuilt_utc`).
- **Naming falls back to `types` on read**, not just at rebuild time: update a `types` row's
  `display_name` after a rebuild, assert `GET /api/almanac/seed/...` reflects the new name without
  needing another rebuild.
- **`guard-dal.ps1` passes** — no SQL leaks outside `FusionRpg.Data`.
- **E2E:** `POST /api/almanac/seed/rebuild` then `GET /api/almanac/seed/plant/{knownId}` returns the
  expected shape end to end through the real server.
- **Enrichment name-matching:** given a planted checked-in export with one exact-name match, one
  near-miss (case/whitespace only — should still match after normalization), and one genuinely
  absent name (renamed/removed since 3.6.1), `POST /api/almanac/seed/enrich` matches the first two
  and reports the third in `unmatched` — never silently drops it.
- **Enrichment never contaminates trusted fields:** a row with `Enrichment` populated has identical
  `sun_cost`/`hp`/`attack`/etc. to the same row before import — the join adds a nested object, it
  never rewrites a core column.

## Boundaries

- **Always:** source combat numbers from `spawn_stats.stats_json`, stamp `contract_version` on every
  row, leave a field `NULL` rather than guess when parsing fails; keep external enrichment in its
  own table, joined, never merged into core columns.
- **Ask first:** changing `AlmanacSeedContractVersion`'s meaning (what triggers a bump) once other
  systems (Demon species catalog) start reading this table; adding a new parsed numeric field beyond
  cost/cooldown (needs its own format catalog first, per the "not attempting" note above); adding a
  second external enrichment source (name-matching heuristics multiply per source).
- **Never:** read `types.hp_base`/`sample_json` for combat numbers; fold recipe data into this table
  (owner call — stays `/api/recipes`); build any FE surface in this module; match enrichment rows by
  anything other than name (no `type_id` coincidence-matching across tool versions); treat
  `almanac_seed_enrichment` as SSOT for anything — it is reference-only, sourced from a fan tool on
  an older game version.

## Success criteria

1. `POST /api/almanac/seed/rebuild` produces one row per known `(side, type_id)` from
   `type_almanac_dump`, with `stats_observed` genuinely reflecting `spawn_stats` coverage (expected
   near-100% once `almanac-spawn-coverage` has run).
2. Every `cost_status = 'parsed'` row's `sun_cost`/`cooldown_sec` matches what a human reading the
   raw almanac card would see — spot-checked against the live samples in this spec — and every
   `cost_status = 'unparsed'` row is visible in the rebuild summary as something to look at, never
   silently merged with `'absent'`.
3. No numeric combat field is ever populated from `types.hp_base`, and plant rows never carry a
   non-null `armor`/`armor_max` — both provable by the regression tests above.
4. A rebuild that fails partway leaves the table exactly as it was before (transaction rollback), and
   a rebuild after a `type_almanac_dump` row disappears removes the corresponding `almanac_seed` row.
5. `guard-dal.ps1` and the full `FusionRpg.Data.Tests`/`FusionRpg.E2E.Tests` suites stay green.
6. The table is directly queryable by the owner (`sqlite3 rpg-hot.sqlite "select * from
   almanac_seed"`) without needing the REST layer — a real reusable BE artifact, not just an API
   response shape.
7. `POST /api/almanac/seed/enrich` reports every unmatched name from the checked-in 3.6.1 export —
   coverage gaps from version drift are visible, never silently absorbed.
