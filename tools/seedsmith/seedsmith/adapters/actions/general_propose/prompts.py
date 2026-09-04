"""seedsmith.adapters.actions.general_propose.prompts --- A-P1 "general-propose"'s own domain
knowledge (spec-general-propose.md SS2, action-corpus module). The model authors ONE role-based
action any creature could hold: a name, a flavour line, and which atom FAMILIES it is built from,
picking only from the pool the planner (A-S1) already fixed for this brief. Every magnitude of any
kind -- rung, cost, cooldown, duration, chance, weight, tier, stack count -- is a table's job,
never the model's (P1, restated for this content type).

**A-P1 is the one propose pipeline with no anchor at all.** No family, no element, no motifs, no
species (spec SS0, "why this cannot be A-P2 with a `scope` flag"). `build_context` RAISES the
moment a brief carries real anchor content rather than silently ignoring it -- silently accepting
one is exactly how "general" quietly becomes "family" (spec SS3).

Closely mirrors `adapters/effects/affix/prompts.py`'s own shape (system prompt, schema,
`build_context`, `build_brief`, `entry_for`, validators) -- named by spec-general-propose.md SS1 as
this module's own closest structural precedent. One real difference: `atomFamilies` is a VOTED
field here (three permuted samples, spec SS2 "Which fields are voted"), so `build_context` takes a
`sample_index` affix's own single-shot `build_context` never needed.
"""
from __future__ import annotations

import copy
from typing import Any, Mapping, Sequence

from ....pipeline.model import BLOCKED_FIELD
from ...demons.anchor.permute import order_for

__all__ = [
    "SYSTEM_PROMPT", "GENERAL_ACTION_SCHEMA", "schema_for_call",
    "build_context", "build_brief", "entry_for",
    "atom_families_are_allowed", "atom_families_not_forbidden",
]

# ---------------------------------------------------------------------------------------------
# The system prompt --- one judgement, three negative clauses (spec SS2 "What the system prompt
# says"). Every clause is stated in the schema's own field descriptions too (F19's own reasoning:
# a description that lives only in prose beside a schema is a description the audit cannot read),
# but the model reads BOTH, and the two are meant to agree, never to say two different things.
# ---------------------------------------------------------------------------------------------

SYSTEM_PROMPT = (
    "You design the identity of an action any creature in the game could hold -- a name, a line "
    "of flavour, and which of the given atom families it is built from. You never write a "
    "number: not a rung, not a cost, not a duration, not a chance -- tables you never see decide "
    "every magnitude. You never name a creature, a family, an element or a species -- a general "
    "action belongs to everyone, and the moment it reads as 'the fire one' it is a family action "
    "and belongs to a different pipeline. You never invent an atom family -- you pick from the "
    "list given, or you set `blocked`."
)

# ---------------------------------------------------------------------------------------------
# The schema. Every `description` string below is copied byte-for-byte from spec-general-propose
# .md SS2's own JSONC block (review F19, 2026-09-03) -- they were written out there specifically
# so this build would not have to (re-)derive them, and are each modelled on the hardened
# `blocked` description at `adapters/demons/anchor/prompts.py:74-82`.
#
# `atomFamilies.items.enum` ships EMPTY here on purpose: the real enum is "filled at call time
# from the brief's own allowedAtomFamilies" (SS2), permuted per (briefId, "atomFamilies",
# sampleIndex) -- `schema_for_call` below does that per call. `audit_schema(GENERAL_ACTION_SCHEMA)`
# (acceptance #1) therefore audits the SHAPE, never one call's own snapshot of a pool.
#
# `blocked` is a STRING here, not the boolean+`reason` pair `affix/prompts.py`'s own
# `AFFIX_SCHEMA` and `validate_heal/schemas.py`'s fixture both use -- spec SS2 states the
# empty-string convention explicitly ("Leave this as the exact empty string...") and that is the
# real, decided shape for THIS pipeline; the two other shapes are each a different module's own
# choice, not a second copy of this one.
# ---------------------------------------------------------------------------------------------

GENERAL_ACTION_SCHEMA: "dict[str, Any]" = {
    "type": "object",
    "properties": {
        "name": {
            "type": "string",
            "description": (
                "The action's display name as a player sees it — two to five words, in the "
                "game's voice. Do NOT restate the atom family ids you picked: 'Burn Spread' is a "
                "label, not a name. It is NOT a sentence and NOT an instruction — no imperative "
                "verb, no trailing punctuation. It never names a creature, a family, an element "
                "or a species; a general action belongs to everyone."
            ),
        },
        "flavor": {
            "type": "string",
            "description": (
                "One line a player would read under the name, under 140 characters, evoking what "
                "the action feels like. It is NOT a rules description: never say what the action "
                "does mechanically, and never write a number, a duration, a chance or a range — "
                "tables you never see decide all of those. It never names a creature, a family, "
                "an element or a species."
            ),
        },
        "atomFamilies": {
            "type": "array", "minItems": 1,
            "description": (
                "Which of the atom families listed above this action is built from — choose one "
                "or more from that list. You are choosing WHICH families, never HOW MUCH of any "
                "of them: this is NOT a place for a magnitude, a weight or a count. Do NOT invent "
                "a family that is not in the list, and do NOT write a concrete atom id — a family "
                "names a pool, and which member of the pool a player gets is decided later, by "
                "code, per player."
            ),
            "items": {"type": "string", "enum": []},
        },
        "rationale": {
            "type": "string",
            "description": (
                "One sentence saying why these atom families make a good action for the role "
                "described above. It is NOT a restatement of the name or the flavour, and it is "
                "NOT a justification of any number — you never see a number, so there is nothing "
                "numeric to justify. Do NOT use it to add an effect you did not put in "
                "`atomFamilies`."
            ),
        },
        BLOCKED_FIELD: {
            "type": "string",
            "description": (
                "Leave this as the exact empty string \"\" when you WERE able to design the "
                "action above — this is the normal case for almost every brief. Only write a "
                "non-empty reason here when the brief genuinely gives you NOTHING to work from "
                "(for example, an empty list of eligible atom families). Do NOT put a name, a "
                "family id, a rationale or any other real answer here — it is a blocked-flag, not "
                "a second answer field. This is NOT a boolean — never write the word \"true\" or "
                "\"false\" here; those are not the empty string and would be read as a genuine "
                "decline."
            ),
        },
    },
    "required": ["name", "flavor", "atomFamilies", "rationale", BLOCKED_FIELD],
    "additionalProperties": False,
}


def schema_for_call(allowed_atom_families: Sequence[str]) -> "dict[str, Any]":
    """A per-call COPY of `GENERAL_ACTION_SCHEMA` with `atomFamilies.items.enum` filled from one
    brief's own (already permuted) allowed set. Never mutates the shared constant -- a `deepcopy`,
    so two calls in the same process never alias each other's enum."""
    schema = copy.deepcopy(GENERAL_ACTION_SCHEMA)
    schema["properties"]["atomFamilies"]["items"]["enum"] = list(allowed_atom_families)
    return schema


# ---------------------------------------------------------------------------------------------
# What `build_context` reads off a brief, and the two raises spec SS3 requires (AC4): a brief
# carrying real anchor content, or missing a planner-owned slot field.
# ---------------------------------------------------------------------------------------------

#: Group B, spec-distribution-planner.md SS2's own brief envelope. A key being PRESENT with every
#: sub-value null/empty is A-S1's own shipped shape for a general-scope brief (measured against
#: `data/seed/actions/_briefs/round-1.json` 2026-09-04) -- the absent-vs-empty discipline this
#: whole program already applies elsewhere (`spec-brief-assembly.md` SS3.2) means that is the
#: NORMAL, legal shape for this scope, not a violation. Only non-empty CONTENT on one of these
#: sub-fields means the brief was mis-routed from a family/signature scope, and that is what
#: raises -- see `_assert_no_anchor`'s own docstring for why a literal "carries the anchor KEY"
#: reading would raise on every real general-scope brief that exists today.
_ANCHOR_CONTENT_KEYS: "tuple[str, ...]" = ("family", "element", "rarity", "themeKey", "motifs", "antiMotifs")

#: Group C, spec-distribution-planner.md SS2's own brief envelope -- the planner-owned mechanical
#: slot (spec-general-propose.md SS2 "What the brief inlines"). Checked for KEY presence only
#: (never a non-null value) -- `kind`/`areaShape` are legitimately `null` on real briefs today
#: (`distribution_planner/derive.py:596`, its own documented wiring gap), and a value being a
#: legal `null` is a different thing from the key being absent.
_REQUIRED_SLOT_FIELDS: "tuple[str, ...]" = ("category", "targetMode", "areaShape", "relation", "kind", "rungBand")

#: A-S1's own three scope windows (spec-distribution-planner.md SS3 step 4): general [1,4],
#: family [1,7], signature [1,10]. Rendered as PLAIN LABELS, never the raw pair the model could
#: copy into its own answer as though it were a real magnitude (spec SS2 "What the brief
#: inlines"). A-P1 only ever sees [1,4] in real production data (it reads general-scope briefs
#: only) -- the other two rows are carried here anyway so this stays a total function rather than
#: one that raises on a shape it merely does not expect today.
_RUNG_BAND_LABELS: "dict[tuple[int, int], str]" = {
    (1, 4): "an early tier, with few structural axes available",
    (1, 7): "a mid tier, with several structural axes available",
    (1, 10): "a late tier, with most structural axes available",
}


def _assert_no_anchor(brief: Mapping[str, Any]) -> None:
    """Spec SS3: "Never read an anchor. If a brief handed to this stage carries `anchor`, the
    stage raises rather than ignoring the field." Read literally that would raise on `anchor`
    being a KEY at all -- but A-S1's own shipped output puts an `anchor` key on EVERY brief it
    emits, general scope included, with every sub-field null/empty (measured against
    `data/seed/actions/_briefs/round-1.json`, entries at `scope: "general"`). A literal
    key-presence check would therefore raise on 100% of real general-scope briefs, which cannot
    be the intended rule -- this repo's own absent-vs-empty discipline
    (`spec-brief-assembly.md` SS3.2: "Absent is a defect; empty is a value") is what resolves the
    apparent conflict: the KEY is A-S1's envelope, always present; real anchor CONTENT is what
    marks a brief as belonging to a different scope, and that is what this function raises on.
    """
    anchor = brief.get("anchor")
    if not isinstance(anchor, Mapping):
        return
    for key in _ANCHOR_CONTENT_KEYS:
        value = anchor.get(key)
        if value:
            raise ValueError(
                f"brief {brief.get('briefId', '?')!r} carries real anchor content "
                f"(anchor.{key}={value!r}) -- general-propose (A-P1) reads no anchor at all "
                f"(spec-general-propose.md SS3); this brief belongs to a family/signature "
                f"pipeline, not this one"
            )


def _require_slot(brief: Mapping[str, Any]) -> Mapping[str, Any]:
    slot = brief.get("slot")
    if not isinstance(slot, Mapping):
        raise ValueError(f"brief {brief.get('briefId', '?')!r} is missing its 'slot' object")
    missing = [f for f in _REQUIRED_SLOT_FIELDS if f not in slot]
    if missing:
        raise ValueError(
            f"brief {brief.get('briefId', '?')!r} is missing planner-owned slot field(s) "
            f"{missing} -- these are A-S1's to fix, never this stage's to invent"
        )
    return slot


def _rung_band_label(rung_band: Any) -> str:
    if not (isinstance(rung_band, (list, tuple)) and len(rung_band) == 2):
        return "an unspecified tier"
    key = (rung_band[0], rung_band[1])
    return _RUNG_BAND_LABELS.get(key, "a tier outside the three known scope windows")


def build_context(brief: Mapping[str, Any], *, sample_index: int,
                  pairing_table: "Mapping[str, Sequence[str]] | None" = None) -> "dict[str, Any]":
    """Read-only inputs `build_brief` renders from and the validators check against -- exactly as
    `affix/prompts.py:49-56` does, so a validator always reads the SAME object the brief was
    rendered from. Raises per `_assert_no_anchor`/`_require_slot` (acceptance #4) before reading
    anything else off `brief`.

    `sample_index` is IN this call, never bolted on after -- three votes over three identical
    orders is one sample with extra steps (`adapters/demons/anchor/permute.py`'s own module
    docstring)."""
    _assert_no_anchor(brief)
    slot = _require_slot(brief)

    brief_id = brief.get("briefId") or brief.get("id")
    if not brief_id:
        raise ValueError("brief is missing 'briefId'")

    pool = brief.get("pool") or {}
    allowed_raw = sorted(set(pool.get("allowedAtomFamilies") or ()))
    forbidden = sorted(set(pool.get("forbiddenAtomFamilies") or ()))
    permuted_allowed = list(order_for(brief_id, "atomFamilies", sample_index, allowed_raw))

    pairing = brief.get("pairing") or {}
    role = pairing.get("role", "none")
    paired_payoff_family = pairing.get("pairedPayoffFamily")

    context: "dict[str, Any]" = {
        "briefId": brief_id,
        "sampleIndex": sample_index,
        "category": slot.get("category"),
        "targetMode": slot.get("targetMode"),
        "areaShape": slot.get("areaShape"),
        "relation": slot.get("relation"),
        "kind": slot.get("kind"),
        "rungBandLabel": _rung_band_label(slot.get("rungBand")),
        "allowedAtomFamilies": permuted_allowed,
        "forbiddenAtomFamilies": forbidden,
        "pairingRole": role,
        "pairedPayoffFamily": paired_payoff_family,
        "avoidNeighbours": [
            n.get("fingerprint") for n in (brief.get("avoidNeighbours") or ())
            if isinstance(n, Mapping) and n.get("fingerprint")
        ],
    }
    if role == "payoff" and pairing_table and paired_payoff_family:
        allowed_set = set(permuted_allowed)
        context["enablerFamilies"] = sorted(
            f for f in pairing_table.get(paired_payoff_family, ()) if f in allowed_set
        )
    return context


def build_brief(context: Mapping[str, Any]) -> str:
    """Inlines literal values and CITES NO FILE -- the same discipline `affix/prompts.py:60-61`
    states a reason for. Nothing here is an anchor: no family, no element, no motifs, no species
    key (acceptance #3)."""
    lines = [
        "Design ONE general action: a role any creature in the game could hold. It has no "
        "anchor -- this action belongs to everyone, never a single creature, family, element or "
        "species.",
        "",
        f"Category: {context['category'] or 'unspecified'}",
        f"Target mode: {context['targetMode'] or 'unspecified'}",
        f"Area shape: {context['areaShape'] or 'unspecified'}",
        f"Relation: {context['relation'] or 'unspecified'}",
        f"Kind: {context['kind'] or 'unspecified'}",
        f"Power tier: {context['rungBandLabel']}",
        "",
        "Eligible atom families -- choose one or more from this list, in this order: "
        + ", ".join(context["allowedAtomFamilies"]),
    ]
    if context["forbiddenAtomFamilies"]:
        lines.append(
            "Forbidden atom families -- never pick any of these, they would make a knowingly "
            "additive pairing multiplicative instead: " + ", ".join(context["forbiddenAtomFamilies"])
        )
    role = context["pairingRole"]
    if role == "payoff":
        line = f"Pairing role: payoff for {context['pairedPayoffFamily']}."
        enablers = context.get("enablerFamilies") or []
        if enablers:
            line += " Its enabler families: " + ", ".join(enablers) + "."
        lines.append(line)
    elif role == "enabler":
        lines.append(f"Pairing role: enabler for {context['pairedPayoffFamily']}.")
    else:
        lines.append("Pairing role: none -- this action pairs with nothing.")
    if context["avoidNeighbours"]:
        lines.append(
            "Do not produce anything like these already-accepted actions (fingerprints): "
            + "; ".join(context["avoidNeighbours"])
        )
    return "\n".join(lines)


# ---------------------------------------------------------------------------------------------
# Validators -- same `(draft, context) -> list[str]` shape as `affix/prompts.py`'s own
# `refs_are_known_atoms`/`bundle_has_at_least_two_refs`. Deliberately narrow: whether a draft's
# NAME/RATIONALE leaks an anchor token is A-S4's own g2 rule (`validate_heal/gates.py:168-176`,
# "A-P1 only... a general-scope brief carries no anchor") -- this module never grades itself
# (spec SS3), so it checks only what it alone can know: whether the picked families are the ones
# THIS brief actually offered.
# ---------------------------------------------------------------------------------------------

def atom_families_are_allowed(draft: Mapping[str, Any], context: Mapping[str, Any]) -> "list[str]":
    """Every picked family must be one of THIS brief's own `allowedAtomFamilies` -- a model
    inventing a family outside the brief's own pool is exactly the fork spec SS3 ("never invent
    an atom family") exists to prevent."""
    allowed = set(context.get("allowedAtomFamilies") or ())
    picked = draft.get("atomFamilies") or []
    unknown = sorted(f for f in picked if f not in allowed)
    if unknown:
        return [
            f"atomFamilies {unknown} not in this brief's own allowedAtomFamilies {sorted(allowed)}"
        ]
    return []


def atom_families_not_forbidden(draft: Mapping[str, Any], context: Mapping[str, Any]) -> "list[str]":
    """A family the planner forbade for this brief (e.g. one half of a known multiplicative pair,
    spec-distribution-planner.md SS3 step 7) is a defect even when it is also allowed elsewhere --
    forbidden always wins."""
    forbidden = set(context.get("forbiddenAtomFamilies") or ())
    picked = draft.get("atomFamilies") or []
    hit = sorted(f for f in picked if f in forbidden)
    if hit:
        return [f"atomFamilies {hit} is in this brief's own forbiddenAtomFamilies"]
    return []


def entry_for(draft: Mapping[str, Any], *, candidate_id: str, brief_id: str,
              provenance: "Mapping[str, Any] | None" = None) -> "dict[str, Any]":
    """The candidate's own action-shaped payload -- NOT a committed seed entry (spec SS3: "Never
    write into `data/seed/actions/`. Acceptance is A-S3's; persistence is A-S6's."). `candidate_id`
    is a scratch identifier for this run, never a minted `action.general.NNNN` id -- those are
    assigned downstream, after acceptance."""
    entry: "dict[str, Any]" = {
        "candidateId": candidate_id,
        "briefId": brief_id,
        "scope": "general",
        "name": draft["name"],
        "flavor": draft["flavor"],
        "atomFamilies": list(draft["atomFamilies"]),
        "rationale": draft["rationale"],
    }
    if provenance:
        entry["_provenance"] = dict(provenance)
    return entry
