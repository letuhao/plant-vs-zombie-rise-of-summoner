"""seedsmith.adapters.items.kinds — the 15 item KindSpecs, ported from
`tools/ItemSeedValidator/Registries/KindCatalog.cs` (read fresh 2026-08-23, not from memory —
the corpus has changed under this program before).

Fifteen, not fourteen: `KindCatalog.cs` also carries `attribute` (`ShapeDefined: false` — its
shape has never been authored against, seed-contract.md has no §10 table for it), which the
corpus stats table quotes as "14 shipped kinds" because `attribute` ships zero rows. It is still
a real, allocated kind — one of the nine currently-empty partitions this program's Coverage
metric exists to catch — so it belongs in this list.

`required`/`optional` are transcribed once, directly from the C# source, because there is no JSON
registry that states them — unlike almost everything else an adapter reads, this is code, not
data, and the citation above is what keeps the port auditable against drift.

`id_pattern` and `runtime_id_fields` are left unset for every kind: encoding them precisely needs
each kind's `idTemplate`/minted-runtime-family rules from `naming.v1.json`, which is real work
with its own failure modes (this is exactly the class of thing that produced the tracking-id vs
runtime-id defects spec-foundation §1 describes) and no S2 acceptance criterion exercises it yet.
Left as an explicit, documented gap for whichever later task (S3's Linkage/Registration
absorption is the first candidate) actually needs it, rather than guessed at here.
"""
from __future__ import annotations

from ..base import KindSpec

COMMON_FIELDS = frozenset({
    "id", "nameKey", "name", "tags", "notes", "enabled", "overrides",
    "flavor", "flavorKey", "iconKey", "unlockGate",
})
COMMON_REQUIRED = frozenset({"id", "nameKey", "name"})


def _defined(kind: str, directory: str, namespace: str, *, required: "set[str]",
            extra: "set[str]") -> KindSpec:
    return KindSpec(
        kind=kind, directory=directory, namespace=namespace,
        required=COMMON_REQUIRED | required,
        optional=COMMON_FIELDS | extra,
    )


def _undefined(kind: str, directory: str, namespace: str) -> KindSpec:
    return KindSpec(kind=kind, directory=directory, namespace=namespace,
                    required=COMMON_REQUIRED, optional=COMMON_FIELDS)


KINDS: "tuple[KindSpec, ...]" = (
    _defined("base-type", "base-types", "baseTypes",
            required={"frame", "role", "class", "band", "iconKey", "tags"},
            extra={"frame", "role", "class", "band", "implicit", "socketMax", "enhanceTrack"}),
    _defined("affix-family", "affix-families", "affixFamilies",
            required={"kindId", "powerBand", "tags"},
            extra={"kindId", "params", "variants", "frames", "side", "roles", "roleGroups",
                   "powerBand", "nameWords", "displayTemplate", "channel"}),
    _defined("unique", "uniques", "uniques",
            required={"frame", "baseType", "rarity", "fixedAtoms", "counterPressure", "tags",
                     "powerAxis"},
            extra={"frame", "baseType", "rarity", "fixedAtoms", "varianceSlot",
                   "counterPressure", "theme", "themeKey", "acquisition", "powerAxis"}),
    _defined("set", "sets", "sets",
            required={"themeKey", "members", "thresholds"},
            extra={"themeKey", "theme", "members", "thresholds"}),
    _defined("gem", "gems", "gems",
            required={"family", "powerBand"},
            extra={"family", "element", "powerBand", "affinityElement"}),
    _defined("material", "materials", "materials",
            required={"runtimeId", "materialClass"},
            extra={"runtimeId", "materialClass", "element", "frame", "grade"}),
    _defined("curve", "curves", "curves",
            required={"input", "points"}, extra={"input", "points"}),
    _undefined("attribute", "attributes", "attributes"),
    _defined("charm", "charms", "charms",
            required={"charmClass", "apCost", "axis", "frameHint", "fixedAtoms"},
            extra={"charmClass", "apCost", "axis", "frameHint", "uniqueCarry", "fixedAtoms",
                   "roleGroups", "poolRolls"}),
    _defined("socket-word", "socket-words", "socketWords",
            required={"runtimeId", "minSockets", "ingredients", "fixedAtoms"},
            extra={"runtimeId", "hostRole", "hostFrame", "minSockets", "ingredients",
                   "fixedAtoms"}),
    _defined("recipe", "recipes", "recipes",
            required={"operation", "outputKind", "frame", "costLines"},
            extra={"operation", "outputKind", "outputRef", "outputQty", "frame", "costLines",
                   "soulsCostBand"}),
    _defined("enhancement-milestone", "enhancement-milestones", "enhancementMilestones",
            required={"runtimeFamily", "kindId", "params", "powerBand"},
            extra={"runtimeFamily", "kindId", "params", "powerBand"}),
    _defined("consumable", "consumables", "consumables",
            required={"classId", "useContext", "family", "powerBand"},
            extra={"classId", "useContext", "family", "element", "powerBand", "manifestCost",
                   "grantsActionId", "cooldownKey"}),
    _defined("drop-table", "drop-tables", "dropTables",
            required={"sourceAllow", "groups"}, extra={"sourceAllow", "groups"}),
    _defined("display-template", "display-templates", "displayTemplates",
            required={"runtimeFamily", "groupId", "status"},
            extra={"runtimeFamily", "plantOverrideKey", "plantOverrideName", "groupId",
                   "status"}),
)

assert len(KINDS) == 15, "KindCatalog.cs carries 15 kinds (14 Defined + 1 Undefined: attribute)"
assert len({k.kind for k in KINDS}) == 15, "duplicate kind id in this port"
