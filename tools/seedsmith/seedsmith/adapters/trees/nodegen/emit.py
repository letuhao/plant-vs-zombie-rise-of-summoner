"""seedsmith.adapters.trees.nodegen.emit — node id minting + grammar; `IdRefused`-shaped, never
sanitised (task H1, spec-tree-language.md §6.4, §2 table's `nameKey` row).

⛔ **This stage does not mint a node id — the plan already did (§2's own row: "Minted by the plan
... before the call. An id the stage picks is an id that collides and an id that churns between
runs").** What THIS module mints and refuses is the one identifier the language stage itself
produces: `nameKey`. Same discipline as `items/setgen/emit.py`'s `IdRefused` — a bad answer is
refused, never quietly repaired, because "silently repairing it teaches the next call nothing"
(`setgen/distribute.py:143-144`, cited by this program's own Boundaries section).

Owns writing `data/seed/passive-tree/nodes/<treeId>.json` (§6.4's middle arrow) — reusing
`tree-plan`'s own canonical-bytes writer (`plan.emit.canonical_json_bytes`) rather than a second
serializer, so the byte-identical-rerun property (§7 gate 24 / H2's own acceptance bullet) holds
for this file the same way it already holds for the plan.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Mapping, Sequence

from ..plan.emit import canonical_json_bytes, content_sha256
from .exclusion import compose_printed_text
from .schema import NAME_KEY_PATTERN

REPO_ROOT = Path(__file__).resolve().parents[6]

_NAME_KEY_RE = re.compile(NAME_KEY_PATTERN)


class NodeKeyRefused(ValueError):
    """A `nameKey` (or a within-tree collision of one) this module refuses to persist, with the
    rule it would have broken in the message — never sanitised into something that passes."""


def assert_name_key_grammar(name_key: str) -> None:
    """§6.3's own pattern, checked again on the write side rather than trusted from the model's
    structured-output mode alone (guardrail 1, `pipeline/model.py`'s own docstring: "a model's
    structured-output mode is an upgrade, never the only check")."""
    if not _NAME_KEY_RE.match(name_key):
        raise NodeKeyRefused(
            f"{name_key!r} fails the nameKey grammar {NAME_KEY_PATTERN!r} — refused, not "
            f"lower-cased or slugified into something that would pass")


def assert_no_duplicate_name_keys(name_keys: "Sequence[str]") -> None:
    """Within-tree collision only. Corpus-wide name collision is `NameCollision`'s job (H4's metric,
    §7 gate 21) — this is the narrower, cheaper check: two nodes of the SAME tree must never share a
    `nameKey`, which would make `tree-surface` unable to address one of them at all."""
    seen: "dict[str, int]" = {}
    for i, key in enumerate(name_keys):
        if key in seen:
            raise NodeKeyRefused(
                f"nameKey {key!r} is used by both node index {seen[key]} and node index {i} in "
                f"the same tree — refused, never renamed out from under the model's answer")
        seen[key] = i


@dataclass(frozen=True)
class NodeSeedRecord:
    """One accepted node, in the shape §6.4's seed file persists. Mirrors §6.3's response fields
    plus the plan-side identity fields this stage never chooses (`nodeId`, `nodeKey`, `branch`,
    `tier`, `nodeClass` — all read from the plan via `plan_read`, never re-derived here).

    `printed_text` is H3's own addition (§2's `printedText` row): template-composed from
    `exclusion.compose_printed_text`, never asked of the model (it is not a §6.3 schema field) and
    never re-derivable from anything BUT the node's own `exclusion_form`/`exclusion_property_keys`
    — so a rerun over the same accepted response always recomputes the identical string."""

    node_id: str
    node_key: str
    branch: str
    tier: int
    node_class: str
    affix_ids: "tuple[str, ...]"
    affinity: "tuple[str, ...]"
    exclusion_form: str
    exclusion_property_keys: "tuple[str, ...]"
    name: str
    name_key: str
    flavor: str
    rationale: str = ""
    printed_text: str = ""

    def to_dict(self) -> dict:
        return {
            "id": self.node_id, "nodeKey": self.node_key, "branch": self.branch,
            "tier": self.tier, "nodeClass": self.node_class,
            "affixIds": list(self.affix_ids), "affinity": list(self.affinity),
            "exclusion": {"form": self.exclusion_form,
                          "propertyKeys": list(self.exclusion_property_keys),
                          "printedText": self.printed_text},
            "name": self.name, "nameKey": self.name_key, "flavor": self.flavor,
            "rationale": self.rationale,
        }


def build_node_record(node_id: str, node_key: str, branch: str, tier: int, node_class: str,
                      response: "Mapping[str, Any]") -> NodeSeedRecord:
    """One accepted §6.3 response, turned into a `NodeSeedRecord` — refuses (via
    `assert_name_key_grammar`) rather than accepting a malformed `nameKey` the schema's own
    `pattern` should already have blocked; belt and braces, per guardrail 1.

    `printedText` is composed here (`role="loser"` — the only side this stage ever persists on a
    node, per `compose_printed_text`'s own docstring), from the response's OWN `exclusion` object
    alone — never from a value the response itself might supply for it, since `printedText` is not
    part of the §6.3 schema at all (§2's table: AUTHORED/FREE, "template-composed").
    """
    name_key = str(response["nameKey"])
    assert_name_key_grammar(name_key)
    exclusion = response.get("exclusion") or {"form": "none", "propertyKeys": []}
    form = str(exclusion.get("form", "none"))
    property_keys = tuple(exclusion.get("propertyKeys") or ())
    return NodeSeedRecord(
        node_id=node_id, node_key=node_key, branch=branch, tier=tier, node_class=node_class,
        affix_ids=tuple(response["affixIds"]), affinity=tuple(response["affinity"]),
        exclusion_form=form, exclusion_property_keys=property_keys,
        name=str(response["name"]), name_key=name_key, flavor=str(response["flavor"]),
        rationale=str(response.get("rationale") or ""),
        printed_text=compose_printed_text(form, property_keys, role="loser"),
    )


def nodes_path(tree_id: str, seed_root: "Path | None" = None) -> Path:
    root = seed_root or (REPO_ROOT / "data" / "seed")
    return root / "passive-tree" / "nodes" / f"{tree_id}.json"


def build_seed_document(tree_id: str, records: "Sequence[NodeSeedRecord]", *,
                        plan_hash: str, prompt_version: str, model: str,
                        confidence: "Mapping[str, float]" = (),
                        minority_values: "Mapping[str, Any]" = ()) -> dict:
    """§6.4's `data/seed/passive-tree/nodes/<treeId>.json` shape: the seed's nodes plus its
    `_provenance` block (`planHash, promptVersion, model, confidence, minorityValues` — named
    verbatim in the spec's own §6.4 diagram). Refuses a duplicate `nameKey` within this tree
    BEFORE writing, per `assert_no_duplicate_name_keys`."""
    assert_no_duplicate_name_keys([r.name_key for r in records])
    return {
        "schemaVersion": 1,
        "treeId": tree_id,
        "nodes": [r.to_dict() for r in records],
        "_provenance": {
            "planHash": plan_hash,
            "promptVersion": prompt_version,
            "model": model,
            "confidence": dict(confidence),
            "minorityValues": dict(minority_values),
        },
    }


def write_seed_document(doc: "Mapping[str, Any]", seed_root: "Path | None" = None) -> Path:
    """Writes via `plan.emit.canonical_json_bytes` + `Path.write_bytes` — never `write_text`, for
    the exact `\\r\\n`-on-Windows reason `plan.emit.canonical_json_bytes`'s own docstring records."""
    path = nodes_path(str(doc["treeId"]), seed_root)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(canonical_json_bytes(dict(doc)))
    return path


def read_seed_document(tree_id: str, seed_root: "Path | None" = None) -> "dict[str, Any] | None":
    path = nodes_path(tree_id, seed_root)
    if not path.exists():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


__all__ = [
    "NodeKeyRefused", "assert_name_key_grammar", "assert_no_duplicate_name_keys",
    "NodeSeedRecord", "build_node_record", "nodes_path", "build_seed_document",
    "write_seed_document", "read_seed_document", "content_sha256",
]
