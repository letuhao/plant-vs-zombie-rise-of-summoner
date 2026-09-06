"""seedsmith.adapters.trees.nodegen.brief — the per-node §6.2 brief; permutation seeded from
`nodeId|field|sampleIndex` (task H1, spec-tree-language.md §6.2).

Follows `setgen/brief.py:48-85`'s anatomy (the spec's own citation) with its two deliberate
omissions: **any number**, and **any option outside the permitted subset**. Two things this text
deliberately does NOT contain, mirroring `items/setgen/brief.py`'s own opening paragraph almost
word for word:

- **any number** — no tier, no potency, no budget share. `tier_to_depth` renders the ONLY size/depth
  signal this stage ever sees as a label (`shallow | mid | deep`), never `tier: 5` — the same "a
  number that must be conveyed is rendered as a label" rule `family_propose/prompts.py:193-197`
  states for its own bands.
- **any option outside the permitted subset** — `render_brief` takes the ALREADY-narrowed
  `permitted_affixes`/`permitted_properties` as arguments; it does not itself decide what is legal,
  because that decision is `quota.py`/H3's, read once and rendered here, never re-derived.

**Permutation is verified, not trusted.** `order_for` (`demons.anchor.permute`) is reused directly
rather than reimplemented — `verify_permutation` (`actions.validate_heal.derive`) is the gate that
later re-derives the same order and raises if a rendered brief does not reproduce it; this module's
job is only to call `order_for` with the right seed, `nodeId|field|sampleIndex` exactly as the
module docstring names it.
"""
from __future__ import annotations

from dataclasses import dataclass
from typing import Sequence

from ..plan.archetypes import TIER_COUNT
from ...demons.anchor.permute import order_for
from .vocab import AffixOption

__all__ = [
    "PROMPT_VERSION",
    "SYSTEM_PROMPT",
    "DepthBand",
    "SiblingSummary",
    "tier_to_depth",
    "permuted_affix_ids",
    "permuted_property_keys",
    "render_brief",
]

PROMPT_VERSION = "tree-language/1"

#: §6.2's SYSTEM block, verbatim — every negative clause here also lives in the schema (§7 gate 2 /
#: `schema.py`'s own field descriptions), per `family_propose/prompts.py:40-44`'s rule that a
#: description living only in prose beside a schema is a description the audit cannot read.
SYSTEM_PROMPT = (
    "You author ONE passive skill node for a build tree. You never write a number: not a "
    "strength, not a duration, not a chance, not a tier — tables you never see decide every "
    "magnitude. You never invent an effect id, an element, a status or a channel; you pick from "
    "the lists given, or you set `blocked`. You never name another node — an exclusion keys on "
    "a PROPERTY, never on a name."
)

#: `shallow | mid | deep` — §2.1's own reasoning restated for this exact label: the model never
#: sees a tier number, and this is the ONLY size/depth signal it ever receives.
DepthBand = str


def tier_to_depth(tier: int, tier_count: int = TIER_COUNT) -> DepthBand:
    """Splits `1..tier_count` into three roughly-equal thirds. Deterministic and total — every
    tier maps to exactly one band, and the boundaries never depend on which tree is asking."""
    if not (1 <= tier <= tier_count):
        raise ValueError(f"tier {tier} is outside 1..{tier_count}")
    third = tier_count / 3.0
    if tier <= third:
        return "shallow"
    if tier <= 2 * third:
        return "mid"
    return "deep"


@dataclass(frozen=True)
class SiblingSummary:
    """One already-accepted tier sibling, named and summarised — never cited, never quoted in
    full — per `distribution_planner/derive.py:516-522,576`'s `accepted_neighbours` precedent this
    module's own spec section names by file:line."""

    node_id: str
    name: str
    affix_ids: "tuple[str, ...]"


def permuted_affix_ids(node_id: str, sample_index: int,
                       permitted: "Sequence[AffixOption]") -> "list[AffixOption]":
    """§6.2's "permuted" clause for the legal-effects list, seeded `nodeId|"affixIds"|sampleIndex`.
    `verify_permutation` re-derives this exact order from the recorded sample and raises on a
    mismatch — this function's only job is to call `order_for` with the right three-part seed."""
    by_id = {o.affix_id: o for o in permitted}
    order = order_for(node_id, "affixIds", sample_index, list(by_id))
    return [by_id[i] for i in order]


def permuted_property_keys(node_id: str, sample_index: int,
                           permitted: "Sequence[str]") -> "list[str]":
    return order_for(node_id, "exclusionPropertyKeys", sample_index, list(permitted))


def render_brief(*, node_id: str, sample_index: int, tree_display_name: str, tree_reading: str,
                 branch: str, tier: int, node_class: str, motifs: "Sequence[str]",
                 anti_motifs: "Sequence[str]", permitted_affixes: "Sequence[AffixOption]",
                 permitted_properties: "Sequence[str]",
                 siblings: "Sequence[SiblingSummary]" = (), tier_count: int = TIER_COUNT) -> str:
    """The full §6.2 USER block for one node. Every list argument is ALREADY the permitted subset
    (H3's job to narrow); this function only orders and renders it."""
    if branch not in ("offensive", "defensive"):
        raise ValueError(f"branch must be 'offensive' or 'defensive', got {branch!r}")
    if node_class not in ("mechanism", "magnitude"):
        raise ValueError(f"node_class must be 'mechanism' or 'magnitude', got {node_class!r}")

    depth = tier_to_depth(tier, tier_count)
    affixes = permuted_affix_ids(node_id, sample_index, permitted_affixes)
    properties = permuted_property_keys(node_id, sample_index, permitted_properties)

    motif_line = ", ".join(motifs) if motifs else "(none named)"
    anti_line = f"\n  Avoid entirely: {', '.join(anti_motifs)}." if anti_motifs else ""
    affix_lines = "\n".join(f"    - {a.one_line}" for a in affixes)
    property_line = ", ".join(properties) if properties else "(no properties open on this tree)"
    sibling_lines = "\n".join(
        f"    - {s.name} ({', '.join(s.affix_ids)})" for s in siblings
    ) or "    (none yet)"
    class_note = (
        "a MECHANISM node grants something the resolver does not otherwise have."
        if node_class == "mechanism" else
        "a MAGNITUDE node makes an existing thing larger."
    )

    return f"""Tree: {tree_display_name} — {tree_reading}
Branch: {branch}.  Depth: {depth}.
This node must be a {node_class} node.
  - {class_note}

Motifs to express: {motif_line}.{anti_line}

Choose, and nothing else:
  1. `affixIds`  — 1 to 3 from the list below. They are this node's whole effect.
  2. `affinity`  — how central each is: core | likely | occasional.
  3. `exclusion` — only if this node's effect genuinely conflicts with a PROPERTY below.
                   Prefer `reroute`. Most nodes have none. `nullification` is the last
                   resort — use it only when the pair can be neither rerouted nor ordered,
                   and say plainly which side wins.
  4. `name`, `nameKey`, `flavor`.

Never choose a number, a strength, a duration or a tier. Those are resolved after you answer.

Legal effects ({len(affixes)}):
{affix_lines}

Legal exclusion properties: {property_line}

Already written in this tier — do not repeat:
{sibling_lines}

If this brief cannot carry a node you would be happy to ship, set `blocked` and say why."""
