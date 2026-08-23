"""Loading the item seed corpus, and the acquisition model built on top of it.

The C# validator next door answers *does this reference resolve?* — a question about one row and
the row it points at. This package answers a different one: *can a player actually get this, and
can they finish it?* A set whose members all resolve is still broken if no item is a member; a gem
that validates perfectly is still dead if no drop table yields it. Referential integrity and
reachability are separate properties and neither implies the other.

Nothing here opens a database or imports a third-party package. It reads JSON and builds a graph.
"""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

# Directories that hold something other than shippable content.
SKIP_DIRS = {"_registry", "_exemplars"}


@dataclass(frozen=True)
class Entry:
    """One authored row, plus enough provenance to point a human at it."""

    id: str
    kind: str
    partition: str
    path: str
    data: dict

    def get(self, key, default=None):
        return self.data.get(key, default)

    @property
    def name(self) -> str:
        return self.data.get("name", self.id)


@dataclass
class Corpus:
    entries: list[Entry] = field(default_factory=list)
    by_id: dict[str, Entry] = field(default_factory=dict)
    by_kind: dict[str, list[Entry]] = field(default_factory=dict)

    @classmethod
    def load(cls, root: Path) -> "Corpus":
        corpus = cls()
        for path in sorted(root.rglob("*.json")):
            if any(part in SKIP_DIRS for part in path.relative_to(root).parts):
                continue
            doc = json.loads(path.read_text(encoding="utf-8"))
            kind = doc.get("kind")
            if not kind:
                continue
            partition = (doc.get("_meta") or {}).get("partition", "(none)")
            rel = path.relative_to(root).as_posix()
            for row in doc.get("entries") or []:
                if not isinstance(row, dict) or "id" not in row:
                    continue
                entry = Entry(row["id"], kind, partition, rel, row)
                corpus.entries.append(entry)
                corpus.by_id[entry.id] = entry
                corpus.by_kind.setdefault(kind, []).append(entry)
        return corpus

    def of(self, kind: str) -> list[Entry]:
        return self.by_kind.get(kind, [])


@dataclass
class Acquisition:
    """Every way a player can come to hold a thing.

    Two shapes, and conflating them is how you get a false finding in both directions:

    * **Specific** — a drop table names `gem.g3-001`, a recipe outputs `item.plant-stem-a-004`.
      Exactly one row becomes reachable.
    * **Categorical** — a drop table says `{entryKind: equipment, role: girdle, frame: plant}`,
      which yields *any* base type in that role and frame. Six hundred base types are reachable
      this way and not one of them is named anywhere.

    Checking only for specific grants would report all 740 base types as unobtainable, which is
    both alarming and wrong. Checking only categorically would miss that 30 of 40 gems really are
    unreachable, because inserts are granted by id and nothing grants them by category.
    """

    specific: set[str] = field(default_factory=set)
    equipment_slots: set[tuple[str, str]] = field(default_factory=set)  # (role, frame)
    material_runtime_ids: set[str] = field(default_factory=set)

    @classmethod
    def build(cls, corpus: Corpus) -> "Acquisition":
        acq = cls()

        for table in corpus.of("drop-table"):
            for group in table.get("groups") or []:
                for row in group.get("entries") or []:
                    kind = row.get("entryKind")
                    ref = row.get("ref")
                    if kind == "equipment":
                        role, frame = row.get("role"), row.get("frame")
                        if role and frame:
                            acq.equipment_slots.add((role, frame))
                    elif kind == "material" and ref:
                        acq.material_runtime_ids.add(ref)
                        acq.specific.add(ref)
                    elif ref:
                        # insert / currency / anything later, all granted by id
                        acq.specific.add(ref)

        # A recipe's output is an acquisition path as surely as a drop is.
        for recipe in corpus.of("recipe"):
            ref = recipe.get("outputRef")
            if ref:
                acq.specific.add(ref)

        return acq

    def reaches(self, entry: Entry) -> bool:
        if entry.id in self.specific:
            return True
        # A material is granted by its runtime id, never by its tracking id — the same
        # tracking-vs-runtime split that has already bitten this corpus three times.
        runtime = entry.get("runtimeId")
        if runtime and runtime in self.specific:
            return True
        if entry.kind == "base-type":
            return (entry.get("role"), entry.get("frame")) in self.equipment_slots
        return False
