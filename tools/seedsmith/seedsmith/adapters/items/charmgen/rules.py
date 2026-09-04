"""seedsmith.adapters.items.charmgen.rules — the charm half of the distributor.

Same shape as `setgen.distribute`, different rules. Four of them, all from `ssot-charms.md`:

| Constraint | Source |
|---|---|
| **`Flat` only** — never `Increased`, never `More` | §3.4 |
| `max_tier` at most **one band below** an equip container of the same rarity | §3.4 |
| A family may not appear on both a `jewel-minor` base type and a charm — **at all**, not at a different tier | §3.6 |
| A signet: `pool_rolls = 0`, `unique_carry = 1`, and it carries a **drawback** | §3.4 |

⭐ Module 12 already turned the last row from an observation into a refusal at the DAL
(`CharmCorpus.ValidateClassRules`, reason codes `charm-signet-has-rolled-half` / `-not-unique-carry`
/ `-has-no-drawback`). This module refuses the same three at GENERATION, before the row exists —
the same rule enforced at both ends on purpose, because the runtime check cannot stop a bad row
being authored and the generator check cannot stop one being hand-edited in later.

⚠ **The axis skew is a ceiling, not a target.** The authored corpus's own axis Gini is **0.133**
(economy 20 : 10 × 4 over the 60 authored rows). *"A skew this generator must not deepen"* is that
number: a generated population may be flatter, never more concentrated. It is an inequality
ceiling, not a content ceiling — nothing here caps how many charms may exist.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass, field

from ..setgen.tuning import SetCharmGenTuning
from ..setgen.vocab import FamilyPick

#: The five power categories the family library already uses — no new vocabulary (ssot-charms §3.5).
CHARM_AXES: "tuple[str, ...]" = ("offense", "survivability", "control", "utility", "economy")

#: `Increased`/`More` families named by ssot-charms §3.4 itself: "no fortitude, ferocity, bulwark,
#: savagery". Enumerated rather than derived, because the op is not a field on the family row — it
#: is a property of the atom kind, and this list is what the SSOT actually states.
NON_FLAT_FAMILIES: "frozenset[str]" = frozenset({
    "atom.fortitude", "atom.ferocity", "atom.bulwark", "atom.savagery",
})

#: The roles a charm's families must NOT share with (ssot-charms §3.6). Rings own the conditional
#: layer; charms own the always-on layer.
JEWEL_MINOR_ROLES: "frozenset[str]" = frozenset({"jewel-minor-a", "jewel-minor-b"})


@dataclass
class CharmPlan:
    charm_class: str
    axis: str
    ap_cost: int
    unique_carry: bool
    prefix_rolls: int
    suffix_rolls: int
    families: "tuple[FamilyPick, ...]" = ()
    drawback: "FamilyPick | None" = None
    problems: "list[str]" = field(default_factory=list)

    @property
    def ok(self) -> bool:
        return not self.problems

    @property
    def pool_rolls(self) -> int:
        return self.prefix_rolls + self.suffix_rolls


def families_on_jewel_minor(all_picks: "list[FamilyPick] | tuple[FamilyPick, ...]",
                            ) -> "frozenset[str]":
    """Every family the corpus makes legal on a `jewel-minor` role — the closed set a charm may not
    draw from. Computed from the real family rows, never hand-listed: §3.6's rule is about the
    corpus's own legality table, so a hand-list would go stale the first time a family moved."""
    return frozenset(p.family for p in all_picks if JEWEL_MINOR_ROLES & set(p.roles))


def distribute_charm(*, charm_class: str, axis: str,
                     families: "tuple[FamilyPick, ...]",
                     drawback: "FamilyPick | None",
                     tuning: SetCharmGenTuning,
                     jewel_minor_families: "frozenset[str]" = frozenset(),
                     equip_max_tier: "int | None" = None,
                     charm_max_tier: "int | None" = None) -> CharmPlan:
    """Price a charm draft. `problems` is empty exactly when it is legal."""
    try:
        rule = tuning.charm_class(charm_class)
    except Exception as exc:                                  # noqa: BLE001 - re-raised as a plan
        plan = CharmPlan(charm_class=charm_class, axis=axis, ap_cost=0, unique_carry=False,
                         prefix_rolls=0, suffix_rolls=0)
        plan.problems.append(f"CharmClassUnknown: {exc}")
        return plan

    prefix = rule.max_pool_rolls // 2
    suffix = rule.max_pool_rolls - prefix
    plan = CharmPlan(
        charm_class=charm_class, axis=axis, ap_cost=rule.ap_cost,
        unique_carry=rule.unique_carry, prefix_rolls=prefix, suffix_rolls=suffix,
        families=families, drawback=drawback,
    )

    if axis not in CHARM_AXES:
        plan.problems.append(
            f"CharmAxisUnknown: {axis!r} is not one of the five power categories {list(CHARM_AXES)}")
    if not families:
        plan.problems.append("CharmHasNoEffect: a charm with no family grants nothing")

    for pick in families:
        if pick.family in NON_FLAT_FAMILIES:
            plan.problems.append(
                f"CharmForbiddenOp: {pick.family!r} is an Increased/More family; a charm carries "
                f"Flat only (ssot-charms §3.4 — a multiplicative bonus applied squad-wide compounds "
                f"with every other multiplier in the build)")
        if pick.family in jewel_minor_families:
            plan.problems.append(
                f"CharmFamilyOnJewelMinor: {pick.family!r} is legal on a jewel-minor base type; a "
                f"family may not appear on both — at all, not at a different tier (ssot-charms §3.6)")

    if rule.requires_drawback and drawback is None:
        plan.problems.append(
            f"CharmSignetHasNoDrawback: class {charm_class!r} must carry an authored negative atom")
    if not rule.requires_drawback and drawback is not None:
        plan.problems.append(
            f"CharmDrawbackNotAllowed: class {charm_class!r} does not carry a drawback; only a "
            f"signet does")
    if rule.max_pool_rolls == 0 and plan.pool_rolls != 0:
        plan.problems.append(
            f"CharmSignetHasRolledHalf: class {charm_class!r} rolls nothing, got {plan.pool_rolls}")

    if equip_max_tier is not None:
        ceiling = equip_max_tier - tuning.charm_max_tier_bands_below_equip
        effective = charm_max_tier if charm_max_tier is not None else ceiling
        if effective > ceiling:
            plan.problems.append(
                f"CharmTierTooHigh: max_tier {effective} exceeds {ceiling} — at equal rarity a "
                f"charm sits at most {tuning.charm_max_tier_bands_below_equip} band(s) below an "
                f"equip container (ssot-charms §3.4)")
    return plan


def gini_permille(counts: "list[int]") -> int:
    """Integer per-mille Gini over a count vector. No float ever reaches the comparison against the
    tuning ceiling — the multiply by 1000 happens before the divide, exactly once."""
    n = len(counts)
    total = sum(counts)
    if n == 0 or total == 0:
        return 0
    ordered = sorted(counts)
    weighted = sum((2 * i - n - 1) * x for i, x in enumerate(ordered, start=1))
    return (weighted * 1000) // (n * total)


def axis_gini_permille(axes: "list[str]") -> int:
    counts = Counter(axes)
    return gini_permille([counts.get(a, 0) for a in CHARM_AXES])
