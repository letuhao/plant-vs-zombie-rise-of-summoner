"""Canonical dungeon anchor serialisation and emission (D1.11, spec-dungeon-seed-contract.md §6).
**One object per file** — unlike the demons anchor's per-family list — so `data/seed/dungeon/
<directory>/<id>.json` holds exactly one entry, plus one `_index.json` per directory
(`id -> filename`) for O(1) lookup without a full-tree load.
"""
from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Mapping, Sequence


def render_entry(entry: Mapping[str, Any]) -> bytes:
    """Sorted keys, two-space indent, `\\n` line ending, CJK unescaped, explicit nulls — the
    demons/anchor/emit.py `render_family_file` rules, applied per single object here."""
    text = json.dumps(dict(entry), indent=2, sort_keys=True, ensure_ascii=False)
    return (text + "\n").encode("utf-8")


def write_entry(directory: Path, entry_id: str, entry: Mapping[str, Any]) -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / f"{entry_id}.json"
    path.write_bytes(render_entry(entry))
    return path


def render_index(ids: "Sequence[str]") -> bytes:
    index = {entry_id: f"{entry_id}.json" for entry_id in sorted(ids)}
    text = json.dumps(index, indent=2, sort_keys=True, ensure_ascii=False)
    return (text + "\n").encode("utf-8")


def write_index(directory: Path, ids: "Sequence[str]") -> Path:
    directory.mkdir(parents=True, exist_ok=True)
    path = directory / "_index.json"
    path.write_bytes(render_index(ids))
    return path


def write_corpus(directory: Path, entries_by_id: "Mapping[str, Mapping[str, Any]]") -> "list[Path]":
    """Writes every entry plus the directory's `_index.json`. A second call over unchanged input
    writes byte-identical content (the rerun test hashes every file, not just checks existence)."""
    written = [write_entry(directory, entry_id, entry) for entry_id, entry in entries_by_id.items()]
    written.append(write_index(directory, list(entries_by_id)))
    return written
