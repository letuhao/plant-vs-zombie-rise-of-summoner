"""The reachability checks.

Each one answers a question the referential validator cannot ask, because each depends on the
*absence* of a row rather than on a row pointing somewhere wrong. Absence is invisible to a
reference check by construction: there is nothing to look at.

Severity is deliberately two-valued. GAP means content that ships and cannot be reached or
finished — dead weight in the build, and the reason this tool exists. NOTE means a shape worth a
human glance that may well be intentional.
"""

from __future__ import annotations

from dataclasses import dataclass

from .corpus import Acquisition, Corpus, Entry

GAP = "GAP"
NOTE = "NOTE"

# Kinds a player is meant to end up holding. Deliberately does NOT include:
#   affix-family / display-template / curve  — machinery, never held
#   set                                      — completed, not acquired; checked separately
#   socket-word                              — produced by combining gems, not dropped
#   enhancement-milestone                    — granted by an item's +X track, not dropped
ACQUIRABLE_KINDS = ("base-type", "unique", "charm", "consumable", "gem", "material")

# core.v1.json roles.list, the two rows with hybridEligible: false. Hard-coded rather than read,
# because this check must keep working on the synthetic corpora the tests build, which ship no
# registry. Both names are stable ids in an append-only registry.
NON_HYBRID_ROLES = frozenset({"ward-array", "jewel-minor-b"})


@dataclass(frozen=True)
class Finding:
    severity: str
    code: str
    subject: str
    partition: str
    message: str


def run_all(corpus: Corpus, acquisition: Acquisition) -> list[Finding]:
    findings: list[Finding] = []
    for check in (
        set_completability,
        unobtainable_content,
        socket_word_ingredients,
        recipe_inputs,
        enhancement_track_bound,
        equipment_slot_coverage,
        dead_end_materials,
    ):
        findings.extend(check(corpus, acquisition))
    return findings


def set_completability(corpus: Corpus, _: Acquisition) -> list[Finding]:
    """A set bonus nobody can ever earn.

    ssot-sets.md §3.1 is explicit that "membership is declared, not inferred", and §4's
    `item_set_member` is keyed `(set_id, container_id)` — a specific base type, plus its role and
    frame. A member that names only a role declares nothing: it does not say which of the ~50 base
    types in that role is the piece, so no item is ever a member, no threshold ever counts, and
    every bonus on the set is unreachable.

    This is the check the whole tool was written for, and it is invisible to a reference validator
    because there is no bad reference — there is no reference at all.
    """
    findings = []
    for entry in corpus.of("set"):
        members = entry.get("members") or []
        pinned = [m for m in members if isinstance(m, dict)
                  and (m.get("baseType") or m.get("containerId") or m.get("container_id"))]
        thresholds = [t.get("pieces") for t in (entry.get("thresholds") or [])
                      if isinstance(t, dict) and isinstance(t.get("pieces"), int)]
        top = max(thresholds) if thresholds else 0

        if members and not pinned:
            findings.append(Finding(
                GAP, "SetUncompletable", entry.id, entry.partition,
                f"'{entry.name}' declares {len(members)} members by role only; none names a base "
                f"type, so no item is ever a member and all {len(thresholds)} thresholds "
                f"(top: {top} pieces) are unreachable"))
        elif pinned and top > len(pinned):
            findings.append(Finding(
                GAP, "SetShortOfThreshold", entry.id, entry.partition,
                f"'{entry.name}' has {len(pinned)} pinned members but its top threshold needs "
                f"{top}; the last bonus can never be earned"))

        for member in members:
            if not isinstance(member, dict):
                continue
            role = member.get("role")
            # ssot-sets.md §3.7: "A set's member roles must all be in the hybrid role core" — the
            # roles that exist on every frame, so a hybrid can complete any set. `ward-array` and
            # `jewel-minor-b` are the two that are not. Nothing was enforcing this; all 128 members
            # happen to comply, and a rule that holds by luck is one authoring wave from not.
            if role and role in NON_HYBRID_ROLES:
                findings.append(Finding(
                    GAP, "SetRoleNotHybridCore", entry.id, entry.partition,
                    f"'{entry.name}' claims role '{role}', which is not in the hybrid role core; "
                    f"a hybrid frame could never complete this set"))
            if not member.get("frame"):
                findings.append(Finding(
                    NOTE, "SetMemberFrameless", entry.id, entry.partition,
                    f"'{entry.name}' member in role '{member.get('role')}' declares no frame; "
                    f"item_set_member.frame is NOT NULL and is validated against the base type"))
                break
    return findings


def unobtainable_content(corpus: Corpus, acquisition: Acquisition) -> list[Finding]:
    """Content with no acquisition path at all.

    Reported one line per kind rather than per entry: 144 identical findings is a wall, and the
    decision an owner makes is per kind anyway ("how do uniques drop?"), never per row.
    """
    findings = []
    for kind in ACQUIRABLE_KINDS:
        entries = corpus.of(kind)
        if not entries:
            continue
        missing = [e for e in entries if not acquisition.reaches(e)]
        if not missing:
            continue
        sample = ", ".join(e.id for e in missing[:3])
        more = f", +{len(missing) - 3} more" if len(missing) > 3 else ""
        severity = GAP if len(missing) == len(entries) else NOTE
        findings.append(Finding(
            severity, "Unobtainable", kind, "(cross-partition)",
            f"{len(missing)} of {len(entries)} '{kind}' entries have no acquisition path — no drop "
            f"table yields them, no recipe outputs them ({sample}{more})"))
    return findings


def socket_word_ingredients(corpus: Corpus, _: Acquisition) -> list[Finding]:
    """A recipe for a socket word whose ingredients do not exist.

    A word names its ingredients by atom family and a minimum power band. If no gem carries that
    family, the word can never be assembled no matter how many gems a player owns.
    """
    supply: dict[str, set[str]] = {}
    for gem in corpus.of("gem"):
        family = gem.get("family")
        if family:
            supply.setdefault(family, set()).add(gem.get("powerBand") or "?")

    findings = []
    for word in corpus.of("socket-word"):
        for ingredient in word.get("ingredients") or []:
            family = ingredient.get("family")
            if family and family not in supply:
                findings.append(Finding(
                    GAP, "IngredientUnsatisfiable", word.id, word.partition,
                    f"'{word.name}' needs a gem carrying '{family}' at position "
                    f"{ingredient.get('position')}; no gem in the corpus supplies that family"))
    return findings


def recipe_inputs(corpus: Corpus, acquisition: Acquisition) -> list[Finding]:
    """A recipe that spends something a player cannot obtain."""
    obtainable = set(acquisition.material_runtime_ids)
    for recipe in corpus.of("recipe"):
        if recipe.get("outputKind") == "material" and recipe.get("outputRef"):
            obtainable.add(recipe.get("outputRef"))

    findings = []
    for recipe in corpus.of("recipe"):
        for line in recipe.get("costLines") or []:
            material = line.get("material")
            if material and material not in obtainable:
                findings.append(Finding(
                    GAP, "RecipeInputUnobtainable", recipe.id, recipe.partition,
                    f"'{recipe.name}' spends '{material}', which no drop table yields and no "
                    f"recipe produces"))
    return findings


def enhancement_track_bound(corpus: Corpus, _: Acquisition) -> list[Finding]:
    """Milestone families nothing ever grants.

    entry-shapes.md §6 authors the family here and puts *which base type grants it at which of
    +4/+8/+12/+16/+20* on the base-type kind, as `item_enhance_track`. If no base type carries a
    track, every milestone is a family the game can describe and never award.
    """
    milestones = corpus.of("enhancement-milestone")
    if not milestones:
        return []
    tracked = [b for b in corpus.of("base-type")
               if b.get("enhanceTrack") or b.get("item_enhance_track")]
    if tracked:
        return []
    return [Finding(
        GAP, "FeatureUnbound", "enhancement-milestone", "(cross-partition)",
        f"{len(milestones)} milestone families exist and not one of {len(corpus.of('base-type'))} "
        f"base types carries an enhance track; the entire +X reward line is unreachable")]


def equipment_slot_coverage(corpus: Corpus, acquisition: Acquisition) -> list[Finding]:
    """A role and frame that has base types but no drop table granting them."""
    have: set[tuple[str, str]] = set()
    for base in corpus.of("base-type"):
        role, frame = base.get("role"), base.get("frame")
        if role and frame:
            have.add((role, frame))

    missing = sorted(have - acquisition.equipment_slots)
    if not missing:
        return []
    shown = ", ".join(f"{r}/{f}" for r, f in missing[:6])
    more = f", +{len(missing) - 6} more" if len(missing) > 6 else ""
    return [Finding(
        GAP, "SlotUncovered", "drop-tables", "(cross-partition)",
        f"{len(missing)} of {len(have)} role/frame slots have base types but no drop table "
        f"grants equipment in them ({shown}{more})")]


def dead_end_materials(corpus: Corpus, acquisition: Acquisition) -> list[Finding]:
    """A material that drops and that nothing anywhere consumes.

    NOTE rather than GAP: a material with no sink is not unreachable, it is merely pointless, and
    "pointless for now" is a legitimate state for a crafting economy mid-build.
    """
    spent = {line.get("material")
             for recipe in corpus.of("recipe")
             for line in recipe.get("costLines") or []}
    idle = [m for m in corpus.of("material")
            if (m.get("runtimeId") or m.id) not in spent]
    if not idle:
        return []
    shown = ", ".join(m.get("runtimeId") or m.id for m in idle[:6])
    return [Finding(
        NOTE, "MaterialNeverSpent", "materials", "(cross-partition)",
        f"{len(idle)} of {len(corpus.of('material'))} materials are consumed by no recipe "
        f"({shown}) — a drop with no sink")]
