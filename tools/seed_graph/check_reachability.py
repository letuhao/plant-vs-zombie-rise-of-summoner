#!/usr/bin/env python3
"""Reachability gate on the item seed corpus.

    python tools/seed_graph/check_reachability.py [seed root] [--notes-as-gaps]

Exits 0 when nothing ships unreachable, 1 when something does, 2 when the corpus cannot be read.

This is the companion to `tools/ItemSeedValidator`, not a replacement. That tool proves every
reference resolves. This one proves the resolved graph is *playable*: that a set can be completed,
that a gem can be found, that a recipe's inputs exist. Both were green on referential integrity
while thirty sets were uncompletable, which is the whole argument for having the second one.
"""

from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from seedgraph import Acquisition, Corpus, GAP, run_all  # noqa: E402

DEFAULT_SEED_ROOT = Path("data/seed/items")


def find_seed_root(explicit: str | None) -> Path | None:
    if explicit:
        path = Path(explicit).resolve()
        return path if path.is_dir() else None
    here = Path.cwd().resolve()
    for directory in (here, *here.parents):
        candidate = directory / DEFAULT_SEED_ROOT
        if candidate.is_dir():
            return candidate
    return None


def main(argv: list[str]) -> int:
    notes_as_gaps = "--notes-as-gaps" in argv
    positional = [a for a in argv if not a.startswith("--")]

    root = find_seed_root(positional[0] if positional else None)
    if root is None:
        print("could not locate data/seed/items; pass the seed root explicitly", file=sys.stderr)
        return 2

    try:
        corpus = Corpus.load(root)
    except (OSError, ValueError) as exc:
        print(f"could not read the corpus: {exc}", file=sys.stderr)
        return 2

    if not corpus.entries:
        print(f"no seed entries found under {root}", file=sys.stderr)
        return 2

    acquisition = Acquisition.build(corpus)
    findings = run_all(corpus, acquisition)

    gaps = [f for f in findings if f.severity == GAP]
    notes = [f for f in findings if f.severity != GAP]

    print()
    print("SEED REACHABILITY")
    print(f"  entries        {len(corpus.entries):>6}")
    print(f"  kinds          {len(corpus.by_kind):>6}")
    print(f"  gaps           {len(gaps):>6}")
    print(f"  notes          {len(notes):>6}")
    print()

    for label, rows in (("GAPS — ships unreachable", gaps), ("NOTES", notes)):
        if not rows:
            continue
        print(label)
        for finding in rows:
            print(f"  {finding.code:<26} {finding.subject}")
            print(f"      [{finding.partition}] {finding.message}")
        print()

    if gaps or (notes_as_gaps and notes):
        total = len(gaps) + (len(notes) if notes_as_gaps else 0)
        print(f"FAIL — {total} reachability gap(s). Content that cannot be reached is content that "
              f"was authored for nothing.")
        return 1

    print(f"PASS — {len(corpus.entries)} entries, everything reachable and completable.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
