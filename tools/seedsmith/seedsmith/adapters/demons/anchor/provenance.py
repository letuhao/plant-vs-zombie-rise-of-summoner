"""`anchor-emit`'s provenance record (demon-seed module 8, spec-anchor-emit.md §3) — the upgrade
path, not bookkeeping. `inferred` is upgradeable *because* this record says why a value is what it
is: a later `spawn_stats` observation promotes `basis`, and re-derivation corrects one entry
visibly, in a diff, with the reason attached.
"""
from __future__ import annotations

from dataclasses import asdict, dataclass, field
from typing import Any

#: Bumped by hand when a pipeline's prompt text changes materially (spec §3: "a description change
#: invalidates exactly the fields that pipeline owns, not the whole entry"). Starts at 1 for every
#: pipeline — a real per-pipeline history is maintained by editing this dict as prompts evolve.
PROMPT_VERSIONS: "dict[str, int]" = {
    "element-primary": 1, "element-secondary": 1, "aptitude-primary": 1, "aptitude-secondary": 1,
    "threat-audit": 1, "deployment": 1, "kit-shape": 1, "identity": 1,
}


@dataclass(frozen=True)
class AnchorProvenance:
    dump_hash: str
    prompt_versions: "dict[str, int]"
    basis: str                                   # observed | stated | inferred | blocked
    confidence: "dict[str, str]" = field(default_factory=dict)     # voted field -> high|split|unresolved
    minority_values: "dict[str, str]" = field(default_factory=dict)  # voted field -> the losing value, split only
    audit_verdict: "str | None" = None            # agree | too-low | too-high; None for inferred/blocked
    attempts: "dict[str, int]" = field(default_factory=dict)   # pipeline id -> attempts used (1 = no repair)
    emitted_utc: str = ""

    def to_dict(self) -> "dict[str, Any]":
        d = asdict(self)
        # camelCase to match the rest of this program's committed JSON (corpus-dump, threat-band).
        return {
            "dumpHash": d["dump_hash"],
            "promptVersions": d["prompt_versions"],
            "basis": d["basis"],
            "confidence": d["confidence"],
            "minorityValues": d["minority_values"],
            "auditVerdict": d["audit_verdict"],
            "attempts": d["attempts"],
            "emittedUtc": d["emitted_utc"],
        }

    @classmethod
    def from_dict(cls, d: "dict[str, Any]") -> "AnchorProvenance":
        return cls(
            dump_hash=d["dumpHash"], prompt_versions=dict(d["promptVersions"]), basis=d["basis"],
            confidence=dict(d.get("confidence") or {}), minority_values=dict(d.get("minorityValues") or {}),
            audit_verdict=d.get("auditVerdict"), attempts=dict(d.get("attempts") or {}),
            emitted_utc=d.get("emittedUtc", ""))
