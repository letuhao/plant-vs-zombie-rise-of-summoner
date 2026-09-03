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
    python tools/tuning/publish.py aptitudes --add-edge "channel=resource.max.poise,source=Bulwark,kMilli=28000"
    python tools/tuning/publish.py action-rungs --add-rung-power-budget 1000

`--add-edge` is the one path that ADDS rather than edits, and it exists because a coverage gap could
not be closed otherwise: `set` refuses to invent a key by design, and the file forbids hand-editing, so
a resource family that was never given a row had no legal way to gain one (resource-symmetry audit,
2026-09-02). It is deliberately narrow — it appends one `{channel, source, kMilli}` object to `edges`
and refuses anything that is not filling in a KNOWN family for a KNOWN source:

  * the channel's family must already appear on some existing edge — a new MEMBER of `resource.max.*`
    is allowed, a brand-new family is not (that is a schema change, not a balance one);
  * the source must already be a source somewhere in `edges`;
  * `(channel, source)` must not already exist — use the `set` path to change a value.

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



def add_edge(doc, spec_raw):
    """Append one {channel, source, kMilli} edge. Refuses rather than guesses, matching set_path."""
    spec = _parse_selector(spec_raw)
    required = {"channel", "source", "kMilli"}
    if set(spec) != required:
        raise KeyError("--add-edge needs exactly channel=, source= and kMilli= (got: %s)"
                       % ", ".join(sorted(spec)) or "nothing")
    if not isinstance(spec["kMilli"], int):
        raise KeyError("kMilli must be an integer (got %r)" % (spec["kMilli"],))

    edges = doc.get("edges")
    if not isinstance(edges, list):
        raise KeyError("'edges' is not an array in this document — --add-edge is aptitudes-shaped")

    real = [e for e in edges if isinstance(e, dict) and "channel" in e]
    channel, source = spec["channel"], spec["source"]

    if any(e.get("channel") == channel and e.get("source") == source for e in real):
        raise KeyError("edge (channel=%s, source=%s) already exists — use the set path to change its "
                       "value, this flag only fills gaps" % (channel, source))

    known_sources = {e.get("source") for e in real}
    if source not in known_sources:
        raise KeyError("'%s' is not a source anywhere in edges — refusing to invent one" % source)

    family = channel.rsplit(".", 1)[0]
    known_families = {e["channel"].rsplit(".", 1)[0] for e in real}
    if family not in known_families:
        raise KeyError("'%s' is a NEW channel family, not a new member of an existing one — that is a "
                       "schema change, not a balance one; refusing" % family)

    edges.append({"channel": channel, "source": source, "kMilli": spec["kMilli"]})
    return channel, source, spec["kMilli"]



def add_rung_power_budget(doc, reference_power):
    """A-G1 (spec-tier-access-gate.md SS3.1): add `powerBudgetMilli` to every row of `rows`, derived
    from the row's OWN already-shipped columns rather than a new curve --

        powerBudgetMilli(r) = poolRolls(r) * referencePower * qPowerMilli(r) / 1000

    long arithmetic (Python ints are unbounded, so this never wraps the way the C# reader must guard
    against), widened before multiplying, divided by 1000 last and exactly once. Refuses rather than
    guesses, matching --add-edge: any row already carrying the column is refused (use `set` to change
    a value), and a row missing `poolRolls`/`qPowerMilli` is refused by name. Also records the
    derivation and the scalar's untuned status in `_meta`, the same direct-write `--label` already
    uses for `_meta.rebalanceLabel` -- `set_path` cannot fill either because both are new keys.
    """
    rows = doc.get("rows")
    if not isinstance(rows, list) or not rows:
        raise KeyError("'rows' is not a non-empty array -- --add-rung-power-budget is action-rungs-shaped")

    if any(isinstance(r, dict) and "powerBudgetMilli" in r for r in rows):
        raise KeyError("some row already has 'powerBudgetMilli' -- use the set path to change a "
                       "value, this flag only fills a first-time gap")

    added = []
    for r in rows:
        if not isinstance(r, dict) or "poolRolls" not in r or "qPowerMilli" not in r:
            raise KeyError("a row is missing 'poolRolls' or 'qPowerMilli' -- cannot derive its power budget")
        pool_rolls, q_power_milli = r["poolRolls"], r["qPowerMilli"]
        if not isinstance(pool_rolls, int) or not isinstance(q_power_milli, int):
            raise KeyError("'poolRolls'/'qPowerMilli' must be integers")

        budget = pool_rolls * reference_power * q_power_milli // 1000
        r["powerBudgetMilli"] = budget
        added.append((r.get("rung"), budget))

    meta = doc.setdefault("_meta", {})
    meta["referencePower"] = reference_power
    meta["referencePowerUntuned"] = True
    meta["powerBudgetDerivation"] = (
        "powerBudgetMilli(r) = poolRolls(r) * referencePower * qPowerMilli(r) / 1000 -- long "
        "arithmetic, widened before multiplying, divided by 1000 last and exactly once (A-G1, "
        "spec-tier-access-gate.md SS3.1). referencePower = PowerMath.One (PowerVector.cs:135, inside "
        "the PowerMath class -- one reference action is worth one unit of power. At "
        "referencePower=1000 the /1000 cancels the *1000 exactly, so the budget IS qPowerMilli's own "
        "curve, unscaled -- rung 1 lands on exactly 1000 for this reason, never by coincidence. "
        "referencePower is untuned: what it tunes against is the smoke batch's accepted-container "
        "cost distribution (a later module's output), not yet produced. It is a single scalar that "
        "moves the whole ladder together and can never introduce a second curve shape."
    )

    return added


def rename_key(doc, spec_raw):
    """Rename one dict key in place, preserving insertion order. Refuses rather than guesses."""
    # `container.path:oldLeaf=newLeaf`. The COLON matters: a tuning key is very often itself dotted
    # (`familyRead."combat.heal.power"`), so a plain dotted path cannot say where the container ends
    # and the leaf begins. Everything after the colon is one literal key name, dots included.
    if "=" not in spec_raw or ":" not in spec_raw.split("=", 1)[0]:
        raise KeyError("--rename-key needs container.path:oldLeaf=newLeaf (got %r)" % spec_raw)
    lhs, new_leaf = spec_raw.split("=", 1)
    container_path, last = lhs.split(":", 1)
    node = doc
    for seg in split_path(container_path):
        node, _ = _step(node, container_path, seg)
    if not isinstance(node, dict) or last not in node:
        raise KeyError("'%s' has no key '%s' — refusing to rename a key that is not there" % (container_path, last))
    if new_leaf in node:
        raise KeyError("'%s' already exists alongside '%s' — refusing to overwrite it" % (new_leaf, last))
    rebuilt = {}
    for k, v in node.items():
        rebuilt[new_leaf if k == last else k] = v
    node.clear()
    node.update(rebuilt)
    return last, new_leaf


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("domain")
    ap.add_argument("sets", nargs="*", metavar="key=value")
    ap.add_argument("--add-edge", action="append", default=[], dest="add_edges",
                    metavar="channel=..,source=..,kMilli=..",
                    help="append one edge to `edges` (fills a coverage gap; refuses duplicates and unknown families)")
    ap.add_argument("--rename-key", action="append", default=[], dest="renames",
                    metavar="container.path:oldLeaf=newLeaf",
                    help="rename one dict key in place (order preserved); refuses if absent or if the new name is taken")
    ap.add_argument("--add-rung-power-budget", type=int, default=None, dest="add_rung_power_budget",
                    metavar="REFERENCE_POWER",
                    help="derive and add `powerBudgetMilli` to every row of `rows` "
                         "(powerBudgetMilli = poolRolls * REFERENCE_POWER * qPowerMilli / 1000); "
                         "refuses if any row already has the column (action-rungs-shaped only)")
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
    for spec in a.renames:
        try:
            old_leaf, new_leaf = rename_key(doc, spec)
        except KeyError as e:
            print("refused: %s" % e, file=sys.stderr)
            return 1
        changes.append((spec, old_leaf, new_leaf))
        print("  %-52s RENAMED -> %s" % (old_leaf, new_leaf))

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

    # AFTER sets/renames on purpose: a rename can create the family an --add-edge then extends.
    for spec in a.add_edges:
        try:
            ch, src, k = add_edge(doc, spec)
        except KeyError as e:
            print("refused: %s" % e, file=sys.stderr)
            return 1
        changes.append((("edges[channel=%s,source=%s].kMilli" % (ch, src)), None, k))
        print("  %-52s ADDED (%r)" % ("%s / %s" % (ch, src), k))

    if a.add_rung_power_budget is not None:
        try:
            added = add_rung_power_budget(doc, a.add_rung_power_budget)
        except KeyError as e:
            print("refused: %s" % e, file=sys.stderr)
            return 1
        for rung, budget in added:
            changes.append(("rows[rung=%s].powerBudgetMilli" % rung, None, budget))
            print("  %-52s ADDED (%r)" % ("rung %s powerBudgetMilli" % rung, budget))

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
