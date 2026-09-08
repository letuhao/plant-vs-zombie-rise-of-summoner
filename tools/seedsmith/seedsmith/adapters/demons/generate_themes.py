"""Regenerate the demon theme registry (D4 artifact, kept consistent with motifs).

**Why this exists.** `themes.v1.json` embeds each demon's motifs. G1 (`motif-prose-filter`) changed
every demon's motifs, so all 84 themes went stale — they still carried the pre-filter stat
vocabulary (`一类` = "armour-class one", `三线`, `伤害`) while `motif-assignments.json` had moved on.
Nothing detected that, because no metric compares the two.

⚠️ **`--rebuild` discards published themes and re-derives them, which append-only normally
forbids** (spec-demon-themes.md §2.4a: a published theme is a snapshot, never re-derived). It is
permitted here for the same reason G1.3's motif regeneration was: **nothing is bound to these keys
yet.** Measured — the items corpus references only legacy `theme.*` keys (38 entries), and zero
`demon.*` keys. That window closes the moment an item is authored against a demon theme; after
that, a motif correction needs `themes.v2.json` plus a migration.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from .themes import DemonThemeInput, build_theme_registry

__all__ = ["regenerate", "build_inputs", "DEMONS_ROOT"]

DEMONS_ROOT = Path(__file__).resolve().parents[5] / "data" / "seed" / "demons"


def build_inputs(root: Path = DEMONS_ROOT) -> "list[DemonThemeInput]":
    """One input per demon, from committed artifacts only."""
    gen, reg = root / "_generated", root / "_registry"
    motifs = json.loads((gen / "motif-assignments.json").read_text(encoding="utf-8"))

    names: "dict[str, str]" = {}
    for path in sorted((root / "demon").rglob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        partition = (doc.get("_meta") or {}).get("partition", "")
        rarity = partition.split("/")[-1] if "/" in partition else "common"
        for row in doc.get("entries", []):
            names[row["id"]] = (row.get("name") or row["id"], rarity)

    out: "list[DemonThemeInput]" = []
    for sid in sorted(motifs):
        rec = motifs[sid]
        display, rarity = names.get(sid, (sid, "common"))
        out.append(DemonThemeInput(
            species_id=sid,
            display_name=display,
            rarity=rarity,
            motifs=tuple(rec.get("motifs") or []),
            anti_motifs=tuple(rec.get("antiMotifs") or []),
            basis=rec.get("basis", "text"),
        ))
    return out


def regenerate(root: Path = DEMONS_ROOT, *, rebuild: bool = False, write: bool = True) -> dict:
    """Derive the theme registry. Without `rebuild`, append-only semantics apply and an already
    published theme is left exactly as it was."""
    reg = root / "_registry" / "themes.v1.json"
    existing = None
    if not rebuild and reg.exists():
        raw = json.loads(reg.read_text(encoding="utf-8")).get("themes", {})
        from .themes import PublishedTheme
        existing = {k: PublishedTheme(
            theme_key=k, species_id=v["speciesId"], display_name=v["displayName"],
            rarity=v["rarity"], motifs=tuple(v["motifs"]), anti_motifs=tuple(v["antiMotifs"]),
            expression=v["expression"], basis=v["basis"], retired=v.get("retired", False),
        ) for k, v in raw.items()}

    registry = build_theme_registry(build_inputs(root), existing_registry=existing)

    if write:
        payload = {
            "schemaVersion": 1,
            "registryVersion": 1,
            "themes": {
                k: {
                    "speciesId": t.species_id, "displayName": t.display_name, "rarity": t.rarity,
                    "motifs": list(t.motifs), "antiMotifs": list(t.anti_motifs),
                    "expression": dict(t.expression), "basis": t.basis, "retired": t.retired,
                }
                for k, t in sorted(registry.items())
            },
        }
        reg.parent.mkdir(parents=True, exist_ok=True)
        reg.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    return {
        "themes": len(registry),
        "retired": sum(1 for t in registry.values() if t.retired),
        "rebuilt": rebuild,
    }


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Regenerate the demon theme registry.")
    ap.add_argument("--rebuild", action="store_true",
                    help="discard published themes and re-derive (reviewed correction only)")
    args = ap.parse_args(argv)
    print(json.dumps(regenerate(rebuild=args.rebuild), ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":  # pragma: no cover - dev entrypoint
    raise SystemExit(main())
