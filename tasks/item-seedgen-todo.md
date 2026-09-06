# Task list: item-seedgen

Source: [item-seedgen-plan.md](item-seedgen-plan.md). 27 tasks, 5 phases, 5 checkpoints. Every module's
own spec (`docs/architecture/item-seedgen/spec-<module-id>.md`) is authoritative for acceptance detail
beyond what's summarized here — read the module's spec before starting its tasks.

## Phase 0 — `generator-harness` (blocks everything)

- [ ] **T1** — Build `RunLedger` (`tools/seedsmith/seedsmith/pipeline/run_ledger.py`): atomic
      temp-file-then-replace writes, `plan_run(subjects, is_valid) -> subjects_needing_work`, reusing
      the demon harness's existing atomic-lock discipline rather than reimplementing file locking.
      **Verify:** a kill-mid-run simulation leaves the ledger parseable and consistent.
- [ ] **T2** — Reconcile-detects-corruption: `plan_run` re-queues a subject whose ledger row says "done"
      but whose on-disk entry fails `is_valid`. **Verify:** a test corpus with one corrupted, "done"-
      marked entry is re-queued by `plan_run`.
- [ ] **T3** — Standardize the CLI flag contract every module below uses: `--write` (default,
      append+reconcile), `--overwrite <id[,id...]>`, `--overwrite all` (requires the literal `all`),
      `--dry-run`. **Verify:** `--overwrite` with no value refuses rather than defaulting to `all`.
- [ ] **T4** — ⭐ **Checkpoint A.** Run `test_run_ledger.py` in full: resume, reconcile, overwrite-by-id,
      overwrite-all-requires-literal, dry-run-writes-nothing. All green before Phase 1 starts.

## Phase 1 — `base-types-gen` · `materials-gen` · `set-charm-live-endpoint` (parallel)

- [ ] **T5** — `base-types-gen`: brief/schema (`basetypegen/brief.py`, `schema.py`) — model picks
      identity fields only; a bare numeric field in the schema fails construction (mirror `setgen`'s
      `Pipeline.__post_init__` guard).
- [ ] **T6** — `base-types-gen`: numeric resolution + emit, wired to `generator-harness`. **Verify:** a
      generated entry imports cleanly through `AtomImporter`/`ItemSeedValidator`, zero new refusals.
- [ ] **T7** — `materials-gen`: brief/schema/vocab — refuses to author content for an id outside the
      closed `MaterialCatalog.cs` vocabulary. **Verify:** the refusal test from the spec passes; a
      generated entry for a REAL id imports cleanly.
- [ ] **T8** — `set-charm-live-endpoint`: add `--endpoint`/`--model` to `items generate`, wired to
      `pipeline.llm_caller.call_model`, matching `effects generate`'s existing pattern. **Verify:**
      `--write --endpoint <url>` (no `--answers`) no longer refuses; `--write` with neither still does.
- [ ] **T9** — `set-charm-live-endpoint`: one real, small, explicitly-authorized live generation (a
      handful of sets/charms, not the held full run) — output shape matches what the replay-transport
      samples already proved importable.
- [ ] **T10** — ⭐ **Checkpoint B.** One real base-type, one real material, one real live-generated
      set/charm, each independently confirmed importable through the real production consumer.

## Phase 2 — `affix-families-gen` · `sockets-gen` (parallel)

- [ ] **T11** — `affix-families-gen`: `opvocab.py` reads the REAL closed op vocabulary per atom kind
      (`stat.derived`'s four ops, `stat.modify`'s three) from its C# source rather than a hand-typed
      Python copy. **Verify:** a generated family's `op` is always legal for its declared kind.
- [ ] **T12** — `affix-families-gen`: brief/schema/emit, wired to `generator-harness`. **Verify:** a
      generated family passes `item_role_family` legality exactly as a hand-typed one would.
- [ ] **T13** — `sockets-gen`: brief/schema/emit, wired to `generator-harness`. **Verify:** running it
      against the allocated-but-empty `gems/2` partition produces real, valid, importable content —
      the specific gap the audit found, closed as this task's own evidence.
- [ ] **T14** — `sockets-gen`: confirm against `GemInsertCorpus.Load` (the real production consumer),
      not a synthetic parser.
- [ ] **T15** — ⭐ **Checkpoint C.** A real generated affix family passes real legality; `gems/2` has
      real content; both reconcile cleanly against `generator-harness`.
- [ ] **T16** — *(reserved — folded into T11-T15 if no separate work remains; do not skip silently, mark
      N/A with a one-line reason if so.)*

## Phase 3 — `recipes-gen` · `drop-tables-gen` (parallel)

- [ ] **T17** — `recipes-gen`: `opvocab.py` reads `ItemWorkbench`'s real, current operation vocabulary.
      **Verify:** a reconcile run against the real 30-entry corpus, using this real vocabulary, reports
      the same drift the 2026-09-05 hand-patch already fixed (proving reconcile would have caught it).
- [ ] **T18** — `recipes-gen`: brief/schema/emit, wired to `generator-harness`. **Verify:** a generated
      recipe's material references resolve against `materials-gen`'s real vocabulary.
- [ ] **T19** — `drop-tables-gen`: brief/schema/emit, matching the CURRENT symbolic (`dropBand`/
      `qtyCurve`) shape exactly — read the real `d1..d4.json` files before writing the schema.
- [ ] **T20** — `drop-tables-gen`: **Verify** a generated table's referenced ids resolve against real
      base-type/affix-family corpora, and its shape is byte-compatible with the (separate, unbuilt)
      band→row expander's own documented input contract.
- [ ] **T21** — ⭐ **Checkpoint D.** The recipe reconcile run (T17) shows zero drift against the current
      corpus; a generated drop-table entry imports cleanly.
- [ ] **T22** — *(reserved — same rule as T16.)*

## Phase 4 — `consumables-gen` · `combination-write-unblock` (parallel)

- [ ] **T23** — `consumables-gen`: brief/schema/emit, wired to `generator-harness`. **Verify:** a
      generated consumable's `family` reference resolves to a real atom family.
- [ ] **T24** — `consumables-gen`: reconcile run against the real 60-entry corpus reports the known
      `grantsActionId`/`cooldownKey` gap precisely (which rows lack them today) — a real, measured
      baseline, this task's own acceptance evidence.
- [ ] **T25** — `combination-write-unblock`: investigate and name PRECISELY what "graph is unwired"
      means in `combogen/grid.py`/`catalogue.py`/`run.py` — cite the specific missing connection.
- [ ] **T26** — ⛔ `combination-write-unblock`: resolve the frozen-registry blocker. **Ask-first gate**
      — if the only path bumps `naming.v1.json`'s `registryVersion`, write the `decisions.md` entry and
      get it approved before touching the registry; do not bump it unilaterally. If a path exists that
      doesn't require touching the frozen registry, take that path instead and note why it works.
- [ ] **T27** — ⭐ **Checkpoint E (final).** `items generate --kind combination --write` produces real,
      valid content for a representative sample of the 102 ids; `combogen/migrate.py`'s retirement of
      `sockwords.json` runs cleanly against it. Consumables reconcile (T24) has run and its gap report
      is recorded. All five module checkpoints (A-E) pass together with no silently-skipped task.

## Carried, not scheduled

- The full ~904/36/~904 set/charm/strain corpus run — a separate, already-held authorization (item
  program's own Checkpoint 0/3), unaffected by T8-T9 merely making a live run technically possible.
- Whether to backfill `grantsActionId`/`cooldownKey` for the 60 existing consumable rows T24's reconcile
  will name — a content decision for the owner, not this plan's to make unprompted.
- The item program's own "Lawn" equip-wiring gap (module 5) — tracked in `tasks/item-todo.md`, not here.
