"""Tests for the `items generate --write` wiring (item module 13's deferred generation graph).

    python -m pytest tools/seedsmith/tests/test_item_gen_wiring.py -q

⭐ **What this file proves and what it deliberately does not.** It proves the PATH: a planned
subject reaches the graph, an authored answer comes back through the injected transport, the
already-built distributors price it or refuse it with their own rule names, a refusal re-prompts,
an exhausted subject escalates without taking the batch with it, and a clean batch writes a seed
file somewhere it was told to. It does not judge the CONTENT — that is what the run report's
distinctness metrics are for, and they are measured over a real batch in the module's todo entry.

⭐ **`Module13DefectsFixedTests` at the bottom is the five defects this wiring MEASURED in module
13's own machinery on 2026-09-06, now asserted in their fixed state.** They were first written as
"this is the state today", with the numbers, so that a fix could not land silently — the fix landed
the same day and the assertions were inverted rather than deleted. The before-numbers stay in the
docstrings, because a fix whose evidence has been deleted cannot be re-checked.
"""
from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from seedsmith.adapters.items.charmgen import rules as charm_rules  # noqa: E402
from seedsmith.adapters.items.setgen import answers as answers_mod  # noqa: E402
from seedsmith.adapters.items.setgen import authored as authored_mod  # noqa: E402
from seedsmith.adapters.items.setgen import brief as brief_mod  # noqa: E402
from seedsmith.adapters.items.setgen import cells, distribute, emit  # noqa: E402
from seedsmith.adapters.items.setgen import schema as schema_mod  # noqa: E402
from seedsmith.adapters.items.setgen import seedfile as seedfile_mod  # noqa: E402
from seedsmith.adapters.items.setgen import tuning as tuning_mod  # noqa: E402
from seedsmith.adapters.items.setgen import vocab as vocab_mod  # noqa: E402
from seedsmith.adapters.items.setgen.run import RunPlan, Subject  # noqa: E402
from seedsmith.adapters.items.setgen.themes import Theme  # noqa: E402
from seedsmith.adapters.items.setgen.verdict import GATING_METRICS, Verdict  # noqa: E402
from seedsmith.workflow.graphs import item_set as graph_mod  # noqa: E402

REPO_ROOT = Path(__file__).resolve().parents[3]
TUNING = tuning_mod.load()
VOCAB = vocab_mod.build(TUNING)

SET_ROLES = ("armament-primary", "core-guard", "manipulator", "footing")


def _build_theme() -> Theme:
    return Theme(
        theme_key="build.might-offense", species_id=None, display_name="Might / Offense",
        motifs=("might", "force", "offense"), anti_motifs=("defense",),
        expression_item="stat emphasis and threshold shape", basis="derived", rarity=None,
        retired=False, population="build", aptitude="Might", archetype="offense")


def _species_theme() -> Theme:
    return Theme(
        theme_key="demon.testspecies", species_id="testspecies", display_name="Test Species",
        motifs=("iron", "patience"), anti_motifs=(), expression_item="material and form",
        basis="text", rarity=None, retired=False, population="species")


def _legal_capability() -> str:
    """A capability pick legal on `SET_ROLES` — resolved, never pinned, so a corpus edit that moves
    a family's roles produces a red test here rather than a mystery refusal in a real run."""
    for pick in VOCAB.capability_for_roles(SET_ROLES):
        if pick.variant is None:
            return pick.family
    raise AssertionError("no element-free capability is legal on the four core roles")


def _legal_set_stat() -> str:
    for pick in VOCAB.stat:
        if (pick.variant is None
                and pick.kind_id in TUNING.stat_kinds
                and pick.family not in distribute.MORE_OP_FAMILIES
                and pick.family not in distribute.MATCH_SCOPE_ONLY_FAMILIES):
            return pick.family
    raise AssertionError("no stat family survives the set-tier rules")


def _legal_charm_families() -> "list[str]":
    """Element-free families from the pool the brief actually prints. Resolved, never pinned."""
    seen: "list[str]" = []
    for pick in charm_rules.charm_pool(TUNING, VOCAB.all_picks):
        if pick.variant is None and pick.family not in seen:
            seen.append(pick.family)
    if len(seen) < 2:
        raise AssertionError("the charm pool has fewer than two element-free families")
    return seen


def _legal_charm_family() -> str:
    return _legal_charm_families()[0]


def _clean_set_answer() -> dict:
    return {
        "name": "Proof Set", "nameKey": "set.proof-set",
        "flavor": "A fixture, and it says so.",
        "capability": {"family": _legal_capability()},
        "members": [{"role": r, "frame": "plant"} for r in SET_ROLES],
        "thresholds": [{"pieces": 2}, {"pieces": 4, "families": [_legal_set_stat()]}],
    }


def _clean_charm_answer() -> dict:
    return {
        "name": "Proof Charm", "nameKey": "charm.proof-charm",
        "flavor": "A fixture, and it says so.",
        "charmClass": "minor", "axis": "survivability", "frameHint": "any",
        "families": [_legal_charm_family()],
    }


def _set_plan(subject_count: int = 1) -> RunPlan:
    theme = _build_theme()
    subjects = [
        Subject(subject_id=f"set-build-build.might-offense-{i}", kind="set", population="build",
                theme_key=theme.theme_key, entry_id=emit.build_set_id("might", "offense", i + 1),
                brief=brief_mod.build_set_brief(theme, TUNING, VOCAB))
        for i in range(subject_count)
    ]
    return RunPlan(subjects=subjects, held=[], already_done=[])


def _charm_plan() -> RunPlan:
    theme = _species_theme()
    return RunPlan(subjects=[Subject(
        subject_id="charm-species-demon.testspecies", kind="charm", population="species",
        theme_key=theme.theme_key, entry_id="charm.(axis-group)-NNN for testspecies",
        brief=brief_mod.build_charm_brief(theme, TUNING, VOCAB))], held=[], already_done=[])


def _answer_file(kind: str, population: str, mapping: dict) -> answers_mod.AnswerFile:
    return answers_mod.AnswerFile(kind=kind, population=population,
                                  prompt_version=brief_mod.PROMPT_VERSION,
                                  by_subject={k: tuple(v if isinstance(v, list) else [v])
                                              for k, v in mapping.items()})


class TransportTests(unittest.TestCase):
    def test_the_transport_module_cannot_reach_the_network(self):
        """The replay path must be provably offline, not merely intended to be. Asserted by module
        text, the same structural discipline `nothing_in_the_generator_writes_the_demons_corpus`
        already uses for the demon corpus."""
        source = (Path(answers_mod.__file__)).read_text(encoding="utf-8")
        body = source.split('"""', 2)[-1]      # skip the header, which discusses llm_caller
        for banned in ("llm_caller", "urllib", "requests", "socket", "http"):
            self.assertNotIn(banned, body, f"{banned!r} appears in the replay transport's code")

    def test_an_answer_file_is_refused_rather_than_defaulted(self):
        cases = {
            "wrong version": {"schemaVersion": 99, "kind": "set", "population": "build",
                              "promptVersion": "x", "answers": {"a": {}}},
            "no answers": {"schemaVersion": 1, "kind": "set", "population": "build",
                           "promptVersion": "x", "answers": {}},
            "missing kind": {"schemaVersion": 1, "population": "build", "promptVersion": "x",
                             "answers": {"a": {}}},
            "answer is not an object": {"schemaVersion": 1, "kind": "set", "population": "build",
                                        "promptVersion": "x", "answers": {"a": ["nope"]}},
        }
        import tempfile
        for label, doc in cases.items():
            with self.subTest(label):
                with tempfile.TemporaryDirectory() as tmp:
                    path = Path(tmp) / "answers.json"
                    path.write_text(json.dumps(doc), encoding="utf-8")
                    with self.assertRaises(answers_mod.AnswerFileError):
                        answers_mod.load_answers(path)

    def test_attempts_are_served_in_order_and_then_escalate(self):
        briefs = {"s1": "BRIEF ONE"}
        file = _answer_file("set", "build", {"s1": [{"n": 1}, {"n": 2}]})
        call = answers_mod.replay_caller(briefs, file)
        self.assertEqual(json.loads(call("sys", "BRIEF ONE")), {"n": 1})
        self.assertEqual(json.loads(call("sys", "BRIEF ONE\n\nmore")), {"n": 2})
        with self.assertRaises(answers_mod.AnswerExhausted):
            call("sys", "BRIEF ONE")

    def test_an_exhausted_subject_carries_the_defects_it_was_asked_to_repair(self):
        """The repair prompt is the only place those strings still exist by the time the transport
        is reached; losing them turns a precise refusal into 'the batch stopped'."""
        briefs = {"s1": "BRIEF"}
        call = answers_mod.replay_caller(briefs, _answer_file("set", "build", {"s1": {"n": 1}}))
        call("sys", "BRIEF")
        repair = ("BRIEF\n\n" + answers_mod.REPAIR_HEADER
                  + "\n- SetTierForbiddenAtom: nope\n- SetRoleForbidden: also nope\nFix exactly these.")
        with self.assertRaises(answers_mod.AnswerExhausted) as caught:
            call("sys", repair)
        self.assertEqual(caught.exception.defects,
                         ("SetTierForbiddenAtom: nope", "SetRoleForbidden: also nope"))

    def test_a_prompt_from_another_plan_is_refused_not_guessed(self):
        call = answers_mod.replay_caller({"s1": "BRIEF"}, _answer_file("set", "build", {"s1": {}}))
        with self.assertRaises(answers_mod.AnswerMissing):
            call("sys", "a completely different brief")

    def test_two_subjects_with_identical_briefs_are_refused_not_cross_served(self):
        """Two subjects built from the same theme produce byte-identical briefs — module 13's own
        `re_running_over_unchanged_themes_is_byte_identical` guarantees it — so prefix matching
        alone would silently hand one subject's answer to the other."""
        call = answers_mod.replay_caller(
            {"s1": "SAME", "s2": "SAME"},
            _answer_file("set", "build", {"s1": {"n": 1}, "s2": {"n": 2}}))
        with self.assertRaises(answers_mod.AnswerMissing) as caught:
            call("sys", "SAME")
        self.assertIn("identical briefs", str(caught.exception))
        call.current_subject_id = "s2"
        self.assertEqual(json.loads(call("sys", "SAME")), {"n": 2})


class SchemaCheckTests(unittest.TestCase):
    """Constrained decoding is the ENDPOINT's guarantee; a replayed answer has to earn it here."""

    def test_a_clean_answer_has_no_schema_defects(self):
        for label, draft, schema in (
            ("set", _clean_set_answer(), schema_mod.set_schema(TUNING)),
            ("charm", _clean_charm_answer(), schema_mod.charm_schema(TUNING)),
        ):
            with self.subTest(label):
                self.assertEqual(answers_mod.schema_defects(draft, schema), [])

    def test_each_closed_keyword_is_actually_enforced(self):
        schema = schema_mod.set_schema(TUNING)
        cases = {
            "unknown field": ({**_clean_set_answer(), "tier": "x"}, "unknown field"),
            "bad role enum": ({**_clean_set_answer(),
                               "members": [{"role": "head-guard", "frame": "plant"}]},
                              "is not one of"),
            "bad pieces enum": ({**_clean_set_answer(),
                                 "thresholds": [{"pieces": 5}, {"pieces": 4}]}, "is not one of"),
            "bad nameKey pattern": ({**_clean_set_answer(), "nameKey": "Set Proof"},
                                    "does not match the required pattern"),
            "short name": ({**_clean_set_answer(), "name": "x"}, "below the minimum"),
            "too many families": ({**_clean_set_answer(),
                                   "thresholds": [{"pieces": 2},
                                                  {"pieces": 4, "families": ["a", "b", "c", "d"]}]},
                                  "above the maximum"),
        }
        for label, (draft, needle) in cases.items():
            with self.subTest(label):
                found = answers_mod.schema_defects(draft, schema)
                self.assertTrue(any(needle in d for d in found), f"{label}: {found}")

    def test_a_boolean_is_not_an_integer(self):
        """`bool` is an `int` in Python and slips through a bare isinstance check."""
        self.assertTrue(answers_mod.schema_defects(
            {"pieces": True}, {"type": "object", "properties": {"pieces": {"type": "integer"}}}))


class OutDirGuardTests(unittest.TestCase):
    def test_the_production_tree_is_refused_unless_asked_for(self):
        inside = seedfile_mod.ITEM_SEED_ROOT / "_sample"
        with self.assertRaises(seedfile_mod.OutDirRefused):
            seedfile_mod.resolve_out_dir(inside)
        self.assertEqual(seedfile_mod.resolve_out_dir(inside, allow_production_tree=True),
                         inside.resolve())

    def test_a_path_outside_the_item_tree_is_allowed(self):
        outside = REPO_ROOT / "tools" / "seedsmith" / "_sample-runs" / "x"
        self.assertEqual(seedfile_mod.resolve_out_dir(outside), outside.resolve())


class IdTests(unittest.TestCase):
    def test_the_axis_group_fold_is_read_from_the_registry(self):
        mapping = seedfile_mod.axis_group_map()
        self.assertEqual(mapping["offense"], "off-ctrl")
        self.assertEqual(mapping["control"], "off-ctrl")
        self.assertEqual(mapping["survivability"], "surv-util")
        self.assertEqual(mapping["economy"], "econ")
        self.assertEqual(set(mapping), set(charm_rules.CHARM_AXES))

    def test_a_generated_charm_continues_the_shipped_partition(self):
        """Ids are never reused, so a generated charm resumes the partition rather than colliding
        with `charm.off-ctrl-001`. Measured off the live corpus, not assumed."""
        self.assertGreater(seedfile_mod.next_charm_seq("off-ctrl"), 1)


class BatchTests(unittest.TestCase):
    def test_a_small_batch_runs_end_to_end_and_writes_a_seed_file(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "sets"
            plan = _set_plan()
            subject_id = plan.subjects[0].subject_id
            result = authored_mod.run_batch(
                plan=plan, answers=_answer_file("set", "build", {subject_id: _clean_set_answer()}),
                tuning=TUNING, vocabulary=VOCAB, out_dir=out, kind="set", population="build",
                authored_utc="1970-01-01T00:00:00Z", model="fixture",
                ledger_path=out / "ledger.json")
            self.assertEqual(len(result.persisted), 1, result.to_dict())
            self.assertEqual(len(result.files), 1)
            doc = json.loads(result.files[0].read_text(encoding="utf-8"))
            self.assertEqual(doc["kind"], "set")
            entry = doc["entries"][0]
            self.assertEqual(entry["id"], "set.might-offense-001")
            self.assertEqual(entry["themeKey"], "build.might-offense")
            self.assertEqual([t["pieces"] for t in entry["thresholds"]], [2, 4])
            self.assertIn("capability", entry["thresholds"][0])
            self.assertIn("atoms", entry["thresholds"][1])
            self.assertTrue((out / "ledger.json").exists())

    def test_a_charm_batch_mints_its_id_from_the_axis_the_model_chose(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            out = Path(tmp) / "charms"
            plan = _charm_plan()
            subject_id = plan.subjects[0].subject_id
            result = authored_mod.run_batch(
                plan=plan,
                answers=_answer_file("charm", "species", {subject_id: _clean_charm_answer()}),
                tuning=TUNING, vocabulary=VOCAB, out_dir=out, kind="charm", population="species",
                authored_utc="1970-01-01T00:00:00Z", model="fixture")
            self.assertEqual(len(result.persisted), 1, result.to_dict())
            entry = result.entries[0]
            self.assertTrue(entry["id"].startswith("charm.surv-util-"), entry["id"])
            self.assertEqual(entry["apCost"], TUNING.charm_class("minor").ap_cost,
                             "apCost is derived from the class, never taken from the answer")

    def test_a_signets_drawback_is_marked_negative_the_way_the_corpus_marks_it(self):
        """`charm.econ-019` writes its cost as `params.sign = "negative"` on an ordinary fixed
        atom. Emitting it unmarked would turn a signet's cost into a second bonus."""
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            plan = _charm_plan()
            families = _legal_charm_families()
            answer = {**_clean_charm_answer(), "charmClass": "signet",
                      "families": [families[0]], "drawback": {"family": families[1]}}
            result = authored_mod.run_batch(
                plan=plan,
                answers=_answer_file("charm", "species", {plan.subjects[0].subject_id: answer}),
                tuning=TUNING, vocabulary=VOCAB, out_dir=Path(tmp) / "charms", kind="charm",
                population="species", authored_utc="1970-01-01T00:00:00Z", model="fixture")
            self.assertEqual(len(result.persisted), 1, result.to_dict())
            atoms = result.entries[0]["fixedAtoms"]
            self.assertEqual(atoms[-1]["family"], families[1])
            self.assertEqual(atoms[-1]["params"]["sign"], "negative")
            self.assertNotIn("sign", atoms[0].get("params", {}))

    def test_a_refused_draft_with_no_repair_escalates_that_subject_only(self):
        """One subject running out of attempts must not take the batch with it."""
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            plan = _set_plan(2)
            bad = {**_clean_set_answer(),
                   "thresholds": [{"pieces": 2},
                                  {"pieces": 4, "families": [sorted(distribute.MORE_OP_FAMILIES)[0]]}]}
            mapping = {plan.subjects[0].subject_id: bad,
                       plan.subjects[1].subject_id: _clean_set_answer()}
            result = authored_mod.run_batch(
                plan=plan, answers=_answer_file("set", "build", mapping), tuning=TUNING,
                vocabulary=VOCAB, out_dir=Path(tmp) / "sets", kind="set", population="build",
                authored_utc="1970-01-01T00:00:00Z", model="fixture")
            outcomes = {o.subject_id: o for o in result.outcomes}
            first = outcomes[plan.subjects[0].subject_id]
            self.assertEqual(first.outcome, "escalated")
            self.assertTrue(any("More-op" in d for d in first.defects), first.defects)
            self.assertEqual(outcomes[plan.subjects[1].subject_id].outcome, "persisted")

    def test_a_blocked_answer_writes_nothing_and_is_reported(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            plan = _set_plan()
            result = authored_mod.run_batch(
                plan=plan,
                answers=_answer_file("set", "build",
                                     {plan.subjects[0].subject_id: {"blocked": "no motifs land"}}),
                tuning=TUNING, vocabulary=VOCAB, out_dir=Path(tmp) / "sets", kind="set",
                population="build", authored_utc="1970-01-01T00:00:00Z", model="fixture")
            self.assertEqual(result.outcomes[0].outcome, "blocked")
            self.assertEqual(result.files, [])

    def test_the_model_call_is_the_only_path_to_a_model(self):
        """A raising stub in `call` proves nothing else in the batch driver reaches an endpoint."""
        import tempfile

        def raising(*_args, **_kwargs):
            raise AssertionError("the batch driver reached a model outside the injected call")

        with tempfile.TemporaryDirectory() as tmp:
            plan = _set_plan()
            with self.assertRaises(AssertionError):
                authored_mod.run_batch(
                    plan=plan,
                    answers=_answer_file("set", "build",
                                         {plan.subjects[0].subject_id: _clean_set_answer()}),
                    tuning=TUNING, vocabulary=VOCAB, out_dir=Path(tmp), kind="set",
                    population="build", authored_utc="1970-01-01T00:00:00Z", model="fixture",
                    call=raising)

    def test_the_report_names_every_gating_metric_even_when_it_did_not_run(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            plan = _set_plan()
            result = authored_mod.run_batch(
                plan=plan,
                answers=_answer_file("set", "build",
                                     {plan.subjects[0].subject_id: _clean_set_answer()}),
                tuning=TUNING, vocabulary=VOCAB, out_dir=Path(tmp) / "sets", kind="set",
                population="build", authored_utc="1970-01-01T00:00:00Z", model="fixture")
            named = {o.metric for o in result.report.outcomes}
            self.assertEqual(named, set(GATING_METRICS))
            self.assertIs(result.report.verdict, Verdict.NOT_MEASURED,
                          "a batch that could not run every gate is never a pass")

    def test_a_held_partition_alone_denies_a_pass(self):
        import tempfile
        with tempfile.TemporaryDirectory() as tmp:
            plan = _set_plan()
            plan.held = [("demon.something", "basis=name")]
            result = authored_mod.run_batch(
                plan=plan,
                answers=_answer_file("set", "build",
                                     {plan.subjects[0].subject_id: _clean_set_answer()}),
                tuning=TUNING, vocabulary=VOCAB, out_dir=Path(tmp) / "sets", kind="set",
                population="build", authored_utc="1970-01-01T00:00:00Z", model="fixture")
            self.assertEqual(result.report.held_partitions, ["demon.something"])
            self.assertIs(result.report.verdict, Verdict.NOT_MEASURED)


class Module13DefectsFixedTests(unittest.TestCase):
    """⭐ Five defects were measured in module 13's OWN machinery on 2026-09-06 while wiring the
    graph, pinned here as "this is the state today", and **fixed on the same day**. Each test below
    now asserts the FIXED state with the same defect number, so a regression reads as the original
    defect coming back rather than as an anonymous failure.

    The before-numbers are kept in the docstrings on purpose: they are the measurement, and a fix
    whose evidence has been deleted cannot be re-checked.
    """

    def test_the_charm_brief_offers_exactly_the_pool_the_distributor_accepts(self):
        """DEFECT 1, FIXED. `build_charm_brief` printed `vocabulary.stat` truncated to the first 60
        of 242 picks, while `distribute_charm` accepted 56 picks across 14 families — every one
        armour or shield. Brief and distributor now read the same expression,
        `charmgen.rules.charm_pool`, and it is printed whole."""
        pool = charm_rules.charm_pool(TUNING, VOCAB.all_picks)
        shown = brief_mod.build_charm_brief(_species_theme(), TUNING, VOCAB)
        self.assertGreater(len(pool), 56)
        for pick in pool:
            self.assertIn(f"  - {pick.pick_id}\n", shown + "\n")
        self.assertNotIn("...and", shown, "the charm pool is never truncated")
        excluded = graph_mod.jewel_minor_for(TUNING, VOCAB)
        for pick in VOCAB.all_picks:
            if pick.family in excluded or pick.family in charm_rules.NON_FLAT_FAMILIES:
                with self.subTest(pick=pick.pick_id):
                    self.assertNotIn(f"  - {pick.pick_id}\n", shown + "\n")

    def test_a_charm_on_every_one_of_the_five_axes_is_authorable_from_the_brief(self):
        """DEFECT 1's consequence, FIXED. The surviving pool was 14 families, all defensive, so
        `offense` / `control` / `utility` / `economy` were unauthorable — four of the five shipped
        axes and 48 of the 70 shipped rows. The pool now spans every tag the family corpus has."""
        pool = charm_rules.charm_pool(TUNING, VOCAB.all_picks)
        families = {p.family for p in pool}
        self.assertIn("atom.might", families, "an offense charm needs an offense family")
        self.assertIn("atom.midas", families, "an economy charm needs an economy family")
        self.assertIn("atom.cleansing", families, "a utility charm needs a utility family")
        self.assertIn("atom.vitality", families, "a survivability charm needs a defensive family")
        # ssot-charms §3.6's own charm family list, every one of which the old rule refused.
        for named in ("atom.vitality", "atom.might", "atom.mending", "atom.regeneration",
                      "atom.sunbloom", "atom.midas", "atom.cleansing"):
            with self.subTest(family=named):
                self.assertIn(named, families)

    def test_the_charm_pool_now_covers_the_families_the_shipped_corpus_uses(self):
        """DEFECT 2, FIXED. 22 of the 29 distinct families the 70 shipped charms use are CAPABILITY
        families; the brief offered `vocabulary.stat` only, so a generated population could not
        resemble the authored one. The pool is drawn from both buckets now.

        ⚠ Two named divergences survive on purpose and are asserted rather than smoothed away:
        three families the shipped charms use are declared by no `affix-families/*.json` file, and
        11 of the families it uses are ssot-charms §3.6's ring layer — 20 of the 70 shipped rows,
        which is shipped content standing against §3.6, not a generator defect. The generator
        refuses to author more of it; changing the corpus is a separate, content-owning decision.
        """
        charms_dir = REPO_ROOT / "data" / "seed" / "items" / "charms"
        used: "set[str]" = set()
        rows_on_ring = 0
        for path in sorted(charms_dir.glob("*.json")):
            doc = json.loads(path.read_text(encoding="utf-8"))
            for entry in doc.get("entries") or []:
                for atom in entry.get("fixedAtoms") or []:
                    used.add(atom["family"])
        capability = {p.family for p in VOCAB.capability}
        stat = {p.family for p in VOCAB.stat}
        self.assertGreater(len(used & capability), len(used & stat),
                           "the shipped corpus is capability-led, and the pool must be too")
        undeclared = used - capability - stat
        self.assertEqual(undeclared, {"atom.commanding", "atom.exposing", "atom.rallying"},
                         "three charm families are declared by no affix-family file")
        pool = {p.family for p in charm_rules.charm_pool(TUNING, VOCAB.all_picks)}
        ring = charm_rules.ring_layer_families(TUNING, VOCAB.all_picks)
        self.assertEqual(used - undeclared - pool, used & ring,
                         "the only shipped families the pool omits are §3.6's ring layer")
        self.assertEqual(len(ring), 13)
        self.assertEqual(len(used & ring), 11)
        for path in sorted(charms_dir.glob("*.json")):
            doc = json.loads(path.read_text(encoding="utf-8"))
            for entry in doc.get("entries") or []:
                if any(a["family"] in ring for a in entry.get("fixedAtoms") or []):
                    rows_on_ring += 1
        self.assertEqual(rows_on_ring, 20, "shipped rows standing against §3.6, counted not guessed")

    def test_the_set_brief_names_the_derived_piece_counts_and_the_schema_agrees(self):
        """DEFECT 3, FIXED. The schema offered `pieces` from `[2, 3, 4, 6]` and the brief said
        "the piece counts", while `threshold_ladder` derived `(2, 4)` from the member count and
        refused everything else — all three sets in the first real batch picked 3 and were refused.
        The enum, the row count and the brief all read the ladder now."""
        ladder = distribute.threshold_ladder(TUNING, TUNING.typical_members)
        self.assertEqual(ladder, (2, 4))
        node = schema_mod.set_schema(TUNING)["properties"]["thresholds"]
        self.assertEqual(node["items"]["properties"]["pieces"]["enum"], [2, 4])
        self.assertEqual((node["minItems"], node["maxItems"]), (2, 2))
        text = brief_mod.build_set_brief(_build_theme(), TUNING, VOCAB)
        self.assertIn("exactly 2 entries, at 2 and 4 pieces", text)
        self.assertIn("not yours to choose", text)

    def test_a_five_member_set_is_authorable(self):
        """DEFECT 4, FIXED. `threshold_ladder(5)` was `(2,)` — one threshold — against a schema
        that required `minItems: 2`, so a five-member set could satisfy neither, and nothing refused
        the member count either. §3.4 says the top threshold is `<=` the member count, not `=`, so
        the ladder now takes the highest legal count the set can reach.

        The two-member end is fixed by the same change from the other side: its ladder is legitimately
        one row, and the schema is now sized from the ladder instead of assuming two.
        """
        self.assertEqual(distribute.threshold_ladder(TUNING, 5), (2, 4))
        five = schema_mod.set_schema(TUNING, member_count=5)["properties"]["thresholds"]
        self.assertEqual((five["minItems"], five["maxItems"]), (2, 2))
        self.assertEqual(five["items"]["properties"]["pieces"]["enum"], [2, 4])
        two = schema_mod.set_schema(TUNING, member_count=2)["properties"]["thresholds"]
        self.assertEqual((two["minItems"], two["maxItems"]), (1, 1))
        for count in (2, 3, 4, 5, 6):
            with self.subTest(members=count):
                ladder = distribute.threshold_ladder(TUNING, count)
                node = schema_mod.set_schema(TUNING, member_count=count)["properties"]["thresholds"]
                self.assertEqual(node["minItems"], len(ladder))
                self.assertEqual(node["items"]["properties"]["pieces"]["enum"], list(ladder))

    def test_cell_occupancy_reads_the_element_the_corpus_actually_writes(self):
        """DEFECT 5, FIXED. `cells.threshold_capability` read a `variant` key; the corpus writes
        `params.element` (`set.frostbitten-vanguard-002`/`-003`, `set.sunwoven-almanac-003`), so
        `atom.deathblast.fire` and `atom.deathblast.ice` collapsed into one cell.

        ⚠ Found while fixing it, and fixed with it: `threshold_families` dropped the element the
        same way, on the higher-threshold atoms — which is where a GENERATED set puts most of its
        element-narrowed picks.
        """
        fire = {"family": "atom.deathblast", "powerBand": "low", "params": {"element": "fire"}}
        ice = {"family": "atom.deathblast", "powerBand": "low", "params": {"element": "ice"}}
        self.assertEqual(cells.threshold_capability({"capability": fire}), "atom.deathblast.fire")
        self.assertEqual(cells.threshold_capability({"capability": ice}), "atom.deathblast.ice")
        self.assertEqual(cells.threshold_families({"atoms": [fire, ice]}),
                         ("atom.deathblast.fire", "atom.deathblast.ice"))
        # The generator's own internal spelling still resolves — both shapes are read.
        self.assertEqual(cells.threshold_capability(
            {"capability": {"family": "atom.deathblast", "variant": "fire"}}),
            "atom.deathblast.fire")
        both = [{"thresholds": [{"pieces": 2, "capability": fire}]},
                {"thresholds": [{"pieces": 2, "capability": ice}]}]
        self.assertEqual(cells.cell_report(both).cells, 2,
                         "two elements of one family are two cells, not one")

    def test_the_axis_gini_report_names_its_own_floor(self):
        """Found while re-sampling the fixes, and fixed with them (not one of the five).

        Three charms over five axes is `[1,1,1,0,0]` at best — **400‰, against a 133‰ ceiling** —
        so the first sample's *"axis Gini 800‰ against a 133‰ ceiling"* and a perfectly diverse
        batch's *"400‰ against a 133‰ ceiling"* read identically, though one is a total collapse and
        the other is as flat as `n` allows. The floor is reported beside the measurement, the way
        `SemanticDedup/NearDuplicate` already says "granularity-bound below ~200 entries".
        """
        self.assertEqual(charm_rules.min_axis_gini_permille(1), 800)
        self.assertEqual(charm_rules.min_axis_gini_permille(3), 400)
        self.assertEqual(charm_rules.min_axis_gini_permille(5), 0)
        self.assertEqual(charm_rules.min_axis_gini_permille(0), 0)
        self.assertEqual(
            charm_rules.smallest_measurable_axis_population(TUNING.charm_axis_gini_max_permille), 5)
        # A one-axis collapse and a maximally spread batch must not print the same line.
        collapsed = charm_rules.axis_gini_permille(["survivability"] * 3)
        spread = charm_rules.axis_gini_permille(["offense", "control", "economy"])
        self.assertEqual((collapsed, spread), (800, 400))

    def test_the_element_read_does_not_move_the_shipped_corpus_numbers(self):
        """The same fix, measured where it matters: the live set corpus reports identically before
        and after, so the gate baseline does not move. Latent at 30 sets, real at ~904."""
        entries = []
        for path in sorted((REPO_ROOT / "data" / "seed" / "items" / "sets").glob("*.json")):
            doc = json.loads(path.read_text(encoding="utf-8"))
            entries.extend(doc.get("entries") or [])
        report = cells.cell_report(entries)
        self.assertEqual((report.population, report.cells, report.maximum, report.singletons),
                         (30, 28, 2, 26))


if __name__ == "__main__":
    unittest.main()
