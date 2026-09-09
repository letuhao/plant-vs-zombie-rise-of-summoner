"""Regenerate the demon theme registry (D4 artifact, kept consistent with motifs).

**Why this exists.** `themes.v1.json` embeds each demon's motifs. G1 (`motif-prose-filter`) changed
the motif derivation, and the old builder only read the legacy 84-row demon slice while the
development dump contains the complete 904-row roster. This entrypoint reads the full almanac dump,
preserves published snapshots, and deterministically appends themes for newly visible species.

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

from .motifs import own_motifs, prose_of
from .themes import DemonThemeInput, build_theme_registry

__all__ = ["regenerate", "build_inputs", "DEMONS_ROOT"]

DEMONS_ROOT = Path(__file__).resolve().parents[5] / "data" / "seed" / "demons"


def build_inputs(root: Path = DEMONS_ROOT) -> "list[DemonThemeInput]":
    """Build one theme input per species in the committed almanac dump.

    The original implementation read ``demon/*.json``, a legacy 84-row authored slice.  That
    made ``theme-refresh`` a no-op over the real 904-row species roster.  The dump is the canonical
    development source for the complete roster; existing motif assignments remain authoritative
    snapshots, while new species receive the same deterministic prose/name motif derivation used by
    ``generate_motifs``.  No model call is made here, and no published theme is rewritten by
    ``build_theme_registry``'s append-only merge.
    """
    gen = root / "_generated"
    motifs = json.loads((gen / "motif-assignments.json").read_text(encoding="utf-8"))

    # Rarity is a snapshot carried by the anchor seed.  Keep the legacy 4-rung values for already
    # published themes through the append-only merge; new rows use the current anchor's rung.
    rarity_by_species: "dict[str, str]" = {}
    for path in sorted((root / "species").glob("*/*.json")):
        if path.name.startswith("_"):
            continue
        raw = json.loads(path.read_text(encoding="utf-8"))
        rows = raw if isinstance(raw, list) else raw.get("entries", [])
        for row in rows:
            sid = str(row.get("speciesId") or "").lower()
            if sid:
                rarity_by_species[sid] = str(row.get("rarity") or "almanac")

    almanac_rows: "list[dict]" = []
    almanac_dir = root / "_dump" / "almanac"
    for path in sorted(almanac_dir.glob("*.json")):
        raw = json.loads(path.read_text(encoding="utf-8"))
        if isinstance(raw, list):
            almanac_rows.extend(row for row in raw if isinstance(row, dict))

    # Keep a test/fixture-friendly fallback for a private root that predates corpus-dump.  The
    # production tree always has the almanac files, so this branch cannot hide their absence there.
    if not almanac_rows:
        for path in sorted((root / "demon").rglob("*.json")):
            doc = json.loads(path.read_text(encoding="utf-8"))
            partition = (doc.get("_meta") or {}).get("partition", "")
            fallback_rarity = partition.split("/")[-1] if "/" in partition else "common"
            for row in doc.get("entries", []):
                almanac_rows.append({
                    "typeName": row.get("id", ""), "displayName": row.get("name", ""),
                    "flavorInfo": row.get("flavorInfo"),
                    "flavorIntroduce": row.get("flavorIntroduce"),
                    "_fallbackRarity": fallback_rarity,
                })

    out: "list[DemonThemeInput]" = []
    seen: set[str] = set()
    for row in sorted(almanac_rows, key=lambda item: str(item.get("typeName") or "").lower()):
        sid = str(row.get("typeName") or "").strip().lower()
        if not sid or sid in seen:
            continue
        seen.add(sid)
        rec = motifs.get(sid)
        if rec is not None:
            row_motifs = tuple(rec.get("motifs") or [])
            anti_motifs = tuple(rec.get("antiMotifs") or [])
            basis = rec.get("basis", "text")
        else:
            prose = prose_of(
                flavor_info=row.get("flavorInfo"),
                flavor_introduce=row.get("flavorIntroduce"),
            )
            row_motifs, basis = own_motifs(
                flavor_text=prose or None,
                name=str(row.get("displayName") or sid),
            )
            anti_motifs = ()
        out.append(DemonThemeInput(
            species_id=sid,
            display_name=str(row.get("displayName") or sid),
            rarity=rarity_by_species.get(sid, str(row.get("_fallbackRarity") or "almanac")),
            motifs=tuple(row_motifs),
            anti_motifs=anti_motifs,
            basis=basis,
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
            lore=str(v.get("flavor") or v.get("lore") or ""),
        ) for k, v in raw.items()}

    inputs = build_inputs(root)
    registry = build_theme_registry(inputs, existing_registry=existing)

    if write:
        payload = {
            "schemaVersion": 1,
            "registryVersion": 1,
            "themes": {
                k: {
                    "speciesId": t.species_id, "displayName": t.display_name, "rarity": t.rarity,
                    "motifs": list(t.motifs), "antiMotifs": list(t.anti_motifs),
                    "expression": dict(t.expression), "basis": t.basis, "retired": t.retired,
                    **({"flavor": t.lore} if t.lore else {}),
                }
                for k, t in sorted(registry.items())
            },
        }
        reg.parent.mkdir(parents=True, exist_ok=True)
        reg.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

        # New full-roster themes may introduce motifs that were absent from the old 84-row
        # vocabulary.  Keep the motif registry append-only and make every published theme motif
        # resolvable by consumers that validate against ``motifs.v1.json``.
        motifs_path = root / "_registry" / "motifs.v1.json"
        old_motifs: list[str] = []
        if motifs_path.exists():
            old_doc = json.loads(motifs_path.read_text(encoding="utf-8"))
            old_motifs = [str(m) for m in old_doc.get("motifs", []) if isinstance(m, str)]
        vocabulary = list(old_motifs)
        for theme in registry.values():
            for motif in theme.motifs:
                if motif not in vocabulary:
                    vocabulary.append(motif)
        motifs_path.write_text(
            json.dumps({"schemaVersion": 1, "registryVersion": 1, "motifs": vocabulary},
                       ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8")

    return {
        "themes": len(registry),
        "inputs": len(inputs),
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
