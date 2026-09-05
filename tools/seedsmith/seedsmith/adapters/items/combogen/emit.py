"""seedsmith.adapters.items.combogen.emit — ids, min tiers, min sockets.

**D27 gives combinations the `combo` container kind** — "the 25 generated resonances AND the 102
Strains/Splices" — so the lane's `gem.combo-*` / `gem.word-*` spelling is retired here as well as in
module 16's `ResonanceGenerator`. `definitions.md` §1 forces the prefix to match the kind.

```text
combo.strain-{aptitude}-{archetype}     combo.strain-might-offense      36
combo.splice-{aptitudeA}-{aptitudeB}    combo.splice-might-agility      66  -- pair sorted by
                                                                            -- Aptitude ordinal
```

⭐ **Sorting the pair by ordinal at MINT time is what makes a Splice unordered by construction.** A
uniqueness check would only discover `(Might, Agility)` and `(Agility, Might)` after both had been
generated — 66 rows late, and one of them a wasted call.

⚠ There is no `{seq:03}` here, and that is deliberate: a Strain's identity is its grid cell, not its
position in a wave. Two runs over the same grid mint the same 102 ids, which is what makes
`re_running_over_an_unchanged_grid_is_byte_identical` true rather than aspirational.
"""
from __future__ import annotations

import re
from dataclasses import dataclass

from .grid import Cell, scan_for_banned_word
from .tuning import ComboTuning

#: `definitions.md` §1: one dot, then a kebab body. Anchored and strict on purpose — a permissive
#: pattern does not reject a bad id, it makes it invisible.
CONTAINER_ID_RE = re.compile(r"^[a-z][a-z0-9]*\.[a-z0-9]+(-[a-z0-9]+)*$")

CONTAINER_PREFIX = "combo"


class IdRefused(ValueError):
    """An id this module refuses to mint, with the rule it would have broken in the message."""


def _kebab_legal(token: str) -> bool:
    return bool(re.fullmatch(r"[a-z0-9]+(-[a-z0-9]+)*", token))


def _assert_container_grammar(minted: str) -> None:
    if minted.count(".") != 1:
        raise IdRefused(f"{minted!r} has {minted.count('.')} dots; the grammar allows exactly one")
    if not CONTAINER_ID_RE.match(minted):
        raise IdRefused(
            f"{minted!r} fails definitions.md §1's container_id grammar (one dot, then [a-z0-9-]+)")
    banned = scan_for_banned_word(minted)
    if banned:
        raise IdRefused(f"{minted!r} contains {banned} — ⛔ D20 bans that word outright")


def strain_id(aptitude_token: str, archetype: str) -> str:
    for token, label in ((aptitude_token, "aptitude"), (archetype, "archetype")):
        if not _kebab_legal(token):
            raise IdRefused(f"{label} token {token!r} is not kebab-legal; it cannot enter an id")
    minted = f"{CONTAINER_PREFIX}.strain-{aptitude_token}-{archetype}"
    _assert_container_grammar(minted)
    return minted


def splice_id(lo_token: str, lo_ordinal: int, hi_token: str, hi_ordinal: int) -> str:
    """The pair, sorted by shipped ordinal. Passing them the wrong way round mints the same id."""
    if lo_ordinal == hi_ordinal:
        raise IdRefused(
            f"a Splice joins two DIFFERENT aptitudes; both sides carry ordinal {lo_ordinal}")
    a, b = ((lo_token, hi_token) if lo_ordinal < hi_ordinal else (hi_token, lo_token))
    for token in (a, b):
        if not _kebab_legal(token):
            raise IdRefused(f"aptitude token {token!r} is not kebab-legal; it cannot enter an id")
    minted = f"{CONTAINER_PREFIX}.splice-{a}-{b}"
    _assert_container_grammar(minted)
    return minted


def combo_id(cell: Cell) -> str:
    if cell.combination_kind == "strain":
        return strain_id(cell.aptitudes[0].token, cell.archetype or "")
    lo, hi = cell.aptitudes
    return splice_id(lo.token, lo.ordinal, hi.token, hi.ordinal)


def name_key(cell: Cell) -> str:
    """The localisation key. `combo.strain-might-offense` is a container id, not a name key, so the
    key gets its own `combination.` namespace and the same grid-derived body."""
    return f"combination.{cell.combination_kind}-{cell.key}"


@dataclass(frozen=True)
class IngredientRow:
    """One `socket_combo_ingredient` row — D41's multiset entry. ⛔ No `position` field: module 16's
    `ComboIngredient` has none either, deliberately, and a matcher that read one would be a bug."""

    family: str
    min_tier: int
    quantity: int

    def to_dict(self) -> dict:
        return {"family": self.family, "minTier": self.min_tier, "quantity": self.quantity}


def ingredient_rows(families: "list[str] | tuple[str, ...]",
                    tuning: ComboTuning) -> "tuple[IngredientRow, ...]":
    """Fold the model's four family picks into `(family, minTier, qty)` rows.

    Deterministic in one function of the answer: the picks are sorted by family id, the ascending
    `minTierPlan` is zipped onto them, and identical `(family, minTier)` pairs fold into a quantity.
    Sorting first is what makes the fold independent of the order the model happened to list them
    in — the same four families in any arrangement produce byte-identical rows, which is D41 at the
    emit layer rather than only at the matcher.
    """
    picks = list(families)
    if len(picks) != tuning.ingredient_count:
        raise IdRefused(
            f"a combination takes exactly {tuning.ingredient_count} ingredients (D20 as amended, "
            f"§2f.2); got {len(picks)}")
    counts: "dict[tuple[str, int], int]" = {}
    for family, tier in zip(sorted(picks), tuning.min_tier_plan):
        key = (family, tier)
        counts[key] = counts.get(key, 0) + 1
    return tuple(IngredientRow(family=f, min_tier=t, quantity=q)
                 for (f, t), q in sorted(counts.items()))


def min_sockets(tuning: ComboTuning) -> int:
    """`min_sockets` is DERIVED from the ingredient count, never authored (the P1 table's fourth
    row). A four-ingredient recipe needs four sockets and there is nothing to choose."""
    return tuning.ingredient_count


def granted_tier(cell: Cell, tuning: ComboTuning, *, all_attuned: bool = False) -> int:
    """The tier a combination grants. Base from tuning, plus D22-as-amended's attuned bonus.

    ⚠ **Never a gate.** A mismatched fill still produces the combination — it just produces it at
    the base tier. §2f.2 reverted the hard requirement by name ("a fee wearing a gate's name"), and
    a function that returned `None` for an unattuned fill would quietly restore it.
    """
    base = tuning.base_tier_for(cell.combination_kind)
    return base + (tuning.attuned_tier_bonus if all_attuned else 0)
