#!/usr/bin/env python3
"""
Minimal tuning publish path (tunables-ssot.md T4 + §7.1) — "the first domain builds the tool it
actually needs," not a general CLI up front. `contracts` is that first domain; §7.1 itself: "the
second domain generalises it if the shape holds" — `aptitudes` is that second domain, and its own
`edges` array (a list of `{channel, source, kMilli}` objects, not a nested dict) is a genuinely new
shape dict-only dotted paths cannot address, so this adds exactly one thing: a bracket selector on a
list segment.

T4: config is versioned and never hand-edited. This writes v{n+1}; v{n} stays on disk untouched, so
reverting a balance pass is restoring a file, not reading a diff.

Usage (repo root):
    python tools/tuning/publish.py contracts loyalty.winGain=20
    python tools/tuning/publish.py contracts personalityRates.loyal.gainPct=130 slots.maxSlots=64
    python tools/tuning/publish.py contracts --label "spring balance pass" loyalty.winGain=20
    python tools/tuning/publish.py aptitudes "edges[channel=resource.regen.hp,source=Vigor].kMilli=83"

Each `key=value` is a dotted path into the JSON document (matching its own nesting — see
data/tuning/contracts.v1.json). The value is parsed as int, then float, then bool, then left as a
string, in that order — the first that round-trips exactly is used.

A path segment may carry a bracket selector, `name[k1=v1,k2=v2]`, to reach one object inside a JSON
array named `name` — the object whose fields match every k=v pair exactly (string equality; ints in
the selector compare against int values). Refuses (does not guess) if zero or more than one array
element matches, same as a missing dict key refuses rather than defaulting.

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


SELECTOR_RE = re.compile(r"^([^\[\]]+)\[([^\[\]]+)\]$")


def split_kv(arg):
    # A bracket selector's own clauses ("channel=resource.regen.hp") contain "=" too, so the naive
    # first-"=" split used for the selector's OWN clauses (there, correct: one "=" per clause, no
    # brackets to worry about) is wrong at the top level, where that "=" is INSIDE a [...] and not
    # the key/value separator. Split on the first "=" that is outside any bracket depth instead.
    depth = 0
    for i, ch in enumerate(arg):
        if ch == "[":
            depth += 1
        elif ch == "]":
            depth -= 1
        elif ch == "=" and depth == 0:
            return arg[:i], arg[i + 1:]
    return None


def split_path(dotted_key):
    # Plain str.split(".") would also split the dots INSIDE a bracket selector's own value (e.g.
    # "edges[channel=resource.regen.hp,source=Vigor].kMilli" -- "resource.regen.hp" is itself
    # dotted). Only split on a "." that is outside any [...].
    parts, depth, current = [], 0, ""
    for ch in dotted_key:
        if ch == "[":
            depth += 1
            current += ch
        elif ch == "]":
            depth -= 1
            current += ch
        elif ch == "." and depth == 0:
            parts.append(current)
            current = ""
        else:
            current += ch
    parts.append(current)
    return parts


def _parse_selector(raw):
    # "k1=v1,k2=v2" -> {"k1": v1, "k2": v2}, values parsed the same way CLI values are (int first).
    pairs = {}
    for kv in raw.split(","):
        if "=" not in kv:
            raise KeyError("selector clause '%s' is not key=value" % kv)
        k, v = kv.split("=", 1)
        pairs[k] = parse_value(v)
    return pairs


def _step(node, dotted_key, seg):
    """Advance one dotted-path segment (plain key, or name[k=v,...] into a list) and return
    (child, description) — description names what was matched, for error messages."""
    m = SELECTOR_RE.match(seg)
    if m is None:
        if not isinstance(node, dict) or seg not in node:
            raise KeyError("'%s' has no '%s' — refusing to invent a new key (T5 spirit: "
                            "publish edits existing tunables, it does not add undocumented ones)"
                            % (dotted_key, seg))
        return node[seg], seg

    list_key, selector_raw = m.group(1), m.group(2)
    if not isinstance(node, dict) or list_key not in node:
        raise KeyError("'%s' has no '%s' — refusing to invent a new key" % (dotted_key, list_key))
    lst = node[list_key]
    if not isinstance(lst, list):
        raise KeyError("'%s' is not an array — a [selector] only applies to one" % list_key)
    selector = _parse_selector(selector_raw)
    matches = [el for el in lst if isinstance(el, dict) and all(el.get(k) == v for k, v in selector.items())]
    if len(matches) == 0:
        raise KeyError("'%s[%s]' matches no element of '%s' — refusing to guess"
                        % (list_key, selector_raw, dotted_key))
    if len(matches) > 1:
        raise KeyError("'%s[%s]' matches %d elements of '%s' — selector must be unique"
                        % (list_key, selector_raw, len(matches), dotted_key))
    return matches[0], "%s[%s]" % (list_key, selector_raw)


def set_path(doc, dotted_key, value):
    parts = split_path(dotted_key)
    node = doc
    for p in parts[:-1]:
        node, _ = _step(node, dotted_key, p)
    last = parts[-1]
    m = SELECTOR_RE.match(last)
    if m is not None:
        raise KeyError("'%s' ends in a [selector] with no field after it — nothing to set" % dotted_key)
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
        split = split_kv(kv)
        if split is None:
            print("not a key=value pair: %r" % kv, file=sys.stderr)
            return 2
        key, raw = split
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
