#!/usr/bin/env python3
"""
Minimal tuning publish path (tunables-ssot.md T4 + §7.1) — "the first domain builds the tool it
actually needs," not a general CLI up front. `contracts` is that first domain.

T4: config is versioned and never hand-edited. This writes v{n+1}; v{n} stays on disk untouched, so
reverting a balance pass is restoring a file, not reading a diff.

Usage (repo root):
    python tools/tuning/publish.py contracts loyalty.winGain=20
    python tools/tuning/publish.py contracts personalityRates.loyal.gainPct=130 slots.maxSlots=64
    python tools/tuning/publish.py contracts --label "spring balance pass" loyalty.winGain=20

Each `key=value` is a dotted path into the JSON document (matching its own nesting — see
data/tuning/contracts.v1.json). The value is parsed as int, then float, then bool, then left as a
string, in that order — the first that round-trips exactly is used.

Exit codes: 0 = published, 1 = no changes / bad input, 2 = usage error.
"""
import argparse, io, json, os, re, sys

TUNING_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "data", "tuning")


def parse_value(raw):
    for conv in (int, float):
        try:
            return conv(raw)
        except ValueError:
            pass
    if raw.lower() in ("true", "false"):
        return raw.lower() == "true"
    return raw


def latest_version(domain):
    pat = re.compile(r"^%s\.v(\d+)\.json$" % re.escape(domain))
    versions = []
    for fn in os.listdir(TUNING_DIR):
        m = pat.match(fn)
        if m:
            versions.append(int(m.group(1)))
    if not versions:
        print("no existing %s.v*.json in %s" % (domain, TUNING_DIR), file=sys.stderr)
        return None
    return max(versions)


def set_path(doc, dotted_key, value):
    parts = dotted_key.split(".")
    node = doc
    for p in parts[:-1]:
        if not isinstance(node, dict) or p not in node:
            raise KeyError("'%s' has no '%s' — refusing to invent a new key (T5 spirit: "
                            "publish edits existing tunables, it does not add undocumented ones)"
                            % (dotted_key, p))
        node = node[p]
    last = parts[-1]
    if not isinstance(node, dict) or last not in node:
        raise KeyError("'%s' does not exist in the current document" % dotted_key)
    old = node[last]
    node[last] = value
    return old


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("domain")
    ap.add_argument("sets", nargs="+", metavar="key=value")
    ap.add_argument("--label", default="", help="short human note, stored in _meta.rebalanceLabel")
    a = ap.parse_args()

    if not os.path.isdir(TUNING_DIR):
        print("no such directory: %s" % TUNING_DIR, file=sys.stderr)
        return 2

    current = latest_version(a.domain)
    if current is None:
        return 2

    src_path = os.path.join(TUNING_DIR, "%s.v%d.json" % (a.domain, current))
    doc = json.loads(io.open(src_path, encoding="utf-8").read())

    changes = []
    for kv in a.sets:
        if "=" not in kv:
            print("not a key=value pair: %r" % kv, file=sys.stderr)
            return 2
        key, raw = kv.split("=", 1)
        value = parse_value(raw)
        try:
            old = set_path(doc, key, value)
        except KeyError as e:
            print("refused: %s" % e, file=sys.stderr)
            return 1
        if old == value:
            print("  %-40s unchanged (%r)" % (key, value))
        else:
            changes.append((key, old, value))
            print("  %-40s %r -> %r" % (key, old, value))

    if not changes:
        print("no changes — nothing published")
        return 1

    new_version = current + 1
    doc["version"] = new_version
    if a.label:
        doc.setdefault("_meta", {})["rebalanceLabel"] = a.label

    dst_path = os.path.join(TUNING_DIR, "%s.v%d.json" % (a.domain, new_version))
    if os.path.exists(dst_path):
        print("refusing to overwrite existing %s" % dst_path, file=sys.stderr)
        return 1

    io.open(dst_path, "w", encoding="utf-8").write(json.dumps(doc, indent=2) + "\n")
    print("\npublished %s (v%d -> v%d, %d change(s)); v%d stays on disk for revert"
          % (a.domain, current, new_version, len(changes), current))
    return 0


if __name__ == "__main__":
    sys.exit(main())
