"""Cross-corpus integrity: every committed node's affixIds must resolve in the CURRENT vocabulary."""
import json, collections, sys
from pathlib import Path

REPO = Path(r"d:\Works\source\plant-vs-zombie-rise-of-summoner")
sys.path.insert(0, str(REPO / "tools" / "seedsmith"))

from seedsmith.adapters.trees.nodegen import vocab as vocab_mod

v = vocab_mod.build()
known = set(v.ids())

seed_root = REPO / "data" / "seed"
nodes_dir = seed_root / "passive-tree" / "nodes"
unknown = collections.defaultdict(set)
node_count = 0
tag_usage = collections.Counter()
for p in sorted(nodes_dir.glob("*.json")):
    if ".bak" in p.name: continue
    doc = json.loads(p.read_text(encoding="utf-8"))
    for n in doc.get("nodes", []):
        node_count += 1
        for aid in n.get("affixIds", []):
            tag_usage[aid] += 1
            if aid not in known:
                unknown[p.stem].add(aid)

print(f"nodes scanned: {node_count} across {len(list(nodes_dir.glob('*.json')))} files")
print(f"unknown affixIds: {sum(len(s) for s in unknown.values())}")
for t, ids in sorted(unknown.items()):
    print(f"  {t}: {sorted(ids)}")

# top used affix ids (for SpeciesUniqueness context)
print("\ntop 10 used affixIds:", tag_usage.most_common(10))

# exclusion propertyKeys must be in plan propertyVocabulary axes (shared trees)
PLAN_AXES = {"nodeClass", "atomTrigger", "element", "status", "channelFamily", "exclusionForm",
             "aptitude", "posture", "branch", "tier", "atomKind", "atomTrigger", "atomAttachPoint"}
bad_props = collections.defaultdict(set)
for p in sorted(nodes_dir.glob("*.json")):
    if ".bak" in p.name: continue
    doc = json.loads(p.read_text(encoding="utf-8"))
    for n in doc.get("nodes", []):
        ex = n.get("exclusion") or {}
        for k in ex.get("propertyKeys", []) or []:
            if k not in PLAN_AXES:
                bad_props[p.stem].add(k)
print(f"\nunknown exclusion propertyKeys: {sum(len(s) for s in bad_props.values())}")
for t, ids in sorted(bad_props.items()):
    print(f"  {t}: {sorted(ids)}")