#!/usr/bin/env python3
"""Wave R3 — give the `+X` enhancement line something to grant.

    python tools/seed_graph/bind_enhance_track.py            # dry run
    python tools/seed_graph/bind_enhance_track.py --apply

Ten milestone families exist and not one of 740 base types carried a track, so the entire
enhancement reward line was unreachable. `entry-shapes.md` §6 scoped `item_enhance_track` onto the
base-type kind — `(base_type_id, at_level, atom_id, seq)` per ssot-enhancement.md §5.4 — and it was
simply never authored.

Two rules from ssot-enhancement.md bound the shape:

* **§5.5** — at most one milestone per enhancement family per track. Every track below uses three
  distinct families, so this holds by construction.
* **§10.5** — "a base with no track is scalar-only, which is legal but flat." Not every base type
  needs one. This script gives one to every base type anyway, and that is the **one genuinely
  arbitrary choice here**: uniform coverage makes the feature exist everywhere and is trivially
  tuned down later by deleting tracks, whereas the reverse needs another pass over 740 rows.
  Flagged for the owner rather than presented as derived.

The family per role is not arbitrary. It follows the role's own job — a weapon slot escalates
damage, the torso slot escalates health, boots escalate evasion — using the `tags` the milestone
families already carry (offensive / defensive / utility).
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

SEED_ROOT = Path("data/seed/items")

# role -> the milestone families it escalates, in order, granted at +4 / +12 / +20.
# jewel-minor carries two steps, not three: core.v1.json gives it the smallest budget of any role
# (15 milli against armament-primary's 160) and ssot-uniques.md §3.7 bars uniques from it for the
# same reason — a duplicated small slot should not also carry the deepest track.
TRACKS = {
    "armament-primary":   ["atom.enhance-edge", "atom.enhance-savagery", "atom.enhance-keen"],
    "armament-secondary": ["atom.enhance-edge", "atom.enhance-keen", "atom.enhance-quicken"],
    "core-guard":         ["atom.enhance-vigor", "atom.enhance-fortify", "atom.enhance-hardy"],
    "head-guard":         ["atom.enhance-vigor", "atom.enhance-hardy", "atom.enhance-aegis"],
    "ward-array":         ["atom.enhance-aegis", "atom.enhance-fortify", "atom.enhance-vigor"],
    "mantle":             ["atom.enhance-evasion", "atom.enhance-vigor", "atom.enhance-aegis"],
    "manipulator":        ["atom.enhance-edge", "atom.enhance-quicken", "atom.enhance-keen"],
    "girdle":             ["atom.enhance-vigor", "atom.enhance-recovery", "atom.enhance-fortify"],
    "footing":            ["atom.enhance-evasion", "atom.enhance-quicken", "atom.enhance-hardy"],
    "sense":              ["atom.enhance-keen", "atom.enhance-quicken", "atom.enhance-evasion"],
    "jewel-major":        ["atom.enhance-savagery", "atom.enhance-fortify", "atom.enhance-keen"],
    "infusion":           ["atom.enhance-recovery", "atom.enhance-quicken", "atom.enhance-vigor"],
    "retinue":            ["atom.enhance-savagery", "atom.enhance-recovery", "atom.enhance-edge"],
    "standard":           ["atom.enhance-vigor", "atom.enhance-edge", "atom.enhance-aegis"],
    "jewel-minor-a":      ["atom.enhance-vigor", "atom.enhance-edge"],
    "jewel-minor-b":      ["atom.enhance-vigor", "atom.enhance-edge"],
}
LEVELS_3 = (4, 12, 20)
LEVELS_2 = (4, 12)


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

    known = set()
    milestones = seed / "enhancement-milestones" / "milestones.json"
    if milestones.exists():
        for row in json.loads(milestones.read_text(encoding="utf-8")).get("entries") or []:
            if row.get("runtimeFamily"):
                known.add(row["runtimeFamily"])

    unknown = {f for fams in TRACKS.values() for f in fams} - known
    if unknown:
        print(f"families in the map that no milestone authors: {sorted(unknown)}", file=sys.stderr)
        return 1

    tracked = untracked = 0
    per_role: dict[str, int] = {}
    for path in sorted((seed / "base-types").rglob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        if doc.get("kind") != "base-type":
            continue
        changed = False
        for entry in doc.get("entries") or []:
            role = entry.get("role")
            families = TRACKS.get(role)
            if not families:
                untracked += 1
                continue
            levels = LEVELS_3 if len(families) == 3 else LEVELS_2
            track = [{"atLevel": lvl, "family": fam} for lvl, fam in zip(levels, families)]
            if entry.get("enhanceTrack") != track:
                entry["enhanceTrack"] = track
                changed = True
            tracked += 1
            per_role[role] = per_role.get(role, 0) + 1
        if changed and apply:
            path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")

    for role in sorted(per_role):
        print(f"  {role:<22} {per_role[role]:>4} base types")
    print()
    print(f"{tracked} base types tracked, {untracked} left scalar-only")
    if not apply:
        print("dry run — pass --apply to write")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
