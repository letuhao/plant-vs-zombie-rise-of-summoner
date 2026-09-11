"""Count action-seed entries and missing descriptions per committed actions file."""
import json
from pathlib import Path

REPO = Path(r"d:\Works\source\plant-vs-zombie-rise-of-summoner")
total = 0
missing_total = 0
for name in ["committed-round-1.json", "committed-round-2.json", "committed-round-909.json",
             "species-innate.json"]:
    p = REPO / "data" / "seed" / "actions" / name
    doc = json.loads(p.read_text(encoding="utf-8"))
    entries = doc.get("entries", [])
    file_kind = doc.get("kind")
    n_missing = sum(1 for e in entries if not e.get("description"))
    has_prov = sum(1 for e in entries if e.get("_provenance"))
    print(f"{name:28s} kind={str(file_kind):12s} entries={len(entries):4d} missing={n_missing:4d} prov={has_prov}")
    if file_kind == "action-seed":
        total += len(entries)
        missing_total += n_missing
print("action-seed TOTAL:", total, "missing:", missing_total)