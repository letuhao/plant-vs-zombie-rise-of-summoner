"""Explicit repair for persisted duplicate item display names, across every colliding kind.

Generation rejects a new collision before it writes. This module handles the older corpus rows that
predate that guard: it changes only a losing row's surface `name`, its derived `nameKey`, and
optionally its `flavor`. Class, atoms, costs, tiers, members and every other gameplay field stay
byte-for-byte intact.

⛔ **Why the groups come from the validator, not from this module.** The collision rule is
`naming.v1.json`'s normalization (lowercase, tokenize, whole-token resolution, drop connectives,
sort, compare) implemented in `tools/ItemSeedValidator/Naming/NameNormalizer.cs`. Reimplementing
that in Python would fork the authority: a repair computed against a slightly different algorithm
could leave the validator still reporting collisions, or rename rows that never collided. So the
plan consumes `dotnet run --project tools/ItemSeedValidator -- <root> --collision-groups`, which
prints the authoritative groups using that exact normalizer.

**Scope (2026-09-12).** Originally `sets` + `charms` only, matched on the exact casefolded string —
which found 0 rows, because the real rule is token-set based ("Rolling Grave Nut" collides with
"Rolling Grave-Nut"). It now covers every kind the validator checks and uses its groups.
"""
from __future__ import annotations

import json
import os
import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .seedfile import ITEM_SEED_ROOT, derive_name_key, NameKeyUnsluggable


@dataclass(frozen=True)
class NameRepair:
    entry_id: str
    kind: str
    path: Path
    old_name: str
    keeper_id: str


def collision_groups(items_root: Path | None = None, *,
                     validator_project: "Path | None" = None) -> "list[dict]":
    """The validator's authoritative collision groups. Raises RuntimeError if the tool cannot run,
    because a repair planned against a guessed grouping is worse than no repair."""
    root = Path(items_root or ITEM_SEED_ROOT)
    project = validator_project or _default_validator_project(root)
    if project is None or not project.exists():
        raise RuntimeError(f"ItemSeedValidator project not found near {root}")
    proc = subprocess.run(
        ["dotnet", "run", "--project", str(project), "--", str(root), "--collision-groups"],
        capture_output=True, text=True, cwd=str(root.parents[2]),
        stdin=subprocess.DEVNULL,  # never inherit the caller's stdin (MCP pipe)
    )
    if proc.returncode != 0 or not proc.stdout.strip():
        raise RuntimeError(f"collision-groups failed ({proc.returncode}): {proc.stderr.strip()[:400]}")
    return json.loads(proc.stdout).get("groups", [])


def _default_validator_project(root: Path) -> "Path | None":
    for parent in [root, *root.parents]:
        candidate = parent / "tools" / "ItemSeedValidator" / "ItemSeedValidator.csproj"
        if candidate.exists():
            return candidate
    return None


def plan(items_root: Path | None = None, *, groups: "list[dict] | None" = None) -> "tuple[NameRepair, ...]":
    """Keep the lexically first id in each authoritative collision group; return every later row."""
    root = Path(items_root or ITEM_SEED_ROOT)
    groups = groups if groups is not None else collision_groups(items_root=root)

    # id -> (path, kind, name); built once so each losing row can be located.
    index: "dict[str, tuple[Path, str, str]]" = {}
    for path in sorted(root.glob("**/*.json")):
        if path.name.startswith("_") or "_exemplars" in path.parts or "_runs" in path.parts:
            continue
        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        kind = document.get("kind")
        for row in document.get("entries") or ():
            if not isinstance(row, dict):
                continue
            entry_id, name = row.get("id"), row.get("name")
            if isinstance(entry_id, str) and isinstance(name, str) and name.strip():
                index[entry_id] = (path, str(kind or ""), name.strip())

    repairs: "list[NameRepair]" = []
    for group in groups:
        members = sorted(group.get("members") or [], key=lambda m: m.get("id") or "")
        if len(members) < 2:
            continue
        keeper = members[0]["id"]
        for member in members[1:]:
            entry_id = member.get("id")
            located = index.get(entry_id)
            if located is None:
                continue
            path, kind, name = located
            repairs.append(NameRepair(entry_id=entry_id, kind=kind, path=path,
                                      old_name=name, keeper_id=keeper))
    return tuple(sorted(repairs, key=lambda r: r.entry_id))


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
The new name must be a legal {repair.kind} name (a thing a player picks up, not a sentence or an
id), must not reuse the old name's idea in a different word order, and must not mention ids, costs,
tiers, mechanics, or numbers."""


def validate_answers(repairs: "tuple[NameRepair, ...]", answers: dict[str, dict], *,
                     items_root: Path | None = None) -> "dict[str, dict]":
    expected = {repair.entry_id for repair in repairs}
    if set(answers) != expected:
        missing = sorted(expected - set(answers))
        extra = sorted(set(answers) - expected)
        raise ValueError(f"name-repair answers must name exactly the {len(expected)} losing rows "
                         f"(missing {missing[:5]}, unexpected {extra[:5]})")
    current_names = _current_names(Path(items_root or ITEM_SEED_ROOT))
    clean: "dict[str, dict]" = {}
    for repair in repairs:
        answer = answers[repair.entry_id]
        name = answer.get("name") if isinstance(answer, dict) else None
        flavor = answer.get("flavor") if isinstance(answer, dict) else None
        if not isinstance(name, str) or not name.strip():
            raise ValueError(f"{repair.entry_id}: replacement name is empty")
        if flavor is not None and not isinstance(flavor, str):
            raise ValueError(f"{repair.entry_id}: flavor must be a string when present")
        normalized = name.strip().casefold()
        if normalized in current_names:
            raise ValueError(f"{repair.entry_id}: replacement name {name!r} already exists")
        current_names.add(normalized)
        # A name the grammar cannot slug cannot mint a legal nameKey; catch it here rather than at
        # the write, where a partial apply would be worse.
        try:
            derive_name_key(repair.kind, name.strip())
        except NameKeyUnsluggable as exc:
            raise ValueError(f"{repair.entry_id}: {exc}") from exc
        clean[repair.entry_id] = {"name": name.strip(),
                                  **({"flavor": flavor} if flavor is not None else {})}
    return clean


def apply(repairs: "tuple[NameRepair, ...]", answers: dict[str, dict], *, write: bool,
          items_root: Path | None = None) -> "tuple[Path, ...]":
    """Apply validated answers, preserving every field unrelated to player-facing identity."""
    clean = validate_answers(repairs, answers, items_root=items_root)
    by_path: "dict[Path, list[NameRepair]]" = {}
    for repair in repairs:
        by_path.setdefault(repair.path, []).append(repair)
    changed: "list[Path]" = []
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
    names: set[str] = set()
    for path in root.glob("**/*.json"):
        if path.name.startswith("_") or "_runs" in path.parts:
            continue
        document = _load(path)
        if document is None:
            continue
        for row in document.get("entries") or ():
            if isinstance(row, dict):
                name = row.get("name")
                if isinstance(name, str) and name.strip():
                    names.add(name.strip().casefold())
    return names


def _load(path: Path) -> "dict | None":
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
