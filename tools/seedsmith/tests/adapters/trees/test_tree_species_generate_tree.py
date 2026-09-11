"""Tests for seedsmith.adapters.trees.species.generate_tree.run_species_tree (task J8's own stated
remainder / task J9's real prerequisite) — the per-species orchestration caller: favour-fit ->
plan/quota -> node generation -> marking -> codex-summary -> the species metadata file.
"""
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parent))

from seedsmith.adapters.trees.species.generate_tree import run_species_tree
from seedsmith.adapters.trees.species.plan import FavourCell
from seedsmith.adapters.trees.species.roster import SpeciesAnchor
from seedsmith.adapters.trees.nodegen import tuning as nodegen_tuning
from seedsmith.adapters.trees.plan import tuning as plan_tuning
from seedsmith.pipeline.llm_caller import LlmCallerConfig

TEST_CONFIG = LlmCallerConfig(max_heal=0, model="test-model")


def _anchor(species_id: str) -> SpeciesAnchor:
    return SpeciesAnchor(
        species_id=species_id, element_primary="fire", aptitude_primary="Onslaught",
        posture="Force", traits=("relentless",), reason="a test fixture creature",
        source_path="test.json")


def _stage_call(system: str, user: str, *, config=None, schema=None) -> str:
    """One combined stub for BOTH injected-`call` stages (favour-fit, codex) -- disambiguated by
    the response schema's own declared properties, the only thing distinguishing them at this
    layer."""
    props = (schema or {}).get("properties", {})
    if "choice" in props:
        return json.dumps({"choice": "offered", "blocked": ""})
    if "codexSummary" in props:
        return json.dumps({"codexSummary": "Rewards fire-forward, relentless aggression.", "blocked": ""})
    raise AssertionError(f"unexpected schema shape for injected call: {sorted(props)}")


def _node_call_stub():
    """Node-generation stub (patches `llm_caller.call_model` directly, `run_language_stage`'s own
    contract) -- groups every 3 consecutive calls into one node (§7.1's own vote-3 cost shape,
    confirmed live in `test_nodegen_language_stage.py`), and reads a real permitted affix id back
    off the schema's own gate-8 enum rather than a hand-typed one that could drift."""
    state = {"n": 0}

    def _call(system, user, *, config=None, temperature=0.2, schema=None):
        node_index = state["n"] // 3
        state["n"] += 1
        response = {
            "affixIds": ["atom.a"], "affinity": ["core"],
            "exclusion": {"form": "none", "propertyKeys": []},
            "name": f"Test Node {node_index}", "nameKey": f"tree.node.test-node-{node_index}",
            "flavor": "A steady line.", "rationale": "", "blocked": "",
        }
        enum = (schema or {}).get("properties", {}).get("affixIds", {}).get("items", {}).get("enum") or []
        if enum:
            response["affixIds"] = [enum[0]]
        return json.dumps(response)

    return _call


def _colliding_node_call_stub():
    """Reproduces the REAL live-model finding this test exists for (2026-09-07,
    `AbyssSwordStar`'s own first proof-of-concept run): the model can independently choose the
    SAME name for two different nodes in one tree. Every node here answers with the identical
    name/nameKey on purpose."""
    state = {"n": 0}

    def _call(system, user, *, config=None, temperature=0.2, schema=None):
        state["n"] += 1
        response = {
            "affixIds": ["atom.a"], "affinity": ["core"],
            "exclusion": {"form": "none", "propertyKeys": []},
            "name": "Abyssal Shell", "nameKey": "tree.node.abyssal-shell",
            "flavor": "A steady line.", "rationale": "", "blocked": "",
        }
        enum = (schema or {}).get("properties", {}).get("affixIds", {}).get("items", {}).get("enum") or []
        if enum:
            response["affixIds"] = [enum[0]]
        return json.dumps(response)

    return _call


class RunSpeciesTreeTests(unittest.TestCase):
    def setUp(self) -> None:
        self.seed_root = Path(tempfile.mkdtemp())
        self.ledger_path = self.seed_root / "_runs" / "ledger.json"
        self.targets = nodegen_tuning.load()
        self.tuning = plan_tuning.load()

        # `species_tree_spec` reads the REAL, checked-in gate-evidence file (never a fixture stand
        # -in, the same "no defaults, no fallbacks" discipline every other tree factory holds to) --
        # copied into this isolated seed_root so the rest of the run stays sandboxed. Walks up to
        # the repo root by AGENTS.md's own presence, matching test_tree_plan_emit.py's own
        # `real_seed_root()` helper, rather than a fragile hardcoded parents[N] depth.
        import shutil
        repo_root = Path(__file__).resolve()
        while repo_root != repo_root.parent and not (repo_root / "AGENTS.md").exists():
            repo_root = repo_root.parent
        evidence_src = repo_root / "data" / "seed" / "passive-tree" / "gate-evidence.v1.json"
        evidence_dst = self.seed_root / "passive-tree" / "gate-evidence.v1.json"
        evidence_dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy(evidence_src, evidence_dst)
        self.anchor = _anchor("AbyssSwordStar")
        self.offered = FavourCell("Onslaught", "air", "spark")
        self.alternates = [FavourCell("Ferocity", "fire", "poison")]

    def test_a_full_run_resolves_favour_generates_marks_and_writes_the_codex_metadata(self) -> None:
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=_node_call_stub()):
            result = run_species_tree(
                "AbyssSwordStar", self.anchor, 0, self.offered, self.alternates,
                targets=self.targets, tuning=self.tuning, ledger_path=self.ledger_path,
                seed_root=self.seed_root, call=_stage_call, config=TEST_CONFIG, workers=1)

        self.assertEqual(FavourCell("Onslaught", "air", "spark"), result.resolved_cell)
        self.assertIsNone(result.favour_unresolved_reason)
        self.assertIsNotNone(result.nodes_seed_path)
        self.assertTrue(result.nodes_seed_path.exists())
        self.assertEqual(40, result.outcome_counts.get("accepted"))
        self.assertEqual(8, len(result.marked_node_ids), "speciesUniqueAffixMin=8 by default")
        self.assertEqual("Rewards fire-forward, relentless aggression.", result.codex_summary)
        self.assertIsNone(result.codex_unresolved_reason)

        self.assertIsNotNone(result.metadata_path)
        self.assertTrue(result.metadata_path.exists())
        doc = json.loads(result.metadata_path.read_text(encoding="utf-8"))
        self.assertEqual("AbyssSwordStar", doc["speciesId"])
        self.assertEqual({"aptitude": "Onslaught", "element": "air", "status": "spark"},
                        doc["mechanicalFavour"])
        self.assertEqual("Rewards fire-forward, relentless aggression.", doc["codexSummary"])
        self.assertEqual(8, len(doc["speciesUniqueNodeIds"]))

        # The nodes themselves committed to the SHARED nodes/ dir, unchanged path convention.
        nodes_doc = json.loads(result.nodes_seed_path.read_text(encoding="utf-8"))
        self.assertEqual(40, len(nodes_doc["nodes"]))

    def test_a_real_name_key_collision_within_a_parallel_batch_is_deduped_not_raised(self) -> None:
        # The exact regression this test guards against: AbyssSwordStar's own first real
        # proof-of-concept run against the live local model crashed the whole orchestrator with an
        # uncaught NodeKeyRefused the first time this function was ever run for real (two nodes,
        # both independently named "Abyssal Shell"). The first fix reported the refusal instead of
        # crashing, but still lost the whole tree; the durable fix (2026-09-11, matching
        # `_derive_unique_name_key`'s own documented contract) deterministically suffixes the second
        # colliding key at record time, so the tree completes exactly as it does on the sequential
        # path -- never renamed "out from under the model's answer", only its key made unique.
        #
        # `workers=4` here is load-bearing, not cosmetic: two subjects in ONE concurrent batch
        # cannot see each other's pick in time to disambiguate, which is the real, narrow condition
        # `AbyssSwordStar`'s own PoC hit. The sequential path (`workers=1`) already self-healed via
        # `known_name_keys`; this proves the parallel path now does too.
        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=_colliding_node_call_stub()):
            result = run_species_tree(
                "AbyssSwordStar", self.anchor, 0, self.offered, self.alternates,
                targets=self.targets, tuning=self.tuning, ledger_path=self.ledger_path,
                seed_root=self.seed_root, call=_stage_call, config=TEST_CONFIG, workers=4)

        self.assertIsNotNone(result.resolved_cell, "the favour lock still resolved cleanly")
        self.assertIsNone(result.node_key_refused_reason,
                          "a within-batch collision is resolved, never surfaced as a refusal")
        self.assertIsNotNone(result.nodes_seed_path, "the tree completes rather than being lost")
        self.assertGreater(result.outcome_counts.get("accepted", 0), 0)

        # Every persisted nameKey is unique within the tree. The stub is adversarial (EVERY node
        # answers "Abyssal Shell"), so the generation-time taken-names gate legitimately routes
        # later identical drafts to `unresolved` -- gate 21's own contract, "re-prompted, never
        # persisted". What matters here is that a same-batch collision no longer aborts the whole
        # tree (the old bug) and never lands a duplicate in the seed document.
        from seedsmith.adapters.trees.nodegen import emit as emit_mod
        nodes_doc = json.loads(result.nodes_seed_path.read_text(encoding="utf-8"))
        keys = [n["nameKey"] for n in nodes_doc["nodes"]]
        self.assertEqual(len(keys), len(set(keys)), "within-tree nameKeys must all differ")
        self.assertIn("tree.node.abyssal-shell", keys)
        emit_mod.assert_no_duplicate_name_keys(keys)

    def test_an_unresolved_favour_never_reaches_node_generation_at_all(self) -> None:
        def _always_none(system, user, *, config=None, schema=None):
            return json.dumps({"choice": "none", "blocked": ""})

        with patch("seedsmith.pipeline.llm_caller.call_model",
                  side_effect=AssertionError("node generation must never be reached")):
            result = run_species_tree(
                "AbyssSwordStar", self.anchor, 0, self.offered, self.alternates,
                targets=self.targets, tuning=self.tuning, ledger_path=self.ledger_path,
                seed_root=self.seed_root, call=_always_none, config=TEST_CONFIG, workers=1)

        self.assertIsNone(result.resolved_cell)
        self.assertEqual("none_of_the_offered_favours_fit", result.favour_unresolved_reason)
        self.assertIsNone(result.nodes_seed_path)
        self.assertEqual(frozenset(), result.marked_node_ids)
        self.assertIsNone(result.metadata_path)

    def test_an_unresolved_codex_still_completes_the_tree_but_writes_no_metadata_file(self) -> None:
        # Three genuinely different, individually CLEAN sentences (no digits, so every sample
        # passes the content validator and reaches the vote) that still never agree -> a real
        # 1-1-1 vote_unresolved, never a guessed sentence.
        answers = iter(["A relentless line.", "A patient line instead.", "Something else entirely."])

        def _favour_offered_codex_split(system, user, *, config=None, schema=None):
            props = (schema or {}).get("properties", {})
            if "choice" in props:
                return json.dumps({"choice": "offered", "blocked": ""})
            return json.dumps({"codexSummary": next(answers), "blocked": ""})

        with patch("seedsmith.pipeline.llm_caller.call_model", side_effect=_node_call_stub()):
            result = run_species_tree(
                "AbyssSwordStar", self.anchor, 0, self.offered, self.alternates,
                targets=self.targets, tuning=self.tuning, ledger_path=self.ledger_path,
                seed_root=self.seed_root, call=_favour_offered_codex_split, config=TEST_CONFIG,
                workers=1)

        self.assertIsNotNone(result.resolved_cell)
        self.assertIsNotNone(result.nodes_seed_path)
        self.assertEqual(8, len(result.marked_node_ids), "marking never depends on the codex stage")
        self.assertIsNone(result.codex_summary)
        self.assertEqual("vote_unresolved", result.codex_unresolved_reason)
        self.assertIsNone(result.metadata_path, "no metadata file without a real codex summary")


if __name__ == "__main__":
    unittest.main()
