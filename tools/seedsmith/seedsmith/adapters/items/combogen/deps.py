"""seedsmith.adapters.items.combogen.deps — acceptance 3a of `combination-write-unblock`, run
against the real corpus through the shared `pipeline.dependency_validator` (module 1,
`generator-harness`), not a second bespoke check.

⚠ **Two closures already do most of this work; this module reports them rather than replacing
them.** `run.plan_run` refuses to build a schema at all when `supply.build()` finds zero families or
`tuning.host_roles()` finds zero roles (`schema.combination_schema`'s own precheck), so a plan can
never come back naming a hostRole/family nothing satisfies — the guarantee acceptance 3a asks for is
already structural. What this module adds is the REPORTED form the spec's Commands block names
(`items validate --deps`): a count per family/role, run through `dependency_validator.validate` so
"resolves, but only barely (1 gem)" stays visible the way a bare boolean would not
(`dependency_validator.py`'s own stated reason for a count, not a bool).

`grants` is `EXTERNAL` (3b, resolved 2026-09-07): validated against the real atom-family corpus
(`items.registries.load_atom_families()`, the same loader `unique`'s own `fixedAtoms.family` uses)
identically to HARD, but never handed to `plan_backfill` — this program has no standing to author
atom-family content that does not exist yet.
"""
from __future__ import annotations

from dataclasses import dataclass

from . import supply as supply_mod
from .tuning import ComboTuning
from .. import registries
from ....pipeline.dependency_validator import (
    CATEGORICAL,
    EXTERNAL,
    ReferenceManifestEntry,
    ValidationReport,
    validate,
)

TARGET_INGREDIENTS = "sockets-gen"
TARGET_HOST_ROLE = "base-types-gen"
TARGET_GRANTS = "effect-atom/atom-family-library"

#: The manifest for one ALREADY-GENERATED combination entry (post-write shape, `emit.assemble_entry`'s
#: own field names) — used by `validate_entries` below, and by `test_combogen.py` against real
#: authored output.
ENTRY_MANIFEST: "tuple[ReferenceManifestEntry, ...]" = (
    ReferenceManifestEntry("ingredients[].family", CATEGORICAL, TARGET_INGREDIENTS),
    ReferenceManifestEntry("hostRole", CATEGORICAL, TARGET_HOST_ROLE),
    ReferenceManifestEntry("grants[]", EXTERNAL, TARGET_GRANTS),
)


@dataclass(frozen=True)
class DepsReport:
    """Acceptance 3a's own pre-flight, over the whole corpus a run COULD draw from — before any
    subject is planned, matching the acceptance criterion's own "before any real generation"
    ordering."""

    ingredient_families_checked: int
    host_roles_checked: int
    ingredient_gem_counts: "dict[str, int]"
    result: ValidationReport

    @property
    def refused(self) -> bool:
        """Mirrors `schema.combination_schema`'s own refusal condition exactly — this is the
        REPORTED form of the same test, not a different one."""
        return self.ingredient_families_checked == 0 or self.host_roles_checked == 0

    def to_dict(self) -> dict:
        reasons: "list[str]" = []
        if self.ingredient_families_checked == 0:
            reasons.append(
                "no ingredient family is supplied by any live gem — run the gem-supply precheck "
                "first (supply.SupplyRefused is what a real run would raise here)")
        if self.host_roles_checked == 0:
            reasons.append(
                "no host role's socket ceiling reaches the ingredient count — no base type could "
                "ever host a combination")
        return {
            "ingredientFamiliesChecked": self.ingredient_families_checked,
            "hostRolesChecked": self.host_roles_checked,
            "ingredientGemCounts": self.ingredient_gem_counts,
            "refused": self.refused,
            "reasons": reasons,
            "detail": self.result.to_dict(),
        }


def preflight(tuning: ComboTuning, *, supply: "supply_mod.SupplyReport | None" = None) -> DepsReport:
    """Run before `run.plan_run` — over every family/role a run COULD request, not over one already
    planned subject. `resolve_categorical` dispatches on `target_module` because the same callback
    serves two different universes (`dependency_validator.validate`'s own contract: one caller-
    supplied resolver per kind, not per manifest entry)."""
    report = supply or supply_mod.build()
    host_roles = tuning.host_roles()
    gem_counts = {family: len(report.bands.get(family, ())) for family in report.families}

    def resolve_categorical(target_module: str, value: object) -> int:
        if target_module == TARGET_INGREDIENTS:
            return gem_counts.get(str(value), 0)
        if target_module == TARGET_HOST_ROLE:
            return 1 if value in host_roles else 0
        return 0

    def resolve_hard(_target_module: str, _value: object) -> bool:
        return True  # never reached — this preflight carries no HARD/EXTERNAL manifest entry

    entries: "dict[str, dict]" = {f"family:{f}": {"ingredients": [f]} for f in report.families}
    entries.update({f"role:{r}": {"hostRole": r} for r in host_roles})
    manifest = (
        ReferenceManifestEntry("ingredients[]", CATEGORICAL, TARGET_INGREDIENTS),
        ReferenceManifestEntry("hostRole", CATEGORICAL, TARGET_HOST_ROLE),
    )
    result = validate(entries, list(manifest), resolve_hard, resolve_categorical)
    return DepsReport(
        ingredient_families_checked=report.family_count,
        host_roles_checked=len(host_roles),
        ingredient_gem_counts=gem_counts,
        result=result,
    )


def validate_entries(entries: "dict[str, dict]", *,
                     supply: "supply_mod.SupplyReport | None" = None,
                     host_roles: "tuple[str, ...] | None" = None,
                     atom_families: "frozenset[str] | None" = None) -> ValidationReport:
    """The per-entry form, run over REAL assembled combinations (`emit.assemble_entry`'s own shape)
    once content exists — `ingredients[].family` and `hostRole` CATEGORICAL against the same live
    corpora `preflight` measured, `grants[]` EXTERNAL against the real atom-family library. A
    combination whose `hostRole`/family/grant nothing satisfies is a real defect this reports on
    the finished entry, distinct from `preflight`'s pre-generation universe check."""
    report = supply or supply_mod.build()
    roles = host_roles if host_roles is not None else set()
    families = atom_families if atom_families is not None else registries.load_atom_families()
    gem_counts = {family: len(report.bands.get(family, ())) for family in report.families}

    def resolve_categorical(target_module: str, value: object) -> int:
        if target_module == TARGET_INGREDIENTS:
            return gem_counts.get(str(value), 0)
        if target_module == TARGET_HOST_ROLE:
            return 1 if value in roles else 0
        return 0

    def resolve_hard(target_module: str, value: object) -> bool:
        if target_module == TARGET_GRANTS:
            return value in families
        return False

    return validate(entries, list(ENTRY_MANIFEST), resolve_hard, resolve_categorical)
