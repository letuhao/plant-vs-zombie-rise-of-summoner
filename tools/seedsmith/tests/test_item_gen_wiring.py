"""Tests for the `items generate --write` wiring (item module 13's deferred generation graph).

    python -m pytest tools/seedsmith/tests/test_item_gen_wiring.py -q

⭐ **What this file proves and what it deliberately does not.** It proves the PATH: a planned
subject reaches the graph, an authored answer comes back through the injected transport, the
already-built distributors price it or refuse it with their own rule names, a refusal re-prompts,
an exhausted subject escalates without taking the batch with it, and a clean batch writes a seed
file somewhere it was told to. It does not judge the CONTENT — that is what the run report's
distinctness metrics are for, and they are measured over a real batch in the module's todo entry.

⚠ **Four assertions here pin measured defects in module 13's OWN machinery rather than in this
wiring.** They are written as "this is the state today", with the number, so that fixing the defect
turns them red and the fix cannot land silently. Each names its defect in the docstring.
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
from seedsmith.adapters.items.setgen import distribute, emit  # noqa: E402
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


def _legal_charm_family() -> str:
    excluded = graph_mod.jewel_minor_for(VOCAB)
    for pick in VOCAB.stat:
        if (pick.variant is None and pick.family not in excluded
                and pick.family not in charm_rules.NON_FLAT_FAMILIES):
            return pick.family
    raise AssertionError("no stat family survives ssot-charms §3.6")


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
            families = [p.family for p in VOCAB.stat
                        if p.variant is None and p.family not in graph_mod.jewel_minor_for(VOCAB)
                        and p.family not in charm_rules.NON_FLAT_FAMILIES]
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


class MeasuredDefectsInModule13Tests(unittest.TestCase):
    """⛔ Four states measured 2026-09-06 while wiring the graph. Each is a defect in module 13's
    OWN machinery, pinned here with its number so a fix turns this red instead of landing silently.
    None of them is fixed by this wiring — see the todo entry for the argument."""

    def test_the_charm_brief_offers_a_pool_it_will_then_refuse(self):
        """DEFECT 1. `build_charm_brief` prints `vocabulary.stat` unnarrowed — 242 picks — but
        `distribute_charm` refuses every family legal on a jewel-minor role (ssot-charms §3.6).
        56 of 242 picks survive, across 14 families, and every one is armour or shield."""
        excluded = graph_mod.jewel_minor_for(VOCAB)
        legal = [p for p in VOCAB.stat
                 if p.family not in excluded and p.family not in charm_rules.NON_FLAT_FAMILIES]
        self.assertEqual(len(VOCAB.stat), 242)
        self.assertEqual(len(legal), 56)
        self.assertEqual(len({p.family for p in legal}), 14)
        shown = brief_mod.build_charm_brief(_species_theme(), TUNING, VOCAB)
        illegal_shown = [p.pick_id for p in VOCAB.stat[:60] if p in set(VOCAB.stat) - set(legal)]
        self.assertTrue(illegal_shown, "the brief's visible pool should contain refused picks")
        for pick in illegal_shown[:3]:
            self.assertIn(pick, shown)

    def test_no_charm_on_four_of_the_five_axes_is_authorable_from_the_brief(self):
        """DEFECT 1, restated as its consequence. Every surviving family is defensive, so an
        `offense` / `control` / `utility` / `economy` charm cannot be authored — yet those are four
        of the five shipped axes and 48 of the 70 shipped charm rows."""
        excluded = graph_mod.jewel_minor_for(VOCAB)
        legal = {p.family for p in VOCAB.stat
                 if p.family not in excluded and p.family not in charm_rules.NON_FLAT_FAMILIES}
        offensive = {"atom.might", "atom.ferocity", "atom.savagery"}
        self.assertEqual(legal & offensive, set())
        self.assertTrue(all(f.startswith(("atom.sh", "atom.arm-", "atom.plating", "atom.carapace",
                                          "atom.warding", "atom.resilience")) for f in legal),
                        sorted(legal))

    def test_the_shipped_charm_corpus_draws_from_a_pool_the_brief_never_offers(self):
        """DEFECT 2. 22 of the 29 distinct families the 70 shipped charms use are CAPABILITY
        families (`atom.freezing`, `atom.searing-strike`, …). The charm brief offers only
        `vocabulary.stat`, so a generated charm population cannot resemble the authored one."""
        charms_dir = REPO_ROOT / "data" / "seed" / "items" / "charms"
        families: "set[str]" = set()
        for path in sorted(charms_dir.glob("*.json")):
            doc = json.loads(path.read_text(encoding="utf-8"))
            for entry in doc.get("entries") or []:
                for atom in entry.get("fixedAtoms") or []:
                    families.add(atom["family"])
        capability = {p.family for p in VOCAB.capability}
        stat = {p.family for p in VOCAB.stat}
        self.assertGreater(len(families & capability), len(families & stat),
                           "the shipped corpus is capability-led; the brief is stat-only")
        self.assertEqual(families - capability - stat,
                         {"atom.commanding", "atom.exposing", "atom.rallying"},
                         "three charm families are declared by no affix-family file")

    def test_a_four_member_set_has_no_three_piece_threshold(self):
        """DEFECT 3. The schema offers `pieces` from `[2, 3, 4, 6]`, and the brief asks the model
        for 'the piece counts', but `threshold_ladder` DERIVES the ladder from the member count and
        refuses anything else. A 4-member set may only carry 2 and 4 — nothing in the brief says
        so, and all three sets in the first real batch picked 3 and were refused."""
        self.assertEqual(distribute.threshold_ladder(TUNING, 4), (2, 4))
        self.assertIn(3, TUNING.legal_threshold_pieces)

    def test_a_five_member_set_is_unauthorable(self):
        """DEFECT 4. `threshold_ladder(5)` is `(2,)` — one threshold — while `set_schema` requires
        `minItems: 2` on `thresholds`. A five-member set cannot satisfy both, and nothing refuses
        the member count itself."""
        self.assertEqual(distribute.threshold_ladder(TUNING, 5), (2,))
        self.assertEqual(schema_mod.set_schema(TUNING)["properties"]["thresholds"]["minItems"], 2)


if __name__ == "__main__":
    unittest.main()
