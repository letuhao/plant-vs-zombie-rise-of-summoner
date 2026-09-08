# demon-corpus-self-heal: implementation plan

## Context

The full demon-species classification run completed (833 species indexed). A new reusable audit
tool, `tools/DemonQualityReport`, scanned the whole corpus across four dimensions — classification
integrity, catalog diversity (closed-vocabulary entropy/coverage per attribute), generation
quality, and simulated-combat balance — and surfaced five real, verified findings:

1. **217 duplicate anchor entries** (26% of 833 species). Root cause traced, not assumed: when a
   species' family reassignment lands it in a new bucket file, `runner.py`'s write path never
   deletes the stale copy from its old file. Same bug class manually fixed twice for individual
   species (SnorkleZombie, ThreePeater) earlier — now confirmed at corpus scale.
2. **`attackTempo`: entropy 0.00, 1 of 5 possible values ever used.** Root cause read directly from
   `prompts.py`, not guessed: `kit-shape` is the one pipeline never wired into
   `option-permutation`/3-way voting, and its brief never lists the `attackTempo` vocabulary
   explicitly — structurally the least-verified judgment in the 8-pipeline stack.
3. **68 species (81‰) unresolved rarity, 29 (34‰) unresolved aptitude, 4 unresolved element, 11
   unresolved threatBand** — genuine 3-way vote splits, per `demons metrics`'s own signal.
4. **98 species refuse to generate** (SpeciesExpander correctly throws rather than shipping
   zero-stat ghosts) — mostly downstream of finding 3.
5. Balance findings (rarity's weak correlation with simulated combat power) — already covered by
   the earlier `seed-to-concrete` finding; not this plan's concern, no action item here.

This plan closes findings 1–4 via a four-phase self-heal: a deterministic code fix, a one-time
data cleanup, a new reusable seedsmith capability (single-pipeline rerun — redeploying one fixed
prompt doesn't currently mean anything cheaper than reclassifying everyone from scratch), and the
actual LLM-driven re-classification, verified by re-running the same audit tool afterward.

## Architecture decisions

- **Root causes get root-cause fixes.** The duplicate bug is fixed in `runner.py` itself, not
  papered over with another one-off cleanup script; the `attackTempo` weakness is fixed by wiring
  `kit-shape` into permutation/voting like every other pipeline, not by hand-picking a better
  default.
- **Single-pipeline rerun is new, reusable infrastructure**, not a throwaway script — the next
  field that needs a prompt fix redeploys through the same mechanism at the same low cost (~1
  call/species instead of ~16-30).
- **Phase C spends real model calls and runs in the background** (matching this session's own
  established pattern for `demons run start/resume` — a harness-tracked background process, driven
  to completion, never a bare `nohup`).
- **Phase D is evidence, not a claim** — before/after numbers from the same real tool, diffed.

## Task list

### Phase A — deterministic fix (no model calls)

- [ ] **A1: fix the stale-duplicate write bug** · **S** · 1-2 files
  - `runner.py`'s `_write_species_entry` (or its caller in `_run_loop`/`_finalize`): before writing
    the new family-bucket entry, remove any existing entry for this `speciesId` from every OTHER
    file in `existing_by_file`, and rewrite those files too if they changed.
  - Acceptance:
    - [ ] A real test: classify a species, force its family to change on a second (re)classify,
      assert the OLD file no longer contains the species after the second write.
    - [ ] Existing `test_run_runner.py` suite stays green.
  - Verify: `python -m pytest tests/test_run_runner.py -v`
  - Files: `tools/seedsmith/seedsmith/adapters/demons/run/runner.py`, its test file.

- [ ] **A2: one-time cleanup of the 217 existing duplicates** · **S** · 0 new files (a driver script, not committed)
  - Reuse `emit.py`'s real `write_family_file`/`build_index`/`render_index` (never hand-written
    JSON) — same discipline as the SnorkleZombie/ThreePeater cleanup, generalized to the whole
    corpus: for every duplicated species, keep the copy `_index.json` already resolves to, remove
    the others from their files, rewrite affected files, rebuild the index.
  - Acceptance:
    - [ ] `DemonQualityReport`'s duplicate count reads 0 afterward.
    - [ ] No species is lost — same total distinct species count before and after.
  - Verify: re-run `dotnet run --project tools/DemonQualityReport`, read Section 1.

### ✅ Checkpoint A
- [ ] `python -m pytest` (seedsmith) full suite green.
- [ ] `DemonQualityReport` Section 1 shows 0 duplicates, 0 on-disk-not-indexed (or a named, real
  reason for whatever remains).

### Phase B — single-pipeline rerun capability

- [ ] **B1: `run-control` gains a pipeline-scoped rerun mode** · **M** · 3-4 files
  - Extend `runner.py`: a rerun that names one pipeline id re-executes ONLY that pipeline's
    judgment for each selected (already-classified) species and MERGES the result into the
    existing anchor entry — every other field stays exactly as it was, never re-rolled.
    `orchestrator.run_one_species` already loops `PIPELINES`; this needs a variant (or a
    `pipelines: Sequence[str] | None` param) that runs a NAMED SUBSET instead of all eight.
  - The CLI's existing `--pipeline PIPELINE` selector flag currently only NAMES which species to
    select (`resolve_selector`'s `"pipeline"` kind returns `all_ids` — a selection surface, not an
    execution scope yet, per `orchestrator.py`'s own comment: "the selector's job is just to name
    which species, which is all"). This task closes that gap for real — verified against the code,
    not assumed fixed already.
  - Acceptance:
    - [ ] `demons run rerun --pipeline kit-shape --all` re-executes ONLY kit-shape for every
      classified species, in one call per species (not eight).
    - [ ] Every OTHER field on the merged entry is byte-identical to before the rerun — proven by
      test, not inspection.
    - [ ] `_provenance.attempts`/`promptVersions` update only for the reran pipeline.
  - Verify: new tests in `test_run_runner.py`/`test_run_orchestrator.py`, full suite green.
  - Files: `runner.py`, `orchestrator.py`, `report/cli.py` (wire the execution scope), tests.

### ✅ Checkpoint B
- [ ] Full seedsmith suite green.
- [ ] A real, small (2-3 species) `--pipeline kit-shape` rerun proven live, merged fields checked
  by hand against the pre-rerun entry.

### Phase C — the actual self-heal (real model calls)

- [ ] **C1: strengthen the `kit-shape` prompt** · **S** · 1 file
  - List the `attackTempo` vocabulary explicitly in the brief (matching every other pipeline's own
    "Choose one: {order}" convention); add differentiating guidance between adjacent tempo values.
    Wire `kit-shape` into `_PERMUTABLE_FIELD`/vote — pick which of its four attributes should be
    voted (matching `VOTED_FIELDS`'s own "five load-bearing fields" scope call — a real design
    choice to make explicitly, not silently skip).
  - Acceptance:
    - [ ] A small real-model smoke batch (10-15 fresh/rerun species) shows attackTempo using more
      than 1 value — proven live, not assumed from the prompt text alone.
  - Files: `prompts.py`, `orchestrator.py` (if voting wiring changes), `vote.py` (VOTED_FIELDS if
    kit-shape's own field joins it).

- [ ] **C2: redeploy kit-shape corpus-wide** · owner-adjacent, real model cost
  - `demons run rerun --pipeline kit-shape --all` (Phase B's mechanism) — ~833 calls.
  - Acceptance: `DemonQualityReport`'s attackTempo entropy rises meaningfully above 0.00.

- [ ] **C3: self-heal every currently-unresolved field** · real model cost
  - `demons run rerun --unresolved` (selector already exists, no new code) — full 8-pipeline
    reclassification for every species with at least one unresolved voted field (~80-110 species
    estimated from the 68/29/4/11 overlap, real count found by running it).
  - Acceptance: unresolved rates in `DemonQualityReport` Section 1 drop; species that resolve
    become generatable (Section 3 failure count drops correspondingly).

### ✅ Checkpoint C
- [ ] Both reruns completed (state=completed, not failed) — `demons run status` checked, not
  assumed from "the command returned."

### Phase D — verification

- [ ] **D1: before/after report** · **S**
  - Re-run `DemonQualityReport --json <path>` after Phases A-C; diff against the pre-heal snapshot
    captured at the start of this plan's execution. Report real deltas: duplicate count,
    attackTempo entropy, unresolved rates per field, generation success rate.
  - Files: none committed — a report to the owner, matching how Phase D's own acceptance line
    reads ("evidence, not a claim").

### ✅ Checkpoint D — plan closes
- [ ] Every number in the audit's five findings has a stated before/after, or a named reason it
  wasn't fully closed (e.g. a genuine 1-1-1 split that stayed unresolved after a real retry).

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Phase B's merge logic silently drops or overwrites a field it shouldn't touch | High (data loss on a real, expensive corpus) | Byte-identical-except-the-reran-pipeline is a hard test assertion, not a spot check |
| Phase C's real model calls hang or the process dies mid-run | Low (checkpointed per species already) | Same background-run + `demons run status`/`resume` discipline used for the original full run |
| `kit-shape`'s voting choice (which of its 4 fields joins `VOTED_FIELDS`) is a real design call | Medium | Named explicitly in C1's own acceptance line rather than picked silently; default to `attackTempo` only (the field with the finding) unless the smoke batch shows `reach`/`targetPreference` also need it |

## Verification commands

```powershell
cd tools\seedsmith
python -m pytest -q
python -m seedsmith demons run rerun --pipeline kit-shape --all
python -m seedsmith demons run rerun --unresolved

cd ..\..
dotnet run --project tools/DemonQualityReport -- --json data/generated/demons-quality-after.json
```
