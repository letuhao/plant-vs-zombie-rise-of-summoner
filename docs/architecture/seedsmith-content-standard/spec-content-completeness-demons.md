# Seedsmith content-completeness — `demons`

**Status:** Proposed 2026-09-08. Module 4 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Depends on
`content-completeness-core` (built, Checkpoint 0 closed 2026-09-08).

Demon species already write real `_provenance` (`data/seed/demons/species/plant/aerial-flora.
json:8-18`) — this module is narrower than actions' (which has nothing): the metric + backfill
wiring is new, a new generation stage is not this module's job.

## 1. Objective

Answer Task 9's own required question first, with real evidence, because the honest answer is not
what the task brief assumes going in: **which demon-species field(s) count as "description/flavor"
for this program's own purpose, distinct from the anchor classification fields
`metrics/demon_coverage.py`/`demon_roster.py` already check?**

**Finding: none exist yet.** Every key across all 904 real, committed entries under
`data/seed/demons/species/**/*.json` was enumerated directly (not sampled):

```
_derived, _provenance, acquisition, aptitudePrimary, aptitudeSecondary, attackTempo, basis,
deployMode, elementPrimary, elementSecondary, family, gameTypeId, posture, pure, rarity, reach,
reason, resourceProfile, side, speciesId, targetPreference, threatBand, traits, variants, verdict
```

No `name`, `flavor`, `description`, or `lore` field exists on any of the 904 entries. The nearest
candidate by shape — `reason`, the one free-text field — is explicitly **not** a description/flavor
field and must not be treated as one:

- It is the threat-classification model's own audit trail, not player-facing prose.
  `anchor/prompts.py:242-246`'s own prompt asks for it directly: *"Choose one: [threat rungs], with
  a one-sentence reason"* / *"Answer agree / too-low / too-high, with a one-sentence reason."* It
  answers "why did the model pick this threat rung," never "what is this creature to a player."
- Its real, committed values carry raw mechanical/stat vocabulary — the exact class of content this
  program's own live precedent (`generate_commander_effects.py`'s G1 motif-prose filter) already
  treats as disqualifying for player-facing text. Real examples, read directly from the committed
  corpus, not invented: `PotatoMine.reason` = *"A single explosion dealing 1800 damage with a radius
  of 0.74 blocks is a high-impact tactical threat..."*; `CherryBomb.reason` = *"A 3x3 area-of-effect
  explosion dealing 1800 damage is a high-tier offensive capability..."*. A player-facing flavor
  field is never allowed to cite a raw damage number — the same rule the commander-effect pipeline
  already enforces for the sibling `demon`/`commander-effect` kinds.

This distinguishes `reason` cleanly from the fields `demon_coverage.py`/`demon_roster.py` already
check (`elementPrimary`, `aptitudePrimary`, `rarity`, `threatBand`, `posture`, `family`,
`deployMode` — all closed-loop, machine-verifiable classification axes) by being neither: it is
free text, but it is not description/flavor either. Task 9's own acceptance is satisfied by naming
this absence precisely, not by mis-filing `reason` into the role to avoid an empty answer (the
`no-manufactured-uncertainty` standard this repo already holds work to: an honest gap costs a
sentence, a fabricated field costs a defect later).

**Decision:** this module registers `CompletenessSpec(domain="demons", kinds={"species"},
field="flavor")` — a genuinely new field, matching this program's own universal naming convention
(`Quality/FlavourMissing`'s items, dungeon's event `flavor`, passive-tree's node `flavor`), not
inherited from `reason`. Populating it is a **new LLM generation stage** (schema, brief, validators)
that this module does **not** build — out of scope per this module's own map-doc description ("this
module adds the missing-field metric and wires automatic backfill to it," not a generator) and per
the plan's own Phase 3 scope (only actions and `passive-tree-identity-content` build brand-new
generation in this program). `Content/FieldMissing` reporting "904 of 904 have no flavor" today is
therefore a correct, real finding — not a placeholder or a failure of this module.

## 2. A second real gap this module closes: `Corpus.load()` cannot see `species` at all

Confirmed by reading `corpus/model.py:183-186`: a file only becomes corpus content if its top-level
JSON is a dict with both a `kind` string and an `entries` list. Every real species-anchor file is a
**bare JSON array** (`anchor/emit.py`'s own `render_family_file`:
`json.dumps(sorted_entries, indent=2, sort_keys=True, ...)` — no wrapper at all), unlike the sibling
`demon`/`commander-effect` kind files under the same `data/seed/demons/` root, which already are
real `{kind, entries}` documents and load correctly through the generic path today (confirmed:
`data/seed/demons/demon/plant/common.json` and `data/seed/demons/commander-effect/all.json` both
carry `"kind": "..."` + `"entries": [...]`). So registering the Task 9 spec alone would be reachable
in the registry and silently inert — `ctx.corpus.by_kind("species")` would return `[]` even against
the real, full 904-entry corpus, which would make `Content/FieldMissing` wrongly report a clean
zero-findings pass instead of the real 904/904 gap.

**The fix (`adapters/demons/completeness.py`, mirroring the live precedent found while grounding
this spec — `adapters/dungeon/completeness.py`'s own `load_dungeon_corpus`, built for the identical
problem on a different domain, same session):** `load_species_corpus(root, into=corpus)` walks
`root/species/**/*.json` directly (skipping `_`-prefixed files, matching `run/runner.py`'s own
`_load_existing_anchors` convention) and adds each row as a real `Entry(kind="species", ...)` — into
the SAME `Corpus` the generic loader already built, since `demons` is a *mixed*-shape adapter
(`demon`/`commander-effect` already parse; only `species` needs the bespoke walk), unlike dungeon
(where every kind needed it). `report/cli.py`'s `cmd_check` gains one `elif args.adapter ==
"demons":` branch calling this, matching the existing `dungeon` branch's own shape exactly.

## 3. The backfill-wiring half: proving the shared engine generalizes to demons' own real resumability

Task 10's own acceptance requires "a resumed demon-species generation run backfills only genuinely
missing/stale entries." Demons' species-anchor generator (`run/runner.py`'s `start()`) already does
exactly this, live, in production, today — **confirmed by reading it directly**:

```python
existing_anchors = _load_existing_anchors(paths.anchors_dir)
already_done = {a["speciesId"] for a in existing_anchors} if not force_selector_ignores_existing else set()
ids = resolve_selector(...)
ids = [i for i in ids if i not in already_done]
```

This is a bespoke, hand-rolled version of exactly the resolved automatic-backfill contract (spec
§3): missing-only, automatic, no staleness check gating it (`force_selector_ignores_existing`
mirrors the manual `--force`/`rerun`/`overwrite-all` path — `runner.py`'s own `rerun`/
`overwrite_all` functions pass it `True`). It is real evidence this domain already independently
converged on the SAME contract `content-completeness-core` generalizes — not evidence the shared
engine has nothing to add.

**What this module builds, not a `runner.py` rewrite:** `runner.py` is a complex, live, production
module (a real mutual-exclusion lock, threaded classification, crash recovery, per-species
checkpointing) — rewriting its internals to literally call `pipeline.backfill.plan_missing` is
outside this narrow module's own risk budget and is not what Task 9/10 ask for (they ask for
*wiring*, not a refactor of a working system). Instead, `completeness.py`'s `missing_species_ids`
wraps `plan_missing` behind a `RunLedger`-shaped duck-typed view over the real anchor list
(`_AnchorLedgerView.read_done()` returns `{speciesId: entry}`, exactly the shape `plan_missing`
already expects), and this module's own test suite proves — against the real, full 904-species
corpus, not a synthetic fixture — that `missing_species_ids` returns the identical id set
`runner.py`'s own bespoke filter would. This is the generalization proof the plan's own Checkpoint
5b asks for ("a resumed run on each domain is a no-op on already-good content, proven, not
assumed"), without touching a module whose own spec (`spec-run-control.md`) this task was never
scoped to reopen.

`Content/FieldStale` (Task 3's registry) reports nothing for demons today — correct, not a bug: no
`flavor` field exists yet to have a staleness key, and the metric's own documented convention is
that a domain which never stamps `_stalenessCurrentKey` simply produces silence, not a false claim
of freshness (`metrics/content_completeness.py`'s own `ContentFieldStale` docstring). Species
anchors keep their OWN, richer, already-real staleness detector (`anchor/emit.py`'s `stale_ids`,
`dumpHash` + per-pipeline `promptVersions` — a different, finer-grained shape than the generic
4-part `brief_hash`/`prompt_version`/`schema_version`/`model_id` key `pipeline/staleness.py`
defines) for the classification fields it already governs; this module does not force species onto
the generic key, matching `content-completeness-core`'s own boundary against inventing a
domain-specific staleness shape inside `core` — the reverse direction (an existing, real,
finer-grained domain detector should not be replaced by the generic one either) is the same
principle read the other way.

## 4. Commands

```powershell
python -m seedsmith.report.cli check data/seed/demons --adapter demons
python -m pytest tools/seedsmith/tests/test_demons_completeness.py -q
python -m pytest tools/seedsmith/tests -q
```

## 5. Project structure

```
tools/seedsmith/seedsmith/adapters/demons/completeness.py   (new) — §2, §3
tools/seedsmith/seedsmith/report/cli.py                     (edited) — one elif branch, §2
tools/seedsmith/tests/test_demons_completeness.py            (new)
```

## 6. Code style

Match this program's own established style exactly (`adapters/dungeon/completeness.py` is the
closest sibling, read in full before writing this one): frozen dataclasses for value shapes,
module-level docstrings stating *why* (a real, cited gap), `from __future__ import annotations`,
real file:line citations for every generalization claim, an idempotent `ensure_*_registered()`
guard rather than relying on `register_completeness` to dedup (it does not, matching
`content_completeness.py`'s own current contract — dungeon's module already established this
per-domain guard idiom; this module reuses it rather than patching `core`).

## 7. Testing strategy

Every claim proven against the real, committed 904-species corpus, not a synthetic fixture:

1. `load_species_corpus` loads all 904 real entries with `kind="species"` from the real
   `data/seed/demons/species` tree.
2. `Content/FieldMissing`, run against that real corpus, reports exactly one finding: 904 of 904
   `species` entries have no `flavor` — the real, unfabricated gap this spec's §1 predicts.
3. `missing_species_ids`, given the real 904 anchors plus a held-out synthetic id standing in for
   "not yet generated," returns exactly that one id — proving the shared engine's automatic path is
   a real no-op against already-good content at full corpus scale, and correctly detects the one
   genuinely missing id when one exists.
4. `ensure_completeness_registered` is idempotent: calling it twice registers the spec once.

## 8. Boundaries

- **Always:** cite real file:line evidence for the "no flavor field exists" finding, not an
  assumption; keep `Content/FieldMissing`'s report against demons `gates=False`; add species
  entries into the corpus the generic loader already built rather than discarding it (demons is
  mixed-shape, not uniformly bare-array like dungeon).
- **Ask first:** building the actual species-flavor LLM generation stage (schema/brief/validators)
  — a real, separate, larger task this module's own scope does not include.
- **Never:** treat `reason` as a flavor/description field (§1); modify `run/runner.py`'s own live
  resumability logic to force it through `plan_missing` directly (§3 proves equivalence instead);
  modify `pipeline/backfill.py`, `pipeline/staleness.py`, or `metrics/content_completeness.py`
  (Core, already built and tested) for a demons-specific need.

## Open questions

None — Task 9's own required naming question is answered in §1 with a negative result (no field
exists yet), which is itself the finding, not a gap in this spec.
