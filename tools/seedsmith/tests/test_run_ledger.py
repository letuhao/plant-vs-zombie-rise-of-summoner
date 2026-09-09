"""Tests for seedsmith.pipeline.run_ledger — item-seedgen's generator-harness (module 1), the
resume/reconcile/overwrite acceptance criteria from
docs/architecture/item-seedgen/spec-generator-harness.md.
"""
from __future__ import annotations

import json
from pathlib import Path

import pytest

from seedsmith.pipeline.run_ledger import RunLedger


@pytest.fixture()
def ledger(tmp_path: Path) -> RunLedger:
    return RunLedger(path=tmp_path / "test.ledger.json")


def test_resume_returns_only_unattempted_subjects(ledger: RunLedger) -> None:
    ledger.mark_done("a", {"shape": "v1"})
    needing_work = ledger.plan(["a", "b", "c"], is_valid=lambda sid, entry: True)
    assert needing_work == ["b", "c"]


def test_terminal_escalation_is_typed_and_checkpointed(ledger: RunLedger) -> None:
    ledger.mark_terminal("a", outcome="escalated", entry_id="set.test-001", attempts=3,
                         defects=["schema repair exhausted"])

    row = ledger.read_done()["a"]
    assert row == {
        "terminalSchemaVersion": 1,
        "outcome": "escalated",
        "entryId": "set.test-001",
        "attempts": 3,
        "defects": ["schema repair exhausted"],
    }


def test_terminal_row_refuses_a_success_outcome(ledger: RunLedger) -> None:
    with pytest.raises(ValueError, match="blocked or escalated"):
        ledger.mark_terminal("a", outcome="persisted", entry_id="set.test-001", attempts=1)


def test_reconcile_requeues_a_done_entry_that_fails_validation(ledger: RunLedger) -> None:
    """The reconcile half `setgen/run.py`'s own `plan_run` does not have: a ledger row saying
    'done' must not be trusted blindly if the real shape it describes is now invalid."""
    ledger.mark_done("a", {"requiredField": "present"})
    ledger.mark_done("b", {"requiredField": "present"})

    def is_valid(subject_id: str, entry: dict) -> bool:
        return "requiredField" in entry

    # Simulate an out-of-band corruption: overwrite "a"'s ledger entry directly (bypassing mark_done)
    # with a shape that no longer has the required field.
    done = ledger.read_done()
    done["a"] = {"somethingElse": True}
    ledger.write_done(done)

    needing_work = ledger.plan(["a", "b"], is_valid=is_valid)
    assert needing_work == ["a"]


def test_overwrite_by_id_touches_only_named_ids(ledger: RunLedger) -> None:
    forced = ledger.force(["x", "y"], scope="ids")
    assert forced == ["x", "y"]


def test_overwrite_all_requires_the_literal_all(ledger: RunLedger) -> None:
    assert ledger.force(["x", "y", "z"], scope="all") == ["x", "y", "z"]
    with pytest.raises(ValueError):
        ledger.force(["x"], scope="")
    with pytest.raises(ValueError):
        ledger.force(["x"], scope="everything")


def test_write_done_is_atomic_and_leaves_no_tmp_file_on_success(ledger: RunLedger) -> None:
    ledger.write_done({"a": {"v": 1}})
    assert ledger.path.exists()
    tmp_files = list(ledger.path.parent.glob("*.tmp"))
    assert tmp_files == []


def test_write_done_is_sort_keys_deterministic_across_two_writes(ledger: RunLedger) -> None:
    """The determinism gap found on adversarial review: setgen/run.py's own ledger writer has no
    sort_keys=True, so two writes of an unchanged dict are not guaranteed byte-identical there. This
    ledger must not inherit that gap."""
    done = {"b": {"y": 2}, "a": {"x": 1}}
    ledger.write_done(done)
    first_bytes = ledger.path.read_bytes()
    # Rewrite with keys in a DIFFERENT insertion order but the same content -- sort_keys must make
    # the serialized bytes identical regardless.
    ledger.write_done({"a": {"x": 1}, "b": {"y": 2}})
    second_bytes = ledger.path.read_bytes()
    assert first_bytes == second_bytes


def test_read_done_on_a_missing_ledger_returns_empty(tmp_path: Path) -> None:
    ledger = RunLedger(path=tmp_path / "does-not-exist.json")
    assert ledger.read_done() == {}
