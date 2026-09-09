"""Explicit repair for persisted duplicate set/charm display names.

Generation rejects a new collision before it writes.  This module handles the older corpus rows
that predate that guard: it changes only a losing row's surface name, derived name key and optional
flavour.  Set members, charm class, atoms and every other gameplay field stay byte-for-byte intact.
"""
from __future__ import annotations

import json
import os
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .seedfile import ITEM_SEED_ROOT, derive_name_key


@dataclass(frozen=True)
class NameRepair:
    entry_id: str
    kind: str
    path: Path
    old_name: str
    keeper_id: str


def plan(items_root: Path | None = None) -> tuple[NameRepair, ...]:
    """Keep the lexically first id in each collision and return every later row to rename."""
    root = Path(items_root or ITEM_SEED_ROOT)
    rows: list[tuple[str, str, Path, str]] = []
    for directory_name, kind in (("sets", "set"), ("charms", "charm")):
        for path in sorted((root / directory_name).glob("*.json")):
            try:
                document = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError):
                continue
            if document.get("kind") != kind:
                continue
            for row in document.get("entries") or ():
                if not isinstance(row, dict):
                    continue
                entry_id, name = row.get("id"), row.get("name")
                if isinstance(entry_id, str) and isinstance(name, str) and name.strip():
                    rows.append((entry_id, kind, path, name.strip()))
    by_name: dict[str, list[tuple[str, str, Path, str]]] = {}
    for row in rows:
        by_name.setdefault(row[3].casefold(), []).append(row)
    repairs: list[NameRepair] = []
    for group in by_name.values():
        ordered = sorted(group, key=lambda row: row[0])
        if len(ordered) < 2:
            continue
        keeper = ordered[0][0]
        repairs.extend(NameRepair(entry_id=entry_id, kind=kind, path=path,
                                  old_name=name, keeper_id=keeper)
                       for entry_id, kind, path, name in ordered[1:])
    return tuple(sorted(repairs, key=lambda repair: repair.entry_id))


def schema() -> dict[str, Any]:
    return {
        "type": "object",
        "additionalProperties": False,
        "required": ["name"],
        "properties": {
            "name": {"type": "string"},
            "flavor": {"type": "string"},
        },
    }


def brief(repair: NameRepair) -> str:
    return f"""Rename one {repair.kind} display name without changing its gameplay data.

Entry id: `{repair.entry_id}`
Current duplicate name: `{repair.old_name}`
The name is already kept by `{repair.keeper_id}`.

Return JSON only: a specific, distinct replacement `name`, and optionally a replacement `flavor`.
Do not mention ids, costs, tiers, mechanics, or numbers. The new name must not be a close reuse of
`{repair.old_name}`."""


def validate_answers(repairs: tuple[NameRepair, ...], answers: dict[str, dict], *,
                     items_root: Path | None = None) -> dict[str, dict]:
    expected = {repair.entry_id for repair in repairs}
    if set(answers) != expected:
        raise ValueError(f"name-repair answers must name exactly {sorted(expected)}")
    # The keeper retains every old duplicate name, so none of those names may be recycled by a
    # losing row (even when the answer happens to name its own former surface text).
    current_names = _current_names(Path(items_root or ITEM_SEED_ROOT))
    clean: dict[str, dict] = {}
    for repair in repairs:
        answer = answers[repair.entry_id]
        name = answer.get("name") if isinstance(answer, dict) else None
        flavor = answer.get("flavor") if isinstance(answer, dict) else None
        if not isinstance(name, str) or not name.strip():
            raise ValueError(f"{repair.entry_id}: replacement name is empty")
        normalized = name.strip().casefold()
        if normalized in current_names:
            raise ValueError(f"{repair.entry_id}: replacement name {name!r} already exists")
        if flavor is not None and not isinstance(flavor, str):
            raise ValueError(f"{repair.entry_id}: flavor must be a string when present")
        current_names.add(normalized)
        clean[repair.entry_id] = {"name": name.strip(), **({"flavor": flavor} if flavor is not None else {})}
    return clean


def apply(repairs: tuple[NameRepair, ...], answers: dict[str, dict], *, write: bool,
          items_root: Path | None = None) -> tuple[Path, ...]:
    """Apply validated answers, preserving every field unrelated to player-facing identity."""
    clean = validate_answers(repairs, answers, items_root=items_root)
    by_path: dict[Path, list[NameRepair]] = {}
    for repair in repairs:
        by_path.setdefault(repair.path, []).append(repair)
    changed: list[Path] = []
    for path, path_repairs in by_path.items():
        document = json.loads(path.read_text(encoding="utf-8"))
        for row in document.get("entries") or ():
            if not isinstance(row, dict) or row.get("id") not in clean:
                continue
            answer = clean[row["id"]]
            row["name"] = answer["name"]
            row["nameKey"] = derive_name_key(document["kind"], answer["name"])
            if "flavor" in answer:
                row["flavor"] = answer["flavor"]
        changed.append(path)
        if write:
            _atomic_json(path, document)
    return tuple(changed)


def _current_names(root: Path) -> set[str]:
    return {
        name.casefold() for directory_name in ("sets", "charms")
        for path in (root / directory_name).glob("*.json")
        for document in [_load(path)] if document is not None
        for row in document.get("entries") or () if isinstance(row, dict)
        for name in [row.get("name")] if isinstance(name, str) and name.strip()
    }


def _load(path: Path) -> dict | None:
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None


def _atomic_json(path: Path, document: dict) -> None:
    handle, temporary = tempfile.mkstemp(dir=str(path.parent), suffix=".tmp")
    try:
        with os.fdopen(handle, "w", encoding="utf-8") as output:
            output.write(json.dumps(document, ensure_ascii=False, indent=2) + "\n")
        os.replace(temporary, path)
    except BaseException:
        Path(temporary).unlink(missing_ok=True)
        raise
