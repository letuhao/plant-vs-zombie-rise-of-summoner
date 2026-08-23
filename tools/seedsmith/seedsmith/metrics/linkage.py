"""seedsmith.metrics.linkage — Linkage + Registration families, absorbed from
`tools/seed_graph/seedgraph/checks.py` (S3, tasks/seedsmith-todo.md).

Each check answers a question a referential validator cannot ask, because each depends on the
*absence* of a row rather than on a row pointing somewhere wrong — absence is invisible to a
reference check by construction: there is nothing to look at. `tools/ItemSeedValidator` stays the
referential gate; this module never re-checks that a reference resolves, only that something
reachable exists to resolve it TO.

Seven check functions, ported near-verbatim (the only change is `corpus.of(kind)` →
`corpus.by_kind(kind)`, seedsmith's own name for the same lookup) — a fresh count against the
live source found **ten** distinct finding codes, not the nine spec-metrics.md quotes from an
earlier, already-acknowledged-fuzzy count ("the two counts get confused easily"). Corrected here
rather than propagated a second time.

`Acquisition` is items-domain knowledge (drop tables, recipes) so these metrics import it
directly from `adapters.items` — they are inherently item-specific, exactly like the tool they
were ported from, not generic checks that happen to need an adapter.
"""
from __future__ import annotations

from ..adapters.items.acquisition import Acquisition
from .model import Ctx, Finding, Loop, Metric, Severity

# core.v1.json roles.list, the two rows with hybridEligible: false. Hard-coded rather than read,
# matching the ported source's own reasoning: this must keep working on synthetic fixtures that
# ship no registry. Both are stable ids in an append-only registry.
NON_HYBRID_ROLES = frozenset({"ward-array", "jewel-minor-b"})

# Kinds a player is meant to end up holding. Deliberately excludes affix-family / display-template
# / curve (machinery, never held), set (completed, not acquired — checked separately),
# socket-word (produced by combining gems, not dropped), enhancement-milestone (granted by an
# item's +X track, not dropped).
ACQUIRABLE_KINDS = ("base-type", "unique", "charm", "consumable", "gem", "material")


def _acquisition(ctx: Ctx) -> Acquisition:
    return Acquisition.build(ctx.corpus)


class SetCompletability(Metric):
    """A set bonus nobody can ever earn — the check the whole tool was originally written for.

    A member declared by role alone names no specific base type, so no item is ever a member, no
    threshold ever counts, and every bonus on the set is unreachable. Invisible to a reference
    validator because there is no bad reference — there is no reference at all.
    """

    id = "Linkage/SetCompletability"
    family = "Linkage"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        findings: list[Finding] = []
        for entry in ctx.corpus.by_kind("set"):
            members = entry.get("members") or []
            pinned = [m for m in members if isinstance(m, dict)
                     and (m.get("baseType") or m.get("containerId") or m.get("container_id"))]
            thresholds = [t.get("pieces") for t in (entry.get("thresholds") or [])
                         if isinstance(t, dict) and isinstance(t.get("pieces"), int)]
            top = max(thresholds) if thresholds else 0

            if members and not pinned:
                findings.append(Finding(
                    metric=self.id, severity=Severity.GAP, subject=entry.id,
                    message=f"'{entry.name}' declares {len(members)} members by role only; none "
                            f"names a base type, so no item is ever a member and all "
                            f"{len(thresholds)} thresholds (top: {top} pieces) are unreachable",
                    evidence={"code": "SetUncompletable", "partition": entry.partition,
                             "memberCount": len(members), "pinnedCount": 0}))
            elif pinned and top > len(pinned):
                findings.append(Finding(
                    metric=self.id, severity=Severity.GAP, subject=entry.id,
                    message=f"'{entry.name}' has {len(pinned)} pinned members but its top "
                            f"threshold needs {top}; the last bonus can never be earned",
                    evidence={"code": "SetShortOfThreshold", "partition": entry.partition,
                             "pinnedCount": len(pinned), "topThreshold": top}))

            for member in members:
                if not isinstance(member, dict):
                    continue
                role = member.get("role")
                if role and role in NON_HYBRID_ROLES:
                    findings.append(Finding(
                        metric=self.id, severity=Severity.GAP, subject=entry.id,
                        message=f"'{entry.name}' claims role '{role}', which is not in the "
                                f"hybrid role core; a hybrid frame could never complete this set",
                        evidence={"code": "SetRoleNotHybridCore", "partition": entry.partition,
                                 "role": role}))
                if not member.get("frame"):
                    findings.append(Finding(
                        metric=self.id, severity=Severity.NOTE, subject=entry.id,
                        message=f"'{entry.name}' member in role '{member.get('role')}' declares "
                                f"no frame; item_set_member.frame is NOT NULL and is validated "
                                f"against the base type",
                        evidence={"code": "SetMemberFrameless", "partition": entry.partition}))
                    break
        return findings


class Unobtainable(Metric):
    """Content with no acquisition path at all — reported per kind, not per row: a wall of
    identical findings is not more actionable, and the decision an owner makes is per kind."""

    id = "Registration/Unobtainable"
    family = "Registration"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        acquisition = _acquisition(ctx)
        findings = []
        for kind in ACQUIRABLE_KINDS:
            entries = ctx.corpus.by_kind(kind)
            if not entries:
                continue
            missing = [e for e in entries if not acquisition.reaches(e)]
            if not missing:
                continue
            sample = ", ".join(e.id for e in missing[:3])
            more = f", +{len(missing) - 3} more" if len(missing) > 3 else ""
            severity = Severity.GAP if len(missing) == len(entries) else Severity.NOTE
            findings.append(Finding(
                metric=self.id, severity=severity, subject=kind,
                message=f"{len(missing)} of {len(entries)} '{kind}' entries have no acquisition "
                        f"path — no drop table yields them, no recipe outputs them "
                        f"({sample}{more})",
                evidence={"code": "Unobtainable", "missingCount": len(missing),
                         "totalCount": len(entries)}))
        return findings


class SocketWordIngredients(Metric):
    """A socket word whose named ingredient family no gem in the corpus supplies."""

    id = "Registration/IngredientUnsatisfiable"
    family = "Registration"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        supply: dict[str, set[str]] = {}
        for gem in ctx.corpus.by_kind("gem"):
            family = gem.get("family")
            if family:
                supply.setdefault(family, set()).add(gem.get("powerBand") or "?")

        findings = []
        for word in ctx.corpus.by_kind("socket-word"):
            for ingredient in word.get("ingredients") or []:
                family = ingredient.get("family")
                if family and family not in supply:
                    findings.append(Finding(
                        metric=self.id, severity=Severity.GAP, subject=word.id,
                        message=f"'{word.name}' needs a gem carrying '{family}' at position "
                                f"{ingredient.get('position')}; no gem in the corpus supplies "
                                f"that family",
                        evidence={"code": "IngredientUnsatisfiable", "partition": word.partition,
                                 "family": family}))
        return findings


class RecipeInputs(Metric):
    """A recipe that spends a material a player cannot obtain."""

    id = "Registration/RecipeInputUnobtainable"
    family = "Registration"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        acquisition = _acquisition(ctx)
        obtainable = set(acquisition.material_runtime_ids)
        for recipe in ctx.corpus.by_kind("recipe"):
            if recipe.get("outputKind") == "material" and recipe.get("outputRef"):
                obtainable.add(recipe.get("outputRef"))

        findings = []
        for recipe in ctx.corpus.by_kind("recipe"):
            for line in recipe.get("costLines") or []:
                material = line.get("material")
                if material and material not in obtainable:
                    findings.append(Finding(
                        metric=self.id, severity=Severity.GAP, subject=recipe.id,
                        message=f"'{recipe.name}' spends '{material}', which no drop table "
                                f"yields and no recipe produces",
                        evidence={"code": "RecipeInputUnobtainable",
                                 "partition": recipe.partition, "material": material}))
        return findings


class EnhancementTrackBound(Metric):
    """Milestone families nothing ever grants — the entire +X reward line unreachable if no
    base type carries an enhance track."""

    id = "Registration/FeatureUnbound"
    family = "Registration"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        milestones = ctx.corpus.by_kind("enhancement-milestone")
        if not milestones:
            return []
        base_types = ctx.corpus.by_kind("base-type")
        tracked = [b for b in base_types if b.get("enhanceTrack") or b.get("item_enhance_track")]
        if tracked:
            return []
        return [Finding(
            metric=self.id, severity=Severity.GAP, subject="enhancement-milestone",
            message=f"{len(milestones)} milestone families exist and not one of "
                    f"{len(base_types)} base types carries an enhance track; the entire +X "
                    f"reward line is unreachable",
            evidence={"code": "FeatureUnbound", "milestoneCount": len(milestones),
                     "baseTypeCount": len(base_types)})]


class EquipmentSlotCoverage(Metric):
    """A role/frame pair with base types but no drop table granting them."""

    id = "Registration/SlotUncovered"
    family = "Registration"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        acquisition = _acquisition(ctx)
        have: set[tuple[str, str]] = set()
        for base in ctx.corpus.by_kind("base-type"):
            role, frame = base.get("role"), base.get("frame")
            if role and frame:
                have.add((role, frame))

        missing = sorted(have - acquisition.equipment_slots)
        if not missing:
            return []
        shown = ", ".join(f"{r}/{f}" for r, f in missing[:6])
        more = f", +{len(missing) - 6} more" if len(missing) > 6 else ""
        return [Finding(
            metric=self.id, severity=Severity.GAP, subject="drop-tables",
            message=f"{len(missing)} of {len(have)} role/frame slots have base types but no "
                    f"drop table grants equipment in them ({shown}{more})",
            evidence={"code": "SlotUncovered", "missingCount": len(missing),
                     "totalSlots": len(have)})]


class DeadEndMaterials(Metric):
    """A material that drops and that nothing anywhere consumes — NOTE, not GAP: pointless is
    not unreachable, and "pointless for now" is a legitimate state mid-build."""

    id = "Registration/MaterialNeverSpent"
    family = "Registration"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"corpus"})
    covers: tuple[str, ...] = ()

    def run(self, ctx: Ctx) -> list[Finding]:
        spent = {line.get("material")
                for recipe in ctx.corpus.by_kind("recipe")
                for line in recipe.get("costLines") or []}
        materials = ctx.corpus.by_kind("material")
        idle = [m for m in materials if (m.get("runtimeId") or m.id) not in spent]
        if not idle:
            return []
        shown = ", ".join(m.get("runtimeId") or m.id for m in idle[:6])
        return [Finding(
            metric=self.id, severity=Severity.NOTE, subject="materials",
            message=f"{len(idle)} of {len(materials)} materials are consumed by no recipe "
                    f"({shown}) — a drop with no sink",
            evidence={"code": "MaterialNeverSpent", "idleCount": len(idle),
                     "totalCount": len(materials)})]


ALL_LINKAGE_METRICS: "tuple[type[Metric], ...]" = (
    SetCompletability, Unobtainable, SocketWordIngredients, RecipeInputs,
    EnhancementTrackBound, EquipmentSlotCoverage, DeadEndMaterials,
)
