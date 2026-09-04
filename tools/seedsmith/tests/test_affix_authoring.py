"""T7.1 (`affix-authoring`, spec-affix-authoring.md, effect-pipeline module 9) — the named,
multi-atom, slotted affix pipeline. Reuses `demon-seed`'s own `classify-pipelines` machinery
(`permute`/`vote`) and T5.0's own `build_generation_graph` skeleton — this file proves BOTH: the
domain logic (derivation, voting, numeric-smuggling), and that no second pipeline shape was forked
to build it.
"""
from __future__ import annotations

import ast
import json
from pathlib import Path

import pytest

from seedsmith.adapters.demons.anchor.audit import numeric_audit
from seedsmith.adapters.demons.anchor.vote import resolve_vote
from seedsmith.adapters.effects.affix.derive import canonical_bundle_key, derive_affix_class
from seedsmith.adapters.effects.affix.generate_affixes import run_voted_draws
from seedsmith.adapters.effects.affix.prompts import (
    AFFIX_SCHEMA,
    ID_PREFIX,
    build_brief,
    build_context,
    bundle_has_at_least_two_refs,
    entry_for,
    refs_are_known_atoms,
)
from seedsmith.workflow.graphs.effect_affix import build_affix_authoring_graph, state_for_affix

REPO_ROOT = Path(__file__).resolve().parents[3]
ADAPTER_DIR = REPO_ROOT / "tools" / "seedsmith" / "seedsmith" / "adapters" / "effects" / "affix"
GRAPH_FILE = REPO_ROOT / "tools" / "seedsmith" / "seedsmith" / "workflow" / "graphs" / "effect_affix.py"


# ---- affix_class is derived, never authored (P1 / seed-contract §2.1) -------------------------------


def test_prefix_when_no_atom_in_the_bundle_has_a_trigger():
    assert derive_affix_class(["atom.a", "atom.b"], has_trigger=lambda a: False) == "prefix"


def test_suffix_when_every_atom_in_the_bundle_has_a_trigger():
    assert derive_affix_class(["atom.a", "atom.b"], has_trigger=lambda a: True) == "suffix"


def test_mixed_when_the_bundle_spans_both_kinds():
    assert derive_affix_class(
        ["atom.passive", "atom.proc"], has_trigger=lambda a: a == "atom.proc"
    ) == "mixed"


def test_an_empty_bundle_raises_rather_than_guessing():
    with pytest.raises(ValueError):
        derive_affix_class([], has_trigger=lambda a: False)


def test_affix_class_is_never_a_field_the_model_authors():
    """The model's own draft schema has no `affixClass` property at all — deriving it is the ONLY
    way it reaches the committed entry."""
    assert "affixClass" not in AFFIX_SCHEMA["properties"]
    entry = entry_for({"name": "Master of Fire and Ice", "refs": ["atom.a", "atom.b"]},
                       affix_id="affix.authored.test", affix_class="mixed")
    assert entry["affixClass"] == "mixed"


# ---- named bundle composition is 3-way voted, same machinery as demon-seed --------------------------


def test_a_3_0_bundle_vote_resolves_high_confidence():
    key = canonical_bundle_key(["atom.a", "atom.b"])
    result = resolve_vote([key, key, key])
    assert result.confidence == "high"
    assert result.value == key


def test_a_reordered_but_identical_bundle_still_counts_as_agreement():
    a = canonical_bundle_key(["atom.b", "atom.a"])
    b = canonical_bundle_key(["atom.a", "atom.b"])
    assert a == b  # canonicalisation, not luck


def test_a_2_1_split_on_bundle_composition_records_the_minority():
    majority = canonical_bundle_key(["atom.a", "atom.b"])
    minority = canonical_bundle_key(["atom.a", "atom.c"])
    result = resolve_vote([majority, majority, minority])
    assert result.confidence == "split"
    assert result.value == majority
    assert result.minority == minority


def test_a_1_1_1_split_on_bundle_composition_resolves_unresolved():
    a = canonical_bundle_key(["atom.a", "atom.b"])
    b = canonical_bundle_key(["atom.a", "atom.c"])
    c = canonical_bundle_key(["atom.a", "atom.d"])
    result = resolve_vote([a, b, c])
    assert result.confidence == "unresolved"
    assert result.value is None  # never silently the first sample


# ---- T7.2: name + ref bundle are voted through the REAL generate_affixes CLI path, not resolve_vote
# ---- called in isolation -----------------------------------------------------------------------------


def _stub_call(responses: "list[dict]"):
    """A `call()` double plus the captured `user` briefs, in call order — the same injection seam
    `build_affix_authoring_graph(call=...)` already uses everywhere else in this file, used here to
    prove `run_voted_draws` (what `generate_affixes.main()` itself calls) makes THREE model calls
    per draw, not one. Zero real HTTP: this never reaches `llm_caller.call_model`."""
    log: "list[str]" = []

    def call(system, user, *, config=None, schema=None):
        log.append(user)
        return json.dumps(responses[len(log) - 1])

    return call, log


def test_named_bundle_composition_is_3_way_voted_via_generate_affixes_cli():
    """A real 2-1 split on the ref bundle, driven through `run_voted_draws` — the actual function
    `generate_affixes.main()` calls — not `resolve_vote` called directly against a fixture."""
    responses = [
        {"name": "Master of Fire and Ice", "refs": ["atom.a", "atom.b"]},
        {"name": "Master of Fire and Ice", "refs": ["atom.b", "atom.a"]},  # reordered, same bundle
        {"name": "Master of Fire and Ice", "refs": ["atom.a", "atom.c"]},  # minority
    ]
    call, log = _stub_call(responses)

    fresh, unresolved, results = run_voted_draws(
        count=1, eligible=["atom.a", "atom.b", "atom.c"],
        atom_triggers={"atom.a": False, "atom.b": False, "atom.c": True},
        provenance_base={"pipeline": "affix-authoring", "model": "test"},
        call=call, workers=1)

    assert len(log) == 3, "one draw must make exactly THREE model calls, not one"
    assert len(set(log)) > 1, "the three calls must carry genuinely different (permuted) briefs"

    assert unresolved == {}
    assert len(fresh) == 1
    entry = next(iter(fresh.values()))
    assert entry["name"] == "Master of Fire and Ice"
    assert sorted(entry["refs"]) == ["atom.a", "atom.b"]
    assert entry["_provenance"]["voteConfidence"]["refs"] == "split"
    assert entry["_provenance"]["voteMinority"]["refs"] == canonical_bundle_key(["atom.a", "atom.c"])
    assert all(r.get("outcome") == "persisted" for r in results.values())


def test_a_1_1_1_split_on_bundle_composition_resolves_unresolved_via_generate_affixes_cli():
    """Three genuinely different bundles from three permuted calls resolve `unresolved` — never
    silently the first sample (spec §4's own explicit warning) — proven through the real CLI path,
    not `resolve_vote` called directly."""
    responses = [
        {"name": "X", "refs": ["atom.a", "atom.b"]},
        {"name": "X", "refs": ["atom.a", "atom.c"]},
        {"name": "X", "refs": ["atom.a", "atom.d"]},
    ]
    call, log = _stub_call(responses)

    fresh, unresolved, results = run_voted_draws(
        count=1, eligible=["atom.a", "atom.b", "atom.c", "atom.d"],
        atom_triggers={"atom.a": False, "atom.b": False, "atom.c": False, "atom.d": False},
        provenance_base={"pipeline": "affix-authoring", "model": "test"},
        call=call, workers=1)

    assert len(log) == 3
    assert fresh == {}, "a 1-1-1 split must never fabricate a persisted entry"
    assert len(unresolved) == 1
    detail = next(iter(unresolved.values()))
    assert detail["reason"] == "vote_unresolved"
    assert detail["refs"]["confidence"] == "unresolved"
    assert detail["name"]["confidence"] == "high"  # the name DID resolve 3-0; only refs split 1-1-1


# ---- validators -----------------------------------------------------------------------------------


def test_refs_outside_the_eligible_set_are_rejected_naming_the_offender():
    ctx = build_context(["atom.a", "atom.b"])
    defects = refs_are_known_atoms({"name": "x", "refs": ["atom.a", "atom.unknown"]}, ctx)
    assert defects and "atom.unknown" in defects[0]


def test_refs_inside_the_eligible_set_pass():
    ctx = build_context(["atom.a", "atom.b"])
    assert refs_are_known_atoms({"name": "x", "refs": ["atom.a", "atom.b"]}, ctx) == []


def test_a_single_ref_bundle_is_a_defect_here_that_is_affix_librarys_job():
    defects = bundle_has_at_least_two_refs({"name": "x", "refs": ["atom.a"]}, {})
    assert defects
    assert bundle_has_at_least_two_refs({"name": "x", "refs": ["atom.a", "atom.b"]}, {}) == []


# ---- the numeric-smuggling audit, same five cases anchor-contract already proves ---------------------


def test_no_magnitude_ever_appears_in_the_authored_schema():
    defects = numeric_audit(AFFIX_SCHEMA)
    assert defects == [], f"authored affix schema smuggles a magnitude: {defects}"


# ---- brief assembly ---------------------------------------------------------------------------------


def test_build_brief_inlines_the_eligible_atoms_literally_never_a_file_citation():
    ctx = build_context(["atom.fire", "atom.ice"], theme_hint="elemental duality")
    brief = build_brief(ctx)
    assert "atom.fire" in brief and "atom.ice" in brief
    assert "elemental duality" in brief
    assert ".json" not in brief  # cites nothing, per commander_effect.py's own established reason


def test_state_for_affix_folds_context_into_a_real_brief():
    state = state_for_affix("test-run", ["atom.fire", "atom.ice"])
    assert state["subject_id"] == "test-run"
    assert "atom.fire" in state["brief"]
    assert state["context"]["eligibleAtoms"] == ["atom.fire", "atom.ice"]


def test_entry_id_uses_the_declared_prefix():
    entry = entry_for({"name": "x", "refs": ["atom.a", "atom.b"]},
                       affix_id=f"{ID_PREFIX}test", affix_class="prefix")
    assert entry["id"].startswith(ID_PREFIX)


# ---- pipeline shape: no fork, same skeleton demon-seed's classify-pipelines already proved -----------


def test_pipeline_shape_matches_demon_seeds_classify_pipelines_exactly():
    """`effect_affix.py` must wire `build_generation_graph`, never construct its own `StateGraph`
    — the repo-wide sweep (`test_no_second_authoring_pipeline_shape_exists`, T5.0) already covers
    every file under `graphs/` including this one; this test additionally proves this specific
    module actually COMPILES into a graph object via that shared skeleton, not merely that it
    avoids the forbidden call."""
    tree = ast.parse(GRAPH_FILE.read_text(encoding="utf-8"))
    calls_state_graph = any(
        isinstance(node, ast.Call) and isinstance(node.func, ast.Name) and node.func.id == "StateGraph"
        for node in ast.walk(tree)
    )
    assert not calls_state_graph

    def raising_call(*_a, **_kw):
        raise AssertionError("graph construction must not call a model")

    graph = build_affix_authoring_graph(call=raising_call)
    assert graph is not None


def test_zero_bare_http_calls_outside_llm_caller():
    """Matches `llm_caller.py`'s own dependency-isolation test convention: this module reaches the
    model only through the injected `call`/`caller` param — a direct `requests`/`httpx`/`urllib`
    import here would bypass `call_with_self_heal`'s self-heal and no-silent-drop guarantees."""
    forbidden = {"requests", "httpx", "urllib"}
    for path in list(ADAPTER_DIR.glob("*.py")) + [GRAPH_FILE]:
        tree = ast.parse(path.read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            mods = ([a.name for a in node.names] if isinstance(node, ast.Import)
                    else [node.module or ""] if isinstance(node, ast.ImportFrom) else [])
            hit = [m for m in mods if m.split(".")[0] in forbidden]
            assert not hit, f"{path.name} imports {hit} directly — route through llm_caller instead"
