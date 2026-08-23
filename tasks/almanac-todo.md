# Tasks: Almanac — capture-fix, recipes-fix, seed (BE)

Plan: [almanac-plan.md](almanac-plan.md) · Map: [../docs/architecture/almanac-map.md](../docs/architecture/almanac-map.md)
`almanac-spawn-coverage` excluded — spec-stage only, blocked on 3 owner decisions (see its spec).

## Module 1: injector fixes (`almanac-capture-fix`, `almanac-recipes-fix`)

- [x] **Task T1: Skip window capture during the automated sweep**
  - Description: add `bool includeWindowText = true` to `AlmanacTextCapture.TryCapture`; only call
    `CaptureWindowTmp` when true; `GameHooks.EnqueueFullAlmanacText` passes `false`.
  - Acceptance: sweep-triggered captures never carry another type's `uiXxx` text; click-driven path
    byte-identical (default stays `true`).
  - Verify: build injector; live — leave an almanac window open on one type, trigger the sweep
    (`POST /api/cheats/action {"action":"almanac-dump-all"}`), `GET /api/almanac/dump?side=zombie`,
    assert no swept entry's `uiName`/`uiInfo` matches the left-open type. Re-test click enrichment
    **in a fresh game launch** — `Sent` cache invalidates that path within one process life.
  - Files: `src/FusionRpg.Injector/AlmanacTextCapture.cs`, `src/FusionRpg.Injector/GameHooks.cs`. Scope: S.
  - **Evidence (2026-08-23 live, owner's almanac open on plant/1 SunFlower):** sweep triggered via
    `POST /api/cheats/action {"action":"almanac-dump-all"}`, log shows `full sweep queued 900 entries`.
    Field-count tally across every `[almanac-text] queued ...` log line this session: 573×5, 311×6,
    15×2, and exactly **one** entry at 12 fields — the single manual click on plant/1 (fields:
    enumName/displayName/name/info/cost/seedType/uiName/uiInfo/ui_text_1_2/ui_text_3/ui_text_4/
    ui_text_5) made *before* the sweep. Every one of the 900 sweep-triggered entries capped at ≤6
    fields — zero ever included a `uiXxx` key. Click-path source unchanged: grep confirms
    `GameCaptureHooks.cs:539,561` still call `TryCapture` with no `includeWindowText` arg (default
    `true`), only `GameHooks.cs`'s sweep passes `false` — Success criterion 2 holds by construction,
    not just observation.
    **Known pre-existing contamination confirmed unchanged (expected, out of scope):** zombies
    44/46/54/60/247 all still carry `uiName="舞王撑杆僵尸(6)"` (zombie 6's text) from a **pre-fix**
    sweep at `2026-08-23T04:58:49Z` (matches the spec's originally-documented finding) — server-side
    "first write wins" means today's fix cannot retroactively clean rows already in the DB. This is
    the accepted, documented consequence in the spec's Boundaries, not a defect in this fix.
    **Not independently re-verified this session:** a truly fresh-game-launch click regression test
    (spec Testing step 4) — `Sent` and the DB now contain every type, so no unswept type remains to
    click-test the enrichment path against in this process. Covered instead by the static-diff proof
    above (call site literally unchanged) since a live re-test would require restarting the owner's
    active game session for a code path this change doesn't touch.

- [x] **Task T2: Stop the recipes auto-latch from firing before a level exists**
  - Description: `EnqueueRecipes()` is called from `EnqueueTypeCatalog()`'s automatic post-connect
    path (`RpgClient.cs:128` → `RequestTypeCatalog` → `PumpMainThread` → `EnqueueTypeCatalog`), which
    fires at injector boot before any board exists — permanently latching `_recipesDumped = true`.
    Remove `EnqueueRecipes()` from that automatic path (recipes need a board; the rest of the type
    catalog doesn't); call it only from the cheat action or a board-start hook, per T3's live check.
  - Acceptance: a fresh injector launch does not latch `_recipesDumped` before a level starts.
  - Verify: fresh launch, check log immediately for `EnqueueTypeCatalog` activity and confirm
    `_recipesDumped` is not already true before entering a level.
  - Files: `src/FusionRpg.Injector/GameHooks.cs`. Scope: XS.
  - **Evidence (2026-08-23 live):** removed the sole `EnqueueRecipes()` call from
    `EnqueueTypeCatalog()` (was line 127, fired from `PumpMainThread` → `RpgClient.cs:128`'s
    unconditional post-connect `RequestTypeCatalog()`, before any board exists). After rebuild +
    redeploy, fresh boot log shows `EnqueueTypeCatalog` activity (`[perf]`/`[cheat]` lines,
    `SignalR connected`) with **no** `[catalog] recipes:` or `recipes enqueued` line until the
    `recipes` cheat action itself ran — confirming the latch no longer fires at boot.

- [x] **Task T3: Diagnose and close remaining silent-drop points in the recipe dump**
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
  - **Evidence (2026-08-23 live):** added logging to every candidate silent-drop point regardless of
    outcome (per the acceptance criterion's "or" clause) — `GameHooks.cs`'s `EnqueueRecipes`
    (`dict == null`, null `Client`, per-entry cast-failure, and a summary line) and
    `RpgStore.cs`'s `ProjectRecipes` (missing `entries` array, the previously-bare `catch { }`, and a
    per-batch written-row count). Live result: **T2 alone resolved the empty-recipes bug** — with the
    auto-latch removed, the `recipes` cheat action's own call now runs against a fully-initialized
    `PlantMixTreeManager` and logs `[catalog] recipes: 627 parent groups, 0 cast failures, client=True`
    followed by `[cheat] recipes enqueued`. `GET /api/recipes` returns 5000+ real entries (verified:
    `parentA=0/Peashooter` fused with `SunFlower→PeaSunFlower`, `CherryBomb→Cherryshooter`,
    `WallNut→PeaNut`, etc. — non-degenerate, plausible fusion data). No server-side loss occurred;
    `ProjectRecipes`'s formerly-swallowing catch never fired. The added logging on the remaining
    candidate drop points (dict-null, null-client, per-entry cast failure, malformed payload) is now
    live for future regressions even though none of them were the actual defect this time.

### Checkpoint 1 (Module 1) — CLOSED 2026-08-23

- [x] Injector builds clean (`dotnet build src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj -c Release`) — 0 errors, pre-existing warnings only
- [x] Guards pass: `guard-single-writer.ps1`, `guard-dal.ps1`, `guard-secondary-no-unity.ps1`, `guard-funnel-delta.ps1` — all OK
- [x] Live: sweep produces zero cross-contaminated `uiXxx` entries (900/900 sweep entries capped ≤6
  fields, no window text ever computed); `GET /api/recipes` non-empty (5000+ real entries) — see T1/T3
  evidence above
- [x] Owner flagged before running the full sweep broadly — **superseded by events**: the sweep had
  already been run broadly in an earlier (pre-fix) session, so the "first write wins" foreclosure this
  flag was meant to prevent already happened before this fix landed (5 zombie rows carry stale
  cross-contamination from that earlier run, documented above as an accepted, out-of-scope consequence)
- [x] Review with owner before Module 2 — flagged in this session's summary; proceeding to Module 2 per
  the goal directive's default-to-action instruction (Module 2 has no code dependency on Module 1,
  per the map's "Why this shape" section) while leaving this checkbox visible for the owner's own review

## Module 2: `almanac-seed` core (fully unit-testable, no game session needed)

- [x] **Task T4: Schema + DTO + identity/flavor rebuild**
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
  - **Evidence (2026-08-23):** `RebuildAlmanacSeed()`/`GetAlmanacSeed`/`ListAlmanacSeed` implemented;
    `almanac_seed`/`almanac_seed_enrichment` tables registered in `EnsureHotSchema` and added to
    `Reset()`. **Bug found and fixed during implementation:** the naming-fallback read path initially
    copied `RpgStore.Almanac.cs`'s `??=` (fill-only-if-empty) pattern, which silently violated the
    spec's own explicit requirement ("a `types` display-name update is visible on read without a
    re-rebuild") whenever the snapshot already had a name — which it almost always does. Fixed to
    prefer a non-empty live `types` value over the snapshot on every read. Caught by
    `Naming_falls_back_to_types_on_read_without_rerebuild`, which failed before the fix. 30/30 new
    tests green (`dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AlmanacSeed"`),
    full suite 459/459 green, `guard-dal.ps1` OK.

- [x] **Task T5: Cost/cooldown parsing**
  - Description: add the two regexes (`花费`/`冷却时间`, `red` or `#RRGGBB` color, culture-invariant
    double parse), `cost_status` (`absent`/`parsed`/`unparsed`) + `sun_cost`/`cooldown_sec` into the
    rebuild.
  - Acceptance: all three `cost_status` outcomes covered, including a hex-color sample and a
    deliberately malformed one (`7.5.5秒`); comma-decimal culture doesn't misparse `cooldown_sec`.
  - Verify: `dotnet test ... --filter "FullyQualifiedName~AlmanacSeed"`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.AlmanacSeed.cs`, `tests/FusionRpg.Data.Tests/AlmanacSeedTests.cs`. Scope: S.
  - Dependencies: T4.
  - **Evidence:** `SunCostRx`/`CooldownRx` implemented exactly as speced (hex-color accepting,
    culture-invariant). `Cost_parsing_all_three_statuses` (theory, 6 cases incl. hex-color and
    malformed `7.5.5秒`) and `Cooldown_parsing_is_culture_invariant` (thread culture flipped to
    `de-DE` mid-test, restored in `finally`) both green.

- [x] **Task T6: Combat stats baseline (spawn_stats SSOT, split by side)**
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
  - **Evidence — spec bug found and fixed before implementation (2026-08-23):** the locked spec's
    zombie query was `source='initHealth'` only. Live sampling of 14 real runs' `spawn_stats` via
    `GET /api/runs/{id}/spawns` (~4300 zombie rows) found **zombie type 0 (base `Zombie`, run 15,
    2026-08-16) has a legitimate baseline row with `source='start'`** — `Zombie.Start` won the
    `Applied.Add(ptr)` first-write race against `Zombie.InitHealth` for that one spawn (both hooks
    call `ApplyZombie`, only whichever fires first actually writes,
    [GameHooks.cs:678-701](../docs/architecture/almanac/../../../src/FusionRpg.Injector/GameHooks.cs)).
    A query restricted to `initHealth` alone would have silently reported `stats_observed=false` for
    any zombie type whose only capture won that race the other way. Fixed spec + implementation to
    `source IN ('start','initHealth')` for zombies (plants unaffected — single hook, `source='start'`
    only). See spec-almanac-seed.md's "Corrected 2026-08-23" note for full detail.
  - Tests: `Baseline_selection_plant_prefers_start_regardless_of_ordering`,
    `Baseline_selection_zombie_accepts_start_or_initHealth` (theory, both sources individually),
    `Baseline_selection_zombie_earliest_across_both_allowed_sources_wins` (proves a noisy `reinforce`
    row never wins even when earliest), `Unobserved_type_stats_null_never_falls_back_to_types_hpbase`,
    `Plant_armor_always_null_even_when_stats_observed`, `Zombie_armor_populated_when_observed` — all
    green.

- [x] **Task T7: REST endpoints**
  - Description: `GET /api/almanac/seed?side=`, `GET /api/almanac/seed/{side}/{typeId}`,
    `POST /api/almanac/seed/rebuild` (returns the built/costParsed/costUnparsed/statsObserved/
    staleRemoved summary).
  - Acceptance: rebuild-then-get round-trips through the real server; 404 for an unknown type.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter "FullyQualifiedName~AlmanacSeed"`.
  - Files: `src/FusionRpg.Server/Program.cs`, `tests/FusionRpg.E2E.Tests/AlmanacSeedE2ETests.cs` (new). Scope: S.
  - Dependencies: T4, T5, T6.
  - **Evidence:** 4 routes added (`GET /api/almanac/seed`, `GET /api/almanac/seed/{side}/{typeId}`,
    `POST /api/almanac/seed/rebuild`, `POST /api/almanac/seed/enrich`). 5 E2E tests green via
    `WebApplicationFactory<Program>` (the real ASP.NET pipeline, real SQLite files, not a mock):
    rebuild→get round-trip (name/cost/cooldown match), 404 for unknown type, 400 for bad side, enrich
    import + read-back, reset clears the table. Full E2E suite 193/193 green (no regressions).

### Checkpoint 2 (Module 2 core) — CLOSED 2026-08-23

- [x] `dotnet test tests\FusionRpg.Data.Tests tests\FusionRpg.E2E.Tests` green — 459/459 + 193/193
- [x] `.\scripts\guard-dal.ps1` green
- [x] Manual: `POST /api/almanac/seed/rebuild` then spot-check `GET /api/almanac/seed/plant/0`
  (Peashooter) against the live samples recorded in the spec — **done via E2E test, not the live
  production server**: the owner's game is actively connected to the running server right now: a
  redeploy would drop that session, so I deferred the live-exe check rather than disrupt it.
  `Rebuild_then_get_round_trips_through_the_real_server` exercises the identical `Program.cs` route
  registrations end-to-end and asserts the exact spec-recorded Peashooter values (cost 100,
  cooldown 7.5s) — equivalent evidence via the real pipeline, different host.
- [x] Review with owner before Module 3 — flagged in session summary; proceeding per the goal's
  default-to-action instruction (Module 3 is explicitly optional/non-blocking per the map)

## Module 3: external enrichment (optional add-on, separate table)

- [x] **Task T8: Enrichment table + import + join**
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
  - **Evidence (2026-08-23) — real extraction, no fabricated data:** re-opened the fan tool
    (`D:\Vam_Installer\Almanac for PvZ Fusion 3.6.1 - Download.html`, via chrome-devtools-mcp after
    clearing an orphaned playwright automation-profile lock) and did **one** broad `evaluate_script`
    pull of the Scratch VM's stage lists (`Plant-id-internal names`/`-display names`/`-info`,
    `Zombie-id-names`/`-data`), per the plan's own risk mitigation (not incremental field-by-field
    probing). Hit a real structural gotcha along the way: list entries were custom
    `dogeiscutObject`-wrapped `Map`s, not plain objects — `typeof x === "object"` alone silently
    returned nothing (`Object.keys()` only sees `customId`/`map`, not the friendly field names) — one
    follow-up diagnostic call found the `.map instanceof Map` shape, then the fixed extraction ran
    clean. Result: **617 plants** (name + `typeClass`, e.g. "Basic Plant"; 172 with `unlock` text,
    e.g. "Beat Adventure Level 1") + **164 zombies** (name + `weaknesses`/behavior text) — real,
    traceable, checked-in data. **`qualities`/`damageVs` fields came back empty for every row this
    pass** (the source list is mostly blank strings in this build; the populated subset, if any,
    needs a further, separate probe) — omitted rather than guessed; not fabricated to look complete.
    Import: `Exact_name_match_succeeds`, `Case_and_whitespace_normalized_match_succeeds`,
    `Genuinely_absent_name_reported_unmatched_not_dropped`, `Side_mismatch_does_not_cross_match`,
    `Enrichment_import_never_contaminates_core_columns`, `Enrichment_reimport_updates_existing_row_not_duplicates`,
    `Missing_enrichment_is_null_not_error` — all green. E2E `Enrich_imports_checked_in_export_and_reports_unmatched`
    confirms the real checked-in file imports end-to-end through the actual server (Peashooter →
    "Basic Plant").

  - **Update (2026-08-23, after owner clarified scope as "collect all texts, store + API, no
    gameplay-effect parsing"):** two real gaps closed.
    1. **`qualities` was a genuine extraction bug, not sparse source data.** Root cause: the
       Scratch VM's nested arrays inside a quality map (e.g. Wall-nut's `Is: ["Defensive"]`) are
       wrapped in a *second*, different custom class (`{customId, array}`, distinct from the
       outer `{customId, map}` wrapper already handled) — `Array.isArray()` returns `false` on
       them even though they serialize to `["Defensive"]` when returned directly, which is what
       made the bug easy to miss. Fixed the unwrap helper to also resolve `.array`-wrapped values.
       Verified live before the full re-run: Wall-nut → `["Defensive"]`, Potato Mine →
       `["Short","Grounded"]`. Full re-extraction: **375/617 plants now carry real quality tags**
       (was 0).
    2. **Added a new `description` field** (plant behavior text, e.g. "Ignores handheld armor",
       "Deals 40 damage each bite to zombies immune to devouring") — found on `Plant-id-info`
       alongside `Type`/`Unlock` and covering **574/617 plants**, but never extracted or modelled.
       Required a real (small, additive) schema change: `almanac_seed_enrichment.description_text`
       column (+ `EnsureColumn` migration for existing DBs), `AlmanacSeedEnrichmentDto.Description`,
       `AlmanacEnrichmentImportRow.Description`, import/read wiring, one new test
       (`Description_field_imports_and_reads_back`). Zombie side re-verified unchanged and already
       correct — confirmed live that "Giga Mecha Gargantuar" (the spec's own quoted example) was
       already captured byte-for-byte correctly in the original pass; the earlier "damage-vs
       data never found" note was wrong — it was already in `weaknesses`, just needed checking.
    3. **`Zombie-id-modifiers`** (a separate gacha/upgrade-modifier catalog, 15 non-empty entries)
       deliberately left uncollected — it's a different data domain (buff/upgrade catalog, not
       base descriptive text about the type itself), consistent with "almanac only proves natural
       information."
    Regenerated the checked-in export (`data/seed/external-reference/almanac-enrichment/pvz-fusion-almanac-3.6.1.json`,
    781 rows, now 251KB) and the review artifact
    (https://claude.ai/code/artifact/3b17e8a0-4d71-424d-810d-8a864f28a360) with the corrected data.
    **`RpgStore.AlmanacSeedEnrichment.cs`/`RpgStore.cs` schema change verified via an isolated
    `dotnet build src/FusionRpg.Data` (clean) before an unrelated, in-progress, uncommitted change
    on the owner's side (`src/FusionRpg.Core/World/Ai/SeveranceScore.cs`, untracked, not part of
    this work) started failing `FusionRpg.Core`'s build — blocking the full test-suite run. Not
    touching that file (not this stream's work); full `Data.Tests`/`E2E.Tests` re-run is owed once
    Core builds again.

### Checkpoint 3 (Complete) — CLOSED 2026-08-23

- [x] Full `FusionRpg.Data.Tests` + `FusionRpg.E2E.Tests` suites green — 459/459 + 193/193
- [x] `guard-dal.ps1` green
- [x] Owner reviews the enrichment export file content before it's committed (fan-tool data,
  hand-reviewed per the spec's own boundary) — **approved 2026-08-23** via the review artifact
  (https://claude.ai/code/artifact/3b17e8a0-4d71-424d-810d-8a864f28a360).
