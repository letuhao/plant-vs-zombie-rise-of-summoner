"""seedsmith.adapters.demons.motifs — motifs and anti-motifs per demon (spec-motif-derive.md).

Pure derivation, no model call, no human pass (owner, Q1) — the point is not sophistication, it is
a mechanism a family member can inherit from and a later pass (`lore-enrich`) can improve without
anyone re-authoring by hand. §2.5's own words: "first-pass motifs... are supposed to be visibly
weak" — this module is deliberately naive, not accidentally so.

Two passes, because a family's own motif pool does not exist until its MEMBERS' own-text motifs
have been derived once:
  pass 1 — `own_motifs(text_or_name)` per demon, independent of family
  pass 2 — aggregate each family's pool from its members' pass-1 motifs
  pass 3 — each demon's final motifs = its families' pools (family-first) + its own, trimmed 3-5

⚠️ **`jieba` dependency, owner-approved 2026-08-31, scoped to this module's tokenizer only.** The
program's standing rule is "stdlib only outside `pipeline`" — this is the one exception, and here is
why a regex genuinely cannot do the job it is standing in for: Chinese has no spaces between words,
so a punctuation-free run of characters is not "one token", it is an un-segmented CLAUSE. Confirmed
on the first real 84-demon run: the original regex tokenizer (`[一-鿿]+`, "maximal CJK run") returned
whole sentences as "motifs" — e.g. `以下能防止爆炸樱桃产生溅射` (an entire clause) — which is unusable
as shared vocabulary. `jieba` performs real dictionary/statistical word segmentation; no
regex-based heuristic can approximate that without linguistic knowledge a regex does not have.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from typing import Mapping, Sequence

import jieba

__all__ = [
    "FamilyMembership",
    "DemonMotifInput",
    "DerivedMotifs",
    "own_motifs",
    "derive_motifs",
]

# Family-first default split (owner-decided starting point, spec §9 Q1 — revisit against the real
# roster, not chosen by feel): a demon with enough inherited motifs takes 2 from its families and
# 1 of its own; family-first ordering is what makes a family read as a family rather than a list
# of individually-flavoured demons.
_FAMILY_SHARE = 2
_OWN_SHARE = 1
_MIN_MOTIFS = 3
_MAX_MOTIFS = 5

_ASCII_TOKEN = re.compile(r"[A-Za-z0-9]+")
_HAS_CJK = re.compile(r"[一-鿿]")

# jieba segments; it does not remove function words. This is a small, documented particle/pronoun/
# common-verb list for THIS game's flavour-text register — measured against the real corpus while
# fixing the whole-clause defect, not exhaustive Chinese NLP stopword research. A motif that is a
# real content word (a noun, a distinctive verb) survives; a grammatical scaffold word does not.
_CJK_STOPWORDS = frozenset({
    "的", "了", "是", "在", "和", "或", "与", "及", "其", "这", "那", "每", "当", "将", "被", "对",
    "向", "从", "上", "下", "内", "外", "次", "个", "以", "以下", "以上", "如果", "则", "后", "前",
    "能", "可以", "会", "都", "也", "还", "并", "且", "为", "使", "让", "给", "但", "而", "此",
    "该", "本", "又", "再", "更", "最", "很", "非常", "一个", "一种", "所有", "任何", "没有", "不会",
    "不能", "只", "只能", "只有", "自己", "拥有", "具有", "进行", "获得", "产生", "造成", "发生",
    "出现", "变成", "成为", "开始", "结束", "之后", "之前", "同时", "时候", "情况", "状态", "效果",
})


def _tokenize(text: str) -> list[str]:
    if _HAS_CJK.search(text):
        return [t for t in jieba.cut(text) if _HAS_CJK.search(t) and t not in _CJK_STOPWORDS and len(t) >= 1]
    return [t.lower() for t in _ASCII_TOKEN.findall(text)]


def own_motifs(*, flavor_text: "str | None", name: str, cap: int = _MAX_MOTIFS) -> "tuple[list[str], str]":
    """A demon's own contribution before any family inheritance. Prefers flavour text (`basis =
    "text"`); falls back to the name (`basis = "name"`) when no text exists — §2.5's expected
    common case, since B3 measured most of the roster this thin. Longer tokens are treated as more
    distinctive than short ones (a real heuristic choice, documented rather than hidden): a
    4-character compound says more about a demon than a 1-character particle.
    """
    source, basis = (flavor_text, "text") if flavor_text else (name, "name")
    if not source:
        return [], "blocked"
    tokens = sorted(set(_tokenize(source)), key=lambda t: (-len(t), t))
    if not tokens:
        return [], "blocked"
    return tokens[:cap], basis


@dataclass(frozen=True)
class FamilyMembership:
    family_id: str
    basis: str  # the (demon, family) candidate's own basis from family-extract: "text" | "name"


@dataclass(frozen=True)
class DemonMotifInput:
    species_id: str
    name: str
    flavor_text: "str | None"
    families: "tuple[FamilyMembership, ...]" = ()  # family-first order = membership order given


@dataclass(frozen=True)
class DerivedMotifs:
    motifs: "list[str]"
    anti_motifs: "list[str]"
    basis: str  # "text" | "name" | "blocked" — the WEAKEST basis among everything that contributed
    tautological: bool  # A2: this demon's OWN motifs and EVERY family it belongs to are basis="name"


_BASIS_RANK = {"text": 0, "name": 1, "blocked": 2}  # lower = stronger; combined basis is the weakest


def _weakest(bases: Sequence[str]) -> str:
    return max(bases, key=lambda b: _BASIS_RANK[b]) if bases else "blocked"


def derive_motifs(
    demons: Sequence[DemonMotifInput],
) -> "dict[str, DerivedMotifs]":
    """§2.2-§2.4, all three passes. Deterministic: sorted iteration everywhere ties could occur."""
    ordered = sorted(demons, key=lambda d: d.species_id)

    # Pass 1 — own motifs, independent of family.
    own: "dict[str, tuple[list[str], str]]" = {
        d.species_id: own_motifs(flavor_text=d.flavor_text, name=d.name) for d in ordered
    }

    # Pass 2 — aggregate each family's pool from its members' own motifs, in speciesId order so
    # the pool itself is deterministic regardless of dict iteration order.
    family_members: "dict[str, list[str]]" = {}
    for d in ordered:
        for fm in d.families:
            family_members.setdefault(fm.family_id, []).append(d.species_id)

    family_pool: "dict[str, list[str]]" = {}
    for family_id, member_ids in family_members.items():
        pool: "list[str]" = []
        for sid in sorted(member_ids):
            for token in own[sid][0]:
                if token not in pool:
                    pool.append(token)
        family_pool[family_id] = pool

    # Pass 3 — per demon, family-first union then trim; anti-motifs from the nearest contrasting
    # family (fewest shared motifs among this demon's OWN family pools); basis is the weakest of
    # everything that actually contributed.
    result: "dict[str, DerivedMotifs]" = {}
    all_families_by_pool_overlap = family_pool  # for contrast lookup below

    for d in ordered:
        own_tokens, own_basis = own[d.species_id]
        family_bases = [fm.basis for fm in d.families]

        if not d.families and own_basis == "blocked":
            result[d.species_id] = DerivedMotifs(motifs=[], anti_motifs=[], basis="blocked", tautological=False)
            continue

        combined: "list[str]" = []
        for fm in d.families:  # family-first, membership order preserved
            for token in family_pool.get(fm.family_id, [])[:_FAMILY_SHARE]:
                if token not in combined:
                    combined.append(token)
        own_slice = own_tokens[:_OWN_SHARE if d.families else _MAX_MOTIFS]
        own_contributed = False
        for token in own_slice:
            if token not in combined:
                combined.append(token)
                own_contributed = True
        motifs = combined[:_MAX_MOTIFS]
        # A token dropped by the final [:​_MAX_MOTIFS] trim never really "contributed" — recompute
        # against what actually survived, so `basis` reflects the emitted motifs, not the scratch list.
        own_contributed = own_contributed and any(t in motifs for t in own_slice)

        # Anti-motifs: the family (among ALL families that exist, not just this demon's own) whose
        # pool shares the FEWEST tokens with this demon's own family pools — "the nearest OTHER
        # family" (§2.3). Only meaningful with >=1 family to contrast against another family.
        anti: "list[str]" = []
        own_family_ids = {fm.family_id for fm in d.families}
        candidate_others = {fid: pool for fid, pool in all_families_by_pool_overlap.items()
                            if fid not in own_family_ids}
        if own_family_ids and candidate_others:
            own_pool_tokens = set(motifs)
            nearest_other = min(
                sorted(candidate_others),  # deterministic tie-break, alphabetic
                key=lambda fid: len(own_pool_tokens & set(candidate_others[fid])),
            )
            anti = [t for t in candidate_others[nearest_other] if t not in own_pool_tokens][:_MAX_MOTIFS]

        # Weakest basis among everything that actually ended up in `motifs` — never among tokens
        # that were derived but then trimmed away, and never "blocked" poisoning a demon whose
        # family motifs did the real work (own_basis="blocked" only when own_tokens is empty, in
        # which case own_contributed is already False).
        contributing_bases = family_bases + ([own_basis] if own_contributed else [])
        basis = _weakest(contributing_bases) if contributing_bases else "blocked"
        # A2: tautological iff this demon's own motifs AND every family it belongs to are all
        # basis="name" — the case where motifs and family both trace back to the same bare string.
        tautological = own_basis == "name" and bool(d.families) and all(b == "name" for b in family_bases)

        result[d.species_id] = DerivedMotifs(
            motifs=motifs, anti_motifs=anti, basis=basis, tautological=tautological,
        )

    return result
