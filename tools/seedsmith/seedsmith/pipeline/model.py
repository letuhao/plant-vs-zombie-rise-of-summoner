"""The `Pipeline` scaffold and its guardrails (spec-pipeline.md §2-§3, plan G1).

Six guardrails, and the load-bearing one is the third:

1. **Schema-validated locally, always.** A model's structured-output mode is an upgrade, never the
   only check — an endpoint that silently ignores the mode would otherwise remove the guard.
2. **Narrow scope per call.** One partition, one kind.
3. **⛔ Never a number.** A schema carrying a numeric magnitude field is rejected *mechanically*, by
   `audit_schema`, not by review. Numbers come from `numerics`, which resolves them from bands and
   the shipped progression — a model inventing one produces a value that looks plausible and is
   unanchored to anything, which is the hardest kind of wrong to find later.
4. **Closed vocabularies inlined** — reusing `briefkit`'s inlining rather than a second copy.
5. **Validate before accept.** Scratch → gate → move. Nothing reaches the corpus unvalidated.
6. **Bounded retry with the exact defect named, then escalate.** A bare retry teaches the model
   nothing; naming the defect is what fixes it. This is `llm_caller.call_with_self_heal` (S0)
   generalized to an arbitrary schema, **reused rather than rebuilt**.

Every schema carries a `blocked` variant: a model that cannot do the job must be able to say so with
a reason. A `blocked` response writes nothing and is **reported, not counted as a failure** — the
difference matters, because a pipeline that treats "I can't" as an error retries it forever.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Callable, Mapping, Sequence

__all__ = [
    "Pipeline",
    "PipelineResult",
    "SchemaDefect",
    "audit_schema",
    "BLOCKED_FIELD",
    "NUMERIC_JSON_TYPES",
]

#: Every schema must accept this instead of forcing the model to invent something.
BLOCKED_FIELD = "blocked"

#: JSON Schema types a magnitude would be expressed in. `integer` is included deliberately: a
#: per-mille integer is exactly the shape a model most plausibly invents, and it is still a number.
NUMERIC_JSON_TYPES = frozenset({"number", "integer"})


@dataclass(frozen=True)
class SchemaDefect:
    path: str
    reason: str

    def __str__(self) -> str:
        return f"{self.path}: {self.reason}"


def audit_schema(schema: Mapping[str, Any], *, path: str = "$") -> list[SchemaDefect]:
    """Reject a schema that lets a model emit a number, or that offers it no way to decline.

    Walks `properties`, `items`, and the composition keywords, because a numeric field nested three
    levels inside an array of objects is exactly as dangerous as a top-level one and considerably
    easier to miss by eye. That is the whole argument for auditing mechanically.

    An `enum` of numbers is **allowed**: a closed set of legal values is a vocabulary, not an
    invention — the model is choosing, not deriving.
    """
    defects: list[SchemaDefect] = []

    declared = schema.get("type")
    types = {declared} if isinstance(declared, str) else set(declared or ())
    if types & NUMERIC_JSON_TYPES and "enum" not in schema and "const" not in schema:
        defects.append(SchemaDefect(
            path,
            f"bare numeric field (type {sorted(types & NUMERIC_JSON_TYPES)}) — magnitudes come "
            f"from `numerics`, never from a model; an invented number looks plausible and is "
            f"anchored to nothing",
        ))

    for name, sub in (schema.get("properties") or {}).items():
        if isinstance(sub, dict):
            defects.extend(audit_schema(sub, path=f"{path}.{name}"))

    items = schema.get("items")
    if isinstance(items, dict):
        defects.extend(audit_schema(items, path=f"{path}[]"))
    elif isinstance(items, list):
        for i, sub in enumerate(items):
            if isinstance(sub, dict):
                defects.extend(audit_schema(sub, path=f"{path}[{i}]"))

    for keyword in ("anyOf", "oneOf", "allOf"):
        for i, sub in enumerate(schema.get(keyword) or ()):
            if isinstance(sub, dict):
                defects.extend(audit_schema(sub, path=f"{path}.{keyword}[{i}]"))

    if path == "$" and BLOCKED_FIELD not in (schema.get("properties") or {}):
        defects.append(SchemaDefect(
            path,
            f"no {BLOCKED_FIELD!r} variant — a model with no way to decline invents instead, and "
            f"a pipeline that reads 'I cannot' as an error retries it forever",
        ))

    return defects


@dataclass(frozen=True)
class PipelineResult:
    """What one pipeline run produced.

    `blocked` is separate from `escalated` on purpose. Blocked means the model declined with a
    reason — reportable, not a failure. Escalated means retries were exhausted with the defect still
    present, which is a real problem for a human. Collapsing them hides the difference exactly when
    someone is deciding whether to intervene.
    """

    accepted: Mapping[str, Any] = field(default_factory=dict)
    blocked: Mapping[str, str] = field(default_factory=dict)
    escalated: Mapping[str, str] = field(default_factory=dict)
    attempts: int = 0

    @property
    def wrote_anything(self) -> bool:
        return bool(self.accepted)

    @property
    def ok(self) -> bool:
        """Blocked is not failure. Only an escalation is."""
        return not self.escalated


@dataclass(frozen=True)
class Pipeline:
    """One generation pipeline, per spec-pipeline.md §2.

    `on_persist` is injected rather than imported so a run can be proven to write **nothing** —
    which is how the `blocked` acceptance is asserted without a filesystem.
    """

    metric: str
    scope: str
    schema: Mapping[str, Any]
    gate: Callable[[Mapping[str, Any]], Sequence[str]]
    on_persist: Callable[[str, Mapping[str, Any]], None]
    model: str = "sonnet"
    max_retries: int = 2

    def __post_init__(self) -> None:
        defects = audit_schema(self.schema)
        if defects:
            raise ValueError(
                f"pipeline {self.metric!r} has an unusable schema:\n"
                + "\n".join(f"  - {d}" for d in defects)
            )
