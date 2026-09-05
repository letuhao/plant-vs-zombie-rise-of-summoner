"""seedsmith.adapters.items.combogen.tuning — the pure parser over BOTH tuning files.

⚠ **Two files, one view, and the split is the point.** `data/tuning/sockets.v1.json` is module 16's
and already carries D20's ingredient count, the per-actor backstop, the attuned tier bonus, the
structural ceiling and the fifteen per-role ceilings. `data/tuning/strain-splice.v1.json` is this
module's and carries only what module 16 does not own. Reading both here — rather than copying six
values into one file — is what stops the generator and the runtime evaluator disagreeing about how
many ingredients a Strain takes.

No key has a default. A missing one raises at load, because a generator silently running on a
default is how an unreviewed number reaches 102 entries (module 13's own reasoning, restated because
this parser is a second instance of it, not a reference to it).
"""
from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[6]
STRAIN_SPLICE_PATH = REPO_ROOT / "data" / "tuning" / "strain-splice.v1.json"
SOCKETS_PATH = REPO_ROOT / "data" / "tuning" / "sockets.v1.json"

#: Keys this module must never define, because module 16 already does. Checked at load against the
#: strain-splice file's own text, so a well-meaning copy-paste fails instead of forking the value.
SOCKETS_OWNED_KEYS: "tuple[str, ...]" = (
    "maxCombosPerActor", "attunedTierBonus", "ingredientCount", "structuralCeiling",
    "socketCeiling", "attunedEffectiveCountBonus",
)


class ComboTuningError(ValueError):
    """A tuning file is structurally unusable for combination generation. Raised at load, so a
    defect lands before the first model call rather than on the hundredth combination."""


@dataclass(frozen=True)
class ComboTuning:
    # --- from sockets.v1.json (module 16's) ---
    ingredient_count: int
    structural_ceiling: int
    max_combos_per_actor: int
    attuned_tier_bonus: int
    insert_tier_count: int
    socket_ceiling: "dict[str, int]"

    # --- from strain-splice.v1.json (this module's) ---
    min_tier_plan: "tuple[int, ...]"
    base_tier: "dict[str, int]"
    catalogue_size_bar: int
    exact_duplicate_names_max: int
    near_duplicate_rate_max_permille: int

    def host_roles(self) -> "tuple[str, ...]":
        """The roles whose ceiling reaches D20's ingredient count — the only chassis a Strain or a
        Splice can ever live in. The Python mirror of `SocketGeometry.RolesThatCanHostAStrain`;
        a test asserts the two agree against the same shipped file.
        """
        return tuple(sorted(r for r, c in self.socket_ceiling.items()
                            if c >= self.ingredient_count))

    def geometric_combo_ceiling(self, host_roles: "tuple[str, ...] | None" = None) -> int:
        """How many Strains/Splices one actor can actually wear today.

        One per item (module 16's evaluator caps it), and only on a role that can hold four
        ingredients — so the real ceiling is the number of such roles, NOT `max_combos_per_actor`.
        Reported rather than enforced: `max_combos_per_actor` is the backstop that only starts
        binding if the four-ceiling set ever widens past it.
        """
        return len(host_roles if host_roles is not None else self.host_roles())

    def base_tier_for(self, combination_kind: str) -> int:
        if combination_kind not in self.base_tier:
            raise ComboTuningError(
                f"no baseTier row for combination kind {combination_kind!r} — the rows are "
                f"{sorted(self.base_tier)}")
        return self.base_tier[combination_kind]


def _require(doc: dict, source: str, *path: str):
    node = doc
    for key in path:
        if not isinstance(node, dict) or key not in node:
            raise ComboTuningError(
                f"{source} is missing {'.'.join(path)!r} — refusing to substitute a default; an "
                f"unreviewed number here reaches every generated combination")
        node = node[key]
    return node


def load(strain_splice_path: "Path | None" = None,
         sockets_path: "Path | None" = None) -> ComboTuning:
    ss_path = strain_splice_path or STRAIN_SPLICE_PATH
    sk_path = sockets_path or SOCKETS_PATH
    ss_text = ss_path.read_text(encoding="utf-8")
    ss = json.loads(ss_text)
    sk = json.loads(sk_path.read_text(encoding="utf-8"))

    _refuse_forked_keys(ss, ss_path.name)

    tuning = ComboTuning(
        ingredient_count=int(_require(sk, sk_path.name, "strainSplice", "ingredientCount")),
        structural_ceiling=int(_require(sk, sk_path.name, "structuralCeiling")),
        max_combos_per_actor=int(_require(sk, sk_path.name, "maxCombosPerActor")),
        attuned_tier_bonus=int(_require(sk, sk_path.name, "resonance", "attunedTierBonus")),
        insert_tier_count=int(_require(sk, sk_path.name, "insertTiers", "count")),
        socket_ceiling={str(k): int(v)
                        for k, v in _require(sk, sk_path.name, "socketCeiling").items()},
        min_tier_plan=tuple(int(t) for t in _require(ss, ss_path.name, "recipe", "minTierPlan")),
        base_tier={str(k): int(v)
                   for k, v in _require(ss, ss_path.name, "recipe", "baseTier").items()},
        catalogue_size_bar=int(_require(ss, ss_path.name, "learnability", "catalogueSizeBar")),
        exact_duplicate_names_max=int(
            _require(ss, ss_path.name, "distinctness", "exactDuplicateNamesMax")),
        near_duplicate_rate_max_permille=int(
            _require(ss, ss_path.name, "distinctness", "nearDuplicateRateMaxPermille")),
    )
    _validate(tuning)
    return tuning


def _keys(node, out: "set[str]") -> None:
    if isinstance(node, dict):
        for key, value in node.items():
            out.add(key)
            _keys(value, out)
    elif isinstance(node, list):
        for value in node:
            _keys(value, out)


def _refuse_forked_keys(doc: dict, name: str) -> None:
    """A value module 16 owns, re-declared here, is a fork — and a forked ingredient count is how a
    generator authors 102 combinations the runtime evaluator can never match.

    Walks the KEYS at every depth rather than searching the file text: the ownership note in this
    very file names all six of them in prose, and a substring search would refuse the document that
    explains why they are absent.
    """
    present: "set[str]" = set()
    _keys(doc, present)
    found = [k for k in SOCKETS_OWNED_KEYS if k in present]
    if found:
        raise ComboTuningError(
            f"{name} declares {found}, which data/tuning/sockets.v1.json already owns (module 16). "
            f"Two sources of truth for an ingredient count or an attunement bonus is how a "
            f"generated combination stops matching the evaluator that has to fire it — read them "
            f"from sockets.v1.json instead")


def _validate(t: ComboTuning) -> None:
    """The structural invariants, each with its own message so a balance pass reads which one it
    broke."""
    if t.ingredient_count < 1:
        raise ComboTuningError(
            f"strainSplice.ingredientCount {t.ingredient_count} is below 1 — a combination with no "
            f"ingredients fires on an empty item")
    if t.ingredient_count > t.structural_ceiling:
        raise ComboTuningError(
            f"strainSplice.ingredientCount {t.ingredient_count} exceeds structuralCeiling "
            f"{t.structural_ceiling} — no item could ever hold one, so all 102 would be inert")
    if len(t.min_tier_plan) != t.ingredient_count:
        raise ComboTuningError(
            f"recipe.minTierPlan has {len(t.min_tier_plan)} entries but D20 fixes the ingredient "
            f"count at {t.ingredient_count} — the plan is zipped onto the ingredients, so a "
            f"length mismatch silently drops or invents a min tier")
    if list(t.min_tier_plan) != sorted(t.min_tier_plan):
        raise ComboTuningError(
            f"recipe.minTierPlan {list(t.min_tier_plan)} is not ascending — it is zipped onto the "
            f"ingredient multiset sorted by family id, so the order decides which duplicate gets "
            f"the cheaper tier and is load-bearing")
    for tier in t.min_tier_plan:
        if not (1 <= tier <= t.insert_tier_count):
            raise ComboTuningError(
                f"recipe.minTierPlan names tier {tier}, outside the shipped insert ladder "
                f"[1..{t.insert_tier_count}] (sockets.v1.json insertTiers.count) — an ingredient "
                f"no insert can satisfy makes the combination unbuildable")
    if not t.base_tier:
        raise ComboTuningError("recipe.baseTier is empty — every combination kind needs a tier")
    for kind, tier in t.base_tier.items():
        if tier < 1:
            raise ComboTuningError(
                f"recipe.baseTier.{kind} is {tier}; a granted tier below 1 grants nothing")
    if t.attuned_tier_bonus < 0:
        raise ComboTuningError(
            f"resonance.attunedTierBonus {t.attuned_tier_bonus} is negative — D22-as-amended makes "
            f"matching affinity a BONUS; a negative value turns it back into a penalty gate")
    if t.catalogue_size_bar < 1:
        raise ComboTuningError(
            "learnability.catalogueSizeBar below 1 is unreachable — the bar is a report threshold, "
            "not a cap, and a bar nothing can clear reports on every run")
    if t.exact_duplicate_names_max < 0 or t.near_duplicate_rate_max_permille < 0:
        raise ComboTuningError("a distinctness threshold is never negative")
