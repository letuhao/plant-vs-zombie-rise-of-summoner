"""`language_consistency` — added 2026-09-01 after the first real commander-effect run.

⛔ **The defect this exists for.** 83/84 drafts passed every other tier-2 check, and **87% were
code-switched**: English prose with Chinese motif tokens spliced in —
*"When a 僵尸 enters the fray, the squad attempts to force a 变心..."*.

**We caused it.** `motif_coverage` requires the subject's motifs VERBATIM; the motifs are Chinese;
the model's default register for this prompt was English. So it satisfied the checker by splicing.
The 8 drafts that came back wholly in Chinese read markedly better.

That is the "a pass rate is not quality" thesis demonstrated at scale — and the right response is a
CHEAPER instrument, not a model-based one: this is mechanically checkable, so it belongs in tier 2.
"""
from __future__ import annotations

import re
from typing import Any, Mapping

__all__ = ["language_consistency"]

_CJK = re.compile(r"[一-鿿]")
#: 3+ Latin letters — ignores incidental initialisms and punctuation.
_LATIN_WORD = re.compile(r"[A-Za-z]{3,}")


def language_consistency(draft: "Mapping[str, Any]", context: "Mapping[str, Any]") -> "list[str]":
    """Reject a value mixing CJK and Latin prose when the subject's motifs are CJK.

    Only fires when the motifs are Chinese — an all-Latin corpus is unaffected, so this stays a
    general validator rather than a demons-specific one."""
    motifs = [m for m in (context.get("motifs") or []) if m]
    if not motifs or not any(_CJK.search(m) for m in motifs):
        return []

    defects: "list[str]" = []
    for name, value in draft.items():
        if not isinstance(value, str) or not value.strip():
            continue
        latin = _LATIN_WORD.findall(value)
        if _CJK.search(value) and latin:
            defects.append(
                f"field {name!r} mixes Chinese and English prose (e.g. {latin[0]!r}) — "
                f"write the whole value in Chinese, the language of this subject's motifs")
    return defects
