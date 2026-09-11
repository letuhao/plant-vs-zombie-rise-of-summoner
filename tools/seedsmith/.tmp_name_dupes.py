"""Verify items corpus entry/file counts and probe specific name collisions."""
import json, sys, collections
from pathlib import Path

REPO = Path(r"d:\Works\source\plant-vs-zombie-rise-of-summoner")
sys.path.insert(0, str(REPO / "tools" / "seedsmith"))
from seedsmith.corpus import Corpus

c = Corpus.load(REPO / "data" / "seed" / "items")
seen = {e.path for e in c.entries.values()}
print(f"entries={len(c.entries)} files={len(seen)}")

for eid in ["atom.sporing", "set.doomfume-001", "charm.surv-util-107", "charm.surv-util-033",
            "droptable.d1-018", "item.plant-thorn-a-013"]:
    e = c.entries.get(eid)
    if e:
        print(eid, "->", e.path, "| name:", e.get("name"))

by_name = collections.defaultdict(list)
for eid, entry in c.entries.items():
    if entry.kind == "display-template":
        continue
    name = entry.get("name")
    if isinstance(name, str) and name:
        by_name[name].append(eid)
dups = {n: ids for n, ids in by_name.items() if len(ids) > 1}
kind_pairs = collections.Counter()
for n, ids in dups.items():
    kind_pairs[tuple(sorted({c.entries[i].kind for i in ids}))] += 1
for kp, n in kind_pairs.most_common():
    print(f"  {kp}: {n}")