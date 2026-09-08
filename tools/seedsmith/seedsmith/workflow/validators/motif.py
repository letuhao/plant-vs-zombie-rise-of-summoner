"""Motif validators — the two that forced a repair in the real 8-demon run."""
from __future__ import annotations

from typing import Any, Mapping

__all__ = ["motif_coverage", "anti_motif_violation", "_text_of"]


def _text_of(draft: "Mapping[str, Any]") -> str:
    """All string values concatenated — a motif may legitimately land in any prose field."""
    return " ".join(str(v) for v in draft.values() if isinstance(v, str))


def motif_coverage(draft: "Mapping[str, Any]", context: "Mapping[str, Any]") -> "list[str]":
    """Reject output using NONE of the subject's motifs.

    This is the check that produced `attempts: 2` in the probe: the first draft ignored the motifs,
    was rejected mechanically, and the retry complied. ⚠️ It proves USE, never GOOD use — see the
    package docstring."""
    motifs = [m for m in (context.get("motifs") or []) if m]
    if not motifs:
        return []
    blob = _text_of(draft)
    if any(m in blob for m in motifs):
        return []
    return [f"uses none of the subject's motifs {list(motifs)} — at least one must appear"]


def anti_motif_violation(draft: "Mapping[str, Any]", context: "Mapping[str, Any]") -> "list[str]":
    """Reject output using a word the subject is defined AGAINST.

    The hardest constraint measured (a NEGATIVE instruction); it held 0/8 violations."""
    blob = _text_of(draft)
    return [f"uses anti-motif {a!r}, which this subject is defined against"
            for a in (context.get("antiMotifs") or []) if a and a in blob]
