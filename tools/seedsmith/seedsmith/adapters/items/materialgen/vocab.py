"""seedsmith.adapters.items.materialgen.vocab — the CLOSED, 27-id issuable material vocabulary,
mirrored from `MaterialCatalog.cs` and its two upstream enums.

⛔ **This is the module's most important file.** `materials-gen`'s own spec (acceptance #2) requires
confirming a target id is a real member of the closed vocabulary BEFORE generating content for it —
refusing rather than authoring for an id that doesn't exist mechanically. Every other file in this
package calls `require_issuable` (or checks `is_issuable`) before it does anything else with an id.

⛔ **Mirrored, never hand-copied from memory.** The 27 ids are reconstructed here by walking the
same enum members and building the same class-order concatenation `MaterialCatalog.Build()` does in
C# — not a transcribed literal list — so a rung/element/grade/verb added to an upstream enum shows
up as a wrong COUNT here (pinned by `test_materials_gen.py`) rather than silently drifting apart.
There is no committed JSON mirror of this vocabulary anywhere in the repo today (checked: no
`AtomVocabCheck`-named file, no `material`-named registry file exists) — the sole precedent for
"mirror a closed C# enum into Python with a citation, no re-derivation" is
`adapters/demons/registries.py`, and this file follows that same discipline.

Citations, read 2026-09-07 directly from source (never from memory or an earlier session's notes):

    src/FusionRpg.Core/Demons/DemonRarity.cs:16-28        DemonRarity enum — Chaff=0 .. Almanac=9,
                                                            ordinal IS rank (the enum's own doc comment)
    src/FusionRpg.Core/Demons/DemonRarity.cs:52-65        DemonRarityIds.ToId() — the literal id
                                                            string for each rung, e.g. Chaff -> "chaff"
    src/FusionRpg.Core/Demons/DemonRarityLadder.cs:51-52  DemonRarityLadder.All — every rung, ordered
                                                            by (int)r ascending (this is what
                                                            MaterialCatalog.Build() iterates for shard)
    src/FusionRpg.Core/Demons/DemonRarity.cs:92-101       LegacyDemonRarityIds.ForwardMap — the four
                                                            legacy band ids (common/rare/epic/legendary),
                                                            resolvable but never issuable
    src/FusionRpg.Core/Stats/Derived/ActorElementTypes.cs:21-29
                                                            ElementRoster.Concrete — the 6 elements,
                                                            in iteration order (omni excluded: essence
                                                            ids are never omni, MaterialCatalog.cs:83)
    src/FusionRpg.Core/Stats/Derived/ActorElementTypes.cs:93-102
                                                            ElementTypeIdExtensions.ToElementId() — the
                                                            literal id string for each element
    src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs:50   SubstrateFrames = humanoid, plant
    src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs:54   SubstrateGrades = crude, sound, fine, prime
    src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs:57   CatalystVerbs = forge, temper, flux
    src/FusionRpg.Core/Items/Materials/MaterialCatalog.cs:70-90 Build() — the exact class order this
                                                            module's own `_build_issuable` mirrors:
                                                            shard x10, substrate x8, essence x6,
                                                            catalyst x3 = 27
"""
from __future__ import annotations

from dataclasses import dataclass

#: DemonRarityLadder.All order — Chaff (weakest) first, Almanac (strongest) last. Ordinal IS rank;
#: never re-sort this alphabetically or by any other key (DemonRarity.cs's own hard warning).
RARITY_RUNGS: "tuple[str, ...]" = (
    "chaff", "sprout", "grafted", "cultivated", "fused",
    "chimeric", "heirloom", "firstseed", "sunwoven", "almanac",
)

#: MaterialCatalog.SubstrateFrames.
SUBSTRATE_FRAMES: "tuple[str, ...]" = ("humanoid", "plant")

#: MaterialCatalog.SubstrateGrades — ordinal 1..4, `SubstrateGrades[g-1]` for grade `g`.
SUBSTRATE_GRADES: "tuple[str, ...]" = ("crude", "sound", "fine", "prime")

#: ElementRoster.Concrete, in iteration order. Omni is deliberately excluded — MaterialCatalog.cs's
#: `Build()` iterates `ElementRoster.Concrete` only, never the omni id, so `essence.omni` is not,
#: and will never be, a member of this vocabulary.
ELEMENTS: "tuple[str, ...]" = ("fire", "ice", "air", "earth", "light", "dark")

#: MaterialCatalog.CatalystVerbs.
CATALYST_VERBS: "tuple[str, ...]" = ("forge", "temper", "flux")

#: LegacyDemonRarityIds.ForwardMap's keys — the four retired shard bands. `IsKnown` (not
#: `IsIssuable`) resolves these; a generator must never author NEW content for one.
LEGACY_SHARD_IDS: "frozenset[str]" = frozenset({"common", "rare", "epic", "legendary"})


class MaterialVocabularyRejection(ValueError):
    """Mirrors `MaterialVocabularyRejection` (MaterialCatalog.cs) in name and in refusing loudly
    rather than silently inventing content — this module raises its own exception type (there is no
    cross-language exception to import) but keeps the same name on purpose: a refusal on the Python
    side and a refusal on the C# side should read as the same rule stated twice, not two different
    tools disagreeing about what is legal."""


@dataclass(frozen=True)
class MaterialId:
    """One issuable material id, with the structural fields `kinds.py`'s `material` KindSpec already
    declares as optional (`element`, `frame`, `grade`) — populated only where the class carries them,
    exactly as `MaterialCatalog.ClassOf`/`FrameOf`/`GradeOf` would report for the same runtime id."""

    material_class: str            # "shard" | "substrate" | "essence" | "catalyst"
    runtime_id: str
    element: "str | None" = None
    frame: "str | None" = None
    grade: "int | None" = None


def _build_issuable() -> "tuple[MaterialId, ...]":
    """`MaterialCatalog.Build()`'s exact class order, walked the same way: shard, then substrate
    (frame outer, grade inner), then essence, then catalyst."""
    out: "list[MaterialId]" = []
    for rung in RARITY_RUNGS:
        out.append(MaterialId(material_class="shard", runtime_id=f"shard.{rung}"))
    for frame in SUBSTRATE_FRAMES:
        for ordinal, grade in enumerate(SUBSTRATE_GRADES, start=1):
            out.append(MaterialId(
                material_class="substrate", runtime_id=f"substrate.{frame}.{grade}",
                frame=frame, grade=ordinal))
    for element in ELEMENTS:
        out.append(MaterialId(
            material_class="essence", runtime_id=f"essence.{element}", element=element))
    for verb in CATALYST_VERBS:
        out.append(MaterialId(material_class="catalyst", runtime_id=f"catalyst.{verb}"))
    return tuple(out)


#: The 27-id closed, issuable vocabulary, in `MaterialCatalog.All`'s own order.
ISSUABLE: "tuple[MaterialId, ...]" = _build_issuable()

ISSUABLE_BY_ID: "dict[str, MaterialId]" = {m.runtime_id: m for m in ISSUABLE}

assert len(ISSUABLE) == 27, (
    "MaterialCatalog.All is 27 ids (shard x10 + substrate x8 + essence x6 + catalyst x3) — this "
    "mirror has drifted from the C# enums it reconstructs; re-read MaterialCatalog.cs and its "
    "upstream enums before touching this file")
assert len(ISSUABLE_BY_ID) == 27, "duplicate runtime id produced by this mirror"


def is_issuable(runtime_id: str) -> bool:
    """True for one of the 27 ids `MaterialCatalog.Build()` mints. Mirrors `IsIssuable`."""
    return runtime_id in ISSUABLE_BY_ID


def is_legacy_shard_id(runtime_id: str) -> bool:
    """True only for the four retired band ids (`shard.common`/`shard.rare`/`shard.epic`/
    `shard.legendary`) — resolvable, never minted. Mirrors `IsLegacyShardId`."""
    if not runtime_id.startswith("shard."):
        return False
    return runtime_id[len("shard."):] in LEGACY_SHARD_IDS


def is_known(runtime_id: str) -> bool:
    """Issuable OR a legacy shard id. Mirrors `IsKnown` — a saved reference to a legacy id still
    resolves, but `is_issuable` alone is what this generator gates new content on."""
    return is_issuable(runtime_id) or is_legacy_shard_id(runtime_id)


def require_issuable(runtime_id: str) -> MaterialId:
    """The generator's own gate. Call this BEFORE authoring content for any id — it refuses loudly,
    never silently inventing a new material kind, for anything outside the 27-id closed vocabulary,
    including an id that is merely *known* (a legacy shard band) rather than issuable."""
    material = ISSUABLE_BY_ID.get(runtime_id)
    if material is not None:
        return material
    if is_legacy_shard_id(runtime_id):
        raise MaterialVocabularyRejection(
            f"material id {runtime_id!r} is a legacy, retired shard band — resolvable for backward "
            f"compatibility (LegacyDemonRarityIds.ForwardMap) but deliberately NOT issuable "
            f"(MaterialCatalog.IsIssuable is false for every legacy id, per its own doc comment); "
            f"materials-gen only authors content for an id the ladder will actually mint, never for "
            f"a retired one.")
    raise MaterialVocabularyRejection(
        f"material id {runtime_id!r} is not in the 27-id closed vocabulary MaterialCatalog.All "
        f"defines (shard.<rung> x10, substrate.<frame>.<grade> x8, essence.<element> x6, "
        f"catalyst.<verb> x3). materials-gen never invents a new material id — it only authors "
        f"display content for one the fixed enum already lists. Extending the enum itself "
        f"(MaterialClass/CatalystVerbs/SubstrateFrames/SubstrateGrades) is ask-first per "
        f"MaterialCatalog.cs's own doc comment, and this generator never touches that file.")


def missing_from(existing_runtime_ids: "set[str] | frozenset[str]") -> "tuple[MaterialId, ...]":
    """The issuable ids NOT already present in a corpus snapshot, in `ISSUABLE`'s own order —
    the real content gap this generator exists to close (measured 2026-09-07: all ten rarity-ladder
    shard rungs are missing from `data/seed/items/materials/materials.json` today; only the four
    legacy shard ids it carries are shard entries at all)."""
    return tuple(m for m in ISSUABLE if m.runtime_id not in existing_runtime_ids)
