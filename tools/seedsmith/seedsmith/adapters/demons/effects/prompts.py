"""seedsmith.adapters.demons.effects — species-effects' own domain knowledge (T5.3,
spec-species-effects.md). Turns one species anchor into a `species-passive.{speciesId}` container
seed: the model judges WHICH affix families this species is eligible for and an affinity ordinal
per pick (`core`/`likely`/`occasional`); everything numeric — the fixed-core band, the pool weight
per affinity, `prefix_rolls`/`suffix_rolls` — is a TABLE, never the model (spec §3, §6).

**`threatBand` is deliberately absent from every context this module builds.** Strength is
`species-generator`'s, through one `P(Θ)` (spec §2's own table: "`threatBand` constrains nothing.
It is a `Θ` offset, so it belongs to magnitude, not to membership."). Grepping this file for
"threatBand" finds nothing outside this docstring — the property is enforced by omission, not by a
check that could itself have a bug.
"""
from __future__ import annotations

from typing import Any, Mapping

from .schema import AFFINITIES

__all__ = [
    "SYSTEM_PROMPT", "ID_PREFIX", "build_context", "build_brief", "entry_for",
    "fixed_core_within_band", "affix_ids_are_known",
]

ID_PREFIX = "species-passive."

SYSTEM_PROMPT = (
    "You design which shared affixes a demon species is eligible to draw from a common library — "
    "you never invent a new affix and you never write a number. For each affix family you judge "
    "eligible, give it exactly one affinity: 'core' (always present on every specimen of this "
    "species — reserve this for the ONE OR TWO effects that define what this creature IS), "
    "'likely' (a common pick, thematically strong), or 'occasional' (a rarer pick, thematically "
    "plausible but not central). Also name the container's own eligibility tags: which tag keys "
    "are REQUIRED on any drawn affix (requireTags), and which key:value pairs are an acceptable "
    "match for at least one (anyOfTags). Ground every pick in the species' own family, traits and "
    "lore — never invent lore the anchor does not support."
)


def build_context(anchor: "Mapping[str, Any]") -> "dict[str, Any]":
    """Read-only inputs the brief and the validators need. Deliberately does NOT read `threatBand` —
    spec §2's own table names it as constraining nothing here."""
    return {
        "speciesId": anchor.get("speciesId", ""),
        "rarity": anchor.get("rarity", ""),
        "elementPrimary": anchor.get("elementPrimary", ""),
        "elementSecondary": anchor.get("elementSecondary", "none"),
        "aptitudePrimary": anchor.get("aptitudePrimary", ""),
        "aptitudeSecondary": anchor.get("aptitudeSecondary", "none"),
        "posture": anchor.get("posture", ""),
        "resourceProfile": list(anchor.get("resourceProfile") or []),
        "family": list(anchor.get("family") or []),
        "traits": list(anchor.get("traits") or []),
        "flavorInfo": anchor.get("flavorInfo", ""),
    }


def build_brief(anchor: "Mapping[str, Any]", context: "Mapping[str, Any]") -> str:
    """Matches `container_authoring.py`'s own `(anchor_inputs, context) -> str` shape. Inlines the
    anchor's own fields literally — never cites a file, the same "cites nothing" discipline
    `commander_effect.py`'s own `build_brief` already states a reason for."""
    lines = [
        f"Species: {context['speciesId']} (family: {', '.join(context['family']) or 'none'})",
        f"Rarity: {context['rarity']}",
        f"Element: {context['elementPrimary']}"
        + (f" / {context['elementSecondary']}" if context['elementSecondary'] != 'none' else ''),
        f"Aptitude: {context['aptitudePrimary']}"
        + (f" / {context['aptitudeSecondary']}" if context['aptitudeSecondary'] != 'none' else ''),
        f"Posture: {context['posture']}",
        "Resources: " + ", ".join(context["resourceProfile"]),
        "Traits: " + ", ".join(context["traits"]),
        "",
        f"Lore: {context['flavorInfo']}",
        "",
        f"Eligible families / rarity bands / tag set this run may draw from: "
        f"{context.get('eligibleFamilies')} / {context.get('rarityBands')} / {context.get('tagSet')}",
        "",
        "Judge which affix families this species is eligible for, with an affinity ordinal "
        f"({', '.join(AFFINITIES)}) for each, and the container's own eligibility tags.",
    ]
    return "\n".join(lines)


def fixed_core_within_band(draft: "Mapping[str, Any]", context: "Mapping[str, Any]") -> "list[str]":
    """spec §4 (A2): a weight cannot express "always" — `core` is the fixed core, and the fixed
    core carries its OWN rarity band. A draft naming more `core` affixes than the band allows must
    repair, naming the conflict, never silently truncate (this repo's own no-silent-clamp rule)."""
    band = context.get("fixedCoreBand")
    if not band:
        return []  # band not supplied — caller's responsibility, not this validator's to assume one
    core_count = sum(1 for a in draft.get("eligibleAffixes") or [] if a.get("affinity") == "core")
    max_core = band.get("max")
    if max_core is not None and core_count > max_core:
        return [
            f"{core_count} affixes marked 'core', but this species' rarity band allows at most "
            f"{max_core} — 'core' means the fixed core, always present, and rarity bounds how many "
            f"guaranteed effects one species may carry"
        ]
    return []


def affix_ids_are_known(draft: "Mapping[str, Any]", context: "Mapping[str, Any]") -> "list[str]":
    """Every picked `affixId` must be one of the run's own declared eligible families — a model
    inventing an affix id outside the shared library is exactly the fork `eligibility-tags` (T5.2)
    exists to prevent."""
    known = set(context.get("eligibleFamilies") or [])
    if not known:
        return []
    unknown = sorted({a.get("affixId") for a in draft.get("eligibleAffixes") or []} - known)
    if unknown:
        return [f"affixId(s) {unknown} are not in this run's own eligible families {sorted(known)}"]
    return []


def entry_for(
    anchor: "Mapping[str, Any]", draft: "Mapping[str, Any]", *,
    affix_class_of: "Any",  # Callable[[str], str] -> "Prefix" | "Suffix" | "Mixed"
    provenance: "Mapping[str, Any] | None" = None,
) -> "dict[str, Any]":
    """The committed seed entry — `core` affixes land in the fixed atom list (always present,
    enforceable); `likely`/`occasional` land in the pool, weighted by
    `demon-species-effects.v1.json`'s own affinity table. `prefixRolls`/`suffixRolls` are the count
    of drawable pool groups per budget the pool ACTUALLY needs to cover its own `likely`/`occasional`
    entries — a `Mixed`-class affix counts against BOTH budgets simultaneously (A1), never doubling
    either count and never omitted from either. No weight, no tier, no magnitude, no `pool_rolls`
    literal anywhere — every one of those is `species-generator`'s or resolved at roll time (spec §6).
    """
    species_id = anchor.get("speciesId", "")
    fixed_ids = [a["affixId"] for a in draft.get("eligibleAffixes", []) if a.get("affinity") == "core"]
    pool_entries = [a for a in draft.get("eligibleAffixes", []) if a.get("affinity") != "core"]

    prefix_needed = 0
    suffix_needed = 0
    for a in pool_entries:
        cls = affix_class_of(a["affixId"])
        if cls in ("Prefix", "Mixed"):
            prefix_needed += 1
        if cls in ("Suffix", "Mixed"):
            suffix_needed += 1

    entry: "dict[str, Any]" = {
        "id": f"{ID_PREFIX}{species_id}",
        "kind": "species-passive",
        "speciesId": species_id,
        "fixedAffixes": fixed_ids,
        "pool": [{"affixId": a["affixId"], "affinity": a["affinity"]} for a in pool_entries],
        "prefixRolls": prefix_needed,
        "suffixRolls": suffix_needed,
        "eligibilityTags": dict(draft.get("eligibilityTags") or {}),
    }
    if provenance:
        entry["_provenance"] = dict(provenance)
    return entry
