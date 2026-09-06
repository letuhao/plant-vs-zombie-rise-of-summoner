"""seedsmith.adapters.items.setgen.seedfile — a priced draft becomes a seed row, and the row goes
somewhere it was asked to go.

⛔ **The output directory is never defaulted.** A generator whose write target has a default writes
into the shipped corpus the first time someone forgets a flag, and the shipped corpus is what every
metric baseline in this program is measured against. `resolve_out_dir` refuses the two production
kind directories unless the caller says so in as many words.

⚠ **Two shapes, read off the corpus rather than invented.** A threshold's element-generated pick is
written `{"family": …, "powerBand": …, "params": {"element": …}}` — measured on
`set.frostbitten-vanguard-002` and `charm.off-ctrl-015`, both shipped — never as a `variant` key,
which is the generator's own internal spelling and appears nowhere in the corpus.

⚠ **A member's `baseType` is NOT emitted, and that is a named gap rather than a guess.** The shipped
rows bind each member to a concrete base type (`item.humanoid-head-b-007`); nothing in module 13
chooses one, because which rung of which ladder a set piece sits on is a design decision no part of
the generator has been given. Emitting a plausible id here would be deterministic code inventing
content, which is P1 inverted. The row carries the (role, frame) pair the model chose and the run
report names the binding step as unwired.
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .distribute import SetPlan
from .vocab import FamilyPick, Vocabulary
from ..charmgen.rules import CharmPlan

REPO_ROOT = Path(__file__).resolve().parents[6]
NAMING_REGISTRY = REPO_ROOT / "data" / "seed" / "items" / "_registry" / "naming.v1.json"
ITEM_SEED_ROOT = REPO_ROOT / "data" / "seed" / "items"

#: The two directories a generated set / charm eventually ships in. Writing here is the production
#: run; everything else is a sample, and the two must never be confusable on disk.
PRODUCTION_KIND_DIRS: "dict[str, Path]" = {
    "set": ITEM_SEED_ROOT / "sets",
    "charm": ITEM_SEED_ROOT / "charms",
}


class OutDirRefused(ValueError):
    """The requested write target is the shipped corpus and the caller did not say so."""


def resolve_out_dir(out_dir: "str | Path", *, allow_production_tree: bool = False) -> Path:
    """Where rows land. Refuses the production kind directories unless explicitly authorised.

    ⚠ Refuses anything under `data/seed/items/` outright when not authorised, not just the two kind
    directories: every metric in this program globs that tree recursively
    (`corpus/model.py`'s `rglob("*.json")`, `ItemSeedValidator`'s `SearchOption.AllDirectories`), so
    a sample dropped anywhere inside it moves finding counts another stream uses as a baseline.
    An underscore prefix does not help — only `_exemplars/` is special-cased, and only there.
    """
    path = Path(out_dir).resolve()
    if allow_production_tree:
        return path
    try:
        path.relative_to(ITEM_SEED_ROOT.resolve())
    except ValueError:
        return path
    raise OutDirRefused(
        f"{path} is inside {ITEM_SEED_ROOT} — every items metric globs that tree recursively, so a "
        f"sample written there changes finding counts other streams baseline against. Write "
        f"outside it, or pass --allow-production-tree if this really is the production run.")


# --------------------------------------------------------------------------------------------
# Resolving what the model named back onto the vocabulary it was offered.
# --------------------------------------------------------------------------------------------

def resolve_pick(picks: "tuple[FamilyPick, ...]", token: str) -> "FamilyPick | None":
    """Map one family string from an answer onto the pick it names.

    ⚠ **Two spellings are accepted because the brief and the schema disagree about which one to
    ask for, and that disagreement is module 13's, not this module's.** `brief._pick_lines` prints
    `pick_id` (`atom.searing-strike.fire`); `schema.set_schema` describes the same field as *"a
    stat.modify / stat.derived family id"* (`atom.searing-strike`). Accepting both here keeps a
    well-meant answer out of the reject pile; the disagreement itself is filed as a defect rather
    than papered over.
    """
    for pick in picks:
        if pick.pick_id == token:
            return pick
    for pick in picks:
        if pick.family == token and pick.variant is None:
            return pick
    return None


def resolve_capability(vocabulary: Vocabulary, node: "dict[str, Any]") -> "FamilyPick | None":
    """`{"family": …, "variant": …}` -> the capability pick, or `None` if it names nothing."""
    family = node.get("family")
    if not isinstance(family, str):
        return None
    variant = node.get("variant")
    if isinstance(variant, str) and variant:
        for pick in vocabulary.capability:
            if pick.family == family and pick.variant == variant:
                return pick
        return None
    return resolve_pick(vocabulary.capability, family)


# --------------------------------------------------------------------------------------------
# Ids.
# --------------------------------------------------------------------------------------------

def axis_group_map(path: "Path | None" = None) -> "dict[str, str]":
    """`axis -> axisGroupId`, read fresh from `naming.v1.json` rather than transcribed.

    The 5-into-3 fold (`offense`/`control` -> `off-ctrl`, `survivability`/`utility` -> `surv-util`,
    `economy` -> `econ`) is the registry's own decision and is exactly the kind of fact
    `registries.py`'s header rule says to read, never copy.
    """
    doc = json.loads((path or NAMING_REGISTRY).read_text(encoding="utf-8"))
    groups = doc["idNamespaces"]["charms"]["axisGroups"]
    return {axis: row["axisGroupId"] for row in groups for axis in row["axes"]}


def next_charm_seq(axis_group_id: str, *, corpus_root: "Path | None" = None) -> int:
    """The first sequence number free in this axis-group partition, measured off the live corpus.

    Ids are never reused (`naming.v1.json`'s own rule), so a generated charm continues the shipped
    partition rather than restarting it. Read, not assumed: the shipped counts move.
    """
    root = corpus_root or (ITEM_SEED_ROOT / "charms")
    pattern = re.compile(rf"^charm\.{re.escape(axis_group_id)}-(\d{{3}})$")
    highest = 0
    if root.exists():
        for file in sorted(root.glob("*.json")):
            doc = json.loads(file.read_text(encoding="utf-8"))
            for entry in doc.get("entries") or []:
                match = pattern.match(str(entry.get("id", "")))
                if match:
                    highest = max(highest, int(match.group(1)))
    return highest + 1


# --------------------------------------------------------------------------------------------
# Rows.
# --------------------------------------------------------------------------------------------

def _atom_row(pick: FamilyPick, power_band: str, *, negative: bool = False) -> "dict[str, Any]":
    """One atom row in the corpus's own shape.

    ⚠ `params.sign = "negative"` is how the shipped corpus marks a signet's drawback
    (`charm.econ-019`, read 2026-09-06) — NOT a separate `drawback` field. Emitting the drawback as
    an ordinary fixed atom would turn a cost into a bonus, silently.
    """
    row: "dict[str, Any]" = {"family": pick.family, "powerBand": power_band}
    params: "dict[str, Any]" = {}
    if pick.variant:
        params["element"] = pick.variant
    if negative:
        params["sign"] = "negative"
    if params:
        row["params"] = params
    return row


def set_entry(*, entry_id: str, theme_key: str, draft: "dict[str, Any]", plan: SetPlan,
              ) -> "dict[str, Any]":
    """One `set` row. `kinds.py` requires `id`/`nameKey`/`name` plus `themeKey`/`members`/
    `thresholds`; everything else here is in that kind's optional set."""
    thresholds: "list[dict[str, Any]]" = []
    for threshold in plan.thresholds:
        row: "dict[str, Any]" = {"pieces": threshold.pieces}
        if threshold.capability is not None:
            row["capability"] = _atom_row(threshold.capability, threshold.power_band)
        else:
            row["atoms"] = [_atom_row(p, threshold.power_band) for p in threshold.stats]
        thresholds.append(row)

    return {
        "id": entry_id,
        "nameKey": draft["nameKey"],
        "name": draft["name"],
        "themeKey": theme_key,
        "members": [{"role": m["role"], "frame": m["frame"]} for m in draft.get("members") or ()],
        "thresholds": thresholds,
        "flavor": draft.get("flavor", ""),
        "tags": [],
        "notes": ("Generated from an authored answer against promptVersion set-charm-gen/1. "
                  "Member baseType binding is NOT resolved here — module 13 builds no base-type "
                  "chooser, and inventing one would be deterministic code writing identity."),
    }


def charm_entry(*, entry_id: str, theme_key: str, draft: "dict[str, Any]", plan: CharmPlan,
                ) -> "dict[str, Any]":
    """One `charm` row. `apCost`, `prefixRolls` and `suffixRolls` come from the CLASS, never from
    the answer — the model chose `charmClass` and the tuning file prices it (ssot-charms §3.4)."""
    fixed = [_atom_row(pick, pick.power_band or "low") for pick in plan.families]
    row: "dict[str, Any]" = {
        "id": entry_id,
        "nameKey": draft["nameKey"],
        "name": draft["name"],
        "charmClass": plan.charm_class,
        "apCost": plan.ap_cost,
        "axis": plan.axis,
        "frameHint": draft.get("frameHint", "any"),
        "fixedAtoms": fixed,
        "prefixRolls": plan.prefix_rolls,
        "suffixRolls": plan.suffix_rolls,
        "tags": [],
        "flavor": draft.get("flavor", ""),
        "notes": f"themeKey: {theme_key}",
    }
    if plan.unique_carry:
        row["uniqueCarry"] = True
    if plan.drawback is not None:
        row["fixedAtoms"] = fixed + [
            _atom_row(plan.drawback, plan.drawback.power_band or "low", negative=True)]
    return row


# --------------------------------------------------------------------------------------------
# Files.
# --------------------------------------------------------------------------------------------

@dataclass(frozen=True)
class SeedFileMeta:
    """The `_meta` block every shipped seed file carries. The clock is injected for
    `provenance.py`'s stated reason — a timestamp is the one field that would otherwise make a
    generated file non-reproducible."""

    batch: str
    partition: str
    prompt_version: str
    model: str
    authored_utc: str
    source_ref: str
    registry_versions: "dict[str, int]"

    def to_dict(self) -> "dict[str, Any]":
        return {
            "batch": self.batch,
            "partition": self.partition,
            "contractVersion": 1,
            "registryVersions": dict(self.registry_versions),
            "exemplarVersion": 1,
            "promptVersion": self.prompt_version,
            "model": self.model,
            "authoredUtc": self.authored_utc,
            "sourceRef": self.source_ref,
        }


def seed_document(kind: str, entries: "list[dict[str, Any]]", meta: SeedFileMeta) -> "dict[str, Any]":
    return {"schemaVersion": 1, "kind": kind, "_meta": meta.to_dict(), "entries": entries}


def write_seed_file(out_dir: Path, filename: str, document: "dict[str, Any]") -> Path:
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / filename
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path
