"""Summarize the cross-kind / same-kind authored-name collisions by kind pair."""
import sys
sys.path.insert(0, r"d:\Works\source\plant-vs-zombie-rise-of-summoner\tools\seedsmith")

from pathlib import Path
from collections import Counter
from seedsmith.corpus import Corpus

LIVE_ITEMS_ROOT = Path(r"d:\Works\source\plant-vs-zombie-rise-of-summoner\data\seed\items")
corpus = Corpus.load(LIVE_ITEMS_ROOT)

by_name: dict[str, list] = {}
for entry in corpus.entries.values():
    name = getattr(entry, "name", None)
    if isinstance(name, str) and name.strip():
        by_name.setdefault(name.strip().casefold(), []).append(entry)

dup_groups = {n: rows for n, rows in by_name.items() if len(rows) > 1}
print(f"total entries={len(corpus.entries)} dup_groups={len(dup_groups)}")

pair_counter: Counter = Counter()
kind_rows: Counter = Counter()
same_kind_counter: Counter = Counter()
cross_examples = []
for name, rows in sorted(dup_groups.items()):
    kinds = sorted({getattr(r, "kind", "?") for r in rows})
    for k in kinds:
        kind_rows[k] += 1
    if len(kinds) > 1:
        pair_counter[tuple(kinds)] += 1
        if len(cross_examples) < 12:
            cross_examples.append((name, kinds, [getattr(r, "id", "?") for r in rows[:4]]))
    else:
        same_kind_counter[kinds[0]] += 1

print("\ncross-kind by kind-pair:")
for pair, n in pair_counter.most_common():
    print(f"  {n:4d}  {' x '.join(pair)}")
print("\nsame-kind dup groups by kind:", dict(same_kind_counter))
print("\nkinds involved in any dup group:", dict(kind_rows))
print("\nsample cross-kind groups:")
for name, kinds, ids in cross_examples:
    print(f"  {name!r}: kinds={kinds} ids={ids}")