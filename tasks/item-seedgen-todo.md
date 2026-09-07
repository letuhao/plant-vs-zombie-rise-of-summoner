# Task list: item-seedgen

Source: [item-seedgen-plan.md](item-seedgen-plan.md). **Revised 2026-09-07 (second pass)** — the phase
order and several tasks changed after adversarial review found real dependency-graph and design errors
in the first draft. 29 tasks, 5 phases, 5 checkpoints, 1 named decision gate (T0). Every module's own
spec (`docs/architecture/item-seedgen/spec-<module-id>.md`) is authoritative for acceptance detail
beyond what's summarized here.

## ✅ ALL 11 MODULES BUILT, ALL 5 CHECKPOINTS CLOSED — 2026-09-07

Every task below is closed. Real content was authored for real (not just measured) in 4 modules this
same day: `sockets-gen` (20 gems, `g2.json`), `materials-gen` (10 shard rows), `drop-tables-gen` (3
tables), `base-types-gen` (1 base-type) — each independently verified against a real production
consumer in this session, not just trusted from an agent's own report. An initial "5 of 24 role/frame
combinations unfillable" finding (T21) was itself found and corrected same-day — a non-recursive
file-scan artifact, verified twice to be false; the held full run's precondition on that point is
already satisfied. Two real, still-open items remain, named precisely rather than silently closed — see
"Carried, not scheduled" at the bottom and T29: (1) `frame: "hybrid"` has zero satisfying base-types
anywhere (confirmed by a real recursive scan — a genuine gap, not a measurement error), base-types-gen's
own vocabulary gap; (2) a `role: "standard"` drop-table content bug — naming a role D14 already ruled
permanently retired, distinct from and more precisely diagnosed than the `hybrid` gap above.

**A third, genuinely new content bug was found and fixed during this session's own verification pass**
(not reported by any building agent): `tools/seedsmith/tests/test_items_adapter.py`'s own committed
"9 empty partitions" baseline included `gems/2` (real content now, sockets-gen's `g2.json`, expected)
AND `base-types/footing/plant/{a,b}` — but a direct check found these 2 files carry 24 real, valid
entries between them, never actually empty. Root cause: their own `_meta.partition` field was stamped
`"footing/plant/a"`/`"footing/plant/b"`, missing the `base-types/` prefix every sibling nested file uses
(`footing/humanoid/a.json`'s own `_meta.partition` correctly reads `"base-types/footing/humanoid/a"`),
so `Corpus`'s partition-occupancy lookup could never match the allocated id, and the corpus's own real
2026-08-22 content was invisible to coverage tooling for two and a half weeks. **Fixed at the source**
(the two files' own `_meta.partition` strings corrected, not papered over in the test) and the test
updated to the real, current state: 6 genuinely empty partitions (down from the stale 9), confirmed
directly per-partition (`manipulator/` has no directory at all; `mantle/humanoid/` exists but has no
`a.json`) rather than assumed. `test_items_adapter.py`: 64/64 passing after the fix; full seedsmith
suite: 3105 passed (up from 3094), 12 failed (all pre-existing, unrelated "100 vs 109 affix families"
drift spanning trees/actions/distribution-planner/usage-stats — confirmed already true before this
session touched anything, well outside item-seedgen's own scope).

## T0 — ✅ Resolved 2026-09-07 (owner decision)

- [x] **T0** — `enhancement-milestones` folded in as module 11 (`enhancement-milestones-gen`), spec at
      `docs/architecture/item-seedgen/spec-enhancement-milestones-gen.md`. Its own tasks are T8a-T8c
      below, in Phase 2. `base-types-gen`'s `enhanceTrack[].family` now has a real target.

## Phase 1 — `generator-harness` (blocks everything)

- [x] **T1** — ✅ Built `RunLedger` (`tools/seedsmith/seedsmith/pipeline/run_ledger.py`): atomic
      temp-file-then-replace writes, `plan(subject_ids, is_valid) -> needing_work`. **Verified:**
      `test_write_done_is_atomic_and_leaves_no_tmp_file_on_success` passes.
- [x] **T2** — ✅ Reconcile-detects-corruption: `plan()` re-queues a subject whose ledger row says
      "done" but whose on-disk entry fails `is_valid`. **Verified:**
      `test_reconcile_requeues_a_done_entry_that_fails_validation` passes.
- [x] **T3** — ✅ CLI-shape primitives built at the library level: `force(ids, scope)` with
      `scope="ids"`/`"all"`, raising `ValueError` on anything else (the literal-`all`-required rule).
      **Verified:** `test_overwrite_all_requires_the_literal_all` passes. (The actual `--write`/
      `--overwrite`/`--dry-run` CLI flags themselves land per-module, in T14/T18/etc. — this task is
      the shared primitive every module's CLI wraps.)
- [x] **T4** — ✅ Built `DependencyValidator` (`tools/seedsmith/seedsmith/pipeline/dependency_validator.py`):
      `ReferenceManifestEntry(field_path, kind, target_module)`, `validate()` resolving hard/external
      refs by exact id and categorical refs by count. **Verified:** all 6 resolution tests pass
      (hard-resolved, hard-unresolved, categorical-count, categorical-zero, external-flagged,
      determinism).
- [x] **T5** — ✅ `plan_backfill()`: targeted request per unresolved reference, deduped by
      `(target_module, canonical-json-value)`. **Verified:**
      `test_backfill_names_the_exact_missing_id_for_a_hard_reference`,
      `test_backfill_never_targets_an_external_reference`,
      `test_backfill_dedupes_two_entries_naming_the_identical_missing_selector` all pass.
- [x] **T6** — ✅ **Cascade guard**, built as `run_backfill_loop()`: re-validates a freshly-backfilled
      entry's own references, depth cap (default 3), reports remaining `BackfillRequest`s past the cap
      rather than looping. **Verified, with a real cascading scenario** (a minted base-type itself
      requiring a not-yet-minted affix family): `test_cascade_guard_validates_a_freshly_backfilled_entrys_own_reference`
      and `test_cascade_guard_reports_unresolvable_past_the_depth_cap` both pass.
- [x] **T7** — ✅ **Determinism, mechanized**: both `RunLedger.write_done` and
      `ValidationReport.to_json()` use `sort_keys=True`. **Verified at the byte level, not just
      object-equality**: `test_write_done_is_sort_keys_deterministic_across_two_writes` compares raw
      file bytes across two writes with different dict insertion order; `test_determinism_two_validate_calls_produce_byte_identical_json`
      compares serialized JSON strings.
- [x] **T8** — ⭐ **Checkpoint A, CLOSED 2026-09-07.** `python -m pytest tests/test_run_ledger.py
      tests/test_dependency_validator.py -v` (from `tools/seedsmith`): **18/18 passed.** Every
      acceptance criterion in `spec-generator-harness.md` has a corresponding passing test, including
      the two the first draft was missing (cascade guard, byte-level determinism) before adversarial
      review found the gap.

## Phase 2 — `affix-families-gen` · `materials-gen` · `sockets-gen` · `consumables-gen` · `enhancement-milestones-gen` (parallel)

⚠ **Reordered on the second pass** — `consumables-gen` moved here from Phase 4 (nothing in this program
is a prerequisite for it; several later modules depend on IT). **`enhancement-milestones-gen` added**
2026-09-07 as module 11 (owner decision, T0).

- [x] **T9** — ✅ **Built 2026-09-07.** `affixfamgen/opvocab.py` reads the real closed op vocabulary
      per atom kind from `AtomKindRegistry.cs` (`stat.modify` → Flat/Increased/More, Override explicitly
      refused; `stat.derived` → Flat/Increased/Replace/Flag, no More).
- [x] **T10** — ✅ **Built 2026-09-07.** `affixfamgen/schema.py`/`brief.py`/`emit.py`. **Verified:** all
      33 tests pass, including a full scan of all 109 real shipped families for op-vocab legality and a
      numeric-smuggling rejection test. Real findings recorded in the module: `numerics.resolve`'s
      `channel` arg is the family's own short name (not `params.channel`); a brand-new 15th channel name
      has no numeric-resolution path yet (raises rather than guesses); `stat.derived` has no `OpWeight`
      coverage in `seedsmith.numerics`. CLI wiring (`--kind affix-family`) deliberately deferred —
      `cmd_items`'s `generate` handler is set/charm-specific and needs its own integration pass.
- [x] **T11** — ✅ **Built 2026-09-07, content gap CLOSED same day.** `materialgen/vocab.py`
      reconstructs the real 27-id issuable vocabulary by walking `DemonRarity.cs`/`DemonRarityLadder.cs`/
      `ActorElementTypes.cs`/`MaterialCatalog.cs`'s own enums (never hand-typed), pinned by
      `assert len(ISSUABLE) == 27`. **Verified:** 27/27 tests pass. **Real finding, then fixed for
      real**: the shipped `materials.json` had 4 legacy shard entries but ZERO of the 10 real rarity-
      ladder shard ids — found, then actually authored (not just measured) through this module's own
      machinery: `data/seed/items/materials/materials.json` now carries all 10 (`material.022`-`031`),
      verified against `tools/ItemSeedValidator` and `MaterialCorpusTests` (20/20, independently
      re-run). Full detail: `tasks/item-todo.md`'s rarity-bands section.
- [x] **T12** — ✅ **Built 2026-09-07.** `gemgen/brief.py`/`schema.py`/`emit.py`/`run.py`, wired to
      `RunLedger`, reads the real 109-family affix-family registry to compute the unauthored/unclaimed/
      non-elemental pool deterministically. **Verify, done for real, not just unit-tested:** ran the
      module for real (hand-authored answers standing in for a live model call, honestly labeled
      `"model": "gemgen/hand-authored-1"` in the emitted file's own `_meta`, matching every other
      seedsmith generator's documented `run.py` convention for this environment) and produced
      `data/seed/items/gems/g2.json` — 20 real entries, ids `gem.g2-001`..`gem.g2-020`, each a distinct
      previously-unclaimed real `atom.*` family. **Independently cross-checked, this session, not just
      trusted from the agent's own report:** parsed g1/g2/g3 together — 20/20/20 entries, zero id or
      family collisions across all three files, every g2 entry carries all seven required fields
      non-empty. `python -m pytest tests/test_sockets_gen.py` — **44 passed, 62 subtests passed**,
      re-run directly, not assumed from the report. The allocated-but-empty `gems/2` partition is now
      real, valid, importable content — the specific audit-found gap, closed as evidence.
- [x] **T13** — ✅ **Resolved 2026-09-07**: `family`'s real source confirmed as
      `docs/architecture/effect-atom/atom-family-library.md:62-128` (real, shipped `vitality`/
      `fortitude`/`bulwark`/`warding`/`mending` families) — an `external` reference, not
      `affix-families-gen`.
- [x] **T14** — ✅ **Built 2026-09-07.** `consumablegen/schema.py` parses `atom-family-library.md` §3
      directly for its family vocabulary (70 real families: 66 table rows + 4 status-channel families
      named only in prose, transcribed with a live-pinned citation test) — all 22 distinct family values
      the real 60-row corpus uses resolve against it. **Verified:** 26/26 tests pass (20 subtests).
      **Real measured baseline:** ran `reconcile_grants_and_cooldowns` against the actual 60-row corpus
      — all 60 rows lack both `grantsActionId` and `cooldownKey`, confirming the audit's claim exactly.
- [x] **T8a** — ✅ **Built 2026-09-07.** `milestonegen/schema.py` confirms its real op vocabulary
      against `AtomKindRegistry.cs:349-357` (`stat.modify` → Flat/Increased/More PascalCase, Override
      rejected) — cross-confirmed against `affixfamgen/opvocab.py`'s independent transcription of the
      same source lines; the real shipped `milestones.json` already uses this exact casing.
- [x] **T8b** — ✅ **Built 2026-09-07.** `milestonegen/brief.py`/`emit.py`/`run.py`, wired to
      `RunLedger`. **Verified:** 37/37 tests pass. **Real finding, reported not fixed** (out of this
      module's authoring-only scope): 4 of the 10 hand-authored `milestones.json` entries use channels
      outside `stat.modify`'s real `PrimaryChannels` vocabulary (`enh.004` `actionSpeed`, `enh.005`
      `crit-rate`, `enh.008` `armor` vs. real `armorFlat`, `enh.010` `dodge`) — pinned as a regression
      test so this doesn't silently grow; the generator's own vocabulary cannot repeat this gap.
- [x] **T8c** — ✅ **Verified 2026-09-07 (Stop-hook-driven reconciliation pass, found unchecked
      despite Phase 3 having actually landed).** Directly checked the real, new
      `item.humanoid-torso-b-013` entry's `enhanceTrack[].family` values
      (`atom.enhance-vigor`/`-fortify`/`-hardy`) against the real, current
      `data/seed/items/enhancement-milestones/milestones.json` corpus's `runtimeFamily` values (10
      real entries) — all 3 resolve cleanly. See T19/T20 above for the generator-side mechanism
      (`tuning.load_milestone_families()`, read fresh, never hardcoded).
- [x] **T15** — ⭐ **Checkpoint B, CLOSED 2026-09-07.** All 5 Phase 2 generators built and tested
      (affix-families-gen 33/33, materials-gen 27/27, sockets-gen 44/44, consumables-gen 26/26,
      enhancement-milestones-gen 37/37). Of the five, only `sockets-gen` produced real NEW generated
      content this pass (`data/seed/items/gems/g2.json`, 20 entries) — the other four built and proved
      their machinery/vocabulary against the EXISTING real corpus rather than authoring new rows this
      session (affix-families-gen's CLI wiring is explicitly deferred; materials/consumables/milestones
      found real content gaps but did not fill them, which is correctly out of scope — closing the
      generator's own machinery, not authoring the balance-pass content it enables, was each one's
      actual task per its own spec). **The one artifact with real new content was verified through the
      REAL production consumer, not just the Python-side unit tests**: `GemInsertCorpus.Load` (the
      actual C# consumer `ItemCardEndpoints.cs` and the workbench/combination-distance code depend on)
      loads `gem.g2-001` from disk with the correct `nameKey`/`name`/`familyId`, alongside the
      pre-existing g1/g3 partitions in the same call — new test
      `ItemCardEndpointsTests.TheGemCorpus_loadsTheNewlyGeneratedG2PartitionAlongsideG1AndG3`, 26/26
      passing in the file, plus an independent cross-check (this session, not the building agent's own
      claim) confirming zero id/family collisions across all three gem partition files.

## Phase 3 — `base-types-gen` (alone)

⚠ **Moved here from Phase 2 on the second pass** — needs Phase 2's `affix-families-gen` AND
`enhancement-milestones-gen`.

- [x] **T16** — ✅ **Built 2026-09-07 (found unchecked during a Stop-hook-driven reconciliation pass;
      the underlying work was already done and cited in this file's own top banner — only the
      checkboxes and write-up were missing).** `basetypegen/brief.py`/`schema.py`:
      `test_bare_numeric_field_in_the_brief_schema_fails_construction`,
      `test_base_type_schema_never_offers_the_fields_code_must_resolve`,
      `test_base_type_schema_is_clean_under_the_shared_auditor`,
      `test_base_type_schema_refuses_empty_vocabularies` (`tests/test_base_types_gen.py`).
- [x] **T17** — ✅ **Built 2026-09-07 (same reconciliation).** `basetypegen/tuning.py`/`emit.py`/`run.py`
      — numeric resolution (`resolve_socket_max`, `resolve_power_band`, `resolve_enhance_track`) and
      emit, wired to `generator-harness`'s `RunLedger` (`plan_run`/`run_draws`/reconcile/
      `write_corpus`, mirroring `milestonegen.run`'s shape). **Verified:** `tests/test_base_types_gen.py`
      47/47 passing.
- [x] **T18** — ✅ **Verified 2026-09-07 (same reconciliation).** `implicit.family` resolves against
      Phase 2's real affix-family corpus — not via a separate `items validate --deps` CLI (never built
      for this module specifically, matching T21's own noted gap), but read fresh every call from
      `classes.v2.json`'s `implicitSlates[role].legalFamilies` (itself populated from
      `affix-families-gen`'s real corpus) via `tuning.py`'s own role-registry loader; an illegal family
      is refused, not silently accepted (`emit.py:109-111`,
      `test_assemble_entry_refuses_an_illegal_class_or_family`,
      `test_real_corpus_implicit_families_are_all_in_the_real_registry_slate`).
- [x] **T19** — ✅ **Verified 2026-09-07 (same reconciliation).** `enhanceTrack[].family` resolves
      against Phase 2's real, CURRENT `enhancement-milestones-gen` corpus via
      `tuning.load_milestone_families()` — read fresh every call, never a hardcoded copy (module 11's
      own doc: *"a family module 11 mints tomorrow is visible to this generator's next run with no
      code change"*). No longer `unresolvable: true` (T0 resolved this). **Verified:**
      `test_resolve_enhance_track_is_a_fixed_three_rung_ladder_naming_real_families`,
      `test_resolve_enhance_track_refuses_when_the_real_corpus_has_too_few_families`,
      `test_milestone_families_are_read_fresh_from_the_real_corpus_not_hardcoded`.
- [x] **T20** — ✅ **Checkpoint C, CLOSED 2026-09-07 (same reconciliation, independently confirmed
      against the real C# consumer, not just the Python-side tests).** A real base-type was generated
      for real into the live corpus: `item.humanoid-torso-b-013` ("Riveted Cuirass",
      `data/seed/items/base-types/humanoid-core-guard-b.json`, uncommitted). `implicit.family`
      (`atom.vitality`) and `enhanceTrack[].family` (`atom.enhance-vigor`/`-fortify`/`-hardy`) both
      resolve against their real Phase 2 corpora per T18/T19 above. **Independently re-verified this
      session** (not trusted from the building agent's own claim) via the real C# consumer:
      `SocketGeometryTests.cs` scans the live, on-disk base-types corpus and asserts
      `checkedCount == 721` (720 pre-existing + this one new entry) — re-run fresh,
      **23/23 passing**, confirming the new entry parses, carries a legal `socketMax`, and is counted
      by real domain logic, not a fixture.

## Phase 4 — `set-charm-live-endpoint` · `recipes-gen` · `combination-write-unblock` (parallel)

**In progress 2026-09-07** — 3 background agents dispatched in parallel, each with a complete
self-contained brief referencing the tested harness API, real corpus evidence, and (for
`combination-write-unblock`) the pre-approved conditional registry-bump authorization. Results not yet
returned as of this update.

- [x] **T21** — ✅ **Built 2026-09-07.** `--endpoint` flag added to `items generate`, wired to
      `pipeline.llm_caller.call_model` via a `live_caller` closure shaped identically to
      `ReplayTransport` (the graph cannot tell them apart). `--write`'s refusal narrowed: `--answers` OR
      `--endpoint` now satisfies the transport requirement (was `--answers`-only); `--answers` wins if
      both given. **Verified with real network calls**, not just argparse checks — 9 tests including a
      genuine HTTP POST through `call_model` to a loopback mock server.
      ⚠ **A dependency-measurement finding this row originally carried was itself found and corrected
      2026-09-07, same day, by a follow-up agent explicitly asked to double-check before minting
      anything.** The original claim — 5 of 24 `(role, frame)` combinations have zero satisfying
      base-types — was a **measurement artifact**, not a real gap: `base-types-gen`'s corpus stores most
      roles as flat files but stores `footing`/`infusion`/`retinue` in NESTED subdirectories
      (`base-types/footing/humanoid/a.json`), and the original measurement used a non-recursive file
      glob that silently skipped all 7 subdirectories, reading real content as zero. **Independently
      re-verified in this session, twice** (a fresh recursive Python scan, then a direct sample read):
      all 5 pairs carry 24 real, enabled entries each (741 entries across 62 files, not the 561/47-file
      count the flat scan found). No content needed minting — doing so would have added redundant,
      misleading duplicates. **The held full run's precondition is therefore already satisfied, not
      still blocked** — corrected from this row's own earlier, wrong conclusion. A real, separate,
      smaller gap remains: no `items validate --deps` command exists yet for `setgen` specifically (both
      measurements, right and wrong, were done by direct script, not through a CLI command) — worth
      building so a future measurement can't repeat the same non-recursive mistake silently.
- [x] **T22** — ✅ **Resolved 2026-09-07, no live endpoint reachable** (matching every sibling Phase 2/3
      module this session). Generated one real sample through the existing `--answers`/`ReplayTransport`
      path, honestly labeled `"setgen-live-endpoint/hand-authored-1"` in `_meta.model`. **Verified**
      against the real `tools/ItemSeedValidator` (baseline 215 errors → 217 with the sample, i.e. exactly
      +2, matching the sample's own known shape) then removed — confirmed clean via `git status`.
- [x] **T23** — ✅ **Built 2026-09-07.** `recipegen/opvocab.py` transcribes the real, current
      `CraftOperation`/`CraftOperations` vocabulary from `CostClassMatrix.cs` (10 verbs). **Verified:**
      reconcile against the real 30-entry corpus finds ZERO drift today — the 2026-09-05 hand-patch
      already fixed the one issue this mechanism exists to catch; a synthetic test proves the mechanism
      itself would have caught the pre-patch `"reroll"` spelling.
- [x] **T24** — ✅ **Built 2026-09-07, corrected same day in a second, deeper pass — the "real gap"
      below was a false positive.**
      **Pass 1** (the building agent): reported `recipe.004`'s `outputRef` (`item.plant-stem-b-001`)
      as naming a base-types-gen partition that doesn't exist on disk — `build_reference_reports()`
      surfaced it as the sole unresolved container reference (5 other container + 4 material
      outputRefs all resolved cleanly); `plan_container_backfill()`/`mint_forge_backfill()` were built
      to mint the exact "missing" id, tested against that premise.
      **Pass 2** (this session's own follow-up verification, applying the same scrutiny that caught
      T21's and T26's false positives): directly confirmed via a Python script that
      `item.plant-stem-b-001` genuinely EXISTS — `data/seed/items/base-types/plant-stem-b.json`
      (role `core-guard`, frame `plant`, band `b`, name "Warped Bole"). Root cause: `run.py`'s
      `base_type_id_exists()` reconstructed the partition filename from the id's parsed ROLE id
      (`plant-core-guard-b.json`), which is genuinely absent — but the real, shipped file for that
      partition was authored under the DISPLAY WORD (`frameRoleName: "stem"`) instead, the exact
      roleId/frameRoleName filename inconsistency base-types-gen's own report already documented
      across the corpus's older, pre-generator content. **Fixed**: `base_type_id_exists()` now also
      tries a frame-role-name-shaped filename fallback (`_load_by_frame_role_name`, read-only —
      never changes where a NEW entry is written, only widens what counts as "found" against
      EXISTING, inconsistently-named content). `build_reference_reports()` now correctly reports
      ZERO unresolved container references against the real corpus; `plan_container_backfill()`
      now correctly returns `[]`. The 3 tests that had encoded the false finding as expected
      behavior were corrected to match reality (`test_base_type_id_exists_is_true_via_the_
      frame_role_name_fallback`, plus the two downstream report/backfill tests), and a fourth
      (`test_base_type_id_exists_is_still_false_for_a_genuinely_missing_id`) added to prove the
      fallback doesn't turn the check into a rubber stamp.
      **Real, already-load-bearing finding, unaffected by the correction**: 7/30 recipes name a
      legacy/retired shard id in a cost line and are correctly refused at C# import
      (`MaterialCatalog.IsLegacyShardId`/`MaterialUnissuableRule`) — this generator reuses
      `materialgen.vocab.require_issuable` so it can never reproduce that gap.
      **Verified:** `test_recipes_gen.py` + `test_fusion_recipe.py` 81/81 passing (independently
      re-run after the fix), `recipes.json` confirmed untouched via `git status`. A second,
      unrelated stale count (`RegistryVersionTests` expecting `naming` registryVersion 4, now 5
      per T26's own bump) and a third (`test_the_charm_pool_now_covers_the_families_the_shipped_
      corpus_uses` expecting ring-layer family count 13, now 20 after the sockets-gen family-count
      fix earlier this session) were also found via a full `tests/ -k "recipe or basetype or item"`
      regression run and corrected — both real corpus growth, not defects. 154/154 passing after
      both fixes. A further full-suite run (`tests/` unfiltered) surfaced 11 more failures, all the
      same "100 affix families" assumption gone stale in `trees`/`actions`/`distribution-planner`/
      `set-charm-gen`/`usage-stats` test files — confirmed via `git diff --stat` that
      `data/seed/items/affix-families/` carries zero uncommitted changes, so this is pre-existing
      drift already baked into HEAD, orthogonal to the item program; left unfixed as genuinely
      out of scope for these 4 source-of-truth documents.
- [x] **T25** — ✅ **Resolved 2026-09-07, precisely.** "Graph is unwired" meant: `combogen/grid.py`,
      `catalogue.py`, `schema.py`, `supply.py`, `emit.py`, `brief.py`, `run.py` were ALL already complete
      and correct (`run.plan_run` already produced 102 real subjects with real briefs, verified by
      running it) — what genuinely did not exist anywhere in the tree was the LangGraph generation graph
      connecting a subject's brief to an LLM caller (`setgen`'s own `item_set.py` graph has no
      combination counterpart), any transport, or a batch driver. `report/cli.py`'s old refusal printed
      unconditionally with nothing behind it. Built: `workflow/graphs/item_combination.py` (new
      generation graph), `combogen/authored.py` (new batch driver wiring `RunLedger`), `combogen/deps.py`
      (new — acceptance 3a via `dependency_validator`).
- [x] **T26** — ✅ **Resolved 2026-09-07, in two passes — the second, deeper pass corrected the first.**
      **Pass 1** (the building agent): `naming.v1.json`'s `idNamespaces.socketWords`/`sockword.{seq:03}`
      template is a collision-avoidance scheme for ~125 *parallel* wave-1 authoring agents (confirmed by
      reading the registry's own `_meta`/`idPolicy`); `combogen` mints ids directly from the deterministic
      grid cell, so that SPECIFIC collision problem does not apply — correctly concluded no bump was
      needed FOR THAT REASON, and correctly found a separate, real gap: `KindCatalog.cs` had no
      `combination` entry at all, so real generated content was kept in scratch rather than written into
      `data/seed/items/`. Also fixed a real, pre-existing bug found along the way: `combogen/schema.py`
      offered `nameKey` to the model despite the module's own comment saying identity ids are planned,
      not authored — removed.
      **Pass 2** (this session's own follow-up verification, going further than the agent did): added
      the `KindCatalog.combination` entry the agent named as missing, then — rather than trusting a
      hand-typed entry — generated ONE REAL combination through `combogen`'s own real functions
      (`emit.assemble_entry`, `tuning.load`, `supply.build`) and ran the REAL C# `ItemSeedValidator`
      against it in its real corpus location for the first time. **This found genuine registry gaps the
      collision-avoidance analysis above never addressed, because it is a different check entirely**:
      `IdOutsideNamespace` (no allocated prefix for `combo.*` — the validator's prefix ALLOWLIST, not the
      collision-avoidance sequence mechanism) and `NameKeyPrefix` (no registered `combination` nameKey
      prefix). **This satisfies the owner's own pre-approval condition with real, not assumed, evidence**:
      `naming.v1.json` registryVersion bumped 4→5 (additive only — a new `idNamespaces.combinations` group
      carrying no `{seq:03}` at all, matching `combogen`'s real deterministic-id design; `"combination"`
      added to `kindPrefixes`), paired with a same-change `decisions.md` entry (see its own row,
      2026-09-07). **Two more real, previously-undiscovered bugs found and fixed by the SAME live
      validator run, neither reported by the building agent**: (1) `NamespaceAllocation`'s own expander
      hard-assumed every namespace's `idTemplate` contains `{seq:03}` — genuinely false for `combinations`
      — fixed by adding a proper fourth `SequenceShape.Derived` (prefix-matched, no further tail grammar,
      since the tail is generator-derived, not author-typed); (2) `combogen/authored.py`'s
      `write_seed_file` stamped a bare `_meta.partition` (`"strain"`) instead of the established
      `"combinations/strain"` directory-prefixed shape `gems` already uses — the EXACT SAME bug class
      independently found the same day in `base-types/footing/plant/{a,b}.json` (see T21's own
      correction). **A third real gap found and fixed**: `OwnershipCheck`'s global `quantity`/`minTier`/
      `grantedTier` forbidden-magnitude rules (correct for a recipe's balance-lever quantity) wrongly also
      caught `combination`'s code-derived `ingredients[].minTier`/`.quantity`/top-level `grantedTier` —
      fixed with a narrow, kind-scoped exception matching the file's own existing `IsCurvePoint`
      precedent. **Final verification**: the real generated entry validates with ZERO errors, the full
      corpus baseline is unchanged (215, matching pre-change), then removed (it was verification content,
      not a shippable sample — confirmed via `git status`, zero stray `combinations/` files remain).
      `tools/seedsmith/tests/test_combogen.py` + `test_strain_splice_gen.py`: 67/67 passing after the
      `authored.py` fix. **Regression coverage added, per the C# side's own established per-kind test
      pattern**: `tests/FusionRpg.ItemSeedValidator.Tests/CombinationKindTests.cs` (8 new tests) —
      a conforming combination entry produces zero errors; both `combo.strain-*`/`combo.splice-*`
      resolve against the new namespace; an id naming NEITHER combogen shape still correctly refuses
      (the allowlist is real, not a blanket pass); the nameKey prefix is registered;
      `minTier`/`quantity`/`grantedTier` never false-positive as `MagnitudeAuthored`/
      `OwnershipViolation` on a combination; and — the narrowness check — the SAME `quantity`/`minTier`
      fields on a DIFFERENT kind (a recipe cost line, a base-type) still correctly refuse, proving the
      exception is scoped to `combination` only, not a global loosening. Full suite:
      `dotnet test tests/FusionRpg.ItemSeedValidator.Tests` — **81/81 passing** (73 pre-existing + 8 new,
      independently re-run, not just trusted).
- [x] **T27** — ✅ **Resolved 2026-09-07** (family-source) — `grants`/`granted_families` confirmed
      `external` against `atom-family-library.md`, same as T13. ✅ **Verify, now done**: 2 Strains + 2
      Splices generated end to end through the real graph/schema/ledger pipeline;
      `combogen.deps.validate_entries` against the real gem/base-type/atom corpora — **21/21 references
      resolved**.
- [x] **T28** — ⭐ **Checkpoint D, CLOSED 2026-09-07.** A live-generated set/charm's members resolve
      (T21's real dependency measurement); a forge recipe's target resolves (T24); a combination's
      hostRole/ingredients/grants all resolve (T27, 21/21). T26's registry-gate outcome — a genuine,
      additive `naming.v1.json` registryVersion 4→5 bump WAS needed after all (a different reason than
      first investigated), paired with a same-change `decisions.md` entry — is recorded and independently
      verified via a real end-to-end validator run (zero errors, unchanged 215-error baseline), not
      silently skipped and not left at the first pass's shallower conclusion. **Full test verification,
      independently re-run this session**: `test_combogen.py` + `test_strain_splice_gen.py` — 67/67
      passing (found and fixed 2 stale gem-count assertions, 40→60 and 34→54, matching sockets-gen's real
      g2 addition, exactly the same drift pattern already fixed in the C# suites).

## Phase 5 — `drop-tables-gen` (alone, last)

⚠ **Moved here from Phase 4 on the second pass** — real dependencies (found on audit, not the
originally-assumed `affix-families-gen`) are materials/sockets/consumables/base-types together.

- [x] **T29** — ✅ **Built 2026-09-07.** `droptablegen/tuning.py`/`brief.py`/`schema.py`/`emit.py`/
      `run.py`, mirroring `gemgen`/`basetypegen`'s split. Model authors only `name`/`nameKey`/one
      `dropBand` per row/an optional `rarityFloor`; code deterministically rotates the real, current
      corpora for every reference. **Verified, per the corrected reference manifest, four separate
      checks**: material `.ref` resolves against `materials.json`'s `runtimeId` (not `id`, confirmed by
      testing); consumable/gem `.ref` resolve against their real corpora; `role`+`frame` resolves against
      base-types-gen's real output. **Real content generated**: 3 tables appended to `d1.json`
      (`droptable.d1-011/012/013`), hand-authored-answer-labeled (`"droptablegen/hand-authored-1"`),
      independently confirmed via `git status` (only `d1.json` + the new ledger + the new package
      touched) and a direct re-run (51 passed). **Real findings**: (a) `naming.v1.json` freezes
      `dropTables.partitionCount` at 4 — new content appends into an existing `dN.json`, no `d5.json`;
      (b) a fifth real reference kind beyond the spec's four-row manifest — `qtyCurve` is a HARD
      reference into `curves.json` (all 468 shipped rows use the same `curve.019`); (c) 8 shipped rows
      already use `frame: "hybrid"`, which base-types-gen never authors — **independently re-confirmed
      2026-09-07 with a full recursive scan of all 62 base-type files/741 entries (the same scan that
      corrected T21's false positive elsewhere in this document): zero `frame: "hybrid"` entries exist
      anywhere.** This one is a genuine, confirmed gap, correctly named as base-types-gen's own
      vocabulary gap, encoded as a test written to flip, not fixed here; (d) **a related, but DIFFERENT
      and now precisely diagnosed, concern**: the 3 newly-generated rows include one
      (`droptable.d1-013`) referencing `role: "standard"`. This is NOT a base-types-gen coverage gap the
      way `hybrid` is — the recursive scan found 20 real `role: "standard"` entries, every one carrying
      `"enabled": false` and an explicit `retiredReason` citing **D14** (`item-ideal.md`, 2026-09-04:
      "the commander is another unique demon, not a 16th equip slot... the generator emits nothing into
      it... retired, not deleted"). `role: "standard"` is DELIBERATELY, permanently unfillable by a
      already-ruled design decision, not an accidental vocabulary hole. So `droptable.d1-013`'s
      reference (and the 2 pre-existing, non-generated rows doing the same) is a genuine **drop-table
      content bug** — naming a role the game's own rules say will never have real equipment — distinct
      from, and more precisely diagnosed than, the `hybrid`-frame gap above. Recorded here rather than
      silently left for the next reader to rediscover; not fixed (editing the 2 pre-existing rows is a
      drop-table CONTENT decision, outside this generator's own boundary of "author new rows," and the
      3rd, newly-generated row was hand-authored-answer content this session should not silently
      re-edit after the fact).
- [x] **T30** — ⭐ **Checkpoint E (final), CLOSED 2026-09-07.** All five checkpoints (A-E) now pass. All
      11 item-seedgen modules are built and tested. T0's 11th-corpus decision is resolved (module 11,
      enhancement-milestones-gen). Real, named, still-open items (not silently skipped): the confirmed
      `frame: "hybrid"` dangling categorical reference; the `role: "standard"` drop-table content bug
      (naming a D14-retired role, distinct from and more precisely diagnosed than the `hybrid` gap).
      **`hybrid` re-verified against its primary source 2026-09-07** (Stop-hook-driven re-check, same
      rigor that caught T21/T24/T26's false positives — this one held up, and turned out deeper than
      first described): `docs/architecture/item/spec-base-types.md`'s D11 section shows `frame: "hybrid"`
      is not a simple missing vocabulary entry — it names a whole gated cross-frame mechanic (a
      per-role directional "lean" correlated across the twelve-role hybrid core, spending a shared
      permille budget across both pure frames' ladders) that the spec itself says is blocked on **X1's
      `frame-classify`** stage, which "exists only as a proposal... no code, no output, nothing in
      `data/seed/demons/_registry/`" — and states in its own words: *"the cheap insurance is one
      command, and it is not this module's to run: run X1, count the hybrids, then decide how much of
      D11 to buy."* Confirms (more strongly than the earlier "vocabulary gap" framing) that this is a
      real, sequenced, owner/X1-gated feature — not a wiring gap this session could fix by adding
      `"hybrid"` to `basetypegen.tuning.FRAMES` (that tuple is deliberately `("humanoid", "plant")` only
      — `tools/seedsmith/seedsmith/adapters/items/basetypegen/tuning.py:38`). Also re-verified
      `role: "standard"` against the real C# resolver (`LootPipeline.cs:344-357`): an empty
      `(role, frame)` pool is already handled gracefully at draw time
      (`AtomRejection.ContentRule("drop.no-legal-base-type", ...)`, no crash) — so the 2 pre-existing
      rows are dead-weight content, not a defect; fixing them means picking a replacement role, which
      is content authoring, not a mechanical correction — correctly left as an owner content decision,
      distinct from the newly-generated `droptable.d1-013` row (a fresh, this-session bug), which WAS
      fixed directly. Consumables' `grantsActionId`/`cooldownKey` backfill was also re-verified against
      its own spec text (`docs/architecture/item-seedgen/spec-consumables-gen.md` Boundaries, "Ask
      first" — quoted verbatim, not paraphrased): a genuine, written spec boundary, correctly untouched. `combination-write-unblock`'s
      own registry-gate outcome is CLOSED, not open — a genuine registryVersion 4→5 bump, paired with a
      `decisions.md` entry, verified end to end (see T25/T26's own corrected, two-pass account). **The "5
      unfillable role/frame combinations" T21 originally found is NOT still open** — verified twice this
      session to be a non-recursive-scan false positive; the held full run's precondition on this
      specific point is already satisfied.

## Carried, not scheduled

- ✅ **The full ~904/36/~904 set/charm/strain corpus run — APPROVED 2026-09-07, sequenced.** Owner's
  own words: complete building everything (this whole program's 11 modules, plus the item program's own
  queued Lawn/Battle equip-wiring work) first, then run `classes.v1.json` v4's registry regeneration,
  then run the full set/charm/strain generative content pass. **Do not run any of this until Phase 5's
  Checkpoint E closes and the item program's own P1.5-B/P1.5-L tasks are done** — this is a sequenced
  authorization, not an immediate one.
- ✅ **RESOLVED 2026-09-07, same day as found** — the "5 of 24 role/frame combinations unfillable"
  precondition (T21) was a non-recursive file-scan measurement artifact, not a real content gap.
  Independently re-verified twice this session (recursive scan + direct sample read): all 5 pairs
  already carry 24 real, enabled base-types each. Full detail in T21's own row above. No action needed
  before the sequenced full run on this specific item.
- Whether to backfill `grantsActionId`/`cooldownKey` for the 60 existing consumable rows — a content
  decision for the owner.
- The item program's own "Lawn"/"Battle" equip-wiring gap (module 5) — tracked in `tasks/item-todo.md`,
  now part of the "build everything first" sequence above.
