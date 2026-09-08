"""seedsmith.adapters.demons.generate_motifs — regenerate the motif artifacts (G1.3).

**Why this module exists:** before it, the committed `motifs.v1.json` and `motif-assignments.json`
were produced by a scratch script that lived nowhere in the repo. The artifacts existed and nothing
could reproduce them — the exact opposite of this program's determinism claim. Regeneration is now
a real, reviewable entrypoint.

No model call. Everything here is a pure function of committed inputs:
    corpus entries + family-assignments + family-candidates -> motifs
"""
from __future__ import annotations

import json
from pathlib import Path

from .motifs import DemonMotifInput, FamilyMembership, derive_motifs, prose_of

__all__ = ["regenerate", "build_inputs", "DEMONS_ROOT"]

DEMONS_ROOT = Path(__file__).resolve().parents[5] / "data" / "seed" / "demons"


def _load_entries(root: Path) -> "dict[str, dict]":
    entries: "dict[str, dict]" = {}
    for path in sorted((root / "demon").rglob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        for row in doc.get("entries", []):
            entries[row["id"]] = row
    return entries


def build_inputs(root: Path = DEMONS_ROOT) -> "list[DemonMotifInput]":
    """Assemble one `DemonMotifInput` per demon from committed artifacts only.

    `basis` per (demon, family) comes from `family-candidates.json` — the ORIGINAL extraction
    record — not from the consolidated assignment, because the assignment has no basis and
    re-deriving one would invent a fact (spec-motif-derive.md §2.4)."""
    entries = _load_entries(root)
    gen = root / "_generated"
    assignments = json.loads((gen / "family-assignments.json").read_text(encoding="utf-8"))
    candidates = json.loads((gen / "family-candidates.json").read_text(encoding="utf-8"))

    # speciesId -> weakest basis observed among its own candidates; "text" beats "name".
    basis_by_species: "dict[str, str]" = {}
    for sid, rows in candidates.items():
        bases = {r.get("basis") for r in rows if r.get("basis")}
        basis_by_species[sid] = "text" if "text" in bases else ("name" if "name" in bases else "text")

    out: "list[DemonMotifInput]" = []
    for sid in sorted(entries):
        e = entries[sid]
        fams = tuple(
            FamilyMembership(family_id=fid, basis=basis_by_species.get(sid, "text"))
            for fid in assignments.get(sid, [])
        )
        text = prose_of(flavor_info=e.get("flavorInfo"), flavor_introduce=e.get("flavorIntroduce"))
        out.append(DemonMotifInput(
            species_id=sid, name=e.get("name") or "", flavor_text=text or None, families=fams,
        ))
    return out


def regenerate(root: Path = DEMONS_ROOT, *, write: bool = True) -> dict:
    """Derive motifs and (optionally) write both artifacts. Returns a summary for the caller to
    report — counts are a RESULT, including a rise in `name`/`blocked`, which the prose filter is
    expected to cause (spec-motif-prose-filter.md §2.3)."""
    derived = derive_motifs(build_inputs(root))

    assignments = {
        sid: {
            "antiMotifs": d.anti_motifs,
            "basis": d.basis,
            "motifs": d.motifs,
            "tautological": d.tautological,
        }
        for sid, d in sorted(derived.items())
    }
    vocabulary: "list[str]" = []
    for sid in sorted(derived):
        for m in derived[sid].motifs:
            if m not in vocabulary:
                vocabulary.append(m)

    if write:
        gen, reg = root / "_generated", root / "_registry"
        gen.mkdir(parents=True, exist_ok=True)
        reg.mkdir(parents=True, exist_ok=True)
        (gen / "motif-assignments.json").write_text(
            json.dumps(assignments, ensure_ascii=False, indent=2, sort_keys=True) + "\n",
            encoding="utf-8")
        (reg / "motifs.v1.json").write_text(
            json.dumps({"schemaVersion": 1, "registryVersion": 1, "motifs": vocabulary},
                       ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8")

    by_basis: "dict[str, int]" = {}
    for d in derived.values():
        by_basis[d.basis] = by_basis.get(d.basis, 0) + 1
    return {
        "demons": len(derived),
        "vocabularySize": len(vocabulary),
        "byBasis": by_basis,
        "tautological": sum(1 for d in derived.values() if d.tautological),
    }


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    print(json.dumps(regenerate(), ensure_ascii=False, indent=2))
