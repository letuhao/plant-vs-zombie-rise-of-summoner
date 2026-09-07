# Seedsmith content-completeness — `dungeon`

**Status:** Built 2026-09-08. Module 5 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Depends on
[spec-content-completeness-core.md](spec-content-completeness-core.md) (Phase 0, done). Idea phase:
[seedsmith-content-standard-ideal.md](../seedsmith-content-standard-ideal.md).

Adopts `content-completeness-core`'s registry and bidirectional language check for the dungeon
domain, and fixes the one real, already-shipped defect the whole program was partly named after:
`data/seed/dungeon/events/event.bargain-demon.allpeater-001.json`'s `flavor` field carried
untranslated Chinese fragments mid-English sentence ("offensive火力", "permanent 分配").

---

## 1. Objective

Answer, for the dungeon domain specifically, the same three questions `core`'s spec poses
generically — and, unlike items/demons/actions/passive-tree, dungeon needed a **second**, deeper
fix before the first question was even askable: the shared `Corpus` loader cannot see dungeon's own
real content at all (§2). This spec is "wire up code that already exists and was never connected,
across two separate layers," not new design — every piece below is either core's own
already-decided shape (Task 4b's fixed `language_consistency`) or dungeon's own already-written,
never-called code (`provenance.py`, `emit.py`).

## 2. Why `_provenance` never reached committed dungeon content — the real, traced root cause

The ideal doc's own finding names `DungeonProvenance`/`stale_ids` (`provenance.py:12,50`) as real,
well-designed, dead code (zero production callers, only `tests/test_dungeon_idempotency.py`
imports it). Tracing the real writer confirms this precisely, and finds it is one layer shallower
than "the plumbing exists but nobody turned the tap":

1. **The real writer never accepted provenance as an input at all.** `emit.py`'s own `write_entry`/
   `write_corpus` (before this module's fix, §4) took an already-built entry `Mapping` and
   serialized it verbatim — there was no parameter, no call, no import of `DungeonProvenance` or
   `stale_ids` anywhere in that file. Not "wired to the wrong thing" — wired to nothing.
2. **The generation layer never attaches provenance to what it produces, either.**
   `pipelines.py`'s `run_event_draws` (and every sibling `run_quest_draws`/`run_room_draws`/
   `run_encounter_draws`/`run_domain_draws`) returns a raw model-parsed `dict` straight from
   `_call_and_parse` (`pipelines.py:330-341` for events specifically) — no `_provenance` key, no
   staleness-key computation, nothing. This is upstream of `emit.py`'s own gap: even a fully wired
   `write_entry` would have received nothing to stamp.
3. **There is no committed orchestrator connecting the two at all.** Grepping `report/cli.py`'s own
   `add_parser` calls (the file's own subcommand registry) finds `demons`, `items`, `effects`,
   `structures`, `trees`, `numerics` — never `dungeon`. `adapters/dungeon/__init__.py`'s own
   `DungeonAdapter` is registered in `adapters/registry.py` and used by the generic `check`/
   `contract --audit` commands, but nothing calls `pipelines.run_event_draws(...)` and then
   `emit.write_corpus(...)` on its output anywhere in this repository. `tests/test_dungeon_
   idempotency.py`'s own `OfflineGuaranteeTests` docstring states (now stale, confirmed by reading
   `pipelines.py` directly — noted, not fixed, out of this task's scope) *"pipelines.py, the one
   module that would [call a model], is not built"* — true when written, false today: `pipelines.py`
   is fully built and calls `call_model` in five real functions, but no CLI subcommand or script
   anywhere calls those functions and commits the result. **The committed content under
   `data/seed/dungeon/events/*.json` was therefore produced by some means outside this adapter's own
   committed code path** — a manual or one-off process this repo has no record of, not a bug in
   `emit.py`/`provenance.py` themselves.

**What this spec does about it, and what it deliberately does not:** inventing the missing
orchestrator (a real "dungeon generate"/"dungeon commit" CLI subcommand tying `pipelines.py` to
`emit.py`) is a genuinely separate, larger piece of work than this task's own scope
(`seedsmith-content-standard-todo.md` Task 12: "wire `DungeonProvenance`/staleness stamping into
the real writer... register a `CompletenessSpec`... fix the real defect"). This spec fixes exactly
what it names: `emit.py` (§4) now CAN stamp `_provenance` the moment any future caller — an
orchestrator that does not exist yet, or a hand-run repair script like the one that fixed the live
defect (§6) — gives it one, and the completeness check (§3) is wired so a person can see the real
gap TODAY by running `check` against the real corpus, rather than waiting for that future
orchestrator to exist.

## 3. The second, deeper wiring gap: the shared `Corpus` loader cannot see dungeon's content shape

Found while grounding this spec, not assumed: `corpus/model.py`'s `Corpus.load()` only recognizes a
file whose top-level JSON has both a non-empty `kind` string and an `entries` LIST
(`corpus/model.py:183-186`, `if not kind or not isinstance(raw_entries, list): continue`).
Dungeon's own `emit.py` docstring states the deliberate, opposite convention: *"One object per
file... unlike the demons anchor's per-family list."* Every real file under
`data/seed/dungeon/<dir>/*.json` (bar each directory's own `_index.json`) is one bare entry object
with no `kind`/`entries` wrapper at all.

**Consequence, proven by test** (`tests/test_dungeon_completeness.py::LoadDungeonCorpusTests::
test_generic_corpus_load_sees_nothing_real_under_dungeon_events`): `Corpus.load(data/seed/dungeon/
events)` returns zero entries. Registering a `CompletenessSpec` for `domain="dungeon"` alone, per
`core`'s own registry shape, would be reachable and completely inert against the real corpus via the
standard `check <root> --adapter dungeon` path — `ContentFieldMissing`/`ContentLanguageContamination`
both read `ctx.corpus.by_kind(kind)`, and that would always be `[]` for `"dungeon-event"`.

**The fix (§4): a dungeon-local corpus builder, not a change to the shared loader.**
`adapters/dungeon/completeness.py`'s `load_dungeon_corpus(root)` reuses `corpus.model.Corpus`/
`Entry` UNCHANGED — no edit to `corpus/model.py` — and walks the one-object-per-file directories per
`kinds.KINDS`'s own `directory -> kind` map, adding each as a real `Entry`. This matches `core`'s own
boundary ("never invent a domain-specific... definition inside `core` itself" — read here as
covering the shared corpus loader too, not only the metric registry): the fix lives in the dungeon
adapter, where dungeon's own file-shape knowledge already lives (`kinds.py`, `emit.py`), not in a
shared module every other domain would have to reason about.

`report/cli.py`'s `cmd_check` is extended with one branch: when `args.adapter == "dungeon"`, it
calls `load_dungeon_corpus` instead of `Corpus.load`, and `ensure_completeness_registered()` before
building `Ctx` — so `python -m seedsmith.report.cli check data/seed/dungeon --adapter dungeon`
actually sees dungeon's real content for the first time.

## 4. What gets registered, and for which field

`CompletenessSpec(domain="dungeon", kinds=frozenset({"dungeon-event"}), field="flavor")` —
matching this task's own stated scope exactly (`todo.md` Task 12: "register a `CompletenessSpec`
for dungeon events"). `kinds.py`'s `EVENT.required` names `flavor` a required field for every event,
the same shape items' own `FlavourMissing` already established for its six kinds.

**Deliberately not registered here, named so the gap is visible rather than silently unclaimed**:
`dungeon-room`, `dungeon-domain`, and `dungeon-quest` all carry the identical `flavor` field in
their own `required` sets (`kinds.py:14,25,54`) and would need only one more `CompletenessSpec`
line each, reusing everything this module already built (`load_dungeon_corpus` already loads all
seven kinds, not just events). Left out of this task on purpose — the acceptance criteria and the
real, already-found live defect both name events specifically, and widening scope silently (adding
checks nobody asked to verify yet) is exactly the kind of untracked scope creep this program's own
discipline warns against. A follow-up task can add the other three kinds' specs as one line each.

`ContentLanguageContamination` needs no separate registration — per `content_completeness.py`'s own
design (Task 4b), it runs the fixed, bidirectional `language_consistency` against every registered
spec's own `field` automatically. Registering the `dungeon-event`/`flavor` spec above is what makes
`ContentLanguageContamination` reachable for dungeon events; this spec does **not** re-derive or
re-import `language_consistency` separately anywhere in the dungeon adapter.

## 5. `emit.py`'s own fix: provenance becomes attachable, not automatic

`write_entry(directory, entry_id, entry, *, provenance=None)` and `write_corpus(directory,
entries_by_id, *, provenance_by_id=None)` — both parameters optional and additive (every existing
call with no provenance argument is byte-for-byte unaffected, proven by
`ProvenanceStampingTests::test_write_entry_with_no_provenance_is_unaffected` and the pre-existing
`RerunIsByteIdenticalTests`, unmodified, still green).

`build_provenance(*, brief_hash, prompt_version, schema_version, model_id)` composes the
`_provenance` dict a caller passes in. **The staleness KEY itself reuses `content-completeness-
core`'s own shared `pipeline.staleness.staleness_key`** (Task 2) — not `provenance.py`'s own
dungeon-local `staleness_key`/`stale_ids`, which stays as-is (still real, still tested, still
usable for its own richer audit fields — `attempts`, `confidence`, `minorityValues` — that core's
four-input key does not carry). This is a deliberate choice, not an oversight: `core`'s own
boundary states every domain reports staleness through the ONE shared key shape
(`Content/FieldStale` reads `_stalenessCurrentKey`/`is_stale` from `pipeline/staleness.py`
directly) — a dungeon entry stamped via `build_provenance` is immediately readable by that shared
metric with no per-domain adapter glue, which a dungeon-only key would not be.

## 6. The live defect: root cause, fix, and what "regenerated through the pipeline" means here

**Root cause of the defect itself** (distinct from the wiring gap above, which is why it was never
CAUGHT, not why it happened): `event.bargain-demon.allpeater-001.json` was generated before Task
4b's bidirectional `language_consistency` fix existed, through whatever process actually produced
it (§2.3 — unrecorded). The original, one-directional `language_consistency` (CJK-motif-in only)
could not have caught this even if dungeon had been wired to it at the time, since this event's
motifs are English and the contamination ran the opposite direction.

**The fix applied**: since no committed CLI orchestrator exists to re-run this event through
`pipelines.run_event_draws`'s own full draw (which would also re-roll `name`/`outcomes`/`reason` —
more than this defect touches, and riskier than necessary for a single-field repair), the fix used
a real, targeted call against the SAME live local model endpoint (`http://localhost:1234/v1/chat/
completions`, confirmed reachable — `curl` returned HTTP 200 and a real model list including
`google/gemma-4-26b-a4b-qat`, the `LlmCallerConfig` default) via `pipeline.llm_caller.call_model`,
with a system/user prompt asking only for a corrected `flavor` string, verified against
`language_consistency` before being written. **This is a real model call, not a fabricated one** —
distinct from a full `run_event_draws` regeneration, which was not attempted because reconstructing
the exact planner `Cell`/motif-slot state for one already-placed event is out of proportion to
fixing one field, and would have put `name`/`outcomes` at risk of drifting for no acceptance-
relevant reason. `_provenance` on the fixed entry names `promptVersion: "dungeon-event-flavor-
repair/1"` — an honestly distinct version string for this one-off repair path, not a claim that it
reused the real `EVENT_SYSTEM_PROMPT`'s own (nonexistent — dungeon has no `PROMPT_VERSION` constant
anywhere, another real, named gap) versioning.

## 7. Testing strategy

`tools/seedsmith/tests/test_dungeon_completeness.py` (new) proves, against the REAL committed
corpus: `Corpus.load()` sees nothing (the negative case, confirming §3's finding), `load_dungeon_
corpus` sees all real events including the defect entry, `ensure_completeness_registered` is
idempotent, `ContentFieldMissing` runs clean against the real event corpus, and — the load-bearing
regression proof — `ContentLanguageContamination` reports zero findings for
`event.bargain-demon.allpeater-001` and its `flavor` field is pure ASCII, both AFTER the fix. These
two tests were run and confirmed FAILING against the real, still-defective file before the fix was
applied (captured as this task's own before/after evidence, not asserted from memory).

`tools/seedsmith/tests/test_dungeon_idempotency.py` gained `ProvenanceStampingTests` (new class):
no-provenance calls unaffected, a provided provenance is stamped, `build_provenance` reuses core's
own `staleness_key` byte-for-byte, a changed `prompt_version` changes the key, and `write_corpus`
stamps only the ids it was given a provenance for.

```powershell
python -m pytest tools/seedsmith/tests/test_dungeon_completeness.py tools/seedsmith/tests/test_dungeon_idempotency.py -q
```

## 8. Project structure

```
tools/seedsmith/seedsmith/adapters/dungeon/completeness.py  (new) — §3, §4
tools/seedsmith/seedsmith/adapters/dungeon/emit.py           (edited) — §5
tools/seedsmith/seedsmith/report/cli.py                      (edited) — §3, cmd_check's dungeon branch
tools/seedsmith/tests/test_dungeon_completeness.py           (new)
tools/seedsmith/tests/test_dungeon_idempotency.py            (edited) — ProvenanceStampingTests
data/seed/dungeon/events/event.bargain-demon.allpeater-001.json (edited) — §6, the one sanctioned content fix
```

## 9. Boundaries

- **Always:** keep every dungeon-registered metric `gates=False` (inherited from `core`, never
  overridden per-domain without a separate, later decision); reuse `content_completeness.py`'s
  registry and `pipeline/staleness.py`'s key shape rather than re-deriving either; verify the live
  defect fix by reading the real file, never by inference.
- **Ask first:** widening the registered `CompletenessSpec` beyond `dungeon-event` to
  `dungeon-room`/`dungeon-domain`/`dungeon-quest` (named in §4 as a real, cheap, deliberately
  deferred follow-up, not a silent scope expansion); building the missing dungeon-generation
  orchestrator (§2.3) — a separate, larger piece of work this spec does not authorize.
- **Never:** modify `corpus/model.py`'s shared `Corpus.load()` to special-case dungeon's file shape
  — the dungeon-local loader (§3) is the fix, not a shared-module carve-out; make `emit.py`'s new
  provenance parameters non-optional (every existing call, including `test_dungeon_idempotency.py`'s
  own pre-existing rerun test, must stay unaffected).

## Open questions

None outstanding for this module's own scope. Two real, named-but-not-fixed gaps carry forward,
both explicitly out of this task's scope per §2/§6 above: (1) no dungeon-generation orchestrator
exists to tie `pipelines.py` to `emit.py` for any FUTURE event, so a fresh, non-repair generation
pass still has nothing to call `write_corpus` on `_provenance`-stamped; (2) dungeon has no
`PROMPT_VERSION` constant of its own the way passive-tree/demons do, so a real staleness key for
dungeon's own MAIN generation prompt (as opposed to this task's one-off repair prompt) cannot be
computed today.
