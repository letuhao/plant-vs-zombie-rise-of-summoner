#!/usr/bin/env python3
"""Wave R1 — bind every set member to a concrete base type.

    python tools/seed_graph/bind_set_members.py            # dry run, prints the plan
    python tools/seed_graph/bind_set_members.py --apply     # writes data/seed/items/sets/*.json

This is a script and not an authoring wave on purpose. Base types are not theme-keyed, so there is
no signal for a model to reason over: picking the piece is a deterministic match on role and frame
under three hard constraints. An LLM asked to do this would produce the same rows more slowly, less
reproducibly, and with a chance of inventing an id — which is exactly the failure that cost six
recipes their references earlier in this build.

What the rules are, and where each comes from:

* **Frame-neutral sets** (ssot-sets.md §3.7 line 222) — "Its members are frame-specific base types,
  at most one per (role, frame)." A theme whose `frameAffinity` is `both` therefore gets TWO rows
  per role, one per frame, and a hybrid may mix them. Thresholds count distinct member *roles*, not
  rows, so doubling the rows does not move the bar.
* **Flavour-locked sets** (§3.7 line 228) — a theme bound to one frame authors one row per role.
* **Hybrid role core** (§3.7 line 233) — member roles must exist on every frame. Already true of
  all 128 authored members; asserted here so a future edit cannot quietly break it.
* **No unique may be a set member** (§3.8, hard no) — both classes carry the same 1.5 AE premium,
  so a unique set piece is a piece paid for twice. Every base type a unique is built on is
  excluded.
* **Overlap between sets is allowed** (owner decision, 2026-08-23). Pieces are still spread so that
  no two sets of the same theme claim the same item in the same role, because six identical-looking
  sets is the failure this is meant to avoid rather than a rule it must obey.
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from seedgraph import Corpus  # noqa: E402
from seedgraph.checks import NON_HYBRID_ROLES  # noqa: E402

SEED_ROOT = Path("data/seed/items")
FRAMES_FOR = {"both": ("humanoid", "plant"), "plant": ("plant",), "humanoid": ("humanoid",)}


def theme_frames(registry_root: Path) -> dict[str, tuple[str, ...]]:
    themes = json.loads((registry_root / "themes.v1.json").read_text(encoding="utf-8"))
    rows = themes.get("themes") or themes.get("list") or []
    out = {}
    for row in rows:
        theme_id = row.get("id") or row.get("themeId")
        if theme_id:
            out[theme_id] = FRAMES_FOR.get(row.get("frameAffinity", "both"), ("humanoid", "plant"))
    return out


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
    frames_by_theme = theme_frames(seed / "_registry")

    # §3.8: a unique may not be a set member.
    claimed_by_unique = {u.get("baseType") for u in corpus.of("unique") if u.get("baseType")}
    if not claimed_by_unique:
        print("WARNING: no uniques on disk — run this AFTER the unique partitions land, or every\n"
              "         exclusion under ssot-sets.md §3.8 is silently vacuous.", file=sys.stderr)

    # (role, frame) -> sorted candidate base type ids, uniques removed
    candidates: dict[tuple[str, str], list[str]] = {}
    for base in corpus.of("base-type"):
        role, frame = base.get("role"), base.get("frame")
        if not role or not frame or base.id in claimed_by_unique:
            continue
        candidates.setdefault((role, frame), []).append(base.id)
    for key in candidates:
        candidates[key].sort()

    theme_index = {t: i for i, t in enumerate(sorted(frames_by_theme))}
    total_rows = 0
    problems: list[str] = []

    for path in sorted((seed / "sets").glob("*.json")):
        doc = json.loads(path.read_text(encoding="utf-8"))
        theme = path.stem
        frames = frames_by_theme.get(theme, ("humanoid", "plant"))
        file_rows = 0

        for set_index, entry in enumerate(doc.get("entries") or []):
            rebuilt = []
            for role_index, member in enumerate(entry.get("members") or []):
                role = member.get("role")
                if not role:
                    problems.append(f"{entry.get('id')}: member with no role")
                    continue
                if role in NON_HYBRID_ROLES:
                    problems.append(f"{entry.get('id')}: role '{role}' is outside the hybrid core")
                    continue
                for frame in frames:
                    pool = candidates.get((role, frame))
                    if not pool:
                        problems.append(
                            f"{entry.get('id')}: no base type for {role}/{frame} after excluding "
                            f"{len(claimed_by_unique)} unique-claimed items")
                        continue
                    # Spread so no two sets of a theme take the same piece in the same role.
                    offset = theme_index.get(theme, 0) * 6 + set_index
                    rebuilt.append({
                        "role": role,
                        "frame": frame,
                        "baseType": pool[offset % len(pool)],
                    })
                    file_rows += 1
            if rebuilt:
                entry["members"] = rebuilt

        total_rows += file_rows
        print(f"  {theme:<24} frames={'+'.join(frames):<16} {file_rows:>3} member rows")
        if apply:
            path.write_text(json.dumps(doc, indent=2) + "\n", encoding="utf-8")

    print()
    print(f"{total_rows} member rows across {len(list((seed / 'sets').glob('*.json')))} files")
    if problems:
        print(f"\n{len(problems)} problem(s):")
        for line in problems[:20]:
            print(f"  {line}")
        return 1
    if not apply:
        print("dry run — pass --apply to write")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
