#!/usr/bin/env python3
"""
Numeric-overflow audit — the power ladder makes old type choices wrong.

Thresholds are computed from the shipped curve (ssot-power-scale.md §4, B=0.4), not estimated:

    float   loses integer exactness at  Theta =     232     <- inside normal play
    int     per-mille                   Theta =   3,213
    int     whole units                 Theta = 103,557
    double                              Theta = 6,710,822   <- and non-deterministic
    long                                Theta = 214,748,300 <- the default

Usage (repo root):
    python scripts/audit-overflow.py                    # full report
    python scripts/audit-overflow.py --category A4      # one category
    python scripts/audit-overflow.py --targets A3       # bare file:line list, for targeted work
    python scripts/audit-overflow.py --fix A4           # apply the ONE safe automatic rewrite
    python scripts/audit-overflow.py --paths src tools  # widen the scan (default: src)

Exit codes: 0 = no critical findings, 1 = critical findings present, 2 = usage error.
"""
import argparse, io, os, re, sys
from collections import defaultdict

# ── Classification ───────────────────────────────────────────────────────────
#
# The distinction the first version of this tool got wrong, and that matters most:
#
#   A per-mille RATIO   (chance, stability, share) is bounded 0..1000 and is SAFE in int forever.
#   A per-mille MAGNITUDE (hp, damage, yield) is unbounded and overflows int at Theta 3,213.
#
# Flagging every *Milli produced 112 findings of which nearly all were bounded ratios. An audit
# that cries wolf gets ignored, so the bar here is precision, not coverage.

# Something the power ladder can multiply.
#
# "hp" is matched case-SENSITIVELY as Hp/HP/hp (never hP) — under a case-INsensitive scan, the
# bare 2-letter token also matches the accidental "hP" that appears whenever some word ending in
# "h" abuts a word starting with "P" (KillEarnWithPatron -> "...Wit-hP-atron..."). Genuine hp
# usage is always Hp (capital H, lowercase p) or all-lowercase hp; "hP" never occurs by design, so
# excluding it loses no real finding. Found via PatronPolicy.cs:55 in the P0.3 triage.
MAGNITUDE = (r"(?:Hp|HP|hp)|(?i:atk|attack|damage|defen[cs]e|armou?r|arm1|arm2|magnitude|"
             r"yield|stock|loam|souls?|essence|shield|heal|absorb|potency)")

# Bounded 0..1000 (or otherwise capped) — per-mille here means "a fraction", never "a quantity".
RATIO = re.compile(
    r"(chance|stability|pressure|depletion|intensity|hazard|progress|share|weight|ratio|rate|"
    r"handicap|odds|probability|percent|opacity|alpha|volume|pitch|threshold|tolerance|"
    r"steepness|falloff|bias|jitter|variance|drift|multiplier|bonus|proc|loot|ceil|floor|"
    r"full|perstar|per_star|scalar|factor|mult)", re.I)

# An UNBOUNDED per-mille quantity — the only kind that can overflow. Evidence: a sweep of every
# *Milli in src/ found them all to be ratios. The repo's per-mille discipline is already correct,
# so A2 flags only names that denote an accumulating total.
UNBOUNDED_MILLI = re.compile(r"(stock|total|sum|balance|treasury|banked|accrued|lifetime|cumulative)", re.I)

# Not a magnitude at all: identifiers, counts, positions, timings.
#
# "unit" and "peractor" added by the P0.3 triage: ElementTable.ShieldUnit returns an elemental
# matchup tier (-1/0/1/2), not a shield amount; ShieldPolicy.MaxShieldsPerActor is a slot count.
# Both end in a word this list already exists to catch, so no new mechanism, just two more entries.
NOT_MAGNITUDE = re.compile(
    r"(id|ids|index|idx|count|len|length|size|version|revision|seed|hash|ms|millis|sec|seconds|"
    r"time|deltatime|unscaleddeltatime|tick|ticks|frame|fps|row|col|column|lane|slot|port|"
    r"priority|ordinal|rank|tier|band|level|x|y|z|width|height|capacity|cap|max|min|limit|"
    r"unit|units|peractor)$", re.I)

SKIP_DIRS = {"bin", "obj", "node_modules", ".git"}
SKIP_FILE = re.compile(r"\.Generated\.cs$|\.designer\.cs$", re.I)

# Float is correct here: frame deltas, probability, rendering, diagnostics, Unity interop.
FLOAT_OK_PATH = re.compile(
    r"(Diagnostics|Vfx|Overlay|Perf|Probability|Sigmoid|Random|Rng|Clock|InjectorLoop|"
    r"EventDrainHost|EffectRuntime|CheatActions|DebugActions|Host[\\/])", re.I)
FLOAT_OK_NAME = re.compile(r"(deltatime|unscaled|sun|scale|interval|duration|seconds|accum)", re.I)

# Where a double magnitude is the SHIPPED architecture rather than a new defect. The stat system
# composes in double by design (stat-system.md); calling that a bug would be re-litigating a
# locked decision. Flagged as REVIEW, not as a finding to fix.
ARCH_DOUBLE = re.compile(r"(Stats[\\/]|CombatDerivedReader|ElementHub|StatModifier|Derived)", re.I)

CATEGORIES = {
    "A1": ("CRITICAL", "float on an unbounded magnitude - non-exact past Theta 232"),
    "A2": ("CRITICAL", "int on a per-mille MAGNITUDE - overflows at Theta 3,213"),
    "A3": ("HIGH",     "int on a magnitude - overflows at Theta 103,557; long is the default"),
    "A4": ("CRITICAL", "cast-after-multiply (long)(a*b) - the multiply already overflowed"),
    "A5": ("HIGH",     "int*int widened on assignment - widen an operand, not the result"),
    "A6": ("MEDIUM",   "unchecked on a magnitude path - overflow must throw, not wrap"),
    "A7": ("REVIEW",   "double magnitude in the shipped stat architecture - decision, not defect"),
}


def rules():
    m = MAGNITUDE
    # A1/A3/A7 don't pass re.I: MAGNITUDE now handles its own case sensitivity (hp is
    # case-sensitive; the rest is wrapped in an inline (?i:...) group), and "float"/"int"/"double"
    # are always-lowercase C# keywords, so a blanket re.I here would just re-fold "hp" back open.
    return [
        ("A1", re.compile(r"\bfloat\s+(\w*(?:%s)\w*)\b" % m)),
        ("A2", re.compile(r"\bint\s+(\w*(?:milli|permille)\w*)\b", re.I)),
        ("A3", re.compile(r"\b(?:public|private|internal|protected)?\s*(?:readonly\s+)?"
                          r"int\s+(\w*(?:%s)\w*)\b" % m)),
        ("A4", re.compile(r"\((?:long|ulong)\)\s*\([^()]*\*[^()]*\)")),
        ("A5", re.compile(r"\blong\s+\w+\s*=\s*(?!\(long\))[A-Za-z_]\w*\s*\*\s*[A-Za-z_]\w*\s*;")),
        ("A6", re.compile(r"\bunchecked\b")),
        ("A7", re.compile(r"\bdouble\s+(\w*(?:%s)\w*)\b" % m)),
    ]


MILLI_SUFFIX = re.compile(r"(?:milli|permille)$", re.I)


def keep(cat, name, path, line):
    """The precision gate. Returns False for anything that cannot actually overflow."""
    if name and NOT_MAGNITUDE.search(name):
        return False
    if cat == "A2":
        # Ratios are bounded and safe forever; only an accumulating total can overflow.
        if RATIO.search(name) or not UNBOUNDED_MILLI.search(name):
            return False
    if cat == "A3":
        # A3's identifier must contain a magnitude word (hp/damage/soul/...), but that word can
        # still name a per-mille RATIO rather than a magnitude: DefenseMilli, SoulLootMilli,
        # EssenceProcMilli are bonuses/chances bounded 0..~a few thousand, not accumulating
        # totals. A2 already draws exactly this line for pure "*Milli" names; A3 needs the same
        # exclusion because its own regex can match a magnitude word THEN a Milli suffix on one
        # identifier. Found via the P0.3 triage (SoulLootMilli and five siblings).
        if MILLI_SUFFIX.search(name) and not UNBOUNDED_MILLI.search(name):
            return False
    if cat == "A1":
        if RATIO.search(name) or FLOAT_OK_PATH.search(path) or FLOAT_OK_NAME.search(name):
            return False
    if cat == "A7":
        if not ARCH_DOUBLE.search(path):
            return False        # outside the stat architecture a double magnitude is just A1
    if cat == "A4" and FLOAT_OK_PATH.search(path):
        return False        # (long)(floatSeconds * n) is a timing conversion, not an int overflow
    if cat == "A6" and not re.search(MAGNITUDE, line, re.I):
        return False
    return True


def scan(paths):
    found, compiled = [], rules()
    for root in paths:
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
            for fn in filenames:
                if not fn.endswith(".cs") or SKIP_FILE.search(fn):
                    continue
                fp = os.path.join(dirpath, fn).replace("\\", "/")
                try:
                    lines = io.open(fp, encoding="utf-8-sig").read().splitlines()
                except (UnicodeDecodeError, OSError):
                    continue
                for i, line in enumerate(lines, 1):
                    stripped = line.strip()
                    if stripped.startswith(("//", "///", "*", "/*")):
                        continue                      # a comment is not code
                    for cat, rx in compiled:
                        mt = rx.search(line)
                        if not mt:
                            continue
                        name = mt.group(1) if mt.groups() else ""
                        if not keep(cat, name, fp, line):
                            continue
                        found.append((cat, fp, i, stripped[:110]))
    return found


def fix_a4(findings, apply):
    """(long)(a * b) -> (long)a * b. The only rewrite that is provably local and safe."""
    rx = re.compile(r"\((long|ulong)\)\s*\(([^()*]+)\*([^()]+)\)")
    by_file = defaultdict(list)
    for cat, fp, ln, _ in findings:
        if cat == "A4":
            by_file[fp].append(ln)
    changed = 0
    for fp, lns in sorted(by_file.items()):
        lines = io.open(fp, encoding="utf-8-sig").read().splitlines(True)
        for ln in lns:
            old = lines[ln - 1]
            new = rx.sub(lambda m: "(%s)%s* %s" % (m.group(1), m.group(2), m.group(3).strip()), old)
            if new != old:
                print("  %s:%d\n    - %s\n    + %s" % (fp, ln, old.strip(), new.strip()))
                lines[ln - 1] = new
                changed += 1
        if apply and changed:
            io.open(fp, "w", encoding="utf-8").writelines(lines)
    print("\n%s %d site(s)%s" % ("Rewrote" if apply else "Would rewrite", changed,
                                 "" if apply else "  (re-run with --fix A4 to apply)"))
    return changed


def main():
    ap = argparse.ArgumentParser(description="Numeric-overflow audit for the power ladder.")
    ap.add_argument("--paths", nargs="*", default=["src"])
    ap.add_argument("--category", help="report one category only")
    ap.add_argument("--targets", metavar="CAT", help="bare file:line list for targeted work")
    ap.add_argument("--fix", metavar="CAT", help="apply automatic rewrite (A4 only)")
    a = ap.parse_args()

    paths = [p for p in a.paths if os.path.isdir(p)]
    if not paths:
        print("no such path(s): %s" % a.paths, file=sys.stderr)
        return 2

    findings = scan(paths)

    if a.fix:
        if a.fix != "A4":
            print("Only A4 is auto-fixable.\n\n"
                  "Widening a type (A1/A2/A3/A7) ripples through every caller, DTO, serializer and\n"
                  "golden that touches it. There is no safe blanket rewrite; use --targets to get the\n"
                  "list and change them behind a compiler that will tell you what broke.",
                  file=sys.stderr)
            return 2
        return 0 if fix_a4(findings, apply=True) >= 0 else 1

    if a.targets:
        for cat, fp, ln, _ in findings:
            if cat == a.targets:
                print("%s:%d" % (fp, ln))
        return 0

    buckets = defaultdict(list)
    for f in findings:
        buckets[f[0]].append(f)

    print("Numeric-overflow audit  —  scanned: %s" % ", ".join(paths))
    print("=" * 100)
    crit = 0
    for cat in sorted(CATEGORIES):
        sev, desc = CATEGORIES[cat]
        rows = buckets.get(cat, [])
        if a.category and cat != a.category:
            continue
        print("\n%s  [%s]  %s" % (cat, sev, desc))
        print("-" * 100)
        if not rows:
            print("  clean")
            continue
        if sev == "CRITICAL":
            crit += len(rows)
        for _, fp, ln, txt in rows[:40]:
            print("  %s:%d\n      %s" % (fp, ln, txt))
        if len(rows) > 40:
            print("  ... and %d more (use --targets %s)" % (len(rows) - 40, cat))

    print("\n" + "=" * 100)
    print("  ".join("%s=%d" % (c, len(buckets.get(c, []))) for c in sorted(CATEGORIES)))
    print("total %d finding(s), %d critical" % (len(findings), crit))
    return 1 if crit else 0


if __name__ == "__main__":
    sys.exit(main())
