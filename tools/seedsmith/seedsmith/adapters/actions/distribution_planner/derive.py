"""seedsmith.adapters.actions.distribution_planner.derive — spec-distribution-planner.md §3's nine
steps (+2b, +4a). Pure derivation; the one file that touches disk is the sibling entrypoint
`../generate_distribution_planner.py`, matching every `derive.py` in this adapter family.

**Two genuine, undocumented gaps this module had to close, flagged rather than hidden** (same
discipline `characteristic_pool/derive.py`'s own `SIGNAL_CATEGORY` docstring uses for its own
closest analogue): the spec's nine algorithm steps never assign `slot.relation` or `slot.kind`,
yet the brief JSON example (§2) shows both and `relation` is REQUIRED on the eventual action-seed
(`kinds.py`'s `ACTION_SEED_REQUIRED`). No weight vector for either exists anywhere in the corpus
(`type-weights.json` carries no relation or kind row). Resolved:

- **`relation` gets a small structural map, `CATEGORY_RELATION`** — the same shape as
  `characteristic_pool.derive.SIGNAL_CATEGORY` (a closed, editorial, non-magnitude mapping, never
  a citation): `attack`/`status` act on an opponent (`enemy`); `support` customarily reaches
  teammates (`ally`); `defense`/`movement` customarily reach the caster (`self`). Flagged in this
  module's build report as the single most owner-review-worthy call it makes, same as
  `SIGNAL_CATEGORY` was for its own module.
- **`slot.kind` is never invented.** `kindHint` is OPTIONAL on the real action-seed schema (not
  in `ACTION_SEED_REQUIRED`), and no step of this module's own algorithm derives it — so every
  brief emits `slot.kind: null`, honest and explicit, the same "absent is a defect, empty/null is
  a value" discipline `spec-family-propose.md`'s `familyActions` and this module's own
  `familyMotifs` already use. A downstream module (A-P1/A-P2, or the model itself) decides it.
"""
from __future__ import annotations

import json
import re
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Mapping, Sequence

from ..characteristic_pool.derive import CATEGORIES
from ..type_weights.tuning import AREA_SHAPES, TARGET_MODES
from ..vocab import PAIRING_ROLES, RELATIONS, STATUSES
from .fingerprint import FingerprintComponents, k_nearest, render_fingerprint, render_fingerprint_string

__all__ = [
    "RUN_WINDOW", "REACTION_AXIS", "RESTRICTION_AXIS", "CATEGORY_RELATION",
    "SpeciesAnchorRow", "parse_species_anchor", "WeightsRow", "parse_type_weights",
    "GENERAL_WEIGHTS", "load_rung_table", "structure_axes_for", "validate_structure_axes",
    "derive_family_motifs", "largest_remainder_count", "largest_remainder_apportion",
    "apportion_categories", "expand_counts",
    "build_pool", "validate_no_family_widening", "validate_atom_family_namespace",
    "validate_pairing_vocabulary", "validate_no_multiplicative_conflict", "validate_rung_band",
    "load_pairing_table", "assign_pairing_roles", "PairingAssignment", "validate_pairing_coverage",
    "audit_no_magnitude_smuggling", "refuse_full_run_if_ungated",
    "plan_subject", "plan_round",
]

# The repo-wide `long` bound (CLAUDE.md's numeric-overflow table) — same guard `type_weights/
# derive.py` uses; Python ints never actually overflow, so this is an EXPLICIT check standing in
# for the `long` every C# consumer eventually reads this as.
_LONG_MAX = 9_223_372_036_854_775_807
_LONG_MIN = -9_223_372_036_854_775_808


def _widen_mul(a: int, b: int) -> int:
    a = int(a)
    b = int(b)
    product = a * b
    if product > _LONG_MAX or product < _LONG_MIN:
        raise OverflowError(f"_widen_mul({a}, {b}) = {product} does not fit a long "
                            f"({_LONG_MIN}..{_LONG_MAX})")
    return product


# ---------------------------------------------------------------------------------------------
# §3 step 3 / 4a — largest-remainder allocation, generalised to distribute a per-subject COUNT
# (not always 1000) across a closed, ordered vocabulary. Reused for category (step 3), targetMode
# and areaShape (step 4a) — the exact same discipline, three times, over three different `total`s.
# ---------------------------------------------------------------------------------------------

def largest_remainder_count(weights_milli: "Mapping[str, int]", order: "Sequence[str]",
                            total: int) -> "dict[str, int]":
    """`weights_milli` sums to 1000 (enforced by whichever loader produced it — A-T1's tuning
    loader for real rows, `_flat_milli` below for the general-scope fallback). Distributes `total`
    whole units across `order` by largest remainder: `long`, widened before multiplying, divided
    by 1000 last, exactly once. Ties break on `order`'s own declared position — a total function,
    never dependent on `weights_milli`'s own dict iteration order.

    This is the ONE largest-remainder implementation in this adapter. The scope-level quota in
    `apportion_categories` needs the same math at a different scale (its weights are a SUM of many
    per-subject vectors, so they do not sum to 1000); rather than a second copy that could round or
    tie-break differently from the per-subject split it must agree with, `largest_remainder_raw`
    below is the general form and this is its fixed-base caller. A tie-break or overflow fix here
    therefore reaches both levels — the point of collapsing the 2026-09-11 review's duplication
    finding."""
    return largest_remainder_raw(weights_milli, order, total, base=1000)


def largest_remainder_raw(weights: "Mapping[str, int]", order: "Sequence[str]", total: int,
                          *, base: int) -> "dict[str, int]":
    """Largest-remainder apportionment of `total` units over `order` for weights of ANY scale,
    normalised by `base` (`sum(weights)` for an arbitrary-scale vector, `1000` for per-mille).
    `long`, widened before multiplying, one division per key, remainder units to the largest
    fractional parts with ties on `order`. Raises if `total` is negative; returns all zeros when
    the weights sum to nothing (nothing to be proportional to)."""
    if total < 0:
        raise ValueError("largest_remainder_raw: total must be non-negative")
    if base <= 0:
        return {k: 0 for k in order}
    scaled = {k: _widen_mul(int(weights[k]), total) for k in order}
    floor = {k: scaled[k] // base for k in order}
    remainder = total - sum(floor.values())
    fracs = sorted(order, key=lambda k: (-(scaled[k] % base), order.index(k)))
    out = dict(floor)
    for k in fracs[:remainder]:
        out[k] += 1
    return out


def expand_counts(counts: "Mapping[str, int]", order: "Sequence[str]") -> "list[str]":
    """A `{key: count}` allocation, flattened into a deterministic list of length `sum(counts)` —
    every `order[0]` slot before any `order[1]` slot. Spec §3 leaves the JOINT distribution of two
    independently-allocated vectors (category counts, target-mode counts) over one subject's
    ordinals unspecified; this is the simplest total function that turns two exact marginals into
    per-ordinal assignments without inventing a correlation the spec never states — acceptance
    #3/#4b test each marginal's exactness, not a joint one."""
    out: "list[str]" = []
    for k in order:
        out.extend([k] * counts[k])
    return out


def largest_remainder_apportion(raw_weights: "Mapping[str, int]", order: "Sequence[str]",
                                total: int) -> "dict[str, int]":
    """Largest-remainder apportionment of `total` units over `order` for raw weights of ANY scale
    (the caller's weights need not sum to 1000, unlike `largest_remainder_count`). Used for the
    SCOPE-level category quota, whose weights are the SUM of many per-subject vectors and therefore
    carry no fixed scale.

    Kept as a named wrapper (not deleted) because it is the exported name `coverage_report` and the
    tests call, and because it documents the arbitrary-scale case at the call site. It delegates to
    `largest_remainder_raw`, the single implementation, so it cannot drift from
    `largest_remainder_count`'s rounding or tie-break — collapsing the 2026-09-11 review's finding
    that this function duplicated and could disagree with the per-subject split it must match."""
    return largest_remainder_raw(raw_weights, order, total,
                                 base=sum(int(raw_weights[k]) for k in order))


def apportion_categories(
        rows: "Sequence[tuple[str, Mapping[str, int]]]", count: int,
        order: "Sequence[str]" = CATEGORIES) -> "dict[str, dict[str, int]]":
    """**The scope-level two-level category allocation — the fix for the inert per-subject planner.**

    The defect this replaces, measured 2026-09-11 over the live 904-species + 227-family roster: the
    planner apportioned `count` briefs across the five categories *per subject* by largest remainder
    over that subject's own `categoryMilli`. At `count == 5` a category needs weight >= 400 per-mille
    to win a second slot (`floor(400*5/1000) == 2`), while the shipped `base=1000, step=250` normalises
    to a maximum of 266 per-mille — so **every one of the 1,131 subjects produced the identical
    vector `1/1/1/1/1`**. The per-subject weight file was completely inert, and the aggregate share
    (attack 21.6% vs movement 18.8%) was thrown away. Worse, the quantization was perverse: `count=5`
    differentiated WORSE than `count=2` (1 distinct vector vs 10).

    The two levels:
    1. **Scope quota** (`largest_remainder_apportion` over the SUM of every subject's `categoryMilli`)
       — exact, and the thing A-S5's cell/`quotaDrift` metrics actually gate on.
    2. **Per-subject assignment** — start from each subject's own largest-remainder split (best row
       fidelity), then repair the column drift with deterministic swaps. Each swap moves one unit
       from a category over its quota to one under it, choosing the subject where the move best
       matches its own lean (`milli[u] - milli[o]` maximised, ties on row index). Every swap reduces
       `sum(|col[c] - Q[c]|)` by exactly 2, so the repair terminates with column sums equal to `Q`
       and every row still summing to `count`.

    `rows` is `(subjectKey, categoryMilli)` in the caller's canonical subject order — the allocation
    is a total function of that order and the weights, never of dict iteration order."""
    n = len(rows)
    if count == 0 or n == 0:
        return {key: {c: 0 for c in order} for key, _ in rows}

    aggregate = {c: 0 for c in order}
    for _key, milli in rows:
        for c in order:
            aggregate[c] += int(milli[c])
    quota = largest_remainder_apportion(aggregate, order, count * n)

    alloc = {key: largest_remainder_count(milli, order, count) for key, milli in rows}
    col = {c: 0 for c in order}
    for by_cat in alloc.values():
        for c in order:
            col[c] += by_cat[c]

    guard = 0
    # Provable termination bound, not a heuristic. Each swap moves one unit from a category over its
    # quota to one under it, so `sum(|col[c] - Q[c]|)` falls by exactly 2. The initial L1 drift is at
    # most `2 * count * n` (every unit in one category), so at most `count * n` swaps are ever needed.
    # The previous bound `4*n*len(order)+len(order)` was SMALLER than that once `count > 2*len(order)`
    # (i.e. above count 10) and was a latent false `did not converge` throw at a large per-subject
    # count; the real path is far below either bound, but the guard should not be able to lie.
    limit = count * n + len(order)
    while True:
        over = [c for c in order if col[c] > quota[c]]
        under = [c for c in order if col[c] < quota[c]]
        if not over and not under:
            break
        if not over or not under:                     # unreachable: both sides sum to count*n
            raise ValueError(f"category repair is inconsistent: col={col} quota={quota}")
        o, u = over[0], under[0]
        best_i, best_gain = None, None
        for i, (key, milli) in enumerate(rows):
            if alloc[key][o] < 1:
                continue
            gain = int(milli[u]) - int(milli[o])
            if best_gain is None or gain > best_gain:
                best_gain, best_i = gain, key
        if best_i is None:                            # unreachable: col[o] > quota[o] >= 0
            raise ValueError(f"category repair found no subject holding {o!r}")
        alloc[best_i][o] -= 1
        col[o] -= 1
        alloc[best_i][u] += 1
        col[u] += 1
        guard += 1
        if guard > limit:                             # unreachable: each swap cuts the L1 drift by 2
            raise ValueError(f"category repair did not converge within {limit} swaps")
    return alloc


# ---------------------------------------------------------------------------------------------
# §3 step 2 — anchor rows read from role-lean.json (species) and live seed family memberships
# enumerated by the caller (see generate_distribution_planner.py).
# ---------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class SpeciesAnchorRow:
    species_key: str
    family: "str | None"
    element_primary: str
    rarity: str
    theme_key: str
    motifs: "tuple[str, ...]"
    anti_motifs: "tuple[str, ...]"


def parse_species_anchor(role_lean_doc: dict) -> "dict[str, SpeciesAnchorRow]":
    out: "dict[str, SpeciesAnchorRow]" = {}
    for e in role_lean_doc["entries"]:
        out[e["speciesKey"]] = SpeciesAnchorRow(
            species_key=e["speciesKey"], family=e.get("family"),
            element_primary=e["element"]["primary"], rarity=e["rarity"], theme_key=e["themeKey"],
            motifs=tuple(e.get("motifs") or ()), anti_motifs=tuple(e.get("antiMotifs") or ()),
        )
    return out


# ---------------------------------------------------------------------------------------------
# §3 step 2b — family motif derivation: intersection -> majority -> frequency, total by
# construction. `familyAntiMotifs` is always the plain union (no fallback ladder needed — a union
# of non-empty sets is never empty unless every member itself carries none).
# ---------------------------------------------------------------------------------------------

def derive_family_motifs(member_rows: "Sequence[tuple[Sequence[str], Sequence[str]]]",
                         family_motif_max: int) -> "tuple[tuple[str, ...], tuple[str, ...], str]":
    """`member_rows`: one `(motifs, antiMotifs)` pair per family member, any order — every
    operation below is a set/counter op, so input order never affects the result. Returns
    `(familyMotifs, familyAntiMotifs, familyMotifBasis)`, both lists sorted byte-wise (Python's
    default string ordering IS byte-wise for valid UTF-8 — code-point order and UTF-8 byte order
    agree for every scalar value, which is the whole point of UTF-8's design)."""
    motif_sets = [set(m) for m, _ in member_rows]
    anti_union: "set[str]" = set()
    for _, a in member_rows:
        anti_union |= set(a)
    anti_motifs = tuple(sorted(anti_union))

    if not motif_sets:
        return (), anti_motifs, "intersection"

    inter = set.intersection(*motif_sets)
    if inter:
        return tuple(sorted(inter)), anti_motifs, "intersection"

    counts: "Counter[str]" = Counter()
    for s in motif_sets:
        counts.update(s)
    n = len(motif_sets)
    threshold = -(-n // 2)                        # ceil(n/2), no float division
    majority = {m for m, c in counts.items() if c >= threshold}
    if majority:
        return tuple(sorted(majority)), anti_motifs, "majority"

    ranked = sorted(counts.items(), key=lambda kv: (-kv[1], kv[0]))
    top = tuple(sorted(m for m, _ in ranked[:family_motif_max]))
    return top, anti_motifs, "frequency"


# ---------------------------------------------------------------------------------------------
# §3 step 4 / 4a — rung window, structure axes (union-to-ceiling), target-shape weights.
# ---------------------------------------------------------------------------------------------

# general [1,4], family [1,7], species(signature) [1,10] — the CEILING, never the floor, decides
# the structure-axis budget (§3 step 4's collapse rule: Rung = rungBand[1]).
RUN_WINDOW: "dict[str, tuple[int, int]]" = {"general": (1, 4), "family": (1, 7), "species": (1, 10)}

REACTION_AXIS = "reaction"
RESTRICTION_AXIS = "restriction"


def load_rung_table(path: Path) -> "dict[int, tuple[str, ...]]":
    doc = json.loads(path.read_text(encoding="utf-8"))
    rows = doc.get("rows")
    if doc.get("cap") != 10 or not isinstance(rows, list) or len(rows) != 10:
        raise ValueError(f"{path}: expected the shipped 10-row rung table (cap: 10)")
    return {row["rung"]: tuple(row["structureBudget"]) for row in rows}


def structure_axes_for(scope: str, rung_table: "Mapping[int, tuple[str, ...]]") -> "tuple[str, ...]":
    """The union-to-ceiling assignment (spec §3 step 5, review F3's correction): the axes budgeted
    at the WINDOW'S TOP rung, minus `reaction` (unspendable — `ActionKind` has exactly three
    members, none reaction-shaped, `StructureBudgetGuard.cs:27-30`)."""
    _, ceiling = RUN_WINDOW[scope]
    return tuple(a for a in rung_table[ceiling] if a != REACTION_AXIS)


def validate_rung_band(scope: str, band: "Sequence[int]") -> None:
    """Every emitted `rungBand` equals its scope's WHOLE window, floor included (spec §3 step 4:
    the signature floor is dropped, so the window is `[1, 10]`, never `[5, 10]`). A band with a
    floor above 1 is refused, naming `spec-rung-semantics.md` §3.2."""
    expected = list(RUN_WINDOW[scope])
    if list(band) != expected:
        raise ValueError(
            f"rungBand {list(band)!r} does not match scope {scope!r}'s window {expected!r} -- "
            f"a floor above 1 is refused (spec-rung-semantics.md SS3.2: the signature floor was "
            f"dropped, the window is [1, 10], never [5, 10])")


def validate_structure_axes(axes: "Sequence[str]") -> None:
    """A brief naming `reaction` is REFUSED, never flagged (spec §4, §5, acceptance #7; review
    F3/F4's correction over the earlier "report it" wording)."""
    if REACTION_AXIS in axes:
        raise ValueError(
            f"structureAxes names {REACTION_AXIS!r} -- unspendable per StructureBudgetGuard.cs:"
            f"27-30 (ActionKind has exactly three members, none reaction-shaped) -- refused")


# Genuine editorial judgment call — see this module's own docstring for the full account. Not a
# magnitude, weight or probability (so it never trips the schema audit); flagged here plainly.
CATEGORY_RELATION: "dict[str, str]" = {
    "attack": "enemy", "defense": "self", "support": "ally", "movement": "self", "status": "enemy",
}
assert set(CATEGORY_RELATION) == set(CATEGORIES), "every category needs a relation"
assert set(CATEGORY_RELATION.values()) <= RELATIONS, "every mapped relation must be real vocabulary"


@dataclass(frozen=True)
class WeightsRow:
    category_milli: "dict[str, int]"
    target_mode_milli: "dict[str, int]"
    area_shape_milli: "dict[str, int]"


def parse_type_weights(doc: dict) -> "dict[tuple[str, str], WeightsRow]":
    out: "dict[tuple[str, str], WeightsRow]" = {}
    for e in doc["entries"]:
        out[(e["scope"], e["scopeKey"])] = WeightsRow(
            category_milli=dict(e["categoryMilli"]), target_mode_milli=dict(e["targetModeMilli"]),
            area_shape_milli=dict(e["areaShapeMilli"]),
        )
    return out


def _flat_milli(order: "Sequence[str]") -> "dict[str, int]":
    """The uniform fallback for the general scope, which A-T1 never rows (`type-weights.json`
    carries species+family only, 103 rows). Structural, not balance-surface: `1000 // n` plus the
    exact remainder to the first members in declared order is the same "no differentiating
    signal" arithmetic identity `characteristic_pool.derive.is_five_way_tie`'s own flat "floor"
    case already produces — never a hand-tuned number."""
    n = len(order)
    base = 1000 // n
    out = {k: base for k in order}
    remainder = 1000 - base * n
    for k in list(order)[:remainder]:
        out[k] += 1
    return out


GENERAL_WEIGHTS = WeightsRow(
    category_milli=_flat_milli(CATEGORIES), target_mode_milli=_flat_milli(TARGET_MODES),
    area_shape_milli=_flat_milli(AREA_SHAPES),
)


# ---------------------------------------------------------------------------------------------
# §3 step 7 — allowedAtomFamilies / forbiddenAtomFamilies. Constraint 4: the SAME eligible set
# for every tier, only forbiddenAtomFamilies narrows, and only via the multiplicative-pairs rule.
# ---------------------------------------------------------------------------------------------

def build_pool(family_ids: "frozenset[str]",
              multiplicative_pairs: "Sequence[tuple[str, str]]") -> "tuple[tuple[str, ...], tuple[str, ...]]":
    """UPDATED 2026-09-04 (A-G1, spec-tier-access-gate.md): the C# module built two of constraint 4's
    three gates -- a per-rung `powerBudgetMilli` row now exists (`data/tuning/action-rungs.v2.json`)
    and `ContentValidation.Budget`'s rung-keyed overload has a real production caller
    (`RpgStore.BuildActionCatalog`, `spec-tier-access-gate.md` §3.2). The THIRD gate -- a
    family-aware, non-additive price -- is still blocked on D2 (multiplicative pricing, open per
    `definitions.md` §13), so this function's own contract is unchanged: the SAME eligible set for
    every tier, always, regardless of how many of the three gates are open. Two of three is not
    three -- see `validate_no_family_widening` below, whose own refusal does not read gate status
    either, for the same reason."""
    allowed = tuple(sorted(family_ids))
    forbidden_set: "set[str]" = set()
    for a, b in multiplicative_pairs:
        forbidden_set.add(a)
        forbidden_set.add(b)
    return allowed, tuple(sorted(forbidden_set))


def validate_no_family_widening(allowed_by_tier: "Mapping[str, Sequence[str]]") -> None:
    """Planted-violation guard: a plan that narrows `allowedAtomFamilies` per tier is refused
    unconditionally -- this function does not read which of constraint 4's three gates are open,
    because two of three (as of A-G1, 2026-09-04) is not the same as C1 being enabled. The third
    gate (a family-aware non-additive price, needing D2) is what keeps this refusal live; it stays
    unconditional so landing the other two gates can never flip this function's own behaviour by
    accident.

    NOTE: as of 2026-09-04 this function itself has no caller in `generate_distribution_planner.py`'s
    real pipeline -- `build_pool` above enforces the same rule structurally (one `allowed_families`
    tuple computed once per run and threaded into every scope), so the actual guarantee in production
    is "no code path accepts a per-tier family set" rather than "a call to this function refused one".
    Tested directly (`test_distribution_planner.py`), but wiring it into the real pipeline as a second,
    explicit check is unclaimed work, not this module's."""
    distinct = {frozenset(v) for v in allowed_by_tier.values()}
    if len(distinct) > 1:
        raise ValueError(
            "allowedAtomFamilies differs across tiers -- C1's family-access widening stays refused "
            "until ALL THREE of constraint 4's gates are open (a per-rung powerBudgetMilli row and a "
            "budget check with a production caller now exist, A-G1; a family-aware non-additive price "
            "needing D2 does not) -- refused")


def validate_atom_family_namespace(ids: "Sequence[str]", family_ids: "frozenset[str]") -> None:
    """Every id must be one of the 98 authored affix families (`data/seed/items/affix-families/
    *.json`, `entries[].id`) -- the 17 fixture rows under `data/seed/atoms/` are never eligible."""
    for fam_id in ids:
        if fam_id not in family_ids:
            raise ValueError(
                f"{fam_id!r} is not one of the 98 authored affix families under "
                f"data/seed/items/affix-families/*.json -- refused (a fixture id under "
                f"data/seed/atoms/ is never eligible)")


def validate_pairing_vocabulary(paired_payoff_family: "str | None", pairing_keys: "frozenset[str]",
                                statuses: "frozenset[str]" = STATUSES) -> None:
    if paired_payoff_family is None:
        return
    if paired_payoff_family in statuses:
        raise ValueError(
            f"pairedPayoffFamily={paired_payoff_family!r} is a STATUS id -- the shipped pairing "
            f"surface keys on atom families only (EnablerPayoffPairings.cs:26,30-31), never a "
            f"status -- refused")
    if paired_payoff_family not in pairing_keys:
        raise ValueError(
            f"pairedPayoffFamily={paired_payoff_family!r} is not a key of pairings.json -- refused")


def validate_no_multiplicative_conflict(allowed: "Sequence[str]", forbidden: "Sequence[str]",
                                        pairs: "Sequence[tuple[str, str]]") -> None:
    """A brief whose EFFECTIVE pool (allowed minus forbidden) still holds BOTH halves of a
    configured multiplicative pair is refused, naming both ids -- the one narrowing rule step 7
    says is always applied."""
    effective = set(allowed) - set(forbidden)
    for a, b in pairs:
        if a in effective and b in effective:
            raise ValueError(
                f"{a!r} and {b!r} are both in the effective pool -- a known multiplicative pair "
                f"(pricing is additive there) may never co-occur unforbidden in one brief")


# ---------------------------------------------------------------------------------------------
# §3 step 6 — pairing role assignment, over ATOM FAMILIES, never statuses. `role` is optional;
# `none` is the common case and a required key on every brief regardless.
# ---------------------------------------------------------------------------------------------

def load_pairing_table(path: Path) -> "dict[str, tuple[str, ...]]":
    doc = json.loads(path.read_text(encoding="utf-8"))
    return {k: tuple(v) for k, v in doc.items()}


@dataclass(frozen=True)
class PairingAssignment:
    role: str
    paired_payoff_family: "str | None"
    forced_enabler: "str | None"          # folded into that ONE brief's own allowedAtomFamilies


def assign_pairing_roles(ordinal_count: int, allowed_universe: "frozenset[str]",
                         pairing_table: "Mapping[str, Sequence[str]]") -> "list[PairingAssignment]":
    """Every brief starts `role: 'none'`. For each payoff key reachable from the shared
    `allowed_universe` (sorted, for determinism), the NEXT two unused ordinals in the subject's
    group are assigned `payoff` then `enabler` with the same `pairedPayoffFamily` — the plan-side
    twin of `EnablerPayoffCoverage.Check` (`EnablerPayoffCoverage.cs:21-34`), assigned rather than
    hoped for. This module never invents a pairing key: `pairing_table` is read verbatim from
    `pairings.json` (production) or a synthetic fixture (tests) — never hard-coded here. Against
    the live pairings table, `role: 'none'` for every brief is the correct output when no payoff
    key exists in the live atom-family namespace (spec §2)."""
    out = [PairingAssignment("none", None, None) for _ in range(ordinal_count)]
    reachable_payoffs = sorted(k for k in pairing_table if k in allowed_universe)
    cursor = 0
    for payoff_family in reachable_payoffs:
        reachable_enablers = [e for e in pairing_table[payoff_family] if e in allowed_universe]
        if not reachable_enablers or cursor + 1 >= ordinal_count:
            continue
        out[cursor] = PairingAssignment("payoff", payoff_family, None)
        out[cursor + 1] = PairingAssignment("enabler", payoff_family, reachable_enablers[0])
        cursor += 2
    return out


def validate_pairing_coverage(group_briefs: "Sequence[Mapping[str, object]]") -> None:
    """The plan-side twin of `EnablerPayoffCoverage.Check` (`EnablerPayoffCoverage.cs:21-34`):
    every `payoff` brief in a `(scope, scopeKey)` group must have a sibling `enabler` brief
    carrying the same `pairedPayoffFamily`. `assign_pairing_roles` always satisfies this by
    construction; this validator exists to CATCH a hand-assembled or corrupted plan that does
    not — the planted-violation shape spec §5's testing table names."""
    enablers_by_family: "dict[str, list]" = {}
    for b in group_briefs:
        pairing = b["pairing"]
        if pairing["role"] == "enabler":
            enablers_by_family.setdefault(pairing["pairedPayoffFamily"], []).append(b)
    for b in group_briefs:
        pairing = b["pairing"]
        if pairing["role"] != "payoff":
            continue
        fam = pairing["pairedPayoffFamily"]
        if not enablers_by_family.get(fam):
            raise ValueError(
                f"payoff brief {b.get('briefId', '?')!r} (pairedPayoffFamily={fam!r}) has no "
                f"sibling enabler brief in its (scope, scopeKey) group -- refused")


# ---------------------------------------------------------------------------------------------
# The schema audit — spec §4/§5: never a magnitude, weight, probability or duration anywhere in a
# brief. `slot.rungBand` is the ONE place a plain int pair is legal (table indices, not a
# magnitude); every other numeric leaf, and every numeric-string leaf (bare or inside a list), is
# refused.
# ---------------------------------------------------------------------------------------------

_NUMERIC_STRING_RE = re.compile(r"^[0-9]+$")


#: `_provenance` is envelope/infrastructure metadata (a corpus hash, a round number, version
#: ints) common to every file this program writes — the same shape `type-weights.json`'s own
#: `_meta.tuningVersion` carries. It is never one of the brief's OWN mechanical fields (groups
#: B-F: anchor/slot/pool/pairing/avoidNeighbours), so the schema audit does not walk it.
_AUDIT_EXEMPT_TOP_LEVEL_KEYS = frozenset({"_provenance"})


def audit_no_magnitude_smuggling(brief: dict) -> None:
    def check_leaf(value: object, path: "tuple[str, ...]") -> None:
        label = ".".join(path)
        if value is None or isinstance(value, bool):
            return                                     # a null or a structural flag, never a magnitude
        if isinstance(value, int):
            raise ValueError(f"{label}: bare numeric field {value!r} refused -- a brief may carry "
                             f"no magnitude outside slot.rungBand")
        if isinstance(value, float):
            raise ValueError(f"{label}: bare float field {value!r} refused")
        if isinstance(value, str) and _NUMERIC_STRING_RE.match(value):
            raise ValueError(f"{label}: numeric-string field {value!r} refused")

    def walk(node: object, path: "tuple[str, ...]") -> None:
        if isinstance(node, dict):
            for k, v in node.items():
                if not path and k in _AUDIT_EXEMPT_TOP_LEVEL_KEYS:
                    continue
                walk(v, path + (k,))
        elif isinstance(node, list):
            if path and path[-1] == "rungBand":
                for v in node:
                    if not isinstance(v, int) or isinstance(v, bool):
                        raise ValueError(f"{'.'.join(path)}: rungBand must be a list of plain ints")
                return
            for i, v in enumerate(node):
                walk(v, path + (f"[{i}]",))
        else:
            check_leaf(node, path)

    walk(brief, ())


# ---------------------------------------------------------------------------------------------
# §3 step 1 — the full-run refusal. `mode: "full"` needs `--full` AND passing smoke-gate evidence
# (A-S5's coverage report); `--full` alone is necessary but not sufficient, matching the spec's
# own "refuses... naming the missing evidence" testing-strategy line.
# ---------------------------------------------------------------------------------------------

SMOKE_GATE_EVIDENCE_NOTE = (
    "a passing quality-gate report from A-S5 (coverage-report); --full is necessary but not sufficient "
    "(spec-distribution-planner.md SS3 step 1)"
)


def refuse_full_run_if_ungated(mode: str, full_flag: bool, gate_evidence_present: bool) -> None:
    if mode != "full":
        return
    if not full_flag or not gate_evidence_present:
        raise ValueError(f"mode: 'full' refused -- missing {SMOKE_GATE_EVIDENCE_NOTE}")


# ---------------------------------------------------------------------------------------------
# §3 step 2b's anchor rendering, and the whole-brief assembly (§3 steps 3-9 orchestrated).
# ---------------------------------------------------------------------------------------------

def brief_anchor(scope: str, scope_key: "str | None", species: "SpeciesAnchorRow | None",
                 family_motifs: "tuple[str, ...]" = (), family_anti_motifs: "tuple[str, ...]" = (),
                 family_motif_basis: "str | None" = None) -> dict:
    """Group B — read from A-S0's output, never invented. Species-only fields (`element`,
    `rarity`, `themeKey`, `motifs`, `antiMotifs`) are honestly null/empty for `family` and
    `general` scope, which have no species of their own; `familyMotifs`/`familyAntiMotifs`/
    `familyMotifBasis` are present as KEYS on every `family`-scoped brief only (acceptance #7b),
    never invented for the other two scopes, which have no family-level derivation to carry."""
    if scope == "species":
        assert species is not None
        return {
            "family": species.family, "element": species.element_primary, "rarity": species.rarity,
            "themeKey": species.theme_key, "motifs": list(species.motifs),
            "antiMotifs": list(species.anti_motifs),
        }
    if scope == "family":
        return {
            "family": scope_key, "element": None, "rarity": None, "themeKey": None,
            "motifs": [], "antiMotifs": [],
            "familyMotifs": list(family_motifs), "familyAntiMotifs": list(family_anti_motifs),
            "familyMotifBasis": family_motif_basis,
        }
    return {"family": None, "element": None, "rarity": None, "themeKey": None,
           "motifs": [], "antiMotifs": []}


def plan_subject(*, scope: str, scope_key: "str | None", count: int, weights: WeightsRow,
                 rung_table: "Mapping[int, tuple[str, ...]]", allowed_families: "tuple[str, ...]",
                 forbidden_pair_ids: "tuple[str, ...]", multiplicative_pairs: "Sequence[tuple[str, str]]",
                 pairing_table: "Mapping[str, Sequence[str]]", anchor: dict, corpus_hash: str,
                 tuning_version: int, round_no: int, prompt_version: int,
                 accepted_neighbours: "Sequence[tuple[str, FingerprintComponents]]" = (),
                 avoid_neighbour_k: int = 0,
                 category_counts: "Mapping[str, int] | None" = None) -> "list[dict]":
    """§3 steps 3-9 for ONE subject (a species, a family, or the single general subject). Ordinals
    are always subject-local, starting at 1 — `briefId` embeds `scope`+`scopeKey`, so a global
    counter would be redundant and order-dependent for no reason.

    `category_counts` is the subject's slice of the SCOPE-level allocation
    (`apportion_categories`), threaded in by `plan_round`. When omitted the function falls back to
    a per-subject largest-remainder split, which is correct for a lone subject (the general scope,
    or a test fixture) but inert at the shipped `count == 5` — see `apportion_categories` for the
    measured defect that makes the scope-level path the production one."""
    if count == 0:
        return []

    if category_counts is None:
        category_counts = largest_remainder_count(weights.category_milli, CATEGORIES, count)
    else:
        category_counts = {c: int(category_counts[c]) for c in CATEGORIES}
        if sum(category_counts.values()) != count:
            raise ValueError(f"category_counts {dict(category_counts)!r} does not sum to {count}")
    target_counts = largest_remainder_count(weights.target_mode_milli, TARGET_MODES, count)
    category_seq = expand_counts(category_counts, CATEGORIES)
    target_seq = expand_counts(target_counts, TARGET_MODES)

    area_count = target_counts.get("area", 0)
    area_counts = (largest_remainder_count(weights.area_shape_milli, AREA_SHAPES, area_count)
                  if area_count else {k: 0 for k in AREA_SHAPES})
    area_seq = expand_counts(area_counts, AREA_SHAPES)

    rung_window = list(RUN_WINDOW[scope])
    structure_axes = structure_axes_for(scope, rung_table)
    validate_structure_axes(structure_axes)
    structure_enforced = RESTRICTION_AXIS not in structure_axes

    allowed_universe = frozenset(allowed_families)
    # ⛔ Species scope never pairs (2026-09-06 finding). `assign_pairing_roles` greedily claims a
    # subject's FIRST two ordinals for the first reachable payoff -- fine for `family`/`general`
    # (few subjects, a shared audience a pairing theme can legitimately dominate a slice of), but
    # a species subject's own count is small enough that the first reachable payoff consumes its
    # ENTIRE budget, forcing every species into the IDENTICAL pairing and
    # crowding out the per-species distinctiveness the game-design docs name as the point of
    # species scope at all (seedsmith-design Step 2, "distinctness is carried by abilities").
    # Species subjects always stay `role: none`; pairing content lives at family/general scope.
    pairing_assignments = (
        [PairingAssignment("none", None, None) for _ in range(count)] if scope == "species"
        else assign_pairing_roles(count, allowed_universe, pairing_table)
    )

    id_key = scope_key if scope_key is not None else "general"
    briefs: "list[dict]" = []
    area_cursor = 0
    # Render the accepted-neighbour fingerprints ONCE, not once per brief. `accepted_neighbours` is
    # identical for every ordinal of this subject, so re-rendering and re-sorting it inside the loop
    # was `count x group_size` work per subject — quadratic as the accepted corpus accumulates rounds
    # (the 2026-09-11 review's finding). `render_fingerprint` returns a plain tuple, so this is a
    # pure hoist with identical output.
    rendered_neighbours = [(aid, render_fingerprint(fp)) for aid, fp in accepted_neighbours]
    for i in range(count):
        ordinal = i + 1
        category = category_seq[i]
        target_mode = target_seq[i]
        if target_mode == "area":
            area_shape = area_seq[area_cursor]
            area_cursor += 1
        else:
            area_shape = None
        relation = CATEGORY_RELATION[category]

        pa = pairing_assignments[i]
        this_allowed = allowed_families
        if pa.forced_enabler and pa.forced_enabler not in this_allowed:
            this_allowed = tuple(sorted(set(this_allowed) | {pa.forced_enabler}))
        validate_no_multiplicative_conflict(this_allowed, forbidden_pair_ids, multiplicative_pairs)
        validate_pairing_vocabulary(pa.paired_payoff_family, frozenset(pairing_table), STATUSES)
        assert pa.role in PAIRING_ROLES

        fp_components = FingerprintComponents(
            atom_families=this_allowed, category=category, target_mode=target_mode,
            area_shape=area_shape, relation=relation, structure_axes=structure_axes,
            pairing_role=pa.role,
        )
        target_fp = render_fingerprint(fp_components)
        neighbours = k_nearest(target_fp, rendered_neighbours, avoid_neighbour_k)
        avoid_neighbours = [{"actionId": aid, "fingerprint": render_fingerprint_string(fp_components)}
                           for aid, _dist in neighbours]

        brief_id = f"brief.{scope}.{id_key}.{ordinal:03d}"
        briefs.append({
            # `id` is the field `seedsmith.corpus.model.Corpus.load` and `kinds.py`'s own
            # `action-brief` KindSpec (`required={"id"}`) actually key on -- without it this
            # module's own output would be silently invisible to `Corpus.load`/`discover_edges`,
            # never appearing in the graph at all. `briefId` is kept alongside it, identical in
            # value, matching spec §2's own worked example verbatim -- a deliberate, documented
            # duplication (infra compatibility over "one field one name" here, since the infra
            # contract is load-bearing and the spec's example predates it).
            "id": brief_id,
            "briefId": brief_id,
            "scope": scope,
            "scopeKey": scope_key,
            "anchor": anchor,
            "slot": {
                "category": category, "targetMode": target_mode, "areaShape": area_shape,
                "relation": relation, "kind": None,
                "rungBand": rung_window, "structureAxes": list(structure_axes),
                "structureEnforced": structure_enforced,
            },
            "pool": {"allowedAtomFamilies": list(this_allowed),
                    "forbiddenAtomFamilies": list(forbidden_pair_ids)},
            # `forcedEnabler` (A-S7 dependency, spec-coverage-assignment.md §3): the exact
            # `PairingAssignment.forced_enabler` already computed above -- serialized here so a
            # downstream module can know WHICH family was forced without re-deriving
            # `assign_pairing_roles`'s own tie-break logic. `None` for every `payoff`/`none` role.
            "pairing": {"role": pa.role, "pairedPayoffFamily": pa.paired_payoff_family,
                       "forcedEnabler": pa.forced_enabler},
            "avoidNeighbours": avoid_neighbours,
            "_provenance": {"corpusHash": corpus_hash, "promptVersion": prompt_version,
                           "round": round_no, "tuningVersion": tuning_version},
        })

    for b in briefs:
        validate_rung_band(b["scope"], b["slot"]["rungBand"])
    validate_pairing_coverage(briefs)
    return briefs


def plan_round(*, species_ids: "Sequence[str]", family_members: "Mapping[str, Sequence[str]]",
               species_anchor: "Mapping[str, SpeciesAnchorRow]",
               weights_by_key: "Mapping[tuple[str, str], WeightsRow]",
               rung_table: "Mapping[int, tuple[str, ...]]", family_ids: "frozenset[str]",
               pairing_table: "Mapping[str, Sequence[str]]", general_count: int,
               per_species_count: int, per_family_count: int,
               multiplicative_pairs: "Sequence[tuple[str, str]]", family_motif_max: int,
               corpus_hash: str, tuning_version: int, round_no: int = 1,
               prompt_version: int = 1,
               accepted_neighbours_by_group: "Mapping[tuple[str, str | None], Sequence[tuple[str, FingerprintComponents]]] | None" = None,
               avoid_neighbour_k: int = 0) -> "list[dict]":
    """§3 steps 2-9 over the whole roster. Subject order: general (one pseudo-subject), then the
    every live species in seed order (`species_ids`, as the caller already ordered it), then the
    consolidated families in sorted order (a total order over family ids — neither dict nor filesystem
    iteration order, matching spec §4's own "never let ordinal assignment depend on..." rule).

    **§3 step 8's `avoidNeighbours` is threaded here, not silently defaulted.** `plan_subject`
    always had the parameters, but this orchestrator never passed them, so every brief shipped an
    empty `avoidNeighbours` list (measured 2026-09-11: 0 of 5,680). The accepted corpus is the
    caller's job to read (`generate_distribution_planner._accepted_neighbours_by_group`), grouped by
    `(scope, scopeKey)`; round 1 legitimately reads none, and `avoid_neighbour_k` comes from A-S3's
    `action-dedup.v1.json` via `load_dedup_k` (default 8)."""
    allowed_families, forbidden_pair_ids = build_pool(family_ids, multiplicative_pairs)
    groups = accepted_neighbours_by_group or {}

    def neighbours_for(scope: str, scope_key: "str | None"):
        return groups.get((scope, scope_key), ())

    briefs: "list[dict]" = []

    if general_count:
        anchor = brief_anchor("general", None, None)
        briefs.extend(plan_subject(
            scope="general", scope_key=None, count=general_count, weights=GENERAL_WEIGHTS,
            rung_table=rung_table, allowed_families=allowed_families,
            forbidden_pair_ids=forbidden_pair_ids, multiplicative_pairs=multiplicative_pairs,
            pairing_table=pairing_table, anchor=anchor, corpus_hash=corpus_hash,
            tuning_version=tuning_version, round_no=round_no, prompt_version=prompt_version,
            accepted_neighbours=neighbours_for("general", None), avoid_neighbour_k=avoid_neighbour_k,
        ))

    # Scope-level two-level allocation (see `apportion_categories`): one exact aggregate quota per
    # scope, split back across subjects with a lean-aware repair. The subject list is the caller's
    # canonical order, so the allocation is a total function of it and the weights.
    species_subjects = [
        (species_id, weights_by_key[("species", species_id)].category_milli)
        for species_id in species_ids
        if species_anchor.get(species_id) is not None and ("species", species_id) in weights_by_key
    ]
    species_categories = apportion_categories(species_subjects, per_species_count)
    for species_id, _milli in species_subjects:
        row = species_anchor[species_id]
        weights = weights_by_key[("species", species_id)]
        anchor = brief_anchor("species", species_id, row)
        briefs.extend(plan_subject(
            scope="species", scope_key=species_id, count=per_species_count, weights=weights,
            rung_table=rung_table, allowed_families=allowed_families,
            forbidden_pair_ids=forbidden_pair_ids, multiplicative_pairs=multiplicative_pairs,
            pairing_table=pairing_table, anchor=anchor, corpus_hash=corpus_hash,
            tuning_version=tuning_version, round_no=round_no, prompt_version=prompt_version,
            accepted_neighbours=neighbours_for("species", species_id),
            avoid_neighbour_k=avoid_neighbour_k,
            category_counts=species_categories[species_id],
        ))

    family_subjects = [
        (family_id, weights_by_key[("family", family_id)].category_milli)
        for family_id in sorted(family_members)
        if ("family", family_id) in weights_by_key
    ]
    family_categories = apportion_categories(family_subjects, per_family_count)
    for family_id, _milli in family_subjects:
        members = family_members[family_id]
        member_rows = [(species_anchor[m].motifs, species_anchor[m].anti_motifs)
                      for m in members if m in species_anchor]
        fam_motifs, fam_anti, fam_basis = derive_family_motifs(member_rows, family_motif_max)
        weights = weights_by_key[("family", family_id)]
        anchor = brief_anchor("family", family_id, None, fam_motifs, fam_anti, fam_basis)
        briefs.extend(plan_subject(
            scope="family", scope_key=family_id, count=per_family_count, weights=weights,
            rung_table=rung_table, allowed_families=allowed_families,
            forbidden_pair_ids=forbidden_pair_ids, multiplicative_pairs=multiplicative_pairs,
            pairing_table=pairing_table, anchor=anchor, corpus_hash=corpus_hash,
            tuning_version=tuning_version, round_no=round_no, prompt_version=prompt_version,
            accepted_neighbours=neighbours_for("family", family_id),
            avoid_neighbour_k=avoid_neighbour_k,
            category_counts=family_categories[family_id],
        ))

    return briefs
