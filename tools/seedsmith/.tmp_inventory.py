"""Mission inventory (v3): ledger-true coverage."""
import json, collections, sys
from pathlib import Path

REPO = Path(r"d:\Works\source\plant-vs-zombie-rise-of-summoner")
sys.path.insert(0, str(REPO / "tools" / "seedsmith"))

from seedsmith.adapters.trees.nodegen import plan_read, run as nodegen_run, emit as nodegen_emit

seed_root = REPO / "data" / "seed"
ledger_path = seed_root / "passive-tree" / "_runs" / "tree-language.ledger.json"
ledger_done = nodegen_run.read_ledger(ledger_path)  # returns doc["done"] as dict
ledger_by_tree = collections.defaultdict(set)
for k in ledger_done:
    tree, _, nid = k.partition(":")
    ledger_by_tree[tree].add(nid)

plan_dir = seed_root / "passive-tree" / "plan"
plans = sorted(plan_dir.glob("*.v1.json"))

print(f"plans={len(plans)} ledgerDone={len(ledger_done)} ledgerTrees={len(ledger_by_tree)}")

rows = []
for p in plans:
    tree_id = p.stem.rsplit(".v1", 1)[0]
    doc = json.loads(p.read_text(encoding="utf-8"))
    tree_plan = plan_read.load_from_dict(doc, source_label=f"plan:{tree_id}")
    expected = {n.node_id for n in tree_plan.nodes}
    seed_doc = nodegen_emit.read_seed_document(tree_id, seed_root=seed_root)
    committed = {n["id"] for n in seed_doc["nodes"]} if seed_doc else set()
    led = ledger_by_tree.get(tree_id, set())
    rows.append({
        "tree": tree_id,
        "expected": len(expected),
        "committed": len(committed),
        "ledgerInPlan": len(led & expected),
        "ledgerNotInPlan": sorted(led - expected),
        "missing": sorted(expected - committed),
        "committedNotInPlan": sorted(committed - expected),
        "pv": (json.loads((seed_root / "passive-tree" / "nodes" / f"{tree_id}.json").read_text(
            encoding="utf-8")).get("_provenance", {}).get("promptVersion")
            if (seed_root / "passive-tree" / "nodes" / f"{tree_id}.json").exists() else None),
    })

total_exp = total_com = total_led_ok = 0
problems = 0
for r in sorted(rows, key=lambda x: x["tree"]):
    total_exp += r["expected"]; total_com += r["committed"]; total_led_ok += r["ledgerInPlan"]
    flag = ""
    if r["missing"]: flag += f" MISSING={r['missing']}"
    if r["ledgerNotInPlan"]: flag += f" LEDGER_STRAY={len(r['ledgerNotInPlan'])}:{r['ledgerNotInPlan'][:3]}"
    if r["committedNotInPlan"]: flag += f" COMMITTED_STRAY={r['committedNotInPlan'][:3]}"
    if flag: problems += 1
    print(f"{r['tree']:22s} exp={r['expected']:3d} com={r['committed']:3d} led_ok={r['ledgerInPlan']:3d} pv={r['pv']}{flag}")

print(f"\nTOTAL expected={total_exp} committed={total_com} ledgerInPlan={total_led_ok} problemTrees={problems}")

plan_trees = {r["tree"] for r in rows}
print("\nledger trees not matching any plan tree:")
for t in sorted(set(ledger_by_tree) - plan_trees):
    ids = sorted(ledger_by_tree[t])
    print(f"  {t}: {len(ids)} entries, sample={ids[:5]}")
    # record shape
    first_key = next(k for k in ledger_done if k.split(':',1)[0] == t)
    print(f"    record: {json.dumps(ledger_done[first_key])[:400]}")

# species node files
nodes_dir = seed_root / "passive-tree" / "nodes"
node_files = {p.stem for p in nodes_dir.glob("*.json") if ".bak" not in p.name}
species_files = sorted(n for n in node_files if n[0].isupper())
print(f"\nspecies node files (capitalised, no plan): {species_files}")
for sf in species_files:
    led = ledger_by_tree.get(sf, set())
    doc = json.loads((nodes_dir / f"{sf}.json").read_text(encoding="utf-8"))
    ids = {n["id"] for n in doc["nodes"]}
    print(f"  {sf}: fileNodes={len(ids)} ledger={len(led)} ledgerNotInFile={len(led - ids)} fileNotInLedger={len(ids - led)}")