#!/usr/bin/env python3
"""Wave R2 — give every acquirable thing a way to be acquired.

    python tools/seed_graph/bind_acquisition.py           # dry run
    python tools/seed_graph/bind_acquisition.py --apply

Closes four of the five remaining reachability gaps:

* 144 uniques with no acquisition path, and no `acquisition` field either
* 70 charms and 60 consumables that no table yields
* 30 of 40 gems named by nothing
* 7 role/frame slots that have base types and no equipment entry

Rule-driven, not judgement-driven, which is why it is a script. `ssot-uniques.md` §4.5 already
fixes the channel policy and `entry-shapes.md` §9 already fixes the entry shape; the only thing
missing was rows. Where §4.5 left a choice — which band uses which channel — the mapping is stated
once here and mirrored into entry-shapes.md §9 rather than being decided per row.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from seedgraph import Acquisition, Corpus  # noqa: E402

SEED_ROOT = Path("data/seed/items")

# ssot-uniques.md §4.5 plus its rung table. `drop` at ordinal >= 90 is the refusal
# `UniqueUnreachable`, so band 90 is deterministic: I1 requires the top rung to have a source that
# does not depend on luck.
BAND_POLICY = {
    "30": ("drop", 1, "occasional"),
    "50": ("source-locked", 2, "seldom"),
    "70": ("source-locked", 2, "seldom"),
    "90": ("deterministic", 4, "exceptional"),
}

# Where the non-unique kinds land. Consumables are common and general; charms are a reward.
CHARM_SLOT, CONSUMABLE_SLOT, GEM_SLOT, EQUIPMENT_SLOT = 3, 1, 1, 1


def band_of(entry) -> str | None:
    """`uniques/{theme}/{ordinal}` — the band is the allocation's own key."""
    parts = entry.partition.split("/")
    return parts[-1] if len(parts) >= 3 else None


def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def main(argv: list[str]) -> int:
    apply = "--apply" in argv
    root = Path.cwd()
    for directory in (root, *root.parents):
        if (directory / SEED_ROOT).is_dir():
            root = directory
            break
    else:
        print("could not locate data/seed/items", file=sys.stderr)
        return 2

    seed = root / SEED_ROOT
    corpus = Corpus.load(seed)
    acq = Acquisition.build(corpus)

    # ---- 1. every unique declares an acquisition, per its rung band -------------------------
    added_acquisition = 0
    unique_by_slot: dict[int, list[tuple[str, str]]] = {}
    for path in sorted((seed / "uniques").glob("*.json")):
        doc = load(path)
        changed = False
        for entry in doc.get("entries") or []:
            band = (doc.get("_meta", {}).get("partition", "")).split("/")[-1]
            policy = BAND_POLICY.get(band)
            if not policy:
                print(f"  ! {entry.get('id')}: unknown rung band '{band}'", file=sys.stderr)
                continue
            channel, slot, drop_band = policy
            if entry.get("acquisition") != channel:
                entry["acquisition"] = channel
                added_acquisition += 1
                changed = True
            unique_by_slot.setdefault(slot, []).append((entry["id"], drop_band))
        if changed and apply:
            path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")

    # ---- 2. what still needs a grant ----------------------------------------------------------
    def unreached(kind: str) -> list[str]:
        return [e.id for e in corpus.of(kind) if not acq.reaches(e)]

    # Built by appending, never as a dict literal: CONSUMABLE_SLOT and GEM_SLOT are both 1, and a
    # literal with a repeated key silently keeps only the last — which quietly dropped all 60
    # consumables on the first run of this script.
    pending: dict[int, list[tuple[str, str, str]]] = {}
    for slot, kind, ids, drop_band in (
        (CHARM_SLOT, "charm", unreached("charm"), "seldom"),
        (CONSUMABLE_SLOT, "consumable", unreached("consumable"), "frequent"),
        (GEM_SLOT, "insert", unreached("gem"), "seldom"),
    ):
        pending.setdefault(slot, []).extend((kind, i, drop_band) for i in ids)
    for slot, rows in unique_by_slot.items():
        pending.setdefault(slot, []).extend(("unique", uid, band) for uid, band in rows)

    # 7 role/frame slots with base types and no equipment entry
    have_slots = {(b.get("role"), b.get("frame")) for b in corpus.of("base-type")
                  if b.get("role") and b.get("frame")}
    missing_slots = sorted(have_slots - acq.equipment_slots)
    pending.setdefault(EQUIPMENT_SLOT, []).extend(
        ("equipment", f"{role}|{frame}", "occasional") for role, frame in missing_slots)

    # ---- 3. write one new group per kind into the first table of each slot ---------------------
    summary: list[str] = []
    for slot in sorted(pending):
        rows = pending[slot]
        if not rows:
            continue
        path = seed / "drop-tables" / f"d{slot}.json"
        if not path.exists():
            print(f"  ! no drop table d{slot}.json for {len(rows)} entries", file=sys.stderr)
            continue
        doc = load(path)
        table = (doc.get("entries") or [None])[0]
        if table is None:
            print(f"  ! d{slot}.json has no tables", file=sys.stderr)
            continue

        by_kind: dict[str, list[dict]] = {}
        for kind, ref, drop_band in rows:
            if kind == "equipment":
                role, frame = ref.split("|")
                row = {"entryKind": "equipment", "role": role, "frame": frame,
                       "dropBand": drop_band}
            else:
                row = {"entryKind": kind, "ref": ref, "dropBand": drop_band}
            by_kind.setdefault(kind, []).append(row)

        for kind, entries in sorted(by_kind.items()):
            group_key = f"r2-{kind}"
            groups = table.setdefault("groups", [])
            existing = next((g for g in groups if g.get("groupKey") == group_key), None)
            if existing is None:
                groups.append({"groupKey": group_key, "entries": entries})
            else:
                existing["entries"] = entries   # idempotent re-run
            summary.append(f"  d{slot}  {group_key:<18} {len(entries):>4} entries")

        if apply:
            path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")

    print(f"acquisition fields set on {added_acquisition} uniques")
    print()
    for line in summary:
        print(line)
    print()
    if not apply:
        print("dry run — pass --apply to write")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
