# Tasks: Almanac — capture-fix, recipes-fix, seed (BE)

Plan: [almanac-plan.md](almanac-plan.md) · Map: [../docs/architecture/almanac-map.md](../docs/architecture/almanac-map.md)
`almanac-spawn-coverage` excluded — spec-stage only, blocked on 3 owner decisions (see its spec).

## Module 1: injector fixes (`almanac-capture-fix`, `almanac-recipes-fix`)

- [ ] **Task T1: Skip window capture during the automated sweep**
  - Description: add `bool includeWindowText = true` to `AlmanacTextCapture.TryCapture`; only call
    `CaptureWindowTmp` when true; `GameHooks.EnqueueFullAlmanacText` passes `false`.
  - Acceptance: sweep-triggered captures never carry another type's `uiXxx` text; click-driven path
    byte-identical (default stays `true`).
  - Verify: build injector; live — leave an almanac window open on one type, trigger the sweep
    (`POST /api/cheats/action {"action":"almanac-dump-all"}`), `GET /api/almanac/dump?side=zombie`,
    assert no swept entry's `uiName`/`uiInfo` matches the left-open type. Re-test click enrichment
    **in a fresh game launch** — `Sent` cache invalidates that path within one process life.
  - Files: `src/FusionRpg.Injector/AlmanacTextCapture.cs`, `src/FusionRpg.Injector/GameHooks.cs`. Scope: S.

- [ ] **Task T2: Stop the recipes auto-latch from firing before a level exists**
  - Description: `EnqueueRecipes()` is called from `EnqueueTypeCatalog()`'s automatic post-connect
    path (`RpgClient.cs:128` → `RequestTypeCatalog` → `PumpMainThread` → `EnqueueTypeCatalog`), which
    fires at injector boot before any board exists — permanently latching `_recipesDumped = true`.
    Remove `EnqueueRecipes()` from that automatic path (recipes need a board; the rest of the type
    catalog doesn't); call it only from the cheat action or a board-start hook, per T3's live check.
  - Acceptance: a fresh injector launch does not latch `_recipesDumped` before a level starts.
  - Verify: fresh launch, check log immediately for `EnqueueTypeCatalog` activity and confirm
    `_recipesDumped` is not already true before entering a level.
  - Files: `src/FusionRpg.Injector/GameHooks.cs`. Scope: XS.

- [ ] **Task T3: Diagnose and close remaining silent-drop points in the recipe dump**
  - Description: with the auto-latch fixed, start a level, trigger `recipes`, confirm
    `[cheat] recipes enqueued` now appears in the log. If `GET /api/recipes` is still empty, add
    logging on the `dict == null` return, the per-entry cast-failure `catch`, and the null-`Client`
    case; check whether server-side `ProjectRecipes`'s swallowing `catch` (`RpgStore.cs:2493`) is the
    actual sink — don't assume the defect is Injector-only.
  - Acceptance: `GET /api/recipes` returns real fusion-tree entries triggered from inside a level, or
    every remaining silent-drop point now logs instead of looking identical to success.
  - Verify: live, per spec testing steps 2-5. **Exact code change is not decided until this
    diagnosis runs — do not pre-write the fix.**
  - Files: `src/FusionRpg.Injector/GameHooks.cs`, possibly `src/FusionRpg.Data/Sqlite/RpgStore.cs`
    (`ProjectRecipes`) if server-side loss is confirmed. Scope: S–M depending on diagnosis outcome.
  - Dependencies: T2.

### Checkpoint 1 (Module 1)

- [ ] Injector builds clean (`dotnet build src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj -c Release`)
- [ ] Guards pass: `guard-single-writer.ps1`, `guard-dal.ps1`, `guard-secondary-no-unity.ps1`, `guard-funnel-delta.ps1`
- [ ] Live: sweep produces zero cross-contaminated `uiXxx` entries; `GET /api/recipes` non-empty from inside a level
- [ ] Owner flagged before running the full sweep broadly (T1's "first write wins" foreclosure — see plan Risks)
- [ ] Review with owner before Module 2

## Module 2: `almanac-seed` core (fully unit-testable, no game session needed)

- [ ] **Task T4: Schema + DTO + identity/flavor rebuild**
  - Description: new `almanac_seed` table (hot DB) + `AlmanacSeedDto` in a new
    `RpgStore.AlmanacSeed.cs` partial file. `RebuildAlmanacSeed()`: for each `type_almanac_dump` row,
    populate `side/type_id/type_name/display_name/flavor_info/flavor_introduce/contract_version/
    rebuilt_utc` only (cost/stats columns exist but stay null this task) — markup-stripped, wrapped
    in one transaction, deletes stale rows (no matching `type_almanac_dump` row), naming falls back
    to `types` on read (matching `RpgStore.Almanac.cs`'s existing pattern, not just at rebuild time).
  - Acceptance: rebuild produces one row per known type; a mid-rebuild failure leaves the table
    unchanged (rollback); a `types` display-name update is visible on read without a re-rebuild.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AlmanacSeed"`;
    `.\scripts\guard-dal.ps1`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs` (new), `src/FusionRpg.Data/Sqlite/RpgStore.cs`
    (table registration), `tests/FusionRpg.Data.Tests/AlmanacSeedTests.cs` (new). Scope: M.

- [ ] **Task T5: Cost/cooldown parsing**
  - Description: add the two regexes (`花费`/`冷却时间`, `red` or `#RRGGBB` color, culture-invariant
    double parse), `cost_status` (`absent`/`parsed`/`unparsed`) + `sun_cost`/`cooldown_sec` into the
    rebuild.
  - Acceptance: all three `cost_status` outcomes covered, including a hex-color sample and a
    deliberately malformed one (`7.5.5秒`); comma-decimal culture doesn't misparse `cooldown_sec`.
  - Verify: `dotnet test ... --filter "FullyQualifiedName~AlmanacSeed"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs`, `tests/FusionRpg.Data.Tests/AlmanacSeedTests.cs`. Scope: S.
  - Dependencies: T4.

- [ ] **Task T6: Combat stats baseline (spawn_stats SSOT, split by side)**
  - Description: two queries (plant: `source='start'`; zombie: `source='initHealth'`), earliest
    `captured_utc`, reading `hpBase`/`attackBase`/`armorBase`/`armorMaxBase` from `stats_json`. Plant
    rows always leave `armor`/`armor_max` null (our capture never emits them for plants — not a bug).
    Never reads `types.hp_base`.
  - Acceptance: unobserved type ⇒ `stats_observed=false`, all four numeric fields null; a seeded
    `types.hp_base` with no `spawn_stats` row still yields `hp=null` (regression test); plant rows
    never carry non-null armor.
  - Verify: `dotnet test ... --filter "FullyQualifiedName~AlmanacSeed"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs`, `tests/FusionRpg.Data.Tests/AlmanacSeedTests.cs`. Scope: S.
  - Dependencies: T4.

- [ ] **Task T7: REST endpoints**
  - Description: `GET /api/almanac/seed?side=`, `GET /api/almanac/seed/{side}/{typeId}`,
    `POST /api/almanac/seed/rebuild` (returns the built/costParsed/costUnparsed/statsObserved/
    staleRemoved summary).
  - Acceptance: rebuild-then-get round-trips through the real server; 404 for an unknown type.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter "FullyQualifiedName~AlmanacSeed"`.
  - Files: `src/FusionRpg.Server/Program.cs`, `tests/FusionRpg.E2E.Tests/AlmanacSeedE2ETests.cs` (new). Scope: S.
  - Dependencies: T4, T5, T6.

### Checkpoint 2 (Module 2 core)

- [ ] `dotnet test tests\FusionRpg.Data.Tests tests\FusionRpg.E2E.Tests` green
- [ ] `.\scripts\guard-dal.ps1` green
- [ ] Manual: `POST /api/almanac/seed/rebuild` then spot-check `GET /api/almanac/seed/plant/0`
  (Peashooter) against the live samples recorded in the spec
- [ ] Review with owner before Module 3

## Module 3: external enrichment (optional add-on, separate table)

- [ ] **Task T8: Enrichment table + import + join**
  - Description: new `almanac_seed_enrichment` table + `RpgStore.AlmanacSeedEnrichment.cs`;
    name-matching import from a checked-in export
    (`data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json` — needs
    producing/reviewing as part of this task, via one broad extraction pass, not incremental
    probing); `POST /api/almanac/seed/enrich` returns `{matched, unmatched}`;
    `GET /api/almanac/seed/...` left-joins it into a nested `Enrichment` object, never merged into
    core columns.
  - Acceptance: exact-name and normalized-name matches succeed; a genuinely absent name is reported
    in `unmatched`, never dropped silently; enrichment import never changes any core column's value.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AlmanacSeedEnrichment"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeedEnrichment.cs` (new),
    `src/FusionRpg.Server/Program.cs`,
    `data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json` (new),
    `tests/FusionRpg.Data.Tests/AlmanacSeedEnrichmentTests.cs` (new). Scope: M-L (4 files).
  - Dependencies: T7.

### Checkpoint 3 (Complete)

- [ ] Full `FusionRpg.Data.Tests` + `FusionRpg.E2E.Tests` suites green
- [ ] `guard-dal.ps1` green
- [ ] Owner reviews the enrichment export file content before it's committed (fan-tool data,
  hand-reviewed per the spec's own boundary)
