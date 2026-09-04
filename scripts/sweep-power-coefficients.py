#!/usr/bin/env python3
"""
Power-coefficient sweep — E44 `power-sweep`, acceptance criteria 1 and 3.

Spec: docs/architecture/effect-atom/spec-power-sweep.md §4.1
SSOT: docs/architecture/power/ssot-power-scale.md (numeric contract: long, widen before
multiplying, divide by 1000 last, overflow throws)

WHAT THIS DOES
--------------
Loads the real generated-atom corpus under data/seed/atoms/generated/ (E43 `family-expand`,
currently 45 rows across 3 files: g-armour.json, g-attack.json, g-life.json) and computes the real
observed magnitude distribution per (kind, channel), split by `op` — `flat` (a raw stat-unit
delta), `increased` / `more` (a percentage modifier). Both share the same channel and therefore the
same `CoefficientTable` row, but are NOT the same unit — see FINDING 2 below.

`CoefficientTable`'s own contract (`PowerCoefficientRow.ReferenceScale`'s doc comment) ties
`ReferenceScale` to "what one RAW unit means for this channel" — i.e. the `flat` op's own unit.
That is the one sub-corpus this table's key granularity (kind × channel, no `op` axis) can actually
fit without conflating two different units under one number. This script therefore fits `CoeffMilli`
(the value spec §2/§4.1 names as "the flat 1000s") from the median `flat`-op magnitude of each
channel, holding `ReferenceScale` at its existing authored value (2/2/10/10 — already a reasonable,
pre-existing per-channel unit pick that this sweep does not need to re-derive, since the spec's own
inventory table already lists ReferenceScale as the dial that "varies", only CoeffMilli is flat).

FORMULA (integer, per-mille, one rounding point — mirrors CostFunction.PriceForChannel exactly):

    normalizedMilli(mag)  = round(mag * 1000 / referenceScale)     # div-round half away from zero
    price(mag, coeffMilli) = round(normalizedMilli(mag) * coeffMilli / 1000)

    fittedCoeffMilli(channel) = round(
        TARGET_POINTS * 1000 / normalizedMilli(medianFlatMagnitude(channel))
    )

TARGET_POINTS = 1000 mirrors the existing "one reference unit = 1000 pts" convention already used
elsewhere in this codebase (`RungPowerBudgetTests`' own `referencePower = PowerMath.One`) — not a
number invented for this sweep.

WHY MEDIAN, NOT MEAN: only 5 tiers per family; the median (=tier 3, the middle tier) is the same
robust "pin one representative point, not an average of a range no one exemplar hits" choice
ssot-power-scale.md §4.3 uses for its own single calibration point (`P(20) = 680`).

FINDING 1 (fit result): with CoeffMilli fitted this way, the four channels' median (tier-3) `flat`
atom prices converge from a 3.7x spread (might/atk=4500, warding/defense=2000, mending/hp=7400,
vitality/maxHp=7400, under the flat-1000 baseline) to 999-1000 across the board — measured, not
asserted; see the printed table.

FINDING 2 (structural limit, reported honestly per spec §7 criterion 7 — NOT fixed here, out of
scope): `increased`/`more` atoms sharing these same 4 channels carry percentage magnitudes (e.g.
23-47 at tier 1, regardless of channel) that are numerically indistinguishable, at this table's
(kind, channel) key granularity, from a `flat` atom's raw-unit magnitude. No choice of CoeffMilli or
ReferenceScale can reconcile the two without widening the coefficient key to include `op` — a
CostFunction/CoefficientTable lookup-key change, out of E44's scope (a coefficient-DATA change).
This is scale-invariant: rescaling CoeffMilli or ReferenceScale moves both op-classes by the same
factor and never changes the ratio between them. Reported so a later module owns it explicitly
rather than rediscovering it.

COVERAGE: only 4 of the 20 `CoefficientTable.Authored()` channel rows (`stat.modify` / atk, defense,
hp, maxHp) have any real generated content to fit against. The other 16 rows (arm1/arm1Max/arm2/
arm2Max/stat.modify"" /stat.derived""/resource.delta/resource.economy/status.apply/status.clear/
shield.grant/spawn.entity/board.action/grid.spawn/grid.clear/box.set) have zero real corpus under
data/seed/atoms/ today and are NOT fitted here — per spec §5, this script does not fit against
synthetic data alone, and inventing numbers for them would be a third refuted flat-number guess.

Usage (repo root):
    python scripts/sweep-power-coefficients.py             # print the fit + full report
    python scripts/sweep-power-coefficients.py --json       # also print the coefficients.v1.json
                                                             # entries this fit produces (for review)

Exit codes: 0 always (this is a research/report tool, not a gate).
"""
import argparse
import glob
import json
import statistics
from collections import defaultdict

ONE = 1000
TARGET_POINTS = 1000  # one reference unit == 1000 pts (PowerMath.One's own convention)

# The existing, authored, PRE-EXISTING referenceScale for these four channels
# (CoefficientTable.cs Authored(), untouched by this sweep — only CoeffMilli is fitted).
AUTHORED_REFERENCE_SCALE = {"atk": 2, "defense": 2, "hp": 10, "maxHp": 10}

CORPUS_GLOB = "data/seed/atoms/generated/family-expand.*.json"


def div_round(n: int, d: int) -> int:
    """PowerMath.DivRound — integer division, round half away from zero, same rule everywhere."""
    if d == 0:
        return 0
    sign = -1 if (n < 0) != (d < 0) else 1
    n, d = abs(n), abs(d)
    return sign * ((n + d // 2) // d)


def mul_milli(value: int, milli: int) -> int:
    """PowerMath.MulMilli."""
    return div_round(value * milli, ONE)


def price_total(mag: int, ref_scale: int, coeff_milli: int) -> int:
    """CostFunction.PriceForChannel's own arithmetic, conditionality neutral (=1000, no trigger)."""
    normalized_milli = div_round(mag * ONE, max(1, ref_scale))
    return mul_milli(normalized_milli, coeff_milli)


def load_corpus():
    atoms = []
    for f in sorted(glob.glob(CORPUS_GLOB)):
        doc = json.load(open(f, encoding="utf-8"))
        for e in doc["entries"]:
            p = e["params"]
            amt = p["amount"]
            mean_mag = div_round(amt["min"] + amt["max"], 2)
            atoms.append({
                "file": f, "family": e["family"], "tier": e["tier"],
                "kind": e["kind"], "channel": p["channel"], "op": p["op"], "mag": mean_mag,
            })
    return atoms


def fit(atoms):
    flat_by_channel = defaultdict(list)
    for a in atoms:
        if a["op"] == "flat":
            flat_by_channel[a["channel"]].append(a["mag"])

    fitted = {}
    detail = {}
    for ch, mags in flat_by_channel.items():
        med = statistics.median(sorted(mags))
        scale = AUTHORED_REFERENCE_SCALE[ch]
        normalized_at_median = div_round(int(med) * ONE, scale)
        coeff = div_round(TARGET_POINTS * ONE, normalized_at_median)
        fitted[ch] = int(coeff)
        detail[ch] = {
            "medianFlatMagnitude": med, "referenceScale": scale,
            "normalizedMilliAtMedian": normalized_at_median, "fittedCoeffMilli": int(coeff),
        }
    return fitted, detail


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", action="store_true", help="also print the coefficients.v1.json entries")
    args = ap.parse_args()

    atoms = load_corpus()
    print(f"Corpus: {len(atoms)} atom(s) from {CORPUS_GLOB}")
    channels = sorted({a["channel"] for a in atoms})
    print(f"Channels touched: {channels}")
    print()

    fitted, detail = fit(atoms)
    print("Fit method: CoeffMilli(channel) = round(1,000,000 / normalizedMilli(medianFlatMagnitude))")
    print("(ReferenceScale held at its existing authored value; only CoeffMilli moves.)\n")
    for ch in sorted(detail):
        d = detail[ch]
        print(f"  {ch:8s} medianFlat={d['medianFlatMagnitude']:<4} referenceScale={d['referenceScale']:<3} "
              f"-> fitted CoeffMilli={d['fittedCoeffMilli']}")
    print()

    fams = defaultdict(list)
    for a in atoms:
        fams[a["family"]].append(a)

    print(f"{'family':16s} {'chan':8s} {'op':10s} {'tier':4s} {'mag':5s} {'flat1000.price':14s} {'fitted.price':12s}")
    for fam, rows in sorted(fams.items()):
        rows.sort(key=lambda r: r["tier"])
        ch = rows[0]["channel"]
        scale = AUTHORED_REFERENCE_SCALE[ch]
        for r in rows:
            baseline = price_total(r["mag"], scale, 1000)
            fitted_price = price_total(r["mag"], scale, fitted[ch])
            print(f"{fam:16s} {ch:8s} {r['op']:10s} {r['tier']:<4d} {r['mag']:<5d} "
                  f"{baseline:<14d} {fitted_price:<12d}")

    print()
    print("== Finding 1: flat-op cross-channel parity at the median (tier 3) tier ==")
    for fam, rows in sorted(fams.items()):
        r = next(x for x in rows if x["tier"] == 3)
        if r["op"] != "flat":
            continue
        ch = r["channel"]
        scale = AUTHORED_REFERENCE_SCALE[ch]
        baseline = price_total(r["mag"], scale, 1000)
        fitted_price = price_total(r["mag"], scale, fitted[ch])
        print(f"  {fam:16s} ({ch:8s}) mag={r['mag']:<5d} flat-1000-price={baseline:<8d} fitted-price={fitted_price:<8d}")

    print()
    print("== Finding 2 (structural, NOT fixed here): op disparity at tier 1, same channel ==")
    print("   (ratio is scale-invariant under any CoeffMilli/ReferenceScale choice)")
    by_chan_op_t1 = defaultdict(dict)
    for a in atoms:
        if a["tier"] == 1:
            by_chan_op_t1[a["channel"]][a["op"]] = a["mag"]
    for ch, ops in sorted(by_chan_op_t1.items()):
        print(f"  {ch}: {ops}")

    if args.json:
        print()
        print("== coefficients.v1.json entries (fitted rows only) ==")
        entries = []
        for ch in sorted(fitted):
            entries.append({
                "kindId": "stat.modify", "channel": ch,
                "coeffMilli": fitted[ch], "referenceScale": AUTHORED_REFERENCE_SCALE[ch],
            })
        print(json.dumps(entries, indent=2))


if __name__ == "__main__":
    main()
