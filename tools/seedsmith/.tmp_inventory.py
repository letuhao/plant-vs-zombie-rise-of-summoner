"""Mission inventory: full per-tree/per-node coverage for PassiveTree."""
import json, sys, collections
from pathlib import Path

ROOT = Path(r"d:\Works\source\plant-vs-zombie-rise-of-summoner\data\seed\passive-tree")

def load(p):
    with open(p, encoding="utf-8") as f:
        return json.load(f)

plans = {}
for p in sorted((ROOT / "plan").glob("*.json")):
    if ".bak" in p.name: continue
    j = load(p)
    plans[p.stem] = j

# expected node count per plan: count nodes in plan structure
def plan_node_ids(plan):
    ids = []
    # discover shape: look for tiers/branches arrays
    def walk(o, path=""):
        if isinstance(o, dict):
            if "nodeIds" in o and isinstance(o["nodeIds"], list):
                ids.extend(o["nodeIds"])
            for k, v in o.items():
                walk(v, path + "/" + k)
        elif isinstance(o, list):
            for i, v in enumerate(o):
                walk(v, path + f"[{i}]")
    walk(plan)
    return ids

nodes_files = {}
for p in sorted((ROOT / "nodes").glob("*.json")):
    if ".bak" in p.name: continue
    j = load(p)
    nodes_files[p.stem] = j

ledger = load(ROOT / "_runs" / "tree-language.ledger.json")
ledger_keys = list(ledger.get("done", {}).keys())
ledger_by_tree = collections.Counter(k.split(":", 1)[0] if ":" in k else "?" for k in ledger_keys)

identities = {p.stem for p in (ROOT / "identity").glob("*.json") if ".bak" not in p.name}
species_records = {p.stem for p in (ROOT / "species").glob("*.json")}

print("=== PLANS ===", len(plans))
plan_summary = {}
for name, j in plans.items():
    ids = plan_node_ids(j)
    plan_summary[name] = ids
    print(f"{name:24s} expectedNodes={len(ids):4d} keys={sorted(j.keys())[:8]}")

print()
print("=== NODE FILES ===", len(nodes_files))
for name, j in nodes_files.items():
    prov = j.get("_provenance", {})
    nds = j.get("nodes", [])
    qc = sum(1 for n in nds if "quotaCell" in n)
    treeid = j.get("treeId", "?")
    print(f"{name:24s} treeId={treeid:20s} nodes={len(nds):3d} quotaCell={qc:3d} pv={prov.get('promptVersion')} model={prov.get('model')} planHash={prov.get('planHash','')!r}")

print()
print("=== LEDGER === total done:", len(ledger_keys))
for tree, c in sorted(ledger_by_tree.items()):
    print(f"{tree:24s} {c:4d}")

print()
print("=== CROSS-CHECK per tree (plan vs nodes vs ledger) ===")
all_trees = sorted(set(plan_summary) | set(nodes_files) | set(ledger_by_tree))
for t in all_trees:
    pid = set(plan_summary.get(t, []))
    nf = nodes_files.get(t)
    nid = {n.get("id") for n in nf.get("nodes", [])} if nf else set()
    led = {k.split(":", 1)[1] for k in ledger_keys if k.split(":", 1)[0] == t} if t in ledger_by_tree else set()
    missing_committed = pid - nid
    extra_committed = nid - pid
    missing_ledger = pid - led
    ledger_only = led - pid - nid
    flag = ""
    if missing_committed: flag += f" MISSING_IN_NODES={len(missing_committed)}{sorted(missing_committed)[:4]}"
    if extra_committed: flag += f" EXTRA_IN_NODES={len(extra_committed)}{sorted(extra_committed)[:4]}"
    if missing_ledger: flag += f" NOT_IN_LEDGER={len(missing_ledger)}{sorted(missing_ledger)[:3]}"
    if ledger_only: flag += f" LEDGER_ONLY={len(ledger_only)}{sorted(ledger_only)[:3]}"
    is_species = t[0].isupper()
    tag = "SPECIES" if is_species else "shared"
    print(f"{t:24s} [{tag:7s}] plan={len(pid):3d} nodes={len(nid):3d} ledger={len(led):3d}{flag}")

print()
print("=== IDENTITY vs PLANS ===")
print("identities:", len(identities), " plans:", set(plans) - identities, " identityless plans above")
print("=== SPECIES records ===", species_records)