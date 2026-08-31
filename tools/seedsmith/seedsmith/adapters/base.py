"""seedsmith.adapters.base — the interface a feature implements to be understood by the core
(spec-foundation §2, field definitions per §7.2).

`LegalityFn` returning `True` unconditionally is a trap: an adapter that forgets to encode real
illegal combinations turns every genuinely-illegal pair into a permanent false Coverage finding
(spec-analytics §2.2). It is a required method, not an optional one with a safe-looking default,
and `adapters._stub` exercises a real `False` case so the trap cannot go unnoticed.
"""
from __future__ import annotations

import re
from dataclasses import dataclass, field
from enum import Enum
from typing import Callable, Protocol, runtime_checkable


class Unit(Enum):
    GAME_UNITS = "game_units"
    PER_MILLE = "per_mille"
    MILLISECONDS = "milliseconds"


@dataclass(frozen=True)
class KindSpec:
    kind: str                          # "base-type"
    directory: str                     # "base-types"
    namespace: str                     # allocation key, e.g. "base-type"
    required: frozenset[str] = frozenset()
    optional: frozenset[str] = frozenset()
    id_pattern: "re.Pattern[str] | None" = None
    runtime_id_fields: frozenset[str] = frozenset()   # fields holding a MINTED id

    # Fields on THIS kind that hold a cross-kind reference (P2). Ordering is derived from these
    # rather than from a hand-written stage label, because a label is a fact stated in two places
    # and the copy nobody edits is the one that goes stale -- which is precisely the 274-error
    # incident: a generation stage kept its old label after the graph beneath it changed.
    reference_fields: frozenset[str] = frozenset()


@dataclass(frozen=True)
class Dimension:
    id: str                            # "role"
    values: tuple[str, ...]
    field: str                         # entry field carrying it
    applies_to: frozenset[str]         # kinds it is meaningful for


@dataclass(frozen=True)
class Channel:
    id: str                            # "maxHp"
    unit: Unit
    reference_base: Callable[[object], int]
    group: str                         # primary | flatDerived | sigmoidDerived | statusMagnitude
    ops: frozenset[str] = frozenset()  # Flat | Increased | More


# (dimA_id, valueA, dimB_id, valueB) -> is this pair possible at all
LegalityFn = Callable[[str, str, str, str], bool]


@dataclass(frozen=True)
class RegistrySet:
    vocabularies: "dict[str, frozenset[str]]" = field(default_factory=dict)
    versions: "dict[str, int]" = field(default_factory=dict)

    def is_legal(self, vocabulary: str, value: str) -> bool:
        return value in self.vocabularies.get(vocabulary, frozenset())


@runtime_checkable
class SeedAdapter(Protocol):
    def kinds(self) -> list[KindSpec]: ...
    def dimensions(self) -> list[Dimension]: ...
    def legal_combinations(self) -> LegalityFn: ...
    def registries(self) -> RegistrySet: ...
    def channels(self) -> list[Channel]: ...
